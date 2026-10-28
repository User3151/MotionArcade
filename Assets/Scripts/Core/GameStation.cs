using System;
using UnityEngine;
using UnityEngine.Events;

namespace MotionArcade.Core
{
    // הבסיס של כל משחק: מחזור Intro → Countdown → Playing → Results, טיימר וניקוד.
    // כל משחק יורש ממנה וממלא רק את מה שמיוחד לו (OnPlayingStarted / OnPlayingEnded / OnPlayingUpdate),
    // כך שלוח התוצאות, הדיווח לסשן והכפתורים עובדים אותו דבר בכל המשחקים.
    // משחק שדורס Awake / Update / OnDestroy חייב לקרוא ל-base. OnPlayingEnded יכול לרוץ גם בזמן פריקת הסצנה,
    // כשחלק מהאובייקטים כבר נהרסו, ולכן הוא צריך לבדוק null לפני שהוא נוגע בהם.
    public abstract class GameStation : MonoBehaviour
    {
        [Header("Game")]
        [SerializeField] private string gameId = "game";               // מפתח קבוע לדוחות ולשיאים
        [SerializeField] private string displayName = "Game";
        [SerializeField, TextArea(2, 5)] private string instructions = "";

        [Header("Round")]
        [SerializeField, Min(0f)] private float countdownSeconds = 3f;
        [SerializeField, Min(1f)] private float roundSeconds = 60f;

        [Header("Events")]
        public UnityEvent onRoundStarted;          // תחילת Playing
        public UnityEvent<int> onRoundEnded;       // סוף Playing, עם הניקוד

        private RoundClock clock;

        public string GameId => gameId;
        public string DisplayName => displayName;
        public string Instructions => instructions;
        public RoundPhase Phase => clock.Phase;
        public float TimeLeft => clock.TimeLeft;
        public float RoundSeconds => clock.RoundSeconds;
        public int Score { get; private set; }

        // שורה קצרה שהלוח מציג בזמן משחק, למשל "DOUBLE POINTS!" בכדורסל. null = כלום.
        // הלוח קורא אותה פעם בשנייה, אז היא צריכה להחזיר מחרוזת קבועה ולא לבנות חדשה (בלי GC).
        public virtual string PlayingHint => null;

        public event Action<RoundPhase> PhaseChanged;
        public event Action<int> ScoreChanged;

        protected virtual void Awake()
        {
            clock = new RoundClock(countdownSeconds, roundSeconds);
            clock.PhaseChanged += HandlePhaseChanged;
        }

        protected virtual void Update()
        {
            clock.Tick(Time.deltaTime);
            if (clock.Phase == RoundPhase.Playing) OnPlayingUpdate();
        }

        // הסצנה נפרקת באמצע סבב (חזרה לאולם): סוגרים את הסבב בלי לדווח עליו
        protected virtual void OnDestroy()
        {
            if (clock == null) return;
            clock.PhaseChanged -= HandlePhaseChanged;
            if (clock.Phase == RoundPhase.Playing) OnPlayingEnded();
        }

        // כפתור START בלוח. מ-Intro או מ-Results בלבד.
        public void StartRound() => clock.Start();

        // חזרה ל-Intro בלי תוצאה. סבב שנקטע באמצע לא נרשם.
        public void AbortRound() => clock.Abort();

        // נקודות נספרות רק בזמן משחק, כדי שכדור שנכנס בספירה לאחור או אחרי הצפירה לא ייחשב
        protected bool AddScore(int points)
        {
            if (clock.Phase != RoundPhase.Playing || points == 0) return false;
            Score += points;
            ScoreChanged?.Invoke(Score);
            return true;
        }

        private void HandlePhaseChanged(RoundPhase previous, RoundPhase next)
        {
            if (previous == RoundPhase.Playing) OnPlayingEnded();

            // ניקוד מתאפס בתחילת סבב, וגם ב-STOP (חזרה ל-Intro): אחרת הלוח מציג נקודות של סבב שלא נספר
            if (next == RoundPhase.Intro || next == RoundPhase.Countdown || (next == RoundPhase.Playing && previous != RoundPhase.Countdown))
            {
                Score = 0;
                ScoreChanged?.Invoke(Score);
            }

            if (next == RoundPhase.Playing)
            {
                OnPlayingStarted();
                onRoundStarted?.Invoke();
            }
            else if (next == RoundPhase.Results)
            {
                var result = new RoundResult(gameId, Score, clock.RoundSeconds, DateTime.UtcNow);
                if (SessionManager.Instance != null) SessionManager.Instance.Record(result);
                onRoundEnded?.Invoke(Score);
            }

            OnPhaseEntered(next);
            PhaseChanged?.Invoke(next);
        }

        // מה שהמשחק עושה בתחילת הסבב (להפעיל מטרות, לפזר כדורים) ובסופו (לכבות, לנקות)
        protected abstract void OnPlayingStarted();
        protected abstract void OnPlayingEnded();
        protected virtual void OnPlayingUpdate() { }

        // כל מעבר שלב, כולל חזרה ל-Intro ב-STOP. בכדורסל: נועל את הכדורים מחוץ לסבב.
        // לא נקרא על ה-Intro ההתחלתי (אין מעבר), אז משחק שצריך מצב התחלתי מגדיר אותו ב-Awake.
        protected virtual void OnPhaseEntered(RoundPhase phase) { }

        // true מרגע START ועד סוף הסבב: ספירה לאחור או משחק
        public bool IsRunning => Phase == RoundPhase.Countdown || Phase == RoundPhase.Playing;
    }
}
