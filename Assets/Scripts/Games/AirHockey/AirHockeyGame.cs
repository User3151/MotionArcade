using MotionArcade.Core;
using MotionArcade.Feedback;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace MotionArcade.Games.AirHockey
{
    // הוקי אוויר מול רובוט, עד 7 גולים. הפיזיקה (HockeyPhysics), הרובוט (HockeyAI) והחוקים (HockeyMatch)
    // הם קוד טהור במרחב של המשטח; כאן רק מחברים אותם לעולם: יד → מחבט, מצב → מיקומים, אירועים → צליל ורטט.
    // המחבט של השחקן: תופסים בידית (grip) והוא הולך אחרי היד על המשטח, בחצי של השחקן בלבד. XRI לא מזיז אותו
    // (בלי track position): הוא נשאר צמוד למשטח גם כשהיד עולה, ולא עף כשמשחררים.
    // מחוץ לסבב המחבט מחכה בגומה הימנית הקרובה, וה-START מעביר אותו לשולחן.
    public class AirHockeyGame : GameStation
    {
        [Header("Table")]
        [SerializeField] private Transform field;          // מרכז המשטח, על פני המשטח: x לרוחב, z אל היריב
        [SerializeField] private Transform puck;
        [SerializeField] private Transform playerMallet;
        [SerializeField] private XRGrabInteractable playerGrab;
        [SerializeField] private Transform opponentMallet;
        [SerializeField] private Transform playerRest;     // הגומה שבה המחבט מחכה

        [Header("Rules")]
        [SerializeField, Min(1)] private int goalsToWin = HockeyMatch.DefaultTarget;
        [SerializeField] private HockeyDifficulty difficulty = HockeyDifficulty.Normal;
        [SerializeField] private float serveDelay = 1.2f;

        [Header("Display")]
        [SerializeField] private TMP_Text youScore;
        [SerializeField] private TMP_Text opponentScore;
        [SerializeField] private TMP_Text rule;
        [SerializeField] private Button difficultyButton;
        [SerializeField] private TMP_Text difficultyLabel;

        [Header("Sound")]
        [SerializeField] private AudioSource puckAudio;    // על הפאק: מכות ודפנות
        [SerializeField] private AudioClip[] hitClips;
        [SerializeField] private AudioClip[] wallClips;
        [SerializeField] private AudioSource tableAudio;   // גולים
        [SerializeField] private AudioClip goalForYou;
        [SerializeField] private AudioClip goalAgainst;

        private const string DifficultyKey = "airhockey.difficulty";
        private static readonly string[] LevelNames = { "EASY", "NORMAL", "HARD" };
        private static readonly string[] RuleTexts = { "FIRST TO 7  •  EASY", "FIRST TO 7  •  NORMAL", "FIRST TO 7  •  HARD" };

        private readonly HockeyRink rink = HockeyRink.Standard;
        private HockeyMatch match;
        private HockeyAI ai;
        private Vector2 puckPosition, puckVelocity, playerPosition, opponentPosition;
        private bool puckInPlay;
        private float serveTimer;
        private IXRInteractor holder;
        private Vector2 grabOffset;
        private float puckY, malletY, playerY;   // גובה מעל המשטח; המחבט של השחקן בגומה גבוה מעט יותר
        private string resultDetails;

        public override bool HasTimer => false;
        public override string ResultDetails => resultDetails;
        public HockeyMatch Match => match;
        public HockeyDifficulty Difficulty => difficulty;
        public Vector2 PuckPosition => puckPosition;
        public Vector2 PuckVelocity => puckVelocity;
        public bool PuckInPlay => puckInPlay;
        public HockeyAI.Mode OpponentMode => ai.Current;

        protected override void Awake()
        {
            base.Awake();
            match = new HockeyMatch(goalsToWin);
            difficulty = (HockeyDifficulty)Mathf.Clamp(PlayerPrefs.GetInt(DifficultyKey, (int)difficulty), 0, 2);
            ai = new HockeyAI(difficulty);
            puckY = field.InverseTransformPoint(puck.position).y;
            malletY = field.InverseTransformPoint(playerMallet.position).y;
            opponentPosition = Home();
            ShowDisplay();
            ToRest();
            playerGrab.enabled = false;
        }

        private void OnEnable()
        {
            playerGrab.selectEntered.AddListener(HandleGrab);
            playerGrab.selectExited.AddListener(HandleRelease);
            if (difficultyButton != null) difficultyButton.onClick.AddListener(NextDifficulty);
        }

        private void OnDisable()
        {
            playerGrab.selectEntered.RemoveListener(HandleGrab);
            playerGrab.selectExited.RemoveListener(HandleRelease);
            if (difficultyButton != null) difficultyButton.onClick.RemoveListener(NextDifficulty);
        }

        private Vector2 Home() => new Vector2(0f, rink.HalfLength - ai.Level.DefenseDepth);
        private Vector2 ToTable(Vector3 world) { Vector3 l = field.InverseTransformPoint(world); return new Vector2(l.x, l.z); }
        private Vector3 ToWorld(Vector2 p, float y) => field.TransformPoint(new Vector3(p.x, y, p.y));

        private void NextDifficulty()
        {
            if (IsRunning) return;
            difficulty = (HockeyDifficulty)(((int)difficulty + 1) % 3);
            PlayerPrefs.SetInt(DifficultyKey, (int)difficulty);
            ai.SetLevel(difficulty);
            ShowDisplay();
        }

        private void ShowDisplay()
        {
            if (youScore != null) youScore.SetText("{0}", match.Player);
            if (opponentScore != null) opponentScore.SetText("{0}", match.Opponent);
            if (rule != null) rule.text = RuleTexts[(int)difficulty];
            if (difficultyLabel != null) difficultyLabel.text = LevelNames[(int)difficulty];
            if (difficultyButton != null) difficultyButton.interactable = !IsRunning;
        }

        private void HandleGrab(SelectEnterEventArgs args)
        {
            holder = args.interactorObject;
            grabOffset = playerPosition - ToTable(holder.GetAttachTransform(playerGrab).position);
            Haptics.Pulse(holder, Haptics.Light, 0.04f);
        }

        private void HandleRelease(SelectExitEventArgs args) => holder = null;

        // מחוץ לסבב: המחבט בגומה, הפאק במרכז, היריב בבית
        private void ToRest()
        {
            if (playerRest != null) playerMallet.position = playerRest.position;
            playerPosition = ToTable(playerMallet.position);
            playerY = field.InverseTransformPoint(playerMallet.position).y;
            puckPosition = Vector2.zero; puckVelocity = Vector2.zero; puckInPlay = false;
            puck.position = ToWorld(puckPosition, puckY);
            opponentPosition = Home();
            opponentMallet.position = ToWorld(opponentPosition, malletY);
        }

        protected override void OnPhaseEntered(RoundPhase phase)
        {
            bool running = phase == RoundPhase.Countdown || phase == RoundPhase.Playing;
            playerGrab.enabled = running;
            if (phase == RoundPhase.Countdown)
            {
                // הספירה לאחור: המחבט עובר לשולחן מול השער, כדי שיהיה זמן לתפוס אותו
                match.Reset();
                ai.SetLevel(difficulty);
                ai.Reset();
                resultDetails = null;
                playerPosition = new Vector2(0f, -rink.HalfLength + 0.25f);
                playerY = malletY;
                playerMallet.position = ToWorld(playerPosition, malletY);
                Serve(toPlayer: true);
            }
            else if (phase == RoundPhase.Intro)
            {
                match.Reset();
                ToRest();
            }
            ShowDisplay();
        }

        protected override void OnPlayingStarted() => ShowDisplay();

        protected override void OnPlayingEnded()
        {
            if (playerGrab != null) playerGrab.enabled = false;
            if (match == null) return;
            resultDetails = match.PlayerWon
                ? $"You {match.Player} : {match.Opponent} Opponent\nYou win!"
                : $"You {match.Player} : {match.Opponent} Opponent\nThe opponent wins";
        }

        private void Serve(bool toPlayer)
        {
            puckPosition = new Vector2(0f, toPlayer ? -0.45f : 0.45f);
            puckVelocity = Vector2.zero;
            puckInPlay = true;
            puck.gameObject.SetActive(true);
            puck.position = ToWorld(puckPosition, puckY);
        }

        private void FixedUpdate()
        {
            if (Phase != RoundPhase.Playing && Phase != RoundPhase.Countdown) return;
            float dt = Time.fixedDeltaTime;

            // השחקן: היד מעל המשטח, בחצי שלו, בלי לגעת בדפנות ובקו האמצע
            Vector2 playerFrom = playerPosition;
            if (holder != null)
            {
                Vector2 hand = ToTable(holder.GetAttachTransform(playerGrab).position) + grabOffset;
                float r = rink.MalletRadius;
                playerPosition = new Vector2(
                    Mathf.Clamp(hand.x, -rink.HalfWidth + r, rink.HalfWidth - r),
                    Mathf.Clamp(hand.y, -rink.HalfLength + r, -r));
            }

            if (Phase == RoundPhase.Countdown) return;   // הפאק והרובוט מחכים ל-GO

            Vector2 opponentFrom = opponentPosition;
            opponentPosition = ai.Step(opponentPosition, puckPosition, puckVelocity, puckInPlay, dt, rink);

            if (!puckInPlay)
            {
                serveTimer -= dt;
                if (serveTimer <= 0f && !match.Over) Serve(match.ServeToPlayer);
                return;
            }

            HockeyStepResult result = HockeyPhysics.Step(ref puckPosition, ref puckVelocity, playerFrom, playerPosition, opponentFrom, opponentPosition, dt, rink);
            if (result.MalletHit >= 0) PlayHit(result.HitSpeed, result.MalletHit == 0);
            else if (result.WallHit) PlayWall(result.WallSpeed);
            if (result.Goal != GoalScored.None) Goal(result.Goal);
        }

        protected override void Update()
        {
            base.Update();
            playerMallet.position = ToWorld(playerPosition, playerY);
            opponentMallet.position = ToWorld(opponentPosition, malletY);
            if (puckInPlay) puck.position = ToWorld(puckPosition, puckY);
        }

        private void Goal(GoalScored goal)
        {
            if (!match.Score(goal)) return;
            puckInPlay = false;
            puck.gameObject.SetActive(false);
            serveTimer = serveDelay;
            bool forYou = goal == GoalScored.ByPlayer;
            if (forYou) AddScore(1);
            if (tableAudio != null)
            {
                AudioClip clip = forYou ? goalForYou : goalAgainst;
                if (clip != null) tableAudio.PlayOneShot(clip);
            }
            if (forYou) Haptics.Pulse(holder, Haptics.Strong, 0.15f);
            ShowDisplay();
            if (match.Over) FinishRound();
        }

        private void PlayHit(float speed, bool byPlayer)
        {
            float strength = Mathf.Clamp01(speed / 4f);
            if (byPlayer) Haptics.Pulse(holder, Mathf.Lerp(Haptics.Light, Haptics.Strong, strength), 0.03f);
            Play(hitClips, Mathf.Lerp(0.35f, 1f, strength));
        }

        private void PlayWall(float speed) => Play(wallClips, Mathf.Lerp(0.2f, 0.9f, Mathf.Clamp01(speed / 4f)));

        private void Play(AudioClip[] clips, float volume)
        {
            if (puckAudio == null || clips == null || clips.Length == 0) return;
            AudioClip clip = clips[Random.Range(0, clips.Length)];
            if (clip != null) puckAudio.PlayOneShot(clip, volume);
        }

        // בדיקות Play אוטומטיות: מכה בפאק בלי יד
        public void DebugPush(Vector2 velocity)
        {
            if (!puckInPlay) return;
            puckVelocity = velocity;
        }

        public void DebugMovePlayer(Vector2 position) => playerPosition = position;
    }
}
