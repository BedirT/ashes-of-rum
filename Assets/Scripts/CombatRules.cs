using System;

namespace AshesOfRum
{
    public enum FlankDirection
    {
        Front,
        Side,
        Rear
    }

    public static class CombatRules
    {
        private const float FrontArcDegrees = 60f;

        public static bool Counters(FormationType attacker, FormationType defender) =>
            (attacker == FormationType.Spearmen && defender == FormationType.Cavalry) ||
            (attacker == FormationType.Archers && defender == FormationType.Spearmen) ||
            (attacker == FormationType.Cavalry && defender == FormationType.Archers);

        public static int Damage(FormationType attacker, FormationType defender, int baseDamage,
            float counterMultiplier)
            => Damage(attacker, defender, baseDamage, counterMultiplier, FlankDirection.Front, 1f, 1f);

        public static int Damage(FormationType attacker, FormationType defender, int baseDamage,
            float counterMultiplier, FlankDirection flank, float sideMultiplier, float rearMultiplier)
        {
            if (baseDamage <= 0) throw new ArgumentOutOfRangeException(nameof(baseDamage));
            if (counterMultiplier < 1f) throw new ArgumentOutOfRangeException(nameof(counterMultiplier));
            if (sideMultiplier < 1f) throw new ArgumentOutOfRangeException(nameof(sideMultiplier));
            if (rearMultiplier < sideMultiplier) throw new ArgumentOutOfRangeException(nameof(rearMultiplier));
            var counter = Counters(attacker, defender) ? counterMultiplier : 1f;
            var flankMultiplier = flank switch
            {
                FlankDirection.Side => sideMultiplier,
                FlankDirection.Rear => rearMultiplier,
                _ => 1f
            };
            return (int)Math.Round(baseDamage * counter * flankMultiplier, MidpointRounding.AwayFromZero);
        }

        public static FlankDirection ClassifyFlank(UnityEngine.Vector3 defenderForward,
            UnityEngine.Vector3 incomingSourceDirection)
        {
            defenderForward.y = 0f;
            incomingSourceDirection.y = 0f;
            if (defenderForward.sqrMagnitude < 0.0001f || incomingSourceDirection.sqrMagnitude < 0.0001f)
                return FlankDirection.Front;
            var angle = UnityEngine.Vector3.Angle(defenderForward, incomingSourceDirection);
            if (angle <= FrontArcDegrees) return FlankDirection.Front;
            return angle >= 180f - FrontArcDegrees ? FlankDirection.Rear : FlankDirection.Side;
        }
    }
}
