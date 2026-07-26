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
        public void HousePlacement_SnapsAndPreservesAnOpenRoute()
        {
            var snapped = HousePlacementRules.Snap(new Vector3(7.4f, 2f, 9.6f));

            Assert.That(snapped, Is.EqualTo(new Vector3(7f, 0f, 10f)));
            Assert.That(HousePlacementRules.IsInsidePlayableBounds(snapped), Is.True);
            Assert.That(HousePlacementRules.IsInsidePlayableBounds(new Vector3(21f, 0f, 10f)), Is.False);
            Assert.That(HousePlacementRules.PreservesRoute(
                new[] { new Vector3(-8f, 0f, 10f), new Vector3(8f, 0f, 10f) }, snapped), Is.True);
        }

        [Test]
        public void HousePlacement_RejectsACompleteWallOff()
        {
            var existing = new[]
            {
                new Vector3(-20f, 0f, 10f), new Vector3(-16f, 0f, 10f),
                new Vector3(-12f, 0f, 10f), new Vector3(-8f, 0f, 10f),
                new Vector3(-4f, 0f, 10f), new Vector3(4f, 0f, 10f),
                new Vector3(8f, 0f, 10f), new Vector3(12f, 0f, 10f),
                new Vector3(16f, 0f, 10f), new Vector3(20f, 0f, 10f)
            };

            Assert.That(HousePlacementRules.PreservesRoute(existing, new Vector3(0f, 0f, 10f)), Is.False);
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
