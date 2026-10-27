using System;
using System.Collections.Generic;
using MotionArcade.Core;
using NUnit.Framework;

namespace MotionArcade.Tests
{
    public class RoundClockTests
    {
        [Test]
        public void StartsInIntro_WithNoTime()
        {
            var clock = new RoundClock(3f, 60f);
            Assert.That(clock.Phase, Is.EqualTo(RoundPhase.Intro));
            Assert.That(clock.TimeLeft, Is.EqualTo(0f));
        }

        [Test]
        public void FullCycle_IntroCountdownPlayingResults()
        {
            var clock = new RoundClock(3f, 10f);
            var seen = new List<RoundPhase>();
            clock.PhaseChanged += (_, next) => seen.Add(next);

            Assert.That(clock.Start(), Is.True);
            Assert.That(clock.TimeLeft, Is.EqualTo(3f));
            clock.Tick(2.9f);
            Assert.That(clock.Phase, Is.EqualTo(RoundPhase.Countdown));
            clock.Tick(0.2f);   // הספירה נגמרה, 0.1 שנייה כבר נגרעה מהסבב
            Assert.That(clock.Phase, Is.EqualTo(RoundPhase.Playing));
            Assert.That(clock.TimeLeft, Is.EqualTo(9.9f).Within(1e-4f));
            clock.Tick(9.8f);
            Assert.That(clock.Phase, Is.EqualTo(RoundPhase.Playing));
            clock.Tick(0.2f);
            Assert.That(clock.Phase, Is.EqualTo(RoundPhase.Results));

            Assert.That(seen, Is.EqualTo(new[] { RoundPhase.Countdown, RoundPhase.Playing, RoundPhase.Results }));
        }

        [Test]
        public void LongFrame_DoesNotSkipPlayingEvent()
        {
            // פריים של 100 שניות (עצירה בדיבאגר): עדיין עוברים דרך Playing, כדי שהמשחק יתחיל ויסתיים כמו שצריך
            var clock = new RoundClock(3f, 10f);
            var seen = new List<RoundPhase>();
            clock.PhaseChanged += (_, next) => seen.Add(next);
            clock.Start();
            clock.Tick(100f);
            Assert.That(seen, Is.EqualTo(new[] { RoundPhase.Countdown, RoundPhase.Playing, RoundPhase.Results }));
        }

        [Test]
        public void ZeroCountdown_GoesStraightToPlaying()
        {
            var clock = new RoundClock(0f, 5f);
            clock.Start();
            Assert.That(clock.Phase, Is.EqualTo(RoundPhase.Playing));
            Assert.That(clock.TimeLeft, Is.EqualTo(5f));
        }

        [Test]
        public void Start_IgnoredWhileRunning_AllowedFromResults()
        {
            var clock = new RoundClock(1f, 1f);
            clock.Start();
            Assert.That(clock.Start(), Is.False, "a second START during the countdown does nothing");
            clock.Tick(1f);
            Assert.That(clock.Start(), Is.False, "nor during play");
            clock.Tick(1f);
            Assert.That(clock.Phase, Is.EqualTo(RoundPhase.Results));
            Assert.That(clock.Start(), Is.True, "PLAY AGAIN");
            Assert.That(clock.Phase, Is.EqualTo(RoundPhase.Countdown));
        }

        [Test]
        public void Abort_ReturnsToIntro_WithoutResults()
        {
            var clock = new RoundClock(1f, 10f);
            var seen = new List<RoundPhase>();
            clock.PhaseChanged += (_, next) => seen.Add(next);
            clock.Start();
            clock.Tick(2f);
            clock.Abort();
            Assert.That(clock.Phase, Is.EqualTo(RoundPhase.Intro));
            Assert.That(seen, Has.No.Member(RoundPhase.Results));
        }

        [Test]
        public void TickIgnoresZeroAndNegative()
        {
            var clock = new RoundClock(0f, 5f);
            clock.Start();
            clock.Tick(0f);
            clock.Tick(-3f);
            Assert.That(clock.TimeLeft, Is.EqualTo(5f));
        }

        [Test]
        public void RoundMustHaveLength()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RoundClock(3f, 0f));
        }
    }

    public class SessionLogTests
    {
        private static RoundResult Round(string game, int score) => new RoundResult(game, score, 60f, DateTime.UtcNow);

        [Test]
        public void Best_PerGame()
        {
            var log = new SessionLog();
            Assert.That(log.TryGetBest("darts", out _), Is.False);
            log.Add(Round("darts", 40));
            log.Add(Round("basketball", 90));
            log.Add(Round("darts", 55));
            log.Add(Round("darts", 10));
            Assert.That(log.TryGetBest("darts", out int best), Is.True);
            Assert.That(best, Is.EqualTo(55));
            Assert.That(log.CountFor("darts"), Is.EqualTo(3));
            Assert.That(log.CountFor("whack"), Is.EqualTo(0));
        }

        [Test]
        public void Best_WorksWithZeroScores()
        {
            var log = new SessionLog();
            log.Add(Round("darts", 0));
            Assert.That(log.TryGetBest("darts", out int best), Is.True);
            Assert.That(best, Is.EqualTo(0));
        }

        [Test]
        public void RoundWithoutGameId_IsRejected()
        {
            Assert.Throws<ArgumentException>(() => new SessionLog().Add(Round("", 5)));
        }
    }

    public class VolumeTests
    {
        [Test]
        public void Decibels_FollowTheEar()
        {
            Assert.That(VolumeSettings.ToDecibels(1f), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(VolumeSettings.ToDecibels(0.5f), Is.EqualTo(-6.0206f).Within(1e-3f));
            Assert.That(VolumeSettings.ToDecibels(0.1f), Is.EqualTo(-20f).Within(1e-3f));
        }

        [Test]
        public void Decibels_SilenceIsTheMixerFloor()
        {
            Assert.That(VolumeSettings.ToDecibels(0f), Is.EqualTo(VolumeSettings.MinDecibels));
            Assert.That(VolumeSettings.ToDecibels(-1f), Is.EqualTo(VolumeSettings.MinDecibels));
            Assert.That(VolumeSettings.ToDecibels(0.00001f), Is.EqualTo(VolumeSettings.MinDecibels));
        }

        [Test]
        public void EveryChannelHasItsOwnKeyAndParameter()
        {
            var keys = new HashSet<string>();
            var parameters = new HashSet<string>();
            foreach (VolumeChannel channel in Enum.GetValues(typeof(VolumeChannel)))
            {
                Assert.That(keys.Add(VolumeSettings.Key(channel)), Is.True);
                Assert.That(parameters.Add(VolumeSettings.Parameter(channel)), Is.True);
            }
        }
    }
}
