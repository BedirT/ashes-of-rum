using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AshesOfRum.Tests
{
    public sealed partial class StartingEconomyPlayModeTests
    {
        private const float TimeoutSeconds = 15f;
        private static readonly Vector3 VisibleHouseSite = new(8f, 0f, -1f);
        private static readonly Vector3 VisibleStorehouseSite = new(8f, 0f, -1f);

        [UnityTest]
        public IEnumerator HealthBars_CoverCombatEntitiesAndTrackSelectionHoverDamageAndFog()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var friendlyHisarBar = economy.FriendlyHisar.GetComponent<WorldHealthBar>();
            var enemyHisarBar = economy.EnemyHisar.GetComponent<WorldHealthBar>();

            Assert.That(friendlyHisarBar, Is.Not.Null);
            Assert.That(enemyHisarBar, Is.Not.Null);
            Assert.That(economy.Workers.All(worker => worker.GetComponent<WorldHealthBar>() != null), Is.True);
            Assert.That(friendlyHisarBar.IsVisible, Is.False);
            economy.SelectHisar();
            yield return null;
            Assert.That(friendlyHisarBar.IsVisible, Is.True, "Selecting the friendly Hisar must show its bar.");
            economy.SelectOnly(economy.Workers[0]);
            yield return null;
            Assert.That(friendlyHisarBar.IsVisible, Is.False);
            var workerBar = economy.Workers[0].GetComponent<WorldHealthBar>();
            Assert.That(workerBar.IsVisible, Is.True);

            enemyHisarBar.SetHovered(true);
            yield return null;
            Assert.That(enemyHisarBar.IsVisible, Is.False,
                "Hover must not leak a hostile health bar through unexplored fog.");
            enemyHisarBar.SetHovered(false);
            var scout = new GameObject("Health bar scout");
            scout.transform.position = economy.EnemyHisar.transform.position;
            economy.FogOfWar.RegisterFriendly(scout.transform);
            economy.FogOfWar.RefreshNow();
            yield return null;
            enemyHisarBar.SetHovered(true);
            yield return null;
            Assert.That(enemyHisarBar.IsVisible, Is.True,
                "Hover over a visible hostile Hisar must show its bar.");
            enemyHisarBar.SetHovered(false);
            yield return null;
            Assert.That(enemyHisarBar.IsVisible, Is.False, "The bar must hide when its only trigger ends.");
            var hisarHealth = economy.EnemyHisar.Health;
            economy.EnemyHisar.ApplyStructuralDamage(100);
            yield return null;
            Assert.That(enemyHisarBar.FillFraction,
                Is.EqualTo((float)(hisarHealth - 100) / economy.EnemyHisar.MaxHealth).Within(0.001f));
            Assert.That(enemyHisarBar.IsVisible, Is.True, "Recent damage must keep the hostile Hisar bar visible.");

            var friendlyFormation = economy.DeployFriendlyForAutomation(FormationType.Spearmen,
                new Vector3(0f, 0f, 2f));
            var hostileFormation = economy.DeployEnemyForAutomation(FormationType.Archers,
                new Vector3(0f, 0f, 3f));
            economy.FogOfWar.RefreshNow();
            economy.SelectOnly(friendlyFormation);
            yield return null;
            Assert.That(friendlyFormation.GetComponent<WorldHealthBar>().IsVisible, Is.True);
            hostileFormation.ApplyFixedDamage(10);
            yield return null;
            Assert.That(hostileFormation.GetComponent<WorldHealthBar>().IsVisible, Is.True);
            Assert.That(hostileFormation.GetComponent<WorldHealthBar>().FillFraction, Is.LessThan(1f));

            economy.CreditSuppliesForAutomation(500);
            Assert.That(economy.TryPlaceWatchtower(economy.Workers[1], VisibleStorehouseSite), Is.True);
            var building = economy.Watchtowers.Single();
            Assert.That(building.GetComponent<WorldHealthBar>(), Is.Not.Null,
                "Unfinished and completed combat structures share the health-bar path.");
            building.ApplyStructuralDamage(20);
            yield return null;
            Assert.That(building.GetComponent<WorldHealthBar>().IsVisible, Is.True);
            Assert.That(building.GetComponent<WorldHealthBar>().FillFraction, Is.LessThan(1f));

            Object.Destroy(scout);
        }

        [UnityTest]
        public IEnumerator Worker_GathersDepositsAndReturnsAutomatically()
        {
            yield return SceneManager.LoadSceneAsync(HarnessContract.SceneName, LoadSceneMode.Single);
            yield return null;
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            Assert.That(economy, Is.Not.Null);
            Assert.That(economy.Workers, Has.Count.EqualTo(StartingEconomyController.WorkerCount));
            Assert.That(GameObject.Find(StartingEconomyController.HisarObjectName), Is.Not.Null);

            var worker = economy.Workers[0];
            Assert.That(economy.TrySelectWorkersForCommand(new[] { worker }, out var selectRejection), Is.True,
                selectRejection);
            Assert.That(economy.TryIssueGatherCommand(economy.Caches[0], out var gatherRejection), Is.True,
                gatherRejection);
            var deadline = Time.realtimeSinceStartup + 12f;
            while (economy.Supplies <= economy.StartingSupplies && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.That(economy.Supplies, Is.EqualTo(economy.StartingSupplies + 10));
            Assert.That(economy.Caches[0].Remaining, Is.EqualTo(390));
            Assert.That(worker.IsSelected, Is.True);
            Assert.That(worker.CurrentActivity, Is.EqualTo(WorkerAgent.Activity.GoingToCache));
        }

        [UnityTest]
        public IEnumerator AgentProtocol_ProjectsFogSafeStableStateAndUsesPlayerCommands()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var projector = new AgentStateProjector(economy);
            var executor = new AgentCommandExecutor(economy, projector);

            var initial = projector.Project(1);
            var repeated = projector.Project(1);
            Assert.That(initial.perspective, Is.EqualTo("player"));
            Assert.That(initial.workers.Select(worker => worker.id),
                Is.EqualTo(new[] { "worker-1", "worker-2", "worker-3", "worker-4" }));
            Assert.That(initial.visibleCaches.Select(cache => cache.id), Does.Contain("cache-1"));
            Assert.That(initial.visibleCaches.Select(cache => cache.id), Does.Not.Contain("cache-3"));
            Assert.That(repeated.stateHash, Is.EqualTo(initial.stateHash));

            Assert.That(executor.Execute(new AgentScriptStep
            {
                action = "select",
                actorIds = new[] { "worker-unknown" }
            }, out var rejection), Is.False);
            Assert.That(rejection, Is.EqualTo("unknown_actor"));
            Assert.That(executor.Execute(new AgentScriptStep
            {
                action = "select",
                actorIds = new[] { "worker-1" }
            }, out rejection), Is.True, rejection);
            Assert.That(executor.Execute(new AgentScriptStep
            {
                action = "move",
                x = FogOfWarSystem.MaxX + 1f,
                z = -2f
            }, out rejection), Is.False);
            Assert.That(rejection, Is.EqualTo("invalid_position"));

            var blockedDestination = new Vector3(5f, 0f, -2f);
            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.transform.position = blockedDestination + Vector3.up;
            blocker.transform.localScale = new Vector3(3f, 2f, 3f);
            var obstacle = blocker.AddComponent<NavMeshObstacle>();
            obstacle.carving = true;
            obstacle.carveOnlyStationary = false;
            yield return new WaitForSeconds(1f);
            Assert.That(executor.Execute(new AgentScriptStep
            {
                action = "move",
                x = blockedDestination.x,
                z = blockedDestination.z
            }, out rejection), Is.False);
            Assert.That(rejection, Is.EqualTo("unreachable"));
            Object.Destroy(blocker);
            yield return new WaitForSeconds(1f);

            Assert.That(executor.Execute(new AgentScriptStep
            {
                action = "move",
                x = -3.5f,
                z = -2f
            }, out rejection), Is.True, rejection);
            yield return WaitUntil(() => economy.Workers[0].CurrentActivity == WorkerAgent.Activity.Idle);
            Assert.That(executor.Execute(new AgentScriptStep
            {
                action = "gather",
                targetId = "cache-1"
            }, out rejection), Is.True, rejection);
            yield return WaitUntil(() => economy.Supplies > economy.StartingSupplies);

            var deposited = projector.Project(2);
            Assert.That(deposited.supplies, Is.GreaterThan(deposited.startingSupplies));
            Assert.That(deposited.workers[0].id, Is.EqualTo("worker-1"));
            Assert.That(deposited.workers[0].selected, Is.True);
            Assert.That(deposited.stateHash, Has.Length.EqualTo(64));

            Assert.That(executor.Execute(new AgentScriptStep
            {
                action = "gather",
                targetId = "cache-3"
            }, out rejection), Is.False);
            Assert.That(rejection, Is.EqualTo("unknown_target"),
                "Hidden target guesses must not reveal whether an entity exists.");

            Assert.That(economy.TryPlaceHouse(economy.Workers[1], VisibleHouseSite), Is.True);
            Assert.That(executor.Execute(new AgentScriptStep
            {
                action = "select",
                actorIds = new[] { "worker-2" }
            }, out rejection), Is.True, rejection);
            Assert.That(executor.Execute(new AgentScriptStep
            {
                action = "move",
                x = -1f,
                z = -2f
            }, out rejection), Is.False);
            Assert.That(rejection, Is.EqualTo("actors_busy"));
        }

        [UnityTest]
        public IEnumerator AgentCommands_RejectWhenAnyWorkerDestinationIsUnreachable()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var workers = economy.Workers.Take(2).ToArray();
            Assert.That(economy.TrySelectWorkersForCommand(workers, out var rejection), Is.True, rejection);

            var moveCenter = new Vector3(5f, 0f, -2f);
            var blockedMoveSlot = moveCenter + new Vector3(0.55f, 0f, 0f);
            var moveBlocker = CreateRouteBlocker("Second worker move slot blocker",
                blockedMoveSlot + Vector3.up, new Vector3(0.8f, 2f, 0.8f));
            yield return new WaitForSeconds(1f);
            Assert.That(workers[0].CanReach(moveCenter + new Vector3(-0.55f, 0f, 0f)), Is.True,
                "The first formation slot must remain reachable so the test exercises all-worker validation.");
            Assert.That(workers[1].CanReach(blockedMoveSlot), Is.False);
            Assert.That(economy.TryIssueWorkerMoveCommand(moveCenter, out rejection), Is.False);
            Assert.That(rejection, Is.EqualTo("unreachable"));
            Assert.That(workers.All(worker => worker.CurrentActivity == WorkerAgent.Activity.Idle), Is.True,
                "A rejected multi-worker move must not dispatch any actor.");
            Object.Destroy(moveBlocker);
            yield return new WaitForSeconds(1f);

            var cache = economy.Caches[0];
            var gatherBlocker = CreateRouteBlocker("Second worker gather slot blocker",
                cache.GetGatherPoint(1) + Vector3.up, new Vector3(0.8f, 2f, 0.8f));
            yield return new WaitForSeconds(1f);
            Assert.That(workers[0].CanReachGatherPoint(cache), Is.True,
                "The first gather slot must remain reachable so the test exercises all-worker validation.");
            Assert.That(workers[1].CanReachGatherPoint(cache), Is.False);
            Assert.That(economy.TryIssueGatherCommand(cache, out rejection), Is.False);
            Assert.That(rejection, Is.EqualTo("unreachable"));
            Assert.That(workers.All(worker => worker.CurrentActivity == WorkerAgent.Activity.Idle), Is.True,
                "A rejected multi-worker gather must not dispatch any actor.");
            Object.Destroy(gatherBlocker);
        }

        [UnityTest]
        public IEnumerator AgentBuildCommand_PlacesCompletesAndProjectsHouseWithoutHiddenBuildings()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var projector = new AgentStateProjector(economy);
            var executor = new AgentCommandExecutor(economy, projector);
            var build = new AgentScriptStep
            {
                action = "build",
                buildingType = "House",
                x = VisibleHouseSite.x,
                z = VisibleHouseSite.z
            };

            var initial = projector.Project(1);
            Assert.That(initial.buildings, Is.Empty,
                "Player state must not expose the opponent's starting construction.");
            Assert.That(executor.Execute(build, out var rejection), Is.False);
            Assert.That(rejection, Is.EqualTo("no_selection"));

            Assert.That(executor.Execute(new AgentScriptStep
            {
                action = "select",
                actorIds = new[] { "worker-1" }
            }, out rejection), Is.True, rejection);
            Assert.That(executor.Execute(new AgentScriptStep
            {
                action = "build",
                buildingType = "Storehouse",
                x = VisibleHouseSite.x,
                z = VisibleHouseSite.z
            }, out rejection), Is.False);
            Assert.That(rejection, Is.EqualTo("unsupported_building"));
            Assert.That(executor.Execute(new AgentScriptStep
            {
                action = "build",
                buildingType = "House",
                x = 0f,
                z = 20f
            }, out rejection), Is.False);
            Assert.That(rejection, Is.EqualTo("target_not_visible"));
            Assert.That(economy.Supplies, Is.EqualTo(economy.StartingSupplies));

            Assert.That(executor.Execute(build, out rejection), Is.True, rejection);
            Assert.That(economy.Supplies, Is.Zero);
            var foundation = projector.Project(2);
            Assert.That(foundation.buildings, Has.Length.EqualTo(1));
            Assert.That(foundation.buildings[0].id, Is.EqualTo("building-1"));
            Assert.That(foundation.buildings[0].type, Is.EqualTo("House"));
            Assert.That(foundation.buildings[0].complete, Is.False);
            Assert.That(foundation.buildings[0].assignedBuilderIds, Is.EqualTo(new[] { "worker-1" }));
            Assert.That(projector.TryResolveBuilding("building-1", out var building), Is.True);
            Assert.That(building, Is.SameAs(economy.Houses[0]));

            Assert.That(executor.Execute(build, out rejection), Is.False);
            Assert.That(rejection, Is.EqualTo("actors_busy"));
            yield return WaitUntil(() => economy.Houses[0].IsComplete);

            var completed = projector.Project(3);
            Assert.That(completed.supplies, Is.Zero);
            Assert.That(completed.populationCapacity, Is.EqualTo(20));
            Assert.That(completed.buildings[0].complete, Is.True);
            Assert.That(completed.buildings[0].progress, Is.EqualTo(1f));
            Assert.That(completed.buildings[0].assignedBuilderIds, Is.Empty);

            Assert.That(executor.Execute(new AgentScriptStep
            {
                action = "select",
                actorIds = new[] { "worker-2" }
            }, out rejection), Is.True, rejection);
            Assert.That(executor.Execute(new AgentScriptStep
            {
                action = "build",
                buildingType = "House",
                x = 4f,
                z = -1f
            }, out rejection), Is.False);
            Assert.That(rejection, Is.EqualTo("insufficient_supplies"));
            Assert.That(economy.Houses, Has.Count.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator AgentBuildCommand_ReportsStablePlacementRejectionsWithoutSpending()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            Assert.That(economy.TrySelectWorkersForCommand(new[] { economy.Workers[0] }, out var rejection),
                Is.True, rejection);

            Assert.That(economy.TryIssueBuildCommand(BuildingType.House,
                new Vector3(100f, 0f, 100f), out rejection), Is.False);
            Assert.That(rejection, Is.EqualTo("invalid_position"));
            Assert.That(economy.TryIssueBuildCommand(BuildingType.House,
                economy.Caches[0].transform.position, out rejection), Is.False);
            Assert.That(rejection, Is.EqualTo("occupied"));

            var unreachableSite = VisibleHouseSite;
            var buildPointBlocker = CreateRouteBlocker("Builder destination blocker",
                unreachableSite + new Vector3(0f, 1f, -2.5f), new Vector3(0.8f, 2f, 0.8f));
            yield return new WaitForSeconds(1f);
            Assert.That(economy.Workers[0].CanReach(unreachableSite + Vector3.back * 2.4f), Is.False);
            Assert.That(economy.TryIssueBuildCommand(BuildingType.House, unreachableSite, out rejection), Is.False);
            Assert.That(rejection, Is.EqualTo("unreachable"));
            Object.Destroy(buildPointBlocker);
            yield return new WaitForSeconds(1f);

            var leftBlocker = CreateRouteBlocker("Agent left route blocker",
                new Vector3(-13.6f, 1f, 10f), new Vector3(22.8f, 2f, 4f));
            var rightBlocker = CreateRouteBlocker("Agent right route blocker",
                new Vector3(13.6f, 1f, 10f), new Vector3(22.8f, 2f, 4f));
            var scout = new GameObject("Agent route placement scout");
            scout.transform.position = new Vector3(0f, 0f, 10f);
            economy.FogOfWar.RegisterFriendly(scout.transform);
            yield return new WaitForSeconds(1f);
            economy.FogOfWar.RefreshNow();
            Assert.That(economy.TryIssueBuildCommand(BuildingType.House,
                new Vector3(0f, 0f, 10f), out rejection), Is.False);
            Assert.That(rejection, Is.EqualTo("route_blocked"));

            Assert.That(economy.Supplies, Is.EqualTo(economy.StartingSupplies));
            Assert.That(economy.Houses, Is.Empty);
            Object.Destroy(leftBlocker);
            Object.Destroy(rightBlocker);
            Object.Destroy(scout);
        }

        [UnityTest]
        public IEnumerator MirroredHisars_UseEqualCacheToDropOffDistances()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();

            Assert.That(economy.Caches.Count, Is.EqualTo(economy.OpponentCaches.Count));
            for (var index = 0; index < economy.Caches.Count; index++)
            {
                var friendlyDistance = Vector3.Distance(economy.Caches[index].transform.position,
                    economy.FriendlyHisar.DropOffPoint);
                var hostileDistance = Vector3.Distance(economy.OpponentCaches[index].transform.position,
                    economy.EnemyHisar.DropOffPoint);
                Assert.That(friendlyDistance, Is.EqualTo(hostileDistance).Within(0.01f),
                    $"Mirrored cache {index + 1} must have the same Hisar deposit route length for both sides.");
            }
        }

        [UnityTest]
        public IEnumerator BattlefieldSupplyBudget_FundsHouseAndTwoFormationsWithoutAutomationCredits()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var tuning = (EconomyTuning)economy.GetType()
                .GetField("tuning", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(economy);
            var availableSupplies = economy.StartingSupplies + economy.Caches.Sum(cache => cache.Remaining);
            var requiredSupplies = tuning.houseCost + tuning.formationCost * 2;
            Assert.That(availableSupplies, Is.GreaterThanOrEqualTo(requiredSupplies),
                "The live battlefield must fund the documented House, first formation, and Cavalry path.");
            Assert.That(tuning.startingPopulationCap + tuning.housePopulationCapacity,
                Is.GreaterThanOrEqualTo(StartingEconomyController.WorkerCount + tuning.formationPopulation * 2));

            var originalTimeScale = Time.timeScale;
            GameObject firstScout = null;
            GameObject secondScout = null;
            try
            {
                Time.timeScale = 20f;
                Assert.That(economy.TryPlaceHouse(economy.Workers[0], VisibleHouseSite), Is.True);
                yield return WaitUntil(() => economy.PopulationCapacity == 20);

                firstScout = new GameObject("First budget scout");
                firstScout.transform.position = economy.Caches[0].transform.position;
                secondScout = new GameObject("Second budget scout");
                secondScout.transform.position = economy.Caches[1].transform.position;
                economy.FogOfWar.RegisterFriendly(firstScout.transform);
                economy.FogOfWar.RegisterFriendly(secondScout.transform);
                economy.FogOfWar.RefreshNow();
                for (var index = 0; index < economy.Workers.Count; index++)
                    economy.Workers[index].IssueGather(economy.Caches[index / 2]);

                var gatherDeadline = Time.realtimeSinceStartup + TimeoutSeconds + 5f;
                while (economy.Supplies < tuning.formationCost * 2 &&
                       Time.realtimeSinceStartup < gatherDeadline) yield return null;
                Assert.That(economy.Supplies, Is.GreaterThanOrEqualTo(tuning.formationCost * 2),
                    $"Gathered {economy.Supplies}; caches retain " +
                    $"{string.Join(", ", economy.Caches.Select(cache => cache.Remaining))}.");
                Assert.That(economy.TryQueueFormation(FormationType.Archers), Is.True);
                Assert.That(economy.TryQueueFormation(FormationType.Cavalry), Is.True);
                Assert.That(economy.Supplies, Is.Zero);
                yield return WaitUntil(() => economy.FriendlyFormations.Count == 2);
                Assert.That(economy.FriendlyFormations.Select(formation => formation.Type),
                    Is.EquivalentTo(new[] { FormationType.Archers, FormationType.Cavalry }));
                Assert.That(economy.EnemyFormations, Is.Empty,
                    "Player production must not grant the opponent free counter formations.");
            }
            finally
            {
                Time.timeScale = originalTimeScale;
                if (firstScout != null) Object.Destroy(firstScout);
                if (secondScout != null) Object.Destroy(secondScout);
            }
        }

        [UnityTest]
        public IEnumerator ReturningWorker_MoveOrderDepositsBeforeMoving()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var worker = economy.Workers[0];
            var cache = economy.Caches[0];

            worker.IssueGather(cache);
            yield return WaitUntil(() => worker.CurrentActivity == WorkerAgent.Activity.Returning &&
                                         worker.CarriedSupplies == 10);
            worker.IssueMove(new Vector3(6f, 0f, -2f));
            yield return WaitUntil(() => economy.Supplies == economy.StartingSupplies + 10);

            Assert.That(worker.CarriedSupplies, Is.Zero);
            Assert.That(cache.Remaining, Is.EqualTo(390));
            Assert.That(worker.CurrentActivity, Is.EqualTo(WorkerAgent.Activity.Moving));
        }

        [UnityTest]
        public IEnumerator ReturningWorker_NewCacheOrderDepositsBeforeGatheringNewCache()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var worker = economy.Workers[0];
            var firstCache = economy.Caches[0];
            var secondCache = economy.Caches[1];

            worker.IssueGather(firstCache);
            yield return WaitUntil(() => worker.CurrentActivity == WorkerAgent.Activity.Returning &&
                                         worker.CarriedSupplies == 10);
            worker.IssueGather(secondCache);
            yield return WaitUntil(() => secondCache.Remaining == 390);

            Assert.That(economy.Supplies, Is.EqualTo(economy.StartingSupplies + 10));
            Assert.That(firstCache.Remaining, Is.EqualTo(390));
            Assert.That(worker.CarriedSupplies, Is.EqualTo(10));
            Assert.That(worker.CurrentActivity, Is.EqualTo(WorkerAgent.Activity.Returning));
        }

        [UnityTest]
        public IEnumerator ReturningWorker_ExhaustedCacheOrderDepositsAndFallsBackToVisibleCache()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var worker = economy.Workers[0];
            var availableCache = economy.Caches[0];
            var exhaustedCache = economy.Caches[1];
            exhaustedCache.TakeBatch(int.MaxValue);

            worker.IssueGather(availableCache);
            yield return WaitUntil(() => worker.CurrentActivity == WorkerAgent.Activity.Returning &&
                                         worker.CarriedSupplies == 10);
            var scout = new GameObject("Returning worker fallback scout");
            scout.transform.position = availableCache.transform.position;
            economy.FogOfWar.RegisterFriendly(scout.transform);
            economy.FogOfWar.RefreshNow();
            worker.IssueGather(exhaustedCache);
            yield return WaitUntil(() => economy.Supplies == economy.StartingSupplies + 20);

            Assert.That(availableCache.Remaining, Is.EqualTo(380));
            Assert.That(exhaustedCache.Remaining, Is.Zero);
            Assert.That(worker.CarriedSupplies, Is.Zero);
            Assert.That(worker.CurrentActivity, Is.EqualTo(WorkerAgent.Activity.GoingToCache));
            Assert.That(economy.LastEconomyNotification, Is.Null);
            Object.Destroy(scout);
        }

        [UnityTest]
        public IEnumerator DepletedCache_UnseenFallbackMakesWorkersIdleAndNotifiesPlayer()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            foreach (var enemyWorker in economy.EnemyWorkers) enemyWorker.Suspend();
            var depletedCache = economy.Caches[0];
            var fallbackCache = economy.Caches[1];
            foreach (var crossSideCache in economy.OpponentCaches) crossSideCache.TakeBatch(int.MaxValue);
            depletedCache.Initialize(10);
            var firstWorker = economy.Workers[0];
            var secondWorker = economy.Workers[1];

            firstWorker.IssueGather(depletedCache);
            secondWorker.IssueGather(depletedCache);
            yield return WaitUntil(() => depletedCache.Remaining == 0);
            yield return WaitUntil(() => firstWorker.CurrentActivity == WorkerAgent.Activity.Idle &&
                                         secondWorker.CurrentActivity == WorkerAgent.Activity.Idle);

            Assert.That(fallbackCache.Remaining, Is.EqualTo(400),
                "Workers must not reveal or retarget to a cache outside shared current vision.");
            Assert.That(economy.LastEconomyNotification, Does.Contain("idle"));
        }

        [UnityTest]
        public IEnumerator DepletedCache_RevealedFallbackIsUsedAutomatically()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var depletedCache = economy.Caches[0];
            var fallbackCache = economy.Caches[1];
            depletedCache.Initialize(10);
            var scout = new GameObject("Fallback cache scout");
            scout.transform.position = fallbackCache.transform.position;
            economy.FogOfWar.RegisterFriendly(scout.transform);
            economy.FogOfWar.RefreshNow();
            Assert.That(economy.FogOfWar.StateAt(fallbackCache.transform.position), Is.EqualTo(FogState.Visible));

            var worker = economy.Workers[0];
            worker.IssueGather(depletedCache);
            yield return WaitUntil(() => fallbackCache.Remaining < 400);

            Assert.That(worker.CurrentActivity, Is.Not.EqualTo(WorkerAgent.Activity.Idle));
            Assert.That(economy.LastEconomyNotification, Is.Null);
            Object.Destroy(scout);
        }

        [UnityTest]
        public IEnumerator DepletedCrossSideCache_PlayerRetargetsToAnotherVisibleNeutralCache()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            foreach (var enemyWorker in economy.EnemyWorkers) enemyWorker.Suspend();
            foreach (var startingCache in economy.Caches) startingCache.TakeBatch(int.MaxValue);
            var depletedCache = economy.OpponentCaches[0];
            var fallbackCache = economy.OpponentCaches[1];
            depletedCache.Initialize(10);
            var depletedScout = new GameObject("Cross-side depleted cache scout");
            depletedScout.transform.position = depletedCache.transform.position;
            var fallbackScout = new GameObject("Cross-side fallback cache scout");
            fallbackScout.transform.position = fallbackCache.transform.position;
            economy.FogOfWar.RegisterFriendly(depletedScout.transform);
            economy.FogOfWar.RegisterFriendly(fallbackScout.transform);
            economy.FogOfWar.RefreshNow();
            Assert.That(economy.FogOfWar.StateAt(depletedCache.transform.position), Is.EqualTo(FogState.Visible));
            Assert.That(economy.FogOfWar.StateAt(fallbackCache.transform.position), Is.EqualTo(FogState.Visible));

            var originalTimeScale = Time.timeScale;
            try
            {
                Time.timeScale = 4f;
                var worker = economy.Workers[0];
                Assert.That(GetPrivateField<IReadOnlyList<ResourceCache>>(worker, "knownCaches").Count,
                    Is.EqualTo(4));
                worker.IssueGather(depletedCache);
                yield return WaitUntil(() => depletedCache.Remaining == 0 &&
                    GetPrivateField<ResourceCache>(worker, "targetCache") == fallbackCache);

                Assert.That(depletedCache.Remaining, Is.Zero);
                Assert.That(fallbackCache.Remaining, Is.GreaterThan(0));
                Assert.That(worker.CurrentActivity, Is.Not.EqualTo(WorkerAgent.Activity.Idle));
                Assert.That(economy.LastEconomyNotification, Is.Null);
            }
            finally
            {
                Time.timeScale = originalTimeScale;
                Object.Destroy(depletedScout);
                Object.Destroy(fallbackScout);
            }
        }

        [UnityTest]
        public IEnumerator DepletedCrossSideCache_OpponentRetargetsToAnotherVisibleNeutralCache()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            Assert.That(economy.EnemyWorkers.Where(worker => worker.CurrentConstruction == null).All(worker =>
                    economy.OpponentCaches.Contains(GetPrivateField<ResourceCache>(worker, "targetCache"))), Is.True,
                "The scripted opponent opening must continue assigning workers to its safe starting caches.");
            foreach (var startingCache in economy.OpponentCaches) startingCache.TakeBatch(int.MaxValue);
            var depletedCache = economy.Caches[0];
            var fallbackCache = economy.Caches[1];
            depletedCache.Initialize(10);
            economy.DeployEnemyForAutomation(FormationType.Spearmen, depletedCache.transform.position);
            economy.DeployEnemyForAutomation(FormationType.Spearmen, fallbackCache.transform.position);

            var originalTimeScale = Time.timeScale;
            try
            {
                Time.timeScale = 4f;
                var worker = economy.EnemyWorkers.First(candidate => candidate.CurrentConstruction == null);
                Assert.That(GetPrivateField<IReadOnlyList<ResourceCache>>(worker, "knownCaches").Count,
                    Is.EqualTo(4));
                worker.IssueGather(depletedCache);
                yield return WaitUntil(() => depletedCache.Remaining == 0 &&
                    GetPrivateField<ResourceCache>(worker, "targetCache") == fallbackCache);

                Assert.That(depletedCache.Remaining, Is.Zero);
                Assert.That(fallbackCache.Remaining, Is.GreaterThan(0));
                Assert.That(worker.CurrentActivity, Is.Not.EqualTo(WorkerAgent.Activity.Idle));
            }
            finally
            {
                Time.timeScale = originalTimeScale;
            }
        }

        [UnityTest]
        public IEnumerator DepletedCrossSideCache_OpponentDoesNotRetargetToUnseenNeutralCache()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            Assert.That(economy.EnemyWorkers.Where(worker => worker.CurrentConstruction == null).All(worker =>
                    economy.OpponentCaches.Contains(GetPrivateField<ResourceCache>(worker, "targetCache"))), Is.True,
                "The scripted opponent opening must continue assigning workers to its safe starting caches.");

            var worker = economy.EnemyWorkers.First(candidate => candidate.CurrentConstruction == null);
            foreach (var otherWorker in economy.EnemyWorkers.Where(candidate => candidate != worker))
                otherWorker.Suspend();
            foreach (var startingCache in economy.OpponentCaches) startingCache.TakeBatch(int.MaxValue);
            var depletedCache = economy.Caches[0];
            var unseenFallback = economy.Caches[1];
            depletedCache.Initialize(10);
            economy.DeployEnemyForAutomation(FormationType.Spearmen, depletedCache.transform.position);

            var visibility = GetPrivateField<System.Func<Vector3, bool>>(worker, "isCurrentlyVisible");
            Assert.That(GetPrivateField<IReadOnlyList<ResourceCache>>(worker, "knownCaches").Count, Is.EqualTo(4));
            Assert.That(visibility(depletedCache.transform.position), Is.True,
                "The Alazhan observer must reveal the assigned depleted cache.");
            Assert.That(visibility(unseenFallback.transform.position), Is.False,
                "The cross-side fallback must begin outside Alazhan current vision.");

            var originalTimeScale = Time.timeScale;
            try
            {
                Time.timeScale = 4f;
                worker.IssueGather(depletedCache);
                yield return WaitUntil(() => depletedCache.Remaining == 0 &&
                                             worker.CurrentActivity == WorkerAgent.Activity.Idle);

                Assert.That(unseenFallback.Remaining, Is.EqualTo(400),
                    "An Alazhan worker must not gather from an unseen cross-side cache.");
                Assert.That(GetPrivateField<ResourceCache>(worker, "targetCache"), Is.Null);
            }
            finally
            {
                Time.timeScale = originalTimeScale;
            }
        }

        [UnityTest]
        public IEnumerator DepletedCache_NoNearbySupplyMakesWorkerIdleAndNotifiesPlayer()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var worker = economy.Workers[0];
            foreach (var cache in economy.Caches) cache.TakeBatch(int.MaxValue);

            worker.IssueGather(economy.Caches[0]);
            yield return null;

            Assert.That(worker.CurrentActivity, Is.EqualTo(WorkerAgent.Activity.Idle));
            Assert.That(economy.LastEconomyNotification, Does.Contain(worker.name));
            Assert.That(economy.LastEconomyNotification, Does.Contain("idle"));
            Assert.That(GameObject.Find("Order").GetComponent<UnityEngine.UI.Text>().text,
                Does.Contain("IDLE - NO SUPPLIES CACHE NEARBY"));
        }

        [UnityTest]
        public IEnumerator GatherOrder_RequiresTheCacheToBeCurrentlyVisible()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var worker = economy.Workers[0];
            var cache = economy.Caches[1];
            Assert.That(economy.FogOfWar.StateAt(cache.transform.position), Is.Not.EqualTo(FogState.Visible));
            economy.SelectOnly(worker);
            var mouse = InputSystem.AddDevice<Mouse>();
            mouse.MakeCurrent();
            var cacheScreenPosition = Camera.main.WorldToScreenPoint(cache.transform.position + Vector3.up * 0.5f);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = cacheScreenPosition });
            InputSystem.Update();
            Physics.SyncTransforms();
            Assert.That(cache.GetComponentsInChildren<Collider>(true).All(item => !item.enabled), Is.True,
                "An unseen neutral cache must not expose an interactive collider.");

            yield return PressMouseButton(economy, mouse, cacheScreenPosition, MouseButton.Right,
                "HandleOrderInput");
            Assert.That(worker.CurrentActivity, Is.EqualTo(WorkerAgent.Activity.Moving));
            Assert.That(GameObject.Find("Order").GetComponent<Text>().text,
                Does.Not.Contain(cache.name.ToUpperInvariant()),
                "An unseen neutral collider must behave like terrain instead of leaking a gather target.");

            var scout = new GameObject("Gather order scout");
            scout.transform.position = cache.transform.position;
            economy.FogOfWar.RegisterFriendly(scout.transform);
            economy.FogOfWar.RefreshNow();
            Assert.That(cache.GetComponentsInChildren<Collider>(true).Any(item => item.enabled), Is.True,
                "Revealing a neutral cache must restore its gather collider.");
            yield return PressMouseButton(economy, mouse, cacheScreenPosition, MouseButton.Right,
                "HandleOrderInput");

            Assert.That(worker.CurrentActivity, Is.EqualTo(WorkerAgent.Activity.GoingToCache));
            Assert.That(GameObject.Find("Order").GetComponent<Text>().text,
                Does.Contain(cache.name.ToUpperInvariant()));
            Object.Destroy(scout);
            InputSystem.RemoveDevice(mouse);
        }

        [UnityTest]
        public IEnumerator NeutralCacheFog_HidesUnexploredAndPreservesLastSeenDepletionState()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var fog = economy.FogOfWar;
            var cache = economy.OpponentCaches[1];
            var renderers = cache.GetComponentsInChildren<Renderer>(true);
            var colliders = cache.GetComponentsInChildren<Collider>(true);

            Assert.That(fog.StateAt(cache.transform.position), Is.EqualTo(FogState.Unexplored));
            Assert.That(renderers, Is.Not.Empty);
            Assert.That(renderers.All(item => !item.enabled), Is.True,
                "An unexplored opponent-side Supply cache must not render.");
            Assert.That(colliders.All(item => !item.enabled), Is.True,
                "An unexplored opponent-side Supply cache must not be interactive.");
            Assert.That(economy.CurrentMatchSummary.firstContactSeconds, Is.LessThan(0f),
                "Registering neutral caches must not record hostile first contact.");

            var scout = new GameObject("Neutral cache fog scout");
            try
            {
                economy.SetOpponentEnabledForAutomation(false);
                foreach (var worker in economy.EnemyWorkers)
                {
                    worker.Suspend();
                    var agent = worker.GetComponent<NavMeshAgent>();
                    if (agent != null && agent.isOnNavMesh) agent.Warp(economy.EnemyHisar.transform.position);
                    else worker.transform.position = economy.EnemyHisar.transform.position;
                }
                fog.RefreshNow();
                scout.transform.position = cache.transform.position;
                fog.RegisterFriendly(scout.transform);
                fog.RefreshNow();
                Assert.That(renderers.All(item => item.enabled), Is.True);
                Assert.That(colliders.Any(item => item.enabled), Is.True);
                Assert.That(economy.CurrentMatchSummary.firstContactSeconds, Is.LessThan(0f),
                    "Revealing a neutral cache must not record hostile first contact.");

                scout.transform.position = new Vector3(-20f, 0f, -10f);
                fog.RefreshNow();
                Assert.That(fog.StateAt(cache.transform.position), Is.EqualTo(FogState.Explored));
                var rememberedColors = renderers.Select(item => item.material.color).ToArray();
                var terrainColor = fog.MinimapColorAt(cache.transform.position);
                Assert.That(terrainColor.r, Is.LessThan(0.9f),
                    "A neutral cache must not create a hostile minimap marker.");

                cache.TakeBatch(int.MaxValue);
                fog.RefreshNow();
                Assert.That(cache.Remaining, Is.Zero);
                for (var index = 0; index < renderers.Length; index++)
                    Assert.That(Vector4.Distance(renderers[index].material.color, rememberedColors[index]),
                        Is.LessThan(0.01f),
                        "Explored fog must preserve the last-seen cache state after unseen depletion.");

                scout.transform.position = cache.transform.position;
                fog.RefreshNow();
                foreach (var itemRenderer in renderers)
                {
                    var actual = itemRenderer.material.color;
                    Assert.That(actual.r, Is.EqualTo(0.24f).Within(0.02f));
                    Assert.That(actual.g, Is.EqualTo(0.22f).Within(0.02f));
                    Assert.That(actual.b, Is.EqualTo(0.19f).Within(0.02f),
                        "Revealing a depleted cache must expose its current exhausted state.");
                }
            }
            finally
            {
                Object.Destroy(scout);
            }
        }

    }
}
