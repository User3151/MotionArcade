using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace MotionArcade.Interaction
{
    // כמה כל אצבע כפופה (0 ישרה, 1 כפופה עד הסוף), לפי הכפתורים ומה שביד. בלי Unity, לבדיקות.
    // סדר האצבעות: אגודל, אצבע, אמה, קמיצה, זרת.
    public static class HandCurl
    {
        public const int Fingers = 5;

        private static readonly float[] Fist = { 0.75f, 0.9f, 0.95f, 0.95f, 0.95f };
        private static readonly float[] Pinch = { 0.6f, 0.55f, 0.8f, 0.9f, 0.95f };
        private static readonly float[] Cup = { 0.15f, 0.12f, 0.12f, 0.14f, 0.18f };

        // grip: L1/R1 (תפיסה), trigger: L2/R2. hasHold = יש חפץ ביד.
        public static float Target(int finger, float grip, float trigger, bool hasHold, HandShape shape)
        {
            if (hasHold)
            {
                switch (shape)
                {
                    case HandShape.Pinch: return Pinch[finger];
                    case HandShape.Cup: return Cup[finger];
                    default: return Fist[finger];
                }
            }
            grip = Mathf.Clamp01(grip);
            trigger = Mathf.Clamp01(trigger);
            switch (finger)
            {
                case 0: return 0.2f + 0.5f * Mathf.Max(grip, trigger);   // האגודל נסגר קצת בכל לחיצה
                case 1: return 0.15f + 0.8f * trigger;                    // ההדק = האצבע המורה
                default: return 0.2f + 0.75f * grip;                      // השאר עם התפיסה
            }
        }
    }

    // יד על הבקר, במקום דגם הבקר. דגם היד מהדוגמה של XR Hands (עצמות OpenXR), וכל אצבע מתכופפת סביב ציר X של
    // המפרקים שלה, לפי HandCurl. גיא ביקש ידיים בכל המשחקים, וה-XR Origin אחד ב-Hub, אז היד מופיעה בכולם.
    // קורא את הכפתורים דרך ה-interactor (selectInput / activateInput), לא ישירות מהבקר, כמו כל קוד המשחק.
    public class ControllerHand : MonoBehaviour
    {
        private static readonly string[] FingerNames = { "Thumb", "Index", "Middle", "Ring", "Little" };
        // כמה מעלות כל מפרק מתכופף בכפיפה מלאה: אגודל (בסיס, אמצע, קצה), שאר האצבעות (בסיס, אמצע, קצה)
        private static readonly float[] ThumbBend = { 20f, 40f, 55f };
        private static readonly float[] FingerBend = { 75f, 95f, 65f };

        [SerializeField] private XRBaseInputInteractor interactor;
        [SerializeField] private GameObject controllerVisual;   // מוסתר כל עוד היד מוצגת
        [SerializeField] private string bonePrefix = "R_";
        [SerializeField] private float speed = 12f;              // כמה מהר האצבעות זזות (חלק לשנייה)

        private Transform[][] joints;
        private Quaternion[][] bind;
        private readonly float[] curl = new float[HandCurl.Fingers];
        private HandHold hold;
        private bool holding;
        private Vector3 restPosition;      // על הבקר, כשהחפץ ביד לא מבקש תנוחה אחרת
        private Quaternion restRotation;

        public float Curl(int finger) => curl[finger];

        private void Awake()
        {
            FindJoints();
            restPosition = transform.localPosition;
            restRotation = transform.localRotation;
        }

        private void OnEnable()
        {
            if (controllerVisual != null) controllerVisual.SetActive(false);
            if (interactor == null) return;
            interactor.selectEntered.AddListener(HandleSelect);
            interactor.selectExited.AddListener(HandleDeselect);
        }

        private void OnDisable()
        {
            if (controllerVisual != null) controllerVisual.SetActive(true);
            if (interactor == null) return;
            interactor.selectEntered.RemoveListener(HandleSelect);
            interactor.selectExited.RemoveListener(HandleDeselect);
        }

        // GetComponent רק ברגע התפיסה, לא בכל פריים
        private void HandleSelect(SelectEnterEventArgs args)
        {
            holding = true;
            hold = args.interactableObject.transform.GetComponentInParent<HandHold>();
        }

        private void HandleDeselect(SelectExitEventArgs args)
        {
            holding = interactor.hasSelection;
            if (!holding) hold = null;
        }

        private void FindJoints()
        {
            joints = new Transform[HandCurl.Fingers][];
            bind = new Quaternion[HandCurl.Fingers][];
            for (int f = 0; f < HandCurl.Fingers; f++)
            {
                string[] parts = f == 0
                    ? new[] { "Metacarpal", "Proximal", "Distal" }
                    : new[] { "Proximal", "Intermediate", "Distal" };
                joints[f] = new Transform[parts.Length];
                bind[f] = new Quaternion[parts.Length];
                for (int j = 0; j < parts.Length; j++)
                {
                    Transform t = FindDeep(transform, bonePrefix + FingerNames[f] + parts[j]);
                    joints[f][j] = t;
                    bind[f][j] = t != null ? t.localRotation : Quaternion.identity;
                }
            }
        }

        private static Transform FindDeep(Transform root, string boneName)
        {
            if (root.name == boneName) return root;
            foreach (Transform child in root)
            {
                Transform found = FindDeep(child, boneName);
                if (found != null) return found;
            }
            return null;
        }

        private void Update()
        {
            float grip = 0f, trigger = 0f;
            if (interactor != null)
            {
                grip = interactor.selectInput.ReadValue();
                trigger = interactor.activateInput.ReadValue();
            }
            bool hasHold = holding && interactor != null && interactor.hasSelection;
            HandShape shape = hold != null ? hold.Shape : HandShape.Fist;
            float step = speed * Time.deltaTime;
            for (int f = 0; f < HandCurl.Fingers; f++)
                curl[f] = Mathf.MoveTowards(curl[f], HandCurl.Target(f, grip, trigger, hasHold, shape), step);
            Apply(curl);

            Vector3 position = restPosition;
            Quaternion rotation = restRotation;
            if (hasHold && hold != null && hold.MovesHand) hold.HandPose(bonePrefix.StartsWith("L"), out position, out rotation);
            float blend = 1f - Mathf.Exp(-speed * 1.5f * Time.deltaTime);   // כמו האצבעות, בלי קפיצה
            transform.SetLocalPositionAndRotation(Vector3.Lerp(transform.localPosition, position, blend),
                                                  Quaternion.Slerp(transform.localRotation, rotation, blend));
        }

        // מציב את האצבעות לפי curl (גם מ-Editor, לצילומי בדיקה)
        public void Apply(float[] values)
        {
            if (joints == null) FindJoints();
            for (int f = 0; f < HandCurl.Fingers; f++)
            {
                float[] bend = f == 0 ? ThumbBend : FingerBend;
                for (int j = 0; j < joints[f].Length; j++)
                {
                    Transform t = joints[f][j];
                    if (t != null) t.localRotation = bind[f][j] * Quaternion.Euler(values[f] * bend[j], 0f, 0f);
                }
            }
        }
    }
}
