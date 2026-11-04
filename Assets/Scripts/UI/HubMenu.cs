using UnityEngine;
using UnityEngine.UI;

namespace MotionArcade.UI
{
    // הלוח בנקודת ההתחלה של הארקייד: שם, הסבר קצר (ללכת לעיגול זוהר מול משחק), SETTINGS ו-EXIT.
    // אין כאן כפתורי משחקים: כל משחק עומד בפינה שלו ונפתח כשעומדים על העיגול שלו.
    public class HubMenu : MonoBehaviour
    {
        [SerializeField] private GameObject menuPanel;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button settingsBackButton;
        [SerializeField] private Button exitButton;

        private void Awake() => ShowMenu();

        private void OnEnable()
        {
            settingsButton.onClick.AddListener(ShowSettings);
            settingsBackButton.onClick.AddListener(ShowMenu);
            exitButton.onClick.AddListener(Exit);
        }

        private void OnDisable()
        {
            settingsButton.onClick.RemoveListener(ShowSettings);
            settingsBackButton.onClick.RemoveListener(ShowMenu);
            exitButton.onClick.RemoveListener(Exit);
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
