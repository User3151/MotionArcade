using TMPro;
using UnityEngine;

namespace MotionArcade.Feedback
{
    // "+2" שעולה ונעלם מעל המקום שבו נקלעה נקודה. טקסט אחד שמוחזר לשימוש, בלי Instantiate ובלי GC.
    public class ScorePopup : MonoBehaviour
    {
        [SerializeField] private TMP_Text text;
        [SerializeField] private float seconds = 0.9f;
        [SerializeField] private float rise = 0.35f;

        private Vector3 start;
        private Color color;
        private float timer;

        private void Awake()
        {
            start = transform.localPosition;
            color = text.color;
            text.enabled = false;
        }

        public void Show(int points)
        {
            text.SetText("+{0}", points);
            timer = seconds;
            text.enabled = true;
        }

        private void Update()
        {
            if (timer <= 0f) return;
            timer -= Time.deltaTime;
            float t = 1f - Mathf.Clamp01(timer / seconds);   // 0 → 1 לאורך ההופעה

            transform.localPosition = start + Vector3.up * (rise * t);
            color.a = 1f - t * t;   // נשאר ברור רוב הזמן ונעלם בסוף
            text.color = color;
            if (timer <= 0f) text.enabled = false;
        }
    }
}
