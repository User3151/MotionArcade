using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace MotionArcade.UI
{
    // סליידר שגוררים עם קרן של שלט.
    // 1. הסליידר הרגיל מחשב את המיקום מנקודה על המסך. כשהקרן מפספסת את הלוח אפילו לפריים אחד, XRI נותן לו
    //    נקודה בקצה הרחוק של הקרן, והידית קופצת לשם ובחזרה. כאן: חיתוך הקרן עם המישור של הסליידר בעולם.
    // 2. גיא (PSVR2): הידית ריצדה מצד לצד בגרירה עם L2/R2, ובסימולטור (grip שנשאר לחוץ) לא. ההדק רשום גם ל-Select
    //    וגם ל-UI Press, וכשהוא מרחף ליד הסף הלחיצה נפתחת ונסגרת שוב ושוב: כל לחיצה חדשה התחילה גרירה מחדש מהנקודה
    //    שבמסך. כאן הגרירה שייכת ליד: הסליידר עוקב בעצמו (Update) אחרי הקרן של אותה יד כל עוד היא לוחצת, ושחרור
    //    קצר מ-releaseGrace לא עוצר אותו. לחיצה חוזרת בזמן הזה ממשיכה את אותה גרירה.
    // מתאים לסליידר אופקי משמאל לימין.
    public class RaySlider : Slider
    {
        [SerializeField] private float releaseGrace = 0.25f;   // שניות

        private int dragPointer = int.MinValue;
        private float releasedAt = -1f;

        public bool Dragging => dragPointer != int.MinValue;

        public override void OnPointerDown(PointerEventData eventData)
        {
            float keep = value;
            bool continuing = Dragging && eventData.pointerId == dragPointer;
            base.OnPointerDown(eventData);   // הבחירה, כמו בסליידר רגיל (הוא גם מזיז לפי המסך: מתקנים מיד)
            if (continuing) SetValueWithoutNotify(keep);
            dragPointer = eventData.pointerId;
            releasedAt = -1f;
            SetFromRay(eventData);
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            base.OnPointerUp(eventData);
            if (eventData.pointerId == dragPointer) releasedAt = Time.unscaledTime;
        }

        public override void OnDrag(PointerEventData eventData)
        {
            if (!IsActive() || !IsInteractable() || eventData.button != PointerEventData.InputButton.Left) return;
            SetFromRay(eventData);
        }

        protected override void OnDisable()
        {
            EndDrag();
            base.OnDisable();
        }

        // Selectable אין Update וירטואלי: זה Update רגיל של המחלקה הזו
        private void Update()
        {
            if (!Dragging) return;
            if (!IsActive() || !IsInteractable() || !TryGetModel(out TrackedDeviceModel model))
            {
                EndDrag();
                return;
            }
            if (model.select)
            {
                releasedAt = -1f;
                if (model.raycastPoints != null && model.raycastPoints.Count >= 2)
                    SetFromLine(model.raycastPoints[0], model.raycastPoints[1]);
            }
            else
            {
                if (releasedAt < 0f) releasedAt = Time.unscaledTime;
                if (Time.unscaledTime - releasedAt > releaseGrace) EndDrag();
            }
        }

        private void EndDrag()
        {
            dragPointer = int.MinValue;
            releasedAt = -1f;
        }

        private bool TryGetModel(out TrackedDeviceModel model)
        {
            model = default;
            if (EventSystem.current == null || !(EventSystem.current.currentInputModule is XRUIInputModule module)) return false;
            IUIInteractor interactor = module.GetInteractor(dragPointer);
            return interactor != null && interactor.TryGetUIModel(out model);
        }

        private RectTransform Area => handleRect != null ? handleRect.parent as RectTransform : fillRect != null ? fillRect.parent as RectTransform : null;

        private void SetFromRay(PointerEventData eventData)
        {
            RaycastResult hit = eventData.pointerCurrentRaycast;
            if (!hit.isValid) return;   // הקרן לא פגעה בלוח בפריים הזה: הידית נשארת במקום
            SetFromWorld(hit.worldPosition);
        }

        // הקרן כקו: חותכים עם המישור של האזור שהידית נעה בו (גם אם הקרן עצמה פגעה בחפץ אחר או בכלום)
        private void SetFromLine(Vector3 from, Vector3 to)
        {
            RectTransform area = Area;
            if (area == null) return;
            var plane = new Plane(area.forward, area.position);
            var ray = new Ray(from, to - from);
            if (!plane.Raycast(ray, out float distance) || distance <= 0f) return;
            SetFromWorld(ray.GetPoint(distance));
        }

        // האזור שהידית נעה בו: ממירים את הנקודה למרחב שלו, ושם x הוא המיקום לאורך הסליידר
        public void SetFromWorld(Vector3 world)
        {
            RectTransform area = Area;
            if (area == null) return;
            Vector3 local = area.InverseTransformPoint(world);
            normalizedValue = Mathf.InverseLerp(area.rect.xMin, area.rect.xMax, local.x);
        }
    }
}
