using UnityEngine;

namespace MotionArcade.Core
{
    // חפץ שאפשר "לזרוק" בלי יד: בדיקות Play אוטומטיות ו-GcProbe משתמשים בזה כדי להריץ את מסלול הזריקה האמיתי
    // (כיוון החץ באוויר, נעיצה, ניקוד) בלי בקר. ההתנהגות זהה לשחרור מהיד במהירות הזו.
    public interface ILaunchable
    {
        void Launch(Vector3 from, Vector3 velocity);
    }
}
