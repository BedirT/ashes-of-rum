using UnityEngine;

namespace AshesOfRum
{
    public interface ICombatStructure
    {
        Component TargetComponent { get; }
        bool IsFriendly { get; }
        bool IsAttackable { get; }
        bool IsDestroyed { get; }
        int Health { get; }
        int MaxHealth { get; }
        Vector3 AimPoint { get; }
        float CombatRadius { get; }
        bool ApplyStructuralDamage(int amount);
    }
}
