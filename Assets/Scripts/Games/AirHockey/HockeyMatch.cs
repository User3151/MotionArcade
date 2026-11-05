namespace MotionArcade.Games.AirHockey
{
    // החוקים: מי שמגיע ראשון ל-7 גולים מנצח. אחרי גול הפאק עובר לצד שספג אותו (כמו בשולחן אמיתי,
    // שבו הפאק יוצא מהשער של מי שספג). בפתיחה הפאק אצל השחקן.
    public class HockeyMatch
    {
        public const int DefaultTarget = 7;

        public int Target { get; }
        public int Player { get; private set; }
        public int Opponent { get; private set; }
        public bool Over => Player >= Target || Opponent >= Target;
        public bool PlayerWon => Player >= Target;
        public bool ServeToPlayer { get; private set; } = true;

        public HockeyMatch(int target = DefaultTarget) => Target = target < 1 ? 1 : target;

        public void Reset()
        {
            Player = 0;
            Opponent = 0;
            ServeToPlayer = true;
        }

        // true = הגול נספר (אחרי שהמשחק נגמר לא סופרים)
        public bool Score(GoalScored goal)
        {
            if (Over || goal == GoalScored.None) return false;
            if (goal == GoalScored.ByPlayer) { Player++; ServeToPlayer = false; }
            else { Opponent++; ServeToPlayer = true; }
            return true;
        }
    }
}
