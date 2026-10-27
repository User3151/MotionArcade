using MotionArcade.Core;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace MotionArcade.Games.Sandbox
{
    // משחק בדיקה לשלב 1: מטרה אחת דולקת בכל פעם, בוחרים בה (grip) ומקבלים נקודה, והיא עוברת למקום אחר.
    // הוא כאן כדי להריץ את כל המחזור (טעינה, ספירה, ניקוד, תוצאה, חזרה לאולם) לפני שיש משחק אמיתי.
    // מגיב רק לאירועי XRI (selectEntered), לא לכפתורים של בקר, כך שגם יד (pinch) תעבוד בשלב 7.
    public class SandboxGame : GameStation
    {
        [SerializeField] private XRSimpleInteractable[] targets;
        [SerializeField] private Color idleColor = new Color(0.25f, 0.28f, 0.35f);
        [SerializeField] private Color litColor = new Color(1f, 0.55f, 0.1f);

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        private Renderer[] renderers;
        private MaterialPropertyBlock block;   // צבע לכל מטרה בלי ליצור עותק של החומר
        private int lit = -1;

        protected override void Awake()
        {
            base.Awake();
            block = new MaterialPropertyBlock();
            renderers = new Renderer[targets.Length];
            for (int i = 0; i < targets.Length; i++)
            {
                renderers[i] = targets[i].GetComponentInChildren<Renderer>();
                targets[i].selectEntered.AddListener(HandleSelected);
            }
            LightUp(-1);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            foreach (XRSimpleInteractable target in targets)
                if (target != null) target.selectEntered.RemoveListener(HandleSelected);
        }

        protected override void OnPlayingStarted() => LightUp(Random.Range(0, targets.Length));

        protected override void OnPlayingEnded() => LightUp(-1);

        private void HandleSelected(SelectEnterEventArgs args)
        {
            if (lit < 0 || (Object)args.interactableObject != targets[lit]) return;
            if (!AddScore(1)) return;

            if (targets.Length < 2) return;   // מטרה אחת: היא פשוט נשארת דולקת

            // מטרה אחרת, אף פעם לא אותה אחת פעמיים ברצף
            int next = Random.Range(0, targets.Length - 1);
            if (next >= lit) next++;
            LightUp(next);
        }

        // רק המטרה הדולקת פעילה: אי אפשר לבחור מטרה כבויה, וגם לא לקבל עליה נקודה
        private void LightUp(int index)
        {
            lit = index;
            for (int i = 0; i < targets.Length; i++)
            {
                // בפריקה באמצע סבב Unity הורס את המטרות באותו פריים, לפעמים לפני ה-OnDestroy של המשחק
                if (targets[i] == null) continue;
                bool on = i == index;
                targets[i].enabled = on;
                if (renderers[i] == null) continue;
                renderers[i].GetPropertyBlock(block);
                block.SetColor(BaseColor, on ? litColor : idleColor);
                renderers[i].SetPropertyBlock(block);
            }
        }
    }
}
