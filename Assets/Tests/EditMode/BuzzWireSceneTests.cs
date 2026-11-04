using System.Linq;
using MotionArcade.Games.BuzzWire;
using MotionArcade.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace MotionArcade.Tests
{
    // החיבורים של Buzz Wire: המסלולים בטווח ובגבהים שהתוכנית דורשת, השרביט בלי החלקה, והלוחות.
    public class BuzzWireSceneTests
    {
        private const string ScenePath = "Assets/Scenes/Game_BuzzWire.unity";

        private Scene scene;
        private bool openedHere;
        private BuzzWireGame game;
        private SerializedObject so;

        [OneTimeSetUp]
        public void Open()
        {
            scene = SceneManager.GetSceneByPath(ScenePath);
            openedHere = !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            game = scene.GetRootGameObjects().Select(r => r.GetComponentInChildren<BuzzWireGame>(true)).First(g => g != null);
            so = new SerializedObject(game);
        }

        [OneTimeTearDown]
        public void Close()
        {
            if (openedHere && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
        }

        private WireTrack[] Tracks()
        {
            var p = so.FindProperty("tracks");
            return Enumerable.Range(0, p.arraySize).Select(i => (WireTrack)p.GetArrayElementAtIndex(i).objectReferenceValue).ToArray();
        }

// המשחק עומד בפינה שלו בארקייד: הכל נבדק ביחס אליו (השורש = המקום שבו השחקן עומד, +Z = מולו)
        private Vector3 L(Transform t) => game.transform.InverseTransformPoint(t.position);
        private Vector3 L(Vector3 world) => game.transform.InverseTransformPoint(world);
        private Vector3 LD(Vector3 direction) => game.transform.InverseTransformDirection(direction);

        private Vector3[] Samples(WireTrack track)
        {
            var container = (SplineContainer)new SerializedObject(track).FindProperty("container").objectReferenceValue;
            return Enumerable.Range(0, 101).Select(i => L((Vector3)container.EvaluatePosition(i / 100f))).ToArray();
        }

        [Test]
        public void ThreeTracks_AllWiredUp()
        {
            var tracks = Tracks();
            Assert.That(tracks.Length, Is.EqualTo(3));
            foreach (var t in tracks)
            {
                var tso = new SerializedObject(t);
                Assert.That(tso.FindProperty("container").objectReferenceValue, Is.Not.Null, t.name);
                Assert.That(tso.FindProperty("wire").objectReferenceValue, Is.Not.Null, t.name);
                Assert.That(tso.FindProperty("startMarker").objectReferenceValue, Is.Not.Null, t.name);
                Assert.That(tso.FindProperty("checkpointMarkers").arraySize, Is.EqualTo(WireProgress.Checkpoints.Length), t.name);
                Assert.That(t.GetComponentInChildren<MeshFilter>(true).sharedMesh, Is.Not.Null, t.name + ": the tube mesh is baked");
                Assert.That(t.GetComponentsInChildren<SplineExtrude>(true), Is.Empty, t.name + ": no mesh rebuilding at runtime");
            }
        }

        [Test]
        public void Tracks_StayInFrontWithinReach()
        {
            foreach (var t in Tracks())
                foreach (var p in Samples(t))
                {
                    Assert.That(p.z, Is.InRange(0.45f, 0.8f), t.name + " " + p);
                    Assert.That(Mathf.Abs(p.x), Is.LessThan(0.7f), t.name + " " + p);
                }
        }

        [Test]
        public void HardestTrack_GoesAboveTheHeadAndDownToTheKnee_AndCrossesTheMiddle()
        {
            var points = Samples(Tracks()[2]);
            Assert.That(points.Max(p => p.y), Is.GreaterThan(1.85f), "above head height for an adult");
            Assert.That(points.Min(p => p.y), Is.LessThan(0.7f), "knee height");
            Assert.That(points.Min(p => p.x), Is.LessThan(-0.4f));
            Assert.That(points.Max(p => p.x), Is.GreaterThan(0.4f));
        }

        [Test]
        public void Wand_HasNoSmoothing_AndRingBiggerThanTheWire()
        {
            var wand = (BuzzWand)so.FindProperty("wand").objectReferenceValue;
            Assert.That(wand, Is.Not.Null);
            var grab = wand.GetComponent<XRGrabInteractable>();
            // הרעד האמיתי של היד הוא מה שהמשחק מודד: בלי החלקה ובלי עיכוב
            Assert.That(grab.movementType, Is.EqualTo(XRBaseInteractable.MovementType.Instantaneous));
            Assert.That(grab.smoothPosition, Is.False);
            Assert.That(grab.smoothRotation, Is.False);
            Assert.That(wand.Ring, Is.Not.Null);
            float wire = so.FindProperty("wireRadius").floatValue;
            float clearance = wand.RingRadius - wand.RingTube - wire;
            Assert.That(clearance, Is.InRange(0.01f, 0.03f), "free space between ring and wire");
            Assert.That(so.FindProperty("wandRest").objectReferenceValue, Is.Not.Null);
        }

        [Test]
        public void ControlsPanel_HasTrackButton_AndIsCentredBeforeStart()
        {
            Assert.That(so.FindProperty("trackButton").objectReferenceValue, Is.Not.Null);
            var dock = scene.GetRootGameObjects().Select(r => r.GetComponentInChildren<PhaseDock>(true)).First(d => d != null);
            var dso = new SerializedObject(dock);
            Assert.That(Mathf.Abs(L((Transform)dso.FindProperty("idlePose").objectReferenceValue).x), Is.LessThan(0.1f));
            Assert.That(Mathf.Abs(L((Transform)dso.FindProperty("runningPose").objectReferenceValue).x), Is.LessThan(0.1f), "stays in view during play");
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
