using UnityEngine;

namespace MotionArcade.Interaction
{
    // איך היד מחזיקה את החפץ הזה (ControllerHand קורא את זה כשהחפץ ביד)
    public enum HandShape
    {
        Fist,    // ידית: פטיש, שרביט
        Pinch,   // בין האגודל לאצבע: חץ
        Cup,     // כף פתוחה סביב: כדור
    }

    public class HandHold : MonoBehaviour
    {
        [SerializeField] private HandShape shape = HandShape.Fist;

        // אופציונלי: היד זזה על הבקר בזמן ההחזקה (ביד ימין; ביד שמאל במראה). בכדור: כף היד מאחורי הכדור ומתחתיו,
        // האצבעות למעלה, כמו בזריקה לסל, במקום אגרוף שנוגע בכדור מאחור.
        [SerializeField] private bool moveHand;
        [SerializeField] private Vector3 handPosition;
        [SerializeField] private Vector3 handRotation;

        public HandShape Shape => shape;
        public bool MovesHand => moveHand;

        public void HandPose(bool left, out Vector3 position, out Quaternion rotation)
        {
            position = left ? new Vector3(-handPosition.x, handPosition.y, handPosition.z) : handPosition;
            rotation = Quaternion.Euler(handRotation.x, left ? -handRotation.y : handRotation.y, left ? -handRotation.z : handRotation.z);
        }
    }
}
