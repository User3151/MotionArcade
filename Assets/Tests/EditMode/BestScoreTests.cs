using MotionArcade.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MotionArcade.Tests
{
    // גיא: בכל משחק השיא נשמר, כתוב בסוף כל סבב ("Best score"), ומתעדכן כששוברים אותו
    public class BestScoreTests
    {
        private class TestStation : GameStation
        {
            public void Init() => Awake();
            public void Points(int p) => AddScore(p);
            public bool Finish() => FinishRound();
            protected override void OnPlayingStarted() { }
            protected override void OnPlayingEnded() { }
        }

        private const string Id = "test.bestscore";
        private GameObject go;
        private TestStation station;

        [SetUp]
        public void Make()
        {
            PlayerPrefs.DeleteKey("best." + Id); PlayerPrefs.DeleteKey("best." + Id + ".text");
            go = new GameObject("Test Station");
            station = go.AddComponent<TestStation>();
            var so = new SerializedObject(station);
            so.FindProperty("gameId").stringValue = Id;
            so.FindProperty("countdownSeconds").floatValue = 0f;   // START נכנס ישר למשחק
            so.ApplyModifiedPropertiesWithoutUndo();
            station.Init();
        }

        [TearDown]
        public void Clean()
        {
            Object.DestroyImmediate(go);
            PlayerPrefs.DeleteKey("best." + Id); PlayerPrefs.DeleteKey("best." + Id + ".text");
        }

        private void Round(int points)
        {
            station.StartRound();
            Assert.That(station.Phase, Is.EqualTo(RoundPhase.Playing));
            station.Points(points);
            station.Finish();
            Assert.That(station.Phase, Is.EqualTo(RoundPhase.Results));
        }

        [Test]
        public void TheFirstRound_SetsTheBest_ALowerOneKeepsIt_AHigherOneBreaksIt()
        {
            Assert.That(station.BestText, Is.Null, "nothing saved yet");
            Round(12);
            Assert.That(station.NewBest, Is.True);
            Assert.That(station.BestLine, Does.Contain("NEW BEST SCORE").And.Contain("12 points"));
            Round(7);
            Assert.That(station.NewBest, Is.False);
            Assert.That(station.BestLine, Is.EqualTo("Best score: 12 points"), "written after every round");
            Round(15);
            Assert.That(station.NewBest, Is.True);
            Assert.That(station.BestText, Is.EqualTo("15 points"));
            Assert.That(PlayerPrefs.GetFloat("best." + Id), Is.EqualTo(15f), "kept between runs");
        }

        [Test]
        public void AnEmptyRound_IsNotARecord()
        {
            Round(0);
            Assert.That(station.NewBest, Is.False);
            Assert.That(station.BestLine, Is.Null);
        }
    }
}
