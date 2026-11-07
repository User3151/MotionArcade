using MotionArcade.Core;
using MotionArcade.Diagnostics;
using MotionArcade.Feedback;
using MotionArcade.Interaction;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MotionArcade.Games.Basketball
{
    // כדורסל ארקייד, כמו מכונת Pop-A-Shot: מגש כדורים מול השחקן, מסילה משופעת שמחזירה כל כדור למגש,
    // ורשתות בצדדים. 60 שניות, 2 נקודות לסל, כפול ב-10 השניות האחרונות.
    // הכדורים נעולים עד START (כמו במכונה אמיתית), כדי שהשחקן לא יתחיל לזרוק בלי לשים לב שהסבב לא התחיל.
    // אתגרים (גיא): כפתור MODE בין סבבים: CLASSIC, 10 IN 30, 5 IN A ROW, 3 SWISHES. אתגר שהושלם מסיים את הסבב,
    // והשיא (הזמן הכי קצר) נשמר. החוקים עצמם ב-BasketballChallenge.
    public class BasketballGame : GameStation
    {
        private const string BonusHint = "DOUBLE POINTS!";

        [Header("Balls")]
        [SerializeField] private Ball[] balls;
        [SerializeField] private Transform[] traySlots;      // לאן מחזירים כדור (מעל המגש)
        [SerializeField] private LayerMask ballLayers = ~0;

        [Header("Hoop")]
        [SerializeField] private Transform ring;                // מרכז הטבעת, y למעלה
        [SerializeField] private float ringRadius = 0.229f;     // עד מרכז הצינור של הטבעת (רדיוס חוקי 22.9 ס"מ)

        // במרחב של המשחק (ה-Transform שלו), לא של העולם: המשחק עומד בפינה שלו בארקייד, ואפשר להזיז אותו
        [Header("Machine bounds (game space)")]
        [SerializeField] private Bounds machine;   // כל הנפח של המכונה, כולל מעל הסל. כדור מחוץ לזה = הלך לאיבוד
        [SerializeField] private Bounds tray;      // המגש: כדור שעומד כאן במקום שלו, לא "תקוע"

        [Header("Feedback")]
        [SerializeField] private AudioSource hoopAudio;
        [SerializeField] private AudioClip swishClip;      // הכדור עובר ברשת
        [SerializeField] private AudioSource crowd;        // הקהל מסביב (בלי מיקום)
        [SerializeField] private AudioClip[] cheerClips;
        [SerializeField] private ScorePopup popup;
        [SerializeField] private ThrowRecorder recorder;   // מדידת זריקות (פיתוח בלבד)
        [SerializeField] private HoopAssist assist;        // עזרת כיוון לכל הכדורים

        [Header("Challenges")]
        [SerializeField] private Button modeButton;
        [SerializeField] private TMP_Text modeLabel;
        [SerializeField] private TMP_Text modeInfo;
        [SerializeField] private TMP_Text bestText;

        private const string ModeKey = "basketball.mode";
        private readonly BasketballChallenge challenge = new BasketballChallenge();
        private string[][] progressHints;   // [mode][progress]: "7 / 10", מחושב פעם אחת (בלי GC באמצע סבב)
        private string resultDetails;
        private float playStart;

        private readonly Collider[] overlap = new Collider[8];   // מוקצה פעם אחת: בדיקת מקום פנוי במגש בלי GC

        public BasketballChallenge Challenge => challenge;
        public BasketballMode Mode => challenge.Mode;
        public override string ResultDetails => resultDetails;

        public override string PlayingHint
        {
            get
            {
                if (Phase != RoundPhase.Playing) return null;
                if (challenge.Mode == BasketballMode.Classic) return BasketballScoring.IsBonus(TimeLeft) ? BonusHint : null;
                string[] hints = progressHints[(int)challenge.Mode];
                return hints[System.Math.Min(challenge.Progress, hints.Length - 1)];
            }
        }

        protected override void Awake()
        {
            base.Awake();
            foreach (Ball ball in balls) ball.Init(this);
            // Unity null (שדה ריק) הוא לא null אמיתי לממשק: מעבירים null מפורש
            IThrowAssist hoop = assist != null ? assist : null;
            foreach (Ball ball in balls)
                if (ball.TryGetComponent(out ControllerThrow thrower)) thrower.Assist = hoop;

            progressHints = new string[4][];
            for (int m = 0; m < 4; m++)
            {
                var info = BasketballChallenge.For((BasketballMode)m);
                string prefix = (BasketballMode)m == BasketballMode.FiveInARow ? "IN A ROW " : (BasketballMode)m == BasketballMode.ThreeSwishes ? "SWISHES " : "BASKETS ";
                progressHints[m] = new string[info.Target + 1];
                for (int p = 0; p <= info.Target; p++) progressHints[m][p] = prefix + p + " / " + info.Target;
            }
            SetMode((BasketballMode)Mathf.Clamp(PlayerPrefs.GetInt(ModeKey, 0), 0, 3));
        }

        private void OnEnable() { if (modeButton != null) modeButton.onClick.AddListener(NextMode); }
        private void OnDisable() { if (modeButton != null) modeButton.onClick.RemoveListener(NextMode); }

        private void NextMode()
        {
            if (IsRunning) return;
            SetMode((BasketballMode)(((int)challenge.Mode + 1) % 4));
            PlayerPrefs.SetInt(ModeKey, (int)challenge.Mode);
        }

        public void SetMode(BasketballMode mode)
        {
            if (IsRunning) return;
            challenge.SetMode(mode);
            SetRoundSeconds(challenge.Current.Seconds);
            ShowMode();
        }

        private string BestKey => "basketball.best." + challenge.Mode;

        private void ShowMode()
        {
            if (modeLabel != null) modeLabel.text = "<  " + challenge.Current.Name + "  >";
            if (modeInfo != null) modeInfo.text = challenge.Current.Description;
            if (modeButton != null) modeButton.interactable = !IsRunning;
            if (bestText != null)
            {
                bool has = PlayerPrefs.HasKey(BestKey);
                float best = PlayerPrefs.GetFloat(BestKey, 0f);
                bestText.text = !has ? "No record yet"
                    : challenge.Mode == BasketballMode.Classic ? "Best: " + Mathf.RoundToInt(best) + " points"
                    : "Best: " + best.ToString("0.0") + " s";
            }
        }

        // ב-Start ולא ב-Awake: כל כדור מוצא את ה-XRGrabInteractable שלו ב-Awake שלו, שאולי עוד לא רץ
        private void Start() => SetBallsGrabbable(IsRunning);   // מתחילים ב-Intro: נעולים

        // נפתחים ב-START (כבר בספירה לאחור, כדי להיות מוכנים), ננעלים בסוף הסבב וב-STOP.
        // ב-START גם כל הכדורים חוזרים למגש, כך שהם זמינים כבר בספירה (כמו בחצים).
        protected override void OnPhaseEntered(RoundPhase phase)
        {
            SetBallsGrabbable(IsRunning);
            if (phase == RoundPhase.Countdown) { ResetBalls(); challenge.Reset(); resultDetails = null; }
            ShowMode();
        }

        private void SetBallsGrabbable(bool value)
        {
            foreach (Ball ball in balls)
                if (ball != null) ball.SetGrabbable(value);
        }

        protected override void OnPlayingStarted()
        {
            challenge.Reset();
            playStart = Time.time;
        }

        private void ResetBalls()
        {
            // כל כדור שלא ביד מקבל משבצת משלו במגש, כדי שהסבב יתחיל עם כל הכדורים זמינים.
            // לא בודקים אם המשבצת פנויה: כל הכדורים זזים עכשיו, אז הם לא יכולים לחסום זה את זה.
            int slot = 0;
            foreach (Ball ball in balls)
            {
                if (ball == null || ball.IsHeld) continue;
                if (slot < traySlots.Length) ball.Teleport(traySlots[slot++].position);
                else ball.Teleport(DropAboveTray());
            }
            Physics.SyncTransforms();
        }

        protected override void OnPlayingEnded()
        {
            if (challenge == null) return;
            var info = challenge.Current;
            bool has = PlayerPrefs.HasKey(BestKey);
            float best = PlayerPrefs.GetFloat(BestKey, 0f);
            if (challenge.Mode == BasketballMode.Classic)
            {
                bool record = BasketballChallenge.Better(BasketballMode.Classic, Score, best, has) && Score > 0;
                if (record) PlayerPrefs.SetFloat(BestKey, Score);
                resultDetails = record ? "New record!" : has ? "Best: " + Mathf.RoundToInt(best) + " points" : null;
                return;
            }
            if (challenge.Complete)
            {
                bool record = BasketballChallenge.Better(challenge.Mode, challenge.CompletedAt, best, has);
                if (record) PlayerPrefs.SetFloat(BestKey, challenge.CompletedAt);
                resultDetails = "CHALLENGE COMPLETE!\n" + info.Name + " in " + challenge.CompletedAt.ToString("0.0") + " s"
                    + (record ? "\nNew record!" : "\nBest: " + best.ToString("0.0") + " s");
            }
            else
            {
                resultDetails = "Not this time: " + challenge.Progress + " of " + info.Target
                    + (challenge.Mode == BasketballMode.FiveInARow ? " (best run " + challenge.BestStreak + ")" : "")
                    + "\nTry again!";
            }
        }

        // כדור נזרק ונגמר בלי סל
        public void Missed(Ball ball)
        {
            if (Phase == RoundPhase.Playing) challenge.Miss();
        }

        public bool InsideMachine(Vector3 position) => machine.Contains(transform.InverseTransformPoint(position));
        public bool InTray(Vector3 position) => tray.Contains(transform.InverseTransformPoint(position));

        // נקרא מכל כדור שלא ביד, בכל צעד פיזיקה, עם המרכז שלו בצעד הקודם ובנוכחי.
        // true = הזריקה הזו נגמרה מבחינת סל: נספר סל, או שהכדור עלה דרך הטבעת מלמטה (אז הירידה שאחרי לא נספרת).
        public bool CheckRing(Ball ball, Vector3 previous, Vector3 current)
        {
            BasketDetector.Crossing crossing = BasketDetector.Through(
                ring.InverseTransformPoint(previous), ring.InverseTransformPoint(current), ringRadius);
            if (crossing == BasketDetector.Crossing.None) return false;
            if (crossing == BasketDetector.Crossing.Down) Basket(ball);
            return true;
        }

        private void Basket(Ball ball)
        {
            if (recorder != null && ball.TouchedHoop) recorder.NoteResult(ball.gameObject, "BASKET");

            ball.MarkScored();
            // באתגרים כל סל = נקודה אחת (הלוח מראה כמה סלים); ב-Classic: 2, כפול בסוף
            int points = challenge.Mode == BasketballMode.Classic ? BasketballScoring.PointsFor(TimeLeft) : 1;
            bool counted = AddScore(points);
            bool completed = counted && challenge.Basket(!ball.TouchedHoop, Time.time - playStart);

            // כל סל עובר ברשת. סוויש (בלי לגעת בטבעת או בלוח): הרשת נשמעת חזק יותר והקהל מריע יותר.
            // אחרי נגיעה הצלצול של הטבעת כבר נשמע (ImpactSound), והרשת חלשה יותר.
            bool swish = !ball.TouchedHoop;
            if (hoopAudio != null && swishClip != null) hoopAudio.PlayOneShot(swishClip, swish ? 1f : 0.6f);
            if (crowd != null && cheerClips != null && cheerClips.Length > 0)
                crowd.PlayOneShot(cheerClips[Random.Range(0, cheerClips.Length)], swish ? 1f : 0.7f);
            if (recorder != null && swish) recorder.NoteResult(ball.gameObject, "SWISH");
            Haptics.Pulse(ball.LastHolder, Haptics.Strong, 0.15f);
            if (counted && popup != null) popup.Show(points);
            if (completed) FinishRound();
        }

        // מחזיר כדור לנקודה פנויה מעל המגש. כדור שמוצב בתוך כדור אחר נזרק על ידי הפיזיקה, לכן בודקים קודם.
        public void ReturnBall(Ball ball)
        {
            Vector3 target = DropAboveTray();   // אם אין משבצת פנויה
            for (int i = 0; i < traySlots.Length; i++)
            {
                if (!IsFree(traySlots[i].position, ball)) continue;
                target = traySlots[i].position;
                break;
            }
            ball.Teleport(target);
            // בלי זה בדיקת המקום הפנוי הבאה (באותו פריים, למשל ב-START) לא רואה את הכדור שהוזז עכשיו,
            // ושני כדורים נשלחים לאותה משבצת
            Physics.SyncTransforms();
        }

        // המגש מלא: מפילים מעל מרכז המגש עם הסטה קטנה לצד. בלי ההסטה כדור יכול לנחות בדיוק על כדור אחר
        // ולהישאר מאוזן עליו, כי הפיזיקה מדויקת מדי.
        private Vector3 DropAboveTray() =>
            transform.TransformPoint(tray.center + new Vector3(Random.Range(-0.3f, 0.3f), 0.35f, Random.Range(-0.04f, 0.04f)));

        private bool IsFree(Vector3 slot, Ball self)
        {
            int count = Physics.OverlapSphereNonAlloc(slot, 0.12f, overlap, ballLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Rigidbody body = overlap[i].attachedRigidbody;
                if (body != null && body.gameObject != self.gameObject && body.TryGetComponent(out Ball _)) return false;
            }
            return true;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(machine.center, machine.size);
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(tray.center, tray.size);
        }
    }
}
