using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MotionArcade.Core
{
    // טוען משחק מעל האולם (additive) ופורק אותו בחזרה. האולם, הריג, הסשן והתאורה נשארים טעונים כל הזמן,
    // אז אין מסך שחור בין משחקים והשחקן לא "קופץ" למקום אחר.
    // משחק אחד בכל פעם: טעינה של משחק חדש פורקת קודם את הקודם.
    // רץ לפני שאר הסקריפטים בסצנה, כדי ש-Instance כבר קיים כשהם נרשמים לאירועים שלו ב-OnEnable
    [DefaultExecutionOrder(-500)]
    public class SceneLoader : MonoBehaviour
    {
        public static SceneLoader Instance { get; private set; }

        public string CurrentGame { get; private set; }   // ריק = באולם
        public bool IsBusy { get; private set; }           // בזמן טעינה או פריקה לחיצות נוספות לא עושות כלום

        public event Action<string> GameLoaded;
        public event Action ReturnedToHub;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[SceneLoader] A second copy was destroyed.", this);
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public bool Load(string sceneName)
        {
            if (IsBusy || string.IsNullOrEmpty(sceneName) || sceneName == CurrentGame) return false;
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"[SceneLoader] '{sceneName}' is not in Build Settings.", this);
                return false;
            }
            StartCoroutine(Switch(sceneName));
            return true;
        }

        public bool ReturnToHub()
        {
            if (IsBusy || string.IsNullOrEmpty(CurrentGame)) return false;
            StartCoroutine(Switch(null));
            return true;
        }

        private IEnumerator Switch(string next)
        {
            IsBusy = true;

            if (!string.IsNullOrEmpty(CurrentGame))
            {
                AsyncOperation unload = SceneManager.UnloadSceneAsync(CurrentGame);
                if (unload != null) yield return unload;
                CurrentGame = null;
                if (next == null) ReturnedToHub?.Invoke();
            }

            if (next != null)
            {
                AsyncOperation load = SceneManager.LoadSceneAsync(next, LoadSceneMode.Additive);
                yield return load;
                CurrentGame = next;
                // האולם נשאר הסצנה הפעילה: ממנו מגיעים ה-skybox, ה-ambient וה-light probes.
                // אובייקטים שהמשחק יוצר בזמן ריצה נכנסים לסצנה הפעילה, אז משחק שיוצר אובייקטים
                // צריך לשים אותם תחת אובייקט בסצנה שלו, כדי שיפורקו איתה.
                GameLoaded?.Invoke(next);
            }

            IsBusy = false;
        }
    }
}
