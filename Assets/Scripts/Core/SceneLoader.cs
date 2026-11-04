using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MotionArcade.Core
{
    // הארקייד: האולם (הריג, הסשן, התאורה) והמשחקים, כל אחד בפינה שלו. כל המשחקים נטענים פעם אחת בהתחלה,
    // additive, ונשארים טעונים: השחקן רואה אותם כל הזמן והולך אליהם. משחק נפתח כשעומדים על העיגול שלו
    // (StationPresence), לא בטעינה.
    // רץ לפני שאר הסקריפטים בסצנה, כדי ש-Instance כבר קיים כשהם נרשמים לאירועים שלו ב-OnEnable
    [DefaultExecutionOrder(-500)]
    public class SceneLoader : MonoBehaviour
    {
        [SerializeField] private string[] games =
            { "Game_Basketball", "Game_Darts", "Game_Whack", "Game_BuzzWire" };

        public static SceneLoader Instance { get; private set; }

        public bool IsBusy { get; private set; }          // בזמן הטעינה הראשונה
        public bool AllLoaded { get; private set; }
        public string[] Games => games;

        public event Action<string> GameLoaded;

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

        private void Start() => StartCoroutine(LoadAll());

        public bool IsLoaded(string sceneName) => SceneManager.GetSceneByName(sceneName).isLoaded;

        private IEnumerator LoadAll()
        {
            IsBusy = true;
            foreach (string game in games)
            {
                // ב-Editor אפשר לפתוח משחק ביד לפני Play: לא טוענים אותו פעמיים
                if (IsLoaded(game)) { GameLoaded?.Invoke(game); continue; }
                if (!Application.CanStreamedLevelBeLoaded(game))
                {
                    Debug.LogError($"[SceneLoader] '{game}' is not in Build Settings.", this);
                    continue;
                }
                // האולם נשאר הסצנה הפעילה: ממנו מגיעים ה-skybox, ה-ambient וה-light probes
                yield return SceneManager.LoadSceneAsync(game, LoadSceneMode.Additive);
                GameLoaded?.Invoke(game);
            }
            AllLoaded = true;
            IsBusy = false;
        }
    }
}
