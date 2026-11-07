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
    // התנועה כמו של יד (גיא, 2026-11-07: "זז ממש מהר, לא טבעי, ואפילו ב-Hard מכניס לעצמו גולים"):
    // - תאוצה מוגבלת ובלימה לפני היעד, במקום קפיצה מיידית למהירות מלאה ועצירה במקום.
    // - היסוס קצר לפני התקפה, והיעד משתנה בהדרגה.
    // - פאק בינו לבין השער שלו, קרוב: הוא עוקף אותו מהצד ולא נכנס בו (שם נולדו הגולים העצמיים).
    public class HockeyAI
    {
        public struct Settings
        {
            public float MaxSpeed;        // מ'/ש'
            public float Accel;           // מ'/ש' לשנייה: כמה מהר הוא מאיץ ובולם
            public float StrikeAccel;     // בזמן מכה
            public float Hesitation;      // שניות של היסוס לפני התקפה (בערך)
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
                        return new Settings { MaxSpeed = 1.35f, Accel = 6.5f, StrikeSpeed = 2.2f, StrikeAccel = 13f, Reaction = 0.25f, Error = 0.11f, DefenseDepth = 0.22f, BankChance = 0.05f, Hesitation = 0.2f };
                    case HockeyDifficulty.Hard:
                        // גיא: ב-Hard הוא קרא כל מכה ולא היה סיכוי להבקיע, ואחר כך: מהיר מדי ולא טבעי
                        return new Settings { MaxSpeed = 2.8f, Accel = 16f, StrikeSpeed = 3.9f, StrikeAccel = 28f, Reaction = 0.09f, Error = 0.04f, DefenseDepth = 0.28f, BankChance = 0.35f, Hesitation = 0.04f };
                    default:
                        return new Settings { MaxSpeed = 2.0f, Accel = 10.5f, StrikeSpeed = 3.0f, StrikeAccel = 20f, Reaction = 0.14f, Error = 0.06f, DefenseDepth = 0.25f, BankChance = 0.2f, Hesitation = 0.1f };
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
        private float hesitate;
        private Vector2 velocity;     // של המחבט שלו
        private Vector2 smoothTarget;
        private bool hasTarget;
        private bool approach;        // בצעד הזה הוא בדרך אל מאחורי הפאק: בלי בלימה
        private float sign = 1f;     // כיוון התנועה האחרון של הפאק לאורך, כדי לבחור טעות חדשה בכל התקפה שלך

        public Mode Current { get; private set; }
        public Vector2 Velocity => velocity;
        public bool Striking => striking;
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
            velocity = Vector2.zero;
            hasTarget = false;
            sign = -1f;   // גם המכה הראשונה אחרי הגשה מקבלת טעות ניחוש (קודם רק אחרי שהפאק היה אצלך)
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

            // איפה הפאק "עכשיו" לפי מה שראה (הוא רואה באיחור, אבל מנחש קדימה כמו אדם)
            Vector2 now = HockeyPhysics.PredictPosition(p, v, settings.Reaction, rink);
            bool inMyHalf = now.y > 0.02f;
            // הפאק בדרך אל פתח השער שלו (גם לאט): קודם לחסום. בלי זה פאק איטי גלש פנימה בזמן שהוא ניסה להיכנס מאחוריו
            bool threat = v.y > 0.05f && Mathf.Abs(HockeyPhysics.PredictX(now, v, rink.HalfLength, rink)) < rink.GoalHalfWidth + 0.12f;

            if (!puckInPlay)
            {
                Current = Mode.Home;
                striking = false;
                target = new Vector2(0f, home);
            }
            else if (inMyHalf && threat)
            {
                // שוער: עומד בין הפאק לשער, על הקו שהפאק יחצה. אם המחבט כבר לפני הפאק, חוסם שם
                Current = Mode.Defend;
                striking = false;
                float line = now.y < mallet.y - 0.02f
                    ? mallet.y
                    : Mathf.Clamp(now.y + contact + 0.05f, home, rink.HalfLength - rink.MalletRadius);
                target = new Vector2(HockeyPhysics.PredictX(now, v, line, rink), line);
            }
            else if (inMyHalf)
            {
                // התקפה: גם על פאק שזז בחצי שלו (גיא: הוא חיכה מאחור בזמן שהפאק זז מצד לצד).
                // מנחש איפה הפאק יהיה כשיגיע אליו, נכנס מאחוריו (בצד השער שלו) ומכה דרכו לכיוון השער שלך.
                if (Current != Mode.Attack) StartAttack(rink);
                Current = Mode.Attack;
                attackTime += dt;
                hesitate -= dt;

                Vector2 q = now;
                for (int i = 0; i < 3; i++)
                {
                    float reach = (Behind(q, contact) - mallet).magnitude / Mathf.Max(0.1f, settings.MaxSpeed * 0.8f) + 0.1f;
                    q = HockeyPhysics.PredictPosition(now, v, Mathf.Min(reach, 0.8f), rink);
                }
                if (hesitate > 0f && !striking)
                {
                    // רגע לפני שהוא יוצא: עוקב אחרי הפאק מקו ההגנה
                    target = new Vector2(Mathf.Clamp(q.x, -rink.HalfWidth * 0.6f, rink.HalfWidth * 0.6f), Mathf.Max(home, q.y + contact + 0.05f));
                }
                else if (q.y < 0.05f)
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
                    // מכה רק כשהוא באמת מאחורי הפאק ביחס לכיוון המכה; אחרת המכה יוצאת לצד (או לשער שלו)
                    Vector2 line = q - mallet;
                    bool lined = line.sqrMagnitude > 1e-6f && Vector2.Dot(line.normalized, dir) > 0.7f;
                    if (!striking && (mallet - behind).magnitude < 0.09f && lined) { striking = true; strikeTime = 0f; }
                    if (striking)
                    {
                        // דרך הפאק, בקו מהמחבט אליו (קרוב לכיוון המכה, כי התחיל מאחוריו)
                        target = q + (line.sqrMagnitude > 1e-6f ? line.normalized : dir) * 0.15f;
                        strikeTime += dt;
                        // המכה נגמרה (הגיע ליעד, חצי שנייה עברה, הפאק כבר בדרך אליך, או שהפאק זז והוא כבר לא מאחוריו): מתחיל מחדש
                        bool lost = line.sqrMagnitude > 1e-6f && Vector2.Dot(line.normalized, dir) < 0.3f;
                        if ((mallet - target).magnitude < 0.01f || strikeTime > 0.5f || v.y < -0.6f || lost) { striking = false; attackTime = 0f; }
                    }
                    else if (attackTime > 1.2f)
                    {
                        // לא מצליח להיכנס מאחוריו (צמוד לדופן האחורית או מול השער): נכנס לצדו ודוחף אותו לרוחב,
                        // לאורך קו השער, ככה שהוא לא יכול להיכנס פנימה. אחר כך יש מקום להיכנס מאחוריו
                        float side = mallet.x >= q.x ? 1f : -1f;
                        if (Mathf.Abs(q.x + side * (contact + 0.01f)) > rink.HalfWidth - rink.MalletRadius) side = -side;
                        Vector2 beside = new Vector2(q.x + side * (contact + 0.01f), q.y);
                        target = (mallet - beside).magnitude > 0.04f ? beside : new Vector2(q.x - side * 0.2f, q.y);
                        if (attackTime > 3f) attackTime = 0f;
                    }
                    else { target = behind; approach = true; }
                }
            }
            else if (v.y > 0.05f)
            {
                // הגנה: הפאק בדרך אליו. הטעות גדלה עם מהירות הפאק: מכה חזקה קשה יותר לקרוא
                if (sign < 0f) error = Range(-1f, 1f) * settings.Error * (1f + v.magnitude / 6f);
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

            bool approaching = approach; approach = false;
            // לא נכנס בפאק שבינו לבין השער שלו: עוקף מהצד. גם באמצע מכה (ב-Easy הוא רואה באיחור ומנחש לא נכון
            // איפה הפאק, ומכה שהתחילה מהצד הלא נכון עלולה לשלוח אותו פנימה): אז המכה נעצרת
            if (puckInPlay)
            {
                Vector2 safe = AvoidOwnGoal(mallet, now, target, contact, rink);
                if (striking && (safe - target).sqrMagnitude > 1e-6f) { striking = false; attackTime = 0f; }
                target = safe;
            }
            return Move(mallet, target, striking, dt, rink, approaching ? settings.MaxSpeed * 0.6f : 0f);
        }

        // פאק קרוב שנמצא בצד של השער שלו (y גבוה יותר מהמחבט), כשמכה בכיוון התנועה הייתה שולחת אותו אל פתח השער:
        // לא נוסעים דרכו. קודם הצידה עד שאין חפיפה, ואז מעלה לצדו. דחיפה הצידה (לאורך הדופן האחורית) מותרת:
        // ככה מוציאים פאק שנתקע צמוד לדופן או מול השער.
        public static Vector2 AvoidOwnGoal(Vector2 mallet, Vector2 puck, Vector2 target, float contact, in HockeyRink rink)
        {
            Vector2 toPuck = puck - mallet;
            if (puck.y < mallet.y - 0.01f || toPuck.magnitude > contact + 0.14f) return target;
            if (Vector2.Dot(target - mallet, toPuck) <= 0f) return target;   // היעד בכיוון אחר: אין סכנה
            if (!PushesIntoGoal(mallet, puck, rink)) return target;
            float side = mallet.x >= puck.x ? 1f : -1f;
            float sideX = puck.x + side * (contact + 0.08f);
            if (Mathf.Abs(sideX) > rink.HalfWidth - rink.MalletRadius) { side = -side; sideX = puck.x + side * (contact + 0.08f); }
            return Mathf.Abs(mallet.x - puck.x) < contact + 0.04f
                ? new Vector2(sideX, mallet.y)
                : new Vector2(sideX, Mathf.Max(mallet.y, puck.y + 0.03f));
        }

        // מכה מהמחבט אל הפאק: לאן היא שולחת אותו על הקו של השער שלו (קו ישר, בלי קפיצות)
        public static bool PushesIntoGoal(Vector2 mallet, Vector2 puck, in HockeyRink rink)
        {
            Vector2 dir = (puck - mallet).normalized;
            if (dir.y < 0.15f) return false;   // הצידה או אחורה
            if (dir.y > 0.5f) return true;     // בחדות אל הקצה שלו: גם בלי לכוון לפתח, הפאק חוזר מהדופן או נכנס דרך הצד
            float x = puck.x + dir.x / dir.y * (rink.HalfLength - puck.y);
            return Mathf.Abs(x) < rink.GoalHalfWidth + rink.PuckRadius * 2f;
        }

        // תנועה של יד: מאיצה עד המהירות המותרת, בולמת לפני היעד (חוץ ממכה, שעוברת דרכו), ולא יותר מהתאוצה
        private Vector2 Move(Vector2 mallet, Vector2 target, bool strike, float dt, in HockeyRink rink, float minSpeed = 0f)
        {
            // היעד בתוך האזור שלו: כך הוא בולם לפני הדופן או קו האמצע, ולא נעצר בהם בבת אחת
            Vector2 inside = Clamp(target, rink);
            bool atEdge = (inside - target).sqrMagnitude > 1e-6f;
            target = inside;
            if (strike || !hasTarget) smoothTarget = target;
            else smoothTarget = Vector2.Lerp(smoothTarget, target, 1f - Mathf.Exp(-dt * 25f));
            hasTarget = true;
            float limit = strike ? settings.StrikeSpeed : settings.MaxSpeed;
            float accel = strike ? settings.StrikeAccel : settings.Accel;
            Vector2 to = smoothTarget - mallet;
            float distance = to.magnitude;
            float want = strike && !atEdge ? limit : Mathf.Min(limit, Mathf.Max(minSpeed, Mathf.Sqrt(2f * accel * distance) * 0.9f));
            Vector2 direction = distance > 1e-4f ? to / distance : Vector2.zero;
            // גם במכה: בולם לפני הקצה של האזור שלו בכיוון התנועה
            want = Mathf.Min(want, Mathf.Sqrt(2f * accel * EdgeDistance(mallet, direction, rink)));
            Vector2 desired = direction * want;
            velocity = Vector2.MoveTowards(velocity, desired, accel * dt);
            // לכל ציר: לא מהר יותר ממה שאפשר לבלום עד הקצה (גם כשהוא מסתובב והמהירות לא לכיוון היעד)
            // תמיד אותה בלימה (70% מהתאוצה של מכה): בלי זה, סוף מכה ליד הקצה עבר בבת אחת לבלימה של מצב רגיל
            float r = rink.MalletRadius, room = 2f * 0.7f * settings.StrikeAccel;
            if (velocity.x > 0f) velocity.x = Mathf.Min(velocity.x, Mathf.Sqrt(room * Mathf.Max(0f, rink.HalfWidth - r - mallet.x)));
            if (velocity.x < 0f) velocity.x = -Mathf.Min(-velocity.x, Mathf.Sqrt(room * Mathf.Max(0f, mallet.x + rink.HalfWidth - r)));
            if (velocity.y > 0f) velocity.y = Mathf.Min(velocity.y, Mathf.Sqrt(room * Mathf.Max(0f, rink.HalfLength - r - mallet.y)));
            if (velocity.y < 0f) velocity.y = -Mathf.Min(-velocity.y, Mathf.Sqrt(room * Mathf.Max(0f, mallet.y - r)));
            Vector2 next = Clamp(mallet + velocity * dt, rink);
            velocity = dt > 0f ? (next - mallet) / dt : Vector2.zero;   // נעצר בדופן / בקו האמצע
            return next;
        }

        private void StartAttack(in HockeyRink rink)
        {
            striking = false;
            attackTime = 0f;
            hesitate = settings.Hesitation * Range(0.6f, 1.4f);
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

        // כמה אפשר לנסוע מכאן בכיוון הזה עד הקצה של האזור שלו
        private static float EdgeDistance(Vector2 from, Vector2 direction, in HockeyRink rink)
        {
            float r = rink.MalletRadius, best = float.MaxValue;
            if (direction.x > 1e-4f) best = Mathf.Min(best, (rink.HalfWidth - r - from.x) / direction.x);
            if (direction.x < -1e-4f) best = Mathf.Min(best, (-rink.HalfWidth + r - from.x) / direction.x);
            if (direction.y > 1e-4f) best = Mathf.Min(best, (rink.HalfLength - r - from.y) / direction.y);
            if (direction.y < -1e-4f) best = Mathf.Min(best, (r - from.y) / direction.y);
            return Mathf.Max(0f, best);
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
