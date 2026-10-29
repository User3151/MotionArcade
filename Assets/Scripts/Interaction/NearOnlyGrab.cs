using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace MotionArcade.Interaction
{
    // חפצי משחק נתפסים רק ביד, מקרוב, לא בקרן מרחוק. הקרן נשארת ללחיצה על כפתורים.
    // תפיסה מרחוק מושכת את החפץ ליד בנקודה מקרית, וזה משנה את כיוון הזריקה; וגם בכלל לא מתרגל הושטה.
    // אותו מסנן גם מונע הדגשה (hover) מהקרן, כדי שלא ייראה כאילו אפשר לתפוס.
    [RequireComponent(typeof(XRBaseInteractable))]
    public class NearOnlyGrab : MonoBehaviour, IXRSelectFilter, IXRHoverFilter
    {
        [SerializeField] private float maxDistance = 0.12f;   // מהיד אל פני החפץ

        private XRBaseInteractable interactable;

        public bool canProcess => isActiveAndEnabled;

        private void Awake()
        {
            interactable = GetComponent<XRBaseInteractable>();
            interactable.selectFilters.Add(this);
            interactable.hoverFilters.Add(this);
        }

        private void OnDestroy()
        {
            if (interactable == null) return;
            interactable.selectFilters.Remove(this);
            interactable.hoverFilters.Remove(this);
        }

        public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable target) => IsNear(interactor, target);
        public bool Process(IXRHoverInteractor interactor, IXRHoverInteractable target) => IsNear(interactor, target);

        private bool IsNear(IXRInteractor interactor, IXRInteractable target)
        {
            // שקע (מחזיק/מגש) הוא לא יד: תמיד מותר
            if (interactor is XRSocketInteractor) return true;
            Vector3 hand = interactor.GetAttachTransform(target).position;
            float best = float.PositiveInfinity;
            foreach (Collider c in interactable.colliders)
            {
                if (c == null || !c.enabled || c.isTrigger) continue;
                float d = Vector3.Distance(hand, c.ClosestPoint(hand));
                if (d < best) best = d;
            }
            return best <= maxDistance;
        }
    }
}
