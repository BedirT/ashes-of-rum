using System;

namespace AshesOfRum
{
    public sealed class PopulationLedger
    {
        private readonly int hardCap;

        public PopulationLedger(int used, int startingCap, int maximumCap)
        {
            if (used < 0) throw new ArgumentOutOfRangeException(nameof(used));
            if (startingCap < used) throw new ArgumentOutOfRangeException(nameof(startingCap));
            if (maximumCap < startingCap) throw new ArgumentOutOfRangeException(nameof(maximumCap));
            Used = used;
            Capacity = startingCap;
            hardCap = maximumCap;
        }

        public int Used { get; private set; }
        public int Capacity { get; private set; }

        public void AddCapacity(int amount)
        {
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            Capacity = Math.Min(hardCap, checked(Capacity + amount));
        }

        public bool TryReserve(int amount)
        {
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (Used + amount > Capacity) return false;
            Used += amount;
            return true;
        }

        public void Release(int amount)
        {
            if (amount <= 0 || amount > Used) throw new ArgumentOutOfRangeException(nameof(amount));
            Used -= amount;
        }
    }
}
