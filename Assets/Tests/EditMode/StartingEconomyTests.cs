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
