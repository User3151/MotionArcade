using System.Collections.Generic;
using MotionArcade.Core;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace MotionArcade.Interaction
{
    // הגוף של השחקן מול העולם (גיא, 2026-11-07):
    // 1. "אפשר לעבור דרך המכונות": ה-CharacterController של XRI נשאר במרכז אזור המשחק ולא זז עם הראש, אז מי שעומד
    //    חצי מטר מהמרכז (או הולך פיזית) נכנס עם הראש למכונות. כאן הגוף תמיד מתחת לראש, ואם הגוף בכל זאת בתוך מכונה
    //    או קיר (הליכה פיזית), השחקן נדחף החוצה. לא בתוך משחק: שם מותר להתכופף אל המכונה.
    // 2. "הפטישים נופלים מהשולחן בלי שאני עושה כלום": הפטישים מונחים 16 ס"מ מול עמדת המשחק, בתוך הרדיוס של הגוף,
    //    והפיזיקה דחפה אותם החוצה. הגוף לא מתנגש בחפצי משחק (כל מה שיש לו Rigidbody): רק במכונות, בקירות ובשולחנות.
    [DefaultExecutionOrder(200)]   // אחרי המעקב של הראש
    public class BodyCollision : MonoBehaviour
    {
        [SerializeField] private XROrigin origin;
        [SerializeField] private CharacterController body;
        [SerializeField] private CapsuleCollider probe;        // טריגר עזר לחישוב חדירה
        [SerializeField] private LayerMask solid = 1;           // Default: מכונות, קירות
        [SerializeField] private float radius = 0.2f;

        private readonly Collider[] hits = new Collider[24];
        private readonly HashSet<Collider> ignored = new HashSet<Collider>();
        private float nextScan;
        private Vector3 lastFreeHead;   // איפה הראש עמד לאחרונה בלי להיות בתוך משהו
        private bool hasFree;

        public Vector3 LastPush { get; private set; }

        private void LateUpdate()
        {
            Transform head = origin.Camera.transform;
            Vector3 local = origin.transform.InverseTransformPoint(head.position);
            body.center = new Vector3(local.x, body.center.y, local.z);

            if (Time.unscaledTime >= nextScan) { IgnoreProps(); nextScan = Time.unscaledTime + 1f; }

            LastPush = Vector3.zero;
            if (StationPresence.Current != null || MotionArcade.UI.ArcadeMenu.Blocking) { hasFree = false; return; }
            Vector3 push = Depenetrate(head.position);
            if (push.sqrMagnitude < 1e-8f)
            {
                lastFreeHead = head.position;
                hasFree = true;
                return;
            }
            // דוחפים החוצה. אם גם אחרי זה הוא בתוך משהו (בין שני ארונות צמודים הדחיפות מבטלות זו את זו),
            // מחזירים את הראש לאיפה שעמד לאחרונה בחוץ: הכיוון שממנו הגיע
            Vector3 after = head.position + push;
            if (Depenetrate(after).sqrMagnitude > 1e-6f && hasFree)
            {
                push = lastFreeHead - head.position;
                push.y = 0f;
            }
            LastPush = push;
            origin.transform.position += push;
            Physics.SyncTransforms();
        }

        // כמה צריך לזוז (אופקית) כדי שהגוף, מתחת לראש, לא יהיה בתוך שום דבר קבוע
        public Vector3 Depenetrate(Vector3 headPosition)
        {
            float floor = origin.transform.position.y;
            float top = Mathf.Max(headPosition.y - 0.1f, floor + 0.7f);
            Vector3 p0 = new Vector3(headPosition.x, floor + 0.3f + radius, headPosition.z);
            Vector3 p1 = new Vector3(headPosition.x, top, headPosition.z);
            probe.radius = radius;
            probe.height = (p1.y - p0.y) + 2f * radius;
            probe.direction = 1;
            Vector3 centre = (p0 + p1) * 0.5f;
            probe.transform.SetPositionAndRotation(centre, Quaternion.identity);
            probe.center = Vector3.zero;

            int n = Physics.OverlapCapsuleNonAlloc(p0, p1, radius, hits, solid, QueryTriggerInteraction.Ignore);
            Vector3 push = Vector3.zero;
            for (int i = 0; i < n; i++)
            {
                Collider other = hits[i];
                if (other == null || other == body || other == probe || other.attachedRigidbody != null) continue;
                if (other.transform.IsChildOf(origin.transform)) continue;
                if (Physics.ComputePenetration(probe, centre, Quaternion.identity, other, other.transform.position, other.transform.rotation, out Vector3 direction, out float distance))
                    push += direction * distance;
            }
            push.y = 0f;
            return push;
        }

        // חפצי המשחק (כדורים, פטישים, חצים, שרביט): הגוף עובר דרכם בלי להזיז אותם. נסרק כל שנייה, כי המשחקים נטענים אחרי ההתחלה
        private void IgnoreProps()
        {
            foreach (Rigidbody rb in FindObjectsByType<Rigidbody>(FindObjectsSortMode.None))
            {
                if (rb.transform.IsChildOf(origin.transform)) continue;
                foreach (Collider c in rb.GetComponentsInChildren<Collider>(true))
                {
                    if (!ignored.Add(c)) continue;
                    Physics.IgnoreCollision(body, c, true);
                }
            }
        }

        public bool Ignores(Collider c) => ignored.Contains(c);
    }
}
