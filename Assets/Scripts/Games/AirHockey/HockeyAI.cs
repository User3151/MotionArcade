using UnityEngine;

namespace MotionArcade.Games.AirHockey
{
    public enum HockeyDifficulty { Easy, Normal, Hard }

    // הרובוט ביריב. רואה את הפאק באיחור (זמן תגובה), שוגה קצת בניחוש, ומוגבל במהירות:
    // שלושת אלה הם כל ההבדל בין הרמות. בכל רגע הוא בוחר אחד משלושה:
    // - הגנה: הפאק בא אליו → זז לנקודה שבה הפאק יחצה את קו ההגנה שלו (עם קפיצות מהדפנות).
    // - התקפה: הפאק איטי בחצי שלו → נכנס מאחוריו ומכה לכיוון השער שלך (לפעמים דרך הדופן).
    // - בית: הפאק אצלך → חוזר מול השער שלו, קצת לצד של הפאק.
    // הוא לעולם לא עובר את קו האמצע ולא נוגע בדפנות.
    public class HockeyAI
    {
        public struct Settings
        {
            public float MaxSpeed;        // מ'/ש'
            public float Reaction;        // שניות של איחור במה שהוא רואה
            public float Error;           // מטרים של טעות בניחוש נקודת החסימה
            public float StrikeSpeed;     // מהירות המכה בהתקפה
            public float DefenseDepth;    // המרחק של קו ההגנה מהשער

            public static Settings For(HockeyDifficulty level)
            {
                switch (level)
                {
                    case HockeyDifficulty.Easy:
                        return new Settings { MaxSpeed = 1.1f, Reaction = 0.3f, Error = 0.1f, StrikeSpeed = 1.5f, DefenseDepth = 0.22f };
                    case HockeyDifficulty.Hard:
                        return new Settings { MaxSpeed = 3.0f, Reaction = 0.06f, Error = 0.015f, StrikeSpeed = 3.6f, DefenseDepth = 0.3f };
                    default:
                        return new Settings { MaxSpeed = 1.9f, Reaction = 0.15f, Error = 0.05f, StrikeSpeed = 2.5f, DefenseDepth = 0.26f };
                }
            }
        }

        public enum Mode { Home, Defend, Attack }

        private const int History = 64;
        private readonly Vector2[] seenPosition = new Vector2[History];
        private readonly Vector2[] seenVelocity = new Vector2[History];
        private readonly float[] seenTime = new float[History];
        private int head;
        private float clock;
        private Settings settings;
        private readonly System.Random random;
        private float error;
        private float aimX;
        private bool striking;
        private float attackTime;
        private float strikeTime;
        private float sign = 1f;     // כיוון התנועה האחרון של הפאק לאורך, כדי לבחור טעות חדשה בכל התקפה שלך

        public Mode Current { get; private set; }
        public Settings Level => settings;

        public HockeyAI(HockeyDifficulty level, int seed = 0)
        {
            random = seed == 0 ? new System.Random() : new System.Random(seed);
            SetLevel(level);
        }

        public void SetLevel(HockeyDifficulty level) => settings = Settings.For(level);

        public void Reset()
        {
            clock = 0f;
            head = 0;
            for (int i = 0; i < History; i++) seenTime[i] = float.NegativeInfinity;
            striking = false;
            Current = Mode.Home;
        }

        private float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);

        // צעד אחד: מה שהרובוט "רואה" עכשיו נכנס להיסטוריה, והוא פועל לפי מה שראה לפני Reaction שניות.
        // מחזיר את המיקום החדש של המחבט שלו.
        public Vector2 Step(Vector2 mallet, Vector2 puck, Vector2 puckVelocity, bool puckInPlay, float dt, in HockeyRink rink)
        {
            clock += dt;
            head = (head + 1) % History;
            seenPosition[head] = puck; seenVelocity[head] = puckVelocity; seenTime[head] = clock;
            Recall(clock - settings.Reaction, out Vector2 p, out Vector2 v);

            float home = rink.HalfLength - settings.DefenseDepth;
            Vector2 target;
            float speed = settings.MaxSpeed;

            if (!puckInPlay)
            {
                Current = Mode.Home;
                target = new Vector2(0f, home);
            }
            else if (p.y > 0f && v.magnitude < 0.6f && v.y < 0.4f)
            {
                // התקפה: קודם מאחורי הפאק (בצד השער שלו), אחר כך דרכו אל נקודת הכיוון
                if (Current != Mode.Attack) { aimX = Range(-0.6f, 0.6f) * rink.HalfWidth; striking = false; attackTime = 0f; }
                Current = Mode.Attack;
                attackTime += dt;
                Vector2 aim = new Vector2(aimX, -rink.HalfLength);
                Vector2 dir = (aim - p).normalized;
                float contact = rink.MalletRadius + rink.PuckRadius;
                Vector2 behind = p - dir * (contact + 0.06f);
                if (!striking && (mallet - behind).magnitude < 0.03f) { striking = true; strikeTime = 0f; }
                if (striking)
                {
                    target = p + dir * 0.15f;
                    speed = settings.StrikeSpeed;
                    strikeTime += dt;
                    // המכה נגמרה (הגיע ליעד, או חצי שנייה עברה) והפאק עדיין איטי אצלו: נכנס שוב מאחוריו.
                    // בלי זה, מכה שהחטיאה השאירה אותו עומד ביעד שלה לנצח.
                    if ((mallet - target).magnitude < 0.01f || strikeTime > 0.5f) { striking = false; attackTime = 0f; }
                }
                else if (attackTime > 1.2f)
                {
                    // לא מצליח להיכנס מאחוריו (צמוד לדופן האחורית): דוחף אותו מאיפה שהוא, כדי שלא ייתקע
                    Vector2 push = p - mallet;
                    target = p + (push.sqrMagnitude > 1e-6f ? push.normalized : Vector2.down) * 0.12f;
                    speed = settings.StrikeSpeed * 0.6f;
                }
                else target = behind;
            }
            else if (v.y > 0.05f)
            {
                // הגנה: הפאק בדרך אליו
                if (sign < 0f) error = Range(-1f, 1f) * settings.Error;
                sign = 1f;
                Current = Mode.Defend;
                float x = HockeyPhysics.PredictX(p, v, home, rink) + error;
                target = new Vector2(x, home);
                striking = false;
            }
            else
            {
                if (v.y < -0.05f) sign = -1f;
                Current = Mode.Home;
                striking = false;
                target = new Vector2(p.x * 0.4f, home);
            }

            return Clamp(Vector2.MoveTowards(mallet, target, speed * dt), rink);
        }

        public static Vector2 Clamp(Vector2 mallet, in HockeyRink rink)
        {
            float r = rink.MalletRadius;
            return new Vector2(
                Mathf.Clamp(mallet.x, -rink.HalfWidth + r, rink.HalfWidth - r),
                Mathf.Clamp(mallet.y, r, rink.HalfLength - r));
        }

        private void Recall(float time, out Vector2 position, out Vector2 velocity)
        {
            // המצב האחרון שנראה לפני הזמן הזה; בהתחלה, מה שיש
            for (int i = 0; i < History; i++)
            {
                int k = (head - i + History) % History;
                if (seenTime[k] <= time)
                {
                    position = seenPosition[k]; velocity = seenVelocity[k];
                    return;
                }
            }
            int oldest = (head + 1) % History;
            if (float.IsNegativeInfinity(seenTime[oldest])) oldest = head;
            position = seenPosition[oldest]; velocity = seenVelocity[oldest];
        }
    }
}
