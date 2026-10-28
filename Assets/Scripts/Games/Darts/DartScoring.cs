using System;

namespace MotionArcade.Games.Darts
{
    // פגיעה אחת בלוח. Number: 1-20, 25 לבול, 0 להחטאה. Multiplier: 1 / 2 / 3 (בול פנימי = 25 כפול).
    public readonly struct DartHit : IEquatable<DartHit>
    {
        public readonly int Number;
        public readonly int Multiplier;

        public DartHit(int number, int multiplier)
        {
            Number = number;
            Multiplier = multiplier;
        }

        public int Points => Number * Multiplier;
        public bool IsMiss => Number == 0;

        public static readonly DartHit Miss = new DartHit(0, 0);

        public bool Equals(DartHit other) => Number == other.Number && Multiplier == other.Multiplier;
        public override bool Equals(object obj) => obj is DartHit other && Equals(other);
        public override int GetHashCode() => Number * 4 + Multiplier;
        public override string ToString() => DartScoring.Label(this);
    }

    // ניקוד לפי המידות הרשמיות של לוח חצים (במטרים, מהמרכז), בלי Unity, כדי שאפשר יהיה לבדוק אותו.
    // אותה פונקציה גם מציירת את הטקסטורה של הלוח, כך שמה שרואים הוא בדיוק מה שנספר.
    public static class DartScoring
    {
        public const float InnerBull = 0.00635f;   // רדיוס בול (50)
        public const float OuterBull = 0.0159f;    // רדיוס הבול החיצוני (25)
        public const float TripleInner = 0.099f;
        public const float TripleOuter = 0.107f;
        public const float DoubleInner = 0.162f;
        public const float DoubleOuter = 0.170f;   // מעבר לזה: החטאה
        public const float BoardRadius = 0.2255f;  // הלוח כולו, כולל טבעת המספרים

        // המספרים בכיוון השעון, מ-20 למעלה. כל מקטע 18°.
        public static readonly int[] Order = { 20, 1, 18, 4, 13, 6, 10, 15, 2, 17, 3, 19, 7, 16, 8, 11, 14, 9, 12, 5 };

        // x ימינה ו-y למעלה, כמו שהשחקן רואה את הלוח
        public static DartHit Score(float x, float y)
        {
            float r = (float)Math.Sqrt(x * x + y * y);
            if (r <= InnerBull) return new DartHit(25, 2);
            if (r <= OuterBull) return new DartHit(25, 1);
            if (r > DoubleOuter) return DartHit.Miss;

            int number = Order[SegmentIndex(x, y)];
            if (r >= DoubleInner) return new DartHit(number, 2);
            if (r >= TripleInner && r <= TripleOuter) return new DartHit(number, 3);
            return new DartHit(number, 1);
        }

        // 0 = המקטע של 20 (למעלה), עולה בכיוון השעון
        public static int SegmentIndex(float x, float y)
        {
            double degrees = Math.Atan2(x, y) * 180.0 / Math.PI;   // 0 למעלה, חיובי לכיוון ימין
            double shifted = (degrees + 9.0 + 360.0) % 360.0;      // 20 מכסה ‎-9° עד +9°
            return (int)(shifted / 18.0) % 20;
        }

        // זווית מרכז המקטע של מספר, במעלות בכיוון השעון מלמעלה (למיקום המספרים סביב הלוח)
        public static float AngleOf(int number) => Array.IndexOf(Order, number) * 18f;

        // תוויות מחושבות פעם אחת: הצגת "T20" באמצע משחק לא בונה מחרוזת חדשה (בלי GC)
        private static readonly string[] Singles = BuildLabels("");
        private static readonly string[] Doubles = BuildLabels("D");
        private static readonly string[] Triples = BuildLabels("T");

        private static string[] BuildLabels(string prefix)
        {
            var labels = new string[21];
            for (int i = 1; i <= 20; i++) labels[i] = prefix + i;
            return labels;
        }

        public static string Label(DartHit hit)
        {
            if (hit.IsMiss) return "MISS";
            if (hit.Number == 25) return hit.Multiplier == 2 ? "BULL" : "25";
            switch (hit.Multiplier)
            {
                case 2: return Doubles[hit.Number];
                case 3: return Triples[hit.Number];
                default: return Singles[hit.Number];
            }
        }
    }

    // Around the Clock: 1, 2, ... 20 ואז בול. כל פגיעה במספר הנוכחי (יחיד, כפול או משולש) מתקדמת אחד.
    // ניקוד = כמה מטרות הושלמו (עד 21).
    public class AroundTheClock
    {
        public const int Bull = 25;
        public const int TargetCount = 21;

        public int Completed { get; private set; }
        public bool IsFinished => Completed >= TargetCount;

        // המטרה הנוכחית: 1-20, ואחריהן 25 (בול). אחרי הסיום: 0.
        public int Target => IsFinished ? 0 : Completed < 20 ? Completed + 1 : Bull;

        // true = הפגיעה קידמה את השחקן
        public bool Register(DartHit hit)
        {
            if (IsFinished || hit.IsMiss || hit.Number != Target) return false;
            Completed++;
            return true;
        }

        public void Reset() => Completed = 0;
    }
}
