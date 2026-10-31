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
    // המוזיקה שנהיית מותחת ככל שהזמן אוזל
    public class TensionCurveTests
    {
        private static float Energy(float timeLeft, float round)
        {
            float sum = 0f;
            for (int i = 0; i < 3; i++) { float v = TensionCurve.Stage(i, 3, timeLeft, round); sum += v * v; }
            return sum;
        }

        [Test]
        public void StartsAtTheFirstStage()
        {
            Assert.That(TensionCurve.Stage(0, 3, 60f, 60f), Is.EqualTo(1f));
            Assert.That(TensionCurve.Stage(1, 3, 60f, 60f), Is.EqualTo(0f));
            Assert.That(TensionCurve.Stage(2, 3, 60f, 60f), Is.EqualTo(0f));
            Assert.That(TensionCurve.Pitch(60f), Is.EqualTo(1f));
        }

        [Test]
        public void Builds_ThenTheFinalStage_InTheLast15Seconds()
        {
            Assert.That(TensionCurve.Stage(1, 3, 30f, 60f), Is.EqualTo(1f), "half way: the build");
            Assert.That(TensionCurve.Stage(0, 3, 30f, 60f), Is.EqualTo(0f));
            Assert.That(TensionCurve.Stage(2, 3, 18f, 60f), Is.EqualTo(0f));
            Assert.That(TensionCurve.Stage(2, 3, 15f, 60f), Is.EqualTo(1f), "15 seconds left: the final stage");
            Assert.That(TensionCurve.Stage(1, 3, 15f, 60f), Is.EqualTo(0f));
            Assert.That(TensionCurve.Stage(2, 3, 2f, 60f), Is.EqualTo(1f));
        }

        [Test]
        public void CrossfadesKeepTheLoudness()
        {
            // לופים לא מסונכרנים: סכום האנרגיות (ריבועי העוצמות) נשאר 1 גם באמצע מעבר
            for (float left = 60f; left >= 0f; left -= 0.25f)
                Assert.That(Energy(left, 60f), Is.EqualTo(1f).Within(1e-4f), "at " + left + "s");
        }

        [Test]
        public void ByFractionOfTheRound_SoBuzzWireBuildsUpToo()
        {
            // Buzz Wire: 120 שניות. אחרי 40 שניות (שליש) כמו כדורסל אחרי 20
            Assert.That(TensionCurve.Stage(1, 3, 80f, 120f), Is.EqualTo(TensionCurve.Stage(1, 3, 40f, 60f)).Within(1e-5f));
            Assert.That(TensionCurve.Stage(2, 3, 14f, 120f), Is.EqualTo(1f), "the final stage is by seconds");
        }

        [Test]
        public void OneStage_PlaysThroughout()
        {
            Assert.That(TensionCurve.Stage(0, 1, 60f, 60f), Is.EqualTo(1f));
            Assert.That(TensionCurve.Stage(0, 1, 2f, 60f), Is.EqualTo(1f));
        }

        [Test]
        public void SpeedsUp_InTheLast15Seconds()
        {
            Assert.That(TensionCurve.Pitch(15f), Is.EqualTo(1f));
            Assert.That(TensionCurve.Pitch(0f), Is.EqualTo(1f + TensionCurve.MaxSpeedUp).Within(1e-5f));
        }

        [Test]
        public void Ticks_EverySecondOfTheLastTen()
        {
            Assert.That(TensionCurve.TickSecond(10.5f), Is.EqualTo(0));
            Assert.That(TensionCurve.TickSecond(10f), Is.EqualTo(10));
            Assert.That(TensionCurve.TickSecond(0.2f), Is.EqualTo(1));
            Assert.That(TensionCurve.TickSecond(0f), Is.EqualTo(0), "no tick at the buzzer");
        }
    }

    public class TensionMusicSceneTests
    {
        [TestCase("Assets/Scenes/Game_Basketball.unity", "basketball", 3, true)]
        [TestCase("Assets/Scenes/Game_Whack.unity", "whack", 3, true)]
        [TestCase("Assets/Scenes/Game_BuzzWire.unity", "buzzwire", 3, true)]
        [TestCase("Assets/Scenes/Game_Darts.unity", "darts", 1, false)]
        public void EveryGame_HasItsOwnMusic_OnTheMusicSlider(string path, string game, int stageCount, bool tense)
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            bool openedHere = !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var music = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TensionMusic>(true)).ToArray();
                Assert.That(music.Length, Is.EqualTo(1));
                var so = new SerializedObject(music[0]);
                var station = so.FindProperty("station").objectReferenceValue as GameStation;
                Assert.That(station, Is.Not.Null);
                Assert.That(station.gameObject.scene, Is.EqualTo(scene));
                var stages = so.FindProperty("stages");
                Assert.That(stages.arraySize, Is.EqualTo(stageCount));
                for (int i = 0; i < stages.arraySize; i++)
                {
                    var clip = (AudioClip)stages.GetArrayElementAtIndex(i).objectReferenceValue;
                    Assert.That(clip, Is.Not.Null, "stage " + i);
                    Assert.That(clip.name, Is.EqualTo(game + "_" + (i + 1)), "this game's own music, in order");
                }
                Assert.That(so.FindProperty("clockTicks").boolValue, Is.EqualTo(tense));
                Assert.That(so.FindProperty("speedUp").boolValue, Is.EqualTo(tense));
                if (tense) Assert.That(so.FindProperty("tick").objectReferenceValue, Is.Not.Null);
                var output = (UnityEngine.Audio.AudioMixerGroup)so.FindProperty("output").objectReferenceValue;
                Assert.That(output, Is.Not.Null);
                Assert.That(output.name, Is.EqualTo("Music"), "the MUSIC slider controls it");
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
