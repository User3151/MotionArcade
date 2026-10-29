using System;

namespace MotionArcade.Games.BuzzWire
{
    // המרחק בין נקודה לטורוס (הטבעת של השרביט), במרחב של הטבעת: Z הוא הנורמל שלה, הטבעת במישור XY.
    // d = √((√(x² + y²) − R)² + z²). כשהוא קטן מעובי החוט ועוד עובי הטבעת, הם נוגעים.
    public static class TorusContact
    {
        public static float Distance(float x, float y, float z, float ringRadius)
        {
            float radial = (float)Math.Sqrt(x * x + y * y) - ringRadius;
            return (float)Math.Sqrt(radial * radial + z * z);
        }

        public static bool Touches(float x, float y, float z, float ringRadius, float ringTube, float wireRadius) =>
            Distance(x, y, z, ringRadius) < ringTube + wireRadius;
    }

    // ההתקדמות לאורך החוט, בלי Unity. t הוא המיקום על החוט (0 בהתחלה, 1 בסוף).
    // - מתקדמים רק כשהטבעת עוטפת את החוט (threaded), ובצעדים רציפים: קפיצה גדולה של t באמצע
    //   (הטבעת הוצאה והוכנסה במקום אחר) נחשבת יציאה מהחוט.
    // - אחרי יציאה חוזרים לנקודת הביקורת האחרונה: ההתקדמות ממשיכה רק כשהטבעת מוכנסת שוב לידה.
    // - נגיעה נספרת ברגע שהטבעת נוגעת, פעם אחת לכל נגיעה (עם השהיה קצרה, כדי שרעד על החוט לא ייספר עשרות פעמים).
    public class WireProgress
    {
        public const float MaxStep = 0.03f;        // הכי רחוק ש-t יכול לזוז בין שתי בדיקות
        public const float ResumeWindow = 0.04f;   // כמה קרוב לנקודת הביקורת צריך להכניס את הטבעת
        public const float FinishAt = 0.985f;
        public const float TouchDebounce = 0.25f;
        public static readonly float[] Checkpoints = { 0.25f, 0.5f, 0.75f };

        public float Position { get; private set; }      // איפה הטבעת על החוט עכשיו (כשהיא עליו)
        public float Best { get; private set; }          // הכי רחוק שהגיעו
        public float Checkpoint { get; private set; }    // נקודת הביקורת האחרונה (0 = ההתחלה)
        public bool OnWire { get; private set; }
        public bool Touching { get; private set; }
        public int Touches { get; private set; }
        public bool Finished { get; private set; }

        private float lastTouchTime = float.NegativeInfinity;

        public event Action<float> CheckpointReached;
        public event Action TouchStarted;

        public void Reset()
        {
            Position = Best = Checkpoint = 0f;
            OnWire = Touching = Finished = false;
            Touches = 0;
            lastTouchTime = float.NegativeInfinity;
        }

        // advance = false: הטבעת "מחליקה" (החוט קצת מחוץ ללולאה). המיקום ממשיך לעקוב אחרי הטבעת, כדי שלא תיחשב
        // קפיצה כשהיא חוזרת, אבל לא מתקדמים: לא שיא חדש, לא נקודת ביקורת ולא סיום.
        public void Update(bool threaded, float t, bool touching, float time, bool advance = true)
        {
            if (Finished) return;

            if (!threaded)
            {
                OnWire = false;
                Touching = false;
                return;
            }

            if (!OnWire)
            {
                // ממשיכים רק מנקודת הביקורת האחרונה
                if (Math.Abs(t - Checkpoint) > ResumeWindow) return;
                OnWire = true;
            }
            else if (Math.Abs(t - Position) > MaxStep)
            {
                OnWire = false;   // קפיצה: הטבעת לא החליקה לאורך החוט
                Touching = false;
                return;
            }

            Position = t;
            if (advance)
            {
                if (Position > Best) Best = Position;
                foreach (float cp in Checkpoints)
                {
                    if (Position < cp || cp <= Checkpoint) continue;
                    Checkpoint = cp;
                    CheckpointReached?.Invoke(cp);
                }
            }

            if (touching && !Touching && time - lastTouchTime >= TouchDebounce)
            {
                Touches++;
                lastTouchTime = time;
                TouchStarted?.Invoke();
            }
            if (touching) lastTouchTime = time;   // נגיעה רציפה לא נספרת שוב עד שמשתחררים
            Touching = touching;

            if (advance && Position >= FinishAt)
            {
                Finished = true;
                Best = 1f;   // הגיע לסוף: 100%, גם אם הבדיקה האחרונה הייתה ב-0.99
            }
        }

        // ניקוד: אחוז ההתקדמות פחות 3 לכל נגיעה, לא פחות מ-0
        public int Score => Math.Max(0, (int)(Best * 100f + 0.0001f) - 3 * Touches);
    }
}
