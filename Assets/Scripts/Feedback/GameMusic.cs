using UnityEngine;
using UnityEngine.Audio;

namespace MotionArcade.Feedback
{
    // מוזיקת הרקע של משחק: מתחילה ברגע שנכנסים למשחק (טעינת הסצנה), לא ב-START, ומתנגנת עד שיוצאים.
    // גיא שמע את האפשרויות (ElevenLabs Music) ורצה את כולן: הקטעים של כל משחק מתנגנים אחד אחרי השני בסבב,
    // עם מעבר של 2 שניות, מקטע אקראי בכל כניסה. משחק עם קטע אחד מנגן אותו בלולאה.
    // מנותבת לקבוצה Music במיקסר. הלחץ בסוף הסבב הוא מ-CountdownTicks, לא מהמוזיקה.
    public class GameMusic : MonoBehaviour
    {
        [SerializeField] private AudioClip[] clips;
        [SerializeField] private AudioMixerGroup output;
        [SerializeField, Range(0f, 1f)] private float volume = 0.5f;
        [SerializeField] private float fadeIn = 1.5f;
        [SerializeField] private float crossfade = 2f;

        private readonly AudioSource[] sources = new AudioSource[2];   // שניים, כדי שהקטע הבא ייכנס בזמן שהקודם דועך
        private readonly float[] targets = new float[2];
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
            Begin(sources[0], clips[index]);
            targets[0] = volume;
            targets[1] = 0f;
        }

        private void OnDisable()
        {
            foreach (AudioSource source in sources) source.Stop();
        }

        private void Begin(AudioSource source, AudioClip clip)
        {
            source.clip = clip;
            source.loop = clips.Length == 1;
            source.volume = 0f;
            source.Play();
        }

        private void Update()
        {
            if (clips == null || clips.Length == 0) return;

            AudioSource current = sources[active];
            if (clips.Length > 1 && current.clip != null && current.time >= current.clip.length - crossfade)
            {
                index = Next(index, clips.Length);
                active = 1 - active;
                fadeSeconds = crossfade;
                Begin(sources[active], clips[index]);
                targets[active] = volume;
                targets[1 - active] = 0f;
            }

            float step = volume * Time.deltaTime / Mathf.Max(0.01f, fadeSeconds);
            for (int i = 0; i < sources.Length; i++)
            {
                AudioSource source = sources[i];
                source.volume = Mathf.MoveTowards(source.volume, targets[i], step);
                if (targets[i] <= 0f && source.volume <= 0f && source.isPlaying) source.Stop();
            }
        }
    }
}
