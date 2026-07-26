using System;

namespace AshesOfRum
{
    public sealed class EconomyWallet
    {
        public EconomyWallet(int startingSupplies)
        {
            if (startingSupplies < 0) throw new ArgumentOutOfRangeException(nameof(startingSupplies));
            Supplies = startingSupplies;
        }

        public int Supplies { get; private set; }

        public bool TrySpend(int amount)
        {
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (Supplies < amount) return false;
            Supplies -= amount;
            return true;
        }

        public void Deposit(int amount)
        {
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            checked { Supplies += amount; }
        }

        public void Refund(int amount) => Deposit(amount);
    }
}
