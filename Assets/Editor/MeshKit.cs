using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MotionArcade.EditorTools
{
    // כלים קטנים לבניית mesh-ים בקוד, בשביל מודלים שאנחנו מעצבים בעצמנו (שולחן ההוקי ועוד):
    // מלבן מעוגל, "לופט" בין טבעות (קופסה מעוגלת עם שוליים משופעים), טבעת מוחלצת (פס ניאון, מסגרת)
    // ו"מחרטה" (גוף סיבובי: מחבט, פאק). כל רצועה מקבלת קודקודים משלה, כך שהקצוות בין צד לגג חדים
    // והעיגול לאורך הטבעת חלק.
    public static class MeshKit
    {
        // נקודות של מלבן מעוגל במישור XZ, נגד כיוון השעון במבט מלמעלה, מתחילות בצד +X
        public static List<Vector2> RoundedRect(float width, float depth, float radius, int cornerSegments = 6)
        {
            radius = Mathf.Clamp(radius, 0f, Mathf.Min(width, depth) * 0.5f - 1e-4f);
            float hx = width * 0.5f - radius, hz = depth * 0.5f - radius;
            var centers = new[] { new Vector2(hx, -hz), new Vector2(hx, hz), new Vector2(-hx, hz), new Vector2(-hx, -hz) };
            var points = new List<Vector2>();
            for (int c = 0; c < 4; c++)
                for (int i = 0; i <= cornerSegments; i++)
                {
                    float a = (c * 90f - 90f + 90f * i / cornerSegments) * Mathf.Deg2Rad;
                    points.Add(centers[c] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
                }
            return points;
        }

        // קופסה מעוגלת עם שיפוע: רשימת (inset, y) מלמטה למעלה. הטבעת התחתונה והעליונה נסגרות במכסה.
        public static Mesh RoundedBox(string name, float width, float depth, float radius, IList<Vector2> insetHeights,
            bool capBottom = true, bool capTop = true, int cornerSegments = 6)
        {
            var rings = new List<List<Vector3>>();
            foreach (Vector2 ih in insetHeights)
            {
                var ring = new List<Vector3>();
                foreach (Vector2 p in RoundedRect(width - 2f * ih.x, depth - 2f * ih.x, Mathf.Max(0.001f, radius - ih.x), cornerSegments))
                    ring.Add(new Vector3(p.x, ih.y, p.y));
                rings.Add(ring);
            }
            var b = new Builder();
            for (int r = 0; r < rings.Count - 1; r++) b.Band(rings[r], rings[r + 1]);
            if (capBottom) b.Cap(rings[0], false);
            if (capTop) b.Cap(rings[rings.Count - 1], true);
            return b.ToMesh(name);
        }

        // טבעת מוחלצת: בין מלבן מעוגל פנימי לחיצוני, מ-y0 עד y1 (פס ניאון סביב השולחן, מסגרת של מסך)
        public static Mesh RoundedRing(string name, float innerWidth, float innerDepth, float innerRadius, float thickness, float y0, float y1, int cornerSegments = 6)
        {
            var inner = RoundedRect(innerWidth, innerDepth, innerRadius, cornerSegments);
            var outer = RoundedRect(innerWidth + 2f * thickness, innerDepth + 2f * thickness, innerRadius + thickness, cornerSegments);
            List<Vector3> At(List<Vector2> ring, float y) { var l = new List<Vector3>(); foreach (var p in ring) l.Add(new Vector3(p.x, y, p.y)); return l; }
            var b = new Builder();
            b.Band(At(outer, y0), At(outer, y1));            // חוץ
            b.Band(At(inner, y1), At(inner, y0));            // פנים
            b.Band(At(outer, y1), At(inner, y1));            // גג
            b.Band(At(inner, y0), At(outer, y0));            // תחתית
            return b.ToMesh(name);
        }

        // גוף סיבובי סביב Y: פרופיל (רדיוס, גובה) מלמטה למעלה. רדיוס 0 בקצה = סגור.
        public static Mesh Lathe(string name, IList<Vector2> profile, int segments = 40)
        {
            var b = new Builder();
            for (int i = 0; i < profile.Count - 1; i++)
            {
                var lower = new List<Vector3>(); var upper = new List<Vector3>();
                for (int s = 0; s < segments; s++)
                {
                    float a = s * Mathf.PI * 2f / segments;
                    var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    lower.Add(dir * profile[i].x + Vector3.up * profile[i].y);
                    upper.Add(dir * profile[i + 1].x + Vector3.up * profile[i + 1].y);
                }
                b.Band(lower, upper);
            }
            return b.ToMesh(name);
        }

        // תיבה פשוטה עם מרכז וגודל (מסגרות, עמודים)
        public static Mesh Box(string name, Vector3 size, float bevel = 0.004f)
        {
            float h = size.y;
            return RoundedBox(name, size.x, size.z, bevel * 1.5f,
                new[] { new Vector2(bevel, -h * 0.5f), new Vector2(0f, -h * 0.5f + bevel), new Vector2(0f, h * 0.5f - bevel), new Vector2(bevel, h * 0.5f) }, cornerSegments: 2);
        }

        private class Builder
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Vector2> uvs = new List<Vector2>();
            private readonly List<int> triangles = new List<int>();

            // רצועה בין שתי טבעות באותו אורך (סגורות). הפנים פונות החוצה כשהטבעות נגד כיוון השעון וה-b מעל a.
            public void Band(List<Vector3> a, List<Vector3> b)
            {
                int n = a.Count, start = vertices.Count;
                float length = 0f;
                for (int i = 0; i <= n; i++)
                {
                    if (i > 0) length += Vector3.Distance(a[i % n], a[(i - 1) % n]);
                    vertices.Add(a[i % n]); uvs.Add(new Vector2(length, 0f));
                    vertices.Add(b[i % n]); uvs.Add(new Vector2(length, 1f));
                }
                for (int i = 0; i < n; i++)
                {
                    int a0 = start + i * 2, b0 = a0 + 1, a1 = a0 + 2, b1 = a0 + 3;
                    triangles.Add(a0); triangles.Add(b0); triangles.Add(a1);
                    triangles.Add(a1); triangles.Add(b0); triangles.Add(b1);
                }
            }

            // מכסה מניפה (הטבעת קמורה). up = פונה למעלה.
            public void Cap(List<Vector3> ring, bool up)
            {
                Vector3 center = Vector3.zero;
                foreach (var p in ring) center += p;
                center /= ring.Count;
                int c = vertices.Count;
                vertices.Add(center); uvs.Add(new Vector2(center.x, center.z));
                int start = vertices.Count;
                foreach (var p in ring) { vertices.Add(p); uvs.Add(new Vector2(p.x, p.z)); }
                for (int i = 0; i < ring.Count; i++)
                {
                    int i0 = start + i, i1 = start + (i + 1) % ring.Count;
                    if (up) { triangles.Add(c); triangles.Add(i1); triangles.Add(i0); }
                    else { triangles.Add(c); triangles.Add(i0); triangles.Add(i1); }
                }
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name, indexFormat = vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                mesh.SetVertices(vertices);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateTangents();
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
