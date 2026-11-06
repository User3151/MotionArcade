using MotionArcade.Core;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MotionArcade.UI
{
    // התפריט של הארקייד, על קנבס אחד עם שלושה עמודים (גיא, 2026-11-06):
    // - מסך פתיחה (Welcome to Motion Arcade): GET IN, SETTINGS, EXIT, ומה עושים בארקייד (העיגולים הכחולים).
    //   כל עוד הוא פתוח השחקן לא זז. GET IN: מסך שחור, והמסך נעלם.
    // - עצירה: כפתור התפריט בבקר (בכל רגע, גם באמצע משחק) פותח אותו מול הפנים ועוצר את הזמן.
    //   RESUME, SETTINGS, LEAVE GAME (רק בתוך משחק: כמו EXIT בלוח), MAIN MENU (חזרה למסך הפתיחה).
    //   לחיצה נוספת על הכפתור סוגרת.
    // - הגדרות (העוצמות): BACK חוזר לעמוד שפתח אותן.
    public class ArcadeMenu : MonoBehaviour
    {
        public enum Page { None, Title, Pause, Settings }

        [Header("Pages")]
        [SerializeField] private GameObject titlePage;
        [SerializeField] private GameObject pausePage;
        [SerializeField] private GameObject settingsPage;

        [Header("Title")]
        [SerializeField] private Button getInButton;
        [SerializeField] private Button titleSettingsButton;
        [SerializeField] private Button titleExitButton;

        [Header("Pause")]
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button pauseSettingsButton;
        [SerializeField] private Button leaveGameButton;
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private TMPro.TMP_Text pauseWhere;   // "In the arcade" / שם המשחק

        [Header("Settings")]
        [SerializeField] private Button settingsBackButton;

        [Header("Placement")]
        [SerializeField] private Transform startSpot;          // נקודת ההתחלה בארקייד: לשם MAIN MENU מחזיר
        [SerializeField] private float titleDistance = 1.5f;
        [SerializeField] private float titleScale = 0.0012f;
        [SerializeField] private float pauseDistance = 1.0f;
        [SerializeField] private float pauseScale = 0.0008f;
        [SerializeField] private bool showTitleOnStart = true;

        [Header("Input")]
        [SerializeField] private string[] menuBindings =
        {
            "<XRController>{LeftHand}/menuButton",
            "<XRController>{RightHand}/menuButton",
            "<XRController>{LeftHand}/secondaryButton",
            "<Keyboard>/escape",
        };

        private InputAction toggle;
        private Page page = Page.None;
        private Page settingsReturn = Page.Title;
        private float timeScaleBefore = 1f;

        public static ArcadeMenu Instance { get; private set; }
        public Page Current => page;
        // התפריט פתוח: השחקן לא זז (LocomotionLock)
        public static bool Blocking => Instance != null && Instance.page != Page.None;
        public bool Paused { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        private void Awake()
        {
            Instance = this;
            toggle = new InputAction("Arcade Menu", InputActionType.Button);
            foreach (string binding in menuBindings) toggle.AddBinding(binding);
        }

        private void OnEnable()
        {
            toggle.Enable();
            toggle.performed += HandleToggle;
            getInButton.onClick.AddListener(GetIn);
            titleSettingsButton.onClick.AddListener(OpenSettings);
            titleExitButton.onClick.AddListener(Quit);
            resumeButton.onClick.AddListener(Resume);
            pauseSettingsButton.onClick.AddListener(OpenSettings);
            leaveGameButton.onClick.AddListener(LeaveGame);
            mainMenuButton.onClick.AddListener(MainMenu);
            settingsBackButton.onClick.AddListener(CloseSettings);
        }

        private void OnDisable()
        {
            toggle.performed -= HandleToggle;
            toggle.Disable();
            getInButton.onClick.RemoveListener(GetIn);
            titleSettingsButton.onClick.RemoveListener(OpenSettings);
            titleExitButton.onClick.RemoveListener(Quit);
            resumeButton.onClick.RemoveListener(Resume);
            pauseSettingsButton.onClick.RemoveListener(OpenSettings);
            leaveGameButton.onClick.RemoveListener(LeaveGame);
            mainMenuButton.onClick.RemoveListener(MainMenu);
            settingsBackButton.onClick.RemoveListener(CloseSettings);
        }

        private void OnDestroy()
        {
            toggle?.Dispose();
            if (Paused) Time.timeScale = timeScaleBefore;
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            if (showTitleOnStart) ShowTitle();
            else Show(Page.None);
        }

        private void HandleToggle(InputAction.CallbackContext context)
        {
            if (page == Page.Title || (page == Page.Settings && settingsReturn == Page.Title)) return;   // במסך הפתיחה אין עצירה
            if (ScreenFader.Instance != null && ScreenFader.Instance.Busy) return;
            if (page == Page.None) OpenPause();
            else Resume();
        }

        // ===== מסך הפתיחה =====

        public void ShowTitle()
        {
            Unpause();
            PlaceInFrontOfStart();
            Show(Page.Title);
        }

        public void GetIn()
        {
            if (page != Page.Title) return;
            if (ScreenFader.Instance == null || !ScreenFader.Instance.Transition(() => Show(Page.None))) Show(Page.None);
        }

        // ===== עצירה =====

        public void OpenPause()
        {
            PlaceInFrontOfHead(pauseDistance, pauseScale);
            bool inGame = StationPresence.Current != null;
            leaveGameButton.gameObject.SetActive(inGame);
            if (pauseWhere != null) pauseWhere.text = inGame && StationPresence.Current.Station != null
                ? "Playing: " + StationPresence.Current.Station.DisplayName
                : "In the arcade";
            if (!Paused)
            {
                timeScaleBefore = Time.timeScale > 0f ? Time.timeScale : 1f;
                Time.timeScale = 0f;   // הסבב, הפאק, הכדורים והשעון עוצרים
                Paused = true;
            }
            Show(Page.Pause);
        }

        public void Resume()
        {
            Unpause();
            Show(Page.None);
        }

        public void LeaveGame()
        {
            Resume();
            if (StationPresence.Current != null) StationPresence.Current.Exit();
        }

        public void MainMenu()
        {
            Unpause();
            Show(Page.None);
            void Back()
            {
                StationPresence.LeaveCurrentNow();
                XROrigin origin = FindFirstObjectByType<XROrigin>();
                if (origin != null && startSpot != null)
                {
                    origin.transform.SetPositionAndRotation(startSpot.position, Quaternion.Euler(0f, startSpot.eulerAngles.y, 0f));
                    Physics.SyncTransforms();
                }
                ShowTitle();
            }
            if (ScreenFader.Instance == null || !ScreenFader.Instance.Transition(Back)) Back();
        }

        // ===== הגדרות =====

        public void OpenSettings()
        {
            if (page == Page.Title || page == Page.Pause) settingsReturn = page;
            Show(Page.Settings);
        }

        public void CloseSettings() => Show(settingsReturn);

        public void Quit()
        {
            PlayerPrefs.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ===== עזר =====

        private void Unpause()
        {
            if (!Paused) return;
            Time.timeScale = timeScaleBefore;
            Paused = false;
        }

        private void Show(Page next)
        {
            page = next;
            titlePage.SetActive(next == Page.Title);
            pausePage.SetActive(next == Page.Pause);
            settingsPage.SetActive(next == Page.Settings);
        }

        // מסך הפתיחה: מול נקודת ההתחלה, בגובה העיניים, ישר מול הפנים
        private void PlaceInFrontOfStart()
        {
            Camera head = Camera.main;
            if (startSpot == null) return;
            Vector3 forward = Vector3.ProjectOnPlane(startSpot.forward, Vector3.up).normalized;
            float eye = EyeHeight(head);
            Vector3 position = startSpot.position + forward * titleDistance + Vector3.up * (eye - 0.05f);
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward, Vector3.up));
            transform.localScale = Vector3.one * titleScale;
        }

        // גובה העיניים מעל הרצפה; לפני שהמעקב התחיל (הראש עוד ב-0) מניחים 1.6
        private float EyeHeight(Camera head)
        {
            if (head == null || startSpot == null) return 1.6f;
            float y = head.transform.position.y - startSpot.position.y;
            return y > 0.9f && y < 2.2f ? y : 1.6f;
        }

        // מסך הפתיחה עולה ויורד עם הראש (יושבים / עומדים, ובשניות הראשונות של המעקב)
        private void Update()
        {
            if (page != Page.Title || startSpot == null) return;
            float y = startSpot.position.y + EyeHeight(Camera.main) - 0.05f;
            Vector3 p = transform.position;
            if (Mathf.Abs(p.y - y) > 0.01f) transform.position = new Vector3(p.x, Mathf.MoveTowards(p.y, y, Time.unscaledDeltaTime * 0.8f), p.z);
        }

        // תפריט העצירה: מול הראש, לכיוון שהשחקן מסתכל (רק סביב הציר האנכי), קצת מתחת לגובה העיניים
        private void PlaceInFrontOfHead(float distance, float scale)
        {
            Camera head = Camera.main;
            if (head == null) return;
            Vector3 forward = Vector3.ProjectOnPlane(head.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.ProjectOnPlane(head.transform.up, Vector3.up);
            forward.Normalize();
            Vector3 position = head.transform.position + forward * distance + Vector3.down * 0.08f;
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(position - head.transform.position, Vector3.up));
            transform.localScale = Vector3.one * scale;
        }
    }
}
