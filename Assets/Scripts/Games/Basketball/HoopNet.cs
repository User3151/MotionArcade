using UnityEngine;

namespace MotionArcade.Games.Basketball
{
    // רשת שמתנהגת כמו רשת: נקודות שמחוברות בחוטים (Verlet), השורה העליונה תלויה על הטבעת, והשאר נופלות,
    // נמתחות ומתנדנדות. כדור שעובר דוחף את הנקודות החוצה, והרשת מאטה אותו בחזרה (כמו רשת ניילון אמיתית,
    // שהכדור "נבלע" בה). גיא ביקש כדורסל שנראה ומרגיש כמו Just Hoops; קודם הרשת הייתה ציור קבוע.
    // בלי קוליידרים: ההתנגשות עם הכדורים מחושבת כאן, כדי שהרשת לא תקפיץ כדורים ולא תשפיע על זיהוי הסל.
    // החוטים מצוירים כפסים דקים ברשת אחת שמתעדכנת בכל פריים, עם מערכים שמוקצים מראש (בלי GC).
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class HoopNet : MonoBehaviour
    {
        [SerializeField] private Rigidbody[] balls;
        [SerializeField] private float ballRadius = 0.12f;
        [SerializeField] private int columns = 12;
        [SerializeField] private int rows = 6;                 // כולל השורה שתלויה על הטבעת
        [SerializeField] private float topRadius = 0.215f;     // הווים, קצת בתוך הטבעת
        [SerializeField] private float bottomRadius = 0.13f;
        [SerializeField] private float length = 0.40f;
        [SerializeField] private float strand = 0.006f;        // רוחב חוט
        [SerializeField, Range(0.9f, 1f)] private float damping = 0.985f;
        [SerializeField] private int iterations = 8;
        [SerializeField] private float maxBallSlowdown = 250f;  // מ'/ש' לשנייה: תקרה לכוח של הרשת על הכדור
        [SerializeField, Range(0f, 1f)] private float catchRate = 0.8f;   // כמה מהחדירה לחוטים מוחזר לכדור בכל צעד
        [SerializeField] private float maxKnotSpeed = 8f;      // מ'/ש': קשר לא עף מהר מזה

        private Vector3[] position;     // במרחב העולם
        private Vector3[] previous;
        private Vector3[] pinned;       // השורה העליונה, במרחב המקומי של הרשת
        private Vector3[] lastCentre;   // לכל כדור: המרכז בצעד הקודם (המסלול שלו בצעד)
        private int[] linkA, linkB;
        private float[] rest;
        private Vector3[] vertices;
        private Mesh mesh;

        public int Knots => position.Length;
        public Vector3 Knot(int i) => position[i];
        public float Length => length;

        private int Index(int row, int column) => row * columns + ((column % columns) + columns) % columns;

        private void Awake()
        {
            int count = rows * columns;
            position = new Vector3[count];
            previous = new Vector3[count];
            pinned = new Vector3[columns];
            if (balls == null) balls = new Rigidbody[0];
            lastCentre = new Vector3[balls.Length];
            for (int i = 0; i < balls.Length; i++) if (balls[i] != null) lastCentre[i] = balls[i].position;
            for (int r = 0; r < rows; r++)
            {
                float t = r / (float)(rows - 1);
                float radius = Mathf.Lerp(topRadius, bottomRadius, t);
                for (int c = 0; c < columns; c++)
                {
                    // שורות לסירוגין חצי עמודה הצידה: יוצא דוגמת יהלומים, כמו רשת אמיתית
                    float angle = (c + 0.5f * (r % 2)) * Mathf.PI * 2f / columns;
                    var local = new Vector3(Mathf.Cos(angle) * radius, -t * length, Mathf.Sin(angle) * radius);
                    int i = Index(r, c);
                    position[i] = previous[i] = transform.TransformPoint(local);
                    if (r == 0) pinned[c] = local;
                }
            }

            // כל קשר מחובר לשניים שמתחתיו (אלכסונים), והשורה התחתונה גם לשכנים שלה (השפה)
            int links = (rows - 1) * columns * 2 + columns;
            linkA = new int[links];
            linkB = new int[links];
            rest = new float[links];
            int k = 0;
            for (int r = 0; r < rows - 1; r++)
                for (int c = 0; c < columns; c++)
                {
                    int shift = r % 2;   // שורה זוגית: c-1 ו-c בשורה הבאה; אי-זוגית: c ו-c+1
                    Link(ref k, Index(r, c), Index(r + 1, c - 1 + shift));
                    Link(ref k, Index(r, c), Index(r + 1, c + shift));
                }
            for (int c = 0; c < columns; c++) Link(ref k, Index(rows - 1, c), Index(rows - 1, c + 1));

            vertices = new Vector3[links * 4];
            var triangles = new int[links * 6];
            for (int i = 0; i < links; i++)
            {
                int v = i * 4, t = i * 6;
                triangles[t] = v; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
                triangles[t + 3] = v + 1; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
            }
            mesh = new Mesh { name = "Hoop Net (live)" };
            mesh.MarkDynamic();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.bounds = new Bounds(new Vector3(0f, -length * 0.6f, 0f), Vector3.one * (length * 2.5f));   // קבוע: בלי חישוב בכל פריים
            GetComponent<MeshFilter>().sharedMesh = mesh;
            UpdateMesh();
        }

        private void Link(ref int k, int a, int b)
        {
            linkA[k] = a;
            linkB[k] = b;
            rest[k] = Vector3.Distance(position[a], position[b]);
            k++;
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            Vector3 gravity = Physics.gravity * (dt * dt);
            float maxStep = maxKnotSpeed * dt;

            for (int i = columns; i < position.Length; i++)
            {
                Vector3 p = position[i];
                Vector3 velocity = Vector3.ClampMagnitude((p - previous[i]) * damping, maxStep);
                position[i] = p + velocity + gravity;
                previous[i] = p;
            }
            for (int c = 0; c < columns; c++)
                position[c] = previous[c] = transform.TransformPoint(pinned[c]);

            // הכדור דוחף קודם והחוטים נמתחים אחר כך: מה שנשאר בתוך הכדור בסוף הוא מה שהרשת לא מוותרת עליו,
            // ו-HoldBalls מחזיר אותו לכדור. בסדר ההפוך הדחיפה תמיד ניצחה, והרשת לא עצרה כלום.
            for (int it = 0; it < iterations; it++)
            {
                PushOutBalls();
                for (int k = 0; k < rest.Length; k++)
                {
                    int a = linkA[k], b = linkB[k];
                    Vector3 delta = position[b] - position[a];
                    float distance = delta.magnitude;
                    if (distance < 1e-6f || distance <= rest[k]) continue;   // חוט נמתח, לא נדחס
                    Vector3 fix = delta * ((distance - rest[k]) / distance);
                    bool aFree = a >= columns, bFree = b >= columns;
                    if (aFree && bFree) { position[a] += fix * 0.5f; position[b] -= fix * 0.5f; }
                    else if (aFree) position[a] += fix;
                    else if (bFree) position[b] -= fix;
                }
            }
            HoldBalls(dt);
            KeepBelowRim();
            for (int i = 0; i < balls.Length; i++)
                if (balls[i] != null) lastCentre[i] = balls[i].position;
        }

        // החוטים נדחפים מחוץ למסלול של הכדור בצעד הזה (מהמרכז הקודם לנוכחי), לא רק מהמקום הסופי שלו:
        // כדור עובר 6-7 ס"מ בצעד, וחוט שהיה נשאר בתוכו היה נדחף לצד הלא נכון ועף.
        private void PushOutBalls()
        {
            float reach = ballRadius + strand * 0.5f;
            for (int ball = 0; ball < balls.Length; ball++)
            {
                Rigidbody body = balls[ball];
                if (body == null || body.isKinematic) continue;
                Vector3 from = lastCentre[ball], to = body.position;
                if (Far(to) && Far(from)) continue;
                if ((from - to).sqrMagnitude > 0.25f) from = to;   // הוצב מחדש (חזר למגש): אין מסלול
                for (int k = 0; k < rest.Length; k++)
                {
                    int a = linkA[k], b = linkB[k];
                    Closest(position[a], position[b], from, to, out float t, out Vector3 onStrand, out Vector3 onPath);
                    Vector3 offset = onStrand - onPath;
                    float d = offset.magnitude;
                    if (d >= reach) continue;
                    Vector3 normal = d > 1e-5f ? offset / d : Outward(onStrand);
                    Vector3 push = normal * (reach - d);
                    bool aFree = a >= columns, bFree = b >= columns;
                    if (aFree && bFree) { position[a] += push * (1f - t); position[b] += push * t; }
                    else if (aFree) position[a] += push;
                    else if (bFree) position[b] += push;
                }
            }
        }

        // אחרי הפתרון, חוט שעדיין בתוך הכדור הוא חוט מתוח שלא יכול לזוז יותר: הרשת עוצרת את הכדור.
        // מה שנשאר בפנים דוחף את הכדור החוצה, עם תקרה, כך שהרשת "בולעת" את הכדור ומורידה אותו, כמו רשת ניילון.
        private void HoldBalls(float dt)
        {
            float reach = ballRadius + strand * 0.5f;
            foreach (Rigidbody body in balls)
            {
                if (body == null || body.isKinematic) continue;
                Vector3 centre = body.position;
                if (Far(centre)) continue;
                Vector3 push = Vector3.zero;
                for (int k = 0; k < rest.Length; k++)
                {
                    Closest(position[linkA[k]], position[linkB[k]], centre, centre, out _, out Vector3 onStrand, out _);
                    Vector3 offset = centre - onStrand;
                    float d = offset.magnitude;
                    if (d >= reach || d < 1e-5f) continue;
                    push += offset * ((reach - d) / d);
                }
                if (push == Vector3.zero) continue;
                Vector3 change = Vector3.ClampMagnitude(push * (catchRate / dt), maxBallSlowdown * dt);
                body.AddForce(change, ForceMode.VelocityChange);
            }
        }

        // רשת לא עולה מעל הטבעת (בלי זה כדור שיצא מהצד הפך אותה למעלה)
        private void KeepBelowRim()
        {
            for (int i = columns; i < position.Length; i++)
            {
                Vector3 local = transform.InverseTransformPoint(position[i]);
                if (local.y <= 0f) continue;
                local.y = 0f;
                position[i] = transform.TransformPoint(local);
            }
        }

        // כדור רחוק מהרשת (במגש, ביד): לא בודקים אותו בכלל
        private bool Far(Vector3 centre) => (centre - transform.position).sqrMagnitude > (length + ballRadius + 0.3f) * (length + ballRadius + 0.3f);

        private Vector3 Outward(Vector3 world)
        {
            Vector3 local = transform.InverseTransformPoint(world);
            local.y = 0f;
            return local.sqrMagnitude > 1e-8f ? transform.TransformDirection(local.normalized) : transform.right;
        }

        // הנקודות הקרובות ביותר בין קטע החוט (p0-p1) לקטע המסלול (q0-q1). t = המיקום על החוט (0-1).
        public static void Closest(Vector3 p0, Vector3 p1, Vector3 q0, Vector3 q1, out float t, out Vector3 onP, out Vector3 onQ)
        {
            Vector3 d1 = p1 - p0, d2 = q1 - q0, r = p0 - q0;
            float a = Vector3.Dot(d1, d1), e = Vector3.Dot(d2, d2), f = Vector3.Dot(d2, r);
            float s;
            if (a <= 1e-10f) { t = 0f; s = e <= 1e-10f ? 0f : Mathf.Clamp01(f / e); }
            else
            {
                float c = Vector3.Dot(d1, r);
                if (e <= 1e-10f) { s = 0f; t = Mathf.Clamp01(-c / a); }
                else
                {
                    float b = Vector3.Dot(d1, d2), denom = a * e - b * b;
                    t = denom > 1e-10f ? Mathf.Clamp01((b * f - c * e) / denom) : 0f;
                    s = (b * t + f) / e;
                    if (s < 0f) { s = 0f; t = Mathf.Clamp01(-c / a); }
                    else if (s > 1f) { s = 1f; t = Mathf.Clamp01((b - c) / a); }
                }
            }
            onP = p0 + d1 * t;
            onQ = q0 + d2 * s;
        }

        private void LateUpdate() => UpdateMesh();

        // כל חוט = פס דק, שפונה החוצה מציר הרשת (מהצד הפנימי והחיצוני רואים אותו באותו רוחב)
        private void UpdateMesh()
        {
            for (int k = 0; k < rest.Length; k++)
            {
                Vector3 a = transform.InverseTransformPoint(position[linkA[k]]);
                Vector3 b = transform.InverseTransformPoint(position[linkB[k]]);
                Vector3 along = b - a;
                Vector3 outward = (a + b) * 0.5f;
                outward.y = 0f;
                Vector3 side = Vector3.Cross(along, outward);
                side = side.sqrMagnitude > 1e-10f ? side.normalized * (strand * 0.5f) : Vector3.right * (strand * 0.5f);
                int v = k * 4;
                vertices[v] = a - side;
                vertices[v + 1] = a + side;
                vertices[v + 2] = b - side;
                vertices[v + 3] = b + side;
            }
            mesh.SetVertices(vertices);   // בלי הקצאה; חומר Unlit, אין צורך בנורמלים
        }

        private void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
        }
    }
}
