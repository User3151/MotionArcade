using MotionArcade.Core;
using MotionArcade.Diagnostics;
using MotionArcade.Feedback;
using MotionArcade.Interaction;
using UnityEngine;

namespace MotionArcade.Games.Basketball
{
    // כדורסל ארקייד, כמו מכונת Pop-A-Shot: מגש כדורים מול השחקן, מסילה משופעת שמחזירה כל כדור למגש,
    // ורשתות בצדדים. 60 שניות, 2 נקודות לסל, כפול ב-10 השניות האחרונות.
    // הכדורים נעולים עד START (כמו במכונה אמיתית), כדי שהשחקן לא יתחיל לזרוק בלי לשים לב שהסבב לא התחיל.
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

        [Header("Machine bounds (world space)")]
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

        private readonly Collider[] overlap = new Collider[8];   // מוקצה פעם אחת: בדיקת מקום פנוי במגש בלי GC

        public override string PlayingHint =>
            Phase == RoundPhase.Playing && BasketballScoring.IsBonus(TimeLeft) ? BonusHint : null;

        protected override void Awake()
        {
            base.Awake();
            foreach (Ball ball in balls) ball.Init(this);
            // Unity null (שדה ריק) הוא לא null אמיתי לממשק: מעבירים null מפורש
            IThrowAssist hoop = assist != null ? assist : null;
            foreach (Ball ball in balls)
                if (ball.TryGetComponent(out ControllerThrow thrower)) thrower.Assist = hoop;
        }

        // ב-Start ולא ב-Awake: כל כדור מוצא את ה-XRGrabInteractable שלו ב-Awake שלו, שאולי עוד לא רץ
        private void Start() => SetBallsGrabbable(IsRunning);   // מתחילים ב-Intro: נעולים

        // נפתחים ב-START (כבר בספירה לאחור, כדי להיות מוכנים), ננעלים בסוף הסבב וב-STOP.
        // ב-START גם כל הכדורים חוזרים למגש, כך שהם זמינים כבר בספירה (כמו בחצים).
        protected override void OnPhaseEntered(RoundPhase phase)
        {
            SetBallsGrabbable(IsRunning);
            if (phase == RoundPhase.Countdown) ResetBalls();
        }

        private void SetBallsGrabbable(bool value)
        {
            foreach (Ball ball in balls)
                if (ball != null) ball.SetGrabbable(value);
        }

        protected override void OnPlayingStarted() { }

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

        protected override void OnPlayingEnded() { }

        public bool InsideMachine(Vector3 position) => machine.Contains(position);
        public bool InTray(Vector3 position) => tray.Contains(position);

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

            int points = BasketballScoring.PointsFor(TimeLeft);
            bool counted = AddScore(points);

            // כל סל עובר ברשת. סוויש (בלי לגעת בטבעת או בלוח): הרשת נשמעת חזק יותר והקהל מריע יותר.
            // אחרי נגיעה הצלצול של הטבעת כבר נשמע (ImpactSound), והרשת חלשה יותר.
            bool swish = !ball.TouchedHoop;
            if (hoopAudio != null && swishClip != null) hoopAudio.PlayOneShot(swishClip, swish ? 1f : 0.6f);
            if (crowd != null && cheerClips != null && cheerClips.Length > 0)
                crowd.PlayOneShot(cheerClips[Random.Range(0, cheerClips.Length)], swish ? 1f : 0.7f);
            if (recorder != null && swish) recorder.NoteResult(ball.gameObject, "SWISH");
            Haptics.Pulse(ball.LastHolder, Haptics.Strong, 0.15f);
            if (counted && popup != null) popup.Show(points);
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
            tray.center + new Vector3(Random.Range(-0.3f, 0.3f), 0.35f, Random.Range(-0.04f, 0.04f));

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
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(machine.center, machine.size);
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(tray.center, tray.size);
        }
    }
}
