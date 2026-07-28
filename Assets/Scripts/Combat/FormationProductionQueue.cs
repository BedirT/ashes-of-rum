using System;
using System.Collections.Generic;

namespace AshesOfRum
{
    public sealed class FormationProductionQueue
    {
        private readonly Queue<FormationType> items = new();
        private readonly EconomyWallet wallet;
        private readonly PopulationLedger population;
        private readonly int cost;
        private readonly int populationCost;
        private readonly float duration;
        private float remaining;

        public FormationProductionQueue(EconomyWallet wallet, PopulationLedger population, int cost,
            int populationCost, float duration)
        {
            this.wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            this.population = population ?? throw new ArgumentNullException(nameof(population));
            this.cost = cost > 0 ? cost : throw new ArgumentOutOfRangeException(nameof(cost));
            this.populationCost = populationCost > 0
                ? populationCost
                : throw new ArgumentOutOfRangeException(nameof(populationCost));
            this.duration = duration > 0f ? duration : throw new ArgumentOutOfRangeException(nameof(duration));
        }

        public int Count => items.Count;
        public FormationType? Active => items.Count == 0 ? null : items.Peek();
        public float Progress => items.Count == 0 ? 0f : 1f - remaining / duration;

        public bool TryEnqueue(FormationType type)
        {
            if (!population.TryReserve(populationCost)) return false;
            if (!wallet.TrySpend(cost))
            {
                population.Release(populationCost);
                return false;
            }
            items.Enqueue(type);
            if (items.Count == 1) remaining = duration;
            return true;
        }

        public bool CancelActive()
        {
            if (items.Count == 0) return false;
            items.Dequeue();
            wallet.Refund(cost);
            population.Release(populationCost);
            remaining = items.Count == 0 ? 0f : duration;
            return true;
        }

        public FormationType? Advance(float seconds)
        {
            if (seconds < 0f) throw new ArgumentOutOfRangeException(nameof(seconds));
            if (items.Count == 0) return null;
            remaining -= seconds;
            if (remaining > 0f) return null;
            var completed = items.Dequeue();
            remaining = items.Count == 0 ? 0f : duration;
            return completed;
        }
    }
}
