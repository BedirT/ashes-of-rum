using NUnit.Framework;
using UnityEngine;

namespace AshesOfRum.Tests
{
    public sealed class StartingEconomyTests
    {
        [Test]
        public void Wallet_DepositIncreasesSupplies()
        {
            var wallet = new EconomyWallet(100);

            wallet.Deposit(10);

            Assert.That(wallet.Supplies, Is.EqualTo(110));
        }

        [Test]
        public void Wallet_SpendAndRefundConserveSupplies()
        {
            var wallet = new EconomyWallet(100);

            Assert.That(wallet.TrySpend(101), Is.False);
            Assert.That(wallet.TrySpend(100), Is.True);
            Assert.That(wallet.Supplies, Is.Zero);
            wallet.Refund(100);

            Assert.That(wallet.Supplies, Is.EqualTo(100));
        }

        [Test]
        public void Population_HouseCapacityStopsAtHardCap()
        {
            var population = new PopulationLedger(4, 12, 20);

            population.AddCapacity(8);
            population.AddCapacity(8);

            Assert.That(population.Used, Is.EqualTo(4));
            Assert.That(population.Capacity, Is.EqualTo(20));
        }

        [Test]
        public void Population_ReserveAndReleaseTracksAvailableCapacity()
        {
            var population = new PopulationLedger(4, 12, 60);

            Assert.That(population.TryReserve(8), Is.True);
            Assert.That(population.TryReserve(1), Is.False);
            population.Release(3);

            Assert.That(population.Used, Is.EqualTo(9));
        }

        [Test]
        public void FormationQueue_SpendsReservesCompletesAndCancelsDeterministically()
        {
            var wallet = new EconomyWallet(800);
            var population = new PopulationLedger(4, 20, 60);
            var queue = new FormationProductionQueue(wallet, population, 400, 8, 3f);

            Assert.That(queue.TryEnqueue(FormationType.Archers), Is.True);
            Assert.That(queue.TryEnqueue(FormationType.Cavalry), Is.True);
            Assert.That(wallet.Supplies, Is.Zero);
            Assert.That(population.Used, Is.EqualTo(20));
            Assert.That(queue.Advance(2.9f), Is.Null);
            Assert.That(queue.Advance(0.1f), Is.EqualTo(FormationType.Archers));
            Assert.That(queue.CancelActive(), Is.True);

            Assert.That(wallet.Supplies, Is.EqualTo(400));
            Assert.That(population.Used, Is.EqualTo(12));
            Assert.That(queue.Count, Is.Zero);
        }

        [Test]
        public void Combat_ExplicitCounterTriangleAppliesOnlyToWinningMatchup()
        {
            Assert.That(CombatRules.Damage(FormationType.Archers, FormationType.Spearmen, 10, 2f), Is.EqualTo(20));
            Assert.That(CombatRules.Damage(FormationType.Spearmen, FormationType.Archers, 10, 2f), Is.EqualTo(10));
            Assert.That(CombatRules.Damage(FormationType.Spearmen, FormationType.Cavalry, 10, 2f), Is.EqualTo(20));
            Assert.That(CombatRules.Damage(FormationType.Cavalry, FormationType.Archers, 10, 2f), Is.EqualTo(20));
        }

        [Test]
        public void HousePlacement_SnapsAndChecksBuildableBounds()
        {
            var snapped = HousePlacementRules.Snap(new Vector3(7.4f, 2f, 9.6f));

            Assert.That(snapped, Is.EqualTo(new Vector3(7f, 0f, 10f)));
            Assert.That(HousePlacementRules.IsInsidePlayableBounds(snapped), Is.True);
            Assert.That(HousePlacementRules.IsInsidePlayableBounds(new Vector3(21f, 0f, 10f)), Is.False);
        }

        [Test]
        public void Cache_TakesFixedBatchAndNeverGoesNegative()
        {
            var cacheObject = new GameObject("Cache");
            try
            {
                var cache = cacheObject.AddComponent<ResourceCache>();
                cache.Initialize(15);

                Assert.That(cache.TakeBatch(10), Is.EqualTo(10));
                Assert.That(cache.TakeBatch(10), Is.EqualTo(5));
                Assert.That(cache.TakeBatch(10), Is.Zero);
                Assert.That(cache.Remaining, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(cacheObject);
            }
        }
    }
}
