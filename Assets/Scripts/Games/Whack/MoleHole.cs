using UnityEngine;

namespace MotionArcade.Games.Whack
{
    // חור אחד עם החפרפרת שלו (12 חורים = 12 חפרפרות קבועות, בלי Instantiate באמצע סבב).
    // ציר ‎+Z‎ של ה-Transform הזה הוא הנורמל של החור, החוצה לכיוון השחקן; החפרפרת עולה לאורכו.
    public class MoleHole : MonoBehaviour
    {
        [SerializeField] private Transform mole;          // שורש החפרפרת (גוף, עיניים, אף)
        [SerializeField] private Collider moleCollider;   // Trigger; פעיל רק כשאפשר לפגוע בה
        [SerializeField] private Renderer body;
        [SerializeField] private Color brown = new Color(0.45f, 0.3f, 0.18f);
        [SerializeField] private Color red = new Color(0.9f, 0.12f, 0.1f);
        [SerializeField] private float hiddenDepth = -0.065f;   // בתוך הצינור
        [SerializeField] private float upHeight = 0.04f;        // בחוץ
        [SerializeField] private AudioSource source;
        [SerializeField] private AudioClip popClip;

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        private readonly MoleMotion motion = new MoleMotion();
        private MaterialPropertyBlock block;
        private Renderer[] renderers;
        private WhackGame game;

        public MoleMotion Motion => motion;
        public Vector3 Normal => transform.forward;
        public Vector3 MolePosition => mole.position;

        public void Init(WhackGame owner)
        {
            game = owner;
            motion.Escaped += HandleEscaped;
        }

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            renderers = mole.GetComponentsInChildren<Renderer>(true);
            Apply();
        }

        private void OnDestroy() => motion.Escaped -= HandleEscaped;

        private void HandleEscaped()
        {
            if (game != null) game.HandleEscaped(this);
        }

        public void Spawn(float upSeconds, bool isRed)
        {
            motion.Spawn(upSeconds, isRed);
            body.GetPropertyBlock(block);
            block.SetColor(BaseColor, isRed ? red : brown);
            body.SetPropertyBlock(block);
            PlayOneShot(popClip);
        }

        public void Hide()
        {
            motion.Hide();
            Apply();
        }

        public void PlayOneShot(AudioClip clip)
        {
            if (source != null && clip != null) source.PlayOneShot(clip);
        }

        private void Update()
        {
            if (!motion.IsBusy) return;
            motion.Tick(Time.deltaTime);
            Apply();
        }

        private void Apply()
        {
            float h = Mathf.SmoothStep(0f, 1f, motion.Height);
            mole.localPosition = new Vector3(0f, 0f, Mathf.Lerp(hiddenDepth, upHeight, h));
            // מכה: החפרפרת "נמעכת" לאורך הנורמל בזמן שהיא נכנסת
            mole.localScale = motion.Current == MoleMotion.State.Bonked ? new Vector3(1.15f, 1.15f, 0.55f) : Vector3.one;
            moleCollider.enabled = motion.IsHittable;
            bool visible = motion.IsBusy;
            for (int i = 0; i < renderers.Length; i++) renderers[i].enabled = visible;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (game == null || !motion.IsHittable) return;
            Rigidbody rb = other.attachedRigidbody;
            if (rb == null || !rb.TryGetComponent(out Mallet mallet) || other != mallet.Head) return;
            // רק הרכיב של המהירות שנכנס לתוך החור: נגיעה מהצד או פטיש שעומד במקום לא נחשבים מכה
            float intoHole = Vector3.Dot(mallet.HeadVelocity, -Normal);
            game.HandleStrike(this, mallet, intoHole);
        }
    }
}
