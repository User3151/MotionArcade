using MotionArcade.Core;
using MotionArcade.Feedback;
using UnityEngine;

namespace MotionArcade.Games.Basketball
{
    // כדורסל ארקייד, כמו מכונת Pop-A-Shot: מגש כדורים מול השחקן, מסילה משופעת שמחזירה כל כדור למגש,
    // ורשתות בצדדים. 60 שניות, 2 נקודות לסל, כפול ב-10 השניות האחרונות.
    // מותר לזרוק גם לפני START (אימון): יש צליל ורטט, אבל הנקודות נספרות רק בזמן משחק.
    public class BasketballGame : GameStation
    {
        private const string BonusHint = "DOUBLE POINTS!";

        [Header("Balls")]
        [SerializeField] private Ball[] balls;
        [SerializeField] private Transform[] traySlots;      // לאן מחזירים כדור (מעל המגש)
        [SerializeField] private LayerMask ballLayers = ~0;

        [Header("Machine bounds (world space)")]
        [SerializeField] private Bounds machine;   // כל הנפח של המכונה, כולל מעל הסל. כדור מחוץ לזה = הלך לאיבוד
        [SerializeField] private Bounds tray;      // המגש: כדור שעומד כאן במקום שלו, לא "תקוע"

        [Header("Feedback")]
        [SerializeField] private AudioSource hoopAudio;
        [SerializeField] private AudioClip basketClip;
        [SerializeField] private ScorePopup popup;

        private readonly BasketDetector detector = new BasketDetector(1f);
        private readonly Collider[] overlap = new Collider[8];   // מוקצה פעם אחת: בדיקת מקום פנוי במגש בלי GC

        public override string PlayingHint =>
            Phase == RoundPhase.Playing && BasketballScoring.IsBonus(TimeLeft) ? BonusHint : null;

        protected override void Awake()
        {
            base.Awake();
            foreach (Ball ball in balls) ball.Init(this);
        }

        protected override void OnPlayingStarted()
        {
            detector.Clear();
            // כל כדור שלא ביד מקבל משבצת משלו במגש, כדי שהסבב יתחיל עם כל הכדורים זמינים.
            // לא בודקים אם המשבצת פנויה: כל הכדורים זזים עכשיו, אז הם לא יכולים לחסום זה את זה.
            int slot = 0;
            foreach (Ball ball in balls)
            {
                if (ball == null || ball.IsHeld) continue;
                detector.Forget(ball.Id);
                if (slot < traySlots.Length) ball.Teleport(traySlots[slot++].position);
                else ball.Teleport(DropAboveTray());
            }
            Physics.SyncTransforms();
        }

        protected override void OnPlayingEnded() { }

        public bool InsideMachine(Vector3 position) => machine.Contains(position);
        public bool InTray(Vector3 position) => tray.Contains(position);

        public void HandleTop(Ball ball) => detector.EnterTop(ball.Id, Time.time, ball.Velocity.y);

        public void HandleBottom(Ball ball)
        {
            if (!detector.EnterBottom(ball.Id, Time.time, ball.Velocity.y)) return;

            int points = BasketballScoring.PointsFor(TimeLeft);
            bool counted = AddScore(points);

            if (hoopAudio != null && basketClip != null) hoopAudio.PlayOneShot(basketClip);
            Haptics.Pulse(ball.LastHolder, Haptics.Strong, 0.15f);
            if (counted && popup != null) popup.Show(points);
        }

        public void ForgetBall(Ball ball) => detector.Forget(ball.Id);

        // מחזיר כדור לנקודה פנויה מעל המגש. כדור שמוצב בתוך כדור אחר נזרק על ידי הפיזיקה, לכן בודקים קודם.
        public void ReturnBall(Ball ball)
        {
            detector.Forget(ball.Id);
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
