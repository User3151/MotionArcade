using MotionArcade.Core;
using MotionArcade.Diagnostics;
using MotionArcade.Feedback;
using MotionArcade.Interaction;
using TMPro;
using UnityEngine;

namespace MotionArcade.Games.Darts
{
    // חצים, Around the Clock: לפגוע ב-1, אחר כך 2, וכך עד 20 ואז בבול. כל מטרה שהושלמה = נקודה.
    // הלוח בגובה ובמרחק הרשמיים (1.73 מ', 2.37 מ'). הסבב נגמר כשהזמן נגמר, או מוקדם כשמגיעים לבול.
    // שלושה חצים במחזיק מול השחקן. הם חוזרים לבד: כשאין חץ ביד, אין חץ במחזיק ויש חצים תקועים בלוח
    // (כלומר שלושתם נזרקו), וגם חץ בודד שנפל למקום שאי אפשר להגיע אליו.
    public class DartsGame : GameStation
    {
        [Header("Darts")]
        [SerializeField] private Dart[] darts;
        [SerializeField] private Transform[] holderSlots;
        [SerializeField] private Bounds holder;       // חץ שנח כאן זמין לשחקן
        [SerializeField] private Bounds playArea;     // חץ מחוץ לזה הלך לאיבוד
        [SerializeField] private float returnAllDelay = 1f;
        [SerializeField] private float lostSeconds = 1.5f;

        [Header("Board")]
        [SerializeField] private DartBoard board;
        [SerializeField] private TMP_Text targetDisplay;   // המטרה הנוכחית בגדול, מעל הלוח
        [SerializeField] private TMP_Text[] numberLabels;  // לפי מספר: [1] עד [20]. [0] לא בשימוש
        [SerializeField] private Color labelColor = Color.white;
        [SerializeField] private Color targetColor = new Color(1f, 0.85f, 0.1f);

        [Header("Feedback")]
        [SerializeField] private AudioSource boardAudio;
        [SerializeField] private AudioClip hitClip;
        [SerializeField] private AudioClip targetClip;
        [SerializeField] private ScorePopup popup;
        [SerializeField] private ThrowRecorder recorder;   // מדידת זריקות (פיתוח בלבד)
        [SerializeField] private DartAssist assist;        // עזרת כיוון עדינה לכל החצים

        private static readonly Color HitColor = Color.white;
        private static readonly Color AdvanceColor = new Color(0.3f, 1f, 0.4f);
        private static readonly Color MissColor = new Color(0.6f, 0.6f, 0.65f);

        private readonly AroundTheClock progress = new AroundTheClock();
        private string[] hints;          // "Hit the 7" לכל מטרה, מחושב פעם אחת (בלי GC באמצע סבב)
        private float[] lostTimers;
        private float returnAllTimer;

        public DartBoard Board => board;

        // לאן השחקן מכוון עכשיו: אמצע הטבעת החיצונית של המקטע (השטח הכי גדול), או מרכז הלוח לבול
        public Vector3 AimPoint
        {
            get
            {
                int target = progress.Target;
                return target >= 1 && target <= 20 ? board.WorldPointOf(target, 0.135f) : board.transform.position;
            }
        }
        public int Target => progress.Target;

        public override string PlayingHint => Phase == RoundPhase.Playing && !progress.IsFinished ? hints[Target] : null;

        protected override void Awake()
        {
            base.Awake();
            hints = new string[AroundTheClock.Bull + 1];
            for (int i = 1; i <= 20; i++) hints[i] = "Hit the " + i;
            hints[AroundTheClock.Bull] = "Hit the BULL";
            lostTimers = new float[darts.Length];
            foreach (Dart dart in darts) dart.Init(this);
            // Unity null (שדה ריק) הוא לא null אמיתי לממשק: מעבירים null מפורש
            IThrowAssist aim = assist != null ? assist : null;
            foreach (Dart dart in darts)
                if (dart.TryGetComponent(out ControllerThrow thrower)) thrower.Assist = aim;
        }

        // ב-Start: כל חץ מוצא את ה-XRGrabInteractable שלו ב-Awake שלו, שאולי עוד לא רץ
        private void Start()
        {
            SetDartsGrabbable(IsRunning);
            ShowTarget();
        }

        protected override void OnPhaseEntered(RoundPhase phase)
        {
            SetDartsGrabbable(IsRunning);
            // START (תחילת הספירה): כל החצים חוזרים למחזיק והמטרה חוזרת ל-1, כדי שאפשר יהיה להתכונן כבר בספירה.
            // STOP (חזרה ל-Intro): גם, כדי שהלוח יהיה נקי. בסוף סבב (Results) החצים נשארים בלוח, לראות איפה פגעו.
            if (phase == RoundPhase.Countdown || phase == RoundPhase.Intro)
            {
                progress.Reset();
                ShowTarget();
                ReturnAll();
            }
        }

