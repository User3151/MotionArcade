using UnityEngine;
using UnityEngine.Audio;

namespace MotionArcade.Feedback
{
    // מוזיקת הרקע של משחק: מתחילה ברגע שנכנסים למשחק (טעינת הסצנה), לא ב-START, ומתנגנת עד שיוצאים.
    // גיא שמע את האפשרויות (ElevenLabs Music) ורצה את כולן: הקטעים של כל משחק מתנגנים אחד אחרי השני בסבב,
    // עם מעבר של 2 שניות, מקטע אקראי בכל כניסה. משחק עם קטע אחד מנגן אותו בלולאה.
    // מנותבת לקבוצה Music במיקסר. הלחץ בסוף הסבב הוא מ-CountdownTicks, לא מהמוזיקה.
    // בארקייד כל המשחקים טעונים יחד: עם waitForPlayer המוזיקה מחכה ל-StationPresence (השחקן על העיגול),
    // נכנסת ויוצאת בדעיכה, ובזמן שאף אחד לא שם היא מושהית (לא מנגנת בשקט ולא מבזבזת stream).
    public class GameMusic : MonoBehaviour
    {
        [SerializeField] private AudioClip[] clips;
        [SerializeField] private AudioMixerGroup output;
        [SerializeField, Range(0f, 1f)] private float volume = 0.5f;
        [SerializeField] private float fadeIn = 1.5f;
        [SerializeField] private float crossfade = 2f;
        [SerializeField] private bool waitForPlayer;
        [SerializeField] private float presenceFade = 1f;

        private readonly AudioSource[] sources = new AudioSource[2];   // שניים, כדי שהקטע הבא ייכנס בזמן שהקודם דועך
        private readonly float[] targets = new float[2];
        private readonly float[] levels = new float[2];   // עוצמת כל קטע לפני ה-gain של הנוכחות
        private float gain = 1f;
        private float gainTarget = 1f;
        private bool paused;
        private int active;
        private int index;
        private float fadeSeconds;

        public int Index => index;
        public AudioClip Playing => sources[active] != null ? sources[active].clip : null;

        public static int Next(int current, int count) => count <= 0 ? 0 : (current + 1) % count;

        private void Awake()
        {
            for (int i = 0; i < sources.Length; i++)
            {
                AudioSource source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;     // מוזיקה, לא צליל במקום מסוים
                source.outputAudioMixerGroup = output;
                source.volume = 0f;
                sources[i] = source;
            }
        }

        private void OnEnable()
        {
            if (clips == null || clips.Length == 0) return;
            index = Random.Range(0, clips.Length);
            active = 0;
            fadeSeconds = fadeIn;
            gain = gainTarget = waitForPlayer ? 0f : 1f;
            Begin(0, clips[index]);
            targets[0] = volume;
            targets[1] = 0f;
            if (waitForPlayer) Pause();
        }

        // StationPresence: השחקן עלה על העיגול (true) או ירד ממנו (false)
        public void SetAudible(bool audible)
        {
            gainTarget = audible ? 1f : 0f;
            if (audible && paused)
            {
                foreach (AudioSource source in sources) if (source.clip != null) source.UnPause();
                paused = false;
            }
        }

        public bool Audible => gainTarget > 0f;

        private void Pause()
        {
            foreach (AudioSource source in sources) source.Pause();
            paused = true;
        }

        private void OnDisable()
        {
            foreach (AudioSource source in sources) source.Stop();
        }

        private void Begin(int slot, AudioClip clip)
        {
            AudioSource source = sources[slot];
            levels[slot] = 0f;
            source.clip = clip;
            // גם שיר יחיד לא בלולאה של Unity: הסוף עובר להתחלה ב-crossfade (הקבצים כבר לא ממוזגים בהתחלה,
            // גיא שמע שם "סאונדים אחד על השני")
            source.loop = false;
            source.volume = 0f;
            source.Play();
        }

        private void Update()
        {
            if (clips == null || clips.Length == 0 || paused) return;

            gain = Mathf.MoveTowards(gain, gainTarget, Time.deltaTime / Mathf.Max(0.01f, presenceFade));
            AudioSource current = sources[active];
            if (current.clip != null && current.time >= current.clip.length - crossfade)
            {
                index = Next(index, clips.Length);
                active = 1 - active;
                fadeSeconds = crossfade;
                Begin(active, clips[index]);
                targets[active] = volume;
                targets[1 - active] = 0f;
            }

            float step = volume * Time.deltaTime / Mathf.Max(0.01f, fadeSeconds);
            for (int i = 0; i < sources.Length; i++)
            {
                AudioSource source = sources[i];
                levels[i] = Mathf.MoveTowards(levels[i], targets[i], step);
                source.volume = levels[i] * gain;
                if (targets[i] <= 0f && levels[i] <= 0f && source.isPlaying) source.Stop();
            }
            if (gainTarget <= 0f && gain <= 0f) Pause();
        }
    }
}
