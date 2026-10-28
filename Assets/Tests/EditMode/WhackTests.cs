using System;
using MotionArcade.Games.Whack;
using NUnit.Framework;

namespace MotionArcade.Tests
{
    public class MoleMotionTests
    {
        [Test]
        public void RisesStaysAndRetreats_ThenReportsEscape()
        {
            var mole = new MoleMotion();
            int escaped = 0;
            mole.Escaped += () => escaped++;
            mole.Spawn(1f, false);
            Assert.That(mole.Current, Is.EqualTo(MoleMotion.State.Rising));
            mole.Tick(MoleMotion.RiseSeconds);
            Assert.That(mole.Current, Is.EqualTo(MoleMotion.State.Up));
            Assert.That(mole.Height, Is.EqualTo(1f));
            mole.Tick(1f);
            Assert.That(mole.Current, Is.EqualTo(MoleMotion.State.Retreating));
            mole.Tick(MoleMotion.RetreatSeconds);
            Assert.That(mole.Current, Is.EqualTo(MoleMotion.State.Hidden));
            Assert.That(escaped, Is.EqualTo(1));
        }

        [Test]
        public void Hit_GivesReactionTime_AndNoEscape()
        {
            var mole = new MoleMotion();
            int escaped = 0;
            mole.Escaped += () => escaped++;
            mole.Spawn(1f, false);
            mole.Tick(0.1f);
            mole.Tick(0.3f);
            Assert.That(mole.TryHit(out float reaction), Is.True);
            Assert.That(reaction, Is.EqualTo(0.4f).Within(1e-4f));
            Assert.That(mole.Current, Is.EqualTo(MoleMotion.State.Bonked));
            Assert.That(mole.TryHit(out _), Is.False, "a bonked mole cannot be hit twice");
            mole.Tick(MoleMotion.BonkSeconds);
            Assert.That(mole.Current, Is.EqualTo(MoleMotion.State.Hidden));
            Assert.That(escaped, Is.EqualTo(0));
        }

        [Test]
        public void CannotHitWhileHiddenOrRetreating()
        {
            var mole = new MoleMotion();
            Assert.That(mole.TryHit(out _), Is.False);
            mole.Spawn(0.2f, false);
            mole.Tick(MoleMotion.RiseSeconds);
            mole.Tick(0.2f);
            Assert.That(mole.Current, Is.EqualTo(MoleMotion.State.Retreating));
            Assert.That(mole.TryHit(out _), Is.False, "too late: it is already going back in");
        }

        [Test]
        public void RedMoleThatLeaves_IsNotAMiss()
        {
            var mole = new MoleMotion();
            int escaped = 0;
            mole.Escaped += () => escaped++;
            mole.Spawn(0.5f, true);
            for (int i = 0; i < 20; i++) mole.Tick(0.1f);
            Assert.That(mole.Current, Is.EqualTo(MoleMotion.State.Hidden));
            Assert.That(escaped, Is.EqualTo(0), "leaving a red mole alone is the right answer");
        }

        [Test]
        public void Hide_ResetsWithoutEscape()
        {
            var mole = new MoleMotion();
            int escaped = 0;
            mole.Escaped += () => escaped++;
            mole.Spawn(1f, false);
            mole.Tick(0.5f);
            mole.Hide();
            Assert.That(mole.IsBusy, Is.False);
            Assert.That(mole.Height, Is.EqualTo(0f));
            Assert.That(escaped, Is.EqualTo(0));
        }
    }

    public class WhackSpawnerTests
    {
        [Test]
        public void DifficultyRisesOverTheRound()
        {
            Assert.That(WhackSpawner.MaxUp(0f), Is.EqualTo(1));
            Assert.That(WhackSpawner.MaxUp(20f), Is.EqualTo(2));
            Assert.That(WhackSpawner.MaxUp(50f), Is.EqualTo(3));
            Assert.That(WhackSpawner.UpSeconds(0f, 60f), Is.EqualTo(1.6f).Within(1e-4f));
            Assert.That(WhackSpawner.UpSeconds(60f, 60f), Is.EqualTo(0.9f).Within(1e-4f));
            Assert.That(WhackSpawner.UpSeconds(30f, 60f), Is.InRange(0.9f, 1.6f));
            Assert.That(WhackSpawner.RedChanceAt(10f), Is.EqualTo(0f));
            Assert.That(WhackSpawner.RedChanceAt(20f), Is.GreaterThan(0f));
        }

        [Test]
        public void PickHole_NeverBusy_NeverTheSameTwice()
        {
            var rng = new Random(7);
            var busy = new bool[12];
            busy[3] = busy[8] = true;
            int last = 5;
            for (int i = 0; i < 500; i++)
            {
                int pick = WhackSpawner.PickHole(busy, last, rng);
                Assert.That(pick, Is.Not.EqualTo(last));
                Assert.That(busy[pick], Is.False);
                last = pick;
            }
        }

        [Test]
        public void PickHole_EveryFreeHoleComesUp()
        {
            var rng = new Random(1);
            var busy = new bool[12];
            var seen = new bool[12];
            int last = -1;
            for (int i = 0; i < 400; i++) { last = WhackSpawner.PickHole(busy, last, rng); seen[last] = true; }
            Assert.That(seen, Has.All.True);
        }

        [Test]
        public void PickHole_NoneFree()
        {
            var busy = new[] { true, true, true };
            Assert.That(WhackSpawner.PickHole(busy, 1, new Random(0)), Is.EqualTo(-1));
            busy[1] = false;
            Assert.That(WhackSpawner.PickHole(busy, 1, new Random(0)), Is.EqualTo(1), "the last hole if it is the only one free");
        }
    }
}
