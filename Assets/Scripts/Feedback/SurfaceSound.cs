using UnityEngine;

namespace MotionArcade.Feedback
{
    // איזה צליל משטח משמיע כשחפץ עם ImpactSound פוגע בו (טבעת: צלצול מתכתי, לוח: חבטה). בלי זה ImpactSound
    // משמיע את הצליל של החפץ עצמו (כדור על רצפה). part מסמן את חלקי הסל, כדי שהכדורסל ידע אם סל היה נקי.
    public class SurfaceSound : MonoBehaviour
    {
        public enum Part { Other, Rim, Backboard }

        [SerializeField] private AudioClip[] clips;
        [SerializeField] private Part part = Part.Other;
        [SerializeField, Range(0f, 2f)] private float volume = 1f;

        public Part Kind => part;
        public float Volume => volume;

        public AudioClip Pick() => clips == null || clips.Length == 0 ? null : clips[Random.Range(0, clips.Length)];
    }
}
