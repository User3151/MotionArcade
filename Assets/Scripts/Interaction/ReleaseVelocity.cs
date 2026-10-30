using System;

namespace MotionArcade.Interaction
{
    // מהירות השחרור של זריקה, מהמיקומים האחרונים של החפץ ביד.
    // XRI ממצע את המהירות על פני רבע שנייה. בזריקה היד מאיצה עד הרגע האחרון, אז ממוצע ארוך נותן
    // בערך חצי מהמהירות האמיתית ברגע השחרור, והכדור נופל קצר. כאן לוקחים רק את החלון האחרון (כ-60 ms)
    // ומעבירים דרכו קו ישר (ריבועים פחותים), שמסנן רעש של דגימה בודדת בלי לפגר אחרי ההאצה.
    // בלי Unity, כדי שאפשר יהיה לבדוק על זריקות סינתטיות.
    public class ReleaseVelocity
    {
        private readonly float[] times;
        private readonly float[] xs, ys, zs;
        private int count;
        private int head;   // המקום הבא לכתיבה

        public ReleaseVelocity(int capacity = 32)
        {
            times = new float[capacity];
            xs = new float[capacity];
            ys = new float[capacity];
            zs = new float[capacity];
        }

        public void Clear()
        {
            count = 0;
            head = 0;
        }

        public void Add(float time, float x, float y, float z)
        {
            times[head] = time;
            xs[head] = x;
            ys[head] = y;
            zs[head] = z;
            head = (head + 1) % times.Length;
            if (count < times.Length) count++;
        }

        // שיפוע הקו (מטר לשנייה) על הדגימות בחלון האחרון. false אם אין מספיק דגימות.
        public bool TryEstimate(float window, out float vx, out float vy, out float vz)
        {
            vx = vy = vz = 0f;
            if (count < 2) return false;

            int newest = (head - 1 + times.Length) % times.Length;
            float latest = times[newest];
            double st = 0, sx = 0, sy = 0, sz = 0;
            int n = 0;
            for (int k = 0; k < count; k++)
            {
                int i = (newest - k + times.Length) % times.Length;
                if (latest - times[i] > window && n >= 2) break;
                double t = times[i] - latest;   // יחסי לדגימה האחרונה, לדיוק
                st += t; sx += xs[i]; sy += ys[i]; sz += zs[i];
                n++;
            }
            if (n < 2) return false;

            double mt = st / n, mx = sx / n, my = sy / n, mz = sz / n;
            double stt = 0, stx = 0, sty = 0, stz = 0;
            for (int k = 0; k < n; k++)
            {
                int i = (newest - k + times.Length) % times.Length;
                double t = times[i] - latest - mt;
                stt += t * t;
                stx += t * (xs[i] - mx);
                sty += t * (ys[i] - my);
                stz += t * (zs[i] - mz);
            }
            if (stt <= 1e-12) return false;
            vx = (float)(stx / stt);
            vy = (float)(sty / stt);
            vz = (float)(stz / stt);
            return true;
        }

        // המהירות הכי גבוהה בחלון האחרון, וכמה שניות לפני הדגימה האחרונה היא הייתה.
        // למדידה: אם השיא רחוק מרגע השחרור, היד כבר האטה כשהחפץ שוחרר (שחרור מאוחר).
        // כל מהירות היא קו ישר על span שניות (כמו TryEstimate), לא הפרש בין שתי דגימות: זמני הפריימים קופצים,
        // ושתי דגימות קרובות מדי בזמן נותנות מהירויות מטורפות.
        public bool TryPeakSpeed(float window, out float peak, out float secondsBefore, float span = 0.03f)
        {
            bool found = TryPeakVelocity(window, out float vx, out float vy, out float vz, out secondsBefore, span);
            peak = found ? (float)Math.Sqrt(vx * vx + vy * vy + vz * vz) : 0f;
            return found;
        }

        // המהירות (כיוון וגודל) של הרגע הכי מהיר בחלון האחרון. לזריקה: בגיא, ברגע שהכפתור שוחרר היד כבר האטה
        // ב-10%-16% מהשיא שלה כמה עשרות מילי-שניות קודם, והחלון של TryEstimate תפס את ההאטה (חצים יצאו נמוכים).
        public bool TryPeakVelocity(float window, out float vx, out float vy, out float vz, out float secondsBefore, float span = 0.03f)
        {
            vx = vy = vz = 0f;
            secondsBefore = 0f;
            if (count < 3) return false;

            int newest = (head - 1 + times.Length) % times.Length;
            float latest = times[newest];
            bool found = false;
            double best = 0;
            for (int end = 0; end < count; end++)
            {
                int e = (newest - end + times.Length) % times.Length;
                if (latest - times[e] > window) break;
                if (!TryFit(end, span, out double fx, out double fy, out double fz, out float middle)) continue;
                double speed = fx * fx + fy * fy + fz * fz;
                if (found && speed <= best) continue;
                best = speed;
                vx = (float)fx; vy = (float)fy; vz = (float)fz;
                secondsBefore = latest - middle;
                found = true;
            }
            return found;
        }

        // קו ישר על הדגימות שבין הדגימה ה-end מהסוף ועד span שניות לפניה. צריך 3 דגימות לפחות,
        // שמכסות לפחות חצי מ-span. middle: אמצע הזמן שהן מכסות.
        private bool TryFit(int end, float span, out double vx, out double vy, out double vz, out float middle)
        {
            vx = vy = vz = 0;
            middle = 0f;
            int newest = (head - 1 + times.Length) % times.Length;
            int e = (newest - end + times.Length) % times.Length;
            float tEnd = times[e];

            double st = 0, sx = 0, sy = 0, sz = 0;
            int n = 0;
            float tFirst = tEnd;
            for (int k = end; k < count; k++)
            {
                int i = (newest - k + times.Length) % times.Length;
                if (tEnd - times[i] > span) break;
                st += times[i] - tEnd; sx += xs[i]; sy += ys[i]; sz += zs[i];
                tFirst = times[i];
                n++;
            }
            if (n < 3 || tEnd - tFirst < 0.5f * span) return false;

            double mt = st / n, mx = sx / n, my = sy / n, mz = sz / n;
            double stt = 0, stx = 0, sty = 0, stz = 0;
            for (int k = end; k < end + n; k++)
            {
                int i = (newest - k + times.Length) % times.Length;
                double t = times[i] - tEnd - mt;
                stt += t * t;
                stx += t * (xs[i] - mx);
                sty += t * (ys[i] - my);
                stz += t * (zs[i] - mz);
            }
            if (stt <= 1e-12) return false;
            vx = stx / stt;
            vy = sty / stt;
            vz = stz / stt;
            middle = 0.5f * (tEnd + tFirst);
            return true;
        }
    }
}
