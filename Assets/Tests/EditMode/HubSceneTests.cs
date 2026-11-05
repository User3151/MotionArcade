using System.Linq;
using MotionArcade.Core;
using MotionArcade.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace MotionArcade.Tests
{
    // החיבורים של האולם שקל לשבור בשקט: כפתור שמצביע על סצנה שלא ב-build, מיקסר בלי פרמטר,
    // מנהל שחסר, או אולם בלי התאורה האפויה (ואז הכל שחור, כי אין בו אור בזמן אמת).
    public class HubSceneTests
    {
        private const string HubPath = "Assets/Scenes/Hub.unity";
        private const string MixerPath = "Assets/Audio/MotionArcade.mixer";

        private Scene hub;
        private bool openedHere;

        [OneTimeSetUp]
        public void OpenHub()
        {
            hub = SceneManager.GetSceneByPath(HubPath);
            openedHere = !hub.isLoaded;
            if (openedHere) hub = EditorSceneManager.OpenScene(HubPath, OpenSceneMode.Additive);
        }

        [OneTimeTearDown]
        public void CloseHub()
        {
            // סוגרים רק אם הבדיקה פתחה אותו, כדי לא לסגור לגיא סצנה שהוא עובד עליה
            if (openedHere && hub.isLoaded) EditorSceneManager.CloseScene(hub, true);
        }

        private T Find<T>() where T : Component =>
            hub.GetRootGameObjects().Select(go => go.GetComponentInChildren<T>(true)).FirstOrDefault(c => c != null);

        [Test]
        public void Mixer_HasThreeVolumesAndGroups()
        {
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            Assert.That(mixer, Is.Not.Null);
            foreach (string parameter in new[] { VolumeSettings.MasterParameter, VolumeSettings.MusicParameter, VolumeSettings.SfxParameter })
                Assert.That(mixer.GetFloat(parameter, out _), Is.True, parameter + " is not exposed");
            Assert.That(mixer.FindMatchingGroups("Music"), Is.Not.Empty);
            Assert.That(mixer.FindMatchingGroups("SFX"), Is.Not.Empty);
        }

        [Test]
        public void Hub_HasManagers_WithTheMixer()
        {
            Assert.That(Find<SessionManager>(), Is.Not.Null);
            Assert.That(Find<SceneLoader>(), Is.Not.Null);
            var volume = Find<VolumeSettings>();
            Assert.That(volume, Is.Not.Null);
            var mixer = new SerializedObject(volume).FindProperty("mixer").objectReferenceValue;
            Assert.That(mixer, Is.SameAs(AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath)));
        }

        [Test]
        public void Loader_LoadsEveryGame_FromTheBuild()
        {
            // כל המשחקים נטענים יחד בהתחלה ונשארים בפינות שלהם; הלוח בנקודת ההתחלה בלי כפתורי משחקים
            var loader = Find<SceneLoader>();
            Assert.That(loader, Is.Not.Null);
            var inBuild = EditorBuildSettings.scenes.Where(s => s.enabled)
                .Select(s => System.IO.Path.GetFileNameWithoutExtension(s.path)).ToArray();
            Assert.That(loader.Games, Is.EquivalentTo(Games));
            foreach (string game in loader.Games) Assert.That(inBuild, Does.Contain(game), game + " is not in Build Settings");
            var menu = Find<HubMenu>();
            Assert.That(menu, Is.Not.Null);
            Assert.That(menu.GetComponentsInChildren<UnityEngine.UI.Button>(true).Select(x => x.name),
                Is.EquivalentTo(new[] { "Settings Button", "Exit Button", "Back Button" }), "settings and exit only");
        }

        [Test]
        public void Player_WalksTurnsAndTeleports_ButNotDuringARound()
        {
            var rig = hub.GetRootGameObjects().First(g => g.name.StartsWith("XR Origin")).transform;
            Transform loco = rig.Find("Locomotion");
            Assert.That(loco.Find("Move").gameObject.activeSelf, Is.True, "left stick walks");
            Assert.That(loco.Find("Teleportation").gameObject.activeSelf, Is.True, "right stick teleports");
            var move = loco.Find("Move").GetComponent<UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement.ContinuousMoveProvider>();
            Assert.That(move.moveSpeed, Is.InRange(1f, 2f), "a comfortable walk");
            Assert.That(rig.GetComponent<CharacterController>().radius, Is.GreaterThanOrEqualTo(0.2f), "keeps the head out of the machines");
            var lockC = rig.GetComponent<MotionArcade.Interaction.LocomotionLock>();
            Assert.That(lockC, Is.Not.Null);
            var locked = new SerializedObject(lockC).FindProperty("locked");
            var names = Enumerable.Range(0, locked.arraySize).Select(i => locked.GetArrayElementAtIndex(i).objectReferenceValue).ToArray();
            Assert.That(names, Has.None.Null);
            Assert.That(names, Does.Contain(move));
            Assert.That(names.OfType<UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationProvider>(), Is.Not.Empty);
            Assert.That(names.OfType<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor>().Count(), Is.EqualTo(2), "both teleport rays");
            var start = hub.GetRootGameObjects().First(g => g.name == "Arcade Room").transform.Find("Start Spot");
            Assert.That(start, Is.Not.Null, "a circle to come back to the middle");
            Assert.That(start.GetComponent<UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationAnchor>(), Is.Not.Null);
        }

        [Test]
        public void EveryGame_OpensFromItsCircle()
        {
            // עומדים על העיגול: מופיע לוח ה-START והמוזיקה מתחילה. לא עומדים: שלט "עמוד על העיגול"
            int teleport = UnityEngine.XR.Interaction.Toolkit.InteractionLayerMask.GetMask("Teleport");
            WithGames(scenes =>
            {
                foreach (Scene sc in scenes)
                {
                    Transform root = sc.GetRootGameObjects()[0].transform;
                    Transform spot = root.Find("Play Spot");
                    Assert.That(spot, Is.Not.Null, sc.name);
                    Assert.That(spot.localPosition, Is.EqualTo(Vector3.zero), sc.name + ": where the game was built to be played");
                    Assert.That(spot.localRotation, Is.EqualTo(Quaternion.identity), sc.name);
                    var anchor = spot.GetComponent<UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationAnchor>();
                    Assert.That(anchor, Is.Not.Null, sc.name);
                    Assert.That((int)anchor.interactionLayers, Is.EqualTo(teleport), sc.name + ": only the teleport ray");
                    Assert.That(anchor.matchOrientation, Is.EqualTo(UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.MatchOrientation.TargetUpAndForward), sc.name + ": lands facing the game");
                    Assert.That(spot.GetComponent<Collider>(), Is.Not.Null, sc.name);

                    var presence = root.GetComponent<StationPresence>();
                    Assert.That(presence, Is.Not.Null, sc.name);
                    var pso = new SerializedObject(presence);
                    Assert.That(pso.FindProperty("station").objectReferenceValue, Is.SameAs(root.GetComponent<GameStation>()), sc.name);
                    var present = pso.FindProperty("showWhenPresent");
                    Assert.That(present.arraySize, Is.GreaterThan(0), sc.name);
                    Assert.That(((GameObject)present.GetArrayElementAtIndex(0).objectReferenceValue).GetComponent<PhaseDock>(), Is.Not.Null, sc.name + ": the START panel");
                    Assert.That(pso.FindProperty("showWhenAway").arraySize, Is.GreaterThan(0), sc.name + ": a sign when nobody plays");
                    var music = (MotionArcade.Feedback.GameMusic)pso.FindProperty("music").objectReferenceValue;
                    Assert.That(music, Is.Not.Null, sc.name);
                    Assert.That(new SerializedObject(music).FindProperty("waitForPlayer").boolValue, Is.True, sc.name + ": four games, one music at a time");
                    Assert.That(root.GetComponentsInChildren<UnityEngine.UI.Button>(true).Any(x => x.name == "Back Button"), Is.False, sc.name + ": you leave by walking away");
                    // מה שמסמן משחק שאפשר לשחק בו (מכונות הנוי בלי אף אחד מאלה): שלט ניאון ועיגול שפועם
                    Assert.That(root.Find("Neon Sign"), Is.Not.Null, sc.name);
                    var glow = spot.GetComponent<PlaySpotGlow>();
                    Assert.That(glow, Is.Not.Null, sc.name);
                    Assert.That(new SerializedObject(glow).FindProperty("presence").objectReferenceValue, Is.SameAs(presence), sc.name + ": calms down when someone stands on it");
                }
            });
        }

        [Test]
        public void Hub_IsBakedWithoutRealtimeLights()
        {
            // Quest: אור בזמן אמת עולה ביוקר. כל המנורות אפויות, ואובייקטים זזים מקבלים אור מ-Light Probes.
            Assert.That(Lightmapping.GetLightingDataAssetForScene(hub), Is.Not.Null, "the Hub has no baked lighting");
            var lights = hub.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Light>(true)).ToArray();
            Assert.That(lights, Is.Not.Empty);
            Assert.That(lights.Where(l => l.lightmapBakeType != LightmapBakeType.Baked).Select(l => l.name), Is.Empty);
            Assert.That(Find<LightProbeGroup>(), Is.Not.Null);
        }

        public static readonly string[] Games = { "Game_Basketball", "Game_Darts", "Game_Whack", "Game_BuzzWire", "Game_AirHockey" };
        private const float RoomHalfX = 10f, RoomHalfZ = 8f;

        // כל משחק בפינה שלו: הנפח שלו (כולל הלוחות) ועוד מקום לעמוד מולו, בעולם
        private static Bounds GameArea(Scene game)
        {
            Transform root = game.GetRootGameObjects()[0].transform;
            var b = new Bounds(root.position + Vector3.up, new Vector3(1.2f, 2f, 1.2f));   // השחקן
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true)) b.Encapsulate(r.bounds);
            var corners = new Vector3[4];
            foreach (Canvas c in root.GetComponentsInChildren<Canvas>(true))
            {
                ((RectTransform)c.transform).GetWorldCorners(corners);
                foreach (Vector3 p in corners) b.Encapsulate(p);
            }
            return b;
        }

        private static void WithGames(System.Action<Scene[]> check)
        {
            var opened = new System.Collections.Generic.List<Scene>();
            var scenes = Games.Select(n =>
            {
                Scene sc = SceneManager.GetSceneByPath($"Assets/Scenes/{n}.unity");
                if (!sc.isLoaded) { sc = EditorSceneManager.OpenScene($"Assets/Scenes/{n}.unity", OpenSceneMode.Additive); opened.Add(sc); }
                return sc;
            }).ToArray();
            try { check(scenes); }
            finally { foreach (Scene sc in opened) EditorSceneManager.CloseScene(sc, true); }
        }

        [Test]
        public void Games_StandInsideTheRoom_ApartFromEachOther()
        {
            WithGames(scenes =>
            {
                var areas = scenes.Select(GameArea).ToArray();
                for (int i = 0; i < areas.Length; i++)
                {
                    Bounds b = areas[i];
                    Assert.That(Mathf.Max(-b.min.x, b.max.x), Is.LessThanOrEqualTo(RoomHalfX), Games[i] + " is in a wall");
                    Assert.That(Mathf.Max(-b.min.z, b.max.z), Is.LessThanOrEqualTo(RoomHalfZ), Games[i] + " is in a wall");
                    for (int j = i + 1; j < areas.Length; j++) Assert.That(b.Intersects(areas[j]), Is.False, Games[i] + " and " + Games[j]);
                    // השחקן מתחיל במרכז: אף משחק לא עליו
                    Assert.That(b.Contains(new Vector3(0f, 1f, 0f)), Is.False, Games[i]);
                }
            });
        }

        [Test]
        public void ArcadeMachines_StayOffTheGamesAndInsideTheRoom()
        {
            // מכונות הנוי על הקירות שבין המשחקים: אף אחת לא בתוך משחק או במקום שעומדים בו מולו
            var group = hub.GetRootGameObjects().First(g => g.name == "Arcade Room").transform.Find("Arcade Machines");
            Assert.That(group, Is.Not.Null);
            Assert.That(group.childCount, Is.GreaterThanOrEqualTo(12), "a room that looks full");
            WithGames(scenes =>
            {
                var areas = scenes.Select(GameArea).ToArray();
                foreach (var box in group.GetComponentsInChildren<BoxCollider>())
                {
                    Bounds b = box.bounds;
                    for (int i = 0; i < areas.Length; i++) Assert.That(b.Intersects(areas[i]), Is.False, box.name + " stands in " + Games[i]);
                    Assert.That(Mathf.Max(-b.min.x, b.max.x), Is.LessThanOrEqualTo(RoomHalfX), box.name + " is in a wall");
                    Assert.That(Mathf.Max(-b.min.z, b.max.z), Is.LessThanOrEqualTo(RoomHalfZ), box.name + " is in a wall");
                    Assert.That(b.min.y, Is.GreaterThanOrEqualTo(-0.01f), box.name + " sinks into the floor");
                }
            });
        }

        [Test]
        public void ArcadeMachines_AreLightAndLitByProbes()
        {
            // מודלים מ-Sketchfab "נאפים" (ArcadePropBaker): חלק לכל חומר ו-Mesh LOD; האולם אפוי, אז הם מקבלים אור מ-Light Probes
            var prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Arcade" })
                .Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
            Assert.That(prefabs.Length, Is.GreaterThanOrEqualTo(6));
            foreach (GameObject prefab in prefabs)
            {
                var renderers = prefab.GetComponentsInChildren<MeshRenderer>();
                Assert.That(renderers.Length, Is.InRange(1, 25), prefab.name + ": one part per material");
                Assert.That(prefab.GetComponent<BoxCollider>(), Is.Not.Null, prefab.name);
                foreach (MeshRenderer r in renderers)
                {
                    Assert.That(r.receiveGI, Is.EqualTo(ReceiveGI.LightProbes), prefab.name + "/" + r.name);
                    Assert.That(r.lightProbeUsage, Is.EqualTo(UnityEngine.Rendering.LightProbeUsage.BlendProbes), prefab.name + "/" + r.name);
                    Mesh mesh = r.GetComponent<MeshFilter>().sharedMesh;
                    Assert.That(mesh, Is.Not.Null, prefab.name + "/" + r.name);
                    if (mesh.GetIndexCount(0) / 3 > 3000) Assert.That(mesh.lodCount, Is.GreaterThan(1), prefab.name + "/" + r.name + " has no Mesh LOD");
                }
            }
        }
    }
}
