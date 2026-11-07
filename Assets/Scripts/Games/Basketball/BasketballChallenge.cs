namespace MotionArcade.Games.Basketball
{
    public enum BasketballMode { Classic, TenIn30, FiveInARow, ThreeSwishes }

    // אתגרי הכדורסל (גיא: "להכניס 10 סלים בפחות מ-30 שניות" וכו'). קוד טהור: סופר סלים, החטאות וסווישים,
    // ואומר מתי האתגר הושלם. Classic הוא המשחק הרגיל (60 שניות, נקודות) ואין לו השלמה.
    public class BasketballChallenge
    {
        public struct Info
        {
            public string Name;          // על הכפתור
            public string Description;   // מתחת לכפתור
            public float Seconds;
            public int Target;           // 0 = בלי יעד (Classic)
        }

        public static Info For(BasketballMode mode)
        {
            switch (mode)
            {
                case BasketballMode.TenIn30:
                    return new Info { Name = "10 IN 30", Description = "Make 10 baskets\nbefore 30 seconds run out.", Seconds = 30f, Target = 10 };
                case BasketballMode.FiveInARow:
                    return new Info { Name = "5 IN A ROW", Description = "Make 5 baskets in a row.\nA miss starts the count again.", Seconds = 60f, Target = 5 };
                case BasketballMode.ThreeSwishes:
                    return new Info { Name = "3 SWISHES", Description = "Make 3 clean baskets that\ntouch neither the rim nor the board.", Seconds = 45f, Target = 3 };
                default:
                    return new Info { Name = "CLASSIC", Description = "60 seconds. 2 points a basket,\ndouble in the last 10 seconds.", Seconds = 60f, Target = 0 };
            }
        }

        public BasketballMode Mode { get; private set; }
        public Info Current { get; private set; }
        public int Baskets { get; private set; }
        public int Swishes { get; private set; }
        public int Streak { get; private set; }
        public int BestStreak { get; private set; }
        public bool Complete { get; private set; }
        public float CompletedAt { get; private set; }   // שניות מתחילת הסבב

        // ההתקדמות אל היעד (מה שמוצג: 7 / 10)
        public int Progress
        {
            get
            {
                switch (Mode)
                {
                    case BasketballMode.TenIn30: return Baskets;
                    case BasketballMode.FiveInARow: return Streak;
                    case BasketballMode.ThreeSwishes: return Swishes;
                    default: return Baskets;
                }
            }
        }

        public BasketballChallenge(BasketballMode mode = BasketballMode.Classic) => SetMode(mode);

        public void SetMode(BasketballMode mode)
        {
            Mode = mode;
            Current = For(mode);
            Reset();
        }

        public void Reset()
        {
            Baskets = Swishes = Streak = BestStreak = 0;
            Complete = false;
            CompletedAt = 0f;
        }

        // true = האתגר הושלם עכשיו
        public bool Basket(bool swish, float elapsed)
        {
            if (Complete) return false;
            Baskets++;
            if (swish) Swishes++;
            Streak++;
            if (Streak > BestStreak) BestStreak = Streak;
            if (Current.Target > 0 && Progress >= Current.Target)
            {
                Complete = true;
                CompletedAt = elapsed;
                return true;
            }
            return false;
        }

        // זריקה שנגמרה בלי סל (חזרה למגש או הלכה לאיבוד)
        public void Miss()
        {
            if (Complete) return;
            Streak = 0;
        }

        // שיא: זמן קצר יותר עדיף. Classic: ניקוד גבוה יותר עדיף
        public static bool Better(BasketballMode mode, float candidate, float best, bool hasBest) =>
            !hasBest || (mode == BasketballMode.Classic ? candidate > best : candidate < best);
    }
}
