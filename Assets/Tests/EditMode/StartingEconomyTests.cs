using System.IO;
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
        public void Population_TracksHouseCapacityHiddenAboveHardCap()
        {
            var population = new PopulationLedger(4, 12, 60);
            for (var i = 0; i < 7; i++) population.AddCapacity(8);
            Assert.That(population.Capacity, Is.EqualTo(60));

            population.RemoveCapacity(8);
            Assert.That(population.Capacity, Is.EqualTo(60));
            population.RemoveCapacity(8);

            Assert.That(population.Capacity, Is.EqualTo(52));
        }

        [Test]
        public void Population_DestroyedHouseRemovesCapacityWithoutKillingOverCapPopulation()
        {
            var population = new PopulationLedger(4, 12, 60);
            population.AddCapacity(8);
            Assert.That(population.TryReserve(16), Is.True);

            population.RemoveCapacity(8);

            Assert.That(population.Used, Is.EqualTo(20));
            Assert.That(population.Capacity, Is.EqualTo(12));
            Assert.That(population.TryReserve(1), Is.False);
            population.RemoveCapacity(8);
            Assert.That(population.Capacity, Is.EqualTo(12));
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
        public void HisarQueue_WorkersAndFormationsShareReservationOrderAndRefunds()
        {
            var tuning = ScriptableObject.CreateInstance<EconomyTuning>();
            try
            {
                var wallet = new EconomyWallet(900);
                var population = new PopulationLedger(4, 20, 60);
                var queue = new HisarProductionQueue(wallet, population, tuning);

                Assert.That(queue.TryEnqueueWorker(), Is.True);
                Assert.That(queue.TryEnqueueFormation(FormationType.Archers), Is.True);
                Assert.That(queue.Active, Is.EqualTo(ProductionItem.Worker));
                Assert.That(wallet.Supplies, Is.EqualTo(400));
                Assert.That(population.Used, Is.EqualTo(13));
                Assert.That(queue.Advance(tuning.workerTrainSeconds), Is.EqualTo(ProductionItem.Worker));
                Assert.That(queue.Active, Is.EqualTo(ProductionItem.Archers));
                Assert.That(queue.CancelActive(), Is.True);

                Assert.That(wallet.Supplies, Is.EqualTo(800));
                Assert.That(population.Used, Is.EqualTo(5));
                Assert.That(queue.Count, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(tuning);
            }
        }

        [Test]
        public void MatchRules_UseLockedAttackWindowsAndReducedStructuralDamage()
        {
            Assert.That(MatchRules.PhaseAt(179.9f, 180f, 360f, 600f), Is.EqualTo(AiPhase.Preparing));
            Assert.That(MatchRules.PhaseAt(180f, 180f, 360f, 600f), Is.EqualTo(AiPhase.Probe));
            Assert.That(MatchRules.PhaseAt(360f, 180f, 360f, 600f), Is.EqualTo(AiPhase.Pressure));
            Assert.That(MatchRules.PhaseAt(600f, 180f, 360f, 600f), Is.EqualTo(AiPhase.FinalAssault));
            Assert.That(MatchRules.StructuralVolleyDamage(8, 2), Is.EqualTo(16));
        }

        [Test]
        public void MatchDirector_FreezesElapsedTimeAfterFirstResult()
        {
            var match = new MatchDirector();
            match.Advance(123f);

            Assert.That(match.Complete(MatchOutcome.Victory), Is.True);
            Assert.That(match.Complete(MatchOutcome.Defeat), Is.False);
            match.Advance(10f);

            Assert.That(match.Outcome, Is.EqualTo(MatchOutcome.Victory));
            Assert.That(match.ElapsedSeconds, Is.EqualTo(123f));
        }

        [Test]
        public void MatchTelemetry_WritesRequiredSummaryAndEventLogFields()
        {
            var directory = Path.Combine(Application.temporaryCachePath, "ashes-match-telemetry-test");
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
            try
            {
                var telemetry = new MatchTelemetry("deterministic-match");
                telemetry.RecordSupplies(true, 10, 5f);
                telemetry.RecordEntityProduced(false, "Cavalry", 12f);
                telemetry.RecordBuildingConstructed(true, "House", 20f);
                telemetry.RecordFirstContact(180f);
                telemetry.RecordAiAttack(AiPhase.Probe, 180f);
                telemetry.Complete(MatchOutcome.Victory, 615f, "Alazhan Hisar");
                telemetry.Write(directory);

                Assert.That(File.Exists(telemetry.SummaryPath), Is.True);
                Assert.That(File.Exists(telemetry.EventLogPath), Is.True);
                var summary = JsonUtility.FromJson<MatchSummary>(File.ReadAllText(telemetry.SummaryPath));
                var eventLog = JsonUtility.FromJson<MatchEventLog>(File.ReadAllText(telemetry.EventLogPath));
                Assert.That(summary.outcome, Is.EqualTo(MatchOutcome.Victory.ToString()));
                Assert.That(summary.friendlySuppliesGathered, Is.EqualTo(10));
                Assert.That(summary.hostileEntitiesProduced, Is.EqualTo(1));
                Assert.That(summary.friendlyBuildingsConstructed, Is.EqualTo(1));
                Assert.That(summary.firstContactSeconds, Is.EqualTo(180f));
                Assert.That(summary.destroyedHisar, Is.EqualTo("Alazhan Hisar"));
                Assert.That(eventLog.events.Exists(item => item.type == "match_completed"), Is.True);
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
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
        public void FormationTuning_CavalryIsClearlyFasterThanFootFormations()
        {
            var tuning = ScriptableObject.CreateInstance<EconomyTuning>();
            try
            {
                Assert.That(tuning.cavalrySpeed, Is.GreaterThan(tuning.footSpeed * 1.4f));
                Assert.That(tuning.sightRadius, Is.GreaterThan(0f));
            }
            finally
            {
                Object.DestroyImmediate(tuning);
            }
        }

        [Test]
        public void FogMap_PreservesExplorationAndClearsOnlyCurrentVisibility()
        {
            var fog = new FogOfWarMap(-10f, 10f, -10f, 10f, 1f);
            fog.UpdateVisibility(new[] { Vector3.zero }, 3f);

            Assert.That(fog.StateAt(Vector3.zero), Is.EqualTo(FogState.Visible));
            Assert.That(fog.StateAt(new Vector3(8f, 0f, 8f)), Is.EqualTo(FogState.Unexplored));

            fog.UpdateVisibility(new[] { new Vector3(8f, 0f, 8f) }, 2f);

            Assert.That(fog.StateAt(Vector3.zero), Is.EqualTo(FogState.Explored));
            Assert.That(fog.StateAt(new Vector3(8f, 0f, 8f)), Is.EqualTo(FogState.Visible));
            var world = fog.UvToWorld(fog.WorldToUv(new Vector3(4f, 0f, -3f)));
            Assert.That(world.x, Is.EqualTo(4f).Within(0.01f));
            Assert.That(world.z, Is.EqualTo(-3f).Within(0.01f));
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
        public void BuildingTuning_PreservesApprovedOneTwoThreeCostRatio()
        {
            var tuning = ScriptableObject.CreateInstance<EconomyTuning>();
            try
            {
                Assert.That(tuning.storehouseCost, Is.EqualTo(tuning.houseCost * 2));
                Assert.That(tuning.watchtowerCost, Is.EqualTo(tuning.houseCost * 3));
            }
            finally
            {
                Object.DestroyImmediate(tuning);
            }
        }

        [Test]
        public void ConstructibleBuilding_CompletesAndDestructionCallbackFiresOnce()
        {
            var root = new GameObject("Storehouse");
            try
            {
                var destructionCount = 0;
                var building = root.AddComponent<ConstructibleBuilding>();
                building.Initialize(BuildingType.Storehouse, 4f, 100, Color.blue, _ => destructionCount++);

                Assert.That(building.Advance(3.9f), Is.False);
                Assert.That(building.Advance(0.1f), Is.True);
                Assert.That(building.IsComplete, Is.True);
                Assert.That(building.ApplyDamage(100), Is.True);
                Assert.That(building.IsDestroyed, Is.True);
                Assert.That(destructionCount, Is.EqualTo(1));
                Assert.That(building.Demolish(), Is.False);
                Assert.That(destructionCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
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
