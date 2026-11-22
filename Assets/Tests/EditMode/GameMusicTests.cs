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

        // המוזיקה של המשחק: האובייקט "Music" (בכדורסל יש לידו גם את הקהל, GameMusic נוסף)
        private static GameMusic MusicOf(Scene scene)
        {
            var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<GameMusic>(true)).ToArray();
            return all.Length == 1 ? all[0] : all.SingleOrDefault(m => m.name == "Music");
        }

        [Test]
        public void Basketball_MusicAndCrowd_BothStartWithPlay()
        {
            const string path = "Assets/Scenes/Game_Basketball.unity";
            Scene scene = SceneManager.GetSceneByPath(path);
            bool openedHere = !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var layers = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<GameMusic>(true)).ToArray();
                Assert.That(layers.Select(l => l.name), Is.EquivalentTo(new[] { "Music", "Crowd Ambience" }));
                var presence = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<StationPresence>(true)).Single();
                var pso = new SerializedObject(presence);
                var heard = new System.Collections.Generic.List<Object> { pso.FindProperty("music").objectReferenceValue };
                var more = pso.FindProperty("moreMusic");
                for (int i = 0; i < more.arraySize; i++) heard.Add(more.GetArrayElementAtIndex(i).objectReferenceValue);
                foreach (var layer in layers)
                {
                    Assert.That(heard, Does.Contain(layer), layer.name + " never becomes audible");
                    Assert.That(new SerializedObject(layer).FindProperty("waitForPlayer").boolValue, Is.True, layer.name);
                }
                float music = new SerializedObject(layers.First(l => l.name == "Music")).FindProperty("volume").floatValue;
                float crowd = new SerializedObject(layers.First(l => l.name == "Crowd Ambience")).FindProperty("volume").floatValue;
                Assert.That(crowd, Is.LessThan(music), "the crowd stays behind the music");
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void Arcade_PlaysTheFourSynthTracks()
        {
            var hub = SceneManager.GetSceneByPath("Assets/Scenes/Hub.unity");
            bool openedHere = !hub.isLoaded;
            if (openedHere) hub = EditorSceneManager.OpenScene("Assets/Scenes/Hub.unity", OpenSceneMode.Additive);
            try
            {
                var music = hub.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<GameMusic>(true)).Single(m => m.name == "Arcade Music");
                var clips = new SerializedObject(music).FindProperty("clips");
                var names = Enumerable.Range(0, clips.arraySize).Select(i => clips.GetArrayElementAtIndex(i).objectReferenceValue?.name);
                Assert.That(names, Is.EqualTo(new[] { "arcade_1", "arcade_2", "arcade_3", "arcade_4" }));
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(hub, true);
            }
        }

        private static T Single<T>(Scene scene) where T : Component
        {
            var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
            Assert.That(all.Length, Is.LessThanOrEqualTo(1), typeof(T).Name);
            return all.FirstOrDefault();
        }

        // גיא (2026-11-22): סינתים מודרניים עם טעם רטרו בכל המשחקים חוץ מההוקי; לכדורסל יש עכשיו מוזיקה מעל הקהל
        [TestCase("Assets/Scenes/Game_Basketball.unity", "basketball_1", true)]
        [TestCase("Assets/Scenes/Game_Whack.unity", "whack_1,whack_2", true)]
        [TestCase("Assets/Scenes/Game_BuzzWire.unity", "buzzwire_1,buzzwire_2,buzzwire_3", true)]
        [TestCase("Assets/Scenes/Game_Darts.unity", "darts_1,darts_2", false)]
        [TestCase("Assets/Scenes/Game_AirHockey.unity", "airhockey_1,airhockey_2", false)]
        public void EveryGame_HasMusic_AndTheTimedOnesACountdown(string path, string trackList, bool countdown)
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            bool openedHere = !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var music = MusicOf(scene);
                Assert.That(music, Is.Not.Null);
                var so = new SerializedObject(music);
                // כל הקטעים שגיא השאיר, אחד אחרי השני
                string[] tracks = trackList.Split(',');
                var clips = so.FindProperty("clips");
                Assert.That(clips.arraySize, Is.EqualTo(tracks.Length));
                for (int i = 0; i < tracks.Length; i++)
                {
                    var clip = (AudioClip)clips.GetArrayElementAtIndex(i).objectReferenceValue;
                    Assert.That(clip, Is.Not.Null, "track " + i);
                    Assert.That(clip.name, Is.EqualTo(tracks[i]));
                    Assert.That(clip.length, Is.GreaterThanOrEqualTo(30f), clip.name + ": a full piece, not a short loop");
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
