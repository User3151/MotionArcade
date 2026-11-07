using MotionArcade.Games.AirHockey;
using NUnit.Framework;
using UnityEngine;

namespace MotionArcade.Tests
{
    // הפיזיקה של הפאק, הרובוט והחוקים. מרחב המשטח: x לרוחב, y לאורך, השער שלך ב- y=-1, של היריב ב- y=+1.
    public class HockeyPhysicsTests
    {
        private static readonly HockeyRink Rink = HockeyRink.Standard;
        private static readonly Vector2 Away = new Vector2(0f, 5f);   // מחבט "רחוק" שלא נוגע בכלום

        private static HockeyStepResult Step(ref Vector2 p, ref Vector2 v, float dt, Vector2? playerFrom = null, Vector2? playerTo = null)
        {
            Vector2 a = playerFrom ?? -Away, b = playerTo ?? a;
            return HockeyPhysics.Step(ref p, ref v, a, b, Away, Away, dt, Rink);
        }

        [Test]
        public void SideWall_BouncesWithALittleLoss()
        {
            var p = new Vector2(0.4f, 0f); var v = new Vector2(2f, 0f);
            var r = Step(ref p, ref v, 0.2f);
            Assert.That(r.WallHit, Is.True);
            Assert.That(v.x, Is.LessThan(0f));
            Assert.That(-v.x, Is.InRange(1.5f, 1.7f), "about 82% of the speed");
            Assert.That(p.x, Is.LessThanOrEqualTo(Rink.HalfWidth - Rink.PuckRadius + 1e-4f));
        }

        [Test]
        public void EndWall_BesideTheGoal_Bounces()
        {
            var p = new Vector2(0.35f, 0.8f); var v = new Vector2(0f, 3f);
            var r = Step(ref p, ref v, 0.2f);
            Assert.That(r.Goal, Is.EqualTo(GoalScored.None));
            Assert.That(v.y, Is.LessThan(0f));
        }

        [Test]
        public void IntoTheMouth_IsAGoal_ForTheRightSide()
        {
            var p = new Vector2(0f, 0.8f); var v = new Vector2(0f, 3f);
            Assert.That(Step(ref p, ref v, 0.2f).Goal, Is.EqualTo(GoalScored.ByPlayer));
            p = new Vector2(0.05f, -0.8f); v = new Vector2(0f, -3f);
            Assert.That(Step(ref p, ref v, 0.2f).Goal, Is.EqualTo(GoalScored.ByOpponent));
        }

        [Test]
        public void AFastPuck_DoesNotPassThroughAMallet()
        {
            var p = new Vector2(0f, 0f); var v = new Vector2(0f, -7f);
            var mallet = new Vector2(0f, -0.3f);
            var r = Step(ref p, ref v, 0.1f, mallet, mallet);
            Assert.That(r.MalletHit, Is.EqualTo(0));
            Assert.That(v.y, Is.GreaterThan(0f), "bounced back");
            Assert.That(p.y, Is.GreaterThan(mallet.y));
        }

        [Test]
        public void AMovingMallet_SendsThePuckFasterThanItMoves()
        {
            const float dt = 1f / 90f;
            var p = new Vector2(0f, -0.49f); var v = Vector2.zero;
            var from = new Vector2(0f, -0.6f); var to = from + new Vector2(0f, 2f * dt);   // 2 מ'/ש'
            var r = Step(ref p, ref v, dt, from, to);
            Assert.That(r.MalletHit, Is.EqualTo(0));
            Assert.That(v.y, Is.GreaterThan(3f), "an infinitely heavy mallet: (1 + restitution) times its speed");
        }

        [Test]
        public void Speed_IsCapped()
        {
            const float dt = 1f / 90f;
            var p = new Vector2(0f, -0.49f); var v = Vector2.zero;
            var from = new Vector2(0f, -0.6f); var to = from + new Vector2(0f, 10f * dt);
            Step(ref p, ref v, dt, from, to);
            Assert.That(v.magnitude, Is.LessThanOrEqualTo(Rink.MaxSpeed + 1e-3f));
        }

