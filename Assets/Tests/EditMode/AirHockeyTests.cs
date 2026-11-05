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
            Assert.That(-v.x, Is.InRange(1.6f, 1.8f), "about 88% of the speed");
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
        {
            ai.Reset();
            var mallet = new Vector2(0f, Rink.HalfLength - ai.Level.DefenseDepth);
            var nobody = new Vector2(0f, -5f);
            lowestPuck = puck; leftItsHalf = false;
            for (float t = 0f; t < seconds; t += Dt)
            {
                Vector2 next = ai.Step(mallet, puck, velocity, true, Dt, Rink);
                if (next.y < Rink.MalletRadius - 1e-4f || Mathf.Abs(next.x) > Rink.HalfWidth - Rink.MalletRadius + 1e-4f) leftItsHalf = true;
                var r = HockeyPhysics.Step(ref puck, ref velocity, nobody, nobody, mallet, next, Dt, Rink);
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
                        var goal = Play(ai, new Vector2(x, y), Vector2.zero, 6f, out Vector2 lowest, out _);
                        Assert.That(goal != GoalScored.None || lowest.y < 0f, Is.True, $"{level}: stuck with the puck at ({x}, {y})");
                    }
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
