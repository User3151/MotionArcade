using System;
using MotionArcade.Interaction;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace MotionArcade.Games.Darts
{
    // איך החץ יושב ביד. גיא: "כשאני מכוון, החץ צריך להיות מקביל לרצפה ומכוון ישר קדימה, והתנועה היא הטיה של
    // 45° קדימה". מה שקובע את זה הוא איך כל שחקן מחזיק את הבקר כשהוא מכוון, ואת זה אי אפשר לדעת מראש, אז:
    // - מתחילים מברירת מחדל: החוד 45° מתחת לקרן של הבקר (בשחרור הבקר הצביע בממוצע 37° למעלה).
    // - אחרי כל זריקה שהייתה לפניה עצירת כיוון (ControllerThrow מוצא אותה), מחשבים איזו אחיזה הייתה מכוונת
    //   את החץ בדיוק אל המטרה מהתנוחה ההיא, מאוזן, ומתקרבים אליה בחצי הדרך. אחרי 2-3 זריקות החץ יושב כמו
    //   שהשחקן מחזיק. מוגבל ל-40° מברירת המחדל, כדי שעצירה שזוהתה בטעות לא תהפוך את החץ.
    // - האחיזה משתנה רק בחצים שלא ביד, אז היא לא קופצת בזמן כיוון. לא נשמרת בין הפעלות: כל שחקן מתחיל מחדש.
    public class DartGrip : MonoBehaviour
    {
        // מרכז הקנה 1.5 ס"מ מעל נקודת האחיזה (בין האגודל לאצבע), ונקודת האחיזה 2.5 ס"מ לפני מרכז החץ
        private static readonly Vector3 BarrelInHand = new Vector3(0f, 0.015f, 0f);
        private static readonly Vector3 GripOnDart = new Vector3(0f, 0f, 0.025f);

        [SerializeField] private DartsGame game;
        [SerializeField] private Dart[] darts;
        [SerializeField] private float defaultPitch = 45f;          // כמה מעלות החוד מתחת לקרן של הבקר
        [SerializeField, Range(0f, 1f)] private float learnRate = 0.5f;
        [SerializeField] private float maxChange = 40f;             // הכי רחוק מברירת המחדל, במעלות

        private Quaternion inHand;   // הסיבוב של החץ ביחס לנקודת האחיזה ביד
        private ControllerThrow[] throwers;
        private Action<ControllerThrow, ThrowRelease> onReleased;

        public Quaternion InHand => inHand;
        public Quaternion Default => Quaternion.Euler(defaultPitch, 0f, 0f);
        public int Learned { get; private set; }   // מכמה זריקות האחיזה למדה

        private void Awake()
        {
            inHand = Default;
            onReleased = HandleReleased;
            throwers = new ControllerThrow[darts.Length];
            for (int i = 0; i < darts.Length; i++) throwers[i] = darts[i].GetComponent<ControllerThrow>();
            Apply();
        }

        private void OnEnable()
        {
            foreach (ControllerThrow t in throwers)
                if (t != null) t.Released += onReleased;
        }

        private void OnDisable()
        {
            foreach (ControllerThrow t in throwers)
                if (t != null) t.Released -= onReleased;
        }

        private void HandleReleased(ControllerThrow thrown, ThrowRelease release)
        {
            if (!release.AimFound || game == null) return;
            Quaternion aimed = PointingAt(release.AimHandRotation, release.AimHandPosition, game.AimPoint);
            Quaternion allowed = Quaternion.RotateTowards(Default, aimed, maxChange);
            inHand = Quaternion.Slerp(inHand, allowed, learnRate);
            Learned++;
            Apply();
        }

        // לכל חץ שלא ביד (החץ שנזרק עכשיו כבר לא ביד)
        private void Apply()
        {
            foreach (Dart dart in darts)
            {
                if (dart == null || dart.Current == Dart.State.Held) continue;
                Transform attach = dart.GetComponent<XRGrabInteractable>().attachTransform;
                AttachPose(inHand, out Vector3 position, out Quaternion rotation);
                attach.SetLocalPositionAndRotation(position, rotation);
            }
        }

        // הסיבוב של החץ ביחס ליד, שמצביע מנקודת האחיזה אל target ומאוזן (הצד העליון של החץ כלפי מעלה)
        public static Quaternion PointingAt(Quaternion hand, Vector3 handPosition, Vector3 target)
        {
            Quaternion toHand = Quaternion.Inverse(hand);
            return Quaternion.LookRotation(toHand * (target - handPosition), toHand * Vector3.up);
        }

        // XRI מיישר את נקודת האחיזה של החץ (attach) עם נקודת האחיזה ביד: סיבוב החץ = יד × attach⁻¹.
        // לכן attach = inHand⁻¹, והמיקום שלו נבחר כך שמרכז הקנה יוצא BarrelInHand ביחס ליד.
        public static void AttachPose(Quaternion inHand, out Vector3 localPosition, out Quaternion localRotation)
        {
            localRotation = Quaternion.Inverse(inHand);
            localPosition = GripOnDart - localRotation * BarrelInHand;
        }
    }
}
