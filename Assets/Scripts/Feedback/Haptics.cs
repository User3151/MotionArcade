using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace MotionArcade.Feedback
{
    // רטט בבקר דרך ה-interactor שמחזיק (או החזיק) את החפץ.
    // עובר דרך XRI ולא דרך מכשיר ספציפי, אז זה עובד ב-PSVR2, Quest ו-Pico בלי קוד נפרד.
    // יד בלי בקר (hand tracking, שלב 7) פשוט לא רוטטת: הבדיקה למטה מדלגת עליה בשקט.
    public static class Haptics
    {
        // עוצמות קבועות, כדי שכל המשחקים ירגישו אותו דבר
        public const float Light = 0.2f;    // תפיסה
        public const float Medium = 0.45f;  // פגיעה
        public const float Strong = 0.8f;   // הצלחה (סל, בול)

        public static void Pulse(IXRInteractor interactor, float amplitude, float seconds)
        {
            if (interactor is XRBaseInputInteractor input && input != null)
                input.SendHapticImpulse(amplitude, seconds);
        }
    }
}
