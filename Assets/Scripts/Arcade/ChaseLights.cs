using UnityEngine;

namespace MotionArcade.Arcade
{
    // שורת נורות שהאור "רץ" לאורכה, כמו מסגרת של שלט בארקייד (גיא: חסרים אורות מהבהבים).
    // הנורות מחולקות ל-groups קבוצות לסירוגין (0,1,2,0,1,2...), ולכל קבוצה חומר משלה שמשתנה בזמן:
    // שלושה חומרים לכל השורה, אז הנורות נשארות ב-SRP Batcher (לא MaterialPropertyBlock לכל נורה).
    public class ChaseLights : MonoBehaviour
    {
        [SerializeField] private Renderer[] bulbs;
        [SerializeField] private Material bulbMaterial;
        [SerializeField, ColorUsage(false, true)] private Color color = new Color(4f, 2.6f, 0.6f);
        [SerializeField, Range(2, 6)] private int groups = 3;
        [SerializeField] private float stepSeconds = 0.18f;
        [SerializeField, Range(0f, 1f)] private float dim = 0.08f;

        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        private Material[] materials;

        public int Lit { get; private set; }   // איזו קבוצה דולקת עכשיו

        private void Awake()
        {
            materials = new Material[groups];
            for (int g = 0; g < groups; g++) materials[g] = new Material(bulbMaterial) { name = bulbMaterial.name + " " + g };
            for (int i = 0; i < bulbs.Length; i++) if (bulbs[i] != null) bulbs[i].sharedMaterial = materials[i % groups];
            Lit = -1;
        }

        private void OnDestroy()
        {
            if (materials == null) return;
            foreach (Material m in materials) Destroy(m);
        }

        public static int GroupAt(float time, float step, int groups) => (int)(time / Mathf.Max(0.01f, step)) % groups;

        private void Update()
        {
            int lit = GroupAt(Time.time, stepSeconds, groups);
            if (lit == Lit) return;
            Lit = lit;
            for (int g = 0; g < groups; g++) materials[g].SetColor(EmissionColor, g == lit ? color : color * dim);
        }
    }
}
