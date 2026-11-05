using MotionArcade.Core;
using UnityEngine;

namespace MotionArcade.UI
{
    // העיגול מול משחק פועם לאט כשאף אחד לא משחק בו ("אפשר לשחק כאן"), ונרגע לאור קבוע וחלש בזמן משחק.
    // רק המשחקים שלנו מקבלים עיגול כזה, אז הפעימה היא מה שמסמן לשחקן מה אפשר לשחק בארקייד.
    // MaterialPropertyBlock ולא עותק של החומר: חומר אחד לכל העיגולים, ובלי הקצאות בכל פריים.
    public class PlaySpotGlow : MonoBehaviour
    {
        [SerializeField] private Renderer disc;
        [SerializeField] private StationPresence presence;   // ריק = תמיד פועם (העיגול במרכז החדר)
        [SerializeField] private float period = 1.8f;
        [SerializeField] private float awayMin = 0.35f;
        [SerializeField] private float awayMax = 1.4f;
        [SerializeField] private float presentLevel = 0.3f;

        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        private MaterialPropertyBlock block;
        private Color emission;

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            emission = disc.sharedMaterial.GetColor(EmissionColor);
        }

        private void Update()
        {
            float level = presence != null && presence.Occupied
                ? presentLevel
                : Mathf.Lerp(awayMin, awayMax, 0.5f + 0.5f * Mathf.Sin(Time.time * (2f * Mathf.PI / period)));
            block.SetColor(EmissionColor, emission * level);
            disc.SetPropertyBlock(block);
        }
    }
}
