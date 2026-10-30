using MotionArcade.Interaction;
using NUnit.Framework;
using UnityEngine;

namespace MotionArcade.Tests
{
    // רגע הכיוון: העצירה שלפני הזריקה (נמדד ונרשם עם כל זריקה)
    public class HandPoseHistoryTests
    {
        private const float Dt = 1f / 90f;
        private static readonly Quaternion Aim = Quaternion.Euler(-60f, 5f, 0f);   // הבקר מצביע 60° למעלה

        // הרמה מהמגש (0.4 ש'), עצירת כיוון (stillSeconds), תנופה לאחור וזריקה (0.35 ש'). מחזיר את זמן השחרור.
        private static float Throw(HandPoseHistory h, float stillSeconds, float jitter = 0.001f)
        {
            float t = 0f;
            var aimAt = new Vector3(0.2f, 1.6f, 0.2f);
            for (float s = 0f; s < 0.4f; s += Dt, t += Dt)
                h.Add(t, Vector3.Lerp(new Vector3(0.3f, 1.0f, 0.4f), aimAt, s / 0.4f), Quaternion.identity);
            for (float s = 0f; s < stillSeconds; s += Dt, t += Dt)
                h.Add(t, aimAt + new Vector3(Mathf.Sin(s * 50f), Mathf.Cos(s * 37f), 0f) * jitter, Aim);
            for (float s = 0f; s < 0.35f; s += Dt, t += Dt)
            {
                // אחורה 15 ס"מ ואז קדימה מהר (נקודת המפנה כמעט בלי מהירות, אבל רק לרגע)
                float z = s < 0.15f ? -0.15f * Mathf.Sin(s / 0.15f * Mathf.PI * 0.5f) : -0.15f + (s - 0.15f) * 4f;
                h.Add(t, aimAt + new Vector3(0f, 0f, z), Quaternion.Euler(-60f + s * 100f, 5f, 0f));
            }
            return t;
        }

        [Test]
        public void FindsTheAimPause_AndItsPose()
        {
            var h = new HandPoseHistory(256);
            float release = Throw(h, 0.5f);
            Assert.That(h.TryFindPause(release, 1.5f, 0.15f, 0.25f, out Vector3 p, out Quaternion r, out float before), Is.True);
            Assert.That(Quaternion.Angle(r, Aim), Is.LessThan(0.5f), "the pose while aiming, not during the swing");
            Assert.That(Vector3.Distance(p, new Vector3(0.2f, 1.6f, 0.2f)), Is.LessThan(0.01f));
            Assert.That(before, Is.InRange(0.3f, 0.4f), "ends where the swing starts");
        }

        [Test]
        public void TheTurnAroundOfTheBackswing_IsNotAPause()
        {
            // זריקה ברצף אחד, בלי לעצור לכוון: אין רגע כיוון (ולא נלמד ממנה)
            var h = new HandPoseHistory(256);
            float release = Throw(h, 0f);
            Assert.That(h.TryFindPause(release, 1.5f, 0.15f, 0.25f, out _, out _, out _), Is.False);
        }

        [Test]
        public void TrackingJitter_StillCountsAsStill()
        {
            var h = new HandPoseHistory(256);
            float release = Throw(h, 0.4f, jitter: 0.002f);   // 2 מ"מ רעד
            Assert.That(h.TryFindPause(release, 1.5f, 0.15f, 0.25f, out _, out _, out _), Is.True);
        }

        [Test]
        public void APauseLongBeforeTheThrow_DoesNotCount()
        {
            var h = new HandPoseHistory(256);
            float release = Throw(h, 0.5f);
            Assert.That(h.TryFindPause(release + 2f, 1.5f, 0.15f, 0.25f, out _, out _, out _), Is.False);
        }

        [Test]
        public void HighFrameRate_StillCoversThePause()
        {
            // 270 פריימים לשנייה (Editor): בלי מרווח מינימלי 256 דגימות היו מכסות רק את התנופה
            var h = new HandPoseHistory(256, 1f / 120f);
            float t = 0f;
            var at = new Vector3(0.2f, 1.6f, 0.2f);
            for (; t < 0.5f; t += 1f / 270f) h.Add(t, at, Aim);
            for (float s = 0f; s < 0.3f; s += 1f / 270f, t += 1f / 270f) h.Add(t, at + new Vector3(0f, 0f, s * s * 20f), Quaternion.Euler(-60f + s * 100f, 5f, 0f));
            Assert.That(h.TryFindPause(t, 1.5f, 0.15f, 0.25f, out _, out Quaternion r, out _), Is.True);
            // ה-25 ms הראשונים של תנועה מאיצה עדיין איטיים מהסף; הבקר הספיק להסתובב בהם רק כ-2°
            Assert.That(Quaternion.Angle(r, Aim), Is.LessThan(3f));
        }

        [Test]
        public void WrapsAround_KeepingTheNewest()
        {
            var h = new HandPoseHistory(64);   // קטן מהזריקה: רק הסוף נשאר
            float release = Throw(h, 0.5f);
            Assert.That(h.Count, Is.EqualTo(64));
            Assert.That(h.TryFindPause(release, 1.5f, 0.15f, 0.25f, out _, out Quaternion r, out _), Is.True);
            Assert.That(Quaternion.Angle(r, Aim), Is.LessThan(0.5f));
        }
    }
}
