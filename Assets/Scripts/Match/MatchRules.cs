using System;

namespace AshesOfRum
{
    public static class MatchRules
    {
        public static AiPhase PhaseAt(float elapsedSeconds, float probeSeconds, float pressureSeconds,
            float finalAssaultSeconds)
        {
            if (elapsedSeconds < 0f) throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            if (probeSeconds <= 0f || pressureSeconds <= probeSeconds || finalAssaultSeconds <= pressureSeconds)
                throw new ArgumentException("AI phase times must be positive and strictly increasing.");
            if (elapsedSeconds >= finalAssaultSeconds) return AiPhase.FinalAssault;
            if (elapsedSeconds >= pressureSeconds) return AiPhase.Pressure;
            return elapsedSeconds >= probeSeconds ? AiPhase.Probe : AiPhase.Preparing;
        }

        public static int StructuralVolleyDamage(int livingMembers, int damagePerMember)
        {
            if (livingMembers < 0) throw new ArgumentOutOfRangeException(nameof(livingMembers));
            if (damagePerMember <= 0) throw new ArgumentOutOfRangeException(nameof(damagePerMember));
            return checked(livingMembers * damagePerMember);
        }
    }
}
