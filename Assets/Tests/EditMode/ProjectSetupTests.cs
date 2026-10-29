using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;

namespace MotionArcade.Tests
{
    // בדיקות הגדרה: שומרות על החלטות של שלב 0 שקל לשבור בלי לשים לב,
    // כולל המלכודות שעלו ב-The Lab (סימולטור כפול, פיזיקה ב-50Hz, רינדור כבד מדי ל-Quest).
    public class ProjectSetupTests
    {
        [Test]
        public void Physics_StepsAt90Hz()
        {
            // ברירת המחדל של Unity היא 50Hz. XRPlatformSetup מתאים את הקצב לקסדה בזמן ריצה,
            // אבל גם ב-Editor בלי קסדה הזריקה צריכה להתנהג כמו בקסדה של 90Hz.
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(1f / 90f).Within(1e-5f));
        }

        [Test]
        public void Quality_SingleLevelOnQuestBudget()
        {
            Assert.That(QualitySettings.names.Length, Is.EqualTo(1), "one quality level for every platform");
            var urp = QualitySettings.GetRenderPipelineAssetAt(0) as UniversalRenderPipelineAsset;
            Assert.That(urp, Is.Not.Null, "the quality level must use a URP asset");
            Assert.That(GraphicsSettings.defaultRenderPipeline, Is.SameAs(urp));
            Assert.That(urp.supportsHDR, Is.False, "HDR costs bandwidth on Quest");
            Assert.That(urp.msaaSampleCount, Is.EqualTo(4));
            Assert.That(urp.renderScale, Is.EqualTo(1f));
            Assert.That(urp.useSRPBatcher, Is.True);
        }

        [Test]
        public void OpenXR_StartsOnPC_WithSinglePassInstanced()
        {
            var settings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.InitManagerOnStart, Is.True);
            Assert.That(settings.Manager.activeLoaders, Has.Some.InstanceOf<OpenXRLoader>());

            var openxr = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Standalone);
            Assert.That(openxr.renderMode, Is.EqualTo(OpenXRSettings.RenderMode.SinglePassInstanced));
        }

        [Test]
        public void OpenXR_PC_HasTheControllerProfilesSteamVRUses()
        {
            // 2026-10-28: Unity יצר מחדש את קובץ הגדרות ה-OpenXR כשנוסף Android, והפרופילים של ה-PC נעלמו
            // (נשאר רק Touch Pro, ש-SteamVR לא תומך בו). בלי פרופיל OpenXR לא חושף בקרים: אין ידיים ואין אינטראקציה.
            // PSVR2 דרך SteamVR מוצג כ-Oculus Touch; Index ו-Simple לגיבוי.
            var openxr = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Standalone);
            var on = openxr.GetFeatures().Where(f => f.enabled).Select(f => f.GetType().Name).ToArray();
            Assert.That(on, Does.Contain("OculusTouchControllerProfile"));
            Assert.That(on, Does.Contain("ValveIndexControllerProfile"));
            Assert.That(on, Does.Contain("KHRSimpleControllerProfile"));
        }

        [Test]
        public void Simulator_IsNotCreatedAutomatically()
        {
            // אם זה דולק, XRI יוצר סימולטור גם כשקסדה מחוברת: שני HMD, והמצלמה מקבלת תנוחה משניהם.
            // המחלקה של ההגדרות היא internal ב-XRI, אז קוראים את השדה ישירות מהנכס
            var asset = AssetDatabase.LoadMainAssetAtPath("Assets/XRI/Settings/Resources/XRDeviceSimulatorSettings.asset");
            Assert.That(asset, Is.Not.Null, "XRI simulator settings asset is missing");
            var flag = new SerializedObject(asset).FindProperty("m_AutomaticallyInstantiateSimulatorPrefab");
            Assert.That(flag, Is.Not.Null);
            Assert.That(flag.boolValue, Is.False);
        }

        [Test]
        public void Build_StartsWithHub()
        {
            var scenes = EditorBuildSettings.scenes;
            Assert.That(scenes.Length, Is.GreaterThan(0));
            Assert.That(scenes[0].path, Is.EqualTo("Assets/Scenes/Hub.unity"));
            Assert.That(scenes[0].enabled, Is.True);
        }

        [Test]
        public void Player_KeepsRunningWithoutFocus()
        {
            // בלי זה Play נעצר כשה-Editor לא בפוקוס, וגם המשחק נעצר מתחת לתפריט של SteamVR
            Assert.That(PlayerSettings.runInBackground, Is.True);
            Assert.That(PlayerSettings.gcIncremental, Is.True);
        }
    }
}
