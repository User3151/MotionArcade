using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace MotionArcade.EditorTools
{
    // שולחן ההוקי, מעוצב מאפס ונבנה בקוד. גיא לא אהב את המודל של Sketchfab, ושלח תמונה של שולחן ארקייד
    // זוהר לדוגמה: משטח סגול-כחול עם משושים וקווים לבנים, דפנות ורודות זוהרות, גומות בפינות, חיצים בצדדים,
    // פס ליים למטה. בלי ה"גשר" מעל השולחן: הניקוד מרחף באוויר. הפאק זוהר והמחבטים בצבעים שבולטים על הסגול.
    // כל המידות והצבעים כאן למעלה; שינוי בעיצוב = שינוי מספר והרצה מחדש מ-"Motion Arcade/Build Air Hockey Table".
    // השורש של המשחק = המקום שבו השחקן עומד, השולחן מולו ב-+Z.
    public static class AirHockeyBuilder
    {
        public const string ScenePath = "Assets/Scenes/Game_AirHockey.unity";
        private const string MeshDir = "Assets/Meshes/AirHockey";
        private const string MaterialDir = "Assets/Materials/AirHockey";
        private const string TextureDir = "Assets/Textures/AirHockey";

        // מידות (מטרים)
        public const float FieldLength = 2.0f;     // לאורך Z, מהשער שלך לשער של היריב
        public const float FieldWidth = 1.05f;
        public const float Rail = 0.06f;           // הדופן הזוהרת סביב המשטח
        public const float RailHeight = 0.035f;    // כמה הדופן מעל המשטח
        public const float DeckSide = 0.12f;       // המשטח הכהה מסביב לדופן (בצדדים)
        public const float DeckEnd = 0.12f;        // ובקצוות (שווה לצדדים: הסיפון הוא טבעת ברוחב אחיד)
        public const float SurfaceY = 0.80f;
        public const float NearEdge = 0.22f;       // מהשחקן עד קצה השולחן
        public const float GoalWidth = 0.30f;
        public const float ApronDepth = 0.21f;
        public const float MalletRadius = 0.05f;
        public const float PuckRadius = 0.04f;

        public static float OuterWidth => FieldWidth + 2f * (Rail + DeckSide);
        public static float OuterLength => FieldLength + 2f * (Rail + DeckEnd);
        public static float CenterZ => NearEdge + OuterLength * 0.5f;

        // צבעים
        private static readonly Color BodyColor = new Color(0.05f, 0.03f, 0.08f);
        private static readonly Color Pink = new Color(1f, 0.12f, 0.7f);
        private static readonly Color Lime = new Color(0.55f, 1f, 0.1f);
        private static readonly Color Blue = new Color(0.2f, 0.45f, 1f);
        private static readonly Color Red = new Color(1f, 0.22f, 0.08f);
        private static readonly Color LineWhite = new Color(0.93f, 0.9f, 1f);
        private static readonly Color FieldNear = new Color(0.5f, 0.1f, 0.78f);    // סגול
        private static readonly Color FieldFar = new Color(0.1f, 0.2f, 0.85f);     // כחול

        [MenuItem("Motion Arcade/Build Air Hockey Table")]
        public static void Build()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.isLoaded)
                scene = File.Exists(ScenePath)
                    ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive)
                    : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            GameObject root = null;
            foreach (GameObject go in scene.GetRootGameObjects()) if (go.name == "Air Hockey Game") root = go;
            if (root == null)
            {
                root = new GameObject("Air Hockey Game");
                SceneManager.MoveGameObjectToScene(root, scene);
            }
            foreach (string old in new[] { "Table", "Player Mallet", "CPU Mallet", "Opponent Mallet", "Puck", "Score Display" })
            {
                Transform t = root.transform.Find(old);
                if (t != null) Object.DestroyImmediate(t.gameObject);
            }
            var table = new GameObject("Table").transform;
            table.SetParent(root.transform, false);

            Directory.CreateDirectory(MeshDir); Directory.CreateDirectory(MaterialDir); Directory.CreateDirectory(TextureDir);
            AssetDatabase.Refresh();

            Material body = Lit("Body", BodyColor, 0.3f, 0.8f);
            Material pinkGlow = Neon("Neon Pink", Pink, 1.1f);
            Material pinkSoft = Lit("Rail Pink", Pink * 0.4f, 0f, 0.9f, Pink * 0.45f);   // דופן "אקריליק" זוהרת, חלשה מהניאון
            Material lime = Neon("Neon Lime", Lime, 1.1f);
            Material blue = Neon("Neon Blue", Blue, 1.2f);
            Material field = FieldMaterial();

            float top = SurfaceY, apronBottom = SurfaceY - ApronDepth;
            Vector3 c = new Vector3(0f, 0f, CenterZ);

            // הגוף: קופסה רחבה עם פינות מעוגלות (בפינות הגומות), שנגמרת מתחת למשטח;
            // מעליה הסיפון: טבעת סביב המשטח והדפנות, קצת יותר גבוהה מהמשטח (המשטח כמו בור)
            float deckY = top + 0.012f;
            Part(table, "Body", Save(MeshKit.RoundedBox("Air Hockey Body", OuterWidth, OuterLength, 0.17f, new[]
            {
                new Vector2(0.03f, apronBottom), new Vector2(0f, apronBottom + 0.03f), new Vector2(0f, top - 0.004f),
            })), body, c);
            Part(table, "Deck", Save(MeshKit.RoundedRing("Air Hockey Deck", FieldWidth + 2f * Rail, FieldLength + 2f * Rail, 0.05f, DeckSide, top - 0.004f, deckY)), body, c);
            // ניאון ורוד סביב הקצה העליון, ליים סביב התחתון
            Part(table, "Neon Rim", Save(MeshKit.RoundedRing("Air Hockey Rim", OuterWidth, OuterLength, 0.17f, 0.014f, -0.012f, 0.004f)), pinkGlow, c + Vector3.up * (deckY - 0.01f));
            Part(table, "Neon Lime", Save(MeshKit.RoundedRing("Air Hockey Lime Strip", OuterWidth - 0.06f, OuterLength - 0.06f, 0.14f, 0.012f, 0f, 0.016f)), lime, c + Vector3.up * (apronBottom + 0.004f));

            // המשטח (מעט מתחת לסיפון, כמו בור)
            Part(table, "Field", Save(FieldMesh()), field, c + Vector3.up * top);
            // לוגו במרכז, שוכב על המשטח וקריא מהעמדה של השחקן
            FlatText(table, "Field Logo", "<size=55%>MOTION ARCADE</size>\nAIR HOCKEY", c + Vector3.up * (top + 0.0015f), 0.75f, LineWhite);

            // הדפנות הזוהרות, עם פתח שער בכל קצה
            float railY = top + RailHeight * 0.5f, sideX = FieldWidth * 0.5f + Rail * 0.5f;
            Mesh sideRail = Save(MeshKit.Box("Air Hockey Side Rail", new Vector3(Rail, RailHeight, FieldLength + 2f * Rail), 0.006f));
            Part(table, "Rail Left", sideRail, pinkSoft, c + new Vector3(-sideX, railY, 0f));
            Part(table, "Rail Right", sideRail, pinkSoft, c + new Vector3(sideX, railY, 0f));
            float segment = (FieldWidth - GoalWidth) * 0.5f;
            Mesh endRail = Save(MeshKit.Box("Air Hockey End Rail", new Vector3(segment, RailHeight, Rail), 0.006f));
            foreach (int end in new[] { -1, 1 })
                foreach (int side in new[] { -1, 1 })
                    Part(table, $"Rail {(end < 0 ? "Near" : "Far")} {(side < 0 ? "Left" : "Right")}", endRail, pinkSoft,
                        c + new Vector3(side * (GoalWidth * 0.5f + segment * 0.5f), railY, end * (FieldLength * 0.5f + Rail * 0.5f)));
            // השערים: חריץ כהה מאחורי הפתח, ופס בצבע השחקן (ליים אצלך, אדום אצל היריב)
            Material redGlow = Neon("Neon Red", Red, 1.1f);
            Mesh goalMouth = Save(MeshKit.Box("Air Hockey Goal Mouth", new Vector3(GoalWidth, 0.03f, DeckEnd + Rail), 0.004f));
            Mesh goalBar = Save(MeshKit.Box("Air Hockey Goal Light", new Vector3(GoalWidth + 0.06f, 0.012f, 0.012f), 0.003f));
            Material dark = Lit("Goal Dark", new Color(0.01f, 0.01f, 0.02f), 0f, 0.3f);
            foreach (int end in new[] { -1, 1 })
            {
                float z = end * (FieldLength * 0.5f + (Rail + DeckEnd) * 0.5f);
                Part(table, end < 0 ? "Goal Near" : "Goal Far", goalMouth, dark, c + new Vector3(0f, deckY - 0.014f, z));
                Part(table, end < 0 ? "Goal Light Near" : "Goal Light Far", goalBar, end < 0 ? lime : redGlow,
                    c + new Vector3(0f, deckY + 0.006f, end * (OuterLength * 0.5f - 0.03f)));
            }

            // גומות זוהרות בפינות (במשחק: המחבט שלך מחכה בגומה הימנית הקרובה)
            Mesh cup = Save(MeshKit.Lathe("Air Hockey Cup", new[]
            {
                // מבחוץ פנימה: הקיר החיצוני למעלה, השפה, והקיר הפנימי למטה (כך הפנים פונות החוצה מהחומר)
                new Vector2(0.068f, -0.006f), new Vector2(0.068f, 0.012f), new Vector2(0.066f, 0.016f), new Vector2(0.053f, 0.016f), new Vector2(0.05f, 0.012f), new Vector2(0.05f, 0.002f),
            }));
            Mesh cupFloor = Save(MeshKit.Lathe("Air Hockey Cup Floor", new[] { new Vector2(0.051f, 0.002f), new Vector2(0f, 0.002f) }));
            float cupX = FieldWidth * 0.5f + Rail + 0.067f, cupZ = OuterLength * 0.5f - 0.085f;
            foreach (int end in new[] { -1, 1 })
                foreach (int side in new[] { -1, 1 })
                {
                    Vector3 p = c + new Vector3(side * cupX, deckY, end * cupZ);
                    string n = $"Cup {(end < 0 ? "Near" : "Far")} {(side < 0 ? "Left" : "Right")}";
                    Part(table, n, cup, pinkGlow, p);
                    Part(table, n + " Floor", cupFloor, dark, p);
                }

            // חיצים בצדדים: ורודים שמצביעים אליך בחצי שלך, כחולים שמצביעים אל היריב בחצי שלו
            Mesh chevronArm = Save(MeshKit.Box("Air Hockey Chevron Arm", new Vector3(0.045f, 0.004f, 0.011f), 0.002f));
            float chevronX = FieldWidth * 0.5f + Rail + DeckSide * 0.5f;
            foreach (int side in new[] { -1, 1 })
                foreach (int half in new[] { -1, 1 })
                    for (int i = 0; i < 3; i++)
                    {
                        float pointing = half;                       // +1: אל היריב, -1: אליך
                        Vector3 tip = c + new Vector3(side * chevronX, deckY + 0.002f, half * (0.2f + i * 0.075f) + pointing * 0.02f);
                        for (int arm = -1; arm <= 1; arm += 2)
                        {
                            var d = new Vector3(arm * Mathf.Sin(55f * Mathf.Deg2Rad), 0f, -pointing * Mathf.Cos(55f * Mathf.Deg2Rad));
                            var go = Part(table, $"Chevron {side} {half} {i} {arm}", chevronArm, half < 0 ? pinkGlow : blue, tip + d * 0.0225f);
                            go.transform.localRotation = Quaternion.Euler(0f, Mathf.Atan2(-d.z, d.x) * Mathf.Rad2Deg, 0f);
                        }
                    }

            // שלט בחזית, מול השחקן
            var plate = new GameObject("Name Plate").transform;
            plate.SetParent(table, false);
            plate.localPosition = new Vector3(0f, apronBottom + 0.105f, NearEdge - 0.003f);
            var plateText = plate.gameObject.AddComponent<TMPro.TextMeshPro>();
            Text(plateText, "MOTION ARCADE", 0.45f, Pink);

            // רגליים: שני לוחות צד עם קצוות ניאון, קורה ביניהם, ומגש החזרה של הפאק מתחת לשער שלך
            Mesh leg = Save(MeshKit.Box("Air Hockey Leg", new Vector3(0.07f, apronBottom - 0.04f, OuterLength * 0.62f), 0.012f));
            Mesh legEdge = Save(MeshKit.Box("Air Hockey Leg Edge", new Vector3(0.016f, apronBottom - 0.06f, 0.016f), 0.003f));
            Mesh foot = Save(MeshKit.Box("Air Hockey Foot", new Vector3(0.16f, 0.04f, OuterLength * 0.7f), 0.008f));
            float legX = OuterWidth * 0.3f;
            foreach (int side in new[] { -1, 1 })
            {
                Part(table, side < 0 ? "Leg Left" : "Leg Right", leg, body, c + new Vector3(side * legX, 0.02f + (apronBottom - 0.04f) * 0.5f, 0f));
                Part(table, side < 0 ? "Foot Left" : "Foot Right", foot, body, c + new Vector3(side * legX, 0.02f, 0f));
                foreach (int end in new[] { -1, 1 })
                    Part(table, $"Leg Neon {side} {end}", legEdge, pinkGlow,
                        c + new Vector3(side * legX, 0.04f + (apronBottom - 0.06f) * 0.5f, end * (OuterLength * 0.31f + 0.004f)));
            }
            Part(table, "Crossbar", Save(MeshKit.Box("Air Hockey Crossbar", new Vector3(legX * 2f, 0.07f, 0.09f), 0.01f)), body, c + new Vector3(0f, 0.18f, 0f));
            const float trayDepth = 0.14f, trayHeight = 0.09f;
            Vector3 trayCenter = new Vector3(0f, apronBottom - trayHeight * 0.5f - 0.02f, NearEdge + 0.08f);
            Part(table, "Puck Tray", Save(MeshKit.Box("Air Hockey Puck Tray", new Vector3(0.26f, trayHeight, trayDepth), 0.012f)), body, trayCenter);
            Part(table, "Puck Tray Neon", Save(MeshKit.Box("Air Hockey Puck Tray Neon", new Vector3(0.27f, 0.012f, 0.012f), 0.003f)), pinkGlow,
                trayCenter + new Vector3(0f, trayHeight * 0.5f - 0.006f, -trayDepth * 0.5f - 0.004f));

            // הניקוד מרחף מעל החצי הרחוק, בלי מסגרת, מעל קו הראייה אל השער של היריב
            var display = new GameObject("Score Display").transform;
            display.SetParent(root.transform, false);
            display.localPosition = new Vector3(0f, 1.78f, CenterZ + 0.45f);
            display.localRotation = Quaternion.Euler(-10f, 0f, 0f);
            FloatText(display, "You Label", "YOU", new Vector3(-0.42f, 0.17f, 0f), 0.7f, Lime);
            FloatText(display, "Opponent Label", "OPPONENT", new Vector3(0.42f, 0.17f, 0f), 0.7f, Red);
            FloatText(display, "You Score", "3", new Vector3(-0.42f, -0.04f, 0f), 2.8f, Lime);
            FloatText(display, "Opponent Score", "5", new Vector3(0.42f, -0.04f, 0f), 2.8f, Red);
            FloatText(display, "Dash", ":", new Vector3(0f, -0.03f, 0f), 2.4f, LineWhite);
            FloatText(display, "Rule", "FIRST TO 7  •  NORMAL", new Vector3(0f, -0.24f, 0f), 0.5f, LineWhite);

            // מחבטים: ליים לך, אדום ליריב, גם הידית זוהרת; פאק לבן-תכלת זוהר
            Material limeBody = Lit("Mallet Player", Lime * 0.7f, 0.1f, 0.85f, Lime * 0.55f);
            Material limeKnob = Lit("Mallet Player Knob", Lime * 0.8f, 0.1f, 0.9f, Lime * 0.35f);
            Material redBody = Lit("Mallet Opponent", Red * 0.75f, 0.1f, 0.85f, Red * 0.55f);
            Material redKnob = Lit("Mallet Opponent Knob", Red * 0.85f, 0.1f, 0.9f, Red * 0.35f);
            Material cap = Lit("Mallet Cap", new Color(0.95f, 0.95f, 1f), 0f, 0.9f, new Color(0.6f, 0.6f, 0.7f));
            Material puck = Lit("Puck", new Color(0.9f, 0.97f, 1f), 0f, 0.7f, new Color(0.75f, 0.95f, 1f) * 2.2f);
            Mesh malletBase = Save(MeshKit.Lathe("Air Hockey Mallet Base", new[]
            {
                new Vector2(0f, 0f), new Vector2(MalletRadius - 0.004f, 0f), new Vector2(MalletRadius, 0.004f), new Vector2(MalletRadius, 0.016f),
                new Vector2(MalletRadius - 0.004f, 0.022f), new Vector2(MalletRadius - 0.012f, 0.026f), new Vector2(0.024f, 0.03f), new Vector2(0f, 0.031f),
            }, 48));
            Mesh malletKnob = Save(MeshKit.Lathe("Air Hockey Mallet Knob", new[]
            {
                new Vector2(0f, 0.029f), new Vector2(0.018f, 0.029f), new Vector2(0.016f, 0.045f), new Vector2(0.0145f, 0.06f), new Vector2(0.017f, 0.068f),
                new Vector2(0.021f, 0.075f), new Vector2(0.0235f, 0.083f), new Vector2(0.0235f, 0.09f), new Vector2(0.021f, 0.097f), new Vector2(0f, 0.097f),
            }, 48));
            Mesh malletCap = Save(MeshKit.Lathe("Air Hockey Mallet Cap", new[]
            {
                new Vector2(0f, 0.097f), new Vector2(0.021f, 0.097f), new Vector2(0.017f, 0.102f), new Vector2(0.008f, 0.105f), new Vector2(0f, 0.106f),
            }, 48));
            Mesh puckMesh = Save(MeshKit.Lathe("Air Hockey Puck", new[]
            {
                new Vector2(0f, 0f), new Vector2(PuckRadius - 0.002f, 0f), new Vector2(PuckRadius, 0.002f), new Vector2(PuckRadius, 0.01f),
                new Vector2(PuckRadius - 0.002f, 0.012f), new Vector2(0f, 0.012f),
            }, 48));
            foreach (bool player in new[] { true, false })
            {
                var m = new GameObject(player ? "Player Mallet" : "Opponent Mallet").transform;
                m.SetParent(root.transform, false);
                m.localPosition = new Vector3(player ? 0.08f : -0.05f, top + 0.001f, CenterZ + (player ? -1f : 1f) * (FieldLength * 0.5f - 0.28f));
                Part(m, "Base", malletBase, player ? limeBody : redBody, Vector3.zero);
                Part(m, "Knob", malletKnob, player ? limeKnob : redKnob, Vector3.zero);
                Part(m, "Cap", malletCap, cap, Vector3.zero);
            }
            var puckGo = new GameObject("Puck").transform;
            puckGo.SetParent(root.transform, false);
            puckGo.localPosition = new Vector3(0.15f, top + 0.001f, CenterZ - 0.3f);
            Part(puckGo, "Body", puckMesh, puck, Vector3.zero);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[AirHockeyBuilder] Table built.");
        }

        private static GameObject Part(Transform parent, string name, Mesh mesh, Material material, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        private static void Text(TMPro.TextMeshPro tmp, string text, float size, Color color)
        {
            tmp.text = text; tmp.fontSize = size; tmp.color = color; tmp.fontStyle = TMPro.FontStyles.Bold;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            tmp.rectTransform.sizeDelta = new Vector2(1.2f, 0.4f);
            var neon = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Neon Sign.mat");
            if (neon != null) tmp.fontSharedMaterial = neon;
        }

        private static void FloatText(Transform parent, string name, string text, Vector3 position, float size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            Text(go.AddComponent<TMPro.TextMeshPro>(), text, size, color);
        }

        // טקסט ששוכב על המשטח, פונה למעלה, וקריא למי שעומד בצד הקרוב
        private static void FlatText(Transform parent, string name, string text, Vector3 position, float size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var tmp = go.AddComponent<TMPro.TextMeshPro>();
            Text(tmp, text, size, color);
            tmp.lineSpacing = -25f;
        }

        // משטח: מלבן עם UV על כל המשטח (u לרוחב, v לאורך, מהשער שלך לשער של היריב)
        private static Mesh FieldMesh()
        {
            float hx = FieldWidth * 0.5f, hz = FieldLength * 0.5f;
            var mesh = new Mesh { name = "Air Hockey Field" };
            mesh.SetVertices(new List<Vector3> { new Vector3(-hx, 0f, -hz), new Vector3(-hx, 0f, hz), new Vector3(hx, 0f, hz), new Vector3(hx, 0f, -hz) });
            mesh.SetUVs(0, new List<Vector2> { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) });
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            return mesh;
        }

        // מרחק (ביחידות תא) מהשפה של המשושה הקרוב, לרשת משושים
        private static float HexEdge(Vector2 p)
        {
            var r = new Vector2(1f, 1.7320508f);
            Vector2 h = r * 0.5f;
            Vector2 a = new Vector2(Mathf.Repeat(p.x, r.x), Mathf.Repeat(p.y, r.y)) - h;
            Vector2 b = new Vector2(Mathf.Repeat(p.x - h.x, r.x), Mathf.Repeat(p.y - h.y, r.y)) - h;
            Vector2 g = a.sqrMagnitude < b.sqrMagnitude ? a : b;
            g = new Vector2(Mathf.Abs(g.x), Mathf.Abs(g.y));
            float hex = Mathf.Max(g.x * 0.5f + g.y * 0.8660254f, g.x);
            return 0.5f - hex;
        }

        // המשטח: מעבר מסגול (אצלך) לכחול (אצל היריב), רשת משושים, קווים לבנים זוהרים:
        // מסגרת, קו אמצע, שני עיגולים במרכז, חצי עיגול וקרני שמש מול כל שער, ונקודות פתיחה כתומות.
        // מפת הזוהר: כל המשטח מואר חלש מבפנים (כמו לוח LED), הקווים והנקודות במלואם.
        private static Material FieldMaterial()
        {
            const int w = 1024, h = 2048;
            float ppm = w / FieldWidth;
            float hx = FieldWidth * 0.5f, hz = FieldLength * 0.5f;
            Color dot = new Color(1f, 0.38f, 0.12f);
            var albedo = new Color32[w * h];
            var glow = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float mx = (x + 0.5f) / ppm - hx, mz = (y + 0.5f) / ppm - hz;
                    float along = (mz + hz) / FieldLength, across = Mathf.Abs(mx) / hx;
                    Color baseColor = Color.Lerp(FieldNear, FieldFar, Mathf.Clamp01(along * 0.85f + across * 0.3f));
                    float edge = HexEdge(new Vector2(mx, mz) / 0.05f);
                    if (edge < 0.035f) baseColor *= 0.55f;          // שפה כהה בין משושים
                    else if (edge < 0.07f) baseColor *= 1.18f;      // והילה בהירה בתוכם
                    Color col = baseColor, g = baseColor * 0.42f;
                    void Line(float distance, float width, Color color)
                    {
                        float d = Mathf.Abs(distance) - width * 0.5f;
                        if (d < 0f) { col = color; g = color; }
                        else if (d < 0.003f) { float k = 1f - d / 0.003f; col = Color.Lerp(col, color, k * 0.6f); g = Color.Lerp(g, color, k * 0.6f); }
                    }
                    // מסגרת במרחק 2.5 ס"מ מהדופן
                    float bx = hx - 0.025f, bz = hz - 0.025f;
                    float frame = Mathf.Max(Mathf.Abs(mx) - bx, Mathf.Abs(mz) - bz);
                    if (frame < 0.003f) Line(frame, 0.006f, LineWhite);
                    float rc = Mathf.Sqrt(mx * mx + mz * mz);
                    if (rc > 0.18f) Line(mz, 0.008f, LineWhite);      // קו האמצע נעצר בעיגול, שהלוגו יהיה נקי
                    Line(rc - 0.18f, 0.008f, LineWhite);
                    Line(rc - 0.14f, 0.004f, LineWhite);
                    foreach (int end in new[] { -1, 1 })
                    {
                        float dz = mz - end * hz, dx = mx;
                        float r = Mathf.Sqrt(dx * dx + dz * dz);
                        Line(r - 0.2f, 0.008f, LineWhite);
                        Line(r - 0.31f, 0.004f, LineWhite);
                        if (r > 0.23f && r < 0.28f)
                        {
                            float angle = Mathf.Atan2(dx, -end * dz) * Mathf.Rad2Deg;   // 0 = ישר אל המרכז
                            float tick = Mathf.Repeat(angle + 5f, 12f) - 6f;
                            if (Mathf.Abs(tick) < 1.1f && Mathf.Abs(angle) < 80f) { col = LineWhite; g = LineWhite; }
                        }
                        float fz = mz - end * hz * 0.5f;
                        if (mx * mx + fz * fz < 0.026f * 0.026f) { col = dot; g = dot; }
                    }
                    albedo[y * w + x] = col;
                    glow[y * w + x] = g;
                }
            }
            Texture2D albedoTex = SaveTexture("Field Albedo", w, h, albedo);
            Texture2D glowTex = SaveTexture("Field Glow", w, h, glow);
            Material m = Lit("Field", Color.white, 0f, 0.92f, Color.white * 1.3f);
            m.SetTexture("_BaseMap", albedoTex);
            m.SetTexture("_EmissionMap", glowTex);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Texture2D SaveTexture(string name, int w, int h, Color32[] pixels)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true, false);
            tex.SetPixels32(pixels); tex.Apply();
            string path = $"{TextureDir}/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.anisoLevel = 8;   // המשטח נראה בזווית חדה
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Material Lit(string name, Color color, float metallic, float smoothness, Color? emission = null)
        {
            string path = $"{MaterialDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Smoothness", smoothness);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            }
            else m.DisableKeyword("_EMISSION");
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material Neon(string name, Color color, float intensity) => Lit(name, color * 0.6f, 0f, 0.5f, color * intensity);

        private static Mesh Save(Mesh mesh)
        {
            string path = $"{MeshDir}/{mesh.name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
            EditorUtility.CopySerialized(mesh, existing);
            return existing;
        }
    }
}
