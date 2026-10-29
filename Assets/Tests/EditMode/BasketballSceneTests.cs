using System.Linq;
using MotionArcade.Feedback;
using MotionArcade.Games.Basketball;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace MotionArcade.Tests
{
    // החיבורים של מכונת הכדורסל: כל מה שאם נשבר, הכדור עובר דרך הטבעת, נופל דרך הרצפה או שותק.
    public class BasketballSceneTests
    {
        private const string ScenePath = "Assets/Scenes/Game_Basketball.unity";

        private Scene scene;
        private bool openedHere;
        private BasketballGame game;

        [OneTimeSetUp]
        public void Open()
        {
            scene = SceneManager.GetSceneByPath(ScenePath);
            openedHere = !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            game = scene.GetRootGameObjects().Select(r => r.GetComponentInChildren<BasketballGame>(true)).First(g => g != null);
        }

        [OneTimeTearDown]
        public void Close()
        {
            if (openedHere && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
        }

        private SerializedObject Game => new SerializedObject(game);

        [Test]
        public void EveryBallHasASlot_AndIsFastPhysics()
        {
            var balls = Game.FindProperty("balls");
            var slots = Game.FindProperty("traySlots");
            Assert.That(balls.arraySize, Is.GreaterThan(0));
            Assert.That(slots.arraySize, Is.GreaterThanOrEqualTo(balls.arraySize));
            for (int i = 0; i < balls.arraySize; i++)
            {
                var ball = (Ball)balls.GetArrayElementAtIndex(i).objectReferenceValue;
                Assert.That(ball, Is.Not.Null, $"ball {i}");
                var body = ball.GetComponent<Rigidbody>();
                // כדור של 6 מ'/ש' עובר 7 ס"מ בצעד פיזיקה: בלי Continuous הוא יכול לעבור דרך טבעת בעובי 2 ס"מ
                Assert.That(body.collisionDetectionMode, Is.EqualTo(CollisionDetectionMode.ContinuousDynamic), ball.name);
                Assert.That(body.interpolation, Is.EqualTo(RigidbodyInterpolation.Interpolate), ball.name);
                var grab = ball.GetComponent<XRGrabInteractable>();
                // זריקה משלנו (החלון האחרון), תפיסה רק ביד, ותמיד באותה נקודה מול כף היד
                Assert.That(grab.throwOnDetach, Is.False, ball.name + ": ControllerThrow throws, not XRI");
                Assert.That(ball.GetComponent<MotionArcade.Interaction.ControllerThrow>(), Is.Not.Null, ball.name);
                Assert.That(ball.GetComponent<MotionArcade.Interaction.NearOnlyGrab>(), Is.Not.Null, ball.name);
                Assert.That(grab.useDynamicAttach, Is.False, ball.name);
                Assert.That(grab.attachTransform, Is.Not.Null.And.Not.SameAs(ball.transform), ball.name);
                Assert.That(ball.GetComponent<SphereCollider>().sharedMaterial, Is.Not.Null, ball.name + " has no physic material");
            }
        }

        [Test]
        public void SlotsStartInsideTheTray()
        {
            var tray = Game.FindProperty("tray").boundsValue;
            var machine = Game.FindProperty("machine").boundsValue;
            var slots = Game.FindProperty("traySlots");
            for (int i = 0; i < slots.arraySize; i++)
            {
                Vector3 p = ((Transform)slots.GetArrayElementAtIndex(i).objectReferenceValue).position;
                Assert.That(tray.Contains(p), Is.True, $"slot {i} at {p} is outside the tray");
                Assert.That(machine.Contains(p), Is.True);
            }
        }

        [Test]
        public void HoopHasBothSensors_PointingAtTheGame()
        {
            var triggers = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<HoopTrigger>(true)).ToArray();
            Assert.That(triggers.Length, Is.EqualTo(2));
            var levels = triggers.Select(t => new SerializedObject(t).FindProperty("level").enumValueIndex).OrderBy(x => x).ToArray();
            Assert.That(levels, Is.EqualTo(new[] { 0, 1 }), "one Top and one Bottom");
            foreach (var t in triggers)
            {
                Assert.That(new SerializedObject(t).FindProperty("game").objectReferenceValue, Is.SameAs(game));
                Assert.That(t.GetComponent<Collider>().isTrigger, Is.True);
            }
        }

        [Test]
        public void EverySoundGoesThroughTheSfxGroup()
        {
            // אחרת סליידר ה-EFFECTS בהגדרות לא משפיע עליו
            var sources = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<AudioSource>(true)).ToArray();
            Assert.That(sources, Is.Not.Empty);
            foreach (var s in sources)
                Assert.That(s.outputAudioMixerGroup != null && s.outputAudioMixerGroup.name == "SFX", Is.True, s.name + " is not routed to SFX");
            foreach (var impact in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ImpactSound>(true)))
                Assert.That(new SerializedObject(impact).FindProperty("clip").objectReferenceValue, Is.Not.Null, impact.name);
        }

        [Test]
        public void ControlsPanel_StartsInFrontOfThePlayer()
        {
            // גיא לא ראה את הלוח כשהיה רק בצד: לפני START הוא חייב להיות מול השחקן, ובזמן סבב בצד
            var dock = scene.GetRootGameObjects().Select(r => r.GetComponentInChildren<MotionArcade.UI.PhaseDock>(true)).First(d => d != null);
            var so = new SerializedObject(dock);
            Assert.That(so.FindProperty("station").objectReferenceValue, Is.SameAs(game));
            var idle = (Transform)so.FindProperty("idlePose").objectReferenceValue;
            var running = (Transform)so.FindProperty("runningPose").objectReferenceValue;
            Assert.That(idle, Is.Not.Null);
            Assert.That(running, Is.Not.Null);
            Assert.That(Mathf.Abs(idle.position.x), Is.LessThan(0.1f), "centred");
            Assert.That(idle.position.z, Is.InRange(0.6f, 1.5f), "within arm's reach of the ray, in front of the player");
            Assert.That(Mathf.Abs(running.position.x), Is.LessThan(0.1f), "stays in view during play (not moved to the side)");
            Assert.That(new SerializedObject(dock).FindProperty("runningScale").floatValue, Is.LessThan(0.8f), "smaller during play");
        }

        [Test]
        public void NoRealtimeLightsInTheGame()
        {
            // המשחק מקבל אור מהאולם האפוי (light probes). אור בזמן אמת כאן היה עולה ביוקר ב-Quest.
            Assert.That(scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Light>(true)), Is.Empty);
        }
    }
}