        [Test]
        public void APuckTrappedBetweenTheMalletAndTheWall_SlowsDown()
        {
            // גיא: פאק מאחורי המחבט, ליד הקיר, קפץ הלוך ושוב ביניהם והגיע למהירויות מוגזמות
            const float dt = 1f / 90f;
            var p = new Vector2(0f, -0.93f); var v = new Vector2(0f, -3f);
            var mallet = new Vector2(0f, -0.82f);
            float max = 0f;
            for (int i = 0; i < 180; i++)
            {
                var next = mallet + new Vector2(0f, -0.3f * dt);   // נלחץ לאט לכיוון הקיר
                if (next.y < -Rink.HalfLength + Rink.MalletRadius) next.y = -Rink.HalfLength + Rink.MalletRadius;
                var r = HockeyPhysics.Step(ref p, ref v, mallet, next, Away, Away, dt, Rink);
                mallet = next;
                if (r.Goal != GoalScored.None) break;
                if (i > 20) max = Mathf.Max(max, v.magnitude);
            }
            Assert.That(max, Is.LessThan(3f), "it loses speed instead of gaining it");
        }

        [Test]
        public void PredictPosition_BouncesOffEveryWall()
        {
            var p = HockeyPhysics.PredictPosition(new Vector2(0.4f, 0.9f), new Vector2(1f, 1f), 0.2f, Rink);
            Assert.That(p.x, Is.LessThanOrEqualTo(Rink.HalfWidth - Rink.PuckRadius + 1e-4f));
            Assert.That(p.y, Is.LessThanOrEqualTo(Rink.HalfLength - Rink.PuckRadius + 1e-4f));
            Assert.That(p.x, Is.EqualTo(0.37f).Within(1e-3f), "0.6 folded at 0.485");
        }

        [Test]
        public void PredictX_FoldsBouncesOffTheSides()
        {
            // ימינה, פוגע בדופן ב-0.485, חוזר שמאלה עוד 0.515
            float x = HockeyPhysics.PredictX(Vector2.zero, new Vector2(1f, 1f), 1f, Rink);
            Assert.That(x, Is.EqualTo(-0.03f).Within(1e-3f));
        }
    }

    public class HockeyAITests
    {
        private static readonly HockeyRink Rink = HockeyRink.Standard;
        private const float Dt = 1f / 90f;

        // מריץ את הפאק והרובוט יחד; השחקן לא משחק. מחזיר איזה גול נכנס (או None).
        private static GoalScored Play(HockeyAI ai, Vector2 puck, Vector2 velocity, float seconds, out Vector2 lowestPuck, out bool leftItsHalf)
            => Play(ai, puck, velocity, seconds, out lowestPuck, out leftItsHalf, out _);

        private static GoalScored Play(HockeyAI ai, Vector2 puck, Vector2 velocity, float seconds, out Vector2 lowestPuck, out bool leftItsHalf, out bool robotTouchedLast)
        {
            robotTouchedLast = false;
            ai.Reset();
            var mallet = new Vector2(0f, Rink.HalfLength - ai.Level.DefenseDepth);
            var nobody = new Vector2(0f, -5f);
            lowestPuck = puck; leftItsHalf = false;
            for (float t = 0f; t < seconds; t += Dt)
            {
                Vector2 next = ai.Step(mallet, puck, velocity, true, Dt, Rink);
                if (next.y < Rink.MalletRadius - 1e-4f || Mathf.Abs(next.x) > Rink.HalfWidth - Rink.MalletRadius + 1e-4f) leftItsHalf = true;
                var r = HockeyPhysics.Step(ref puck, ref velocity, nobody, nobody, mallet, next, Dt, Rink);
                if (r.MalletHit == 1) robotTouchedLast = true;
                mallet = next;
                if (puck.y < lowestPuck.y) lowestPuck = puck;
                if (r.Goal != GoalScored.None) return r.Goal;
            }
            return GoalScored.None;
        }

        [Test]
        public void Levels_GetFasterSharperAndQuicker()
        {
            var easy = HockeyAI.Settings.For(HockeyDifficulty.Easy);
            var normal = HockeyAI.Settings.For(HockeyDifficulty.Normal);
            var hard = HockeyAI.Settings.For(HockeyDifficulty.Hard);
            Assert.That(easy.MaxSpeed, Is.LessThan(normal.MaxSpeed));
            Assert.That(normal.MaxSpeed, Is.LessThan(hard.MaxSpeed));
            Assert.That(easy.Reaction, Is.GreaterThan(normal.Reaction));
            Assert.That(normal.Reaction, Is.GreaterThan(hard.Reaction));
            Assert.That(easy.Error, Is.GreaterThan(hard.Error));
            Assert.That(easy.StrikeSpeed, Is.LessThan(hard.StrikeSpeed));
        }

