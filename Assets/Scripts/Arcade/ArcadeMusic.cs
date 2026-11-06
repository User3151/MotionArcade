using MotionArcade.Core;
using MotionArcade.Feedback;
using UnityEngine;

namespace MotionArcade.Arcade
{
    // המוזיקה ורעש הרקע של הארקייד (מכונות מצפצפות, מטבעות). מתנגנים במסך הפתיחה ובהליכה בחדר,
    // ונעלמים בהדרגה כשנכנסים למשחק (PLAY): שם המוזיקה של המשחק מחליפה אותם. חוזרים ב-EXIT.
    public class ArcadeMusic : MonoBehaviour
    {
        [SerializeField] private GameMusic music;
        [SerializeField] private GameMusic ambience;

        private bool inGame;

        private void Start() => Apply(StationPresence.Current != null, true);

        private void Update()
        {
            bool now = StationPresence.Current != null;
            if (now != inGame) Apply(now, false);
        }

        private void Apply(bool playing, bool force)
        {
            if (!force && playing == inGame) return;
            inGame = playing;
            if (music != null) music.SetAudible(!playing);
            if (ambience != null) ambience.SetAudible(!playing);
        }
    }
}
