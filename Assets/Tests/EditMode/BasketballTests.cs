using MotionArcade.Games.Basketball;
using NUnit.Framework;

namespace MotionArcade.Tests
{
    public class BasketDetectorTests
    {
        [Test]
        public void FallingThroughTopThenBottom_IsABasket()
        {
            var d = new BasketDetector(1f);
            d.EnterTop(1, 10.0f, -3f);
            Assert.That(d.EnterBottom(1, 10.2f, -3.2f), Is.True);
        }

        [Test]
        public void OnlyCountsOnce()
        {
            var d = new BasketDetector(1f);
            d.EnterTop(1, 10f, -3f);
            Assert.That(d.EnterBottom(1, 10.1f, -3f), Is.True);
            Assert.That(d.EnterBottom(1, 10.15f, -3f), Is.False, "bouncing inside the net must not score again");
        }

        [Test]
        public void ComingUpFromBelow_IsNotABasket()
        {
            var d = new BasketDetector(1f);
            Assert.That(d.EnterBottom(1, 10f, 4f), Is.False);
            d.EnterTop(1, 10.1f, 3.5f);
            Assert.That(d.EnterBottom(1, 10.2f, 3f), Is.False);
        }

        [Test]
        public void BottomWithoutTop_IsNotABasket()
        {
            // נפל מחוץ לטבעת ונכנס לחיישן התחתון מהצד
            Assert.That(new BasketDetector(1f).EnterBottom(1, 5f, -2f), Is.False);
        }

        [Test]
        public void RimOutThenLate_IsNotABasket()
        {
            // נגע בחיישן העליון, קפץ מהטבעת החוצה, ורק אחרי יותר משנייה הגיע לתחתון (למשל בדרך חזרה במסילה)
            var d = new BasketDetector(1f);
            d.EnterTop(1, 10f, -2f);
            Assert.That(d.EnterBottom(1, 11.5f, -2f), Is.False);
        }

        [Test]
        public void BallsAreTrackedSeparately()
        {
            var d = new BasketDetector(1f);
            d.EnterTop(1, 10f, -3f);
            Assert.That(d.EnterBottom(2, 10.1f, -3f), Is.False, "ball 2 never passed the top");
            Assert.That(d.EnterBottom(1, 10.2f, -3f), Is.True);
        }

        [Test]
        public void Forget_DropsArmedBall()
        {
            var d = new BasketDetector(1f);
            d.EnterTop(1, 10f, -3f);
            d.Forget(1);   // נתפס ביד בדרך
            Assert.That(d.EnterBottom(1, 10.1f, -3f), Is.False);
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
