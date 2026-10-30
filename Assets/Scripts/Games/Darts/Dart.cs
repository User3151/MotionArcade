using MotionArcade.Core;
using MotionArcade.Feedback;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace MotionArcade.Games.Darts
{
    // חץ. הזריקה עצמה ב-ControllerThrow; כאן:
    // - ביד, החץ הולך עם מיקום היד אבל לא עם הסיבוב שלה (XRGrabInteractable.trackRotation כבוי): הוא תמיד מאוזן
    //   ומצביע על המטרה הנוכחית. שלושה ניסיונות לכוון זווית אחיזה קבועה (22°, 45°, ואחיזה שלומדת מעצירת הכיוון)
    //   לא נתנו לגיא חץ מאוזן מול הלוח: הזווית שבה כל אחד מחזיק את הבקר כשהוא מכוון שונה, ועוברת 65°-72°.
    // - בזמן טיסה החוד פונה לכיוון התעופה: כמו הנוצות של חץ אמיתי, החץ מסתובב אליו בהדרגה (alignSpeed)
    //   ולא בבת אחת. ברגע השחרור הוא עוד בזווית של היד, וקפיצה לכיוון התעופה נראתה לא טבעית.
    // - לפני כל צעד שולחים קרן מהחוד קדימה. אם היא פוגעת בלוח בצעד הזה, החץ ננעץ בנקודת הפגיעה
    //   לפני שהפיזיקה מספיקה להקפיץ אותו. חץ דק ומהיר עלול לעבור דרך משטח או לנתר ממנו בלי זה.
    // - זווית רדודה מדי (מעל 65° מהניצב) לא ננעצת: הפיזיקה מקפיצה את החץ, והוא נופל.
    [RequireComponent(typeof(Rigidbody), typeof(XRGrabInteractable))]
    public class Dart : MonoBehaviour, ILaunchable
    {
        public enum State { Loose, Held, Flying, Stuck }

        [SerializeField] private float tipDistance = 0.08f;     // מהמרכז לחוד, לאורך ‎+Z
        [SerializeField] private float embedDepth = 0.012f;     // כמה החוד שוקע בלוח
        [SerializeField] private float maxStickAngle = 65f;
        [SerializeField] private float minStickSpeed = 1.5f;
        // מעלות לשנייה. החץ יוצא מהיד מצביע על המטרה, והתעופה עולה ממנה בכ-20°: מתיישר תוך 0.03 ש'
        [SerializeField] private float alignSpeed = 720f;

        private Rigidbody body;
        private XRGrabInteractable grab;
        private DartsGame game;
        private bool unlocked;
        private float releasedAt;

        public State Current { get; private set; } = State.Loose;
        public IXRInteractor LastHolder { get; private set; }
        public Vector3 Velocity => body.linearVelocity;
        public Vector3 Position => body.position;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            grab = GetComponent<XRGrabInteractable>();
        }

        private void OnEnable()
        {
            grab.selectEntered.AddListener(HandleGrabbed);
            grab.selectExited.AddListener(HandleReleased);
        }

        private void OnDisable()
        {
            grab.selectEntered.RemoveListener(HandleGrabbed);
            grab.selectExited.RemoveListener(HandleReleased);
        }

        public void Init(DartsGame owner) => game = owner;

        // מחוץ לסבב אי אפשר לתפוס. חץ תקוע בלוח אף פעם לא נתפס: הוא קינמטי, ו-XRI היה מחזיר אותו לקינמטי
        // אחרי הזריקה, כך שהוא לא היה עף. הוא חוזר למחזיק לבד.
        public void SetGrabbable(bool value)
        {
            unlocked = value;
            grab.enabled = value && Current != State.Stuck;
        }

        private void HandleGrabbed(SelectEnterEventArgs args)
        {
            LastHolder = args.interactorObject;
            Current = State.Held;
            if (game != null && game.Board != null)
            {
                // מיד, ולא רק בצעד הפיזיקה הבא: אחרת ברגע התפיסה רואים אותו לפריים בזווית שבה שכב במחזיק
                Quaternion aiming = Aiming(body.position, game.AimPoint);
                body.rotation = aiming;
                transform.rotation = aiming;
            }
            Haptics.Pulse(args.interactorObject, Haptics.Light, 0.03f);
        }

        private void HandleReleased(SelectExitEventArgs args)
        {
            Current = State.Flying;
            releasedAt = Time.time;
        }

        private void FixedUpdate()
        {
            if (Current == State.Held)
            {
                if (game != null && game.Board != null) body.MoveRotation(Aiming(body.position, game.AimPoint));
                return;
            }
            if (Current != State.Flying) return;

            Vector3 velocity = body.linearVelocity;
            float speed = velocity.magnitude;
            if (speed < 0.3f)
            {
                // XRI מחיל את מהירות הזריקה פריים אחרי השחרור, לא ברגע עצמו. בלי ההמתנה החץ היה "נוחת" עוד לפני שנזרק.
                if (Time.time - releasedAt > 0.15f) Current = State.Loose;
                return;
            }

            Vector3 direction = velocity / speed;
            Quaternion facing = Quaternion.RotateTowards(body.rotation, Quaternion.LookRotation(direction), alignSpeed * Time.fixedDeltaTime);
            body.MoveRotation(facing);
            body.angularVelocity = Vector3.zero;

            if (game == null || game.Board == null) return;
            Vector3 tip = body.position + facing * new Vector3(0f, 0f, tipDistance);
            float step = speed * Time.fixedDeltaTime + 0.02f;
            if (game.Board.StickSurface.Raycast(new Ray(tip, direction), out RaycastHit hit, step))
            {
                float angle = Vector3.Angle(direction, -hit.normal);
                if (angle <= maxStickAngle && speed >= minStickSpeed) Stick(hit.point, direction);
            }
        }

        // ביד: החוד אל המטרה, והחץ לא מוטה לצד
        public static Quaternion Aiming(Vector3 position, Vector3 target) => Quaternion.LookRotation(target - position, Vector3.up);

        // פגע במשהו אחר (קיר, רצפה, קצה הלוח בזווית רדודה): מפסיקים לכוון אותו, והוא נופל כמו שהוא
        private void OnCollisionEnter(Collision collision)
        {
            if (Current == State.Flying) Current = State.Loose;
        }

        private void Stick(Vector3 point, Vector3 direction)
        {
            Current = State.Stuck;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            Quaternion facing = Quaternion.LookRotation(direction);
            Vector3 position = point + direction * embedDepth - facing * new Vector3(0f, 0f, tipDistance);
            body.position = position;
            body.rotation = facing;
            transform.SetPositionAndRotation(position, facing);
            grab.enabled = false;
            game.HandleStuck(this, point);
        }

        // זריקה בלי יד (ILaunchable): ממקום של יד, במהירות נתונה. מכאן הכל כמו אחרי שחרור אמיתי.
        public void Launch(Vector3 from, Vector3 velocity)
        {
            if (Current == State.Held) return;
            Teleport(from, Quaternion.LookRotation(velocity));
            body.linearVelocity = velocity;
            Current = State.Flying;
            releasedAt = Time.time;
        }

        // חזרה למחזיק: שוב דינמי, נח במקום, וניתן לתפיסה אם הסבב פתוח
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            if (Current == State.Held) return;
            body.isKinematic = false;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = position;
            body.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
            Current = State.Loose;
            grab.enabled = unlocked;
        }
    }
}
