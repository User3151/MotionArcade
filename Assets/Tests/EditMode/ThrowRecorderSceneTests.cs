using System.Linq;
using MotionArcade.Diagnostics;
using MotionArcade.Interaction;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MotionArcade.Tests
{
    // מדידת הזריקות מחוברת בשני משחקי הזריקה: לכל החפצים שנזרקים, למטרה הנכונה, ולמשחק שמדווח תוצאות.
    public class ThrowRecorderSceneTests
    {
        [TestCase("Assets/Scenes/Game_Basketball.unity", "Hoop", ThrowRecorder.TargetPlane.RimFromAbove, 4)]
        [TestCase("Assets/Scenes/Game_Darts.unity", "Dartboard", ThrowRecorder.TargetPlane.BoardFace, 3)]
        public void RecorderIsWired(string path, string targetName, ThrowRecorder.TargetPlane plane, int minThrowables)
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            bool openedHere = !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var recorders = roots.SelectMany(r => r.GetComponentsInChildren<ThrowRecorder>(true)).ToArray();
                Assert.That(recorders.Length, Is.EqualTo(1));
                var so = new SerializedObject(recorders[0]);

                var station = so.FindProperty("station").objectReferenceValue as MotionArcade.Core.GameStation;
                Assert.That(station, Is.Not.Null);
                var gameSo = new SerializedObject(station);
                Assert.That(gameSo.FindProperty("recorder").objectReferenceValue, Is.EqualTo(recorders[0]), "the game reports results to it");

                var all = roots.SelectMany(r => r.GetComponentsInChildren<ControllerThrow>(true)).ToArray();
                Assert.That(all.Length, Is.GreaterThanOrEqualTo(minThrowables));
                var list = so.FindProperty("throwables");
                var wired = Enumerable.Range(0, list.arraySize).Select(i => list.GetArrayElementAtIndex(i).objectReferenceValue).ToArray();
                Assert.That(wired, Is.EquivalentTo(all), "every thrown object is measured");

                var target = so.FindProperty("target").objectReferenceValue as Transform;
                Assert.That(target, Is.Not.Null);
                Assert.That(target.name, Is.EqualTo(targetName));
                Assert.That((ThrowRecorder.TargetPlane)so.FindProperty("plane").enumValueIndex, Is.EqualTo(plane));
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void LogsAreWrittenNextToTheProject_NotToAppData()
        {
            // חשוב: כונן C כמעט מלא. ב-Editor הקבצים ליד Assets (בכונן של הפרויקט).
            string folder = ThrowRecorder.Folder;
            string project = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, ".."));
            Assert.That(folder, Does.StartWith(project));
        }
    }
}
