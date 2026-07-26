using System;

namespace AshesOfRum
{
    public static class CombatRules
    {
        public static bool Counters(FormationType attacker, FormationType defender) =>
            (attacker == FormationType.Spearmen && defender == FormationType.Cavalry) ||
            (attacker == FormationType.Archers && defender == FormationType.Spearmen) ||
            (attacker == FormationType.Cavalry && defender == FormationType.Archers);

        public static int Damage(FormationType attacker, FormationType defender, int baseDamage,
            float counterMultiplier)
        {
            if (baseDamage <= 0) throw new ArgumentOutOfRangeException(nameof(baseDamage));
            if (counterMultiplier < 1f) throw new ArgumentOutOfRangeException(nameof(counterMultiplier));
            return Counters(attacker, defender)
                ? (int)Math.Round(baseDamage * counterMultiplier, MidpointRounding.AwayFromZero)
                : baseDamage;
        }
    }
}
