#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using MotionArcade.Core;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MotionArcade.Diagnostics
{
    // בדיקת GC אוטומטית ב-build של פיתוח. הכלל בתוכנית: 0 בייט הקצאה לפריים בזמן Playing.
    // ב-Editor אי אפשר למדוד את זה, כי ה-Editor עצמו מקצה זיכרון בכל פריים.
    // מופעל רק עם הדגל ‎-gcprobe‎ בשורת הפקודה: טוען משחק, מתחיל סבב, מודד 300 פריימים, כותב ללוג ויוצא.
    // לא קיים בכלל ב-build רגיל (#if למעלה).
    // ידוע: כשכדורים מתנגשים, Unity 6.3 עצמו מקצה 24 בייט בחלק מהפריימים בתוך Physics.OnSceneContact
    // (נמצא בהקלטת Profiler, 2026-10-28). זה קוד של המנוע, לא של המשחק, ואין לו מנוי בפרויקט.
    // ב-build של Windows (Mono) הקריאה הראשונה לכל פונקציה מקצה זיכרון לקומפילציה (Mono.JIT). זה חד-פעמי,
    // ולא קיים ב-Quest (IL2CPP, הכל מקומפל מראש). לכן יש סבב חימום לפני המדידה.
    public class GcProbe : MonoBehaviour
    {
        private const string Flag = "-gcprobe";
        private const int Frames = 300;

        private string gameScene = "Game_Sandbox";

        private static byte[] sink;   // שומר את ההקצאה של בדיקת השפיות, כדי שהקומפיילר לא יוותר עליה

        // ‎-gcprobe‎ לבד מודד את Game_Sandbox; ‎-gcprobe Game_Basketball‎ מודד משחק אחר
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, Flag);
            if (index < 0) return;
            var go = new GameObject("GC Probe");
            DontDestroyOnLoad(go);
            var probe = go.AddComponent<GcProbe>();
            if (index + 1 < args.Length && !args[index + 1].StartsWith("-")) probe.gameScene = args[index + 1];
        }

        private static string ArgAfter(string flag)
        {
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, flag);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        // באמצע המדידה כל חפץ פיזיקלי במשחק נזרק קצת למעלה וקדימה: התנגשויות, צלילי פגיעה וחזרה למגש
        // נכנסים למדידה, ולא רק סצנה שעומדת במקום
        private void KickBodies()
        {
            Scene scene = SceneManager.GetSceneByName(gameScene);
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Rigidbody body in root.GetComponentsInChildren<Rigidbody>())
                {
                    // חץ: זריקה אמיתית מגובה יד אל הלוח (כיוון באוויר, נעיצה, ניקוד). שאר החפצים: דחיפה למעלה וקדימה.
                    if (body.TryGetComponent(out ILaunchable launchable))
                        launchable.Launch(body.position + Vector3.up * 0.6f, new Vector3(UnityEngine.Random.Range(-0.3f, 0.3f), 2.6f, 5.5f));
                    else if (!body.isKinematic)
                        body.linearVelocity = new Vector3(UnityEngine.Random.Range(-0.5f, 0.5f), 4f, 2.5f);
                }
        }

        private IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(3f);   // האולם מתייצב (XR, shaders)

            // כל המשחקים נטענים יחד בארקייד: מחכים לטעינה ובוחרים את המשחק לפי הסצנה שלו
            if (SceneLoader.Instance == null)
            {
                Finish("FAILED: no SceneLoader");
                yield break;
            }
            while (!SceneLoader.Instance.AllLoaded) yield return null;

            GameStation station = null;
            foreach (GameStation s in GameStation.All) if (s.gameObject.scene.name == gameScene) station = s;
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

            // לפני המדידה: GetCommandLineArgs מחזיר מערך חדש בכל קריאה, וזה היה נספר בפריים הראשון שנמדד
            string capture = ArgAfter("-gcprobe-capture");

            // זריקה ראשונה לחימום: בפעם הראשונה שכמה כדורים מתנגשים יחד, מנוע הפיזיקה מגדיל חוצצים פנימיים
            // (הקצאה חד-פעמית של עשרות בייטים). מודדים את הזריקה השנייה, כלומר את המצב הקבוע של המשחק.
            KickBodies();
            yield return new WaitForSecondsRealtime(1.5f);
            KickBodies();   // לפני המדידה: החיפוש עצמו מקצה זיכרון, והכדורים כבר באוויר כשהיא מתחילה
            yield return null;   // המונה סופר את כל הפריים, כולל מה שקרה בו לפני שהמדידה התחילה

            // ‎-gcprobe-capture <file>‎: גם מקליט את חלון המדידה ל-Profiler, כדי לראות בדיוק מי הקצה (נפתח ב-Editor)
            if (capture != null)
            {
                UnityEngine.Profiling.Profiler.logFile = capture;
                UnityEngine.Profiling.Profiler.enableBinaryLog = true;
                UnityEngine.Profiling.Profiler.enableAllocationCallstacks = true;
                UnityEngine.Profiling.Profiler.enabled = true;
            }
            using (var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", Frames))
            {
                for (int i = 0; i < Frames; i++) yield return null;

                if (capture != null) UnityEngine.Profiling.Profiler.enabled = false;
                long max = 0, sum = 0;
                var detail = new System.Text.StringBuilder();
                int withAlloc = 0;
                for (int i = 0; i < recorder.Count; i++)
                {
                    long bytes = recorder.GetSample(i).Value;
                    sum += bytes;
                    if (bytes > max) max = bytes;
                    if (bytes > 0) { withAlloc++; if (detail.Length < 200) detail.Append(" f").Append(i).Append(":").Append(bytes); }
                }
                Finish($"calibration={calibration}B frames={recorder.Count} phase={station.Phase} framesWithAlloc={withAlloc} avgBytes={(recorder.Count > 0 ? sum / recorder.Count : 0)} maxBytes={max}{detail}");
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
