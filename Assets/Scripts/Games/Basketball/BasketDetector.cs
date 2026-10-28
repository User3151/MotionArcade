using System.Collections.Generic;

namespace MotionArcade.Games.Basketball
{
    // מחליט מה נחשב סל, בלי Unity, כדי שאפשר יהיה לבדוק את זה בבדיקות EditMode.
    // שני חיישנים: אחד ממש מעל הטבעת ואחד מתחתיה. סל = הכדור נכנס לעליון בתנועה כלפי מטה,
    // ואחר כך לתחתון (עדיין כלפי מטה) תוך זמן קצר. כך לא נספר:
    // - כדור שעולה דרך הטבעת מלמטה,
    // - כדור שנגע בחיישן העליון, קפץ מהטבעת ונפל בחוץ,
    // - כדור שנופל לתחתון מהצד (מעבר לטבעת) בלי לעבור בעליון.
    public class BasketDetector
    {
        private readonly float windowSeconds;
        // כדור → הזמן שנכנס לעליון. קיבולת מראש: מילון ריק מקצה זיכרון בפעם הראשונה שכל כדור חדש נכנס,
        // וזה היה קורה באמצע סבב (נמדד ב-GcProbe)
        private readonly Dictionary<int, float> armedAt = new Dictionary<int, float>(16);

        public BasketDetector(float windowSeconds = 1f)
        {
            this.windowSeconds = windowSeconds;
        }

        // ballId: מזהה קבוע לכל כדור. verticalSpeed: שלילי = נופל.
        public void EnterTop(int ballId, float time, float verticalSpeed)
        {
            if (verticalSpeed < 0f) armedAt[ballId] = time;
            else armedAt.Remove(ballId);   // עולה דרך הטבעת: לא יכול להיות סל
        }

        // true = סל
        public bool EnterBottom(int ballId, float time, float verticalSpeed)
        {
            if (!armedAt.TryGetValue(ballId, out float armed)) return false;
            armedAt.Remove(ballId);
            return verticalSpeed < 0f && time - armed <= windowSeconds;
        }

        // כדור שחזר למגש או סבב חדש: מוחקים מצב ישן
        public void Forget(int ballId) => armedAt.Remove(ballId);
        public void Clear() => armedAt.Clear();
    }

    public static class BasketballScoring
    {
        public const int PointsPerBasket = 2;
        public const float BonusSeconds = 10f;   // השניות האחרונות של הסבב: ניקוד כפול, כמו במכונות ארקייד

        public static bool IsBonus(float timeLeft) => timeLeft > 0f && timeLeft <= BonusSeconds;

        public static int PointsFor(float timeLeft) => IsBonus(timeLeft) ? PointsPerBasket * 2 : PointsPerBasket;
    }
}
