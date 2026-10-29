using System;
using MotionArcade.Interaction;
using NUnit.Framework;

namespace MotionArcade.Tests
{
    public class ReleaseVelocityTests
    {
        private const float Dt = 1f / 90f;

        [Test]
        public void ConstantVelocity_IsExact()
        {
            var r = new ReleaseVelocity();
            for (int i = 0; i < 20; i++) r.Add(i * Dt, 3f * i * Dt, 1f, -2f * i * Dt);
            Assert.That(r.TryEstimate(0.06f, out float vx, out float vy, out float vz), Is.True);
            Assert.That(vx, Is.EqualTo(3f).Within(1e-3f));
            Assert.That(vy, Is.EqualTo(0f).Within(1e-3f));
            Assert.That(vz, Is.EqualTo(-2f).Within(1e-3f));
        }

        [Test]
        public void AcceleratingThrow_KeepsMostOfTheReleaseSpeed()
        {
            // יד שמאיצה ב-20 מ'/ש² במשך 0.3 ש': ברגע השחרור היא נעה ב-6 מ'/ש'
            var r = new ReleaseVelocity();
            const float a = 20f;
            int n = (int)(0.3f / Dt);
            for (int i = 0; i <= n; i++) { float t = i * Dt; r.Add(t, 0.5f * a * t * t, 0f, 0f); }
            float release = a * n * Dt;
            r.TryEstimate(0.06f, out float recent, out _, out _);
            r.TryEstimate(0.25f, out float longAverage, out _, out _);   // בערך מה ש-XRI עושה כברירת מחדל
            Assert.That(recent, Is.GreaterThan(0.88f * release), "the last 60 ms is close to the real release speed");
            Assert.That(longAverage, Is.LessThan(0.65f * release), "a quarter-second average loses a third or more");
        }

        [Test]
        public void TrackingNoise_IsFilteredByTheLine()
        {
            // רעש של ±2 מ"מ בכל דגימה (רעד, מעקב): דגימה בודדת הייתה נותנת שגיאה של ±0.36 מ'/ש'
            var r = new ReleaseVelocity();
            var rng = new Random(3);
            for (int i = 0; i < 30; i++) r.Add(i * Dt, 4f * i * Dt + (float)(rng.NextDouble() - 0.5) * 0.004f, 0f, 0f);
            r.TryEstimate(0.06f, out float vx, out _, out _);
            Assert.That(vx, Is.EqualTo(4f).Within(0.4f));
        }

        [Test]
        public void NotEnoughSamples_ReturnsFalse()
        {
            var r = new ReleaseVelocity();
            Assert.That(r.TryEstimate(0.06f, out _, out _, out _), Is.False);
            r.Add(0f, 0f, 0f, 0f);
            Assert.That(r.TryEstimate(0.06f, out _, out _, out _), Is.False);
            r.Add(Dt, 0.01f, 0f, 0f);
            Assert.That(r.TryEstimate(0.06f, out float vx, out _, out _), Is.True);
            Assert.That(vx, Is.EqualTo(0.9f).Within(1e-3f));
        }

        [Test]
        public void OldSamples_AreForgottenAfterClear()
        {
            var r = new ReleaseVelocity();
            for (int i = 0; i < 10; i++) r.Add(i * Dt, 10f * i * Dt, 0f, 0f);
            r.Clear();
            for (int i = 0; i < 10; i++) r.Add(1f + i * Dt, 0f, 0f, 2f * i * Dt);
            r.TryEstimate(0.06f, out float vx, out _, out float vz);
            Assert.That(vx, Is.EqualTo(0f).Within(1e-3f));
            Assert.That(vz, Is.EqualTo(2f).Within(1e-3f));
        }

        [Test]
        public void RingBuffer_WrapsAround()
        {
            var r = new ReleaseVelocity(8);
            for (int i = 0; i < 100; i++) r.Add(i * Dt, 5f * i * Dt, 0f, 0f);
            r.TryEstimate(0.06f, out float vx, out _, out _);
            Assert.That(vx, Is.EqualTo(5f).Within(1e-3f));
        }
    }
}
