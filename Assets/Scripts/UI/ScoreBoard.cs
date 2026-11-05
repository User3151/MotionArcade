using MotionArcade.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MotionArcade.UI
{
    // הלוח של כל משחק: שם, הסבר, ספירה לאחור, זמן, ניקוד ותוצאה, עם START / STOP.
    // (BACK הוסר: בארקייד יוצאים ממשחק פשוט כשהולכים מהעיגול שלו, ראו StationPresence.)
    // לא יודע כלום על המשחק עצמו, רק על GameStation, אז אותו לוח משמש את כל המשחקים.
    // הכפתורים יכולים לשבת בקנבס אחר מהתצוגה (בכדורסל: לוח גבוה מעל הסל, כפתורים ליד השחקן).
    public class ScoreBoard : MonoBehaviour
    {
        [SerializeField] private GameStation station;

        [Header("Text")]
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text info;      // הסבר ב-Intro, רמז בזמן משחק, תוצאה ב-Results
        [SerializeField] private TMP_Text clock;     // 3-2-1 בספירה, שניות שנשארו במשחק
        [SerializeField] private TMP_Text score;
        [SerializeField] private TMP_Text displayHint;   // אופציונלי: הרמז גם בתצוגה הגדולה, שהשחקן מסתכל עליה כל הזמן

        [Header("Buttons")]
        [SerializeField] private Button startButton;
        [SerializeField] private TMP_Text startLabel;
        [SerializeField] private Button stopButton;   // רק בזמן ספירה ומשחק: עוצר בלי לרשום את הסבב

        [Header("Sound (optional)")]
        [SerializeField] private AudioSource sfx;
        [SerializeField] private AudioClip countdownTick;
        [SerializeField] private AudioClip roundStart;
        [SerializeField] private AudioClip roundEnd;

        private int shownSeconds = -1;
        private string shownHint;

        private void OnEnable()
        {
            station.PhaseChanged += HandlePhase;
            station.ScoreChanged += HandleScore;
            startButton.onClick.AddListener(station.StartRound);
            if (stopButton != null) stopButton.onClick.AddListener(station.AbortRound);
        }

        private void OnDisable()
        {
            station.PhaseChanged -= HandlePhase;
            station.ScoreChanged -= HandleScore;
            startButton.onClick.RemoveListener(station.StartRound);
            if (stopButton != null) stopButton.onClick.RemoveListener(station.AbortRound);
        }

        // ב-Start ולא ב-OnEnable: השעון של GameStation נוצר ב-Awake שלה, שיכול לרוץ אחרי ה-OnEnable כאן
        private void Start()
        {
            if (title != null) title.text = station.DisplayName;
            HandlePhase(station.Phase);
            HandleScore(station.Score);
        }

        private void Update()
        {
            RoundPhase phase = station.Phase;
            if (phase != RoundPhase.Countdown && phase != RoundPhase.Playing) return;
            if (phase == RoundPhase.Playing && !station.HasTimer) return;

            // מעגלים למעלה: "3" כל עוד נשארו יותר משתי שניות. מעדכנים רק כשהמספר משתנה.
            int seconds = Mathf.CeilToInt(station.TimeLeft);
            if (seconds == shownSeconds) return;
            shownSeconds = seconds;
            clock.SetText("{0}", seconds);   // SetText עם מספר לא יוצר מחרוזת חדשה (בלי GC)

            if (phase == RoundPhase.Countdown) Play(countdownTick);
            else ShowHint(station.PlayingHint);
        }

        private void HandlePhase(RoundPhase phase)
        {
            shownSeconds = -1;
            bool running = phase == RoundPhase.Countdown || phase == RoundPhase.Playing;
            startButton.gameObject.SetActive(!running);
            if (stopButton != null) stopButton.gameObject.SetActive(running);
            clock.gameObject.SetActive(phase == RoundPhase.Countdown || (running && station.HasTimer));

            switch (phase)
            {
                case RoundPhase.Intro:
                    if (displayHint != null) displayHint.text = "";
                    info.text = station.Instructions;
                    startLabel.text = "START";
                    break;
                case RoundPhase.Countdown:
                    info.text = "Get ready";
                    break;
                case RoundPhase.Playing:
                    shownHint = null;
                    info.text = "";
                    ShowHint(station.PlayingHint);
                    Play(roundStart);
                    break;
                case RoundPhase.Results:
                    if (displayHint != null) displayHint.text = "";
                    info.text = ResultText();
                    startLabel.text = "PLAY AGAIN";
                    Play(roundEnd);
                    break;
            }
        }

        // השוואה לפי הפניה: המשחק מחזיר מחרוזות קבועות, אז אין כאן השוואת תווים ואין הקצאה
        private void ShowHint(string hint)
        {
            if ((object)hint == shownHint) return;
            shownHint = hint;
            info.text = hint ?? "";
            if (displayHint != null) displayHint.text = hint ?? "";
        }

        private void HandleScore(int value)
        {
            if (score != null) score.SetText("{0}", value);   // ריק במשחק שמציג ניקוד משלו (הוקי)
        }

        private void Play(AudioClip clip)
        {
            if (sfx != null && clip != null) sfx.PlayOneShot(clip);
        }

        private string ResultText()
        {
            string text = (station.FinishedEarly ? "Complete!" : "Time's up!") + $"\nScore: {station.Score}";
            string details = station.ResultDetails;
            if (!string.IsNullOrEmpty(details)) text += "\n" + details;
            SessionManager session = SessionManager.Instance;
            if (session == null) return text;

            // הסבב הזה כבר נרשם לפני שהלוח מתעדכן, אז משווים רק לסבבים שלפניו
            var rounds = session.Log.Rounds;
            bool hasEarlier = false;
            int earlierBest = 0;
            for (int i = 0; i < rounds.Count - 1; i++)
            {
                if (rounds[i].GameId != station.GameId) continue;
                if (!hasEarlier || rounds[i].Score > earlierBest) earlierBest = rounds[i].Score;
                hasEarlier = true;
            }
            if (hasEarlier)
                text += station.Score > earlierBest ? "\nNew session best!" : $"\nSession best: {earlierBest}";
            return text;
        }

    }
}
