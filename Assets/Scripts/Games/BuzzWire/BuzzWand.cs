using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace MotionArcade.Games.BuzzWire
{
    // השרביט: ידית, מוט, וטבעת בקצה. ה-XRGrabInteractable מוגדר Instantaneous ובלי החלקה,
    // כדי שכל רעד של היד יגיע לטבעת: זה בדיוק מה שהמשחק מודד.
    // הוא קינמטי תמיד ולא נופל: כשמשחררים אותו הוא חוזר לעמדה שלו.
    [RequireComponent(typeof(XRGrabInteractable))]
    public class BuzzWand : MonoBehaviour
    {
        [SerializeField] private Transform ring;            // ‎+Z שלו = הנורמל של הטבעת
        [SerializeField] private float ringRadius = 0.025f; // רדיוס הקו האמצעי של הטבעת
        [SerializeField] private float ringTube = 0.003f;   // עובי החוט שממנו הטבעת עשויה

        private XRGrabInteractable grab;
        private Rigidbody body;

        public Transform Ring => ring;
        public float RingRadius => ringRadius;
        public float RingTube => ringTube;
        public bool IsHeld => grab.isSelected;
        public IXRInteractor Holder { get; private set; }

        public event Action Released;

        private void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            body = GetComponent<Rigidbody>();
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

        private void HandleGrabbed(SelectEnterEventArgs args) => Holder = args.interactorObject;
        private void HandleReleased(SelectExitEventArgs args) => Released?.Invoke();

        public void SetGrabbable(bool value) => grab.enabled = value;

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            if (IsHeld) return;
            transform.SetPositionAndRotation(position, rotation);
            if (body != null)
            {
                body.position = position;
                body.rotation = rotation;
            }
        }
    }
}