        [Test]
        public void Hard_BlocksAStraightShot()
        {
            var ai = new HockeyAI(HockeyDifficulty.Hard, seed: 3);
            var goal = Play(ai, new Vector2(0.1f, -0.5f), new Vector2(0f, 3f), 1.5f, out _, out _);
            Assert.That(goal, Is.Not.EqualTo(GoalScored.ByPlayer));
        }

        [Test]
        public void Easy_LetsInMoreThanHard()
        {
            int Goals(HockeyDifficulty level)
            {
                var ai = new HockeyAI(level, seed: 11);
                var shots = new System.Random(5);
                int goals = 0;
                for (int i = 0; i < 20; i++)
                {
                    var from = new Vector2((float)(shots.NextDouble() - 0.5) * 0.8f, -0.6f);
                    var aim = new Vector2((float)(shots.NextDouble() - 0.5) * 0.25f, 1f);
                    var velocity = (aim - from).normalized * (3f + (float)shots.NextDouble() * 2f);
                    if (Play(ai, from, velocity, 1.2f, out _, out _) == GoalScored.ByPlayer) goals++;
                }
                return goals;
            }
            int easy = Goals(HockeyDifficulty.Easy), hard = Goals(HockeyDifficulty.Hard);
            Assert.That(easy, Is.GreaterThan(hard), $"easy let in {easy}, hard {hard}");
            Assert.That(hard, Is.LessThan(10), "hard stops most of them");
        }

        [Test]
        public void AttacksASlowPuck_InItsHalf_AndStaysInItsHalf()
        {
            foreach (HockeyDifficulty level in new[] { HockeyDifficulty.Easy, HockeyDifficulty.Normal, HockeyDifficulty.Hard })
            {
                var ai = new HockeyAI(level, seed: 7);
                Play(ai, new Vector2(0.15f, 0.45f), Vector2.zero, 4f, out Vector2 lowest, out bool left);
                Assert.That(lowest.y, Is.LessThan(0f), level + ": the puck was sent over to the player");
                Assert.That(left, Is.False, level + ": never past the centre line or into a wall");
            }
        }

        [Test]
        public void NeverStalls_WithAStillPuckAnywhereInItsHalf()
        {
            // מהמשחק האוטומטי: מכה שהחטיאה השאירה את הרובוט עומד מול פאק שעומד. מכל נקודה בחצי שלו, בכל רמה,
            // הפאק חייב לחזור לחצי של השחקן (או להיכנס לשער) תוך 6 שניות
            foreach (HockeyDifficulty level in new[] { HockeyDifficulty.Easy, HockeyDifficulty.Normal, HockeyDifficulty.Hard })
                foreach (float x in new[] { -0.44f, -0.2f, 0f, 0.2f, 0.44f })
                    foreach (float y in new[] { 0.12f, 0.38f, 0.65f, 0.92f })
                    {
                        var ai = new HockeyAI(level, seed: 1 + (int)(x * 100) + (int)(y * 1000));
                        // לא נתקע: מגיע לפאק ומזיז אותו (ב-Easy המכות חלשות ולפעמים לצד, כמו של מתחיל)
                        var goal = Play(ai, new Vector2(x, y), Vector2.zero, 6f, out Vector2 lowest, out _, out bool touched);
                        Assert.That(goal != GoalScored.None || lowest.y < 0f || touched, Is.True, $"{level}: stuck with the puck at ({x}, {y})");
                    }
        }

