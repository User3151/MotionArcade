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
        [Test]
        public void Playlist_GoesRound()
        {
            Assert.That(GameMusic.Next(0, 3), Is.EqualTo(1));
            Assert.That(GameMusic.Next(2, 3), Is.EqualTo(0));
            Assert.That(GameMusic.Next(0, 1), Is.EqualTo(0));
        }

        private static T Single<T>(Scene scene) where T : Component
        {
            var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
            Assert.That(all.Length, Is.LessThanOrEqualTo(1), typeof(T).Name);
            return all.FirstOrDefault();
        }

        [TestCase("Assets/Scenes/Game_Basketball.unity", "basketball", 3, true)]
        [TestCase("Assets/Scenes/Game_Whack.unity", "whack", 1, true)]
        [TestCase("Assets/Scenes/Game_BuzzWire.unity", "buzzwire", 3, true)]
        [TestCase("Assets/Scenes/Game_Darts.unity", "darts", 3, false)]
        public void EveryGame_HasMusic_AndTheTimedOnesACountdown(string path, string game, int tracks, bool countdown)
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            bool openedHere = !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var music = Single<GameMusic>(scene);
                Assert.That(music, Is.Not.Null);
                var so = new SerializedObject(music);
                // גיא בחר: כל הקטעים של המשחק, אחד אחרי השני (בחפרפרות רק הראשון)
                var clips = so.FindProperty("clips");
                Assert.That(clips.arraySize, Is.EqualTo(tracks));
                for (int i = 0; i < tracks; i++)
                {
                    var clip = (AudioClip)clips.GetArrayElementAtIndex(i).objectReferenceValue;
                    Assert.That(clip, Is.Not.Null, "track " + i);
                    Assert.That(clip.name, Is.EqualTo(game + "_" + (i + 1)));
                    Assert.That(clip.length, Is.GreaterThan(60f), clip.name + ": the full piece, not the old 20 s loop");
                }
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
