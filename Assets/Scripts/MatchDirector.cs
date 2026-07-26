using System;

namespace AshesOfRum
{
    public sealed class MatchDirector
    {
        public float ElapsedSeconds { get; private set; }
        public MatchOutcome Outcome { get; private set; } = MatchOutcome.InProgress;
        public bool IsComplete => Outcome != MatchOutcome.InProgress;

        public void Advance(float seconds)
        {
            if (seconds < 0f) throw new ArgumentOutOfRangeException(nameof(seconds));
            if (!IsComplete) ElapsedSeconds += seconds;
        }

        public bool Complete(MatchOutcome outcome)
        {
            if (outcome == MatchOutcome.InProgress) throw new ArgumentException("A completed match needs an outcome.");
            if (IsComplete) return false;
            Outcome = outcome;
            return true;
        }
    }
}
