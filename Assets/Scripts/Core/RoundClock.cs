using System;

namespace MotionArcade.Core
{
    // השלבים של סבב, אותו דבר בכל המשחקים
    public enum RoundPhase
    {
        Intro,       // הסבר וכפתור START. מחכים לשחקן
        Countdown,   // 3, 2, 1: זמן להתייצב לפני שהשעון רץ
        Playing,     // הסבב עצמו. רק כאן נצברות נקודות
        Results,     // התוצאה מוצגת עד שהשחקן מתחיל שוב או חוזר לאולם
    }

    // השעון של סבב, בלי שום תלות ב-Unity: מקבל זמן שעבר ומחליט מתי עוברים שלב.
    // מופרד מ-GameStation כדי שאפשר יהיה לבדוק אותו בבדיקות EditMode בלי סצנה.
    public class RoundClock
    {
        public RoundPhase Phase { get; private set; } = RoundPhase.Intro;
        public float CountdownSeconds { get; }
        public float RoundSeconds { get; }

        // זמן שנשאר לשלב הנוכחי. ב-Intro וב-Results אין זמן, ולכן 0.
        public float TimeLeft { get; private set; }

        // השלב הקודם והחדש. נקרא אחרי שהשלב כבר התחלף.
        public event Action<RoundPhase, RoundPhase> PhaseChanged;

        public RoundClock(float countdownSeconds, float roundSeconds)
        {
            if (roundSeconds <= 0f) throw new ArgumentOutOfRangeException(nameof(roundSeconds), "a round must last more than 0 seconds");
            CountdownSeconds = Math.Max(0f, countdownSeconds);
            RoundSeconds = roundSeconds;
        }

        // START: מתחילים ספירה לאחור. מותר מ-Intro ומ-Results; באמצע סבב לא עושים כלום.
        public bool Start()
        {
            if (Phase != RoundPhase.Intro && Phase != RoundPhase.Results) return false;

            if (CountdownSeconds > 0f) Enter(RoundPhase.Countdown, CountdownSeconds);
            else Enter(RoundPhase.Playing, RoundSeconds);
            return true;
        }

        // יציאה באמצע (חזרה לאולם): בלי Results, כי סבב שנקטע לא נחשב
        public void Abort()
        {
            if (Phase == RoundPhase.Intro) return;
            Enter(RoundPhase.Intro, 0f);
        }

        // נקרא בכל פריים. פריים ארוך (עצירה של המשחק, טעינה) לא מדלג על שלב:
        // ספירה לאחור שנגמרת עוברת ל-Playing, והזמן העודף נגרע כבר מהסבב.
        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds <= 0f) return;
            if (Phase != RoundPhase.Countdown && Phase != RoundPhase.Playing) return;

            TimeLeft -= deltaSeconds;
            if (TimeLeft > 0f) return;

            if (Phase == RoundPhase.Countdown)
            {
                float overflow = -TimeLeft;
                Enter(RoundPhase.Playing, RoundSeconds);
                Tick(overflow);
            }
            else
            {
                Enter(RoundPhase.Results, 0f);
            }
        }

        private void Enter(RoundPhase next, float seconds)
        {
            RoundPhase previous = Phase;
            Phase = next;
            TimeLeft = seconds;
            PhaseChanged?.Invoke(previous, next);
        }
    }
}
