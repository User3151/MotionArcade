using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.XR.Management;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

namespace MotionArcade.Tests
{
    // הגדרות Android ל-Quest (ובהמשך Pico). אין לנו מכשיר לבדוק עליו, אז כל הגדרה שנשברת בשקט
    // תתגלה רק כשמישהו ינסה להתקין APK. הבדיקות כאן תופסות את זה מוקדם.
    public class AndroidSetupTests
    {
        [Test]
        public void Player_IL2CPP_ARM64_Vulkan()
        {
            // OpenXR ב-Android עובד רק ב-ARM64, ו-ARM64 דורש IL2CPP
            Assert.That(PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android), Is.EqualTo(ScriptingImplementation.IL2CPP));
            Assert.That(PlayerSettings.Android.targetArchitectures, Is.EqualTo(AndroidArchitecture.ARM64));
            Assert.That(PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android), Is.False);
            Assert.That(PlayerSettings.GetGraphicsAPIs(BuildTarget.Android), Is.EqualTo(new[] { GraphicsDeviceType.Vulkan }));
        }

        [Test]
        public void Player_MinApi29()
        {
            // Android 10: הרצפה של Pico 4 ושל Quest 2
            Assert.That((int)PlayerSettings.Android.minSdkVersion, Is.GreaterThanOrEqualTo(29));
        }

        [Test]
        public void Identity_HasNoRealName()
        {
            // הריפו יהיה ציבורי: שם החברה ומזהה החבילה לא חושפים את השם האמיתי
            Assert.That(PlayerSettings.companyName, Is.EqualTo("User3151"));
            Assert.That(PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android), Is.EqualTo("com.user3151.motionarcade"));
        }

        [Test]
        public void OpenXR_StartsOnAndroid()
        {
            var settings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.InitManagerOnStart, Is.True);
            Assert.That(settings.Manager.activeLoaders, Has.Some.InstanceOf<OpenXRLoader>());
        }

        [Test]
        public void OpenXR_QuestFeaturesOn_WithMultiview()
        {
            var openxr = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            // ב-Android "Single Pass Instanced" הוא Multiview: שתי העיניים בקריאת ציור אחת
            Assert.That(openxr.renderMode, Is.EqualTo(OpenXRSettings.RenderMode.SinglePassInstanced));

            // לפי שם הטיפוס, כדי לא לחבר את אסמבלי הבדיקות לכל אסמבלי של פיצ'ר
            string[] on = openxr.GetFeatures().Where(f => f.enabled).Select(f => f.GetType().Name).ToArray();
            Assert.That(on, Does.Contain("MetaQuestFeature"));
            Assert.That(on, Does.Contain("OculusTouchControllerProfile"));
            Assert.That(on, Does.Contain("FoveatedRenderingFeature"));

            // הבקר נקרא אחרי ההמתנה לפריים, אז התנוחה שמגיעה לזריקה ולמדידה טרייה יותר
            Assert.That(openxr.latencyOptimization, Is.EqualTo(OpenXRSettings.LatencyOptimization.PrioritizeInputPolling));
        }

        [Test]
        public void OpenXR_FoveationDoesNotAskForEyeTracking()
        {
            // מעקב עיניים מוסיף הרשאה ל-manifest וחלון בקשת הרשאה ב-Quest Pro, ואין לו שימוש כאן
            var foveation = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android).GetFeature<FoveatedRenderingFeature>();
            Assert.That(foveation, Is.Not.Null);
            var eye = new SerializedObject(foveation).FindProperty("useEyeTracking");
            Assert.That(eye, Is.Not.Null);
            Assert.That(eye.boolValue, Is.False);
        }
    }
}
