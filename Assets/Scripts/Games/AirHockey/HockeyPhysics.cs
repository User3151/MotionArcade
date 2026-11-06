using UnityEngine;

namespace MotionArcade.Games.AirHockey
{
    // הפיזיקה של הפאק, דו-ממדית במרחב של המשטח: x לרוחב, y לאורך (מהשער של השחקן ב- -HalfLength
    // אל השער של היריב ב- +HalfLength). לא PhysX: פאק של 6 מ'/ש' ומחבט שהיד מזיזה קינמטית עוברים
    // אחד דרך השני ב-PhysX. כאן כל צעד מתחלק לצעדים קטנים (פחות מחצי רדיוס תנועה בכל אחד),
    // והמחבט נע בתוכם בקו ישר מהמקום הקודם שלו לחדש, כך שגם מכה מהירה פוגשת את הפאק.
    // המחבט כבד בלי סוף (היד לא נעצרת), אז הוא מעביר לפאק את המהירות שלו ועוד הקפצה.
    public struct HockeyRink
    {
        public float HalfWidth, HalfLength, GoalHalfWidth;
        public float PuckRadius, MalletRadius;
        public float WallRestitution, MalletRestitution;
        public float Friction;     // דעיכה לשנייה (כרית אוויר: מעט)
        public float MaxSpeed;

        public static HockeyRink Standard => new HockeyRink
        {
            HalfWidth = 0.525f, HalfLength = 1.0f, GoalHalfWidth = 0.15f,
            PuckRadius = 0.04f, MalletRadius = 0.05f,
            // גיא: הפאק מהיר מדי. 4.5 מ'/ש' עדיין מכה חזקה בשולחן של 2 מ'
            WallRestitution = 0.82f, MalletRestitution = 0.6f,
            Friction = 0.2f, MaxSpeed = 4.5f,
        };
    }

    public enum GoalScored { None, ByPlayer, ByOpponent }

    public struct HockeyStepResult
    {
        public GoalScored Goal;
        public int MalletHit;        // -1 = אף אחד, 0 = השחקן, 1 = היריב
        public float HitSpeed;       // מהירות יחסית בפגיעה, לעוצמת הצליל והרטט
        public bool WallHit;
        public float WallSpeed;
    }

    public static class HockeyPhysics
    {
        public static HockeyStepResult Step(ref Vector2 position, ref Vector2 velocity,
            Vector2 playerFrom, Vector2 playerTo, Vector2 opponentFrom, Vector2 opponentTo, float dt, in HockeyRink rink)
        {
            var result = new HockeyStepResult { MalletHit = -1 };
            if (dt <= 0f) return result;

            float travel = Mathf.Max(velocity.magnitude * dt, (playerTo - playerFrom).magnitude, (opponentTo - opponentFrom).magnitude);
            int steps = Mathf.Clamp(Mathf.CeilToInt(travel / (rink.PuckRadius * 0.4f)), 1, 48);
            float h = dt / steps;
            Vector2 playerVelocity = (playerTo - playerFrom) / dt, opponentVelocity = (opponentTo - opponentFrom) / dt;

            for (int s = 1; s <= steps; s++)
            {
                float t = (float)s / steps;
                position += velocity * h;
                velocity *= Mathf.Max(0f, 1f - rink.Friction * h);

                Collide(ref position, ref velocity, Vector2.Lerp(playerFrom, playerTo, t), playerVelocity, 0, rink, ref result);
                Collide(ref position, ref velocity, Vector2.Lerp(opponentFrom, opponentTo, t), opponentVelocity, 1, rink, ref result);
                if (Walls(ref position, ref velocity, rink, ref result)) return result;

                float speed = velocity.magnitude;
                if (speed > rink.MaxSpeed) velocity *= rink.MaxSpeed / speed;
            }
            return result;
        }

        private static void Collide(ref Vector2 position, ref Vector2 velocity, Vector2 mallet, Vector2 malletVelocity, int index,
            in HockeyRink rink, ref HockeyStepResult result)
        {
            float reach = rink.PuckRadius + rink.MalletRadius;
            Vector2 offset = position - mallet;
            float distance = offset.magnitude;
            if (distance >= reach) return;
            Vector2 normal = distance > 1e-5f ? offset / distance : Vector2.up * (index == 0 ? 1f : -1f);
            position = mallet + normal * reach;
            Vector2 relative = velocity - malletVelocity;
            float approach = Vector2.Dot(relative, normal);
            if (approach >= 0f) return;   // כבר מתרחקים
            float before = velocity.magnitude;
            velocity -= (1f + rink.MalletRestitution) * approach * normal;
            // תקרה למה שמכה אחת מוסיפה: פאק שנלכד בין המחבט לדופן קפץ ביניהם ונהיה מהיר יותר ויותר (גיא).
            // מכה אמיתית מעבירה בערך את מהירות המחבט; קפיצה בין מחבט כמעט עומד לדופן מאבדת אנרגיה
            float limit = Mathf.Max(before * 0.9f, malletVelocity.magnitude * 1.7f + 0.4f);
            float after = velocity.magnitude;
            if (after > limit) velocity *= limit / after;
            if (-approach > result.HitSpeed) { result.HitSpeed = -approach; result.MalletHit = index; }
        }

