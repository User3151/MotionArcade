using MotionArcade.Arcade;
using MotionArcade.Games.Basketball;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MotionArcade.Tests
{
    // מה שמחייה את הארקייד (גיא: "המכונות נראות מתות"): מסכי דמו, אורות, נורות רצות; והטבעת שזזה מהכדור.
    public class ArcadeLifeTests
    {
        [Test]
        public void AttractScreen_PlaysTheGameSeveralTimes_ThenItsTitle()
        {
            // 9 פריימי משחק x 3 לולאות, ואז 3 פריימי שם
            Assert.That(AttractScreen.FrameAt(0, 9, 3), Is.EqualTo(0));
            Assert.That(AttractScreen.FrameAt(8, 9, 3), Is.EqualTo(8));
            Assert.That(AttractScreen.FrameAt(9, 9, 3), Is.EqualTo(0), "the game loops");
            Assert.That(AttractScreen.FrameAt(26, 9, 3), Is.EqualTo(8));
            Assert.That(AttractScreen.FrameAt(27, 9, 3), Is.EqualTo(9), "then the title with INSERT COIN");
            Assert.That(AttractScreen.FrameAt(29, 9, 3), Is.EqualTo(11));
        }

        [Test]
        public void LightPulse_BlinksAndBreathes()
        {
            Assert.That(LightPulse.Evaluate(LightPulse.Mode.Blink, 0.1f, 1f, 0f), Is.EqualTo(1f));
            Assert.That(LightPulse.Evaluate(LightPulse.Mode.Blink, 0.6f, 1f, 0f), Is.EqualTo(0f));
            float lo = 1f, hi = 0f;
            for (float t = 0f; t < 2f; t += 0.05f) { float v = LightPulse.Evaluate(LightPulse.Mode.Pulse, t, 2f, 0f); lo = Mathf.Min(lo, v); hi = Mathf.Max(hi, v); }
            Assert.That(lo, Is.LessThan(0.05f)); Assert.That(hi, Is.GreaterThan(0.95f));
        }

        [Test]
        public void ChaseLights_MoveAlongTheFrame()
        {
            Assert.That(ChaseLights.GroupAt(0f, 0.2f, 3), Is.EqualTo(0));
            Assert.That(ChaseLights.GroupAt(0.25f, 0.2f, 3), Is.EqualTo(1));
            Assert.That(ChaseLights.GroupAt(0.45f, 0.2f, 3), Is.EqualTo(2));
            Assert.That(ChaseLights.GroupAt(0.65f, 0.2f, 3), Is.EqualTo(0));
        }

        [Test]
        public void TheRim_DipsMoreFromAHardHitAtTheFront_AndComesBack()
        {
            var pivot = new GameObject("Pivot").transform;
            var rim = new GameObject("Rim"); rim.transform.SetParent(pivot, false); rim.transform.localPosition = new Vector3(0f, 0f, -0.229f);
            var spring = rim.AddComponent<RimSpring>();
            var so = new SerializedObject(spring); so.FindProperty("pivot").objectReferenceValue = pivot; so.ApplyModifiedPropertiesWithoutUndo();
            try
            {
                float Peak(Vector3 impulse, Vector3 local)
                {
                    spring.Kick(impulse, pivot.TransformPoint(local));
                    float peak = 0f;
                    for (int i = 0; i < 300; i++) { spring.Step(1f / 90f); peak = Mathf.Max(peak, spring.Angle); }
                    return peak;
                }
                float front = Peak(new Vector3(0f, -2f, 0f), new Vector3(0f, 0f, -0.45f));
                Assert.That(front, Is.InRange(1.5f, 7f), "a ball dropping on the front of the rim: a visible dip of a few degrees");
                Assert.That(spring.Angle, Is.EqualTo(0f), "back at rest after ~3 s");
                Assert.That(pivot.localRotation, Is.EqualTo(Quaternion.identity));
                float back = Peak(new Vector3(0f, -2f, 0f), new Vector3(0f, 0f, -0.02f));
                Assert.That(back, Is.LessThan(front * 0.6f), "near the hinge it hardly moves");
                float soft = Peak(new Vector3(0f, -0.5f, 0f), new Vector3(0f, 0f, -0.45f));
                Assert.That(soft, Is.LessThan(front), "a soft touch dips less than a hard hit");
                // הטבעת יורדת (החזית למטה), לא עולה
                spring.Kick(new Vector3(0f, -2f, 0f), pivot.TransformPoint(new Vector3(0f, 0f, -0.45f)));
                for (int i = 0; i < 5; i++) spring.Step(1f / 90f);
                Assert.That(pivot.TransformPoint(new Vector3(0f, 0f, -0.45f)).y, Is.LessThan(0f));
            }
            finally { Object.DestroyImmediate(pivot.gameObject); }
        }
    }
}
