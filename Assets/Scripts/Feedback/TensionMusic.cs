using MotionArcade.Core;
using UnityEngine;
using UnityEngine.Audio;

namespace MotionArcade.Feedback
{
    // כמה מכל שלב של המוזיקה מתנגן בכל רגע של הסבב, ובאיזו מהירות. בלי Unity מעבר ל-Mathf, לבדיקות.
    // גיא: "ככל שהטיימר מתקרב לסוף, המוזיקה יותר מותחת ומלחיצה". שלושה שלבים: התחלה, בנייה, סוף.
    // הבנייה נכנסת לפי החלק מהסבב שעבר (25%-40%), כדי שזה יעבוד גם בסבב של 60 וגם של 120 שניות;
    // הסוף לפי שניות: 15 השניות האחרונות. התקתוק בעשר האחרונות.
    public static class TensionCurve
    {
        public const int MaxStages = 3;
        public const float FinalStageSeconds = 15f;
        public const float TickSeconds = 10f;
        public const float SpeedUpSeconds = 15f;
        public const float MaxSpeedUp = 0.06f;     // 6% מהר יותר בשנייה האחרונה

        // עוצמה (0-1) של שלב stage מתוך stages. הלופים לא מסונכרנים זה לזה, אז המעבר שומר על אנרגיה (שורש
        // המשקל): במעבר ליניארי בין שני שירים שונים העוצמה הייתה צונחת באמצע.
        public static float Stage(int stage, int stages, float timeLeft, float roundSeconds)
        {
            if (stages <= 1) return stage == 0 ? 1f : 0f;
            float elapsed = roundSeconds > 0f ? Mathf.Clamp01(1f - timeLeft / roundSeconds) : 1f;
            float build = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, 0.4f, elapsed));
            float final = stages >= 3
                ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(FinalStageSeconds + 2f, FinalStageSeconds, timeLeft))
                : 0f;
            float weight;
            switch (stage)
            {
                case 0: weight = (1f - build) * (1f - final); break;
                case 1: weight = build * (1f - final); break;
                case 2: weight = final; break;
                default: weight = 0f; break;
            }
            return Mathf.Sqrt(weight);
        }

        public static float Pitch(float timeLeft) => 1f + MaxSpeedUp * (1f - Mathf.Clamp01(timeLeft / SpeedUpSeconds));

        // תקתוק בכל שנייה שלמה של 10 האחרונות: 0 = אין. מחזיר את מספר השנייה (10 עד 1).
        public static int TickSecond(float timeLeft)
        {
            int second = Mathf.CeilToInt(timeLeft);
            return second >= 1 && second <= TickSeconds ? second : 0;
        }
    }

    // מוזיקה לסבב. במשחקים עם לחץ זמן (כדורסל, חפרפרות, Buzz Wire): שלושה לופים של ElevenLabs, אחד לכל שלב,
    // שעוברים מאחד לשני לפי TensionCurve ככל שהזמן אוזל. ב-15 השניות האחרונות הכל גם מואץ קצת, ובעשר האחרונות
    // יש תקתוק שעון שעולה בגובה ובעוצמה. בחצים: לופ אחד רגוע, בלי תקתוק ובלי האצה.
    // מתחילה עם הסבב, ונעצרת בדעיכה קצרה בסוף הסבב או ב-STOP. המקורות נוצרים ב-Awake (בטעינת הסצנה), מנותבים
    // לקבוצה Music במיקסר, כך שסליידר המוזיקה שולט בהם.
    public class TensionMusic : MonoBehaviour
    {
        [SerializeField] private GameStation station;
        [SerializeField] private AudioClip[] stages = new AudioClip[TensionCurve.MaxStages];
        [SerializeField] private AudioClip tick;
        [SerializeField] private AudioMixerGroup output;
        [SerializeField] private bool clockTicks = true;
        [SerializeField] private bool speedUp = true;
        [SerializeField, Range(0f, 1f)] private float volume = 0.6f;
        [SerializeField, Range(0f, 1f)] private float tickVolume = 0.6f;
        [SerializeField] private float stopFade = 0.6f;

        private AudioSource[] sources;
        private AudioSource tickSource;
        private float[] levels;
        private float master;
        private bool playing;
        private bool stopping;
        private int lastTick;

        public bool IsPlaying => playing;
        public int StageCount => sources.Length;
        public float Level(int stage) => levels[stage];
        public float CurrentPitch => sources.Length > 0 ? sources[0].pitch : 1f;

        private void Awake()
        {
            sources = new AudioSource[stages.Length];
            levels = new float[stages.Length];
            for (int i = 0; i < stages.Length; i++)
            {
                sources[i] = CreateSource("Music Stage " + (i + 1), stages[i]);
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
            // כל השלבים מתחילים יחד מהתחלת הלופ, בשקט, ורק העוצמות שלהם משתנות
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
                float pitch = speedUp ? TensionCurve.Pitch(timeLeft) : 1f;
                for (int i = 0; i < sources.Length; i++)
                {
                    // העקומה עצמה כבר הדרגתית; כאן רק כדי שההתחלה לא תקפוץ מ-0
                    levels[i] = Mathf.MoveTowards(levels[i], TensionCurve.Stage(i, sources.Length, timeLeft, round), dt / 0.3f);
                    sources[i].pitch = pitch;
                }

                int second = clockTicks ? TensionCurve.TickSecond(timeLeft) : 0;
                if (second != 0 && second != lastTick && tick != null)
                {
                    lastTick = second;
                    float urgency = (TensionCurve.TickSeconds - second) / (TensionCurve.TickSeconds - 1f);   // 0 ב-10, 1 ב-1
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
