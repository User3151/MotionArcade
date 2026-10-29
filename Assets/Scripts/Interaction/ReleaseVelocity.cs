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
    }
}
