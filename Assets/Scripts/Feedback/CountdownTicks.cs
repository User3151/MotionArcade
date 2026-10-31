using MotionArcade.Core;
using UnityEngine;
using UnityEngine.Audio;

namespace MotionArcade.Feedback
{
    // מתי לתקתק: בכל שנייה שלמה של העשר האחרונות. בלי Unity מעבר ל-Mathf, לבדיקות.
    public static class Countdown
    {
        public const float Seconds = 10f;

        // מספר השנייה (10 עד 1), או 0 = אין תקתוק
        public static int TickSecond(float timeLeft)
        {
            int second = Mathf.CeilToInt(timeLeft);
            return second >= 1 && second <= Seconds ? second : 0;
        }

        // 0 בשנייה העשירית, 1 בשנייה האחרונה
        public static float Urgency(int second) => Mathf.Clamp01((Seconds - second) / (Seconds - 1f));
    }

    // ספירה לאחור קולית בסוף הסבב: תקתוק שעון בכל שנייה מהעשר האחרונות, גבוה וחזק יותר ככל שמתקרבים לאפס.
    // גיא: זה מה שיוצר את הלחץ, לא המוזיקה. מנותב לקבוצה SFX (סליידר האפקטים).
    public class CountdownTicks : MonoBehaviour
    {
        [SerializeField] private GameStation station;
        [SerializeField] private AudioClip tick;
        [SerializeField] private AudioMixerGroup output;
        [SerializeField, Range(0f, 1f)] private float volume = 0.7f;

        private AudioSource source;
        private int lastTick;

        private void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.outputAudioMixerGroup = output;
        }

        private void Update()
        {
            if (station.Phase != RoundPhase.Playing)
            {
                lastTick = 0;
                return;
            }
            int second = Countdown.TickSecond(station.TimeLeft);
            if (second == 0 || second == lastTick || tick == null) return;
            lastTick = second;
            float urgency = Countdown.Urgency(second);
            source.pitch = 1f + 0.5f * urgency;
            source.PlayOneShot(tick, volume * (0.6f + 0.4f * urgency));
        }
    }
}
