using System;
using System.Collections;
using UnityEngine;

namespace MotionArcade.Core
{
    // מעבר "מסך שחור" בכניסה למשחק וביציאה ממנו: כדור שחור קטן סביב המצלמה (נראה מבפנים) שנהיה אטום,
    // ברגע השחור מזיזים את השחקן, ואז הוא נעלם. ב-VR זה מונע את הקפיצה הפתאומית של המקום (מבלבל ולא נוח).
    // הכדור קרוב מכל דבר אחר (גם מהידיים), אז הוא מסתיר הכל בלי לגעת בבדיקת העומק.
    [DefaultExecutionOrder(-400)]
    public class ScreenFader : MonoBehaviour
    {
        [SerializeField] private Renderer sphere;
        [SerializeField] private float fadeOut = 0.25f;
        [SerializeField] private float hold = 0.3f;
        [SerializeField] private float fadeIn = 0.35f;

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private MaterialPropertyBlock block;

        public static ScreenFader Instance { get; private set; }
        public bool Busy { get; private set; }
        public float Alpha { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        private void Awake()
        {
            Instance = this;
            block = new MaterialPropertyBlock();
            SetAlpha(0f);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // מחשיך, מריץ את atBlack, ומאיר בחזרה. false = כבר באמצע מעבר.
        public bool Transition(Action atBlack)
        {
            if (Busy) return false;
            StartCoroutine(Run(atBlack));
            return true;
        }

        private IEnumerator Run(Action atBlack)
        {
            Busy = true;
            yield return Fade(0f, 1f, fadeOut);
            // תקלה באמצע (למשל משחק שנפרק) לא משאירה את השחקן בחושך
            try { atBlack?.Invoke(); }
            catch (Exception e) { Debug.LogException(e, this); }
            yield return new WaitForSecondsRealtime(hold);
            yield return Fade(1f, 0f, fadeIn);
            Busy = false;
        }

        private IEnumerator Fade(float from, float to, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                SetAlpha(Mathf.Lerp(from, to, t / seconds));
                yield return null;
            }
            SetAlpha(to);
        }

        private void SetAlpha(float alpha)
        {
            Alpha = alpha;
            sphere.enabled = alpha > 0.001f;
            block.SetColor(BaseColor, new Color(0f, 0f, 0f, alpha));
            sphere.SetPropertyBlock(block);
        }
    }
}
