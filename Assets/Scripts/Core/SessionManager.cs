using System;
using UnityEngine;

namespace MotionArcade.Core
{
    // מחזיק את הסשן לאורך כל הביקור באולם. יושב ב-Hub, שלא נפרק אף פעם,
    // והמשחקים (שנטענים ונפרקים) מדווחים אליו כשסבב נגמר.
    // בשלב 6 הוא יחזיק גם את פרופיל הכיול ויכתוב את הדוח.
    // רץ לפני שאר הסקריפטים בסצנה, כדי ש-Instance כבר קיים כשהם נרשמים לאירועים שלו ב-OnEnable
    [DefaultExecutionOrder(-500)]
    public class SessionManager : MonoBehaviour
    {
        public static SessionManager Instance { get; private set; }

        public SessionLog Log { get; } = new SessionLog();

        // לוחות תוצאות ולוח השיאים באולם מאזינים לזה
        public event Action<RoundResult> RoundRecorded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[SessionManager] A second copy was destroyed.", this);
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Record(RoundResult result)
        {
            Log.Add(result);
            RoundRecorded?.Invoke(result);
        }
    }
}