        [Test]
        public void AttacksAPuckMovingSidewaysInItsHalf()
        {
            // גיא: כשהפאק זז מצד לצד בחצי שלו, הרובוט נשאר מאחור ועקב אחריו במקום לתקוף. בכל הרמות
            foreach (HockeyDifficulty level in new[] { HockeyDifficulty.Easy, HockeyDifficulty.Normal, HockeyDifficulty.Hard })
                foreach (float speed in new[] { 0.8f, 1.5f })
                {
                    var ai = new HockeyAI(level, seed: 5);
                    var goal = Play(ai, new Vector2(-0.3f, 0.35f), new Vector2(speed, 0.05f), 5f, out Vector2 lowest, out bool left);
                    Assert.That(goal == GoalScored.ByOpponent || lowest.y < 0f, Is.True, $"{level} at {speed} m/s: the puck was never sent back");
                    Assert.That(left, Is.False, level + ": stays in its half");
                }
        }

        [Test]
        public void NeverScoresOnItself()
        {
            // גיא: גם ב-Hard הרובוט הכניס לעצמו גולים, מהמהירות וצורת התנועה. פאק איטי בכל מקום בחצי שלו, לכל כיוון.
            // גול עצמי = הרובוט נגע בפאק אחרון. פאק שמתחיל 10 ס"מ מהשער ונכנס בלי שנגע בו לא נספר: גם אדם לא היה מספיק
            int own = 0, runs = 0;
            foreach (HockeyDifficulty level in new[] { HockeyDifficulty.Easy, HockeyDifficulty.Normal, HockeyDifficulty.Hard })
                foreach (float x in new[] { -0.4f, -0.15f, 0f, 0.15f, 0.4f })
                    foreach (float y in new[] { 0.15f, 0.45f, 0.75f, 0.9f })
                        foreach (var v in new[] { Vector2.zero, new Vector2(0.5f, 0f), new Vector2(-0.4f, 0.3f), new Vector2(0.2f, 0.5f) })
                        {
                            if (y > 0.85f && v.y > 0f) continue;   // כבר בפתח השער ובדרך פנימה: גם אדם לא היה מספיק
                            var ai = new HockeyAI(level, seed: 3 + runs);
                            runs++;
                            if (Play(ai, new Vector2(x, y), v, 6f, out _, out _, out bool touched) == GoalScored.ByPlayer && touched) own++;
                        }
            Assert.That(own, Is.LessThanOrEqualTo(runs / 40), $"{own} own goals in {runs} slow-puck situations");
        }

        [Test]
        public void MovesLikeAHand_NotInJumps()
        {
            // תאוצה מוגבלת: אין קפיצה מעמידה למהירות מלאה בצעד אחד
            const float dt = 1f / 90f;
            foreach (HockeyDifficulty level in new[] { HockeyDifficulty.Easy, HockeyDifficulty.Normal, HockeyDifficulty.Hard })
            {
                var ai = new HockeyAI(level, seed: 4);
                ai.Reset();
                var mallet = new Vector2(0f, Rink.HalfLength - ai.Level.DefenseDepth);
                var puck = new Vector2(0.35f, -0.6f); var v = new Vector2(-0.6f, 3f);
                var nobody = new Vector2(0f, -5f);
                Vector2 last = Vector2.zero; float maxAccel = 0f, maxSpeed = 0f;
                for (int i = 0; i < 400; i++)
                {
                    var next = ai.Step(mallet, puck, v, true, dt, Rink);
                    HockeyPhysics.Step(ref puck, ref v, nobody, nobody, mallet, next, dt, Rink);
                    var vel = (next - mallet) / dt; mallet = next;
                    if (i > 0) maxAccel = Mathf.Max(maxAccel, (vel - last).magnitude / dt);
                    maxSpeed = Mathf.Max(maxSpeed, vel.magnitude); last = vel;
                }
                // לפני התיקון: קפיצות של 100+ מ'/ש' לשנייה. בלימה בצעדים בדידים יכולה לעבור קצת את התאוצה
                Assert.That(maxAccel, Is.LessThanOrEqualTo(ai.Level.StrikeAccel * 1.5f), level + " accelerates like a hand");
                Assert.That(maxSpeed, Is.LessThanOrEqualTo(ai.Level.StrikeSpeed + 0.01f), level.ToString());
            }
            var easy = HockeyAI.Settings.For(HockeyDifficulty.Easy); var hard = HockeyAI.Settings.For(HockeyDifficulty.Hard);
            Assert.That(hard.Accel, Is.LessThan(30f), "fast, but it builds up speed (no jumps)");
            Assert.That(easy.Accel, Is.LessThan(hard.Accel));
        }

