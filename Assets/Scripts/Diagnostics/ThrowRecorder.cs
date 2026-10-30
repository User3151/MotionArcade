using System;
using System.IO;
using System.Text;
using MotionArcade.Core;
using MotionArcade.Interaction;
using UnityEngine;

namespace MotionArcade.Diagnostics
{
    // מדידת זריקות, לכיוון תחושת הזריקה לפי נתונים במקום ניחוש. רק ב-Editor ובבנייה של פיתוח.
    // כל זריקה נשמרת ברגע השחרור (מערך שמוקצה מראש, בלי GC באמצע סבב): מהירות היד בשלוש מדידות, מתי היה
    // שיא המהירות, מתי הכפתור התחיל להשתחרר, ולאן המסלול היה מגיע במישור המטרה בלי מכשולים.
    // המשחק מוסיף מה קרה בפועל (סל, מקטע בלוח). הכל נכתב ל-CSV בסוף סבב, ב-STOP וכשהסצנה נסגרת.
    // הקבצים: ThrowLogs ליד תיקיית Assets (ב-Editor) או ליד ה-exe, ולא ב-AppData שעל כונן C.
    public class ThrowRecorder : MonoBehaviour
    {
        public enum TargetPlane
        {
            RimFromAbove,   // מישור אופקי בגובה הטבעת, הכדור חוצה אותו בירידה. Transform המטרה = מרכז הטבעת
            BoardFace,      // פני הלוח, החץ חוצה אותם לכיוון ‎+Z של הלוח. Transform המטרה = מרכז הלוח
        }

        private const float ResultWindow = 6f;   // תוצאה שמגיעה יותר מאוחר מזה כבר לא שייכת לזריקה

        [SerializeField] private GameStation station;
        [SerializeField] private ControllerThrow[] throwables;
        [SerializeField] private Transform target;
        [SerializeField] private TargetPlane plane = TargetPlane.RimFromAbove;
        [SerializeField, Min(16)] private int capacity = 512;

        private static string sessionFile;   // קובץ אחד לכל הרצה, לכל המשחקים

        private ThrowRecord[] records;
        private int count;
        private int written;
        private int number;
        private Vector3 aim;
        private int aimNumber;
        private Action<ControllerThrow, ThrowRelease> onReleased;
        private Action<RoundPhase> onPhase;

        public static bool Active => Application.isEditor || Debug.isDebugBuild;

        public int Count => count;
        public ThrowRecord this[int index] => records[index];
        public string LastFile { get; private set; }

        public static string Folder => Application.platform == RuntimePlatform.Android
            ? Path.Combine(Application.persistentDataPath, "ThrowLogs")
            : Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ThrowLogs"));

        private void Awake()
        {
            if (!Active)
            {
                enabled = false;
                return;
            }
            records = new ThrowRecord[capacity];
            onReleased = HandleReleased;
            onPhase = HandlePhase;
            aim = target != null ? target.position : Vector3.zero;
        }

        private void OnEnable()
        {
            if (records == null) return;
            foreach (ControllerThrow t in throwables)
                if (t != null) t.Released += onReleased;
            if (station != null) station.PhaseChanged += onPhase;
        }

        private void OnDisable()
        {
            if (records == null) return;
            foreach (ControllerThrow t in throwables)
                if (t != null) t.Released -= onReleased;
            if (station != null) station.PhaseChanged -= onPhase;
        }

        private void OnDestroy()
        {
            if (records != null) Flush();
        }

        // לאן השחקן אמור לכוון עכשיו (בחצים: המקטע הנוכחי)
        public void SetAim(Vector3 world, int targetNumber)
        {
            aim = world;
            aimNumber = targetNumber;
        }

        // מה קרה לזריקה האחרונה של החפץ הזה
        public void NoteResult(GameObject thrown, string label) => Note(thrown, label, false, default);
        public void NoteResult(GameObject thrown, string label, Vector3 world) => Note(thrown, label, true, world);

        private void Note(GameObject thrown, string label, bool hasPoint, Vector3 world)
        {
            if (records == null || thrown == null) return;
            int id = thrown.GetInstanceID();
            for (int i = count - 1; i >= 0; i--)
            {
                if (records[i].ObjectId != id) continue;
                if (records[i].Result != null || Time.time - records[i].Release.Time > ResultWindow) return;
                records[i].Result = label;
                records[i].HasResultPoint = hasPoint;
                if (hasPoint) records[i].ResultPoint = ToPlane(world);
                return;
            }
        }

        private void HandlePhase(RoundPhase phase)
        {
            if (phase == RoundPhase.Results || phase == RoundPhase.Intro) Flush();
        }

        private void HandleReleased(ControllerThrow thrown, ThrowRelease release)
        {
            if (count == records.Length)
            {
                Flush();   // מלא (מאות זריקות בלי סוף סבב): כותבים ומתחילים מחדש
                count = written = 0;
            }

            var r = new ThrowRecord
            {
                Number = ++number,
                Phase = station != null ? station.Phase : RoundPhase.Intro,
                ObjectId = thrown.gameObject.GetInstanceID(),
                Release = release,
                AimNumber = aimNumber,
                Aim = ToPlane(aim),
            };
            Measure(ref r);
            records[count++] = r;
        }

        private void Measure(ref ThrowRecord r)
        {
            Vector3 p = r.Release.Position;
            Vector3 v = r.Release.Applied;
            Vector3 g = Physics.gravity;

            r.Apex = v.y > 0f ? p.y + v.y * v.y / (2f * -g.y) : p.y;

            Vector3 toAim = aim - p;
            float distance = new Vector2(toAim.x, toAim.z).magnitude;
            r.NeedSpeed = ThrowMath.SpeedToHit(distance, toAim.y, ThrowMath.PitchOf(v) * Mathf.Deg2Rad, -g.y);
            r.AimYawError = Mathf.DeltaAngle(ThrowMath.YawOf(toAim), ThrowMath.YawOf(v));

            if (target == null) return;
            bool rim = plane == TargetPlane.RimFromAbove;
            Vector3 normal = rim ? target.up : target.forward;
            r.Crossed = ThrowMath.TryCrossPlane(p, v, g, target.position, normal, rim ? -1 : 1, out Vector3 crossing, out _);
            if (r.Crossed) r.Cross = ToPlane(crossing);
        }

        private Vector2 ToPlane(Vector3 world)
        {
            if (target == null) return Vector2.zero;
            Vector3 local = target.InverseTransformPoint(world);
            return plane == TargetPlane.RimFromAbove ? new Vector2(local.x, local.z) : new Vector2(local.x, local.y);
        }

        // כותב את מה שעוד לא נכתב. מחזיר את הקובץ, או null אם לא היה מה לכתוב.
        public string Flush()
        {
            if (records == null || written >= count) return null;
            try
            {
                if (sessionFile == null)
                {
                    Directory.CreateDirectory(Folder);
                    sessionFile = Path.Combine(Folder, "throws_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv");
                }
                var sb = new StringBuilder(256 * (count - written + 1));
                if (!File.Exists(sessionFile)) sb.Append(ThrowCsv.Header).Append('\n');
                string game = station != null ? station.GameId : name;
                for (int i = written; i < count; i++) ThrowCsv.Append(sb, game, records[i]);
                File.AppendAllText(sessionFile, sb.ToString());
                written = count;
                LastFile = sessionFile;
                Debug.Log("[ThrowRecorder] " + game + ": throws saved to " + sessionFile);
                return sessionFile;
            }
            catch (IOException e)
            {
                Debug.LogWarning("[ThrowRecorder] could not write " + sessionFile + ": " + e.Message);
                return null;
            }
        }
    }
}
