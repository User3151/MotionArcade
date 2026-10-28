using MotionArcade.Core;
using MotionArcade.Feedback;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace MotionArcade.Games.Whack
{
    // פטיש. מודד בעצמו את מהירות הראש, מהפרש מיקומים בין צעדי פיזיקה: חפץ שמוחזק ב-XRI זז קינמטית,
    // ולכן rb.linearVelocity שלו לא אמין. החור משתמש במהירות הזו כדי להחליט אם זו מכה אמיתית.
    [RequireComponent(typeof(Rigidbody), typeof(XRGrabInteractable))]
    public class Mallet : MonoBehaviour, ILaunchable
    {
        [SerializeField] private Collider head;   // Trigger על הראש: רק הוא מכה, לא הידית

        private Rigidbody body;
        private XRGrabInteractable grab;
        private Vector3 lastHeadPosition;
        private bool hasLast;

        public Collider Head => head;
        public Vector3 HeadVelocity { get; private set; }
        public bool IsHeld => grab.isSelected;
        public IXRInteractor Holder { get; private set; }
        public Vector3 Position => body.position;
        public Vector3 Velocity => body.linearVelocity;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            grab = GetComponent<XRGrabInteractable>();
        }

        private void OnEnable() => grab.selectEntered.AddListener(HandleGrabbed);
        private void OnDisable() => grab.selectEntered.RemoveListener(HandleGrabbed);

        private void HandleGrabbed(SelectEnterEventArgs args)
        {
            Holder = args.interactorObject;
            Haptics.Pulse(args.interactorObject, Haptics.Light, 0.04f);
        }

        private void FixedUpdate()
        {
            Vector3 position = head.transform.position;
            HeadVelocity = hasLast ? (position - lastHeadPosition) / Time.fixedDeltaTime : Vector3.zero;
            lastHeadPosition = position;
            hasLast = true;
        }

        // מחוץ לסבב אי אפשר לתפוס. כבוי באמצע החזקה = XRI משחרר אותו מהיד.
        public void SetGrabbable(bool value) => grab.enabled = value;

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            if (IsHeld) return;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = position;
            body.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
            hasLast = false;   // בלי זה הקפיצה עצמה נמדדת כמכה מהירה
        }

        // בדיקות אוטומטיות ו-GcProbe: מכה בלי יד
        public void Launch(Vector3 from, Vector3 velocity)
        {
            if (IsHeld) return;
            Teleport(from, transform.rotation);
            body.linearVelocity = velocity;
        }
    }
}
