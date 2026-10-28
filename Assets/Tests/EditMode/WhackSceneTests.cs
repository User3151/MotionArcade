using System.Linq;
using MotionArcade.Games.Whack;
using MotionArcade.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace MotionArcade.Tests
{
    // החיבורים של Whack-A-Mole: החורים בטווח היד וסביב השחקן, החפרפרות יכולות להיפגע, הפטישים והשולחן.
    public class WhackSceneTests
    {
        private const string ScenePath = "Assets/Scenes/Game_Whack.unity";

        private Scene scene;
        private bool openedHere;
        private WhackGame game;
        private SerializedObject so;

        [OneTimeSetUp]
        public void Open()
        {
            scene = SceneManager.GetSceneByPath(ScenePath);
            openedHere = !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            game = scene.GetRootGameObjects().Select(r => r.GetComponentInChildren<WhackGame>(true)).First(g => g != null);
            so = new SerializedObject(game);
        }

        [OneTimeTearDown]
        public void Close()
        {
            if (openedHere && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
        }

        private MoleHole[] Holes()
        {
            var p = so.FindProperty("holes");
            return Enumerable.Range(0, p.arraySize).Select(i => (MoleHole)p.GetArrayElementAtIndex(i).objectReferenceValue).ToArray();
        }

        [Test]
        public void TwelveHoles_AroundThePlayer_WithinReach()
        {
            var holes = Holes();
            Assert.That(holes.Length, Is.EqualTo(12));
            Assert.That(holes, Has.None.Null);
            foreach (var h in holes)
            {
                Vector3 p = h.transform.position;
                float horizontal = new Vector2(p.x, p.z).magnitude;
                Assert.That(horizontal, Is.InRange(0.6f, 0.8f), h.name + ": reachable with a mallet from the stand spot");
                Assert.That(p.y, Is.InRange(0.85f, 1.75f), h.name);
                // הפתח פונה אל השחקן ולמעלה: מכה מלמעלה ולפנים נכנסת לחור
                Assert.That(Vector3.Dot(h.Normal, -new Vector3(p.x, 0f, p.z).normalized), Is.GreaterThan(0.3f), h.name + " faces the player");
                Assert.That(h.Normal.y, Is.GreaterThan(0.3f), h.name + " tilts up");
            }
            // גם שמאל וגם ימין, גם נמוך וגם גבוה: הטווח של התנועה
            Assert.That(holes.Min(h => h.transform.position.x), Is.LessThan(-0.5f));
            Assert.That(holes.Max(h => h.transform.position.x), Is.GreaterThan(0.5f));
            Assert.That(holes.Max(h => h.transform.position.y) - holes.Min(h => h.transform.position.y), Is.GreaterThan(0.6f));
        }

        [Test]
        public void EveryMole_IsAKinematicTriggerWithASound()
        {
            foreach (var h in Holes())
            {
                var rb = h.GetComponent<Rigidbody>();
                Assert.That(rb != null && rb.isKinematic, Is.True, h.name + ": a moving trigger needs a kinematic body");
                var hso = new SerializedObject(h);
                var col = (Collider)hso.FindProperty("moleCollider").objectReferenceValue;
                Assert.That(col != null && col.isTrigger, Is.True, h.name);
                Assert.That(col.attachedRigidbody, Is.SameAs(rb), h.name + ": trigger events must reach MoleHole");
                Assert.That(hso.FindProperty("popClip").objectReferenceValue, Is.Not.Null, h.name);
                Assert.That(hso.FindProperty("source").objectReferenceValue, Is.Not.Null, h.name);
            }
        }

        [Test]
        public void TwoMallets_WithAHeadTrigger_OnTheTable()
        {
            var mallets = so.FindProperty("mallets");
            var slots = so.FindProperty("malletSlots");
            var table = so.FindProperty("table").boundsValue;
            Assert.That(mallets.arraySize, Is.EqualTo(2));
            Assert.That(slots.arraySize, Is.EqualTo(2));
            for (int i = 0; i < 2; i++)
            {
                var m = (Mallet)mallets.GetArrayElementAtIndex(i).objectReferenceValue;
                Assert.That(m, Is.Not.Null);
                Assert.That(m.Head != null && m.Head.isTrigger, Is.True, m.name);
                Assert.That(m.Head.attachedRigidbody, Is.SameAs(m.GetComponent<Rigidbody>()), m.name);
                var grab = m.GetComponent<XRGrabInteractable>();
                Assert.That(grab.attachTransform, Is.Not.Null, m.name + ": held at the handle");
                Assert.That(grab.throwOnDetach, Is.False, m.name);
                var slot = (Transform)slots.GetArrayElementAtIndex(i).objectReferenceValue;
                Assert.That(table.Contains(slot.position), Is.True, m.name + " slot");
                Assert.That(slot.position.z, Is.GreaterThan(0.15f), "not inside the player's body");
            }
        }

        [Test]
        public void ControlsPanel_CentredBeforeStart()
        {
            var dock = scene.GetRootGameObjects().Select(r => r.GetComponentInChildren<PhaseDock>(true)).First(d => d != null);
            var dso = new SerializedObject(dock);
            var idle = (Transform)dso.FindProperty("idlePose").objectReferenceValue;
            var running = (Transform)dso.FindProperty("runningPose").objectReferenceValue;
            Assert.That(Mathf.Abs(idle.position.x), Is.LessThan(0.1f));
            Assert.That(Mathf.Abs(running.position.x), Is.GreaterThan(0.85f));
        }

        [Test]
        public void EverySoundGoesThroughTheSfxGroup()
        {
            var sources = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<AudioSource>(true)).ToArray();
            Assert.That(sources.Length, Is.GreaterThanOrEqualTo(12));
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
