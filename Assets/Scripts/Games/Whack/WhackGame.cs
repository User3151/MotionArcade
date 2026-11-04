using MotionArcade.Core;
using MotionArcade.Feedback;
using UnityEngine;

namespace MotionArcade.Games.Whack
{
    // Whack-A-Mole על קשת סביב השחקן: ארבעה טורים (משמאל לימין) ושלוש שורות (נמוך, באמצע, גבוה).
    // החפרפרות מכריחות סיבוב גו, כיפוף, הושטה למעלה ושימוש בשתי הידיים; יש שני פטישים.
    // מכה נספרת רק כשראש הפטיש נע לתוך החור מהר מספיק. 60 שניות, הקושי עולה לאורך הסבב,
    // ואחרי 15 שניות מופיעות אדומות: פגיעה בהן מורידה 2 נקודות.
    public class WhackGame : GameStation
    {
        private const string RedHint = "Don't hit the RED moles!";
        private const string PlusOne = "+1";
        private const string MinusTwo = "-2";

        [Header("Moles")]
        [SerializeField] private MoleHole[] holes;
        [SerializeField] private float strikeSpeed = 1.0f;   // מ'/ש' לתוך החור

        [Header("Mallets")]
        [SerializeField] private Mallet[] mallets;
        [SerializeField] private Transform[] malletSlots;   // אחד לכל פטיש, על השולחן
        // במרחב של המשחק, לא של העולם: המשחק עומד בפינה שלו בארקייד
        [SerializeField] private Bounds table;
        [SerializeField] private Bounds playArea;
        [SerializeField] private float lostSeconds = 1.5f;

        [Header("Feedback")]
        [SerializeField] private AudioClip hitClip;
        [SerializeField] private AudioClip redClip;
        [SerializeField] private ScorePopup popup;

        private static readonly Color HitColor = new Color(0.3f, 1f, 0.4f);
        private static readonly Color RedColor = new Color(1f, 0.25f, 0.2f);

        private readonly System.Random rng = new System.Random();
        private bool[] busy;
        private float[] lostTimers;
        private float elapsed;
        private float nextSpawn;
        private int lastHole = -1;
        private int hits, misses, redHits;
        private float reactionSum;

        public override string PlayingHint =>
            Phase == RoundPhase.Playing && elapsed >= WhackSpawner.RedFromSeconds ? RedHint : null;

        public override string ResultDetails =>
            $"Hit {hits}   Missed {misses}   Red hit {redHits}" +
            (hits > 0 ? $"\nAverage reaction {reactionSum / hits:0.00} s" : "");

        public float StrikeSpeed => strikeSpeed;

        protected override void Awake()
        {
            base.Awake();
            busy = new bool[holes.Length];
            lostTimers = new float[mallets.Length];
            foreach (MoleHole hole in holes) hole.Init(this);
        }

        // ב-Start: כל פטיש מוצא את ה-XRGrabInteractable שלו ב-Awake שלו
        private void Start() => SetMalletsGrabbable(IsRunning);

        protected override void OnPhaseEntered(RoundPhase phase)
        {
            SetMalletsGrabbable(IsRunning);
            if (phase == RoundPhase.Countdown || phase == RoundPhase.Intro) ReturnMallets();
        }

        protected override void OnPlayingStarted()
        {
            elapsed = 0f;
            nextSpawn = 0.6f;
            lastHole = -1;
            hits = misses = redHits = 0;
            reactionSum = 0f;
        }

        protected override void OnPlayingEnded()
        {
            foreach (MoleHole hole in holes)
                if (hole != null) hole.Hide();
        }

        protected override void OnPlayingUpdate()
        {
            elapsed += Time.deltaTime;
            nextSpawn -= Time.deltaTime;
            if (nextSpawn > 0f) return;

            int up = 0;
            for (int i = 0; i < holes.Length; i++)
            {
                busy[i] = holes[i].Motion.IsBusy;
                if (busy[i]) up++;
            }
            if (up >= WhackSpawner.MaxUp(elapsed))
            {
                nextSpawn = 0.1f;
                return;
            }

            int pick = WhackSpawner.PickHole(busy, lastHole, rng);
            if (pick < 0)
            {
                nextSpawn = 0.1f;
                return;
            }
            bool red = rng.NextDouble() < WhackSpawner.RedChanceAt(elapsed);
            holes[pick].Spawn(WhackSpawner.UpSeconds(elapsed, RoundSeconds), red);
            lastHole = pick;
            nextSpawn = WhackSpawner.Gap(rng);
        }

        protected override void Update()
        {
            base.Update();
            ManageMallets();
        }

        // נקרא מהחור כשראש פטיש נכנס לחפרפרת. intoHole: מהירות הראש לתוך החור (מ'/ש')
        public void HandleStrike(MoleHole hole, Mallet mallet, float intoHole)
        {
            if (Phase != RoundPhase.Playing || intoHole < strikeSpeed) return;
            bool wasRed = hole.Motion.IsRed;
            if (!hole.Motion.TryHit(out float reaction)) return;

            if (wasRed)
            {
                redHits++;
                if (Score > 0) AddScore(-Mathf.Min(2, Score));
                hole.PlayOneShot(redClip);
                Haptics.Pulse(mallet.Holder, Haptics.Medium, 0.2f);
                ShowPopup(hole, MinusTwo, RedColor);
            }
            else
            {
                hits++;
                reactionSum += reaction;
                AddScore(1);
                hole.PlayOneShot(hitClip);
                Haptics.Pulse(mallet.Holder, Haptics.Strong, 0.08f);
                ShowPopup(hole, PlusOne, HitColor);
            }
        }

        public void HandleEscaped(MoleHole hole)
        {
            if (Phase == RoundPhase.Playing) misses++;
        }

        private void ShowPopup(MoleHole hole, string label, Color color)
        {
            if (popup != null) popup.ShowAt(hole.MolePosition + hole.Normal * 0.08f + Vector3.up * 0.06f, label, color);
        }

        private void SetMalletsGrabbable(bool value)
        {
            foreach (Mallet mallet in mallets)
                if (mallet != null) mallet.SetGrabbable(value);
        }

        private void ReturnMallets()
        {
            for (int i = 0; i < mallets.Length; i++)
            {
                if (mallets[i] == null || mallets[i].IsHeld) continue;
                mallets[i].Teleport(malletSlots[i].position, malletSlots[i].rotation);
                lostTimers[i] = 0f;
            }
            Physics.SyncTransforms();
        }

        // פטיש שנפל לרצפה או עף מהאזור חוזר לשולחן: השחקן לא יכול ללכת להביא אותו
        private void ManageMallets()
        {
            for (int i = 0; i < mallets.Length; i++)
            {
                Mallet mallet = mallets[i];
                if (mallet == null || mallet.IsHeld || table.Contains(transform.InverseTransformPoint(mallet.Position)))
                {
                    lostTimers[i] = 0f;
                    continue;
                }
                bool resting = mallet.Velocity.sqrMagnitude < 0.05f * 0.05f;
                lostTimers[i] = resting || !playArea.Contains(transform.InverseTransformPoint(mallet.Position)) ? lostTimers[i] + Time.deltaTime : 0f;
                if (lostTimers[i] <= lostSeconds) continue;
                mallet.Teleport(malletSlots[i].position, malletSlots[i].rotation);
                lostTimers[i] = 0f;
                Physics.SyncTransforms();
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(table.center, table.size);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(playArea.center, playArea.size);
        }
    }
}
