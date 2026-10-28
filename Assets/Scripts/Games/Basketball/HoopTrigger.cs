using UnityEngine;

namespace MotionArcade.Games.Basketball
{
    // אחד משני החיישנים של הסל (Trigger collider). מעביר למשחק איזה כדור נכנס ובאיזו מהירות.
    [RequireComponent(typeof(Collider))]
    public class HoopTrigger : MonoBehaviour
    {
        public enum Level { Top, Bottom }

        [SerializeField] private BasketballGame game;
        [SerializeField] private Level level;

        private void OnTriggerEnter(Collider other)
        {
            Rigidbody body = other.attachedRigidbody;
            if (body == null || !body.TryGetComponent(out Ball ball)) return;

            if (level == Level.Top) game.HandleTop(ball);
            else game.HandleBottom(ball);
        }
    }
}
