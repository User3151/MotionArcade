using UnityEngine;

namespace MotionArcade.Games.Basketball
{
    // הטבעת מגיבה למכה של הכדור (גיא: "שהסל יקפוץ למעלה למטה, יגיב למשקל של הכדור"): כמו טבעת אמיתית,
    // היא יורדת קצת סביב הציר שבו היא מחוברת ללוח, וחוזרת בקפיץ עם רטט קצר. הרשת תלויה על הטבעת
    // (ילדה של אותו ציר), אז היא זזה איתה. מכה בחזית הטבעת (רחוק מהציר) מזיזה יותר ממכה ליד הלוח.
    // יושב על הטבעת (OnCollisionEnter מגיע לקוליידר), ומסובב את הציר שמעליה.
    public class RimSpring : MonoBehaviour
    {
        [SerializeField] private Transform pivot;              // בגב הטבעת, ליד הלוח; הטבעת והרשת ילדות שלו
        [SerializeField] private float frequency = 6f;         // הרץ: כמה מהר היא קופצת חזרה
        [SerializeField, Range(0f, 1f)] private float dampingRatio = 0.22f;
        [SerializeField] private float degreesPerImpulse = 55f; // מעלות לשנייה לכל ניוטון-שנייה של מכה, בחזית הטבעת
        [SerializeField] private float maxDegrees = 7f;

        private Quaternion rest;
        private bool hasRest;
        private float angle;      // מעלות; חיובי = החזית למטה
        private float speed;      // מעלות לשנייה

        public float Angle => angle;

        private void Awake() => Init();

        private void Init()
        {
            if (hasRest) return;
            rest = pivot.localRotation;
            hasRest = true;
        }

        private void OnCollisionEnter(Collision collision) => Kick(collision.impulse, collision.GetContact(0).point);

        // גם לבדיקות: מכה בנקודה, עם מתקף (ניוטון-שנייה)
        public void Kick(Vector3 impulse, Vector3 point)
        {
            Init();
            // כמה רחוק מהציר: 0 ליד הלוח, 1 בחזית הטבעת (קוטר 0.46 מ')
            Vector3 local = pivot.InverseTransformPoint(point);
            float lever = Mathf.Clamp01(-local.z / 0.46f);
            // רק החלק האנכי של המכה מוריד את הטבעת; מכה מהצד מזיזה מעט
            float push = Mathf.Abs(impulse.y) + 0.25f * new Vector2(impulse.x, impulse.z).magnitude;
            speed += push * degreesPerImpulse * (0.3f + 0.7f * lever);
        }

        private void FixedUpdate() => Step(Time.fixedDeltaTime);

        public void Step(float dt)
        {
            if (angle == 0f && speed == 0f) return;
            Init();
            float w = 2f * Mathf.PI * frequency;
            // קפיץ מרוסן (חצי-מפורש: יציב גם בצעדים גדולים)
            speed += (-w * w * angle - 2f * dampingRatio * w * speed) * dt;
            angle += speed * dt;
            // היא לא עולה מעל המקום שלה (מחוברת לקפיץ שמושך למטה), ולא יורדת בלי סוף
            if (angle < 0f) { angle = 0f; speed = Mathf.Max(0f, -speed * 0.3f); }
            if (angle > maxDegrees) { angle = maxDegrees; speed = Mathf.Min(0f, speed); }
            if (angle < 0.01f && Mathf.Abs(speed) < 0.5f) { angle = 0f; speed = 0f; }
            pivot.localRotation = rest * Quaternion.AngleAxis(-angle, Vector3.right);
        }
    }
}
