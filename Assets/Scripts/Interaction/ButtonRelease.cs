namespace MotionArcade.Interaction
{
    // מתי השחקן התחיל לשחרר את הכפתור שמחזיק חפץ: הרגע שבו הלחיצה ירדה מתחת ל-90% מהלחיצה הכי חזקה מאז התפיסה.
    // trigger מתחיל לעלות עשרות מילי-שניות לפני ש-XRI מחשיב אותו משוחרר; בזמן הזה היד כבר מאטה.
    // חזרה מעל ה-90% (סתם הרפיה קלה) מבטלת. כפתור דיגיטלי (L1/R1) קופץ מ-1 ל-0, אז ההפרש יוצא 0.
    // בלי Unity, לבדיקות.
    public class ButtonRelease
    {
        public const float DropFraction = 0.9f;

        private float peak;

        public float DropStart { get; private set; } = float.NaN;   // NaN = עוד לא התחיל להשתחרר

        public void Reset()
        {
            peak = 0f;
            DropStart = float.NaN;
        }

        public void Update(float time, float value)
        {
            if (value > peak) peak = value;
            if (value >= peak * DropFraction) DropStart = float.NaN;
            else if (float.IsNaN(DropStart)) DropStart = time;
        }

        // כמה זמן עבר מתחילת השחרור ועד time. NaN אם לא ידוע.
        public float SecondsSinceDrop(float time) => float.IsNaN(DropStart) ? float.NaN : time - DropStart;
    }
}
