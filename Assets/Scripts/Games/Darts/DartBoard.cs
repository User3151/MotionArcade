using UnityEngine;

namespace MotionArcade.Games.Darts
{
    // הלוח בעולם. ה-Transform הזה הוא מרכז הלוח: x ימינה, y למעלה, והפנים לכיוון השחקן (‎-Z).
    // stickSurface: המשטח שחצים ננעצים בו (הלוח והשעם סביבו). פגיעה מחוץ לטבעת הכפולה = החטאה, אבל החץ עדיין נתקע.
    // size: הלוח המצויר (Face, Numbers) גדול פי size מלוח תקני, והניקוד מחולק בו. בזריקה בלי משקל ביד הפיזור
    // של גיא היה ±13 ס"מ לצדדים, כמעט כמו כל הלוח התקני (17 ס"מ עד הכפולה), אז הלוח גדול ב-30% וקרוב יותר.
    public class DartBoard : MonoBehaviour
    {
        [SerializeField] private Collider stickSurface;
        [SerializeField, Min(0.1f)] private float size = 1f;

        public Collider StickSurface => stickSurface;
        public float Size => size;

        public DartHit ScoreAt(Vector3 worldPoint)
        {
            Vector3 local = transform.InverseTransformPoint(worldPoint);
            return DartScoring.Score(local.x / size, local.y / size);
        }

        // הנקודה בעולם של מרכז מקטע (לבדיקות ולמיקום תוויות). radius במידות של לוח תקני.
        public Vector3 WorldPointOf(int number, float radius)
        {
            float angle = DartScoring.AngleOf(number) * Mathf.Deg2Rad;
            radius *= size;
            return transform.TransformPoint(new Vector3(Mathf.Sin(angle) * radius, Mathf.Cos(angle) * radius, 0f));
        }
    }
}
