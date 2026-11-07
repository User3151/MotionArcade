using System.Linq;
using MotionArcade.Core;
using MotionArcade.Games.AirHockey;
using MotionArcade.Games.Basketball;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MotionArcade.Tests
{
    // ההערות של גיא מבדיקת הקסדה של 2026-11-07
    public class BasketballChallengeTests
    {
        [Test]
        public void TenIn30_CompletesOnTheTenthBasket()
        {
            var c = new BasketballChallenge(BasketballMode.TenIn30);
            Assert.That(c.Current.Seconds, Is.EqualTo(30f));
            for (int i = 0; i < 9; i++) Assert.That(c.Basket(false, i * 2f), Is.False);
            c.Miss();
            Assert.That(c.Progress, Is.EqualTo(9), "a miss does not cost a basket here");
            Assert.That(c.Basket(false, 21.5f), Is.True);
            Assert.That(c.Complete, Is.True);
            Assert.That(c.CompletedAt, Is.EqualTo(21.5f));
            Assert.That(c.Basket(false, 22f), Is.False, "nothing counts after it is done");
        }

        [Test]
        public void FiveInARow_AMissStartsAgain()
        {
            var c = new BasketballChallenge(BasketballMode.FiveInARow);
            for (int i = 0; i < 4; i++) c.Basket(false, i);
            c.Miss();
            Assert.That(c.Progress, Is.EqualTo(0));
            Assert.That(c.BestStreak, Is.EqualTo(4));
            for (int i = 0; i < 4; i++) Assert.That(c.Basket(true, 10 + i), Is.False);
            Assert.That(c.Basket(false, 20f), Is.True);
        }

        [Test]
        public void ThreeSwishes_CountOnlyCleanBaskets()
        {
            var c = new BasketballChallenge(BasketballMode.ThreeSwishes);
            c.Basket(false, 1f); c.Basket(true, 2f); c.Basket(false, 3f); c.Basket(true, 4f);
            Assert.That(c.Progress, Is.EqualTo(2));
            Assert.That(c.Basket(true, 5f), Is.True);
        }

        [Test]
        public void Classic_NeverCompletes_AndRecordsAreTheRightWayRound()
        {
            var c = new BasketballChallenge(BasketballMode.Classic);
            for (int i = 0; i < 40; i++) Assert.That(c.Basket(true, i), Is.False);
            Assert.That(BasketballChallenge.Better(BasketballMode.Classic, 30f, 20f, true), Is.True, "more points");
            Assert.That(BasketballChallenge.Better(BasketballMode.TenIn30, 18f, 20f, true), Is.True, "less time");
            Assert.That(BasketballChallenge.Better(BasketballMode.TenIn30, 25f, 20f, true), Is.False);
            Assert.That(BasketballChallenge.Better(BasketballMode.TenIn30, 25f, 0f, false), Is.True, "first record");
        }

        [Test]
        public void TheRoundLength_ChangesOnlyBetweenRounds()
        {
            var clock = new RoundClock(3f, 60f);
            Assert.That(clock.SetRoundSeconds(30f), Is.True);
            clock.Start(); clock.Tick(3.1f);
            Assert.That(clock.Phase, Is.EqualTo(RoundPhase.Playing));
            Assert.That(clock.TimeLeft, Is.LessThanOrEqualTo(30f));
            Assert.That(clock.SetRoundSeconds(60f), Is.False, "not in the middle of a round");
        }
    }

    public class PlayTestSceneTests
    {
        private static T InScene<T>(string path, out Scene scene, out bool openedHere) where T : Component
        {
            scene = SceneManager.GetSceneByPath(path);
            openedHere = !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            return scene.GetRootGameObjects().Select(r => r.GetComponentInChildren<T>(true)).First(x => x != null);
        }

        private static void Close(Scene scene, bool openedHere) { if (openedHere) EditorSceneManager.CloseScene(scene, true); }

        [Test]
        public void Basketball_HasAModeButton_ForTheChallenges()
        {
            var game = InScene<BasketballGame>("Assets/Scenes/Game_Basketball.unity", out var sc, out bool opened);
            try
            {
                var so = new SerializedObject(game);
                foreach (string f in new[] { "modeButton", "modeLabel", "modeInfo", "bestText" })
                    Assert.That(so.FindProperty(f).objectReferenceValue, Is.Not.Null, f);
                // גיא: צליל מספק של קליעה. פעמון על סל, ארפג'ו על סוויש, פנפרה על אתגר שהושלם
                foreach (string f in new[] { "chime", "basketChime", "swishChime", "challengeChime" })
                    Assert.That(so.FindProperty(f).objectReferenceValue, Is.Not.Null, f);
                var chime = (AudioSource)so.FindProperty("chime").objectReferenceValue;
                Assert.That(chime.outputAudioMixerGroup != null && chime.outputAudioMixerGroup.name == "SFX", Is.True);
                var button = (UnityEngine.UI.Button)so.FindProperty("modeButton").objectReferenceValue;
                Assert.That(button.transform.IsChildOf(game.transform.Find("Controls")), Is.True, "on the game's panel");
            }
            finally { Close(sc, opened); }
        }

        [Test]
        public void AirHockey_ThePuckHasADot_AndTheCountdownHidesOutsideIt()
        {
            var game = InScene<AirHockeyGame>("Assets/Scenes/Game_AirHockey.unity", out var sc, out bool opened);
            try
            {
                var so = new SerializedObject(game);
                var countdown = (GameObject)so.FindProperty("countdown").objectReferenceValue;
                var dash = (GameObject)so.FindProperty("dash").objectReferenceValue;
                Assert.That(countdown, Is.Not.Null); Assert.That(dash, Is.Not.Null);
                Assert.That(countdown.activeSelf, Is.False, "the 3 between the scores (Guy's screenshot)");
                Assert.That(dash.activeSelf, Is.True);
                var spin = (Transform)so.FindProperty("puckSpin").objectReferenceValue;
                Assert.That(spin, Is.Not.Null);
                var dot = spin.Find("Dot");
                Assert.That(dot, Is.Not.Null, "a dot on the puck");
                Assert.That(new Vector2(dot.localPosition.x, dot.localPosition.z).magnitude, Is.GreaterThan(0.01f), "off-centre, so the spin shows");
            }
            finally { Close(sc, opened); }
        }

        [Test]
        public void TheRig_DoesNotTurnAround_AndItsBodyCollides()
        {
            var hub = SceneManager.GetSceneByPath("Assets/Scenes/Hub.unity");
            bool opened = !hub.isLoaded;
            if (opened) hub = EditorSceneManager.OpenScene("Assets/Scenes/Hub.unity", OpenSceneMode.Additive);
            try { CheckRig(hub); } finally { Close(hub, opened); }
        }

        private static void CheckRig(Scene hub)
        {
            var rig = hub.GetRootGameObjects().First(g => g.name.StartsWith("XR Origin"));
            foreach (var mb in rig.GetComponentsInChildren<MonoBehaviour>(true).Where(m => m.GetType().Name.EndsWith("TurnProvider")))
                Assert.That(new SerializedObject(mb).FindProperty("m_EnableTurnAround").boolValue, Is.False, mb.GetType().Name + ": stick down does not spin 180");
            var body = rig.GetComponent<MotionArcade.Interaction.BodyCollision>();
            Assert.That(body, Is.Not.Null, "Guy walked through the machines");
            var so = new SerializedObject(body);
            foreach (string f in new[] { "origin", "body", "probe" }) Assert.That(so.FindProperty(f).objectReferenceValue, Is.Not.Null, f);
            var probe = (CapsuleCollider)so.FindProperty("probe").objectReferenceValue;
            Assert.That(probe.isTrigger, Is.True, "the probe never pushes anything itself");
            Assert.That(so.FindProperty("solid").intValue & 1, Is.EqualTo(1), "machines are on Default");
        }
    }
}
