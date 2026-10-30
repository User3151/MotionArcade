using MotionArcade.Core;
using UnityEngine;
using UnityEngine.Audio;

namespace MotionArcade.Feedback
{
    // כמה מכל שכבה מתנגן בכל רגע של הסבב, ובאיזו מהירות. בלי Unity מעבר ל-Mathf, לבדיקות.
    // גיא: "ככל שהטיימר מתקרב לסוף, המוזיקה יותר מותחת ומלחיצה". השכבות נכנסות לפי החלק מהסבב שעבר,
    // כדי שזה יעבוד גם בסבב של 60 וגם של 120 שניות. השכבה האחרונה והתקתוק לפי שניות: 10 השניות האחרונות.
    public static class TensionCurve
    {
        public const int Layers = 4;               // בסיס, דחף, כלי קשת, סוף
        public const float FinalSeconds = 10f;
        public const float SpeedUpSeconds = 15f;
        public const float MaxSpeedUp = 0.08f;     // 8% מהר יותר בשנייה האחרונה (וגם חצי טון וקצת גבוה יותר)

        public static float Layer(int layer, float timeLeft, float roundSeconds)
        {
            float elapsed = roundSeconds > 0f ? Mathf.Clamp01(1f - timeLeft / roundSeconds) : 1f;
            switch (layer)
            {
                case 0: return 1f;
                case 1: return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.2f, 0.35f, elapsed));
                case 2: return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 0.7f, elapsed));
                case 3: return timeLeft <= FinalSeconds ? 1f : 0f;
                default: return 0f;
            }
        }

        public static float Pitch(float timeLeft) => 1f + MaxSpeedUp * (1f - Mathf.Clamp01(timeLeft / SpeedUpSeconds));

        // תקתוק בכל שנייה שלמה של 10 האחרונות: 0 = אין. מחזיר את מספר השנייה (10 עד 1).
        public static int TickSecond(float timeLeft)
        {
            int second = Mathf.CeilToInt(timeLeft);
            return second >= 1 && second <= FinalSeconds ? second : 0;
        }
    }

    // מוזיקה שמתחילה עם הסבב ונהיית מותחת ככל שהזמן אוזל: ארבע שכבות של אותה לופ, שמתחילות יחד באותו רגע
    // (PlayScheduled) ולכן נשארות מסונכרנות, וכל אחת עולה לפי TensionCurve. ב-15 השניות האחרונות הכל מואץ,
    // וב-10 האחרונות יש תקתוק שעון שעולה בגובה ובעוצמה. נעצרת בדעיכה קצרה בסוף הסבב או ב-STOP.
    // המקורות נוצרים ב-Awake (בטעינת הסצנה), מנותבים לקבוצה Music במיקסר, כך שסליידר המוזיקה שולט בהם.
    public class TensionMusic : MonoBehaviour
    {
        [SerializeField] private GameStation station;
        [SerializeField] private AudioClip[] layers = new AudioClip[TensionCurve.Layers];
        [SerializeField] private AudioClip tick;
        [SerializeField] private AudioMixerGroup output;
        [SerializeField, Range(0f, 1f)] private float volume = 0.7f;
        [SerializeField, Range(0f, 1f)] private float tickVolume = 0.6f;
        [SerializeField] private float layerFade = 1.5f;    // שניות לשכבה להיכנס במלואה
        [SerializeField] private float stopFade = 0.5f;

        private AudioSource[] sources;
        private AudioSource tickSource;
        private float[] levels;
        private float master;
        private bool playing;
        private bool stopping;
        private int lastTick;

        public bool IsPlaying => playing;
        public float Level(int layer) => levels[layer];
        public float CurrentPitch => sources.Length > 0 ? sources[0].pitch : 1f;

        private void Awake()
        {
            sources = new AudioSource[layers.Length];
            levels = new float[layers.Length];
            for (int i = 0; i < layers.Length; i++)
            {
                sources[i] = CreateSource("Music Layer " + (i + 1), layers[i]);
                sources[i].loop = true;
            }
            tickSource = CreateSource("Clock Tick", tick);
        }

        private AudioSource CreateSource(string sourceName, AudioClip clip)
        {
            var go = new GameObject(sourceName);
            go.transform.SetParent(transform, false);
            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;     // מוזיקה, לא צליל במקום מסוים
            source.clip = clip;
            source.outputAudioMixerGroup = output;
            source.volume = 0f;
            return source;
        }

        private void OnEnable() => station.PhaseChanged += HandlePhase;

        private void OnDisable()
        {
            station.PhaseChanged -= HandlePhase;
            StopNow();
        }

        private void HandlePhase(RoundPhase phase)
        {
            if (phase == RoundPhase.Playing) Begin();
            else if (playing) stopping = true;
        }

        private void Begin()
        {
            // כל השכבות באותו רגע של שעון האודיו: התחלה ב-Play רגיל הייתה מפזרת אותן בין פריימים
            double at = AudioSettings.dspTime + 0.05;
            for (int i = 0; i < sources.Length; i++)
            {
                levels[i] = 0f;
                sources[i].pitch = 1f;
                sources[i].volume = 0f;
                if (sources[i].clip != null) sources[i].PlayScheduled(at);
            }
            master = 1f;
            playing = true;
            stopping = false;
            lastTick = 0;
        }

        private void Update()
        {
            if (!playing) return;
            float dt = Time.deltaTime;

            if (stopping)
            {
                master -= dt / Mathf.Max(0.01f, stopFade);
                if (master <= 0f)
                {
                    StopNow();
                    return;
                }
            }
            else
            {
                float timeLeft = station.TimeLeft;
                float round = station.RoundSeconds;
                float pitch = TensionCurve.Pitch(timeLeft);
                for (int i = 0; i < sources.Length; i++)
                {
                    // שכבה ראשונה נכנסת מיד, השאר בהדרגה; השכבה האחרונה נכנסת מהר (רבע מהזמן), כמו אזעקה
                    float fade = i == 0 ? 0.05f : i == sources.Length - 1 ? layerFade * 0.25f : layerFade;
                    levels[i] = Mathf.MoveTowards(levels[i], TensionCurve.Layer(i, timeLeft, round), dt / fade);
                    sources[i].pitch = pitch;
                }

                int second = TensionCurve.TickSecond(timeLeft);
                if (second != 0 && second != lastTick && tick != null)
                {
                    lastTick = second;
                    float urgency = (TensionCurve.FinalSeconds - second) / (TensionCurve.FinalSeconds - 1f);   // 0 ב-10, 1 ב-1
                    tickSource.pitch = 1f + 0.5f * urgency;
                    tickSource.PlayOneShot(tick, tickVolume * (0.6f + 0.4f * urgency));
                }
            }

            for (int i = 0; i < sources.Length; i++) sources[i].volume = levels[i] * volume * master;
        }

        private void StopNow()
        {
            playing = false;
            stopping = false;
            if (sources == null) return;
            foreach (AudioSource source in sources)
            {
                source.Stop();
                source.volume = 0f;
            }
        }
    }
}
