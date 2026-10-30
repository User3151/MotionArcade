using System.Linq;
using MotionArcade.Interaction;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace MotionArcade.Tests
{
    // הידיים על הבקרים
    public class HandCurlTests
    {
        [Test]
        public void TriggerBendsTheIndex_GripTheOthers()
        {
            float relaxedIndex = HandCurl.Target(1, 0f, 0f, false, HandShape.Fist);
            Assert.That(HandCurl.Target(1, 0f, 1f, false, HandShape.Fist), Is.GreaterThan(relaxedIndex + 0.5f));
            Assert.That(HandCurl.Target(1, 1f, 0f, false, HandShape.Fist), Is.EqualTo(relaxedIndex), "grip does not move the index");
            for (int f = 2; f < HandCurl.Fingers; f++)
            {
                Assert.That(HandCurl.Target(f, 1f, 0f, false, HandShape.Fist), Is.GreaterThan(0.9f), "finger " + f);
                Assert.That(HandCurl.Target(f, 0f, 1f, false, HandShape.Fist), Is.LessThan(0.3f), "trigger alone leaves finger " + f);
            }
        }

        [Test]
        public void HeldObjects_HaveTheirOwnShape()
        {
            // כדור: כף כמעט פתוחה; חץ: האגודל והמורה נפגשים, השאר מקופלות; פטיש: אגרוף
            for (int f = 0; f < HandCurl.Fingers; f++)
                Assert.That(HandCurl.Target(f, 1f, 1f, true, HandShape.Cup), Is.LessThan(0.3f), "cup finger " + f);
            Assert.That(HandCurl.Target(0, 0f, 0f, true, HandShape.Pinch), Is.InRange(0.4f, 0.7f));
            Assert.That(HandCurl.Target(1, 0f, 0f, true, HandShape.Pinch), Is.InRange(0.4f, 0.7f));
            Assert.That(HandCurl.Target(4, 0f, 0f, true, HandShape.Pinch), Is.GreaterThan(0.85f));
            for (int f = 1; f < HandCurl.Fingers; f++)
                Assert.That(HandCurl.Target(f, 0f, 0f, true, HandShape.Fist), Is.GreaterThan(0.85f), "fist finger " + f);
        }

        [Test]
        public void HandPose_MirrorsForTheLeftHand()
        {
            var go = new GameObject("hold");
            try
            {
                var hold = go.AddComponent<HandHold>();
                var so = new SerializedObject(hold);
                so.FindProperty("handPosition").vector3Value = new Vector3(0.02f, 0.01f, -0.03f);
                so.FindProperty("handRotation").vector3Value = new Vector3(-90f, 10f, 20f);
                so.ApplyModifiedPropertiesWithoutUndo();
                hold.HandPose(false, out Vector3 rp, out Quaternion rr);
                hold.HandPose(true, out Vector3 lp, out Quaternion lr);
                Assert.That(lp, Is.EqualTo(new Vector3(-rp.x, rp.y, rp.z)));
                Assert.That(Quaternion.Angle(lr, Quaternion.Euler(-90f, -10f, -20f)), Is.LessThan(0.01f));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }

    public class HandSceneTests
    {
        private static Scene Open(string path, out bool openedHere)
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            openedHere = !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            return scene;
        }

        [Test]
        public void BothControllers_ShowAHand_InsteadOfTheController()
        {
            Scene hub = Open("Assets/Scenes/Hub.unity", out bool opened);
            try
            {
                var hands = hub.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ControllerHand>(true)).ToArray();
                Assert.That(hands.Length, Is.EqualTo(2));
                foreach (var hand in hands)
                {
                    var so = new SerializedObject(hand);
                    var interactor = (Component)so.FindProperty("interactor").objectReferenceValue;
                    Assert.That(interactor, Is.Not.Null, hand.name);
                    Assert.That(hand.transform.parent, Is.SameAs(interactor.transform.parent), hand.name + " sits on the controller it reads");
                    var visual = (GameObject)so.FindProperty("controllerVisual").objectReferenceValue;
                    Assert.That(visual, Is.Not.Null, hand.name);
                    Assert.That(visual.transform.parent, Is.SameAs(hand.transform.parent), hand.name);
                    bool left = hand.transform.parent.name.StartsWith("Left");
                    Assert.That(so.FindProperty("bonePrefix").stringValue, Is.EqualTo(left ? "L_" : "R_"), hand.name);
                    // כל העצמות נמצאו (שם הדגם תואם לקידומת)
                    Assert.That(hand.GetComponentsInChildren<Transform>(true).Count(t => t.name.StartsWith(left ? "L_" : "R_") && t.name.EndsWith("Distal")), Is.EqualTo(5), hand.name);
                    var renderer = hand.GetComponentInChildren<SkinnedMeshRenderer>(true);
                    Assert.That(renderer.sharedMaterials.Length, Is.EqualTo(1), "only the hand material, no depth-only pass");
                    Assert.That(renderer.sharedMaterial.shader.name, Does.Contain("Universal Render Pipeline"));
                }
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(hub, true);
            }
        }

        [TestCase("Assets/Scenes/Game_Basketball.unity", HandShape.Cup)]
        [TestCase("Assets/Scenes/Game_Darts.unity", HandShape.Pinch)]
        [TestCase("Assets/Scenes/Game_Whack.unity", HandShape.Fist)]
        [TestCase("Assets/Scenes/Game_BuzzWire.unity", HandShape.Fist)]
        public void EveryGrabbable_SaysHowTheHandHoldsIt(string path, HandShape shape)
        {
            Scene scene = Open(path, out bool opened);
            try
            {
                var grabs = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<XRGrabInteractable>(true)).ToArray();
                Assert.That(grabs, Is.Not.Empty);
                foreach (var g in grabs)
                {
                    var hold = g.GetComponent<HandHold>();
                    Assert.That(hold, Is.Not.Null, g.name);
                    Assert.That(hold.Shape, Is.EqualTo(shape), g.name);
                    // בכדור: כף היד מאחורי הכדור, האצבעות למעלה
                    Assert.That(hold.MovesHand, Is.EqualTo(shape == HandShape.Cup), g.name);
                }
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
