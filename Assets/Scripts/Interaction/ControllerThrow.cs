using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace MotionArcade.Interaction
{
    // הזריקה עצמה, במקום ה-throwOnDetach של XRI (שצריך להיות כבוי על החפץ):
    // בזמן שהחפץ ביד שומרים את המיקומים שלו, וברגע השחרור נותנים לו את מהירות החלון האחרון (ReleaseVelocity).
    // המיקום הוא של החפץ עצמו, לא של הבקר, אז גם "הצלפה" של שורש כף היד (חץ) נכנסת למהירות.
    [RequireComponent(typeof(Rigidbody), typeof(XRGrabInteractable))]
    public class ControllerThrow : MonoBehaviour
    {
        [SerializeField] private float velocityScale = 1f;   // מכפיל לכל משחק: כדור כבד לזרוק בלי משקל ביד
        [SerializeField] private float window = 0.06f;       // שניות אחרונות לפני השחרור
        [SerializeField] private float maxSpeed = 15f;       // הגנה מקפיצת מעקב (בקר שאיבד מעקב לרגע)

        private readonly ReleaseVelocity samples = new ReleaseVelocity(32);
        private Rigidbody body;
        private XRGrabInteractable grab;
        private Vector3 pending;
        private bool applyNextStep;

        public Vector3 LastReleaseVelocity { get; private set; }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            grab = GetComponent<XRGrabInteractable>();
            grab.throwOnDetach = false;   // הזריקה כאן; אחרת XRI היה דורס את המהירות
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

        private void HandleGrabbed(SelectEnterEventArgs args) => samples.Clear();

        private void FixedUpdate()
        {
            if (applyNextStep)
            {
                // XRI מסיים לנתק את החפץ אחרי האירוע; מוודאים שהמהירות שלנו היא זו שנשארה
                applyNextStep = false;
                if (!body.isKinematic) body.linearVelocity = pending;
            }
            if (grab.isSelected)
            {
                Vector3 p = body.position;
                samples.Add(Time.time, p.x, p.y, p.z);
            }
        }

        private void HandleReleased(SelectExitEventArgs args)
        {
            // שחרור שנגרם מנעילה (STOP, סוף סבב): החפץ פשוט נופל
            if (args.isCanceled || !samples.TryEstimate(window, out float vx, out float vy, out float vz))
            {
                LastReleaseVelocity = Vector3.zero;
                return;
            }
            Vector3 v = Vector3.ClampMagnitude(new Vector3(vx, vy, vz) * velocityScale, maxSpeed);
            LastReleaseVelocity = v;
            pending = v;
            applyNextStep = true;
            if (!body.isKinematic) body.linearVelocity = v;
        }
    }
}