        [Test]
        public void StrikesAStillPuckQuickly_AtEveryLevel()
        {
            // גיא: הרובוט "המון זמן מול הדיסקית ונשאר מולו עד שהוא מחליט לתת מכה"
            foreach (HockeyDifficulty level in new[] { HockeyDifficulty.Easy, HockeyDifficulty.Normal, HockeyDifficulty.Hard })
                foreach (var start in new[] { new Vector2(0f, 0.3f), new Vector2(0.3f, 0.4f), new Vector2(-0.3f, 0.5f), new Vector2(0.1f, 0.65f) })
                {
                    var ai = new HockeyAI(level, seed: 2);
                    ai.Reset();
                    var mallet = new Vector2(0f, Rink.HalfLength - ai.Level.DefenseDepth);
                    var puck = start; var v = Vector2.zero; var nobody = new Vector2(0f, -5f);
                    float struck = -1f;
                    for (float t = 0f; t < 3f && struck < 0f; t += Dt)
                    {
                        var next = ai.Step(mallet, puck, v, true, Dt, Rink);
                        var r = HockeyPhysics.Step(ref puck, ref v, nobody, nobody, mallet, next, Dt, Rink);
                        mallet = next;
                        if (r.MalletHit == 1 && v.y < -0.3f) struck = t;
                    }
                    Assert.That(struck, Is.InRange(0f, 1.3f), $"{level}: a still puck at {start} was struck toward you after {struck:0.00} s");
                }
        }

        [Test]
        public void Hard_StillLetsSomeShotsIn()
        {
            // גיא: ב-Hard הוא חזה כל מכה ולא היה אפשר להבקיע בכלל
            var ai = new HockeyAI(HockeyDifficulty.Hard, seed: 11);
            var shots = new System.Random(9);
            int goals = 0;
            for (int i = 0; i < 30; i++)
            {
                var from = new Vector2((float)(shots.NextDouble() - 0.5) * 0.8f, -0.55f);
                var aim = new Vector2((float)(shots.NextDouble() - 0.5) * 0.25f, 1f);
                if (shots.NextDouble() < 0.4) aim.x = (aim.x >= 0f ? 1f : -1f) * 2f * (Rink.HalfWidth - Rink.PuckRadius) - aim.x;   // דרך הדופן
                var velocity = (aim - from).normalized * (3.2f + (float)shots.NextDouble() * 1.3f);
                if (Play(ai, from, velocity, 1.4f, out _, out _) == GoalScored.ByPlayer) goals++;
            }
            Assert.That(goals, Is.InRange(2, 15), $"hard let in {goals} of 30 good shots");
        }

        [Test]
        public void APuckStuckAtItsBackWall_StillGetsPushedOut()
        {
            var ai = new HockeyAI(HockeyDifficulty.Normal, seed: 2);
            Play(ai, new Vector2(0.4f, Rink.HalfLength - Rink.PuckRadius - 0.001f), Vector2.zero, 5f, out Vector2 lowest, out _);
            Assert.That(lowest.y, Is.LessThan(Rink.HalfLength - 0.2f));
        }
    }

    public class HockeyMatchTests
    {
        [Test]
        public void FirstToSeven_Wins_AndThenNothingCounts()
        {
            var match = new HockeyMatch();
            for (int i = 0; i < 6; i++) match.Score(GoalScored.ByOpponent);
            for (int i = 0; i < 7; i++) Assert.That(match.Score(GoalScored.ByPlayer), Is.True);
            Assert.That(match.Over, Is.True);
            Assert.That(match.PlayerWon, Is.True);
            Assert.That(match.Score(GoalScored.ByOpponent), Is.False);
            Assert.That(match.Opponent, Is.EqualTo(6));
        }

        [Test]
        public void ThePuck_GoesToWhoeverConceded()
        {
            var match = new HockeyMatch();
            Assert.That(match.ServeToPlayer, Is.True, "the player starts");
            match.Score(GoalScored.ByPlayer);
            Assert.That(match.ServeToPlayer, Is.False);
            match.Score(GoalScored.ByOpponent);
            Assert.That(match.ServeToPlayer, Is.True);
            match.Reset();
            Assert.That(match.Player + match.Opponent, Is.EqualTo(0));
        }
    }
}
