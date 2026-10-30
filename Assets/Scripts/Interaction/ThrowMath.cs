using UnityEngine;

namespace MotionArcade.Interaction
{
    // חשבון של מסלול זריקה, בלי התנגדות אוויר. בשביל מדידת הזריקות (ThrowRecorder) ועזרת כיוון בהמשך.
    public static class ThrowMath
    {
        // המהירות שצריך בזווית pitch (רדיאנים מעל האופק) כדי שמסלול יעבור בנקודה שנמצאת distance קדימה
        // (אופקית) ו-height למעלה. NaN אם בזווית הזו אי אפשר להגיע לשם בכלל (הקו ישר מתחת למטרה).
        // y = x·tanθ − g·x² / (2·v²·cos²θ)  →  v² = g·x² / (2·cos²θ·(x·tanθ − y))
        public static float SpeedToHit(float distance, float height, float pitch, float gravity)
        {
            float cos = Mathf.Cos(pitch);
            float rise = distance * Mathf.Tan(pitch) - height;
            if (distance <= 0f || cos <= 1e-4f || rise <= 1e-5f) return float.NaN;
            return Mathf.Sqrt(gravity * distance * distance / (2f * cos * cos * rise));
        }

        // איפה המסלול (מנקודה p במהירות v, עם gravity) חוצה מישור (point, normal), ומתי.
        // direction = +1: רק חצייה בכיוון הנורמל (חץ שעף לתוך הלוח), ‎-1: נגד הנורמל (כדור שיורד דרך מישור הטבעת).
        // false אם המסלול לא מגיע למישור בכיוון הזה (כדור שלא עלה עד הטבעת).
        public static bool TryCrossPlane(Vector3 p, Vector3 v, Vector3 gravity, Vector3 point, Vector3 normal,
                                         int direction, out Vector3 crossing, out float time)
        {
            crossing = default;
            time = 0f;
            // n·(p + v·t + ½·g·t² − point) = 0
            float a = 0.5f * Vector3.Dot(normal, gravity);
            float b = Vector3.Dot(normal, v);
            float c = Vector3.Dot(normal, p - point);

            float t0, t1;
            if (Mathf.Abs(a) < 1e-6f)
            {
                if (Mathf.Abs(b) < 1e-6f) return false;
                t0 = t1 = -c / b;
            }
            else
            {
                float disc = b * b - 4f * a * c;
                if (disc < 0f) return false;
                float root = Mathf.Sqrt(disc);
                t0 = (-b - root) / (2f * a);
                t1 = (-b + root) / (2f * a);
                if (t0 > t1) (t0, t1) = (t1, t0);
            }

            // הזמן החיובי הראשון שבו המהירות לאורך הנורמל בכיוון המבוקש
            for (int k = 0; k < 2; k++)
            {
                float t = k == 0 ? t0 : t1;
                if (t <= 0f) continue;
                float along = Vector3.Dot(normal, v + gravity * t);
                if (along * direction <= 0f) continue;
                time = t;
                crossing = p + v * t + 0.5f * t * t * gravity;
                return true;
            }
            return false;
        }

        // עזרת כיוון: מקרבת זריקה למסלול שעובר ב-aim, בחלק strength (0 עד 1) מהשגיאה, בפנייה ובמהירות.
        // זווית היציאה (גובה הקשת) נשארת של השחקן; המהירות מתקרבת למה שהיה צריך בזווית הזו.
        // זריקה רחוקה מדי מהנכון (פנייה מעל maxYawError, או מהירות מחוץ ל-minRatio..maxRatio) לא מתוקנת:
        // זריקה גרועה עדיין מחטיאה. applied = האם תוקן.
        public static Vector3 Assist(Vector3 position, Vector3 velocity, Vector3 aim, float gravity, float strength,
                                     float maxYawError, float minRatio, float maxRatio, out bool applied)
        {
            applied = false;
            float speed = velocity.magnitude;
            Vector3 to = aim - position;
            float distance = new Vector2(to.x, to.z).magnitude;
            if (speed < 0.5f || distance < 0.3f || strength <= 0f) return velocity;

            float pitch = PitchOf(velocity) * Mathf.Deg2Rad;
            float need = SpeedToHit(distance, to.y, pitch, gravity);
            if (float.IsNaN(need)) return velocity;   // קשת נמוכה מדי: אין מהירות שמגיעה
            float ratio = speed / need;
            float yawError = Mathf.DeltaAngle(YawOf(to), YawOf(velocity));
            if (ratio < minRatio || ratio > maxRatio || Mathf.Abs(yawError) > maxYawError) return velocity;

            strength = Mathf.Clamp01(strength);
            float yaw = (YawOf(velocity) - strength * yawError) * Mathf.Deg2Rad;
            float newSpeed = speed + strength * (need - speed);
            float horizontal = Mathf.Cos(pitch) * newSpeed;
            applied = true;
            return new Vector3(Mathf.Sin(yaw) * horizontal, Mathf.Sin(pitch) * newSpeed, Mathf.Cos(yaw) * horizontal);
        }

        // זווית מעל האופק ופנייה (ימינה = חיובי, 0 = ‎+Z), במעלות
        public static float PitchOf(Vector3 v)
        {
            float speed = v.magnitude;
            return speed < 1e-5f ? 0f : Mathf.Asin(Mathf.Clamp(v.y / speed, -1f, 1f)) * Mathf.Rad2Deg;
        }

        public static float YawOf(Vector3 v) => Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;
    }
}
