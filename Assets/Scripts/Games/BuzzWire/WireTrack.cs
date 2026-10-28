using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace MotionArcade.Games.BuzzWire
{
    // מסלול אחד: החוט (Spline), הצינור שמרונדר סביבו, וסמני ההתחלה ונקודות הביקורת.
    // השאילתות (הנקודה הקרובה, מיקום לפי t) רצות על NativeSpline במרחב העולם: struct שנבנה פעם אחת,
    // כך שאין הקצאות זיכרון באמצע סבב.
    public class WireTrack : MonoBehaviour
    {
        [SerializeField] private string displayName = "Track";
        [SerializeField] private SplineContainer container;
        [SerializeField] private Renderer wire;
        [SerializeField] private Renderer startMarker;        // כחול: כאן מכניסים את הטבעת
        [SerializeField] private Renderer[] checkpointMarkers; // ב-25%, 50%, 75%
        [SerializeField] private Color wireColor = new Color(0.8f, 0.82f, 0.86f);
        [SerializeField] private Color touchColor = new Color(1f, 0.15f, 0.1f);
        [SerializeField] private Color pendingColor = new Color(0f, 0.85f, 1f);
        [SerializeField] private Color passedColor = new Color(0.3f, 1f, 0.4f);
        [SerializeField] private Color startColor = new Color(0.2f, 0.4f, 1f);

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        private NativeSpline native;
        private bool created;
        private MaterialPropertyBlock block;
        private bool shownTouching = true;   // כדי שהצביעה הראשונה תתבצע
        private Vector3[] markerScales;

        public string DisplayName => displayName;
        public float Length { get; private set; }

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            native = new NativeSpline(container.Spline, container.transform.localToWorldMatrix, Allocator.Persistent);
            created = true;
            Length = native.GetLength();
            markerScales = new Vector3[checkpointMarkers.Length + 1];
            for (int i = 0; i < checkpointMarkers.Length; i++) markerScales[i] = checkpointMarkers[i].transform.localScale;
            markerScales[checkpointMarkers.Length] = startMarker.transform.localScale;
            SetTouching(false);
            ShowMarkers(0f, false, 0f);
        }

        private void OnDestroy()
        {
            if (created) native.Dispose();
            created = false;
        }

        // t של הנקודה הקרובה ביותר על החוט, והנקודה עצמה (בעולם)
        public float Nearest(Vector3 worldPoint, out Vector3 point)
        {
            SplineUtility.GetNearestPoint(native, (float3)worldPoint, out float3 nearest, out float t, 8, 3);
            point = nearest;
            return t;
        }

        public Vector3 Evaluate(float t) => (Vector3)native.EvaluatePosition(Mathf.Clamp01(t));

        public void SetTouching(bool touching)
        {
            if (touching == shownTouching) return;
            shownTouching = touching;
            Color c = touching ? touchColor : wireColor;
            Paint(wire, c, touching ? c * 1.5f : c * 0.15f);
        }

        // נקודות שעברו: ירוק. השאר: תכלת. כשהטבעת יצאה מהחוט, הסמן שאליו צריך לחזור פועם.
        public void ShowMarkers(float checkpoint, bool waitingForRing, float time)
        {
            int resume = -1;   // ‎-1 = סמן ההתחלה
            for (int i = 0; i < checkpointMarkers.Length; i++)
            {
                bool passed = checkpoint >= WireProgress.Checkpoints[i] - 0.001f;
                Paint(checkpointMarkers[i], passed ? passedColor : pendingColor, (passed ? passedColor : pendingColor) * 0.6f);
                checkpointMarkers[i].transform.localScale = markerScales[i];
                if (passed) resume = i;
            }
            startMarker.transform.localScale = markerScales[checkpointMarkers.Length];
            Paint(startMarker, startColor, startColor * 0.6f);

            if (!waitingForRing) return;
            float pulse = 1f + 0.5f * (0.5f + 0.5f * Mathf.Sin(time * 8f));
            if (resume < 0) startMarker.transform.localScale = markerScales[checkpointMarkers.Length] * pulse;
            else checkpointMarkers[resume].transform.localScale = markerScales[resume] * pulse;
        }

        private void Paint(Renderer r, Color baseColor, Color emission)
        {
            r.GetPropertyBlock(block);
            block.SetColor(BaseColor, baseColor);
            block.SetColor(EmissionColor, emission);
            r.SetPropertyBlock(block);
        }
    }
}
