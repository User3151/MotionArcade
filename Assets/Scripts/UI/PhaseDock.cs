using MotionArcade.Core;
using UnityEngine;

namespace MotionArcade.UI
{
    // מזיז ומקטין לוח לפי שלב הסבב. הלוח תמיד נשאר במרכז, בשדה הראייה:
    // מחוץ לסבב (Intro / Results) הוא בגודל מלא מול השחקן, עם ההסבר, START ו-BACK;
    // מ-START ועד הסוף הוא קטן יותר ונמוך יותר, מתחת לקו הראייה אל המטרה, ונשארים בו STOP ו-BACK.
    // (בגרסה הקודמת הוא זז לצד, וגיא לא מצא אותו באמצע סבב.)
    // התנועה קצרה וחלקה: קפיצה של לוח מול העיניים ב-VR מבלבלת.
    // faceHead: הלוח מסתובב אל הראש של השחקן בכל תנועה ובכל כניסה (גיא: בהוקי הוא נטה באלכסון קדימה, לא מול הפנים).
    // ה-pose נותן רק מיקום; הזווית מחושבת מהגובה האמיתי של הראש, בלי הטיה הצידה.
    public class PhaseDock : MonoBehaviour
    {
        [SerializeField] private GameStation station;
        [SerializeField] private Transform idlePose;
        [SerializeField] private Transform runningPose;
        [SerializeField, Range(0.3f, 1f)] private float runningScale = 0.6f;
        [SerializeField] private float moveSeconds = 0.35f;
        [SerializeField] private bool faceHead = true;

        private Transform target;
        private float progress = 1f;
        private Vector3 fromPosition;
        private Quaternion fromRotation;
        private Quaternion targetRotation;
        private Vector3 fromScale;
        private Vector3 baseScale;

        private void Awake() => baseScale = transform.localScale;
        private void OnEnable()
        {
            station.PhaseChanged += HandlePhase;
            // הלוח מופיע (PLAY): מיד במקום, מול הראש
            if (target != null)
            {
                transform.SetPositionAndRotation(target.position, RotationFor(target));
                transform.localScale = ScaleFor(target);
                progress = 1f;
            }
        }
        private void OnDisable() => station.PhaseChanged -= HandlePhase;

        // ב-Start: השלב של GameStation קיים רק אחרי ה-Awake שלה
        private void Start()
        {
            target = station.IsRunning ? runningPose : idlePose;
            transform.SetPositionAndRotation(target.position, RotationFor(target));
            transform.localScale = ScaleFor(target);
        }

        public Quaternion RotationFor(Transform pose)
        {
            Camera head = faceHead ? Camera.main : null;
            if (head == null) return pose.rotation;
            Vector3 toPanel = pose.position - head.transform.position;
            if (toPanel.sqrMagnitude < 0.01f) return pose.rotation;
            // הקנבס נראה מהצד של -forward: forward מצביע מהראש אל הלוח
            return Quaternion.LookRotation(toPanel, Vector3.up);
        }

        private Vector3 ScaleFor(Transform pose) => pose == runningPose ? baseScale * runningScale : baseScale;

        private void HandlePhase(RoundPhase phase)
        {
            Transform next = station.IsRunning ? runningPose : idlePose;
            if (next == target) return;
            target = next;
            fromPosition = transform.position;
            fromRotation = transform.rotation;
            fromScale = transform.localScale;
            targetRotation = RotationFor(target);
            progress = 0f;
        }

        private void Update()
        {
            if (progress >= 1f || target == null) return;
            progress = moveSeconds > 0f ? Mathf.Min(1f, progress + Time.deltaTime / moveSeconds) : 1f;
            float t = Mathf.SmoothStep(0f, 1f, progress);
            transform.SetPositionAndRotation(
                Vector3.Lerp(fromPosition, target.position, t),
                Quaternion.Slerp(fromRotation, targetRotation, t));
            transform.localScale = Vector3.Lerp(fromScale, ScaleFor(target), t);
        }
    }
}
