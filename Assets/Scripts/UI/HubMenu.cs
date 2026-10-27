using System;
using MotionArcade.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MotionArcade.UI
{
    // הלוח באולם: כרטיס לכל משחק, SETTINGS ו-EXIT.
    // כשמשחק נטען הלוח נעלם (המשחק מופיע באותו מקום מול השחקן), וחוזר כשחוזרים לאולם.
    // משחק בלי סצנה עדיין מוצג, אבל כבוי, עם תווית SOON.
    public class HubMenu : MonoBehaviour
    {
        [Serializable]
        private class GameEntry
        {
            public Button button;
            public string sceneName;   // ריק = עוד לא קיים
        }

        [SerializeField] private GameObject menuPanel;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private GameEntry[] games;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button settingsBackButton;
        [SerializeField] private Button exitButton;

        private UnityAction[] gameHandlers;

        private void Awake()
        {
            // מטפל לכל כפתור, נוצר פעם אחת כדי שאפשר יהיה גם להסיר אותו ב-OnDisable
            gameHandlers = new UnityAction[games.Length];
            for (int i = 0; i < games.Length; i++)
            {
                string scene = games[i].sceneName;
                gameHandlers[i] = () => LoadGame(scene);
                games[i].button.interactable = !string.IsNullOrEmpty(scene);
            }
            ShowMenu();
        }

        private void OnEnable()
        {
            for (int i = 0; i < games.Length; i++) games[i].button.onClick.AddListener(gameHandlers[i]);
            settingsButton.onClick.AddListener(ShowSettings);
            settingsBackButton.onClick.AddListener(ShowMenu);
            exitButton.onClick.AddListener(Exit);
            if (SceneLoader.Instance != null)
            {
                SceneLoader.Instance.GameLoaded += HandleGameLoaded;
                SceneLoader.Instance.ReturnedToHub += ShowMenu;
            }
        }

        private void OnDisable()
        {
            for (int i = 0; i < games.Length; i++) games[i].button.onClick.RemoveListener(gameHandlers[i]);
            settingsButton.onClick.RemoveListener(ShowSettings);
            settingsBackButton.onClick.RemoveListener(ShowMenu);
            exitButton.onClick.RemoveListener(Exit);
            if (SceneLoader.Instance != null)
            {
                SceneLoader.Instance.GameLoaded -= HandleGameLoaded;
                SceneLoader.Instance.ReturnedToHub -= ShowMenu;
            }
        }

        private void LoadGame(string sceneName)
        {
            if (SceneLoader.Instance == null)
            {
                Debug.LogError("[HubMenu] No SceneLoader in the scene.", this);
                return;
            }
            SceneLoader.Instance.Load(sceneName);
        }

        private void HandleGameLoaded(string _)
        {
            menuPanel.SetActive(false);
            settingsPanel.SetActive(false);
        }

        private void ShowMenu()
        {
            settingsPanel.SetActive(false);
            menuPanel.SetActive(true);
        }

        private void ShowSettings()
        {
            menuPanel.SetActive(false);
            settingsPanel.SetActive(true);
        }

        private void Exit()
        {
            PlayerPrefs.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
