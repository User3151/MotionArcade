using System;
using System.Globalization;
using System.Text;
using MotionArcade.Core;
using MotionArcade.Interaction;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace MotionArcade.Diagnostics
{
    // זריקה אחת, כמו שהיא נשמרת לקובץ. מיקומים על המטרה הם במישור שלה, במטרים:
    // טבעת: x ימינה, y עומק (חיובי = ארוך, מעבר למרכז הטבעת). לוח: x ימינה, y למעלה.
    public struct ThrowRecord
    {
        public int Number;
        public RoundPhase Phase;
        public int ObjectId;
        public ThrowRelease Release;

        public bool Crossed;           // המסלול (בלי מכשולים) מגיע למישור המטרה
        public Vector2 Cross;          // איפה
        public Vector2 Aim;            // לאן היה צריך (מרכז הטבעת, המקטע הנוכחי בלוח)
        public int AimNumber;          // בחצים: המטרה של Around the Clock. 0 = אין
        public float NeedSpeed;        // המהירות שהייתה מביאה לנקודת הכיוון, באותה זווית יציאה. NaN = הזווית נמוכה מדי
        public float AimYawError;      // כמה מעלות ימינה (+) או שמאלה (‎-) מהכיוון לנקודת הכיוון
        public float Apex;             // הגובה הכי גבוה של המסלול

        public string Result;          // "BASKET", ‏"T20", ‏"MISS"... null = לא דווח
        public bool HasResultPoint;
        public Vector2 ResultPoint;    // איפה החץ ננעץ בפועל
    }

    // שורת CSV לכל זריקה. מספרים תמיד עם נקודה (InvariantCulture), גם במחשב שמוגדר לפסיק עשרוני.
    public static class ThrowCsv
    {
        public const string Header =
            "game,n,phase,time_s,hand,rel_x,rel_y,rel_z," +
            "applied_speed,applied_pitch,applied_yaw,obj_speed," +
            "hand_speed,hand_pitch,hand_yaw,dev_speed,dev_pitch,dev_yaw," +
            "peak_hand_speed,peak_before_ms,select_drop_ms,axis_vs_flight_deg,hand_fwd_pitch," +
            "need_speed,speed_ratio,aim_yaw_err,crossed,cross_x_cm,cross_y_cm,aim_x_cm,aim_y_cm,err_x_cm,err_y_cm,apex_y," +
            "target,result,result_x_cm,result_y_cm";

        public static void Append(StringBuilder sb, string game, in ThrowRecord r)
        {
            ThrowRelease t = r.Release;
            Vector3 v = t.Applied;
            sb.Append(game).Append(',').Append(r.Number).Append(',').Append(r.Phase).Append(',');
            Num(sb, t.Time, "0.000");
            sb.Append(t.Hand == InteractorHandedness.Left ? "L," : t.Hand == InteractorHandedness.Right ? "R," : "?,");
            Num(sb, t.Position.x); Num(sb, t.Position.y); Num(sb, t.Position.z);

            Num(sb, v.magnitude); Num(sb, ThrowMath.PitchOf(v), "0.0"); Num(sb, ThrowMath.YawOf(v), "0.0");
            Num(sb, t.ObjectVelocity.magnitude);
            Direction(sb, t.HandVelocity);
            Direction(sb, t.DeviceVelocity);

            Num(sb, t.HandPeakSpeed);
            Num(sb, t.PeakSecondsBefore * 1000f, "0");
            Num(sb, t.SelectDropSeconds * 1000f, "0");
            Num(sb, v.sqrMagnitude > 1e-6f ? Vector3.Angle(t.Forward, v) : float.NaN, "0.0");
            Num(sb, t.HandForward.sqrMagnitude > 1e-6f ? ThrowMath.PitchOf(t.HandForward) : float.NaN, "0.0");

            Num(sb, r.NeedSpeed);
            Num(sb, r.NeedSpeed > 0f ? v.magnitude / r.NeedSpeed : float.NaN);
            Num(sb, r.AimYawError, "0.0");
            sb.Append(r.Crossed ? "1," : "0,");
            if (r.Crossed)
            {
                Num(sb, r.Cross.x * 100f, "0.0"); Num(sb, r.Cross.y * 100f, "0.0");
            }
            else sb.Append(",,");
            Num(sb, r.Aim.x * 100f, "0.0"); Num(sb, r.Aim.y * 100f, "0.0");
            if (r.Crossed)
            {
                Num(sb, (r.Cross.x - r.Aim.x) * 100f, "0.0"); Num(sb, (r.Cross.y - r.Aim.y) * 100f, "0.0");
            }
            else sb.Append(",,");
            Num(sb, r.Apex);

            sb.Append(r.AimNumber).Append(',').Append(r.Result ?? "").Append(',');
            if (r.HasResultPoint)
            {
                Num(sb, r.ResultPoint.x * 100f, "0.0");
                Num(sb, r.ResultPoint.y * 100f, "0.0", last: true);
            }
            else sb.Append(',');
            sb.Append('\n');
        }

        // מהירות, זווית מעל האופק ופנייה. ריק אם לא ידוע.
        private static void Direction(StringBuilder sb, Vector3 v)
        {
            if (float.IsNaN(v.x))
            {
                sb.Append(",,,");
                return;
            }
            Num(sb, v.magnitude); Num(sb, ThrowMath.PitchOf(v), "0.0"); Num(sb, ThrowMath.YawOf(v), "0.0");
        }

        private static void Num(StringBuilder sb, float value, string format = "0.000", bool last = false)
        {
            if (!float.IsNaN(value) && !float.IsInfinity(value)) sb.Append(value.ToString(format, CultureInfo.InvariantCulture));
            if (!last) sb.Append(',');
        }
    }
}
