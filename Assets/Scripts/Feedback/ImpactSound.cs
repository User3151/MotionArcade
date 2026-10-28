using UnityEngine;

namespace MotionArcade.Feedback
{
    // צליל של נפילה או מכה: ככל שההתנגשות מהירה יותר, הצליל חזק יותר (מועתק מ-The Lab).
    // שם על חפץ עם Rigidbody, יחד עם AudioSource שמנותב לקבוצה SFX במיקסר.
    [RequireComponent(typeof(AudioSource))]
    public class ImpactSound : MonoBehaviour
    {
        [SerializeField] private AudioClip clip;
        [SerializeField] private float minSpeed = 0.7f;    // מתחת לזה לא משמיעים כלום (חפץ שנח או מתגלגל)
        [SerializeField] private float fullSpeed = 4f;     // מהירות שבה הצליל בעוצמה מלאה
        [SerializeField] private float cooldown = 0.12f;   // כדי שקפיצות קטנות לא ייצרו צרור צלילים

        private AudioSource source;
        private float nextAllowedTime;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (clip == null) return;
            float speed = collision.relativeVelocity.magnitude;
            if (speed < minSpeed || Time.time < nextAllowedTime) return;

            nextAllowedTime = Time.time + cooldown;
            source.pitch = Random.Range(0.92f, 1.08f);
            source.PlayOneShot(clip, Mathf.Clamp01(speed / fullSpeed));
        }
    }
}
