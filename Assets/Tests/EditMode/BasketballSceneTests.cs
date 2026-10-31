using System.Linq;
using MotionArcade.Feedback;
using MotionArcade.Games.Basketball;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace MotionArcade.Tests
{
    // החיבורים של מכונת הכדורסל: כל מה שאם נשבר, הכדור עובר דרך הטבעת, נופל דרך הרצפה או שותק.
    public class BasketballSceneTests
    {
        private const string ScenePath = "Assets/Scenes/Game_Basketball.unity";

        private Scene scene;
        private bool openedHere;
        private BasketballGame game;

        [OneTimeSetUp]
        public void Open()
        {
            scene = SceneManager.GetSceneByPath(ScenePath);
            openedHere = !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            game = scene.GetRootGameObjects().Select(r => r.GetComponentInChildren<BasketballGame>(true)).First(g => g != null);
        }

        [OneTimeTearDown]
        public void Close()
        {
            if (openedHere && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
        }

        private SerializedObject Game => new SerializedObject(game);

        [Test]
        public void AimAssist_AimsAtTheRimCentre()
        {
            var assist = (HoopAssist)Game.FindProperty("assist").objectReferenceValue;
            Assert.That(assist, Is.Not.Null, "the game hands the assist to every ball");
            var aim = (Transform)new SerializedObject(assist).FindProperty("aim").objectReferenceValue;
            Assert.That(aim, Is.Not.Null);
            Assert.That(aim.name, Is.EqualTo("Hoop"));
            Assert.That(assist.Strength, Is.InRange(0.5f, 0.9f));
        }

        [Test]
        public void ReboundsAreSofter()
        {
            // הכדור חזר מהלוח ומהקיר "מהר מאוד, לא כמו כדור עם משקל": ממוצע עם הכדור, לוח 0.5 וטבעת 0.45
            float Bounce(string name) => AssetDatabase.LoadAssetAtPath<PhysicsMaterial>("Assets/Physics/" + name + ".physicMaterial").bounciness;
            float ball = Bounce("Ball");
            Assert.That((ball + Bounce("Backboard")) * 0.5f, Is.LessThanOrEqualTo(0.55f));
            Assert.That((ball + Bounce("Rim")) * 0.5f, Is.LessThanOrEqualTo(0.5f));
            Assert.That(ball * 0.5f, Is.LessThanOrEqualTo(0.35f), "walls and floor without a material");
        }

        [Test]
        public void EveryBallHasASlot_AndIsFastPhysics()
        {
            var balls = Game.FindProperty("balls");
            var slots = Game.FindProperty("traySlots");
            Assert.That(balls.arraySize, Is.GreaterThan(0));
            Assert.That(slots.arraySize, Is.GreaterThanOrEqualTo(balls.arraySize));
            for (int i = 0; i < balls.arraySize; i++)
            {
                var ball = (Ball)balls.GetArrayElementAtIndex(i).objectReferenceValue;
                Assert.That(ball, Is.Not.Null, $"ball {i}");
                var body = ball.GetComponent<Rigidbody>();
                // כדור של 6 מ'/ש' עובר 7 ס"מ בצעד פיזיקה: בלי Continuous הוא יכול לעבור דרך טבעת בעובי 2 ס"מ
                Assert.That(body.collisionDetectionMode, Is.EqualTo(CollisionDetectionMode.ContinuousDynamic), ball.name);
                Assert.That(body.interpolation, Is.EqualTo(RigidbodyInterpolation.Interpolate), ball.name);
                var grab = ball.GetComponent<XRGrabInteractable>();
                // זריקה משלנו (החלון האחרון), תפיסה רק ביד, ותמיד באותה נקודה מול כף היד
                Assert.That(grab.throwOnDetach, Is.False, ball.name + ": ControllerThrow throws, not XRI");
                Assert.That(ball.GetComponent<MotionArcade.Interaction.ControllerThrow>(), Is.Not.Null, ball.name);
                Assert.That(ball.GetComponent<MotionArcade.Interaction.NearOnlyGrab>(), Is.Not.Null, ball.name);
                Assert.That(grab.useDynamicAttach, Is.False, ball.name);
                Assert.That(grab.attachTransform, Is.Not.Null.And.Not.SameAs(ball.transform), ball.name);
                Assert.That(ball.GetComponent<SphereCollider>().sharedMaterial, Is.Not.Null, ball.name + " has no physic material");
                // מהמדידה: מהירות מהבקר (לא מהכדור, שהצלפת היד הוסיפה לו 33%), וסיבוב לאחור.
                // המכפיל: 1.3 בסשן השני יצא 20% חזק מדי בממוצע (46 זריקות), אז 1.1
                var thrower = new SerializedObject(ball.GetComponent<MotionArcade.Interaction.ControllerThrow>());
                Assert.That(thrower.FindProperty("source").enumValueIndex, Is.EqualTo(0), ball.name);
                Assert.That(thrower.FindProperty("velocityScale").floatValue, Is.EqualTo(1.1f).Within(1e-4f), ball.name);
                Assert.That(thrower.FindProperty("backspin").floatValue, Is.GreaterThan(0f), ball.name);
            }
        }

        [Test]
        public void SlotsStartInsideTheTray()
        {
            var tray = Game.FindProperty("tray").boundsValue;
            var machine = Game.FindProperty("machine").boundsValue;
            var slots = Game.FindProperty("traySlots");
            for (int i = 0; i < slots.arraySize; i++)
            {
                Vector3 p = ((Transform)slots.GetArrayElementAtIndex(i).objectReferenceValue).position;
                Assert.That(tray.Contains(p), Is.True, $"slot {i} at {p} is outside the tray");
                Assert.That(machine.Contains(p), Is.True);
            }
        }

        [Test]
        public void BasketCheck_UsesTheRealRim()
        {
            // הסל נבדק במישור הטבעת, ברדיוס שלה: מרכז ה-Transform חייב להיות מרכז הטבעת, בלי scale ובלי הטיה
            var ring = (Transform)Game.FindProperty("ring").objectReferenceValue;
            Assert.That(ring, Is.Not.Null);
            Assert.That(ring.lossyScale.x, Is.EqualTo(1f).Within(1e-4f), "the radius is in metres");
            Assert.That(Vector3.Angle(ring.up, Vector3.up), Is.LessThan(0.5f));
            // טבעת עגולה (טורוס) ולא 16 מקטעים ישרים: הכדור פוגע בה כמו בטבעת אמיתית
            var rim = ring.Find("Rim").GetComponent<MeshCollider>();
            Assert.That(rim, Is.Not.Null);
            Assert.That(rim.convex, Is.False, "a ring, not its convex hull");
            Assert.That(ring.Find("Rim").childCount, Is.EqualTo(0), "the old capsule segments are gone");
            float radius = Game.FindProperty("ringRadius").floatValue;
            float min = float.MaxValue, max = 0f, top = float.MinValue;
            foreach (Vector3 v in rim.sharedMesh.vertices)
            {
                Vector3 p = ring.InverseTransformPoint(rim.transform.TransformPoint(v));
                float d = new Vector2(p.x, p.z).magnitude;
                min = Mathf.Min(min, d); max = Mathf.Max(max, d); top = Mathf.Max(top, p.y);
            }
            Assert.That((min + max) * 0.5f, Is.EqualTo(radius).Within(0.002f), "the tube centre is the basket radius");
            Assert.That((max - min) * 0.5f, Is.InRange(0.007f, 0.011f), "a rim rod of about 16-20 mm");
            Assert.That(top, Is.EqualTo((max - min) * 0.5f).Within(0.002f), "centred on the ring plane");
            // החיישנים הישנים (מעל ומתחת לטבעת) הוסרו: כדור שעבר בטבעת פספס את התחתון ולא נספר
            Assert.That(ring.Find("Sensor Top"), Is.Null);
            Assert.That(ring.Find("Sensor Bottom"), Is.Null);
        }

        [Test]
        public void TheNetIsLive_AndKnowsEveryBall()
        {
            var ring = (Transform)Game.FindProperty("ring").objectReferenceValue;
            var net = ring.Find("Net").GetComponent<HoopNet>();
            Assert.That(net, Is.Not.Null, "a simulated net, not a drawn one");
            Assert.That(net.transform.localPosition, Is.EqualTo(Vector3.zero), "hangs from the ring plane");
            var balls = new SerializedObject(net).FindProperty("balls");
            var gameBalls = Game.FindProperty("balls");
            Assert.That(balls.arraySize, Is.EqualTo(gameBalls.arraySize));
            for (int i = 0; i < balls.arraySize; i++)
                Assert.That(balls.GetArrayElementAtIndex(i).objectReferenceValue,
                    Is.SameAs(((Component)gameBalls.GetArrayElementAtIndex(i).objectReferenceValue).GetComponent<Rigidbody>()));
            Assert.That(net.GetComponent<Collider>(), Is.Null, "the net touches balls only through HoopNet, so it cannot bounce them");
            var material = net.GetComponent<MeshRenderer>().sharedMaterial;
            Assert.That(material.GetFloat("_Cull"), Is.EqualTo(0f), "strands seen from both sides");
        }

        [Test]
        public void TheBall_LooksLikeABasketball()
        {
            var ball = (Ball)Game.FindProperty("balls").GetArrayElementAtIndex(0).objectReferenceValue;
            var material = ball.GetComponent<MeshRenderer>().sharedMaterial;
            Assert.That(material.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
            Assert.That(material.GetTexture("_BaseMap"), Is.Not.Null, "orange with black seams");
            Assert.That(material.GetTexture("_BumpMap"), Is.Not.Null, "pebbled grip and seam grooves");
            Assert.That(ball.GetComponent<Rigidbody>().angularDamping, Is.LessThanOrEqualTo(0.15f), "the backspin lasts to the hoop, and shows");
        }

        [Test]
        public void SoundsLikeAnArena()
        {
            // כמו ב-Just Hoops: צלצול טבעת, חבטה בלוח, רשת, ותשואות קהל על סל
            var ring = (Transform)Game.FindProperty("ring").objectReferenceValue;
            var rim = ring.Find("Rim").GetComponent<SurfaceSound>();
            Assert.That(rim, Is.Not.Null);
            Assert.That(rim.Kind, Is.EqualTo(SurfaceSound.Part.Rim));
            Assert.That(rim.Pick(), Is.Not.Null);
            var board = ring.Find("Backboard").GetComponent<SurfaceSound>();
            Assert.That(board, Is.Not.Null);
            Assert.That(board.Kind, Is.EqualTo(SurfaceSound.Part.Backboard));
            Assert.That(board.Pick(), Is.Not.Null);
            // הקוליידר של הטבעת מוצא את הצליל שלה (ImpactSound מחפש בהורים)
            Assert.That(ring.Find("Rim").GetComponent<Collider>().GetComponentInParent<SurfaceSound>(), Is.SameAs(rim));

            Assert.That(Game.FindProperty("swishClip").objectReferenceValue, Is.Not.Null);
            var crowd = (AudioSource)Game.FindProperty("crowd").objectReferenceValue;
            Assert.That(crowd, Is.Not.Null);
            Assert.That(crowd.spatialBlend, Is.EqualTo(0f), "the crowd is all around");
            Assert.That(Game.FindProperty("cheerClips").arraySize, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void TheGameIsLouderThanTheCrowd()
        {
            // גיא: הקהל רעש מדי ביחס לכדורים, לסל ולטיימר. הטבעת 3.3 מ' מהשחקן: עם minDistance 1 היא נשמעה בשליש עוצמה,
            // ותשואות ב-2D ב-0.8 הסתירו אותה. עכשיו הכדור והטבעת בעוצמה מלאה מהעמדה, והקהל מאחוריהם
            var ring = (Transform)Game.FindProperty("ring").objectReferenceValue;
            var hoop = ring.GetComponentInParent<AudioSource>();
            Assert.That(hoop, Is.Not.Null);
            Assert.That(hoop.minDistance, Is.GreaterThanOrEqualTo(3.5f), "swish at full volume from the throwing spot");
            var balls = Game.FindProperty("balls");
            for (int i = 0; i < balls.arraySize; i++)
                Assert.That(((Component)balls.GetArrayElementAtIndex(i).objectReferenceValue).GetComponent<AudioSource>().minDistance,
                    Is.GreaterThanOrEqualTo(3.5f), "rim and backboard hits play on the ball, at the hoop");
            Assert.That(((AudioSource)Game.FindProperty("crowd").objectReferenceValue).volume, Is.LessThanOrEqualTo(0.5f), "cheers");
            var roots = scene.GetRootGameObjects();
            foreach (var ambience in roots.SelectMany(r => r.GetComponentsInChildren<GameMusic>(true)))
                Assert.That(new SerializedObject(ambience).FindProperty("volume").floatValue, Is.LessThanOrEqualTo(0.35f), "crowd ambience");
            foreach (var ticks in roots.SelectMany(r => r.GetComponentsInChildren<CountdownTicks>(true)))
                Assert.That(new SerializedObject(ticks).FindProperty("volume").floatValue, Is.EqualTo(1f), "the timer above the crowd");
        }

        [Test]
        public void EverySoundGoesThroughTheSfxGroup()
        {
            // אחרת סליידר ה-EFFECTS בהגדרות לא משפיע עליו
            var sources = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<AudioSource>(true)).ToArray();
            Assert.That(sources, Is.Not.Empty);
            foreach (var s in sources)
                Assert.That(s.outputAudioMixerGroup != null && s.outputAudioMixerGroup.name == "SFX", Is.True, s.name + " is not routed to SFX");
            foreach (var impact in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ImpactSound>(true)))
                Assert.That(new SerializedObject(impact).FindProperty("clip").objectReferenceValue, Is.Not.Null, impact.name);
        }

        [Test]
        public void ControlsPanel_StartsInFrontOfThePlayer()
        {
            // גיא לא ראה את הלוח כשהיה רק בצד: לפני START הוא חייב להיות מול השחקן, ובזמן סבב בצד
            var dock = scene.GetRootGameObjects().Select(r => r.GetComponentInChildren<MotionArcade.UI.PhaseDock>(true)).First(d => d != null);
            var so = new SerializedObject(dock);
            Assert.That(so.FindProperty("station").objectReferenceValue, Is.SameAs(game));
            var idle = (Transform)so.FindProperty("idlePose").objectReferenceValue;
            var running = (Transform)so.FindProperty("runningPose").objectReferenceValue;
            Assert.That(idle, Is.Not.Null);
            Assert.That(running, Is.Not.Null);
            Assert.That(Mathf.Abs(idle.position.x), Is.LessThan(0.1f), "centred");
            Assert.That(idle.position.z, Is.InRange(0.6f, 1.5f), "within arm's reach of the ray, in front of the player");
            Assert.That(Mathf.Abs(running.position.x), Is.LessThan(0.1f), "stays in view during play (not moved to the side)");
            Assert.That(new SerializedObject(dock).FindProperty("runningScale").floatValue, Is.LessThan(0.8f), "smaller during play");
        }

        [Test]
        public void NoRealtimeLightsInTheGame()
        {
            // המשחק מקבל אור מהאולם האפוי (light probes). אור בזמן אמת כאן היה עולה ביוקר ב-Quest.
            Assert.That(scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Light>(true)), Is.Empty);
        }
    }
}
