using UnityEngine;

namespace MotionArcade.Games.Darts
{
    // הלוח בעולם. ה-Transform הזה הוא מרכז הלוח: x ימינה, y למעלה, והפנים לכיוון השחקן (‎-Z).
    // stickSurface: המשטח שחצים ננעצים בו (הלוח והשעם סביבו). פגיעה מחוץ לטבעת הכפולה = החטאה, אבל החץ עדיין נתקע.
    public class DartBoard : MonoBehaviour
    {
        [SerializeField] private Collider stickSurface;

        public Collider StickSurface => stickSurface;

        public DartHit ScoreAt(Vector3 worldPoint)
        {
            Vector3 local = transform.InverseTransformPoint(worldPoint);
            return DartScoring.Score(local.x, local.y);
        }

        // הנקודה בעולם של מרכז מקטע (לבדיקות ולמיקום תוויות)
        public Vector3 WorldPointOf(int number, float radius)
        {
            float angle = DartScoring.AngleOf(number) * Mathf.Deg2Rad;
            return transform.TransformPoint(new Vector3(Mathf.Sin(angle) * radius, Mathf.Cos(angle) * radius, 0f));
        }
    }
}