        protected override void OnPlayingStarted()
        {
            progress.Reset();
            ShowTarget();
        }

        protected override void OnPlayingEnded() { }

        protected override void Update()
        {
            base.Update();
            ManageReturns();
        }

        // נקרא מהחץ ברגע שננעץ
        public void HandleStuck(Dart dart, Vector3 point)
        {
            DartHit hit = board.ScoreAt(point);
            bool advanced = Phase == RoundPhase.Playing && progress.Register(hit);
            // לפני FinishRound: הסיום כותב את המדידה לקובץ
            if (recorder != null) recorder.NoteResult(dart.gameObject, DartScoring.Label(hit), point);

            if (advanced)
            {
                AddScore(1);
                Play(targetClip);
                Haptics.Pulse(dart.LastHolder, Haptics.Strong, 0.12f);
                ShowTarget();
            }
            else
            {
                Play(hitClip);
                Haptics.Pulse(dart.LastHolder, Haptics.Medium, 0.06f);
            }

            if (popup != null)
                popup.ShowAt(point + board.transform.up * 0.05f - board.transform.forward * 0.03f,
                             DartScoring.Label(hit), advanced ? AdvanceColor : hit.IsMiss ? MissColor : HitColor);

            if (advanced && progress.IsFinished) FinishRound();
        }

        private void Play(AudioClip clip)
        {
            if (boardAudio != null && clip != null) boardAudio.PlayOneShot(clip);
        }

        private void ShowTarget()
        {
            int target = progress.Target;
            if (recorder != null) recorder.SetAim(AimPoint, target);
            if (targetDisplay != null)
            {
                if (target == AroundTheClock.Bull) targetDisplay.SetText("BULL");
                else if (target == 0) targetDisplay.SetText("DONE");
                else targetDisplay.SetText("{0}", target);   // בלי GC
            }
            for (int i = 1; i < numberLabels.Length; i++)
                if (numberLabels[i] != null) numberLabels[i].color = i == target ? targetColor : labelColor;
        }

        private void SetDartsGrabbable(bool value)
        {
            foreach (Dart dart in darts)
                if (dart != null) dart.SetGrabbable(value);
        }

        private void ManageReturns()
        {
            bool anyInPlay = false;   // ביד או באוויר
            int inHolder = 0, stuck = 0;

            for (int i = 0; i < darts.Length; i++)
            {
                Dart dart = darts[i];
                switch (dart.Current)
                {
                    case Dart.State.Held:
                    case Dart.State.Flying:
                        anyInPlay = true;
                        lostTimers[i] = 0f;
                        break;
                    case Dart.State.Stuck:
                        stuck++;
                        lostTimers[i] = 0f;
                        break;
                    default:
                        if (holder.Contains(dart.Position))
                        {
                            inHolder++;
                            lostTimers[i] = 0f;
                            break;
                        }
                        // על הרצפה, מתחת ללוח, או מחוץ לאזור: השחקן לא יכול להגיע אליו
                        bool resting = dart.Velocity.sqrMagnitude < 0.05f * 0.05f;
                        lostTimers[i] = resting || !playArea.Contains(dart.Position) ? lostTimers[i] + Time.deltaTime : 0f;
                        if (lostTimers[i] > lostSeconds)
                        {
                            ReturnOne(dart);
                            lostTimers[i] = 0f;
                        }
                        break;
                }
            }

            // כל החצים נזרקו ונתקעו: מחזירים את כולם אחרי רגע, כדי שהשחקן יספיק לראות איפה פגע
            if (!anyInPlay && inHolder == 0 && stuck > 0)
            {
                returnAllTimer += Time.deltaTime;
                if (returnAllTimer > returnAllDelay) ReturnAll();
            }
            else returnAllTimer = 0f;
        }

        private void ReturnAll()
        {
            returnAllTimer = 0f;
            int slot = 0;
            foreach (Dart dart in darts)
            {
                if (dart == null || dart.Current == Dart.State.Held) continue;
                if (slot < holderSlots.Length) dart.Teleport(holderSlots[slot].position, holderSlots[slot].rotation);
                slot++;
            }
            Physics.SyncTransforms();
        }

        private void ReturnOne(Dart dart)
        {
            // משבצת שאין לידה חץ אחר
            foreach (Transform slot in holderSlots)
            {
                bool free = true;
                foreach (Dart other in darts)
                    if (other != dart && (other.Position - slot.position).sqrMagnitude < 0.04f * 0.04f) free = false;
                if (!free) continue;
                dart.Teleport(slot.position, slot.rotation);
                Physics.SyncTransforms();
                return;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(holder.center, holder.size);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(playArea.center, playArea.size);
        }
    }
}
