using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MotionArcade.EditorTools
{
    // מכונות הארקייד בחדר מגיעות מ-Sketchfab (Assets/ThirdParty, רישיונות ב-CREDITS.md) כמו שהן:
    // הגאצ'ה בנויה מ-131 חלקים, הארון הניאוני מ-39, ויש בקבצים תוספות (בן אדם ושטיח בפינבול, כדור שמיים
    // בהוקי ובמירוצים). כאן כל מודל "נאפה" ל-prefab קל: חלק אחד לכל חומר, בגודל אמיתי, החזית ל-+Z,
    // נקודת האחיזה במרכז הרצפה, ו-Mesh LOD למודלים הכבדים (הכל רחוק 2-6 מ' מהשחקן).
    // מריצים שוב מהתפריט "Motion Arcade/Bake Arcade Props" אחרי כל שינוי ברשימה.
    public static class ArcadePropBaker
    {
        public class Spec
        {
            public string Name;
            public string Source;            // תיקייה תחת Assets/ThirdParty
            public float Height;             // מטרים, מהרצפה עד הנקודה הגבוהה
            public float Yaw;                // סיבוב כך שהחזית (הצד שהשחקן ניגש אליו) פונה ל-+Z
            public string[] DropParts = new string[0];       // חלק מהשם או מהנתיב בהיררכיה
            public string[] DropMaterials = new string[0];
            public System.Action<Material> FixMaterial;
        }

        public const string PrefabFolder = "Assets/Prefabs/Arcade";
        private const string MeshFolder = "Assets/Meshes/Arcade";
        private const string MaterialFolder = "Assets/Materials/Arcade";
        private const int LodAbove = 3000;   // משולשים: מתחת לזה אין מה לחסוך

        public static readonly Spec[] All =
        {
            new Spec { Name = "Neon Cabinet", Source = "NeonCabinet", Height = 1.80f },
            new Spec { Name = "Claw Machine", Source = "ClawMachine", Height = 1.95f },
            new Spec { Name = "Gacha", Source = "Gacha", Height = 0.60f },
            new Spec
            {
                Name = "Pinball", Source = "Pinball", Height = 1.90f,
                DropParts = new[] { "manNwoman" }, DropMaterials = new[] { "Carpet" },
            },
            new Spec
            {
                Name = "Air Hockey", Source = "AirHockey", Height = 1.40f,
                DropParts = new[] { "Sphere.001_0" }, DropMaterials = new[] { "Background" },
            },
            new Spec
            {
                Name = "Racing Cabinet", Source = "RacingCabinet", Height = 1.90f,
                DropParts = new[] { "Sphere_0" }, DropMaterials = new[] { "Backdrop" },
                // מתכת מלאה וחלקה לגמרי: בלי משהו להחזיר היא יוצאת שחורה
                FixMaterial = m => { SetIf(m, "metallicFactor", 0.15f); SetIf(m, "roughnessFactor", 0.45f); },
            },
        };

        [MenuItem("Motion Arcade/Bake Arcade Props")]
        public static void BakeAll()
        {
            foreach (Spec spec in All) Bake(spec);
            AssetDatabase.SaveAssets();
        }

        public static GameObject Bake(Spec spec)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/ThirdParty/{spec.Source}/scene.gltf");
            if (source == null) { Debug.LogError($"[ArcadePropBaker] {spec.Source}: no scene.gltf"); return null; }

            // השורש של glTF נושא לפעמים scale וסיבוב משלו: עוטפים אותו במקום לדרוס אותם
            var temp = new GameObject("_bake");
            try
            {
                Object.Instantiate(source, temp.transform, false);
                temp.transform.rotation = Quaternion.Euler(0f, spec.Yaw, 0f);
                var parts = temp.GetComponentsInChildren<MeshRenderer>(true).Where(r => !Dropped(r, spec)).ToList();
                Bounds raw = WorldBounds(parts);
                temp.transform.localScale = Vector3.one * (spec.Height / raw.size.y);
                Bounds bounds = WorldBounds(parts);
                Vector3 pivot = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                Matrix4x4 toProp = Matrix4x4.Translate(-pivot);

                // חלק אחד לכל חומר
                var byMaterial = new Dictionary<Material, List<CombineInstance>>();
                foreach (MeshRenderer r in parts)
                {
                    Mesh mesh = r.GetComponent<MeshFilter>().sharedMesh;
                    Matrix4x4 m = toProp * r.transform.localToWorldMatrix;
                    for (int s = 0; s < mesh.subMeshCount && s < r.sharedMaterials.Length; s++)
                    {
                        Material mat = r.sharedMaterials[s];
                        if (mat == null || mesh.GetTopology(s) != MeshTopology.Triangles) continue;   // בגאצ'ה יש קווים
                        if (!byMaterial.TryGetValue(mat, out var list)) byMaterial[mat] = list = new List<CombineInstance>();
                        // CombineMeshes לא הופך את סדר המשולשים במטריצה עם שיקוף, אז חלק משוקף היה נראה מבפנים
                        Mesh part = m.determinant < 0f ? Flipped(mesh, s) : mesh;
                        list.Add(new CombineInstance { mesh = part, subMeshIndex = part == mesh ? s : 0, transform = m });
                    }
                }

                string meshDir = Folder(MeshFolder, spec.Name);
                string matDir = Folder(MaterialFolder, spec.Name);
                var root = new GameObject(spec.Name);
                int index = 0;
                foreach (var pair in byMaterial.OrderBy(p => p.Key.name))
                {
                    var combined = new Mesh { name = $"{spec.Name} {index:00} {pair.Key.name}", indexFormat = IndexFormat.UInt32 };
                    combined.CombineMeshes(pair.Value.ToArray(), true, true);
                    combined.RecalculateBounds();
                    if (combined.triangles.Length / 3 > LodAbove) MeshLodUtility.GenerateMeshLods(combined, 6);
                    combined.UploadMeshData(false);
                    string meshPath = $"{meshDir}/{index:00} {Safe(pair.Key.name)}.asset";
                    combined = Save(combined, meshPath);

                    var material = new Material(pair.Key) { name = pair.Key.name };
                    spec.FixMaterial?.Invoke(material);
                    string matPath = $"{matDir}/{index:00} {Safe(pair.Key.name)}.mat";
                    material = Save(material, matPath);

                    var child = new GameObject(Safe(pair.Key.name));
                    child.transform.SetParent(root.transform, false);
                    child.AddComponent<MeshFilter>().sharedMesh = combined;
                    var renderer = child.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = material;
                    // החדר אפוי; המכונות מקבלות את התאורה שלו דרך ה-Light Probes ולא נכנסות ל-lightmap
                    renderer.receiveGI = ReceiveGI.LightProbes;
                    renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    GameObjectUtility.SetStaticEditorFlags(child, StaticEditorFlags.BatchingStatic);
                    index++;
                }

                var box = root.AddComponent<BoxCollider>();
                box.center = bounds.center - pivot;
                box.size = bounds.size;

                Folder(PrefabFolder, null);
                string prefabPath = $"{PrefabFolder}/{spec.Name}.prefab";
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Object.DestroyImmediate(root);
                Debug.Log($"[ArcadePropBaker] {spec.Name}: {parts.Count} parts → {index} meshes, size {bounds.size:F2}");
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(temp);
            }
        }

        // עדכון במקום (ולא מחיקה ויצירה): ה-GUID נשאר, אז החדר והגרסאות הצבעוניות לא מאבדים את ההפניה
        private static T Save<T>(T asset, string path) where T : Object
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null) { AssetDatabase.CreateAsset(asset, path); return asset; }
            EditorUtility.CopySerialized(asset, existing);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static bool Dropped(Renderer r, Spec spec)
        {
            string path = r.name;
            for (Transform t = r.transform.parent; t != null; t = t.parent) path = t.name + "/" + path;
            if (spec.DropParts.Any(path.Contains)) return true;
            return r.sharedMaterials.Any(m => m != null && spec.DropMaterials.Any(m.name.Contains));
        }

        private static Bounds WorldBounds(List<MeshRenderer> parts)
        {
            Bounds b = parts[0].bounds;
            foreach (MeshRenderer r in parts) b.Encapsulate(r.bounds);
            return b;
        }

        private static Mesh Flipped(Mesh mesh, int subMesh)
        {
            var copy = Object.Instantiate(mesh);
            int[] tris = mesh.GetTriangles(subMesh);
            for (int i = 0; i < tris.Length; i += 3) (tris[i + 1], tris[i + 2]) = (tris[i + 2], tris[i + 1]);
            copy.subMeshCount = 1;
            copy.SetTriangles(tris, 0);
            return copy;
        }

        private static void SetIf(Material m, string property, float value)
        {
            if (m.HasProperty(property)) m.SetFloat(property, value);
        }

        private static string Folder(string parent, string name)
        {
            string path = name == null ? parent : $"{parent}/{name}";
            Directory.CreateDirectory(path);
            AssetDatabase.Refresh();
            return path;
        }

        private static string Safe(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name.Trim();
        }
    }
}
