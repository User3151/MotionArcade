using UnityEngine;
using UnityEngine.Audio;

namespace MotionArcade.Feedback
{
    // מוזיקת הרקע של משחק: מתחילה ברגע שנכנסים למשחק (טעינת הסצנה), לא ב-START, ומתנגנת בלולאה עד שיוצאים.
    // גיא בחר בין כמה אפשרויות לכל משחק (ElevenLabs Music). מנותבת לקבוצה Music במיקסר, כך שסליידר המוזיקה שולט בה.
    // הלחץ בסוף הסבב הוא מ-CountdownTicks, לא מהמוזיקה.
    public class GameMusic : MonoBehaviour
    {
        [SerializeField] private AudioClip clip;
        [SerializeField] private AudioMixerGroup output;
        [SerializeField, Range(0f, 1f)] private float volume = 0.5f;
        [SerializeField] private float fadeIn = 1.5f;

        private AudioSource source;

        public AudioSource Source => source;

        private void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;     // מוזיקה, לא צליל במקום מסוים
            source.clip = clip;
            source.outputAudioMixerGroup = output;
            source.volume = 0f;
        }

        private void OnEnable()
        {
            if (clip != null) source.Play();
        }

        private void OnDisable() => source.Stop();

        private void Update()
        {
            if (source.volume < volume) source.volume = Mathf.MoveTowards(source.volume, volume, volume * Time.deltaTime / Mathf.Max(0.01f, fadeIn));
        }
    }
}
