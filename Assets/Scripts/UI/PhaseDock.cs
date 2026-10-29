using MotionArcade.Core;
using UnityEngine;

namespace MotionArcade.UI
{
    // מזיז ומקטין לוח לפי שלב הסבב. הלוח תמיד נשאר במרכז, בשדה הראייה:
    // מחוץ לסבב (Intro / Results) הוא בגודל מלא מול השחקן, עם ההסבר, START ו-BACK;
    // מ-START ועד הסוף הוא קטן יותר ונמוך יותר, מתחת לקו הראייה אל המטרה, ונשארים בו STOP ו-BACK.
    // (בגרסה הקודמת הוא זז לצד, וגיא לא מצא אותו באמצע סבב.)
    // התנועה קצרה וחלקה: קפיצה של לוח מול העיניים ב-VR מבלבלת.
    public class PhaseDock : MonoBehaviour
    {
        [SerializeField] private GameStation station;
        [SerializeField] private Transform idlePose;
        [SerializeField] private Transform runningPose;
        [SerializeField, Range(0.3f, 1f)] private float runningScale = 0.6f;
        [SerializeField] private float moveSeconds = 0.35f;

        private Transform target;
        private float progress = 1f;
        private Vector3 fromPosition;
        private Quaternion fromRotation;
        private Vector3 fromScale;
        private Vector3 baseScale;

        private void Awake() => baseScale = transform.localScale;
        private void OnEnable() => station.PhaseChanged += HandlePhase;
        private void OnDisable() => station.PhaseChanged -= HandlePhase;

        // ב-Start: השלב של GameStation קיים רק אחרי ה-Awake שלה
        private void Start()
        {
            target = station.IsRunning ? runningPose : idlePose;
            transform.SetPositionAndRotation(target.position, target.rotation);
            transform.localScale = ScaleFor(target);
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
            progress = 0f;
        }

        private void Update()
        {
            if (progress >= 1f || target == null) return;
            progress = moveSeconds > 0f ? Mathf.Min(1f, progress + Time.deltaTime / moveSeconds) : 1f;
            float t = Mathf.SmoothStep(0f, 1f, progress);
            transform.SetPositionAndRotation(
                Vector3.Lerp(fromPosition, target.position, t),
                Quaternion.Slerp(fromRotation, target.rotation, t));
            transform.localScale = Vector3.Lerp(fromScale, ScaleFor(target), t);
        }
    }
}
