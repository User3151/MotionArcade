using MotionArcade.Games.Basketball;
using NUnit.Framework;
using UnityEngine;

namespace MotionArcade.Tests
{
    // סל = מרכז הכדור חוצה את מישור הטבעת כלפי מטה, בתוך הטבעת (מרחב הטבעת: y למעלה, מישור ב-y=0)
    public class BasketDetectorTests
    {
        private const float R = 0.229f;

        private static BasketDetector.Crossing Through(Vector3 a, Vector3 b) => BasketDetector.Through(a, b, R);

        [Test]
        public void FallingThroughTheMiddle_IsABasket()
        {
            Assert.That(Through(new Vector3(0f, 0.03f, 0.02f), new Vector3(0f, -0.04f, 0.04f)), Is.EqualTo(BasketDetector.Crossing.Down));
        }

        [Test]
        public void CleanSwishWithSidewaysSpeed_IsABasket()
        {
            // מהמשחק של גיא: חצה 7 ס"מ מהמרכז בדרך לצד, ונסחף ל-19 ס"מ עד הגובה של החיישן התחתון הישן
            Assert.That(Through(new Vector3(0.05f, 0.02f, 0.04f), new Vector3(0.06f, -0.05f, 0.07f)), Is.EqualTo(BasketDetector.Crossing.Down));
        }

        [Test]
        public void FallingOutsideTheRing_IsNot()
        {
            // נופל מאחורי הטבעת, בין הטבעת ללוח
            Assert.That(Through(new Vector3(0f, 0.03f, 0.33f), new Vector3(0f, -0.04f, 0.34f)), Is.EqualTo(BasketDetector.Crossing.None));
            Assert.That(Through(new Vector3(-0.36f, 0.02f, 0f), new Vector3(-0.37f, -0.05f, 0f)), Is.EqualTo(BasketDetector.Crossing.None));
        }

        [Test]
        public void UsesTheCrossingPoint_NotTheSamples()
        {
            // שתי הדגימות מחוץ לטבעת (צעד ארוך באלכסון), אבל החצייה עצמה במרכז
            Assert.That(Through(new Vector3(0f, 0.05f, -0.25f), new Vector3(0f, -0.05f, 0.25f)), Is.EqualTo(BasketDetector.Crossing.Down));
            // הגבול: הנקודה שבה המישור נחצה, ליד שפת הטבעת (22.9 ס"מ)
            Assert.That(Through(new Vector3(0f, 0.01f, 0.20f), new Vector3(0f, -0.09f, 0.40f)), Is.EqualTo(BasketDetector.Crossing.Down), "crossed at 22 cm");
            Assert.That(Through(new Vector3(0f, 0.01f, 0.22f), new Vector3(0f, -0.09f, 0.42f)), Is.EqualTo(BasketDetector.Crossing.None), "crossed at 24 cm");
        }

        [Test]
        public void ComingUpFromBelow_IsUp()
        {
            Assert.That(Through(new Vector3(0f, -0.03f, 0f), new Vector3(0f, 0.04f, 0f)), Is.EqualTo(BasketDetector.Crossing.Up));
        }

        [Test]
        public void NotCrossing_IsNone()
        {
            Assert.That(Through(new Vector3(0f, 0.30f, 0f), new Vector3(0f, 0.20f, 0f)), Is.EqualTo(BasketDetector.Crossing.None), "still above");
            Assert.That(Through(new Vector3(0f, -0.10f, 0f), new Vector3(0f, -0.20f, 0f)), Is.EqualTo(BasketDetector.Crossing.None), "already below");
            Assert.That(Through(Vector3.zero, Vector3.zero), Is.EqualTo(BasketDetector.Crossing.None), "resting on the plane");
        }
    }

    public class BasketballScoringTests
    {
        [Test]
        public void TwoPoints_DoubleInTheLastTenSeconds()
        {
            Assert.That(BasketballScoring.PointsFor(45f), Is.EqualTo(2));
            Assert.That(BasketballScoring.PointsFor(10.01f), Is.EqualTo(2));
            Assert.That(BasketballScoring.PointsFor(10f), Is.EqualTo(4));
            Assert.That(BasketballScoring.PointsFor(0.5f), Is.EqualTo(4));
        }

        [Test]
        public void NoBonusOutsideTheRound()
        {
            // ב-Intro / Results אין זמן (0): לא "בונוס"
            Assert.That(BasketballScoring.IsBonus(0f), Is.False);
        }
    }
}
