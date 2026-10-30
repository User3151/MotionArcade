using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using MotionArcade.Diagnostics;
using MotionArcade.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace MotionArcade.Tests
{
    public class ThrowMeasurementTests
    {
        private const float Dt = 1f / 90f;
        private static readonly Vector3 G = new Vector3(0f, -9.81f, 0f);

        [Test]
        public void PeakSpeed_FindsWhenTheHandWasFastest()
        {
            // יד שמאיצה ל-5 מ'/ש' ב-0.25 ש', ואז מאטה 80 ms לפני השחרור (שחרור מאוחר)
            var r = new ReleaseVelocity(64);
            float x = 0f, v = 0f, t = 0f;
            for (; t < 0.25f; t += Dt) { r.Add(t, x, 0f, 0f); v += 20f * Dt; x += v * Dt; }
            for (float s = 0f; s < 0.08f; s += Dt, t += Dt) { r.Add(t, x, 0f, 0f); v -= 30f * Dt; x += v * Dt; }
            r.Add(t, x, 0f, 0f);
            Assert.That(r.TryPeakSpeed(0.3f, out float peak, out float before), Is.True);
            Assert.That(peak, Is.EqualTo(5f).Within(0.3f));   // קו על 30 ms סביב השיא: קצת מתחת לשיא הרגעי
            Assert.That(before, Is.EqualTo(0.08f).Within(0.02f));
        }

        [Test]
        public void PeakSpeed_IgnoresSamplesAtTheSameTime()
        {
            // שני צעדי פיזיקה באותו פריים: אותו זמן, מיקום קצת אחר. לא "אינסוף מטר לשנייה".
            var r = new ReleaseVelocity();
            for (int i = 0; i < 10; i++)
            {
                float t = i * Dt;
                r.Add(t, 2f * t, 0f, 0f);
                r.Add(t, 2f * t + 0.001f, 0f, 0f);
            }
            Assert.That(r.TryPeakSpeed(0.3f, out float peak, out _), Is.True);
            Assert.That(peak, Is.EqualTo(2f).Within(0.3f));
        }

        [Test]
        public void PeakSpeed_SurvivesJitteryFrameTimes()
        {
            // זמני פריים לא אחידים (1 עד 20 ms), יד ב-4 מ'/ש' קבוע: השיא צריך להיות בערך 4, לא קפיצה של זוג דגימות צפופות
            var r = new ReleaseVelocity(64);
            var random = new System.Random(7);
            float t = 0f;
            for (int i = 0; i < 40; i++)
            {
                r.Add(t, 4f * t + (float)(random.NextDouble() - 0.5) * 0.001f, 0f, 0f);   // ±0.5 מ"מ רעש
                t += 0.001f + (float)random.NextDouble() * 0.019f;
            }
            Assert.That(r.TryPeakSpeed(0.3f, out float peak, out _), Is.True);
            Assert.That(peak, Is.EqualTo(4f).Within(0.3f));
        }

        [Test]
        public void ButtonRelease_TriggerEasedOffBeforeTheThrow()
        {
            // trigger לחוץ עד הסוף, ואז עולה במשך 60 ms עד שה-XRI משחרר (בערך ב-0.4)
            var b = new ButtonRelease();
            b.Update(0f, 0.3f);
            b.Update(0.05f, 1f);
            b.Update(0.10f, 0.95f);   // הרפיה קלה: עדיין לא שחרור
            Assert.That(float.IsNaN(b.DropStart), Is.True);
            b.Update(0.12f, 0.85f);
            b.Update(0.15f, 0.6f);
            b.Update(0.18f, 0.4f);
            Assert.That(b.SecondsSinceDrop(0.18f), Is.EqualTo(0.06f).Within(1e-5f));
        }

        [Test]
        public void ButtonRelease_ComingBackUpCancels_AndDigitalIsZero()
        {
            var b = new ButtonRelease();
            b.Update(0f, 1f);
            b.Update(0.1f, 0.7f);
            b.Update(0.2f, 1f);   // לחץ שוב
            Assert.That(float.IsNaN(b.DropStart), Is.True);
            b.Update(0.3f, 0f);   // L1/R1: מ-1 ל-0 בבת אחת
            Assert.That(b.SecondsSinceDrop(0.3f), Is.EqualTo(0f));
            b.Reset();
            Assert.That(float.IsNaN(b.SecondsSinceDrop(1f)), Is.True);
        }

        [Test]
        public void PeakVelocity_TakesTheDirectionOfTheFastestMoment()
        {
            // יד שעולה ומאיצה קדימה, ובסוף מאטה ויורדת (כמו אחרי שחרור מאוחר). הכיוון בשיא: למעלה-קדימה.
            var r = new ReleaseVelocity(64);
            float t = 0f, z = 0f, y = 0f, vz = 0f;
            for (; t < 0.2f; t += Dt) { r.Add(t, 0f, y, z); vz += 25f * Dt; z += vz * Dt; y += 1.5f * Dt; }
            for (float s = 0f; s < 0.05f; s += Dt, t += Dt) { r.Add(t, 0f, y, z); vz -= 40f * Dt; z += vz * Dt; y -= 1.5f * Dt; }
            r.Add(t, 0f, y, z);
            Assert.That(r.TryPeakVelocity(0.1f, out float px, out float py, out float pz, out _), Is.True);
            Assert.That(pz, Is.EqualTo(5f).Within(0.5f));   // קו על 30 ms: קצת מתחת לשיא הרגעי
            Assert.That(py, Is.GreaterThan(0.5f), "still rising at the peak");
            r.TryEstimate(0.06f, out _, out float ly, out float lz);
            Assert.That(lz, Is.LessThan(pz - 0.3f), "the last 60 ms caught the slow-down");
            Assert.That(ly, Is.LessThan(py));
        }

        [Test]
        public void Assist_FixesMostOfANearMiss()
        {
            var p = new Vector3(0.2f, 2.0f, 0.4f);
            var aim = new Vector3(0f, 2.45f, 3.25f);
            Vector3 to = aim - p;
            float pitch = 50f * Mathf.Deg2Rad;
            float need = ThrowMath.SpeedToHit(new Vector2(to.x, to.z).magnitude, to.y, pitch, 9.81f);
            float yaw = ThrowMath.YawOf(to) + 6f;   // 6° ימינה, 15% חזק מדי
            Vector3 v = Throw(need * 1.15f, 50f, yaw);
            Vector3 fixedV = ThrowMath.Assist(p, v, aim, 9.81f, 0.75f, 15f, 0.6f, 1.6f, out bool applied);
            Assert.That(applied, Is.True);
            Assert.That(fixedV.magnitude / need, Is.EqualTo(1f + 0.15f * 0.25f).Within(0.005f));
            Assert.That(Mathf.DeltaAngle(ThrowMath.YawOf(to), ThrowMath.YawOf(fixedV)), Is.EqualTo(1.5f).Within(0.05f));
            Assert.That(ThrowMath.PitchOf(fixedV), Is.EqualTo(50f).Within(0.05f), "the arc stays the player's");
        }

        [Test]
        public void Assist_LeavesWildThrowsAlone()
        {
            var p = new Vector3(0f, 2.0f, 0.4f);
            var aim = new Vector3(0f, 2.45f, 3.25f);
            Vector3 sideways = Throw(6f, 50f, 30f);
            Assert.That(ThrowMath.Assist(p, sideways, aim, 9.81f, 0.75f, 15f, 0.6f, 1.6f, out bool a1), Is.EqualTo(sideways));
            Assert.That(a1, Is.False);
            Vector3 lob = Throw(2f, 50f, 0f);   // הרבה מתחת למה שצריך
            Assert.That(ThrowMath.Assist(p, lob, aim, 9.81f, 0.75f, 15f, 0.6f, 1.6f, out bool a2), Is.EqualTo(lob));
            Assert.That(a2, Is.False);
            Vector3 flat = Throw(8f, 5f, 0f);   // קשת שלא מגיעה לגובה הטבעת בכלל
            Assert.That(ThrowMath.Assist(p, flat, aim, 9.81f, 0.75f, 15f, 0.6f, 1.6f, out bool a3), Is.EqualTo(flat));
            Assert.That(a3, Is.False);
        }

        private static Vector3 Throw(float speed, float pitchDeg, float yawDeg)
        {
            float pitch = pitchDeg * Mathf.Deg2Rad, yaw = yawDeg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(yaw) * Mathf.Cos(pitch), Mathf.Sin(pitch), Mathf.Cos(yaw) * Mathf.Cos(pitch)) * speed;
        }

        [Test]
        public void SpeedToHit_MatchesTheTextbook()
        {
            // 45°, אותו גובה: v² = g·d
            Assert.That(ThrowMath.SpeedToHit(2f, 0f, 45f * Mathf.Deg2Rad, 9.81f), Is.EqualTo(Mathf.Sqrt(9.81f * 2f)).Within(1e-3f));
            // זווית שלא מגיעה לגובה הזה בכלל
            Assert.That(float.IsNaN(ThrowMath.SpeedToHit(2f, 1f, 10f * Mathf.Deg2Rad, 9.81f)), Is.True);
        }

        [Test]
        public void SpeedToHit_ReallyReachesTheRim()
        {
            // הזריקה בכדורסל: 2.85 מ' קדימה, 0.85 מ' למעלה, ב-52°. בערך 6.1 מ'/ש', והמסלול עובר במרכז הטבעת.
            float pitch = 52f * Mathf.Deg2Rad;
            float speed = ThrowMath.SpeedToHit(2.85f, 0.85f, pitch, 9.81f);
            Assert.That(speed, Is.EqualTo(6.1f).Within(0.1f));
            var p = new Vector3(0f, 1.6f, 0.4f);
            var v = new Vector3(0f, Mathf.Sin(pitch), Mathf.Cos(pitch)) * speed;
            Assert.That(ThrowMath.TryCrossPlane(p, v, G, new Vector3(0f, 2.45f, 0f), Vector3.up, -1, out Vector3 at, out _), Is.True);
            Assert.That(at.z, Is.EqualTo(3.25f).Within(0.005f));
        }

        [Test]
        public void CrossPlane_DartFliesIntoTheBoard()
        {
            var p = new Vector3(0.1f, 1.6f, 0.3f);
            var v = new Vector3(0f, 1.5f, 5f);
            Assert.That(ThrowMath.TryCrossPlane(p, v, G, new Vector3(0f, 1.73f, 2.37f), Vector3.forward, 1, out Vector3 at, out float time), Is.True);
            float t = 2.07f / 5f;
            Assert.That(time, Is.EqualTo(t).Within(1e-4f));
            Assert.That(at.z, Is.EqualTo(2.37f).Within(1e-4f));
            Assert.That(at.y, Is.EqualTo(1.6f + 1.5f * t - 0.5f * 9.81f * t * t).Within(1e-4f));
            Assert.That(at.x, Is.EqualTo(0.1f).Within(1e-5f));
            // אותו חץ שנזרק אחורה לא מגיע ללוח
            Assert.That(ThrowMath.TryCrossPlane(p, -v, G, new Vector3(0f, 1.73f, 2.37f), Vector3.forward, 1, out _, out _), Is.False);
        }

        [Test]
        public void CrossPlane_BallCountsOnTheWayDown_NotUp()
        {
            // משוחרר מתחת לטבעת: חוצה את הגובה שלה פעמיים. נחשבת רק הירידה.
            var p = new Vector3(0f, 1.6f, 0.4f);
            var v = new Vector3(0f, 6f, 3f);
            Assert.That(ThrowMath.TryCrossPlane(p, v, G, new Vector3(0f, 2.45f, 0f), Vector3.up, -1, out _, out float time), Is.True);
            Assert.That(v.y + G.y * time, Is.LessThan(0f));
            float up = (6f - Mathf.Sqrt(36f - 2f * 9.81f * 0.85f)) / 9.81f;
            Assert.That(time, Is.GreaterThan(up + 0.1f));
        }

        [Test]
        public void CrossPlane_ShortBall_NeverReachesTheRim()
        {
            var p = new Vector3(0f, 1.6f, 0.4f);
            var v = new Vector3(0f, 3f, 3f);   // שיא בגובה 2.06
            Assert.That(ThrowMath.TryCrossPlane(p, v, G, new Vector3(0f, 2.45f, 0f), Vector3.up, -1, out _, out _), Is.False);
        }

        [Test]
        public void Csv_UsesDots_EvenWithACommaCulture()
        {
            CultureInfo before = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");   // פסיק עשרוני
                var sb = new StringBuilder();
                ThrowCsv.Append(sb, "darts", Sample());
                string line = sb.ToString().TrimEnd('\n');
                Assert.That(line.Split(',').Length, Is.EqualTo(ThrowCsv.Header.Split(',').Length), line);
                Assert.That(line, Does.Contain(",4.500,"), "applied speed with a dot");
                Assert.That(line, Does.Contain(",T20,"));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = before;
            }
        }

        [Test]
        public void Csv_UnknownMeasurementsAreEmpty()
        {
            ThrowRecord r = Sample();
            r.Release.DeviceVelocity = new Vector3(float.NaN, float.NaN, float.NaN);
            r.Release.SelectDropSeconds = float.NaN;
            r.Crossed = false;
            r.Result = null;
            r.HasResultPoint = false;
            var sb = new StringBuilder();
            ThrowCsv.Append(sb, "basketball", r);
            string[] cells = sb.ToString().TrimEnd('\n').Split(',');
            string[] header = ThrowCsv.Header.Split(',');
            Assert.That(cells.Length, Is.EqualTo(header.Length));
            string Cell(string column) => cells[System.Array.IndexOf(header, column)];
            Assert.That(Cell("dev_speed"), Is.Empty);
            Assert.That(Cell("select_drop_ms"), Is.Empty);
            Assert.That(Cell("cross_x_cm"), Is.Empty);
            Assert.That(Cell("result"), Is.Empty);
            Assert.That(Cell("hand_speed"), Is.EqualTo("4.000"));
            Assert.That(Cell("crossed"), Is.EqualTo("0"));
            Assert.That(Cell("aim_found"), Is.EqualTo("0"));
            Assert.That(Cell("aim_hand_pitch"), Is.Empty);
        }

        [Test]
        public void Csv_AimPose_WhereTheControllerAndTheDartPointed()
        {
            // בכיוון הבקר הצביע 50° למעלה, והחץ ישב ביד 50° מתחת לקרן: החץ היה מאוזן
            ThrowRecord r = Sample();
            r.Release.AimFound = true;
            r.Release.AimSecondsBefore = 0.42f;
            r.Release.AimHandRotation = Quaternion.Euler(-50f, 0f, 0f);
            r.Release.ObjectInHand = Quaternion.Euler(50f, 0f, 0f);
            var sb = new StringBuilder();
            ThrowCsv.Append(sb, "darts", r);
            string[] cells = sb.ToString().TrimEnd('\n').Split(',');
            string[] header = ThrowCsv.Header.Split(',');
            Assert.That(cells.Length, Is.EqualTo(header.Length));
            string Cell(string column) => cells[System.Array.IndexOf(header, column)];
            Assert.That(Cell("aim_found"), Is.EqualTo("1"));
            Assert.That(Cell("aim_before_ms"), Is.EqualTo("420"));
            float Value(string column) => float.Parse(Cell(column), CultureInfo.InvariantCulture);
            Assert.That(Value("aim_hand_pitch"), Is.EqualTo(50f).Within(0.11f));
            Assert.That(Value("aim_obj_pitch"), Is.EqualTo(0f).Within(0.11f));
            Assert.That(Value("grip_pitch"), Is.EqualTo(-50f).Within(0.11f));
            Assert.That(Cell("result_y_cm"), Is.EqualTo("10.0"), "the column before the new ones is unchanged");
        }

        private static ThrowRecord Sample() => new ThrowRecord
        {
            Number = 3,
            Phase = MotionArcade.Core.RoundPhase.Playing,
            Release = new ThrowRelease
            {
                Time = 12.5f,
                Hand = InteractorHandedness.Right,
                Position = new Vector3(0.2f, 1.5f, 0.4f),
                Forward = Vector3.forward,
                ObjectVelocity = new Vector3(0f, 0f, 4.1f),
                Applied = new Vector3(0f, 0f, 4.5f),
                HandVelocity = new Vector3(0f, 0f, 4f),
                DeviceVelocity = new Vector3(0f, 0.2f, 4.2f),
                HandForward = Vector3.forward,
                HandPeakSpeed = 4.6f,
                PeakSecondsBefore = 0.04f,
                SelectDropSeconds = 0.03f,
            },
            Crossed = true,
            Cross = new Vector2(0.05f, -0.1f),
            Aim = new Vector2(0f, 0.135f),
            AimNumber = 20,
            NeedSpeed = 5f,
            Result = "T20",
            HasResultPoint = true,
            ResultPoint = new Vector2(0.01f, 0.1f),
        };
    }
}
