using System.Linq;
using MotionArcade.Games.AirHockey;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace MotionArcade.Tests
{
    // החיבורים של שולחן ההוקי: הפיזיקה צריכה להתאים למה שרואים, והמחבט צריך להתנהג כמו מחבט.
    public class AirHockeySceneTests
    {
        private const string ScenePath = "Assets/Scenes/Game_AirHockey.unity";

        private Scene scene;
        private bool openedHere;
        private AirHockeyGame game;
        private SerializedObject so;

        [OneTimeSetUp]
        public void Open()
        {
            scene = SceneManager.GetSceneByPath(ScenePath);
            openedHere = !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            game = scene.GetRootGameObjects().Select(r => r.GetComponentInChildren<AirHockeyGame>(true)).First(g => g != null);
            so = new SerializedObject(game);
        }

        [OneTimeTearDown]
        public void Close()
        {
            if (openedHere && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
        }

        private T Ref<T>(string name) where T : Object => (T)so.FindProperty(name).objectReferenceValue;

        [Test]
        public void EverythingIsWired()
        {
            foreach (string name in new[] { "field", "puck", "playerMallet", "playerGrab", "opponentMallet", "playerRest",
                "youScore", "opponentScore", "rule", "difficultyButton", "difficultyLabel", "puckAudio", "tableAudio", "goalForYou", "goalAgainst" })
                Assert.That(so.FindProperty(name).objectReferenceValue, Is.Not.Null, name);
            Assert.That(so.FindProperty("hitClips").arraySize, Is.GreaterThanOrEqualTo(2));
            Assert.That(so.FindProperty("wallClips").arraySize, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void FirstToSeven_WithoutAClock()
        {
            Assert.That(so.FindProperty("goalsToWin").intValue, Is.EqualTo(7));
            Assert.That(game.HasTimer, Is.False, "the board hides the clock; only the countdown shows");
            Assert.That(so.FindProperty("roundSeconds").floatValue, Is.GreaterThanOrEqualTo(600f), "just a cap, not a timer");
        }

        [Test]
        public void ThePhysics_MatchesTheTable()
        {
            // המשטח שרואים והמשטח של הפיזיקה באותו גודל, ופתח השער בגודל של השער בפיזיקה
            var rink = HockeyRink.Standard;
            var field = Ref<Transform>("field");
            Bounds b = field.GetComponent<MeshFilter>().sharedMesh.bounds;
            Assert.That(b.size.x, Is.EqualTo(rink.HalfWidth * 2f).Within(0.002f));
            Assert.That(b.size.z, Is.EqualTo(rink.HalfLength * 2f).Within(0.002f));
            Transform table = field.parent;
            float Inner(string rail) { var t = table.Find(rail); var mb = t.GetComponent<MeshFilter>().sharedMesh.bounds; return Mathf.Abs(field.InverseTransformPoint(t.position).x) - mb.extents.x; }
            Assert.That(Inner("Rail Near Left"), Is.EqualTo(rink.GoalHalfWidth).Within(0.002f), "goal mouth");
            Assert.That(Inner("Rail Far Right"), Is.EqualTo(rink.GoalHalfWidth).Within(0.002f));
            Assert.That(field.InverseTransformPoint(Ref<Transform>("puck").position).y, Is.InRange(0f, 0.01f), "the puck sits on the field");
            var puckMesh = Ref<Transform>("puck").GetComponentInChildren<MeshFilter>().sharedMesh;
            Assert.That(puckMesh.bounds.extents.x, Is.EqualTo(rink.PuckRadius).Within(0.001f));
            var malletMesh = Ref<Transform>("opponentMallet").GetComponentInChildren<MeshFilter>().sharedMesh;
            Assert.That(malletMesh.bounds.extents.x, Is.EqualTo(rink.MalletRadius).Within(0.001f));
        }

        [Test]
        public void TheMallet_IsHeldByHand_AndStaysOnTheTable()
        {
            var grab = Ref<XRGrabInteractable>("playerGrab");
            Assert.That(grab.trackPosition, Is.False, "the game moves it along the field, not XRI");
            Assert.That(grab.trackRotation, Is.False);
            Assert.That(grab.throwOnDetach, Is.False);
            Assert.That(grab.useDynamicAttach, Is.False);
            Assert.That(grab.attachTransform, Is.Not.Null.And.Not.SameAs(grab.transform), "held at the knob");
            Assert.That(grab.GetComponent<MotionArcade.Interaction.NearOnlyGrab>(), Is.Not.Null);
            Assert.That(grab.GetComponent<Rigidbody>().isKinematic, Is.True);
            var rest = Ref<Transform>("playerRest");
            var cup = rest.parent.Find("Cup Near Right");
            Assert.That(Vector3.Distance(new Vector3(rest.position.x, 0f, rest.position.z), new Vector3(cup.position.x, 0f, cup.position.z)), Is.LessThan(0.005f),
                "waits in the near right cup outside a round");
        }

        [Test]
        public void DuringARound_ThePanelStaysAboveTheTable()
        {
            // בהוקי המטרה למטה (הפאק על המשטח): הלוח הקטן עולה מעל קו הראייה, ולא באמצע כמו בחצים
            var dock = scene.GetRootGameObjects().Select(r => r.GetComponentInChildren<MotionArcade.UI.PhaseDock>(true)).First(d => d != null);
            var running = (Transform)new SerializedObject(dock).FindProperty("runningPose").objectReferenceValue;
            Vector3 local = game.transform.InverseTransformPoint(running.position);
            Assert.That(Mathf.Abs(local.x), Is.LessThan(0.1f), "centred");
            Assert.That(local.y, Is.GreaterThan(1.85f), "above the eyes' line to the field");
            var board = dock.GetComponent<MotionArcade.UI.ScoreBoard>();
            Assert.That(board, Is.Not.Null);
            var bso = new SerializedObject(board);
            Assert.That(bso.FindProperty("station").objectReferenceValue, Is.SameAs(game));
            Assert.That(bso.FindProperty("clock").objectReferenceValue, Is.Not.Null, "3-2-1 above the table");
        }

        [Test]
        public void EverySoundGoesThroughTheSfxOrMusicGroup_AndNoLights()
        {
            foreach (var s in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<AudioSource>(true)))
                Assert.That(s.outputAudioMixerGroup != null && (s.outputAudioMixerGroup.name == "SFX" || s.outputAudioMixerGroup.name == "Music"), Is.True, s.name);
            Assert.That(scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Light>(true)), Is.Empty);
        }
    }
}
