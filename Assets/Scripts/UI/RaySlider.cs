using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MotionArcade.UI
{
    // סליידר שגוררים עם קרן של שלט.
    // 1. הסליידר הרגיל מחשב את המיקום מנקודה על המסך. כשהקרן מפספסת את הלוח אפילו לפריים אחד, XRI נותן לו
    //    נקודה בקצה הרחוק של הקרן, והידית קופצת לשם ובחזרה. כאן: נקודת הפגיעה של הקרן בעולם, ופריים בלי פגיעה מדולג.
    // 2. גיא (PSVR2): ההדק (L2/R2) רשום גם ל-Select וגם ל-UI Press, וכשהוא מרחף ליד הסף הלחיצה נפתחת ונסגרת שוב ושוב.
    //    לחיצה חוזרת תוך releaseGrace ממשיכה את אותה גרירה, בלי הקפיצה של הסליידר הרגיל לנקודה שבמסך.
    // 3. גיא: אחרי שחרור הסליידר "המשיך לזוז מצד לצד" עד כמה לחיצות על R2. סליידר שנלחץ נשאר בחור (selected),
    //    והסטיקים, שהולכים ומסתובבים איתם, הזיזו אותו כמו חיצי מקלדת. כאן אין ניווט, והוא משחרר את הבחירה בסוף הגרירה.
    // מתאים לסליידר אופקי משמאל לימין.
    public class RaySlider : Slider
    {
        [SerializeField] private float releaseGrace = 0.25f;   // שניות

        private int dragPointer = int.MinValue;
        private float releasedAt = -1f;

        public bool Dragging => dragPointer != int.MinValue;

        protected override void Awake()
        {
            base.Awake();
            navigation = new Navigation { mode = Navigation.Mode.None };
        }

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
            if (eventData.pointerId != dragPointer || releasedAt >= 0f) return;   // רק היד שגוררת, ורק בזמן לחיצה
            SetFromRay(eventData);
        }

        // ניווט (סטיק / חיצים) לא מזיז את הסליידר
        public override void OnMove(AxisEventData eventData) { }

        protected override void OnDisable()
        {
            EndDrag();
            base.OnDisable();
        }

        // Selectable אין Update וירטואלי: זה Update רגיל של המחלקה הזו. רק מסיים גרירה אחרי שחרור; לא מזיז דבר
        private void Update()
        {
            if (!Dragging || releasedAt < 0f) return;
            if (Time.unscaledTime - releasedAt > releaseGrace) EndDrag();
        }

        private void EndDrag()
        {
            bool was = Dragging;
            dragPointer = int.MinValue;
            releasedAt = -1f;
            if (was && EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject)
                EventSystem.current.SetSelectedGameObject(null);
        }

        private void SetFromRay(PointerEventData eventData)
        {
            RaycastResult hit = eventData.pointerCurrentRaycast;
            if (!hit.isValid) return;   // הקרן לא פגעה בלוח בפריים הזה: הידית נשארת במקום
            SetFromWorld(hit.worldPosition);
        }

        // האזור שהידית נעה בו: ממירים את הנקודה למרחב שלו, ושם x הוא המיקום לאורך הסליידר
        public void SetFromWorld(Vector3 world)
        {
            RectTransform area = handleRect != null ? handleRect.parent as RectTransform : fillRect != null ? fillRect.parent as RectTransform : null;
            if (area == null) return;
            Vector3 local = area.InverseTransformPoint(world);
            normalizedValue = Mathf.InverseLerp(area.rect.xMin, area.rect.xMax, local.x);
        }
    }
}
