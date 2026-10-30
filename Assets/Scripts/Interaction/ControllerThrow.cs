using System;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem.Controls;
using InputXRController = UnityEngine.InputSystem.XR.XRController;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace MotionArcade.Interaction
{
    // מה ידוע ברגע השחרור: המהירות ששימשה לזריקה, ולידה עוד מדידות של אותה תנועה, להשוואה (ThrowRecorder).
    // כל המהירויות בעולם, במטר לשנייה. NaN = לא ידוע (למשל אין מהירות מהקסדה).
    public struct ThrowRelease
    {
        public float Time;
        public InteractorHandedness Hand;
        public Vector3 Position;          // מרכז החפץ
        public Vector3 Forward;           // ציר ‎+Z של החפץ (בחץ: לאן החוד מצביע)
        public Vector3 ObjectVelocity;    // מהמיקומים של החפץ, לפני המכפיל
        public Vector3 Thrown;            // הזריקה של השחקן: אחרי המכפיל, לפני עזרת הכיוון (זה מה שנמדד רפואית)
        public Vector3 Applied;           // מה שהחפץ קיבל בפועל, אחרי עזרת הכיוון
        public Vector3 HandVelocity;      // מהמיקומים של הבקר עצמו, 60 ms אחרונים
        public Vector3 DeviceVelocity;    // מה שהקסדה מדווחת על הבקר (OpenXR), אם יש
        public Vector3 HandForward;       // לאן הבקר מצביע
        public Vector3 HandLocalFlight;   // כיוון הזריקה במרחב של נקודת האחיזה ביד (לכיוון האחיזה של החץ)
        public float HandPeakSpeed;       // המהירות הכי גבוהה של הבקר ב-0.3 השניות האחרונות
        public float PeakSecondsBefore;   // כמה זמן לפני השחרור היה השיא
        public float SelectDropSeconds;   // מתי הכפתור התחיל להשתחרר (ירד מתחת ל-90% מהשיא) עד שהזריקה יצאה. NaN = לא ידוע
    }

    // מתקנת זריקה לפני שהיא יוצאת (עזרת כיוון). מחזירה את המהירות החדשה.
    public interface IThrowAssist
    {
        Vector3 Adjust(Vector3 position, Vector3 velocity);
    }

    // הזריקה עצמה, במקום ה-throwOnDetach של XRI (שצריך להיות כבוי על החפץ).
    // המהירות באה מהבקר עצמו (HandPeak), מהרגע הכי מהיר שלו ב-0.1 השניות האחרונות. ככה זה יצא מהמדידה של גיא:
    // - בכדור, שיושב 12 ס"מ לפני היד, הצלפת שורש כף היד הוסיפה לחפץ 33% מעל מהירות היד, והכדורים עפו רחוק מדי.
    // - בחצים היד כבר האטה ב-16% מהשיא כשהכפתור שוחרר, והחצים יצאו חלשים ונמוכים.
    // אם אין מספיק דגימות של הבקר (חפץ שנתפס לרגע), נופלים חזרה למיקומים של החפץ.
    // בנוסף נמדדים מצב הכפתור, המהירות שהקסדה מדווחת ועוד, ונשלחים ב-Released (למדידת הזריקות).
    [RequireComponent(typeof(Rigidbody), typeof(XRGrabInteractable))]
    public class ControllerThrow : MonoBehaviour
    {
        public enum VelocitySource { HandPeak, Object }

        private const float MeasureWindow = 0.3f;

        [SerializeField] private VelocitySource source = VelocitySource.HandPeak;
        [SerializeField] private float velocityScale = 1f;   // מכפיל לכל משחק, לפי מדידה (ראו הסבר למעלה)
        [SerializeField] private float peakWindow = 0.1f;    // HandPeak: כמה אחורה מחפשים את השיא
        [SerializeField] private float window = 0.06f;       // Object: שניות אחרונות לפני השחרור
        [SerializeField] private float maxSpeed = 15f;       // הגנה מקפיצת מעקב (בקר שאיבד מעקב לרגע)
        [SerializeField] private float backspin = 0f;        // סיבובים לשנייה לאחור (כדור: נרגע על הטבעת במקום לנתר)

        private readonly ReleaseVelocity samples = new ReleaseVelocity(32);
        private readonly ReleaseVelocity handSamples = new ReleaseVelocity(64);   // בכל פריים, עד 120 בשנייה
        private Rigidbody body;
        private XRGrabInteractable grab;
        private Vector3 pending;
        private Vector3 pendingSpin;
        private bool applyNextStep;

        // היד שמחזיקה עכשיו
        private Transform hand;
        private Transform handAttach;
        private Transform trackingSpace;
        private XRBaseInputInteractor input;
        private InteractorHandedness handedness;
        private Vector3Control deviceVelocity;
        private readonly ButtonRelease button = new ButtonRelease();

        public Vector3 LastReleaseVelocity { get; private set; }

        // עזרת כיוון, אם המשחק נותן (בכדורסל). null = הזריקה כמו שהיא.
        public IThrowAssist Assist { get; set; }

        // אחרי כל זריקה אמיתית (לא שחרור בגלל נעילה)
        public event Action<ControllerThrow, ThrowRelease> Released;

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

        private void HandleGrabbed(SelectEnterEventArgs args)
        {
            samples.Clear();
            handSamples.Clear();
            hand = args.interactorObject.transform;
            handAttach = args.interactorObject.GetAttachTransform(grab);
            input = args.interactorObject as XRBaseInputInteractor;
            handedness = args.interactorObject.handedness;
            XROrigin origin = hand.GetComponentInParent<XROrigin>();
            trackingSpace = origin != null && origin.CameraFloorOffsetObject != null ? origin.CameraFloorOffsetObject.transform : null;
            deviceVelocity = FindDeviceVelocity(handedness);
            button.Reset();
        }

        // המהירות שהקסדה מחשבת לבקר (OpenXR). רק בבקרים, לא בידיים. ב-PSVR2 דרך SteamVR יצאה חצי מהמהירות
        // האמיתית ובכיוונים משונים, אז היא רק נרשמת, לא משמשת לזריקה.
        // החיפוש לפי שם מקצה זיכרון, אז הוא נשמר לכל יד ומתחדש רק כשהבקר עצמו מתחלף (נכבה ונדלק).
        private static InputXRController cachedDevice0, cachedDevice1;
        private static Vector3Control cachedVelocity0, cachedVelocity1;

        private static Vector3Control FindDeviceVelocity(InteractorHandedness side)
        {
            bool left = side == InteractorHandedness.Left;
            InputXRController device = left ? InputXRController.leftHand
                                : side == InteractorHandedness.Right ? InputXRController.rightHand : null;
            if (device == null) return null;
            if (left && device == cachedDevice0) return cachedVelocity0;
            if (!left && device == cachedDevice1) return cachedVelocity1;

            Vector3Control velocity = device.TryGetChildControl<Vector3Control>("devicePose/velocity")
                ?? device.TryGetChildControl<Vector3Control>("deviceVelocity");
            if (left) { cachedDevice0 = device; cachedVelocity0 = velocity; }
            else { cachedDevice1 = device; cachedVelocity1 = velocity; }
            return velocity;
        }

        private void Update()
        {
            if (!grab.isSelected || hand == null) return;
            Vector3 p = hand.position;
            handSamples.Add(Time.time, p.x, p.y, p.z);

            if (input != null) button.Update(Time.time, input.selectInput.ReadValue());
        }

        private void FixedUpdate()
        {
            if (applyNextStep)
            {
                // XRI מסיים לנתק את החפץ אחרי האירוע; מוודאים שהמהירות שלנו היא זו שנשארה
                applyNextStep = false;
                if (!body.isKinematic)
                {
                    body.linearVelocity = pending;
                    if (backspin > 0f) body.angularVelocity = pendingSpin;
                }
            }
            if (grab.isSelected)
            {
                Vector3 p = body.position;
                samples.Add(Time.time, p.x, p.y, p.z);
            }
        }

        private void HandleReleased(SelectExitEventArgs args)
        {
            bool haveObject = samples.TryEstimate(window, out float ox, out float oy, out float oz);
            // שחרור שנגרם מנעילה (STOP, סוף סבב): החפץ פשוט נופל
            if (args.isCanceled || !TryThrowVelocity(haveObject, new Vector3(ox, oy, oz), out Vector3 raw))
            {
                LastReleaseVelocity = Vector3.zero;
                hand = null;
                return;
            }

            Vector3 thrown = Vector3.ClampMagnitude(raw * velocityScale, maxSpeed);
            Vector3 v = Assist != null ? Assist.Adjust(body.position, thrown) : thrown;
            LastReleaseVelocity = v;
            pending = v;
            pendingSpin = Backspin(v);
            applyNextStep = true;
            if (!body.isKinematic)
            {
                body.linearVelocity = v;
                if (backspin > 0f)
                {
                    body.maxAngularVelocity = Mathf.Max(body.maxAngularVelocity, pendingSpin.magnitude + 1f);
                    body.angularVelocity = pendingSpin;
                }
            }

            // כפתור דיגיטלי (L1/R1) יכול לקפוץ ל-0 ולשחרר באותו פריים, לפני ש-Update ראה אותו יורד
            if (input != null) button.Update(Time.time, input.selectInput.ReadValue());
            Released?.Invoke(this, Measure(haveObject ? new Vector3(ox, oy, oz) : Vector3.zero, thrown, v));
            hand = null;
        }

        private bool TryThrowVelocity(bool haveObject, Vector3 objectVelocity, out Vector3 velocity)
        {
            if (source == VelocitySource.HandPeak
                && handSamples.TryPeakVelocity(peakWindow, out float vx, out float vy, out float vz, out _))
            {
                velocity = new Vector3(vx, vy, vz);
                return true;
            }
            velocity = objectVelocity;
            return haveObject;
        }

        // סיבוב לאחור סביב הציר האופקי שניצב לכיוון הזריקה: החלק העליון של הכדור נע אחורה
        private Vector3 Backspin(Vector3 v)
        {
            if (backspin <= 0f) return Vector3.zero;
            Vector3 flat = new Vector3(v.x, 0f, v.z);
            if (flat.sqrMagnitude < 1e-4f) return Vector3.zero;
            Vector3 right = Vector3.Cross(Vector3.up, flat.normalized);
            return -right * (backspin * 2f * Mathf.PI);
        }

        private ThrowRelease Measure(Vector3 objectVelocity, Vector3 thrown, Vector3 applied)
        {
            var unknown = new Vector3(float.NaN, float.NaN, float.NaN);
            var info = new ThrowRelease
            {
                Time = Time.time,
                Hand = handedness,
                Position = body.position,
                Forward = body.rotation * Vector3.forward,
                ObjectVelocity = objectVelocity,
                Thrown = thrown,
                Applied = applied,
                HandVelocity = unknown,
                DeviceVelocity = unknown,
                HandForward = hand != null ? hand.forward : Vector3.zero,
                HandLocalFlight = handAttach != null && thrown.sqrMagnitude > 1e-6f
                    ? handAttach.InverseTransformDirection(thrown.normalized) : unknown,
                HandPeakSpeed = float.NaN,
                PeakSecondsBefore = float.NaN,
                SelectDropSeconds = button.SecondsSinceDrop(Time.time),
            };
            if (handSamples.TryEstimate(window, out float hx, out float hy, out float hz))
                info.HandVelocity = new Vector3(hx, hy, hz);
            if (handSamples.TryPeakSpeed(MeasureWindow, out float peak, out float before))
            {
                info.HandPeakSpeed = peak;
                info.PeakSecondsBefore = before;
            }
            if (TryDeviceVelocity(out Vector3 device)) info.DeviceVelocity = device;
            return info;
        }

        private bool TryDeviceVelocity(out Vector3 world)
        {
            world = default;
            Vector3 local = default;
            bool found = false;
            if (deviceVelocity != null && deviceVelocity.device.added)
            {
                local = deviceVelocity.ReadValue();
                found = local.sqrMagnitude > 1e-8f;
            }
            if (!found)
            {
                // ה-API הישן של XR, אם ה-Input System לא נותן מהירות
                XRNode node = handedness == InteractorHandedness.Left ? XRNode.LeftHand : XRNode.RightHand;
                InputDevice legacy = InputDevices.GetDeviceAtXRNode(node);
                found = legacy.isValid && legacy.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceVelocity, out local)
                        && local.sqrMagnitude > 1e-8f;
            }
            if (!found) return false;
            // הקסדה מדווחת במרחב המעקב; המעבר לעולם הוא דרך ה-Camera Offset של ה-XR Origin
            world = trackingSpace != null ? trackingSpace.TransformDirection(local) : local;
            return true;
        }
    }
}
