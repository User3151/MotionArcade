using System.Linq;
using MotionArcade.Core;
using MotionArcade.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace MotionArcade.Tests
{
    // החיבורים של האולם שקל לשבור בשקט: כפתור שמצביע על סצנה שלא ב-build, מיקסר בלי פרמטר,
    // מנהל שחסר, או אולם בלי התאורה האפויה (ואז הכל שחור, כי אין בו אור בזמן אמת).
    public class HubSceneTests
    {
        private const string HubPath = "Assets/Scenes/Hub.unity";
        private const string MixerPath = "Assets/Audio/MotionArcade.mixer";

        private Scene hub;
        private bool openedHere;

        [OneTimeSetUp]
        public void OpenHub()
        {
            hub = SceneManager.GetSceneByPath(HubPath);
            openedHere = !hub.isLoaded;
            if (openedHere) hub = EditorSceneManager.OpenScene(HubPath, OpenSceneMode.Additive);
        }

        [OneTimeTearDown]
        public void CloseHub()
        {
            // סוגרים רק אם הבדיקה פתחה אותו, כדי לא לסגור לגיא סצנה שהוא עובד עליה
            if (openedHere && hub.isLoaded) EditorSceneManager.CloseScene(hub, true);
        }

        private T Find<T>() where T : Component =>
            hub.GetRootGameObjects().Select(go => go.GetComponentInChildren<T>(true)).FirstOrDefault(c => c != null);

        [Test]
        public void Mixer_HasThreeVolumesAndGroups()
        {
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            Assert.That(mixer, Is.Not.Null);
            foreach (string parameter in new[] { VolumeSettings.MasterParameter, VolumeSettings.MusicParameter, VolumeSettings.SfxParameter })
                Assert.That(mixer.GetFloat(parameter, out _), Is.True, parameter + " is not exposed");
            Assert.That(mixer.FindMatchingGroups("Music"), Is.Not.Empty);
            Assert.That(mixer.FindMatchingGroups("SFX"), Is.Not.Empty);
        }

        [Test]
        public void Hub_HasManagers_WithTheMixer()
        {
            Assert.That(Find<SessionManager>(), Is.Not.Null);
            Assert.That(Find<SceneLoader>(), Is.Not.Null);
            var volume = Find<VolumeSettings>();
            Assert.That(volume, Is.Not.Null);
            var mixer = new SerializedObject(volume).FindProperty("mixer").objectReferenceValue;
            Assert.That(mixer, Is.SameAs(AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath)));
        }

        [Test]
        public void Menu_GamesPointToScenesInBuild()
        {
            var menu = Find<HubMenu>();
            Assert.That(menu, Is.Not.Null);
            var inBuild = EditorBuildSettings.scenes.Where(s => s.enabled)
                .Select(s => System.IO.Path.GetFileNameWithoutExtension(s.path)).ToArray();

            var games = new SerializedObject(menu).FindProperty("games");
            int playable = 0;
            for (int i = 0; i < games.arraySize; i++)
            {
                var entry = games.GetArrayElementAtIndex(i);
                Assert.That(entry.FindPropertyRelative("button").objectReferenceValue, Is.Not.Null, $"game {i} has no button");
                string scene = entry.FindPropertyRelative("sceneName").stringValue;
                if (string.IsNullOrEmpty(scene)) continue;
                Assert.That(inBuild, Does.Contain(scene), scene + " is on the menu but not in Build Settings");
                playable++;
            }
            Assert.That(playable, Is.GreaterThan(0));
        }

        [Test]
        public void Hub_IsBakedWithoutRealtimeLights()
        {
            // Quest: אור בזמן אמת עולה ביוקר. כל המנורות אפויות, ואובייקטים זזים מקבלים אור מ-Light Probes.
            Assert.That(Lightmapping.GetLightingDataAssetForScene(hub), Is.Not.Null, "the Hub has no baked lighting");
            var lights = hub.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Light>(true)).ToArray();
            Assert.That(lights, Is.Not.Empty);
            Assert.That(lights.Where(l => l.lightmapBakeType != LightmapBakeType.Baked).Select(l => l.name), Is.Empty);
            Assert.That(Find<LightProbeGroup>(), Is.Not.Null);
        }
    }
}
