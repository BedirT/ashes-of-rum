using System;
using System.Collections.Generic;

namespace AshesOfRum
{
    public sealed class HisarProductionQueue
    {
        private readonly Queue<ProductionItem> items = new();
        private readonly EconomyWallet wallet;
        private readonly PopulationLedger population;
        private readonly EconomyTuning tuning;
        private float remaining;

        public HisarProductionQueue(EconomyWallet economyWallet, PopulationLedger populationLedger,
            EconomyTuning economyTuning)
        {
            wallet = economyWallet ?? throw new ArgumentNullException(nameof(economyWallet));
            population = populationLedger ?? throw new ArgumentNullException(nameof(populationLedger));
            tuning = economyTuning != null ? economyTuning : throw new ArgumentNullException(nameof(economyTuning));
        }

        public int Count => items.Count;
        public ProductionItem? Active => items.Count == 0 ? null : items.Peek();
        public float Progress => items.Count == 0 ? 0f : 1f - remaining / Duration(items.Peek());

        public bool TryEnqueueWorker() => TryEnqueue(ProductionItem.Worker);

        public bool TryEnqueueFormation(FormationType type) => TryEnqueue(type.ToProductionItem());

        public bool CancelActive()
        {
            if (items.Count == 0) return false;
            var cancelled = items.Dequeue();
            wallet.Refund(Cost(cancelled));
            population.Release(PopulationCost(cancelled));
            remaining = items.Count == 0 ? 0f : Duration(items.Peek());
            return true;
        }

        public ProductionItem? Advance(float seconds)
        {
            if (seconds < 0f) throw new ArgumentOutOfRangeException(nameof(seconds));
            if (items.Count == 0) return null;
            remaining -= seconds;
            if (remaining > 0f) return null;
            var completed = items.Dequeue();
            remaining = items.Count == 0 ? 0f : Duration(items.Peek());
            return completed;
        }

        private bool TryEnqueue(ProductionItem item)
        {
            var populationCost = PopulationCost(item);
            if (!population.TryReserve(populationCost)) return false;
            if (!wallet.TrySpend(Cost(item)))
            {
                population.Release(populationCost);
                return false;
            }
            items.Enqueue(item);
            if (items.Count == 1) remaining = Duration(item);
            return true;
        }

        private int Cost(ProductionItem item) => item == ProductionItem.Worker
            ? tuning.workerCost
            : tuning.formationCost;

        private int PopulationCost(ProductionItem item) => item == ProductionItem.Worker
            ? 1
            : tuning.formationPopulation;

        private float Duration(ProductionItem item) => item == ProductionItem.Worker
            ? tuning.workerTrainSeconds
            : tuning.formationTrainSeconds;
    }
}
