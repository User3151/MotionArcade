using UnityEngine;

namespace MotionArcade.Arcade
{
    // מסך "דמו" של מכונת ארקייד (attract mode): המכונות בחדר נראו כבויות (גיא: "כאילו הן לא עובדות").
    // הטקסטורה היא רשת של פריימים (columns x rows): קודם פריימי משחק בלולאה, ואז מסך שם המשחק עם INSERT COIN
    // מהבהב, כמו מכונה אמיתית שמחכה לשחקן. מחליפים פריים דרך _BaseMap_ST (גם ה-emission של URP Lit משתמש בו),
    // ב-MaterialPropertyBlock: חומר אחד לכל המסכים של אותו משחק. כל מכונה מתחילה בנקודה אחרת, כדי שלא יזוזו יחד.
    public class AttractScreen : MonoBehaviour
    {
        [SerializeField] private Renderer screen;
        [SerializeField] private int columns = 4;
        [SerializeField] private int rows = 3;
        [SerializeField] private int gameFrames = 9;
        [SerializeField] private int titleFrames = 3;
        [SerializeField] private int gameLoops = 3;          // כמה פעמים המשחק רץ לפני מסך השם
        [SerializeField] private float frameSeconds = 0.14f;
        [SerializeField] private float titleSeconds = 0.6f;

        private static readonly int BaseMapSt = Shader.PropertyToID("_BaseMap_ST");
        private MaterialPropertyBlock block;
        private int step;          // מיקום ברצף כולו (משחק x לולאות, ואז השם)
        private float timer;

        public int Frame { get; private set; }

        private int Length => gameFrames * gameLoops + titleFrames;

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            if (screen == null) screen = GetComponent<Renderer>();
            step = Random.Range(0, Length);
            timer = Random.Range(0f, frameSeconds);
            Apply();
        }

        private void Update()
        {
            timer += Time.deltaTime;
            float duration = step < gameFrames * gameLoops ? frameSeconds : titleSeconds;
            if (timer < duration) return;
            timer -= duration;
            step = (step + 1) % Length;
            Apply();
        }

        public static int FrameAt(int step, int gameFrames, int gameLoops) =>
            step < gameFrames * gameLoops ? step % gameFrames : gameFrames + (step - gameFrames * gameLoops);

        private void Apply()
        {
            Frame = FrameAt(step, gameFrames, gameLoops);
            int col = Frame % columns, row = Frame / columns;
            float sx = 1f / columns, sy = 1f / rows;
            // שורה 0 למעלה בתמונה; ב-UV ה-0 למטה
            block.SetVector(BaseMapSt, new Vector4(sx, sy, col * sx, 1f - (row + 1) * sy));
            screen.SetPropertyBlock(block);
        }
    }
}
