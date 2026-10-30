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
        [Test]
        public void StartsWithTheBaseOnly()
        {
            Assert.That(TensionCurve.Layer(0, 60f, 60f), Is.EqualTo(1f));
            for (int i = 1; i < TensionCurve.Layers; i++)
                Assert.That(TensionCurve.Layer(i, 60f, 60f), Is.EqualTo(0f), "layer " + i);
            Assert.That(TensionCurve.Pitch(60f), Is.EqualTo(1f));
        }

        [Test]
        public void LayersComeIn_OneAfterTheOther()
        {
            // בכל רגע, יותר זמן עבר = לא פחות מתח בשום שכבה
            for (int i = 0; i < TensionCurve.Layers; i++)
            {
                float previous = 0f;
                for (float left = 60f; left >= 0f; left -= 0.5f)
                {
                    float level = TensionCurve.Layer(i, left, 60f);
                    Assert.That(level, Is.GreaterThanOrEqualTo(previous - 1e-5f), $"layer {i} at {left}s");
                    previous = level;
                }
            }
            // באמצע הסבב: בסיס ודחף, בלי כלי הקשת ובלי הסוף
            Assert.That(TensionCurve.Layer(1, 30f, 60f), Is.EqualTo(1f));
            Assert.That(TensionCurve.Layer(2, 30f, 60f), Is.EqualTo(0f));
            Assert.That(TensionCurve.Layer(3, 30f, 60f), Is.EqualTo(0f));
            // 15 שניות לסוף: הכל חוץ מהסוף
            Assert.That(TensionCurve.Layer(2, 15f, 60f), Is.EqualTo(1f));
            Assert.That(TensionCurve.Layer(3, 15f, 60f), Is.EqualTo(0f));
            Assert.That(TensionCurve.Layer(3, 9.9f, 60f), Is.EqualTo(1f));
        }

        [Test]
        public void ByFractionOfTheRound_SoBuzzWireBuildsUpToo()
        {
            // Buzz Wire: 120 שניות. אחרי 60 שניות (חצי) כמו כדורסל אחרי 30
            Assert.That(TensionCurve.Layer(1, 60f, 120f), Is.EqualTo(TensionCurve.Layer(1, 30f, 60f)));
            Assert.That(TensionCurve.Layer(2, 60f, 120f), Is.EqualTo(TensionCurve.Layer(2, 30f, 60f)));
        }

        [Test]
        public void SpeedsUp_InTheLast15Seconds()
        {
            Assert.That(TensionCurve.Pitch(15f), Is.EqualTo(1f));
            Assert.That(TensionCurve.Pitch(7.5f), Is.EqualTo(1f + TensionCurve.MaxSpeedUp * 0.5f).Within(1e-5f));
            Assert.That(TensionCurve.Pitch(0f), Is.EqualTo(1f + TensionCurve.MaxSpeedUp).Within(1e-5f));
        }

        [Test]
        public void Ticks_EverySecondOfTheLastTen()
        {
            Assert.That(TensionCurve.TickSecond(10.5f), Is.EqualTo(0));
            Assert.That(TensionCurve.TickSecond(10f), Is.EqualTo(10));
            Assert.That(TensionCurve.TickSecond(9.3f), Is.EqualTo(10));
            Assert.That(TensionCurve.TickSecond(0.2f), Is.EqualTo(1));
            Assert.That(TensionCurve.TickSecond(0f), Is.EqualTo(0), "no tick at the buzzer");
        }
    }

    public class TensionMusicSceneTests
    {
        [TestCase("Assets/Scenes/Game_Basketball.unity")]
        [TestCase("Assets/Scenes/Game_Whack.unity")]
        [TestCase("Assets/Scenes/Game_BuzzWire.unity")]
        public void TimedGames_HaveTensionMusic_OnTheMusicSlider(string path)
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
                var layers = so.FindProperty("layers");
                Assert.That(layers.arraySize, Is.EqualTo(TensionCurve.Layers));
                float length = -1f;
                for (int i = 0; i < layers.arraySize; i++)
                {
                    var clip = (AudioClip)layers.GetArrayElementAtIndex(i).objectReferenceValue;
                    Assert.That(clip, Is.Not.Null, "layer " + i);
                    // כל השכבות באותו אורך בדיוק, אחרת הן יוצאות מסנכרון אחרי כמה סיבובים
                    if (length < 0f) length = clip.length;
                    Assert.That(clip.length, Is.EqualTo(length).Within(0.001f), clip.name);
                }
                Assert.That(so.FindProperty("tick").objectReferenceValue, Is.Not.Null);
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
