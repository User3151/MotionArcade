using UnityEngine;

namespace MotionArcade.Interaction
{
    // התנוחות האחרונות של היד (מיקום וסיבוב), כדי למצוא את רגע הכיוון: העצירה הקצרה שלפני הזריקה,
    // שבה השחקן מחזיק את החץ מול העיניים ומכוון. ככה יודעים איך הוא מחזיק את הבקר כשהחץ אמור להיות ישר.
    // נקודת המפנה בסוף תנופה לאחור גם היא כמעט בלי מהירות, אבל רק לרגע, ולכן העצירה צריכה להימשך (minStill).
    // מערכים שמוקצים מראש, בלי GC. בלי Unity מעבר ל-Vector3/Quaternion, לבדיקות.
    public class HandPoseHistory
    {
        private const float SpeedSpan = 0.05f;   // המהירות נמדדת על 50 ms, כדי שרעד המעקב (מ"מ בודדים) לא ייראה כתנועה

        private readonly float[] times;
        private readonly Vector3[] positions;
        private readonly Quaternion[] rotations;
        private readonly float minInterval;
        private int start;
        private int count;

        // minInterval: דגימה אחת לכל היותר בכל פרק זמן כזה, כדי שהמערך יכסה אותו זמן בכל קצב פריימים.
        // ב-Editor בלי קסדה יש 270 פריימים לשנייה, ו-256 דגימות כיסו רק את התנופה עצמה, בלי העצירה שלפניה.
        public HandPoseHistory(int capacity, float minInterval = 0f)
        {
            times = new float[capacity];
            positions = new Vector3[capacity];
            rotations = new Quaternion[capacity];
            this.minInterval = minInterval;
        }

        public int Count => count;

        public void Clear()
        {
            start = 0;
            count = 0;
        }

        public void Add(float time, Vector3 position, Quaternion rotation)
        {
            if (count > 0 && time - times[At(count - 1)] < minInterval) return;
            int i;
            if (count < times.Length) i = (start + count++) % times.Length;
            else
            {
                i = start;
                start = (start + 1) % times.Length;
            }
            times[i] = time;
            positions[i] = position;
            rotations[i] = rotation;
        }

        private int At(int k) => (start + k) % times.Length;   // k = 0 הכי ישנה

        // העצירה האחרונה לפני now: לפחות minStill שניות שבהן היד זזה לאט מ-maxSpeed, ושנגמרה לכל היותר
        // maxBefore שניות לפני now. מחזיר את התנוחה ברגע האחרון של העצירה (רגע לפני שהתנועה מתחילה).
        public bool TryFindPause(float now, float maxBefore, float minStill, float maxSpeed,
            out Vector3 position, out Quaternion rotation, out float secondsBefore)
        {
            position = default;
            rotation = Quaternion.identity;
            secondsBefore = float.NaN;

            int runEnd = -1;   // הדגימה הכי חדשה בעצירה הנוכחית (בסריקה מהחדש לישן)
            int j = count - 1; // הדגימה שמולה מודדים מהירות: לפחות SpeedSpan לפני הנוכחית
            for (int k = count - 1; k >= 0; k--)
            {
                float t = times[At(k)];
                if (runEnd < 0 && t < now - maxBefore) return false;   // עצירה שנגמרה מוקדם מדי לא שייכת לזריקה הזו

                while (j >= 0 && times[At(j)] > t - SpeedSpan) j--;
                if (j < 0) return false;   // אין מספיק היסטוריה כדי למדוד מהירות כאן
                float dt = t - times[At(j)];
                bool still = dt > 0f && (positions[At(k)] - positions[At(j)]).magnitude / dt < maxSpeed;

                if (!still)
                {
                    runEnd = -1;
                    continue;
                }
                if (runEnd < 0)
                {
                    if (t > now) continue;
                    runEnd = k;
                }
                if (times[At(runEnd)] - t >= minStill)
                {
                    int end = At(runEnd);
                    position = positions[end];
                    rotation = rotations[end];
                    secondsBefore = now - times[end];
                    return true;
                }
            }
            return false;
        }
    }
}
