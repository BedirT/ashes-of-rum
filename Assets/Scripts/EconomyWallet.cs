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

        public void Deposit(int amount)
        {
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            checked { Supplies += amount; }
        }
    }
}
