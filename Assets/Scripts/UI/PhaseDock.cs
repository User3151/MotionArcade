using MotionArcade.Core;
using UnityEngine;

namespace MotionArcade.UI
{
    // מזיז לוח בין שני מקומות לפי שלב הסבב:
    // מחוץ לסבב (Intro / Results) הוא במרכז, מול השחקן, כדי שיראה אותו מיד כשהוא נכנס למשחק ואחרי כל סבב;
    // מ-START ועד הסוף הוא זז הצידה, מחוץ לקו הזריקה, ונשארים בו STOP ו-BACK.
    // התנועה קצרה וחלקה: קפיצה של לוח מול העיניים ב-VR מבלבלת.
    public class PhaseDock : MonoBehaviour
    {
        [SerializeField] private GameStation station;
        [SerializeField] private Transform idlePose;      // מרכז, מול השחקן
        [SerializeField] private Transform runningPose;   // בצד
        [SerializeField] private float moveSeconds = 0.35f;

        private Transform target;
        private float progress = 1f;
        private Vector3 fromPosition;
        private Quaternion fromRotation;

        private void OnEnable() => station.PhaseChanged += HandlePhase;
        private void OnDisable() => station.PhaseChanged -= HandlePhase;

        // ב-Start: השלב של GameStation קיים רק אחרי ה-Awake שלה
        private void Start()
        {
            target = station.IsRunning ? runningPose : idlePose;
            transform.SetPositionAndRotation(target.position, target.rotation);
        }

        private void HandlePhase(RoundPhase phase)
        {
            Transform next = station.IsRunning ? runningPose : idlePose;
            if (next == target) return;
            target = next;
            fromPosition = transform.position;
            fromRotation = transform.rotation;
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
        }
    }
}
