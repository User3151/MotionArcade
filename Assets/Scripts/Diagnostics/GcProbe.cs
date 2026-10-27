#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using MotionArcade.Core;
using Unity.Profiling;
using UnityEngine;

namespace MotionArcade.Diagnostics
{
    // בדיקת GC אוטומטית ב-build של פיתוח. הכלל בתוכנית: 0 בייט הקצאה לפריים בזמן Playing.
    // ב-Editor אי אפשר למדוד את זה, כי ה-Editor עצמו מקצה זיכרון בכל פריים.
    // מופעל רק עם הדגל ‎-gcprobe‎ בשורת הפקודה: טוען משחק, מתחיל סבב, מודד 300 פריימים, כותב ללוג ויוצא.
    // לא קיים בכלל ב-build רגיל (#if למעלה).
    public class GcProbe : MonoBehaviour
    {
        private const string Flag = "-gcprobe";
        private const int Frames = 300;

        [SerializeField] private string gameScene = "Game_Sandbox";

        private static byte[] sink;   // שומר את ההקצאה של בדיקת השפיות, כדי שהקומפיילר לא יוותר עליה

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), Flag) < 0) return;
            var go = new GameObject("GC Probe");
            DontDestroyOnLoad(go);
            go.AddComponent<GcProbe>();
        }

        private IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(3f);   // האולם מתייצב (XR, shaders)

            if (SceneLoader.Instance == null || !SceneLoader.Instance.Load(gameScene))
            {
                Finish("FAILED: could not load " + gameScene);
                yield break;
            }
            while (SceneLoader.Instance.IsBusy) yield return null;

            GameStation station = FindFirstObjectByType<GameStation>();
            if (station == null)
            {
                Finish("FAILED: no GameStation in " + gameScene);
                yield break;
            }
            station.StartRound();
            while (station.Phase != RoundPhase.Playing) yield return null;
            yield return new WaitForSecondsRealtime(1f);   // הפריימים הראשונים של הסבב לא מייצגים

            // בדיקת שפיות: הקצאה מכוונת של 4KB חייבת להופיע במונה. אחרת "0" לא אומר כלום.
            long calibration = 0;
            using (var check = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 10))
            {
                yield return null;
                sink = new byte[4096];
                for (int i = 0; i < 3; i++) yield return null;
                for (int i = 0; i < check.Count; i++) calibration = Math.Max(calibration, check.GetSample(i).Value);
            }
            if (calibration < 4096)
            {
                Finish($"FAILED: the GC counter did not see a 4096-byte allocation (max {calibration})");
                yield break;
            }

            using (var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", Frames))
            {
                for (int i = 0; i < Frames; i++) yield return null;

                long max = 0, sum = 0;
                int withAlloc = 0;
                for (int i = 0; i < recorder.Count; i++)
                {
                    long bytes = recorder.GetSample(i).Value;
                    sum += bytes;
                    if (bytes > max) max = bytes;
                    if (bytes > 0) withAlloc++;
                }
                Finish($"calibration={calibration}B frames={recorder.Count} phase={station.Phase} framesWithAlloc={withAlloc} avgBytes={(recorder.Count > 0 ? sum / recorder.Count : 0)} maxBytes={max}");
            }
        }

        private static void Finish(string result)
        {
            Debug.Log("[GcProbe] " + result);
            Application.Quit();
        }
    }
}
#endif
