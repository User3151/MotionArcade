using MotionArcade.Games.BuzzWire;
using NUnit.Framework;

namespace MotionArcade.Tests
{
    public class TorusContactTests
    {
        private const float R = 0.025f, Tube = 0.003f, Wire = 0.004f;

        [Test]
        public void WireThroughTheCentre_IsFarFromTheRing()
        {
            Assert.That(TorusContact.Distance(0f, 0f, 0f, R), Is.EqualTo(R).Within(1e-6f));
            Assert.That(TorusContact.Touches(0f, 0f, 0f, R, Tube, Wire), Is.False);
        }

        [Test]
        public void WireNearTheRingEdge_Touches()
        {
            // 2 מ"מ מהטבעת, בתוך המישור שלה
            Assert.That(TorusContact.Distance(R - 0.002f, 0f, 0f, R), Is.EqualTo(0.002f).Within(1e-6f));
            Assert.That(TorusContact.Touches(R - 0.005f, 0f, 0f, R, Tube, Wire), Is.True);
            Assert.That(TorusContact.Touches(0f, R - 0.008f, 0f, R, Tube, Wire), Is.False, "8 mm clear");
        }

        [Test]
        public void OutOfThePlane_UsesTheTubeDistance()
        {
            // מעל קו הטבעת, 5 מ"מ לאורך הנורמל
            Assert.That(TorusContact.Distance(R, 0f, 0.005f, R), Is.EqualTo(0.005f).Within(1e-6f));
            // 3-4-5: רדיאלית 3 מ"מ פנימה, 4 מ"מ לאורך הנורמל
            Assert.That(TorusContact.Distance(R - 0.003f, 0f, 0.004f, R), Is.EqualTo(0.005f).Within(1e-5f));
        }
    }

    public class WireProgressTests
    {
        private static void Slide(WireProgress p, float from, float to, float step = 0.01f, bool touching = false, float startTime = 0f)
        {
            float time = startTime;
            for (float t = from; t <= to + 1e-5f; t += step, time += 0.02f) p.Update(true, t, touching, time);
        }

        [Test]
        public void MustStartAtTheBeginning()
        {
            var p = new WireProgress();
            p.Update(true, 0.5f, false, 0f);
            Assert.That(p.OnWire, Is.False, "putting the ring on the middle of the wire does not count");
            p.Update(true, 0.02f, false, 0.1f);
            Assert.That(p.OnWire, Is.True);
        }

        [Test]
        public void SlidingAlong_PassesCheckpointsAndFinishes()
        {
            var p = new WireProgress();
            int checkpoints = 0;
            p.CheckpointReached += _ => checkpoints++;
            Slide(p, 0f, 1f);
            Assert.That(checkpoints, Is.EqualTo(3));
            Assert.That(p.Finished, Is.True);
            Assert.That(p.Score, Is.EqualTo(100));
        }

        [Test]
        public void Jumping_CountsAsLeavingTheWire()
        {
            var p = new WireProgress();
            Slide(p, 0f, 0.2f);
            p.Update(true, 0.6f, false, 5f);
            Assert.That(p.OnWire, Is.False);
            Assert.That(p.Best, Is.EqualTo(0.2f).Within(1e-4f), "the jump did not add progress");
        }

        [Test]
        public void AfterLeaving_ResumeOnlyAtTheLastCheckpoint()
        {
            var p = new WireProgress();
            Slide(p, 0f, 0.4f);
            Assert.That(p.Checkpoint, Is.EqualTo(0.25f));
            p.Update(false, 0f, false, 10f);
            Assert.That(p.OnWire, Is.False);
            p.Update(true, 0.4f, false, 10.1f);
            Assert.That(p.OnWire, Is.False, "not where you left off");
            p.Update(true, 0.26f, false, 10.2f);
            Assert.That(p.OnWire, Is.True, "back at the checkpoint");
            Assert.That(p.Best, Is.EqualTo(0.4f).Within(1e-4f), "best progress is kept for the score");
        }

        [Test]
        public void Touches_CountOncePerContact()
        {
            var p = new WireProgress();
            Slide(p, 0f, 0.1f);
            Slide(p, 0.1f, 0.2f, touching: true, startTime: 1f);   // נגיעה ארוכה אחת
            Assert.That(p.Touches, Is.EqualTo(1));
            Slide(p, 0.2f, 0.3f, startTime: 2f);
            Slide(p, 0.3f, 0.32f, touching: true, startTime: 3f);
            Assert.That(p.Touches, Is.EqualTo(2));
        }

        [Test]
        public void TrembleOnTheWire_IsNotManyTouches()
        {
            // רעד שנוגע ומשתחרר כל 20 ms נספר כנגיעה אחת בכל רבע שנייה, לא 50 בשנייה
            var p = new WireProgress();
            Slide(p, 0f, 0.05f);
            float time = 1f;
            for (int i = 0; i < 50; i++, time += 0.02f) p.Update(true, 0.05f, i % 2 == 0, time);
            Assert.That(p.Touches, Is.LessThanOrEqualTo(4));
            Assert.That(p.Touches, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void Score_IsProgressMinusThreePerTouch_NeverNegative()
        {
            var p = new WireProgress();
            Slide(p, 0f, 0.5f);
            Assert.That(p.Score, Is.EqualTo(50));
            for (int i = 0; i < 30; i++) { p.Update(true, 0.5f, true, 10f + i); p.Update(true, 0.5f, false, 10.5f + i); }
            Assert.That(p.Score, Is.EqualTo(0));
        }
    }
}
