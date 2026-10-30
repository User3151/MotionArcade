using System.Linq;
using MotionArcade.Games.Darts;
using MotionArcade.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace MotionArcade.Tests
{
    // החיבורים של משחק החצים: מידות הלוח, משטח הנעיצה, החצים והמחזיק, והלוחות.
    public class DartsSceneTests
    {
        private const string ScenePath = "Assets/Scenes/Game_Darts.unity";

        private Scene scene;
        private bool openedHere;
        private DartsGame game;
        private SerializedObject so;

        [OneTimeSetUp]
        public void Open()
        {
            scene = SceneManager.GetSceneByPath(ScenePath);
            openedHere = !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            game = scene.GetRootGameObjects().Select(r => r.GetComponentInChildren<DartsGame>(true)).First(g => g != null);
            so = new SerializedObject(game);
        }

        [OneTimeTearDown]
        public void Close()
        {
            if (openedHere && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
        }

        [Test]
        public void Board_AtRegulationHeight_CloserAndBigger_FacingThePlayer()
        {
            var board = (DartBoard)so.FindProperty("board").objectReferenceValue;
            Assert.That(board, Is.Not.Null);
            Assert.That(board.transform.position.y, Is.EqualTo(1.73f).Within(0.005f), "bull height");
            // גיא, אחרי מדידה: הלוח התקני (2.37 מ', רדיוס 17 ס"מ) רחוק וקטן מדי לזריקה בלי משקל ביד
            Assert.That(board.transform.position.z, Is.EqualTo(2.0f).Within(0.005f), "throwing distance from the stand spot");
            Assert.That(board.Size, Is.EqualTo(1.3f).Within(1e-4f), "30% bigger than regulation");
            Assert.That(board.transform.Find("Face").localScale.x, Is.EqualTo(0.451f * board.Size).Within(0.002f), "the drawn board matches the scoring size");
            Assert.That(board.ScoreAt(board.transform.TransformPoint(0f, 0.15f, 0f)).Number, Is.EqualTo(20), "15 cm up is still inside the 20 (17 cm x 1.3)");
            Assert.That(Vector3.Angle(board.transform.forward, Vector3.forward), Is.LessThan(1f), "face toward -Z, the player");
            Assert.That(board.StickSurface, Is.Not.Null);
            Assert.That(board.StickSurface.isTrigger, Is.False, "a glancing dart must bounce off it");
            // משטח הנעיצה מתחיל בדיוק בפני הלוח, כדי שהחוד ננעץ בגובה הלוח
            Assert.That(board.transform.InverseTransformPoint(board.StickSurface.bounds.min).z, Is.EqualTo(0f).Within(0.002f));
        }

        [Test]
        public void ThreeDarts_EachWithASlotInTheHolder()
        {
            var darts = so.FindProperty("darts");
            var slots = so.FindProperty("holderSlots");
            var holder = so.FindProperty("holder").boundsValue;
            Assert.That(darts.arraySize, Is.EqualTo(3));
            Assert.That(slots.arraySize, Is.GreaterThanOrEqualTo(3));
            for (int i = 0; i < darts.arraySize; i++)
            {
                var dart = (Dart)darts.GetArrayElementAtIndex(i).objectReferenceValue;
                Assert.That(dart, Is.Not.Null);
                var body = dart.GetComponent<Rigidbody>();
                Assert.That(body.collisionDetectionMode, Is.EqualTo(CollisionDetectionMode.ContinuousDynamic), dart.name);
                var grab = dart.GetComponent<XRGrabInteractable>();
                Assert.That(grab.throwOnDetach, Is.False, dart.name + ": ControllerThrow throws, not XRI");
                Assert.That(dart.GetComponent<MotionArcade.Interaction.ControllerThrow>(), Is.Not.Null, dart.name);
                Assert.That(dart.GetComponent<MotionArcade.Interaction.NearOnlyGrab>(), Is.Not.Null, dart.name);
                // תמיד באותה אחיזה: מרכז הקנה 1.5 ס"מ מעל נקודת האחיזה (בין האגודל לאצבע), והחוד 22° מתחת
                // לקרן של הבקר. מהמדידה: הבקר הצביע 49° למעלה בזמן שהזריקות יצאו ב-26°.
                Assert.That(grab.useDynamicAttach, Is.False, dart.name);
                Quaternion r = grab.attachTransform.localRotation;
                Vector3 barrelInHand = Quaternion.Inverse(r) * (new Vector3(0f, 0f, 0.025f) - grab.attachTransform.localPosition);
                Assert.That(Vector3.Distance(barrelInHand, new Vector3(0f, 0.015f, 0f)), Is.LessThan(1e-3f), dart.name + " barrel in the fingers");
                Vector3 tipInHand = Quaternion.Inverse(r) * Vector3.forward;
                Assert.That(Vector3.Angle(tipInHand, Quaternion.Euler(22f, 0f, 0f) * Vector3.forward), Is.LessThan(0.5f), dart.name + ": tip 22° below the ray");
                var thrower = new SerializedObject(dart.GetComponent<MotionArcade.Interaction.ControllerThrow>());
                Assert.That(thrower.FindProperty("source").enumValueIndex, Is.EqualTo(0), "speed from the hand's peak");
                Assert.That(thrower.FindProperty("velocityScale").floatValue, Is.EqualTo(1f).Within(1e-4f));
                var slot = (Transform)slots.GetArrayElementAtIndex(i).objectReferenceValue;
                Assert.That(holder.Contains(slot.position), Is.True, $"slot {i} outside the holder bounds");
                Assert.That(Vector3.Angle(slot.forward, Vector3.forward), Is.LessThan(1f), "darts lie pointing at the board");
            }
        }

        [Test]
        public void EveryNumberHasALabel()
        {
            var labels = so.FindProperty("numberLabels");
            Assert.That(labels.arraySize, Is.EqualTo(21));
            for (int n = 1; n <= 20; n++)
                Assert.That(labels.GetArrayElementAtIndex(n).objectReferenceValue, Is.Not.Null, "label " + n);
            Assert.That(so.FindProperty("targetDisplay").objectReferenceValue, Is.Not.Null);
        }

        [Test]
        public void ControlsPanel_CentredBeforeStart_AndBelowTheLineOfSight()
        {
            var dock = scene.GetRootGameObjects().Select(r => r.GetComponentInChildren<PhaseDock>(true)).First(d => d != null);
            var dso = new SerializedObject(dock);
            var idle = (Transform)dso.FindProperty("idlePose").objectReferenceValue;
            var running = (Transform)dso.FindProperty("runningPose").objectReferenceValue;
            Assert.That(Mathf.Abs(idle.position.x), Is.LessThan(0.1f));
            Assert.That(Mathf.Abs(running.position.x), Is.LessThan(0.1f), "stays in view during play (not moved to the side)");
            // הלוח המרכזי לא מסתיר את מרכז לוח החצים מעין בגובה 1.6 מ'
            var rt = (RectTransform)dock.transform;
            float topEdge = idle.position.y + rt.rect.height * rt.lossyScale.y * 0.5f;
            var board = (DartBoard)so.FindProperty("board").objectReferenceValue;
            float sightAtPanel = 1.6f + (1.73f - 1.6f) * idle.position.z / board.transform.position.z;
            Assert.That(topEdge, Is.LessThan(sightAtPanel));
        }

        [Test]
        public void EverySoundGoesThroughTheSfxGroup()
        {
            var sources = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<AudioSource>(true)).ToArray();
            Assert.That(sources, Is.Not.Empty);
            foreach (var s in sources)
                Assert.That(s.outputAudioMixerGroup != null && s.outputAudioMixerGroup.name == "SFX", Is.True, s.name);
        }

        [Test]
        public void NoRealtimeLightsInTheGame()
        {
            Assert.That(scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Light>(true)), Is.Empty);
        }
    }
}
