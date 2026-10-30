using UnityEngine;

namespace MotionArcade.Games.Basketball
{
    // מחליט מה נחשב סל, בלי Unity, כדי שאפשר יהיה לבדוק את זה בבדיקות EditMode.
    // סל = מרכז הכדור חוצה את מישור הטבעת כלפי מטה, בתוך הטבעת. אין טווח ביניים: כדור שמרכזו במישור הטבעת,
    // רחוק מהציר פחות מהטבעת אבל יותר מ-(טבעת פחות רדיוס הכדור), נוגע בה, והפיזיקה לא נותנת לו להישאר שם.
    // לכן כל חצייה בתוך הטבעת היא כדור שעובר בה, וכדור שנופל מבחוץ חוצה רחוק ממנה.
    // קודם היו כאן שני חיישנים, מעל הטבעת ו-32 ס"מ מתחתיה. לרשת אין פיזיקה, אז כדור שעבר בטבעת עם מהירות
    // לצד נסחף עד שהגיע לגובה התחתון, פספס אותו (16 ס"מ), ולא נספר. גיא ראה סלים נקיים שלא נספרו.
    public static class BasketDetector
    {
        public enum Crossing { None, Down, Up }

        // previous, current: מרכז הכדור במרחב של הטבעת (y למעלה, מישור הטבעת ב-y=0) בשני צעדי פיזיקה רצופים.
        // radius: רדיוס הטבעת (עד מרכז הצינור שלה).
        public static Crossing Through(Vector3 previous, Vector3 current, float radius)
        {
            bool down = previous.y > 0f && current.y <= 0f;
            bool up = previous.y <= 0f && current.y > 0f;
            if (!down && !up) return Crossing.None;

            // איפה בדיוק הכדור חצה את המישור, בין שתי הדגימות (בצעד אחד הוא עובר 7 ס"מ)
            float t = previous.y / (previous.y - current.y);
            float x = previous.x + (current.x - previous.x) * t;
            float z = previous.z + (current.z - previous.z) * t;
            if (x * x + z * z > radius * radius) return Crossing.None;
            return down ? Crossing.Down : Crossing.Up;
        }
    }

    public static class BasketballScoring
    {
        public const int PointsPerBasket = 2;
        public const float BonusSeconds = 10f;   // השניות האחרונות של הסבב: ניקוד כפול, כמו במכונות ארקייד

        public static bool IsBonus(float timeLeft) => timeLeft > 0f && timeLeft <= BonusSeconds;

        public static int PointsFor(float timeLeft) => IsBonus(timeLeft) ? PointsPerBasket * 2 : PointsPerBasket;
    }
}
