using System;
using MotionArcade.Feedback;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.UI;

namespace MotionArcade.Core
{
    // הכניסה למשחק ויציאה ממנו (גיא: "מסך עם PLAY, מסך שחור לשנייה, ואתה במשחק; EXIT מחזיר אותך למקום שעמדת").
    // מול כל משחק עומד קיוסק עם PLAY, ליד העיגול שאליו מתלפטרים. PLAY: מחשיכים, השחקן עובר לעמדת המשחק
    // (השורש של המשחק, מול המכונה), מופיע לוח ה-START והמוזיקה מתחילה. EXIT: אם יש סבב הוא נעצר בלי להירשם,
    // מחשיכים, והשחקן חוזר בדיוק למקום ולכיוון שבהם עמד כשלחץ PLAY.
    // בזמן משחק ההליכה, הסיבוב והטלפורט נעולים (LocomotionLock): יוצאים רק ב-EXIT, ולא בטעות.
    // משחק אחד בכל פעם: Current.
    public class StationPresence : MonoBehaviour
    {
        [SerializeField] private GameStation station;
        [SerializeField] private GameObject playScreen;          // הקיוסק עם PLAY
        [SerializeField] private Button playButton;
        [SerializeField] private GameObject[] showWhenPresent;   // לוח START / STOP / EXIT
        [SerializeField] private Button exitButton;
        [SerializeField] private GameMusic music;                // אופציונלי

        private Vector3 returnPosition;
        private Quaternion returnRotation;

        public static StationPresence Current { get; private set; }
        public bool Occupied { get; private set; }               // השחקן בתוך המשחק הזה
        public GameStation Station => station;
        public event Action<bool> OccupiedChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Current = null;

        private void OnEnable()
        {
            if (playButton != null) playButton.onClick.AddListener(Play);
            if (exitButton != null) exitButton.onClick.AddListener(Exit);
        }

        private void OnDisable()
        {
            if (playButton != null) playButton.onClick.RemoveListener(Play);
            if (exitButton != null) exitButton.onClick.RemoveListener(Exit);
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        private void Start() => Apply(false);

        public void Play()
        {
            if (Occupied || Current != null) return;
            XROrigin origin = FindFirstObjectByType<XROrigin>();
            if (origin == null) return;
            Action enter = () =>
            {
                returnPosition = origin.transform.position;
                returnRotation = origin.transform.rotation;
                // הראש מעל עמדת המשחק, מבט אל המכונה (+Z של המשחק)
                origin.MatchOriginUpCameraForward(Vector3.up, transform.forward);
                Vector3 head = origin.Camera.transform.position;
                origin.MoveCameraToWorldLocation(new Vector3(transform.position.x, head.y, transform.position.z));
                Physics.SyncTransforms();
                Apply(true);
            };
            if (ScreenFader.Instance == null || !ScreenFader.Instance.Transition(enter)) enter();
        }

        public void Exit()
        {
            if (!Occupied) return;
            if (station != null && station.IsRunning) station.AbortRound();
            XROrigin origin = FindFirstObjectByType<XROrigin>();
            Action leave = () =>
            {
                if (origin != null)
                {
                    origin.transform.SetPositionAndRotation(returnPosition, returnRotation);
                    Physics.SyncTransforms();
                }
                Apply(false);
            };
            if (ScreenFader.Instance == null || !ScreenFader.Instance.Transition(leave)) leave();
        }

        private void Apply(bool inside)
        {
            Occupied = inside;
            if (inside) Current = this;
            else if (Current == this) Current = null;
            if (playScreen != null) playScreen.SetActive(!inside);
            foreach (GameObject go in showWhenPresent) if (go != null) go.SetActive(inside);
            if (music != null) music.SetAudible(inside);
            OccupiedChanged?.Invoke(inside);
        }
    }
}
