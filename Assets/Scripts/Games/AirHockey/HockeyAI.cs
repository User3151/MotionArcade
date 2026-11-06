using UnityEngine;

namespace MotionArcade.Games.AirHockey
{
    public enum HockeyDifficulty { Easy, Normal, Hard }

    // הרובוט ביריב. רואה את הפאק באיחור (זמן תגובה), שוגה קצת בניחוש, ומוגבל במהירות:
    // שלושת אלה הם כל ההבדל בין הרמות. בכל רגע הוא בוחר אחד משלושה:
    // - הגנה: הפאק בא אליו → זז לנקודה שבה הפאק יחצה את קו ההגנה שלו (עם קפיצות מהדפנות).
    // - התקפה: הפאק בחצי שלו (גם בתנועה) → מנחש איפה יפגוש אותו, נכנס מאחוריו ומכה לשער שלך (לפעמים דרך הדופן).
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
            public float BankChance;      // כמה מההתקפות דרך הדופן

            public static Settings For(HockeyDifficulty level)
            {
                switch (level)
                {
                    case HockeyDifficulty.Easy:
                        return new Settings { MaxSpeed = 1.0f, Reaction = 0.3f, Error = 0.12f, StrikeSpeed = 1.5f, DefenseDepth = 0.22f, BankChance = 0f };
                    case HockeyDifficulty.Hard:
                        // גיא: ב-Hard הוא קרא כל מכה ולא היה סיכוי להבקיע. איטי יותר, מגיב מאוחר יותר וטועה יותר
                        return new Settings { MaxSpeed = 2.2f, Reaction = 0.11f, Error = 0.055f, StrikeSpeed = 3.0f, DefenseDepth = 0.28f, BankChance = 0.35f };
                    default:
                        return new Settings { MaxSpeed = 1.6f, Reaction = 0.17f, Error = 0.08f, StrikeSpeed = 2.3f, DefenseDepth = 0.25f, BankChance = 0.2f };
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
        private float bank;
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
            float contact = rink.MalletRadius + rink.PuckRadius;
            Vector2 target;
            float speed = settings.MaxSpeed;

            // איפה הפאק "עכשיו" לפי מה שראה (הוא רואה באיחור, אבל מנחש קדימה כמו אדם)
            Vector2 now = HockeyPhysics.PredictPosition(p, v, settings.Reaction, rink);
            bool inMyHalf = now.y > 0.02f;
            bool comingFast = v.y > 0.9f;

            if (!puckInPlay)
            {
                Current = Mode.Home;
                striking = false;
                target = new Vector2(0f, home);
            }
            else if (inMyHalf && !(comingFast && now.y < mallet.y))
            {
                // התקפה: גם על פאק שזז בחצי שלו (גיא: הוא חיכה מאחור בזמן שהפאק זז מצד לצד).
                // מנחש איפה הפאק יהיה כשיגיע אליו, נכנס מאחוריו (בצד השער שלו) ומכה דרכו לכיוון השער שלך.
                if (Current != Mode.Attack) StartAttack(rink);
                Current = Mode.Attack;
                attackTime += dt;

                Vector2 q = now;
                for (int i = 0; i < 3; i++)
                {
                    float reach = (Behind(q, contact) - mallet).magnitude / Mathf.Max(0.1f, settings.MaxSpeed);
                    q = HockeyPhysics.PredictPosition(now, v, Mathf.Min(reach, 0.8f), rink);
                }
                if (q.y < 0.05f)
                {
                    // עד שיגיע, הפאק כבר יעבור אליך: מחכה לו בקו ההגנה
                    target = new Vector2(HockeyPhysics.PredictX(now, v, home, rink), home);
                    striking = false;
                }
                else
                {
                    Vector2 dir = (Aim(rink) - q).normalized;
                    Vector2 behind = q - dir * (contact + 0.05f);
                    // המחבט לפני הפאק (בין הפאק לקו האמצע): עוקף מהצד, כדי לא לדחוף אותו לשער של עצמו
                    if (!striking && mallet.y < q.y - 0.02f && Mathf.Abs(mallet.x - q.x) < contact + 0.02f)
                    {
                        float side = mallet.x >= q.x ? 1f : -1f;
                        if (Mathf.Abs(q.x + side * (contact + 0.06f)) > rink.HalfWidth - rink.MalletRadius) side = -side;
                        behind = new Vector2(q.x + side * (contact + 0.06f), q.y);
                    }
                    if (!striking && (mallet - behind).magnitude < 0.035f) { striking = true; strikeTime = 0f; }
                    if (striking)
                    {
                        target = q + dir * 0.15f;
                        speed = settings.StrikeSpeed;
                        strikeTime += dt;
                        // המכה נגמרה (הגיע ליעד, או חצי שנייה עברה, או הפאק כבר בדרך אליך): מתחיל מחדש
                        if ((mallet - target).magnitude < 0.01f || strikeTime > 0.5f || v.y < -0.6f) { striking = false; attackTime = 0f; }
                    }
                    else if (attackTime > 1.2f)
                    {
                        // לא מצליח להיכנס מאחוריו (צמוד לדופן האחורית): דוחף אותו מאיפה שהוא, כדי שלא ייתקע
                        Vector2 push = q - mallet;
                        target = q + (push.sqrMagnitude > 1e-6f ? push.normalized : Vector2.down) * 0.12f;
                        speed = settings.StrikeSpeed * 0.6f;
                    }
                    else target = behind;
                }
            }
            else if (v.y > 0.05f)
            {
                // הגנה: הפאק בדרך אליו. הטעות גדלה עם מהירות הפאק: מכה חזקה קשה יותר לקרוא
                if (sign < 0f) error = Range(-1f, 1f) * settings.Error * (1f + v.magnitude / 2.5f);
                sign = 1f;
                Current = Mode.Defend;
                striking = false;
                float x = HockeyPhysics.PredictX(p, v, home, rink) + error;
                target = new Vector2(x, home);
            }
            else
            {
                if (v.y < -0.05f) sign = -1f;
                Current = Mode.Home;
                striking = false;
                target = new Vector2(now.x * 0.4f, home);
            }

            return Clamp(Vector2.MoveTowards(mallet, target, speed * dt), rink);
        }

        private void StartAttack(in HockeyRink rink)
        {
            striking = false;
            attackTime = 0f;
            // לאן לכוון: לשער, ולפעמים (ברמות הגבוהות) דרך הדופן
            aimX = Range(-0.6f, 0.6f) * rink.GoalHalfWidth;
            bank = Range(0f, 1f) < settings.BankChance ? (Range(0f, 1f) < 0.5f ? -1f : 1f) : 0f;
        }

        private Vector2 Aim(in HockeyRink rink)
        {
            if (bank == 0f) return new Vector2(aimX, -rink.HalfLength);
            // מכה דרך הדופן = לכוון לתמונת המראה של השער מעבר לדופן
            float wall = rink.HalfWidth - rink.PuckRadius;
            return new Vector2(bank * 2f * wall - aimX, -rink.HalfLength);
        }

        private static Vector2 Behind(Vector2 q, float contact) => q + new Vector2(0f, contact + 0.05f);

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
