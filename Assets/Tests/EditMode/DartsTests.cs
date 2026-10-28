using System;
using MotionArcade.Core;
using MotionArcade.Games.Darts;
using NUnit.Framework;

namespace MotionArcade.Tests
{
    public class DartScoringTests
    {
        // נקודה במרחק r מהמרכז, בזווית במעלות בכיוון השעון מלמעלה
        private static DartHit At(float r, float degrees)
        {
            double a = degrees * Math.PI / 180.0;
            return DartScoring.Score((float)(Math.Sin(a) * r), (float)(Math.Cos(a) * r));
        }

        [Test]
        public void Bulls()
        {
            Assert.That(DartScoring.Score(0f, 0f), Is.EqualTo(new DartHit(25, 2)));
            Assert.That(DartScoring.Score(0f, 0f).Points, Is.EqualTo(50));
            Assert.That(At(0.010f, 123f), Is.EqualTo(new DartHit(25, 1)));
            Assert.That(At(0.010f, 123f).Points, Is.EqualTo(25));
        }

        [Test]
        public void TwentyAtTheTop_RingsByRadius()
        {
            Assert.That(At(0.05f, 0f), Is.EqualTo(new DartHit(20, 1)));
            Assert.That(At(0.103f, 0f), Is.EqualTo(new DartHit(20, 3)));
            Assert.That(At(0.103f, 0f).Points, Is.EqualTo(60));
            Assert.That(At(0.135f, 0f), Is.EqualTo(new DartHit(20, 1)));
            Assert.That(At(0.166f, 0f), Is.EqualTo(new DartHit(20, 2)));
            Assert.That(At(0.175f, 0f).IsMiss, Is.True);
        }

        [Test]
        public void CompassPoints_FollowTheStandardOrder()
        {
            Assert.That(At(0.05f, 90f).Number, Is.EqualTo(6), "right");
            Assert.That(At(0.05f, 180f).Number, Is.EqualTo(3), "bottom");
            Assert.That(At(0.05f, 270f).Number, Is.EqualTo(11), "left");
        }

        [Test]
        public void SegmentBoundaries_AreNineDegreesEachSideOfTwenty()
        {
            Assert.That(At(0.05f, 8.5f).Number, Is.EqualTo(20));
            Assert.That(At(0.05f, 9.5f).Number, Is.EqualTo(1));
            Assert.That(At(0.05f, -8.5f).Number, Is.EqualTo(20));
            Assert.That(At(0.05f, -9.5f).Number, Is.EqualTo(5));
        }

        [Test]
        public void EveryNumberIsOnTheBoardOnce()
        {
            for (int n = 1; n <= 20; n++)
            {
                Assert.That(Array.IndexOf(DartScoring.Order, n), Is.Not.Negative, n.ToString());
                Assert.That(At(0.05f, DartScoring.AngleOf(n)).Number, Is.EqualTo(n), "the centre of segment " + n);
            }
        }

        [Test]
        public void Labels()
        {
            Assert.That(DartScoring.Label(new DartHit(20, 3)), Is.EqualTo("T20"));
            Assert.That(DartScoring.Label(new DartHit(7, 2)), Is.EqualTo("D7"));
            Assert.That(DartScoring.Label(new DartHit(14, 1)), Is.EqualTo("14"));
            Assert.That(DartScoring.Label(new DartHit(25, 2)), Is.EqualTo("BULL"));
            Assert.That(DartScoring.Label(new DartHit(25, 1)), Is.EqualTo("25"));
            Assert.That(DartScoring.Label(DartHit.Miss), Is.EqualTo("MISS"));
        }

        [Test]
        public void Labels_AreNotRebuilt()
        {
            // אותה מחרוזת בכל קריאה: אין הקצאה באמצע סבב
            Assert.That(DartScoring.Label(new DartHit(20, 3)), Is.SameAs(DartScoring.Label(new DartHit(20, 3))));
        }
    }

    public class AroundTheClockTests
    {
        [Test]
        public void OneToTwentyThenBull()
        {
            var atc = new AroundTheClock();
            for (int n = 1; n <= 20; n++)
            {
                Assert.That(atc.Target, Is.EqualTo(n));
                Assert.That(atc.Register(new DartHit(n, 1 + n % 3)), Is.True, "any ring of the target counts");
            }
            Assert.That(atc.Target, Is.EqualTo(AroundTheClock.Bull));
            Assert.That(atc.Register(new DartHit(25, 1)), Is.True, "outer bull counts");
            Assert.That(atc.IsFinished, Is.True);
            Assert.That(atc.Completed, Is.EqualTo(21));
            Assert.That(atc.Target, Is.EqualTo(0));
        }

        [Test]
        public void WrongNumbersAndMisses_DoNotAdvance()
        {
            var atc = new AroundTheClock();
            Assert.That(atc.Register(new DartHit(20, 3)), Is.False);
            Assert.That(atc.Register(DartHit.Miss), Is.False);
            Assert.That(atc.Register(new DartHit(25, 2)), Is.False);
            Assert.That(atc.Completed, Is.EqualTo(0));
            Assert.That(atc.Register(new DartHit(1, 1)), Is.True);
        }

        [Test]
        public void NothingCountsAfterFinishing()
        {
            var atc = new AroundTheClock();
            for (int n = 1; n <= 20; n++) atc.Register(new DartHit(n, 1));
            atc.Register(new DartHit(25, 2));
            Assert.That(atc.Register(new DartHit(25, 2)), Is.False);
            Assert.That(atc.Completed, Is.EqualTo(21));
        }
    }

    public class RoundClockFinishTests
    {
        [Test]
        public void Finish_EndsAPlayingRound()
        {
            var clock = new RoundClock(0f, 60f);
            clock.Start();
            Assert.That(clock.Finish(), Is.True);
            Assert.That(clock.Phase, Is.EqualTo(RoundPhase.Results));
        }

        [Test]
        public void Finish_DoesNothingOutsidePlay()
        {
            var clock = new RoundClock(3f, 60f);
            Assert.That(clock.Finish(), Is.False, "Intro");
            clock.Start();
            Assert.That(clock.Finish(), Is.False, "Countdown");
            Assert.That(clock.Phase, Is.EqualTo(RoundPhase.Countdown));
        }
    }
}
