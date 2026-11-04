using System;
using MotionArcade.Feedback;
using UnityEngine;

namespace MotionArcade.Core
{
    // כל המשחקים עומדים בארקייד כל הזמן. משחק "נפתח" רק כשהשחקן עומד על העיגול שלו (השורש של המשחק):
    // אז מופיע לוח ה-START והמוזיקה שלו מתחילה. משחק שאף אחד לא עומד מולו נשאר שקט ומציג רק שלט "עמוד על העיגול".
    // יוצאים מהעיגול באמצע סבב: הסבב נעצר כמו ב-STOP (לא נרשם), כי אי אפשר לשחק ממקום אחר.
    // המרחק נמדד מהראש (המצלמה) על הרצפה, עם שוליים ביציאה, כדי שהטיה קטנה של הגוף לא תסגור את המשחק.
    public class StationPresence : MonoBehaviour
    {
        [SerializeField] private GameStation station;
        [SerializeField] private float enterRadius = 0.45f;
        [SerializeField] private float exitRadius = 0.8f;
        [SerializeField] private GameObject[] showWhenPresent;   // לוח START / STOP
        [SerializeField] private GameObject[] showWhenAway;      // שלט "עמוד על העיגול"
        [SerializeField] private GameMusic music;                // אופציונלי

        private Transform head;

        public bool Occupied { get; private set; }
        public GameStation Station => station;
        public event Action<bool> OccupiedChanged;

        private void Start() => Apply(false, force: true);

        private void Update()
        {
            if (head == null)
            {
                Camera main = Camera.main;
                if (main == null) return;
                head = main.transform;
            }
            Vector3 local = transform.InverseTransformPoint(head.position);
            float distance = new Vector2(local.x, local.z).magnitude;
            bool occupied = Occupied ? distance <= exitRadius : distance <= enterRadius;
            if (occupied != Occupied) Apply(occupied, force: false);
        }

        private void Apply(bool occupied, bool force)
        {
            if (!force && occupied == Occupied) return;
            Occupied = occupied;
            if (!occupied && station != null && station.IsRunning) station.AbortRound();
            foreach (GameObject go in showWhenPresent) if (go != null) go.SetActive(occupied);
            foreach (GameObject go in showWhenAway) if (go != null) go.SetActive(!occupied);
            if (music != null) music.SetAudible(occupied);
            OccupiedChanged?.Invoke(occupied);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireSphere(Vector3.zero, enterRadius);
        }
    }
}
