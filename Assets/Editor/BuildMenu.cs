using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MotionArcade.EditorTools
{
    // בנייה בלחיצה אחת, מהתפריט "Motion Arcade/Build" או מה-CLI:
    //   unity build --target Android --execute-method MotionArcade.EditorTools.BuildMenu.BuildAndroid
    // כל ההגדרות (IL2CPP, ARM64, Vulkan, OpenXR) כבר שמורות בפרויקט, אז כאן רק בוחרים יעד ותיקייה.
    // שורת סיכום נכתבת גם ל-Builds/last_build.txt, כדי שאפשר יהיה לבדוק את התוצאה בלי לפתוח את ה-Console.
    public static class BuildMenu
    {
        private const string Root = "Builds";   // מחוץ ל-Assets, ו-.gitignore מתעלם ממנה

        [MenuItem("Motion Arcade/Build/Android APK (Quest)")]
        public static void BuildAndroid() =>
            Build(BuildTarget.Android, BuildTargetGroup.Android, "Android/MotionArcade.apk", BuildOptions.None);

        [MenuItem("Motion Arcade/Build/Windows (PC VR)")]
        public static void BuildWindows() =>
            Build(BuildTarget.StandaloneWindows64, BuildTargetGroup.Standalone, "Windows/MotionArcade.exe", BuildOptions.None);

        // לבדיקת GC ו-Profiler: ב-Editor ההקצאות של ה-Editor עצמו מתערבבות במדידה, ב-build הזה לא
        [MenuItem("Motion Arcade/Build/Windows Development (Profiler)")]
        public static void BuildWindowsDevelopment() =>
            Build(BuildTarget.StandaloneWindows64, BuildTargetGroup.Standalone, "WindowsDev/MotionArcade.exe",
                  BuildOptions.Development | BuildOptions.ConnectWithProfiler);

        private static void Build(BuildTarget target, BuildTargetGroup group, string relativePath, BuildOptions options)
        {
            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                Debug.LogError("[BuildMenu] No enabled scenes in Build Settings.");
                return;
            }

            string path = Path.Combine(Root, relativePath);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                target = target,
                targetGroup = group,
                locationPathName = path,
                options = options,
            });

            var s = report.summary;
            string line = $"{s.platform} {s.result} errors={s.totalErrors} warnings={s.totalWarnings} " +
                          $"size={s.totalSize / (1024f * 1024f):0.0}MB time={s.totalTime.TotalSeconds:0}s path={path}";
            Directory.CreateDirectory(Root);
            File.WriteAllText(Path.Combine(Root, "last_build.txt"), line);

            if (s.result == BuildResult.Succeeded) Debug.Log("[BuildMenu] " + line);
            else Debug.LogError("[BuildMenu] " + line);

            // ב-CLI (batch mode) קוד יציאה שאינו 0 הוא מה שמסמן כישלון
            if (Application.isBatchMode && s.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}
