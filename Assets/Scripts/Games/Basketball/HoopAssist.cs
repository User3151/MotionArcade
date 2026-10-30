using MotionArcade.Interaction;
using UnityEngine;

namespace MotionArcade.Games.Basketball
{
    // עזרת כיוון לכדורסל (כמו ב-Just Hoops וב-VR Shoot Around). בלי משקל ביד, מהירות היד של גיא השתנתה ב-±15%
    // בין זריקות והכיוון ב-±6°, בזמן שסל דורש ±2% ו-±2°. העזרה מתקנת 75% מהשגיאה (ראו ThrowMath.Assist),
    // רק בזריקה שהולכת בערך לסל. מדידת הזריקות נשמרת תמיד על הזריקה המקורית (ThrowRelease.Thrown).
    public class HoopAssist : MonoBehaviour, IThrowAssist
    {
        [SerializeField] private Transform aim;                    // מרכז הטבעת
        [SerializeField, Range(0f, 1f)] private float strength = 0.75f;
        [SerializeField] private float maxYawError = 15f;          // מעלות
        [SerializeField] private float minSpeedRatio = 0.6f;
        [SerializeField] private float maxSpeedRatio = 1.6f;

        public float Strength
        {
            get => strength;
            set => strength = Mathf.Clamp01(value);
        }

        public Vector3 Adjust(Vector3 position, Vector3 velocity)
        {
            if (aim == null || !isActiveAndEnabled) return velocity;
            return ThrowMath.Assist(position, velocity, aim.position, -Physics.gravity.y, strength,
                                    maxYawError, minSpeedRatio, maxSpeedRatio, out _);
        }
    }
}
