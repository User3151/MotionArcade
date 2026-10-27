using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;

namespace MotionArcade.Core
{
    // מתאים את הריצה לקסדה שמחוברת בפועל, פעם אחת בתחילת הסצנה:
    // 1. פיזיקה בקצב הרענון של הקסדה. זריקה ומדידת מהירות נשענות על FixedUpdate,
    //    ואם הפיזיקה רצה בקצב אחר מהתמונה, כדור שנזרק "מקפץ" ומהירות השחרור יוצאת רועשת.
    // 2. בלי קסדה (Editor בלי SteamVR) מפעיל את סימולטור XRI. ההפעלה האוטומטית של XRI כבויה בכוונה:
    //    היא יוצרת את הסימולטור גם כשיש קסדה, ואז יש שני HMD והמצלמה מקבלת תנוחה משניהם.
    [DefaultExecutionOrder(-1000)]
    public class XRPlatformSetup : MonoBehaviour
    {
        [SerializeField] private GameObject simulatorPrefab;
        [SerializeField] private bool simulatorOnlyInEditor = true;   // ב-build אמיתי אין סימולטור
        [SerializeField] private float fallbackRefreshRate = 90f;     // כשהקסדה לא מדווחת קצב
        [SerializeField] private float headsetWaitSeconds = 5f;       // SteamVR יכול לפתוח session באיחור

        public static bool HeadsetActive { get; private set; }
        public static float RefreshRate { get; private set; }

        private static readonly List<XRDisplaySubsystem> displays = new List<XRDisplaySubsystem>();
        private static bool started;   // רק עותק אחד פועל, גם אם סצנה נוספת מביאה עוד אחד

        // עם "Enter Play Mode Options" בלי domain reload, ערכים סטטיים נשארים מהריצה הקודמת
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            HeadsetActive = false;
            RefreshRate = 0f;
            started = false;
        }

        private IEnumerator Start()
        {
            if (started)
            {
                Debug.LogWarning("[XRPlatformSetup] A second copy was ignored.", this);
                yield break;
            }
            started = true;
            ApplyRate(fallbackRefreshRate);

            // activeLoader ריק = OpenXR לא הצליח לעלות (אין runtime או אין קסדה). אין טעם לחכות.
            var manager = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
            if (manager == null || manager.activeLoader == null)
            {
                UseSimulator("XR loader is not active");
                yield break;
            }

            // ה-loader עלה. מחכים שהתצוגה תרוץ ותדווח קצב רענון, כי זה לא קורה באותו פריים.
            float deadline = Time.realtimeSinceStartup + headsetWaitSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (TryGetRunningDisplay(out var display) &&
                    display.TryGetDisplayRefreshRate(out float rate) && rate >= 60f)
                {
                    HeadsetActive = true;
                    ApplyRate(rate);
                    Debug.Log($"[XRPlatformSetup] Headset running at {rate:0.#} Hz, physics step {Time.fixedDeltaTime * 1000f:0.##} ms");
                    yield break;
                }
                yield return null;
            }

            UseSimulator($"no running XR display after {headsetWaitSeconds:0.#} s");
        }

        private static void ApplyRate(float rate)
        {
            RefreshRate = rate;
            Time.fixedDeltaTime = 1f / rate;
        }

        private static bool TryGetRunningDisplay(out XRDisplaySubsystem running)
        {
            SubsystemManager.GetSubsystems(displays);
            foreach (var d in displays)
            {
                if (d.running)
                {
                    running = d;
                    return true;
                }
            }
            running = null;
            return false;
        }

        private void UseSimulator(string reason)
        {
            HeadsetActive = false;
            if (simulatorPrefab == null || (simulatorOnlyInEditor && !Application.isEditor))
            {
                Debug.LogWarning($"[XRPlatformSetup] No headset ({reason}) and no simulator for this build.");
                return;
            }
            Instantiate(simulatorPrefab).name = simulatorPrefab.name;
            Debug.Log($"[XRPlatformSetup] No headset ({reason}): XR Interaction Simulator started.");
        }
    }
}
