using MotionArcade.Interaction;
using UnityEngine;

namespace MotionArcade.Games.Darts
{
    // עזרת כיוון עדינה לחצים, אל המטרה הנוכחית (DartsGame.AimPoint). בסשן השלישי של גיא (40 חצים) הגובה התפזר
    // ב-±15 ס"מ, בעיקר מעוצמת הזריקה (±15%), והזריקות נטו בעקביות 3.5° שמאלה. העזרה מתקנת חצי מהשגיאה
    // (ThrowMath.Assist: כיוון לצדדים ומהירות, הקשת נשארת של השחקן), ורק בזריקה שהולכת בערך למטרה.
    // חלשה יותר מבכדורסל (75%): בחצים הדיוק הוא המשחק עצמו. המדידה נשמרת על הזריקה המקורית.
    public class DartAssist : MonoBehaviour, IThrowAssist
    {
        [SerializeField] private DartsGame game;
        [SerializeField, Range(0f, 1f)] private float strength = 0.5f;
        [SerializeField] private float maxYawError = 10f;          // מעלות
        [SerializeField] private float minSpeedRatio = 0.7f;
        [SerializeField] private float maxSpeedRatio = 1.4f;

        public float Strength
        {
            get => strength;
            set => strength = Mathf.Clamp01(value);
        }

        public Vector3 Adjust(Vector3 position, Vector3 velocity)
        {
            if (game == null || !isActiveAndEnabled) return velocity;
            return ThrowMath.Assist(position, velocity, game.AimPoint, -Physics.gravity.y, strength,
                                    maxYawError, minSpeedRatio, maxSpeedRatio, out _);
        }
    }
}
