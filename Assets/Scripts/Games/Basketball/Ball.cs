using MotionArcade.Feedback;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace MotionArcade.Games.Basketball
{
    // כדור כדורסל. הזריקה עצמה ב-ControllerThrow; כאן רק:
    // - זוכרים מי זרק אותו אחרון, כדי לרטוט ביד הנכונה כשהוא נכנס לסל,
    // - בכל צעד פיזיקה מעבירים למשחק את המרכז הקודם והנוכחי, לבדיקת סל. כל זריקה נספרת פעם אחת לכל היותר,
    //   ומתחדשת כשהכדור נתפס שוב או חוזר למגש,
    // - מחזירים אותו למגש אם הוא יצא מהמכונה או נתקע (על הלוח, על הטבעת), כי השחקן לא יכול ללכת להביא אותו.
    [RequireComponent(typeof(Rigidbody), typeof(XRGrabInteractable))]
    public class Ball : MonoBehaviour
    {
        [SerializeField] private float outsideSeconds = 1.5f;   // מחוץ למכונה: נותנים לו רגע לנחות, ואז מחזירים
        [SerializeField] private float stuckSeconds = 3f;       // עומד במקום שאינו המגש
        [SerializeField] private float stillSpeed = 0.05f;

        private Rigidbody body;
        private XRGrabInteractable grab;
        private BasketballGame game;
        private float outsideTimer;
        private float stillTimer;
        private Vector3 previous;
        private bool hasPrevious;   // false אחרי תפיסה והצבה: אין "צעד קודם" אמיתי להשוות אליו
        private bool ringDone;      // הזריקה הזו כבר נספרה (או עלתה דרך הטבעת מלמטה)

        public bool IsHeld => grab.isSelected;
        public Vector3 Velocity => body.linearVelocity;
        public IXRInteractor LastHolder { get; private set; }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            grab = GetComponent<XRGrabInteractable>();
        }

        private void OnEnable()
        {
            grab.selectEntered.AddListener(HandleGrabbed);
        }

        private void OnDisable()
        {
            grab.selectEntered.RemoveListener(HandleGrabbed);
        }

        public void Init(BasketballGame owner) => game = owner;

        // מחוץ לסבב אי אפשר לתפוס את הכדור. כבוי באמצע החזקה (סוף סבב, STOP) = XRI משחרר אותו מהיד.
        public void SetGrabbable(bool value) => grab.enabled = value;

        private void HandleGrabbed(SelectEnterEventArgs args)
        {
            LastHolder = args.interactorObject;
            outsideTimer = 0f;
            stillTimer = 0f;
            ringDone = false;   // זריקה חדשה
            Haptics.Pulse(args.interactorObject, Haptics.Light, 0.04f);
        }

        private void FixedUpdate()
        {
            if (game == null || IsHeld)
            {
                outsideTimer = stillTimer = 0f;
                hasPrevious = false;
                return;
            }

            Vector3 position = body.position;
            float dt = Time.fixedDeltaTime;

            if (hasPrevious && !ringDone) ringDone = game.CheckRing(this, previous, position);
            previous = position;
            hasPrevious = true;

            outsideTimer = game.InsideMachine(position) ? 0f : outsideTimer + dt;
            stillTimer = body.linearVelocity.sqrMagnitude < stillSpeed * stillSpeed && !game.InTray(position) ? stillTimer + dt : 0f;

            if (outsideTimer > outsideSeconds || stillTimer > stuckSeconds)
                game.ReturnBall(this);
        }

        // הצבה ישירה (בלי לעבור דרך העולם). גם transform וגם body, כי עם Interpolate ה-transform לבד נדרס.
        public void Teleport(Vector3 position)
        {
            if (IsHeld) return;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = position;
            transform.position = position;
            outsideTimer = stillTimer = 0f;
            hasPrevious = false;   // בלי זה הקפיצה עצמה (מהלוח למגש) הייתה נבדקת כתנועה
            ringDone = false;
        }
    }
}
