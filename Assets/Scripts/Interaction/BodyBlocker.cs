using UnityEngine;

namespace MotionArcade.Interaction
{
    // קוליידר שחוסם רק את הגוף של השחקן (גיא: "אני עדיין הולך דרך החפצים עם הג'ויסטיק"). שולחן ההוקי, הקיוסקים
    // ו-Buzz Wire היו בלי קוליידרים בכלל: לשולחן יש פיזיקה משלו, ולקיוסקים ולמוטות לא היה צורך.
    // על השכבה Ignore Raycast, כך שהקרניים של הידיים עוברות דרכו, ו-BodyCollision מבטל את ההתנגשות שלו עם כל חפץ
    // משחק (שרביט, כדור, חץ), כך שהוא עוצר רק את ה-CharacterController ואת הדחיפה החוצה של הראש.
    [RequireComponent(typeof(Collider))]
    public class BodyBlocker : MonoBehaviour { }
}
