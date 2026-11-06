using UnityEngine;

namespace MotionArcade.Arcade
{
    // אור שחי: משנה את עוצמת ה-emission של רנדרר (נורה, כפתור START מהבהב, פס ניאון שפועם, שלט שמרצד).
    // MaterialPropertyBlock: בלי עותקים של חומרים. הצבע הבסיסי נלקח מהחומר עצמו, והעוצמה מוכפלת.
    public class LightPulse : MonoBehaviour
    {
        public enum Mode
        {
            Pulse,     // נושם: עולה ויורד בגל
            Blink,     // דולק / כבוי (כפתור START)
            Flicker,   // דולק רוב הזמן, מרצד לרגע (שלט ניאון ישן)
            Chase,     // חלק מרצף: נדלק כשהגל עובר בו (phase = המקום ברצף)
        }

        [SerializeField] private Renderer target;
        [SerializeField] private string colorProperty = "_EmissionColor";   // glTF: "emissiveFactor"
        [SerializeField] private Mode mode = Mode.Pulse;
        [SerializeField] private float period = 1.5f;
        [SerializeField, Range(0f, 1f)] private float phase;
        [SerializeField] private float low = 0.35f;
        [SerializeField] private float high = 1.4f;
        [SerializeField] private bool randomPhase = true;
        [SerializeField, ColorUsage(false, true)] private Color color = Color.clear;   // ריק = הצבע שבחומר (כפתור בלי זוהר משלו צריך צבע)

        private MaterialPropertyBlock block;
        private int property;
        private Color baseColor;
        private float flickerUntil, nextFlicker;

        public float Level { get; private set; }

        public void Setup(Renderer renderer, string colorName, Mode lightMode, float seconds, float startPhase, float min, float max, bool randomise, Color fixedColor = default)
        {
            target = renderer; colorProperty = colorName; mode = lightMode; period = seconds; phase = startPhase; low = min; high = max; randomPhase = randomise; color = fixedColor;
        }

        private void Awake()
        {
            if (target == null) target = GetComponent<Renderer>();
            property = Shader.PropertyToID(colorProperty);
            Material material = target.sharedMaterial;
            baseColor = color.a > 0f ? color : material != null && material.HasProperty(property) ? material.GetColor(property) : Color.white;
            block = new MaterialPropertyBlock();
            if (randomPhase) phase = Random.value;
            nextFlicker = Time.time + Random.Range(1f, 6f);
        }

        // מחזור אחד, 0..1 בזמן t (טהור, לבדיקות)
        public static float Evaluate(Mode mode, float t, float period, float phase)
        {
            float u = Mathf.Repeat(t / Mathf.Max(0.01f, period) + phase, 1f);
            switch (mode)
            {
                case Mode.Blink: return u < 0.5f ? 1f : 0f;
                case Mode.Chase: return Mathf.Clamp01(1f - u * 3.5f);   // נדלק חזק ודועך: הגל "רץ" לאורך הרצף
                default: return 0.5f + 0.5f * Mathf.Sin(u * 2f * Mathf.PI);
            }
        }

        private void Update()
        {
            float level;
            if (mode == Mode.Flicker)
            {
                if (Time.time > nextFlicker) { flickerUntil = Time.time + Random.Range(0.08f, 0.35f); nextFlicker = Time.time + Random.Range(2f, 8f); }
                level = Time.time < flickerUntil && Random.value < 0.6f ? 0.15f : 1f;
            }
            else level = Evaluate(mode, Time.time, period, phase);
            Level = level;
            block.SetColor(property, baseColor * Mathf.Lerp(low, high, level));
            target.SetPropertyBlock(block);
        }
    }
}
