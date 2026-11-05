using MotionArcade.Core;
using UnityEngine;

namespace MotionArcade.Interaction
{
    // בארקייד הולכים עם הסטיק השמאלי, מסתובבים ומתלפטרים לעיגולים עם הימני.
    // בתוך משחק (מ-PLAY ועד EXIT) כל אלה נעולים: יד שזזה בזריקה או במכה לא תזיז את השחקן מהעמדה,
    // הקרן של הטלפורט לא מופיעה, ויוצאים רק ב-EXIT. גם סבב שרץ (למשל בבדיקה אוטומטית) נועל.
    public class LocomotionLock : MonoBehaviour
    {
        [SerializeField] private Behaviour[] locked;   // ספקי התנועה והסיבוב, והקרניים של הטלפורט

        private bool isLocked;

        public bool IsLocked => isLocked;

        private void Update()
        {
            bool lockNow = GameStation.AnyRunning || StationPresence.Current != null;
            if (lockNow == isLocked) return;
            isLocked = lockNow;
            foreach (Behaviour b in locked) if (b != null) b.enabled = !lockNow;
        }

        private void OnDisable()
        {
            if (!isLocked) return;
            isLocked = false;
            foreach (Behaviour b in locked) if (b != null) b.enabled = true;
        }
    }
}
