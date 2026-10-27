using System;
using UnityEngine;
using UnityEngine.Audio;

namespace MotionArcade.Core
{
    public enum VolumeChannel { Master, Music, Sfx }

    // עוצמות השמע: נשמרות ב-PlayerPrefs ומופעלות על ה-AudioMixer.
    // כל AudioSource בפרויקט מנותב לקבוצה Music או SFX במיקסר, ולכן אין צורך לרשום מקורות כאן
    // (כמו שהיה ב-The Lab): צליל חדש פשוט מקבל קבוצה ב-Inspector.
    // רץ לפני שאר הסקריפטים בסצנה, כדי ש-Instance כבר קיים כשהם נרשמים לאירועים שלו ב-OnEnable
    [DefaultExecutionOrder(-500)]
    public class VolumeSettings : MonoBehaviour
    {
        // שמות הפרמטרים שחשופים במיקסר (Exposed Parameters)
        public const string MasterParameter = "MasterVolume";
        public const string MusicParameter = "MusicVolume";
        public const string SfxParameter = "SfxVolume";

        public const float MinDecibels = -80f;   // השקט של המיקסר

        public static VolumeSettings Instance { get; private set; }

        [SerializeField] private AudioMixer mixer;

        public event Action<VolumeChannel, float> Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[VolumeSettings] A second copy was destroyed.", this);
                Destroy(this);
                return;
            }
            Instance = this;
            if (mixer == null) Debug.LogError("[VolumeSettings] No AudioMixer assigned.", this);
        }

        // ב-Start ולא ב-Awake: AudioMixer.SetFloat שנקרא ב-Awake של הסצנה הראשונה לא תמיד נקלט
        private void Start()
        {
            foreach (VolumeChannel channel in (VolumeChannel[])Enum.GetValues(typeof(VolumeChannel)))
                ApplyToMixer(channel, Get(channel));
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // 0..1 כמו שהסליידר מציג
        public float Get(VolumeChannel channel) => Mathf.Clamp01(PlayerPrefs.GetFloat(Key(channel), 1f));

        public void Set(VolumeChannel channel, float linear)
        {
            linear = Mathf.Clamp01(linear);
            PlayerPrefs.SetFloat(Key(channel), linear);
            ApplyToMixer(channel, linear);
            Changed?.Invoke(channel, linear);
        }

        // הכתיבה לדיסק נעשית כשסוגרים את ההגדרות, לא בכל תזוזה של הסליידר
        public void Save() => PlayerPrefs.Save();

        private void ApplyToMixer(VolumeChannel channel, float linear)
        {
            if (mixer == null) return;
            if (!mixer.SetFloat(Parameter(channel), ToDecibels(linear)))
                Debug.LogError($"[VolumeSettings] The mixer has no exposed parameter '{Parameter(channel)}'.", this);
        }

        // האוזן שומעת עוצמה בלוגריתם: חצי סליידר צריך להישמע כחצי, לא כמעט כמו מלא.
        // 1 = 0dB (בלי שינוי), 0.5 = ‎-6dB, 0.1 = ‎-20dB, 0 = שקט מוחלט.
        public static float ToDecibels(float linear)
        {
            if (linear <= 0.0001f) return MinDecibels;
            return Mathf.Max(MinDecibels, 20f * Mathf.Log10(linear));
        }

        public static string Key(VolumeChannel channel) => "volume." + channel.ToString().ToLowerInvariant();

        public static string Parameter(VolumeChannel channel)
        {
            switch (channel)
            {
                case VolumeChannel.Music: return MusicParameter;
                case VolumeChannel.Sfx: return SfxParameter;
                default: return MasterParameter;
            }
        }
    }
}
