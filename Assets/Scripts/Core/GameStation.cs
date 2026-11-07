using System;
using System.Collections.Generic;
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

        // כל המשחקים טעונים יחד בארקייד. AnyRunning: יש סבב פעיל באחד מהם (אז נועלים את ההליכה, כדי לא לזוז בטעות)
        private static readonly List<GameStation> stations = new List<GameStation>();
        public static IReadOnlyList<GameStation> All => stations;
        public static bool AnyRunning
        {
            get
            {
                for (int i = 0; i < stations.Count; i++) if (stations[i].IsRunning) return true;
                return false;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => stations.Clear();

        public string GameId => gameId;
        public string DisplayName => displayName;
        public string Instructions => instructions;
        public RoundPhase Phase => clock.Phase;
        public float TimeLeft => clock.TimeLeft;
        public float RoundSeconds => clock.RoundSeconds;
        public int Score { get; private set; }
        public float PlayedSeconds { get; private set; }   // כמה זמן נמשך הסבב האחרון (גם אם הסתיים מוקדם)

        // ===== שיא אישי (גיא: בכל משחק, בסוף כל סבב "Best score", ומתעדכן כששוברים אותו) =====
        // נשמר ב-PlayerPrefs, כך שהוא נשאר בין הפעלות. כל משחק קובע מה נחשב שיא (גבוה יותר = טוב יותר) ואיך כותבים אותו.
        private const string BestPrefix = "best.";
        public string BestLine { get; private set; }    // לשורת התוצאה: "NEW BEST SCORE!" או "Best score: ..."
        public bool NewBest { get; private set; }

        // מפתח השיא: משחק עם כמה מצבים (רמות, מסלולים, אתגרים) שומר שיא נפרד לכל אחד
        protected virtual string RecordKey => gameId;

        // השיא של הסבב שנגמר: ערך להשוואה (גבוה = טוב) וטקסט להצגה. false = הסבב הזה לא קובע שיא
        protected virtual bool TryGetRecord(out float value, out string text)
        {
            value = Score;
            text = Score == 1 ? "1 point" : Score + " points";
            return Score > 0;
        }

        // השיא השמור למצב הנוכחי (למשל ללוח לפני סבב), או null
        public string BestText => PlayerPrefs.HasKey(BestPrefix + RecordKey) ? PlayerPrefs.GetString(BestPrefix + RecordKey + ".text", "") : null;

        private void UpdateBest()
        {
            string key = BestPrefix + RecordKey;
            bool has = PlayerPrefs.HasKey(key);
            float best = PlayerPrefs.GetFloat(key, 0f);
            NewBest = false;
            if (TryGetRecord(out float value, out string text) && (!has || value > best))
            {
                PlayerPrefs.SetFloat(key, value);
                PlayerPrefs.SetString(key + ".text", text);
                PlayerPrefs.Save();
                NewBest = true;
            }
            string stored = BestText;
            BestLine = NewBest ? "NEW BEST SCORE!  " + stored : stored != null ? "Best score: " + stored : null;
        }

        // שורה קצרה שהלוח מציג בזמן משחק, למשל "DOUBLE POINTS!" בכדורסל. null = כלום.
        // הלוח קורא אותה פעם בשנייה, אז היא צריכה להחזיר מחרוזת קבועה ולא לבנות חדשה (בלי GC).
        public virtual string PlayingHint => null;

        // משחק בלי שעון (הוקי: עד 7 גולים): הלוח מציג רק את הספירה לאחור, ובזמן משחק מסתיר את השעון.
        // roundSeconds של משחק כזה הוא רק תקרה, שסבב לא יימשך לנצח.
        public virtual bool HasTimer => true;

        // שורות נוספות למסך התוצאה (ב-Whack: פגיעות, החטאות, זמן תגובה). נבנה פעם אחת בסוף הסבב.
        public virtual string ResultDetails => null;

        public event Action<RoundPhase> PhaseChanged;
        public event Action<int> ScoreChanged;

        protected virtual void Awake()
        {
            clock = new RoundClock(countdownSeconds, roundSeconds);
            clock.PhaseChanged += HandlePhaseChanged;
            stations.Add(this);
        }

        protected virtual void Update()
        {
            clock.Tick(Time.deltaTime);
            if (clock.Phase == RoundPhase.Playing)
            {
                PlayedSeconds += Time.deltaTime;
                OnPlayingUpdate();
            }
        }

        // הסצנה נפרקת באמצע סבב (חזרה לאולם): סוגרים את הסבב בלי לדווח עליו
        protected virtual void OnDestroy()
        {
            stations.Remove(this);
            if (clock == null) return;
            clock.PhaseChanged -= HandlePhaseChanged;
            if (clock.Phase == RoundPhase.Playing) OnPlayingEnded();
        }

        // אורך הסבב הבא (רק בין סבבים)
        protected bool SetRoundSeconds(float seconds) => clock.SetRoundSeconds(seconds);

        // כפתור START בלוח. מ-Intro או מ-Results בלבד.
        public void StartRound() => clock.Start();

        // חזרה ל-Intro בלי תוצאה. סבב שנקטע באמצע לא נרשם.
        public void AbortRound() => clock.Abort();

        // המשחק הושלם לפני הזמן: עוברים ל-Results עכשיו, והסבב נרשם
        protected bool FinishRound()
        {
            if (clock.Phase != RoundPhase.Playing) return false;
            FinishedEarly = true;   // לפני המעבר, כדי שהלוח יראה אותו כשהוא מציג את התוצאה
            return clock.Finish();
        }

        // הסבב האחרון הושלם לפני הזמן (הלוח כותב "Complete!" במקום "Time's up!")
        public bool FinishedEarly { get; private set; }

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
                FinishedEarly = false;
                Score = 0;
                ScoreChanged?.Invoke(Score);
            }

            if (next == RoundPhase.Results) UpdateBest();   // אחרי OnPlayingEnded (המשחק סיכם את הסבב), לפני שהלוח מתעדכן

            if (next == RoundPhase.Playing)
            {
                PlayedSeconds = 0f;
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
