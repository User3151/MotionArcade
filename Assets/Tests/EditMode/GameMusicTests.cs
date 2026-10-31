using System.Linq;
using MotionArcade.Core;
using MotionArcade.Feedback;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MotionArcade.Tests
{
    public class CountdownTests
    {
        [Test]
        public void Ticks_EverySecondOfTheLastTen()
        {
            Assert.That(Countdown.TickSecond(10.5f), Is.EqualTo(0));
            Assert.That(Countdown.TickSecond(10f), Is.EqualTo(10));
            Assert.That(Countdown.TickSecond(9.3f), Is.EqualTo(10));
            Assert.That(Countdown.TickSecond(0.2f), Is.EqualTo(1));
            Assert.That(Countdown.TickSecond(0f), Is.EqualTo(0), "no tick at the buzzer");
        }

        [Test]
        public void GetsMoreUrgent_TowardsZero()
        {
            Assert.That(Countdown.Urgency(10), Is.EqualTo(0f));
            Assert.That(Countdown.Urgency(1), Is.EqualTo(1f));
            Assert.That(Countdown.Urgency(5), Is.GreaterThan(Countdown.Urgency(6)));
        }
    }

    public class GameMusicSceneTests
    {
        private static T Single<T>(Scene scene) where T : Component
        {
            var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
            Assert.That(all.Length, Is.LessThanOrEqualTo(1), typeof(T).Name);
            return all.FirstOrDefault();
        }

        [TestCase("Assets/Scenes/Game_Basketball.unity", true)]
        [TestCase("Assets/Scenes/Game_Whack.unity", true)]
        [TestCase("Assets/Scenes/Game_BuzzWire.unity", true)]
        [TestCase("Assets/Scenes/Game_Darts.unity", false)]
        public void EveryGame_HasMusic_AndTheTimedOnesACountdown(string path, bool countdown)
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            bool openedHere = !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var music = Single<GameMusic>(scene);
                Assert.That(music, Is.Not.Null);
                var so = new SerializedObject(music);
                Assert.That(so.FindProperty("clip").objectReferenceValue, Is.Not.Null);
                Assert.That(((Object)so.FindProperty("output").objectReferenceValue).name, Is.EqualTo("Music"), "the MUSIC slider");

                var ticks = Single<CountdownTicks>(scene);
                Assert.That(ticks != null, Is.EqualTo(countdown));
                if (!countdown) return;
                var tso = new SerializedObject(ticks);
                var station = tso.FindProperty("station").objectReferenceValue as GameStation;
                Assert.That(station, Is.Not.Null);
                Assert.That(station.gameObject.scene, Is.EqualTo(scene));
                Assert.That(tso.FindProperty("tick").objectReferenceValue, Is.Not.Null);
                Assert.That(((Object)tso.FindProperty("output").objectReferenceValue).name, Is.EqualTo("SFX"), "the EFFECTS slider");
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
