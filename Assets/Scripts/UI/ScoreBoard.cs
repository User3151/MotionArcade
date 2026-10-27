using MotionArcade.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MotionArcade.UI
{
    // הלוח של כל משחק: שם, הסבר, ספירה לאחור, זמן, ניקוד ותוצאה, עם START ו-BACK.
    // לא יודע כלום על המשחק עצמו, רק על GameStation, אז אותו לוח משמש את כל המשחקים.
    public class ScoreBoard : MonoBehaviour
    {
        [SerializeField] private GameStation station;

        [Header("Text")]
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text info;      // הסבר ב-Intro, תוצאה ב-Results
        [SerializeField] private TMP_Text clock;     // 3-2-1 בספירה, שניות שנשארו במשחק
        [SerializeField] private TMP_Text score;

        [Header("Buttons")]
        [SerializeField] private Button startButton;
        [SerializeField] private TMP_Text startLabel;
        [SerializeField] private Button backButton;

        private int shownSeconds = -1;

        private void OnEnable()
        {
            station.PhaseChanged += HandlePhase;
            station.ScoreChanged += HandleScore;
            startButton.onClick.AddListener(station.StartRound);
            backButton.onClick.AddListener(Back);
        }

        private void OnDisable()
        {
            station.PhaseChanged -= HandlePhase;
            station.ScoreChanged -= HandleScore;
            startButton.onClick.RemoveListener(station.StartRound);
            backButton.onClick.RemoveListener(Back);
        }

        // ב-Start ולא ב-OnEnable: השעון של GameStation נוצר ב-Awake שלה, שיכול לרוץ אחרי ה-OnEnable כאן
        private void Start()
        {
            title.text = station.DisplayName;
            HandlePhase(station.Phase);
            HandleScore(station.Score);
        }

        private void Update()
        {
            RoundPhase phase = station.Phase;
            if (phase != RoundPhase.Countdown && phase != RoundPhase.Playing) return;

            // מעגלים למעלה: "3" כל עוד נשארו יותר משתי שניות. מעדכנים רק כשהמספר משתנה.
            int seconds = Mathf.CeilToInt(station.TimeLeft);
            if (seconds == shownSeconds) return;
            shownSeconds = seconds;
            clock.SetText("{0}", seconds);   // SetText עם מספר לא יוצר מחרוזת חדשה (בלי GC)
        }

        private void HandlePhase(RoundPhase phase)
        {
            shownSeconds = -1;
            bool idle = phase == RoundPhase.Intro || phase == RoundPhase.Results;
            startButton.gameObject.SetActive(idle);
            backButton.gameObject.SetActive(idle);
            clock.gameObject.SetActive(!idle);

            switch (phase)
            {
                case RoundPhase.Intro:
                    info.text = station.Instructions;
                    startLabel.text = "START";
                    break;
                case RoundPhase.Countdown:
                    info.text = "Get ready";
                    break;
                case RoundPhase.Playing:
                    info.text = "";
                    break;
                case RoundPhase.Results:
                    info.text = ResultText();
                    startLabel.text = "PLAY AGAIN";
                    break;
            }
        }

        private void HandleScore(int value) => score.SetText("{0}", value);

        private string ResultText()
        {
            string text = $"Time's up!\nScore: {station.Score}";
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

        private void Back()
        {
            if (SceneLoader.Instance != null) SceneLoader.Instance.ReturnToHub();
            else Debug.LogWarning("[ScoreBoard] No SceneLoader: open the Hub scene and load the game from there.", this);
        }
    }
}
