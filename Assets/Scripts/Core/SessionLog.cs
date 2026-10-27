using System;
using System.Collections.Generic;

namespace MotionArcade.Core
{
    // סבב אחד שהסתיים. בשלב 6 יתווספו לכאן המדדים (טווח, רעד, זמני תגובה).
    public readonly struct RoundResult
    {
        public readonly string GameId;
        public readonly int Score;
        public readonly float DurationSeconds;
        public readonly DateTime EndedAtUtc;

        public RoundResult(string gameId, int score, float durationSeconds, DateTime endedAtUtc)
        {
            GameId = gameId;
            Score = score;
            DurationSeconds = durationSeconds;
            EndedAtUtc = endedAtUtc;
        }
    }

    // כל הסבבים של הסשן הנוכחי, בזיכרון. מחלקה רגילה (לא MonoBehaviour) כדי שאפשר לבדוק אותה בלי סצנה.
    public class SessionLog
    {
        private readonly List<RoundResult> rounds = new List<RoundResult>();

        public IReadOnlyList<RoundResult> Rounds => rounds;

        public void Add(RoundResult result)
        {
            if (string.IsNullOrEmpty(result.GameId)) throw new ArgumentException("a round needs a game id", nameof(result));
            rounds.Add(result);
        }

        // השיא של משחק בסשן הזה. false אם עוד לא שוחק.
        public bool TryGetBest(string gameId, out int best)
        {
            best = 0;
            bool found = false;
            foreach (RoundResult r in rounds)
            {
                if (r.GameId != gameId) continue;
                if (!found || r.Score > best) best = r.Score;
                found = true;
            }
            return found;
        }

        public int CountFor(string gameId)
        {
            int count = 0;
            foreach (RoundResult r in rounds) if (r.GameId == gameId) count++;
            return count;
        }
    }
}
