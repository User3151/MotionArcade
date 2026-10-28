using System;

namespace MotionArcade.Games.Whack
{
    // התנועה של חפרפרת אחת, בלי Unity: Hidden → Rising → Up → Retreating → Hidden, או Bonked כשפוגעים בה.
    // Height הוא 0 (בפנים) עד 1 (בחוץ); ה-MonoBehaviour רק ממיר אותו למיקום.
    public class MoleMotion
    {
        public enum State { Hidden, Rising, Up, Retreating, Bonked }

        public const float RiseSeconds = 0.15f;
        public const float RetreatSeconds = 0.18f;
        public const float BonkSeconds = 0.25f;

        public State Current { get; private set; } = State.Hidden;
        public bool IsRed { get; private set; }
        public float Height { get; private set; }

        // פגיעה אפשרית רק כשהיא בחוץ או בדרך החוצה
        public bool IsHittable => Current == State.Rising || Current == State.Up;
        public bool IsBusy => Current != State.Hidden;

        private float upSeconds;
        private float timer;          // זמן בתוך השלב הנוכחי
        private float sinceSpawn;     // מתחילת העלייה: זמן התגובה של השחקן

        // החפרפרת חזרה פנימה בלי שפגעו בה (רק לחומות, לא לאדומות: מהן נכון להתעלם)
        public event Action Escaped;

        public void Spawn(float secondsUp, bool red)
        {
            if (IsBusy) return;
            upSeconds = Math.Max(0.1f, secondsUp);
            IsRed = red;
            sinceSpawn = 0f;
            Enter(State.Rising);
        }

        // true = פגיעה. reactionSeconds: מתחילת העלייה ועד הפגיעה.
        public bool TryHit(out float reactionSeconds)
        {
            reactionSeconds = sinceSpawn;
            if (!IsHittable) return false;
            Enter(State.Bonked);
            return true;
        }

        // סוף סבב: כל החפרפרות נכנסות, בלי להיחשב כ"ברחה"
        public void Hide()
        {
            Current = State.Hidden;
            Height = 0f;
            timer = 0f;
        }

        public void Tick(float dt)
        {
            if (Current == State.Hidden || dt <= 0f) return;
            timer += dt;
            sinceSpawn += dt;

            switch (Current)
            {
                case State.Rising:
                    Height = Math.Min(1f, timer / RiseSeconds);
                    if (timer >= RiseSeconds) Enter(State.Up);
                    break;
                case State.Up:
                    Height = 1f;
                    if (timer >= upSeconds) Enter(State.Retreating);
                    break;
                case State.Retreating:
                    Height = Math.Max(0f, 1f - timer / RetreatSeconds);
                    if (timer >= RetreatSeconds)
                    {
                        Hide();
                        if (!IsRed) Escaped?.Invoke();
                    }
                    break;
                case State.Bonked:
                    Height = Math.Max(0f, 1f - timer / BonkSeconds);
                    if (timer >= BonkSeconds) Hide();
                    break;
            }
        }

        private void Enter(State next)
        {
            Current = next;
            timer = 0f;
        }
    }

    // מתי ואיפה מופיעה חפרפרת. הקושי עולה לאורך הסבב: יותר חפרפרות יחד, פחות זמן בחוץ,
    // ואחרי 15 שניות גם אדומות (אסור לפגוע בהן: עיכוב תגובה). כרגע ערכים קבועים; בשלב 6 לפי הכיול.
    public static class WhackSpawner
    {
        public const float RedFromSeconds = 15f;
        public const float RedChance = 0.2f;

        public static int MaxUp(float elapsed) => elapsed < 15f ? 1 : elapsed < 35f ? 2 : 3;

        // 1.6 שניות בחוץ בהתחלה, 0.9 בסוף
        public static float UpSeconds(float elapsed, float roundSeconds)
        {
            float t = roundSeconds > 0f ? Math.Max(0f, Math.Min(1f, elapsed / roundSeconds)) : 0f;
            return 1.6f + (0.9f - 1.6f) * t;
        }

        public static float RedChanceAt(float elapsed) => elapsed >= RedFromSeconds ? RedChance : 0f;

        // הפסקה עד החפרפרת הבאה
        public static float Gap(Random rng) => 0.35f + (float)rng.NextDouble() * 0.45f;

        // חור פנוי אקראי, לא אותו חור פעמיים ברצף (אלא אם רק הוא פנוי). ‎-1 אם אין חור פנוי.
        public static int PickHole(bool[] busy, int last, Random rng)
        {
            int free = 0;
            for (int i = 0; i < busy.Length; i++) if (!busy[i] && i != last) free++;
            if (free == 0) return last >= 0 && last < busy.Length && !busy[last] ? last : -1;

            int pick = rng.Next(free);
            for (int i = 0; i < busy.Length; i++)
            {
                if (busy[i] || i == last) continue;
                if (pick-- == 0) return i;
            }
            return -1;
        }
    }
}