        // true = גול (הפאק נכנס לשער ונעצר שם)
        private static bool Walls(ref Vector2 position, ref Vector2 velocity, in HockeyRink rink, ref HockeyStepResult result)
        {
            float r = rink.PuckRadius;
            float maxX = rink.HalfWidth - r;
            if (position.x > maxX) { position.x = 2f * maxX - position.x; Bounce(ref velocity.x, -1f, rink, ref result); }
            else if (position.x < -maxX) { position.x = -2f * maxX - position.x; Bounce(ref velocity.x, 1f, rink, ref result); }

            float maxY = rink.HalfLength - r;
            if (Mathf.Abs(position.y) <= maxY) return false;
            float end = Mathf.Sign(position.y);
            bool inMouth = Mathf.Abs(position.x) < rink.GoalHalfWidth;
            if (inMouth)
            {
                // עמודי השער: הפינות של הפתח, כמו עיגול ברדיוס אפס
                for (int side = -1; side <= 1; side += 2)
                {
                    var post = new Vector2(side * rink.GoalHalfWidth, end * rink.HalfLength);
                    Vector2 offset = position - post;
                    float d = offset.magnitude;
                    if (d < r && d > 1e-5f)
                    {
                        Vector2 n = offset / d;
                        position = post + n * r;
                        float vn = Vector2.Dot(velocity, n);
                        if (vn < 0f) { velocity -= (1f + rink.WallRestitution) * vn * n; result.WallHit = true; result.WallSpeed = Mathf.Max(result.WallSpeed, -vn); }
                    }
                }
                if (Mathf.Abs(position.y) > rink.HalfLength && Mathf.Abs(position.x) < rink.GoalHalfWidth)
                {
                    result.Goal = end > 0f ? GoalScored.ByPlayer : GoalScored.ByOpponent;
                    velocity = Vector2.zero;
                    return true;
                }
                return false;
            }
            position.y = end * (2f * maxY) - position.y;
            Bounce(ref velocity.y, -end, rink, ref result);
            return false;
        }

        private static void Bounce(ref float component, float inward, in HockeyRink rink, ref HockeyStepResult result)
        {
            if (component * inward >= 0f) return;
            result.WallHit = true;
            result.WallSpeed = Mathf.Max(result.WallSpeed, Mathf.Abs(component));
            component = -component * rink.WallRestitution;
        }

        // איפה הפאק יהיה בעוד t שניות, עם קפיצות מכל הדפנות (בלי שערים ובלי חיכוך): לניחוש של הרובוט
        public static Vector2 PredictPosition(Vector2 position, Vector2 velocity, float t, in HockeyRink rink)
        {
            Vector2 raw = position + velocity * Mathf.Max(0f, t);
            return new Vector2(Fold(raw.x, rink.HalfWidth - rink.PuckRadius), Fold(raw.y, rink.HalfLength - rink.PuckRadius));
        }

        private static float Fold(float x, float max)
        {
            float span = 2f * max;
            float u = Mathf.Repeat(x + max, 2f * span);
            return (u <= span ? u : 2f * span - u) - max;
        }

        // איפה הפאק יחצה את הקו y (עם קפיצות מהדפנות). למטה: לאן היריב צריך להגיע כדי לחסום.
        public static float PredictX(Vector2 position, Vector2 velocity, float lineY, in HockeyRink rink)
        {
            if (Mathf.Abs(velocity.y) < 1e-4f) return position.x;
            float time = (lineY - position.y) / velocity.y;
            if (time < 0f) return position.x;
            float maxX = rink.HalfWidth - rink.PuckRadius;
            float x = position.x + velocity.x * time;
            // קיפול: קפיצות מהדפנות הן שיקוף במרווח [-maxX, maxX]
            float span = 2f * maxX;
            float u = Mathf.Repeat(x + maxX, 2f * span);
            return (u <= span ? u : 2f * span - u) - maxX;
        }
    }
}
