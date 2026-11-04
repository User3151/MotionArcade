using MotionArcade.Core;
using UnityEngine;

namespace MotionArcade.Interaction
{
    // בארקייד הולכים עם הסטיק השמאלי, מסתובבים ומתלפטרים לעיגולים עם הימני.
    // בזמן סבב (מ-START ועד הסוף, בכל אחד מהמשחקים) כל אלה נעולים: יד שזזה בזריקה או במכה
    // לא תזיז את השחקן מהעיגול, וגם הקרן של הטלפורט לא מופיעה.
    public class LocomotionLock : MonoBehaviour
    {
        [SerializeField] private Behaviour[] locked;   // ספקי התנועה והסיבוב, והקרניים של הטלפורט

        private bool isLocked;

        public bool IsLocked => isLocked;

        private void Update()
        {
            bool lockNow = GameStation.AnyRunning;
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
