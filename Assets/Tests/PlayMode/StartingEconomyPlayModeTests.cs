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
    public sealed class StartingEconomyPlayModeTests
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
            economy.SelectOnly(worker);
            economy.IssueGatherForSmoke(economy.Caches[0]);
            var deadline = Time.realtimeSinceStartup + 12f;
            while (economy.Supplies <= economy.StartingSupplies && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.That(economy.Supplies, Is.EqualTo(economy.StartingSupplies + 10));
            Assert.That(economy.Caches[0].Remaining, Is.EqualTo(390));
            Assert.That(worker.IsSelected, Is.True);
            Assert.That(worker.CurrentActivity, Is.EqualTo(WorkerAgent.Activity.GoingToCache));
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

        [UnityTest]
        public IEnumerator HouseConstruction_SpendsCompletesRaisesCapacityAndResumesGathering()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var worker = economy.Workers[0];
            var cache = economy.Caches[0];
            worker.IssueGather(cache);
            yield return null;

            Assert.That(economy.TryPlaceHouse(worker, VisibleHouseSite), Is.True);
            Assert.That(economy.Supplies, Is.Zero);
            Assert.That(economy.PopulationCapacity, Is.EqualTo(12));
            Assert.That(economy.Houses, Has.Count.EqualTo(1));
            Assert.That(economy.Houses[0].IsComplete, Is.False);
            worker.IssueMove(Vector3.zero);
            Assert.That(worker.CurrentConstruction, Is.SameAs(economy.Houses[0]));

            yield return WaitUntil(() => economy.Houses[0].IsComplete);

            Assert.That(economy.PopulationUsed, Is.EqualTo(4));
            Assert.That(economy.PopulationCapacity, Is.EqualTo(20));
            Assert.That(worker.CurrentActivity, Is.EqualTo(WorkerAgent.Activity.GoingToCache));
            Assert.That(GameObject.Find("Order").GetComponent<UnityEngine.UI.Text>().text,
                Does.Contain("HOUSE COMPLETE"));
        }

        [UnityTest]
        public IEnumerator HouseConstruction_CancelRefundsAndDoesNotRaiseCapacity()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var worker = economy.Workers[0];

            Assert.That(economy.TryPlaceHouse(worker, VisibleHouseSite), Is.True);
            economy.SelectOnly(worker);
            GameObject.Find("Cancel Build").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return null;

            Assert.That(economy.Supplies, Is.EqualTo(economy.StartingSupplies));
            Assert.That(economy.PopulationCapacity, Is.EqualTo(12));
            Assert.That(economy.Houses, Is.Empty);
            Assert.That(worker.CurrentActivity, Is.EqualTo(WorkerAgent.Activity.Idle));
            Assert.That(GameObject.Find("Order").GetComponent<UnityEngine.UI.Text>().text,
                Does.Contain("REFUNDED"));
        }

        [UnityTest]
        public IEnumerator InProgressBuildings_AreAttackableAndAbandonBothSidesWithoutRefund()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var tuning = GetPrivateField<EconomyTuning>(economy, "tuning");
            var hostileAttackers = CreateFormationForTest("Hostile foundation attackers",
                FormationType.Spearmen, false, tuning);
            var friendlyAttackers = CreateFormationForTest("Friendly foundation attackers",
                FormationType.Spearmen, true, tuning);

            try
            {
                var friendlyBuilder = economy.Workers[0];
                Assert.That(economy.TryPlaceHouse(friendlyBuilder, VisibleHouseSite), Is.True);
                var friendlyFoundation = economy.Houses.Single();
                Assert.That(friendlyFoundation.IsComplete, Is.False);
                Assert.That(friendlyBuilder.CurrentConstruction, Is.SameAs(friendlyFoundation));
                Assert.That(friendlyFoundation.IsAttackable, Is.True);
                Assert.That(hostileAttackers.IssueFocus(friendlyFoundation), Is.True);
                var friendlySupplies = economy.Supplies;

                Assert.That(friendlyFoundation.ApplyStructuralDamage(friendlyFoundation.Health), Is.True);

                Assert.That(friendlyFoundation.IsDestroyed, Is.True);
                Assert.That(economy.Houses, Is.Empty);
                Assert.That(friendlyBuilder.CurrentConstruction, Is.Null,
                    "Destroying the player's foundation must abandon its assigned builder.");
                Assert.That(economy.Supplies, Is.EqualTo(friendlySupplies),
                    "Enemy destruction of unfinished construction must not refund Supplies.");
                Assert.That(economy.PopulationCapacity, Is.EqualTo(12));

                var hostileFoundation = economy.EnemyBuildings.Single(building => !building.IsComplete);
                var hostileBuilder = economy.EnemyWorkers.Single(worker =>
                    ReferenceEquals(worker.CurrentConstruction, hostileFoundation));
                Assert.That(hostileFoundation.IsAttackable, Is.True);
                Assert.That(friendlyAttackers.IssueFocus(hostileFoundation), Is.True);
                var hostileSupplies = economy.OpponentSupplies;

                Assert.That(hostileFoundation.ApplyStructuralDamage(hostileFoundation.Health), Is.True);

                Assert.That(hostileFoundation.IsDestroyed, Is.True);
                Assert.That(economy.EnemyBuildings.Contains(hostileFoundation), Is.False);
                Assert.That(hostileBuilder.CurrentConstruction, Is.Null,
                    "Destroying the opponent's foundation must clear its real construction state.");
                Assert.That(economy.OpponentSupplies, Is.EqualTo(hostileSupplies),
                    "The opponent must obey the same no-refund destruction rule.");
                Assert.That(economy.OpponentPopulationCapacity, Is.EqualTo(12));
                yield return null;
            }
            finally
            {
                Object.Destroy(hostileAttackers.gameObject);
                Object.Destroy(friendlyAttackers.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator BuilderDeaths_DestroyAndRecordBothFoundationsExactlyOnce()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var friendlyBuilder = economy.Workers[0];
            Assert.That(economy.TryPlaceHouse(friendlyBuilder, VisibleHouseSite), Is.True);
            var friendlyFoundation = economy.Houses.Single();
            var friendlyDestroyed = economy.CurrentMatchSummary.friendlyBuildingsDestroyed;
            var friendlyLost = economy.CurrentMatchSummary.friendlyEntitiesLost;

            friendlyBuilder.ApplyFixedDamage(friendlyBuilder.Health);

            Assert.That(friendlyFoundation.IsDestroyed, Is.True);
            Assert.That(economy.Houses, Is.Empty);
            Assert.That(economy.CurrentMatchSummary.friendlyBuildingsDestroyed,
                Is.EqualTo(friendlyDestroyed + 1));
            Assert.That(economy.CurrentMatchSummary.friendlyEntitiesLost, Is.EqualTo(friendlyLost + 1));
            Assert.That(friendlyFoundation.ApplyStructuralDamage(friendlyFoundation.Health), Is.False);
            Assert.That(economy.CurrentMatchSummary.friendlyBuildingsDestroyed,
                Is.EqualTo(friendlyDestroyed + 1), "A removed foundation must be recorded exactly once.");

            var hostileFoundation = economy.EnemyBuildings.Single(building => !building.IsComplete);
            var hostileBuilder = economy.EnemyWorkers.Single(worker =>
                ReferenceEquals(worker.CurrentConstruction, hostileFoundation));
            var hostileDestroyed = economy.CurrentMatchSummary.hostileBuildingsDestroyed;
            var hostileLost = economy.CurrentMatchSummary.hostileEntitiesLost;

            hostileBuilder.ApplyFixedDamage(hostileBuilder.Health);

            Assert.That(hostileFoundation.IsDestroyed, Is.True);
            Assert.That(economy.EnemyBuildings.Contains(hostileFoundation), Is.False);
            Assert.That(economy.CurrentMatchSummary.hostileBuildingsDestroyed,
                Is.EqualTo(hostileDestroyed + 1));
            Assert.That(economy.CurrentMatchSummary.hostileEntitiesLost, Is.EqualTo(hostileLost + 1));
            Assert.That(hostileFoundation.ApplyStructuralDamage(hostileFoundation.Health), Is.False);
            Assert.That(economy.CurrentMatchSummary.hostileBuildingsDestroyed,
                Is.EqualTo(hostileDestroyed + 1), "A removed hostile foundation must be recorded exactly once.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator HousePlacement_InvalidPositionDoesNotSpendSupplies()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();

            Assert.That(economy.TryPlaceHouse(economy.Workers[0], new Vector3(-7f, 0f, 4f)), Is.False);

            Assert.That(economy.Supplies, Is.EqualTo(economy.StartingSupplies));
            Assert.That(economy.Houses, Is.Empty);
            Assert.That(GameObject.Find("Order").GetComponent<UnityEngine.UI.Text>().text,
                Does.Contain("INVALID"));
        }

        [UnityTest]
        public IEnumerator BuildingPlacement_RejectsExploredAndUnexploredTerrainWithoutSpendingSupplies()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var worker = economy.Workers[0];
            var exploredPosition = new Vector3(12f, 0f, 10f);
            var unexploredPosition = new Vector3(-12f, 0f, 20f);
            var scout = new GameObject("Placement visibility scout");
            scout.transform.position = exploredPosition;
            economy.FogOfWar.RegisterFriendly(scout.transform);
            economy.FogOfWar.RefreshNow();
            scout.transform.position = new Vector3(0f, 0f, -8f);
            economy.FogOfWar.RefreshNow();

            Assert.That(economy.FogOfWar.StateAt(exploredPosition), Is.EqualTo(FogState.Explored));
            Assert.That(economy.FogOfWar.StateAt(unexploredPosition), Is.EqualTo(FogState.Unexplored));
            Assert.That(economy.TryPlaceHouse(worker, exploredPosition), Is.False);
            Assert.That(GameObject.Find("Order").GetComponent<Text>().text,
                Does.Contain("NOT CURRENTLY VISIBLE"));
            Assert.That(economy.TryPlaceHouse(worker, unexploredPosition), Is.False);

            Assert.That(economy.Supplies, Is.EqualTo(economy.StartingSupplies));
            Assert.That(economy.Houses, Is.Empty);
            Object.Destroy(scout);
        }

        [UnityTest]
        public IEnumerator HousePlacement_WorkerOccupiedFootprintsDoNotSpendSupplies()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var assignedWorker = economy.Workers[0];
            var otherWorker = economy.Workers[1];
            Assert.That(assignedWorker.GetComponent<NavMeshAgent>().Warp(new Vector3(0f, 0f, 4f)), Is.True);
            Assert.That(otherWorker.GetComponent<NavMeshAgent>().Warp(new Vector3(5f, 0f, 4f)), Is.True);
            Physics.SyncTransforms();

            Assert.That(economy.TryPlaceHouse(assignedWorker, assignedWorker.transform.position), Is.False);
            Assert.That(economy.TryPlaceHouse(assignedWorker, otherWorker.transform.position), Is.False);

            Assert.That(economy.Supplies, Is.EqualTo(economy.StartingSupplies));
            Assert.That(economy.Houses, Is.Empty);
            Assert.That(GameObject.Find("Order").GetComponent<UnityEngine.UI.Text>().text,
                Does.Contain("POSITION OCCUPIED"));
        }

        [UnityTest]
        public IEnumerator HousePlacement_LastNavigableRouteIsRejectedByNavMesh()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            CreateRouteBlocker("Left Route Blocker", new Vector3(-13.6f, 1f, 10f), new Vector3(22.8f, 2f, 4f));
            CreateRouteBlocker("Right Route Blocker", new Vector3(13.6f, 1f, 10f), new Vector3(22.8f, 2f, 4f));
            yield return null;
            yield return null;

            var worker = economy.Workers[0];
            var scout = new GameObject("Route placement scout");
            scout.transform.position = new Vector3(0f, 0f, 10f);
            economy.FogOfWar.RegisterFriendly(scout.transform);
            economy.FogOfWar.RefreshNow();
            Assert.That(worker.CanReach(new Vector3(0f, 0f, 20f)), Is.True,
                "The worker must be able to navigate through the final open gap before placement.");

            Assert.That(economy.TryPlaceHouse(worker, new Vector3(0f, 0f, 10f)), Is.False);

            Assert.That(economy.Supplies, Is.EqualTo(economy.StartingSupplies));
            Assert.That(economy.Houses, Is.Empty);
            Assert.That(GameObject.Find("Order").GetComponent<UnityEngine.UI.Text>().text,
                Does.Contain("MUST PRESERVE A ROUTE"));
            Object.Destroy(scout);
        }

        [UnityTest]
        public IEnumerator StorehouseConstruction_CancelsRefundsCompletesAndBecomesNearestDropOff()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var worker = economy.Workers[0];
            economy.CreditSuppliesForAutomation(100);

            Assert.That(economy.TryPlaceStorehouse(worker, VisibleStorehouseSite), Is.True);
            Assert.That(economy.Supplies, Is.Zero);
            Assert.That(economy.CancelConstruction(worker), Is.True);
            yield return null;
            Assert.That(economy.Supplies, Is.EqualTo(200));
            Assert.That(economy.Storehouses, Is.Empty);

            Assert.That(economy.TryPlaceStorehouse(worker, VisibleStorehouseSite), Is.True);
            yield return WaitUntil(() => economy.Storehouses.Count == 1 && economy.Storehouses[0].IsComplete);
            var storehouse = economy.Storehouses[0];
            worker.IssueGather(economy.Caches[1]);
            yield return WaitUntil(() => worker.CurrentActivity == WorkerAgent.Activity.Returning &&
                                         worker.CarriedSupplies == 10);

            Assert.That(Vector3.Distance(worker.LastDropOffPoint, storehouse.DropOffPoint), Is.LessThan(0.01f));
            yield return WaitUntil(() => economy.Supplies == 10);
            yield return WaitUntil(() => worker.CurrentActivity == WorkerAgent.Activity.Returning &&
                                         worker.CarriedSupplies == 10);
            economy.SelectOnly(storehouse);
            Assert.That(economy.RequestDemolition(), Is.False);
            Assert.That(economy.RequestDemolition(), Is.True);
            yield return WaitUntil(() => economy.Storehouses.Count == 0);
            yield return WaitUntil(() => economy.Supplies == 20);
            var hisar = GameObject.Find(StartingEconomyController.HisarObjectName).GetComponent<Hisar>();
            Assert.That(Vector3.Distance(worker.LastDropOffPoint, hisar.DropOffPoint), Is.LessThan(0.01f));
            Assert.That(GameObject.Find("Order").GetComponent<UnityEngine.UI.Text>().text,
                Does.Not.Contain("IDLE"));
        }

        [UnityTest]
        public IEnumerator Watchtower_SupportsMovingAssaultAndTargetsNearestHostileInRange()
        {
            var tuning = ScriptableObject.CreateInstance<EconomyTuning>();
            var towerObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            towerObject.name = "Supporting Watchtower";
            var building = towerObject.AddComponent<ConstructibleBuilding>();
            building.Initialize(BuildingType.Watchtower, 0.1f, tuning.buildingHealth, Color.blue, _ => { });
            Assert.That(building.Advance(0.1f), Is.True);

            var nearest = CreateFormationForTest("Nearest hostile", FormationType.Spearmen, false, tuning);
            nearest.transform.position = new Vector3(0f, 0f, 8f);
            var farther = CreateFormationForTest("Farther hostile", FormationType.Spearmen, false, tuning);
            farther.transform.position = new Vector3(4f, 0f, 7.5f);
            var outOfRange = CreateFormationForTest("Out of range hostile", FormationType.Spearmen, false, tuning);
            outOfRange.transform.position = new Vector3(0f, 0f, 10f);
            var targets = new List<FormationAgent> { farther, outOfRange, nearest };
            var tower = towerObject.AddComponent<WatchtowerAttack>();
            tower.Initialize(tuning, () => targets);

            yield return WaitUntil(() => nearest.TotalMemberHealth < tuning.memberHealth * 8);
            Assert.That(GameObject.FindObjectsByType<Renderer>(FindObjectsSortMode.None)
                .Any(itemRenderer => itemRenderer.name == "Watchtower Projectile"), Is.True);
            Assert.That(tower.CurrentTarget, Is.SameAs(nearest));
            Assert.That(farther.TotalMemberHealth, Is.EqualTo(tuning.memberHealth * 8));
            Assert.That(outOfRange.TotalMemberHealth, Is.EqualTo(tuning.memberHealth * 8));

            targets.Remove(farther);
            targets.Remove(outOfRange);
            var assaultDestination = CreateFormationForTest("Assault destination", FormationType.Archers, true, tuning);
            assaultDestination.transform.position = new Vector3(0f, 0f, -12f);
            Assert.That(nearest.IssueFocus(assaultDestination), Is.True);
            assaultDestination.enabled = false;
            yield return WaitUntil(() => Vector3.Distance(nearest.transform.position, towerObject.transform.position) >
                                         tuning.watchtowerRange && nearest.transform.position.z < 0f);
            yield return new WaitForSeconds(tuning.projectileSeconds + 0.1f);

            Assert.That(nearest.MemberCount, Is.EqualTo(8),
                "A formation moving through one tower's range must survive intact rather than be erased.");
            Assert.That(nearest.TotalMemberHealth, Is.LessThanOrEqualTo(tuning.memberHealth * 6),
                "The supporting tower should still inflict meaningful deterministic damage during the assault.");
            targets.Clear();
            Object.Destroy(nearest.gameObject);
            Object.Destroy(assaultDestination.gameObject);
            yield return null;

            farther.transform.position = new Vector3(0f, 0f, 8f);
            targets.Add(farther);
            yield return WaitUntil(() => farther.MemberCount < 8);
            tower.enabled = false;

            Assert.That(farther.MemberCount, Is.InRange(6, 7),
                "Sustained tower fire may cause casualties but must leave meaningful survivors.");
            Object.Destroy(towerObject);
            Object.Destroy(farther.gameObject);
            Object.Destroy(outOfRange.gameObject);
            Object.Destroy(tuning);
        }

        [UnityTest]
        public IEnumerator BuildHotkeys_KeepDForCameraAndUseRForStorehousePlacement()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            economy.CreditSuppliesForAutomation(100);
            economy.SelectOnly(economy.Workers[0]);
            var keyboard = Keyboard.current ?? InputSystem.AddDevice<Keyboard>();
            var mouse = Mouse.current ?? InputSystem.AddDevice<Mouse>();
            var cameraController = Object.FindAnyObjectByType<RtsCameraController>();
            var cameraStart = cameraController.transform.position;

            InputSystem.QueueStateEvent(mouse, new MouseState
            {
                position = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)
            });
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
            InputSystem.Update();
            InvokePrivateMethod(cameraController, "Update");
            InvokePrivateMethod(economy, "HandleBuildInput");

            Assert.That(cameraController.transform.position.x, Is.GreaterThan(cameraStart.x));
            Assert.That(economy.IsBuildingPlacementActive, Is.False,
                "Pressing D to pan the camera must not trigger a contextual worker command.");

            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            GameObject.Find("Build Storehouse").GetComponent<Button>().onClick.Invoke();
            Assert.That(economy.IsBuildingPlacementActive, Is.True);
            Assert.That(GameObject.Find("Storehouse Placement Preview"), Is.Not.Null);

            InvokePrivateMethod(economy, "EndBuildingPlacement", "Storehouse placement cancelled");
            yield return null;
            Assert.That(economy.IsBuildingPlacementActive, Is.False);
        }

        [UnityTest]
        public IEnumerator Placement_DisablesBuildSwitchingAndIgnoresHudPointerClicks()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            economy.CreditSuppliesForAutomation(500);
            economy.SelectOnly(economy.Workers[0]);
            var houseButton = GameObject.Find("Build House").GetComponent<Button>();
            var storehouseButton = GameObject.Find("Build Storehouse").GetComponent<Button>();
            houseButton.onClick.Invoke();
            yield return null;

            Assert.That(economy.IsBuildingPlacementActive, Is.True);
            Assert.That(storehouseButton.interactable, Is.False);
            Assert.That(PlacementPreviewCount(), Is.EqualTo(1));
            Assert.That(GameObject.Find("House Placement Preview"), Is.Not.Null);

            var pointer = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(null, storehouseButton.transform.position)
            };
            ExecuteEvents.Execute(storehouseButton.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            yield return null;
            Assert.That(GameObject.Find("House Placement Preview"), Is.Not.Null);
            Assert.That(GameObject.Find("Storehouse Placement Preview"), Is.Null);
            Assert.That(PlacementPreviewCount(), Is.EqualTo(1));

            var mouse = Mouse.current ?? InputSystem.AddDevice<Mouse>();
            SetPrivateField(economy, "placementPosition", new Vector3(12f, 0f, -1f));
            SetPrivateField(economy, "placementValid", true);

            InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer.position }
                .WithButton(MouseButton.Left));
            InputSystem.Update();
            InvokePrivateMethod(economy, "HandleBuildInput");
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer.position });
            InputSystem.Update();
            yield return null;

            Assert.That(economy.Houses, Is.Empty,
                "Clicking the HUD during placement must not place the building behind it.");
            Assert.That(economy.Supplies, Is.EqualTo(600));
            Assert.That(economy.IsBuildingPlacementActive, Is.True);
            Assert.That(PlacementPreviewCount(), Is.EqualTo(1));

            InvokePrivateMethod(economy, "EndBuildingPlacement", "House placement cancelled");
            yield return null;
            Assert.That(economy.IsBuildingPlacementActive, Is.False);
            Assert.That(PlacementPreviewCount(), Is.Zero);
        }

        [UnityTest]
        public IEnumerator CompletedBuilding_DemolitionRequiresConfirmationAndNeverRefunds()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            economy.CreditSuppliesForAutomation(100);
            Assert.That(economy.TryPlaceStorehouse(economy.Workers[0], VisibleStorehouseSite), Is.True);
            yield return WaitUntil(() => economy.Storehouses.Count == 1 && economy.Storehouses[0].IsComplete);
            var storehouse = economy.Storehouses[0];
            economy.SelectOnly(storehouse);

            Assert.That(economy.RequestDemolition(), Is.False);
            Assert.That(economy.Storehouses, Has.Count.EqualTo(1));
            Assert.That(economy.Supplies, Is.Zero);
            Assert.That(GameObject.Find("Demolish Building").GetComponentInChildren<UnityEngine.UI.Text>().text,
                Does.Contain("CONFIRM"));
            Assert.That(economy.RequestDemolition(), Is.True);
            yield return WaitUntil(() => economy.Storehouses.Count == 0);

            Assert.That(economy.Supplies, Is.Zero);
            Assert.That(storehouse.IsDestroyed, Is.True);
            Assert.That(GameObject.Find("Order").GetComponent<UnityEngine.UI.Text>().text,
                Does.Contain("NO REFUND"));
        }

        [UnityTest]
        public IEnumerator CompletedHouse_DemolitionRemovesItsPopulationCapacity()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            Assert.That(economy.TryPlaceHouse(economy.Workers[0], VisibleHouseSite), Is.True);
            yield return WaitUntil(() => economy.Houses.Count == 1 && economy.Houses[0].IsComplete);
            var house = economy.Houses[0];
            Assert.That(economy.PopulationCapacity, Is.EqualTo(20));
            economy.SelectOnly(house);

            Assert.That(economy.RequestDemolition(), Is.False);
            Assert.That(economy.RequestDemolition(), Is.True);
            yield return WaitUntil(() => economy.Houses.Count == 0);

            Assert.That(economy.PopulationCapacity, Is.EqualTo(12));
            Assert.That(economy.Supplies, Is.Zero);
        }

        [UnityTest]
        public IEnumerator HisarQueue_TrainsFormationAndArchersWinReadableCounterFight()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            economy.CreditSuppliesForAutomation(300);
            economy.SelectHisar();
            yield return null;

            Assert.That(GameObject.Find("Train Archers").activeInHierarchy, Is.True);
            Assert.That(economy.TryQueueFormation(FormationType.Archers), Is.True);
            Assert.That(economy.Supplies, Is.Zero);
            Assert.That(economy.PopulationUsed, Is.EqualTo(12));
            Assert.That(economy.CancelActiveTraining(), Is.True);
            Assert.That(economy.Supplies, Is.EqualTo(400));
            Assert.That(economy.PopulationUsed, Is.EqualTo(4));
            Assert.That(economy.TryQueueFormation(FormationType.Archers), Is.True);

            yield return WaitUntil(() => economy.FriendlyFormations.Count == 1);
            economy.DeployEnemyForAutomation(FormationType.Spearmen, new Vector3(0f, 0f, 17f));
            var archers = economy.FriendlyFormations[0];
            var spearmen = economy.EnemyFormations[0];
            Assert.That(archers.MemberCount, Is.EqualTo(8));
            Assert.That(spearmen.MemberCount, Is.EqualTo(8));
            Assert.That(economy.FogOfWar.IsCurrentlyVisible(spearmen), Is.False);
            economy.SelectOnly(archers);
            economy.IssueAttackMoveForSelected(spearmen.transform.position);
            yield return WaitUntil(() => economy.FogOfWar.IsCurrentlyVisible(spearmen));

            yield return WaitUntil(() => economy.EnemyFormations.Count == 0);

            Assert.That(archers.MemberCount, Is.GreaterThanOrEqualTo(4));
            Assert.That(economy.PopulationUsed, Is.EqualTo(4 + archers.MemberCount));
            Assert.That(GameObject.Find("Order").GetComponent<UnityEngine.UI.Text>().text,
                Does.Contain("ENEMY FORMATION DEFEATED"));
        }

        [UnityTest]
        public IEnumerator FormationVisuals_UseSupportedUrpMaterialsForBodiesMarkersRingsAndArrows()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            economy.CreditSuppliesForAutomation(300);
            Assert.That(economy.TryQueueFormation(FormationType.Archers), Is.True);
            yield return WaitUntil(() => economy.FriendlyFormations.Count == 1);
            var archers = economy.FriendlyFormations[0];
            economy.DeployEnemyForAutomation(FormationType.Spearmen, new Vector3(0f, 0f, 17f));
            var spearmen = economy.EnemyFormations[0];

            Assert.That(archers.HasSupportedVisualMaterials(), Is.True);
            Assert.That(spearmen.HasSupportedVisualMaterials(), Is.True);
            var friendlyMarkers = archers.GetComponentsInChildren<Renderer>(true)
                .Where(itemRenderer => itemRenderer.name == "Black Falcon Diamond").ToArray();
            var hostileMarkers = spearmen.GetComponentsInChildren<Renderer>(true)
                .Where(itemRenderer => itemRenderer.name == "Living Flame Square").ToArray();
            Assert.That(friendlyMarkers, Has.Length.EqualTo(8));
            Assert.That(hostileMarkers, Has.Length.EqualTo(8));
            Assert.That(friendlyMarkers.All(itemRenderer => itemRenderer.transform.localRotation != Quaternion.identity),
                Is.True);
            Assert.That(hostileMarkers.All(itemRenderer => itemRenderer.transform.localRotation == Quaternion.identity),
                Is.True);
            Assert.That(archers.GetComponentInChildren<FormationFrontIndicator>(true), Is.Not.Null);
            Assert.That(spearmen.GetComponentInChildren<FormationFrontIndicator>(true), Is.Not.Null);

            Assert.That(archers.ExecuteAttackVolley(spearmen), Is.True);
            yield return null;
            var arrows = GameObject.FindObjectsByType<Renderer>(FindObjectsSortMode.None)
                .Where(itemRenderer => itemRenderer.name == "Arrow").ToArray();
            Assert.That(arrows, Has.Length.EqualTo(8));
            Assert.That(arrows.All(FormationAgent.UsesSupportedMaterial), Is.True);
            archers.ApplyFixedDamage(archers.MaximumMemberHealth);
            Assert.That(archers.GetComponentInChildren<FormationFrontIndicator>(true), Is.Not.Null,
                "The front indicator must survive member casualties.");
        }

        [UnityTest]
        public IEnumerator Combat_LivingMembersDriveOutputAndNonlethalHitsFlashTheMember()
        {
            var tuning = ScriptableObject.CreateInstance<EconomyTuning>();
            var fullAttackers = CreateFormationForTest("Full attackers", FormationType.Spearmen, true, tuning);
            var reducedAttackers = CreateFormationForTest("Reduced attackers", FormationType.Spearmen, true, tuning);
            var fullTarget = CreateFormationForTest("Full target", FormationType.Archers, false, tuning);
            var reducedTarget = CreateFormationForTest("Reduced target", FormationType.Archers, false, tuning);

            for (var i = 0; i < 7; i++) reducedAttackers.ApplyDeterministicHit(FormationType.Archers);
            Assert.That(reducedAttackers.MemberCount, Is.EqualTo(1));

            var fullHealthBefore = fullTarget.TotalMemberHealth;
            var reducedHealthBefore = reducedTarget.TotalMemberHealth;
            Assert.That(fullAttackers.ExecuteAttackVolley(fullTarget), Is.True);
            Assert.That(reducedAttackers.ExecuteAttackVolley(reducedTarget), Is.True);

            Assert.That(fullAttackers.LastAttackMemberCount, Is.EqualTo(8));
            Assert.That(reducedAttackers.LastAttackMemberCount, Is.EqualTo(1));
            Assert.That(fullHealthBefore - fullTarget.TotalMemberHealth, Is.EqualTo(80));
            Assert.That(reducedHealthBefore - reducedTarget.TotalMemberHealth, Is.EqualTo(10));
            Assert.That(fullTarget.MemberCount, Is.EqualTo(8), "A base-damage volley must be nonlethal per member.");
            Assert.That(fullTarget.GetComponentsInChildren<FormationMemberVisual>()
                .All(visual => visual.IsShowingHitFeedback), Is.True);
            Assert.That(reducedTarget.GetComponentsInChildren<FormationMemberVisual>()
                .Count(visual => visual.IsShowingHitFeedback), Is.EqualTo(1));

            yield return new WaitForSeconds(0.2f);
            Assert.That(fullTarget.GetComponentsInChildren<FormationMemberVisual>()
                .Any(visual => visual.IsShowingHitFeedback), Is.False);

            Object.Destroy(fullAttackers.gameObject);
            Object.Destroy(reducedAttackers.gameObject);
            Object.Destroy(fullTarget.gameObject);
            Object.Destroy(reducedTarget.gameObject);
            Object.Destroy(tuning);
        }

        [UnityTest]
        public IEnumerator MeleeAttackFeedback_FiresOnlyWhenAtLeastOneMemberStrikes()
        {
            var tuning = ScriptableObject.CreateInstance<EconomyTuning>();
            var attackCueCount = 0;
            var attackers = CreateFormationForTest("Cooldown attackers", FormationType.Spearmen, true, tuning,
                onAttack: _ => attackCueCount++);
            var target = CreateFormationForTest("Cooldown target", FormationType.Archers, false, tuning);

            var healthBefore = target.TotalMemberHealth;
            Assert.That(attackers.ExecuteAttackVolley(target), Is.True);
            Assert.That(attackers.LastAttackMemberCount, Is.EqualTo(8));
            Assert.That(attackCueCount, Is.EqualTo(1));
            var healthAfterStrike = target.TotalMemberHealth;
            Assert.That(healthAfterStrike, Is.LessThan(healthBefore));

            for (var frame = 0; frame < 5; frame++)
            {
                Assert.That(attackers.ExecuteAttackVolley(target), Is.True);
                Assert.That(attackers.LastAttackMemberCount, Is.Zero);
                yield return null;
            }

            Assert.That(target.TotalMemberHealth, Is.EqualTo(healthAfterStrike),
                "Cooldown-only frames must not apply another member strike.");
            Assert.That(attackCueCount, Is.EqualTo(1),
                "Cooldown-only frames must not emit formation attack feedback.");

            Object.Destroy(attackers.gameObject);
            Object.Destroy(target.gameObject);
            Object.Destroy(tuning);
        }

        [UnityTest]
        public IEnumerator Combat_ReorientationBlocksAttacksForFixedDurationAndHudShowsFacingState()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var tuning = GetPrivateField<EconomyTuning>(economy, "tuning");
            var attacker = economy.DeployFriendlyForAutomation(FormationType.Spearmen,
                new Vector3(0f, 0f, 7f));
            var defender = economy.DeployEnemyForAutomation(FormationType.Spearmen,
                new Vector3(0f, 0f, 8.5f));
            attacker.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            economy.FogOfWar.RefreshNow();
            economy.SelectOnly(attacker);
            Assert.That(attacker.IssueFocus(defender), Is.True);

            yield return null;
            var healthBefore = defender.TotalMemberHealth;
            Assert.That(attacker.IsTurning, Is.True);
            yield return null;
            Assert.That(GameObject.Find("Selection").GetComponent<UnityEngine.UI.Text>().text,
                Does.Contain("FACING").And.Contain("TURNING"));

            yield return new WaitForSeconds(tuning.reorientationSeconds * 0.75f);
            Assert.That(defender.TotalMemberHealth, Is.EqualTo(healthBefore),
                "The formation must not attack before its fixed reorientation completes.");
            yield return WaitUntil(() => defender.TotalMemberHealth < healthBefore);
            Assert.That(attacker.IsTurning, Is.False);
            Assert.That(Vector3.Angle(attacker.transform.forward,
                defender.transform.position - attacker.transform.position), Is.LessThan(3f));
            attacker.IssueStop();
            Assert.That(attacker.IsTurning, Is.False);
            Assert.That(attacker.CurrentOrder, Is.EqualTo(FormationOrder.Idle));
        }

        [UnityTest]
        public IEnumerator ArcherProjectile_ResolvesFlankAgainstDefenderFacingAtImpact()
        {
            var tuning = ScriptableObject.CreateInstance<EconomyTuning>();
            var archers = CreateFormationForTest("Flanking Archers", FormationType.Archers, true, tuning);
            var target = CreateFormationForTest("Turning Archers", FormationType.Archers, false, tuning);
            archers.transform.position = Vector3.zero;
            target.transform.position = new Vector3(0f, 0f, 6f);
            target.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            var healthBefore = target.TotalMemberHealth;

            Assert.That(archers.ExecuteAttackVolley(target), Is.True);
            yield return null;
            target.transform.rotation = Quaternion.identity;
            yield return new WaitForSeconds(tuning.projectileSeconds + 0.1f);

            var rearDamage = CombatRules.Damage(FormationType.Archers, FormationType.Archers,
                tuning.baseDamage, tuning.counterMultiplier, FlankDirection.Rear,
                tuning.sideDamageMultiplier, tuning.rearDamageMultiplier);
            Assert.That(healthBefore - target.TotalMemberHealth, Is.EqualTo(rearDamage * 8));
            Assert.That(target.LastReceivedFlank, Is.EqualTo(FlankDirection.Rear));
            Assert.That(target.GetComponentsInChildren<FormationMemberVisual>()
                .All(visual => visual.LastHitFlank == FlankDirection.Rear), Is.True,
                "Every projectile impact should display the stronger rear-hit reaction.");
            Object.Destroy(archers.gameObject);
            Object.Destroy(target.gameObject);
            Object.Destroy(tuning);
        }

        [UnityTest]
        public IEnumerator ArcherProjectiles_TrackAndDamageEightIndividualMembers()
        {
            var tuning = ScriptableObject.CreateInstance<EconomyTuning>();
            tuning.reorientationSeconds = 2f;
            var archers = CreateFormationForTest("Individual Archers", FormationType.Archers, true, tuning);
            var cavalry = CreateFormationForTest("Individual Cavalry", FormationType.Cavalry, false, tuning);
            archers.transform.position = Vector3.zero;
            cavalry.transform.position = new Vector3(4f, 0f, 6f);
            yield return null;
            var displacedTarget = cavalry.Members[0];
            displacedTarget.TeleportBy(Vector3.right * 2f);
            var healthBefore = cavalry.Members.ToDictionary(member => member.Identity, member => member.Health);
            archers.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            Assert.That(archers.IssueFocus(cavalry), Is.True);
            yield return new WaitForSeconds(0.3f);

            foreach (var archer in archers.Members)
            {
                var expectedTarget = cavalry.Members[archer.Identity % cavalry.MemberCount];
                Assert.That(archer.AttackTarget, Is.SameAs(expectedTarget));
                Assert.That(Vector3.Angle(archer.transform.forward,
                        expectedTarget.WorldPosition - archer.WorldPosition), Is.LessThan(3f),
                    $"Archer {archer.Identity} must face the same soldier reserved for its projectile.");
            }

            Assert.That(archers.ExecuteAttackVolley(cavalry), Is.True);
            yield return WaitUntil(() => archers.ProjectileHitsLanded == 8);

            Assert.That(cavalry.Members, Has.Count.EqualTo(8));
            Assert.That(cavalry.Members.All(member => member.ProjectileImpactCount == 1), Is.True,
                "A volley must reserve one visible projectile for each living target soldier.");
            Assert.That(archers.ProjectileHitsLanded, Is.EqualTo(8));
            Assert.That(cavalry.Members.All(member => member.Health < healthBefore[member.Identity]), Is.True,
                "Projectile damage must belong to the soldier the arrow visibly reaches.");
            Assert.That(cavalry.Members.All(member =>
                    Vector3.Distance(member.LastProjectileImpactPosition, member.WorldPosition) < 0.35f), Is.True,
                "Each arrow must finish at its moving member target rather than the formation center.");

            Object.Destroy(archers.gameObject);
            Object.Destroy(cavalry.gameObject);
            Object.Destroy(tuning);
        }

        [UnityTest]
        public IEnumerator FormationMembers_FlowBehindTheAnchorAndRegroupWithoutTeleporting()
        {
            var tuning = ScriptableObject.CreateInstance<EconomyTuning>();
            var formation = CreateFormationForTest("Flowing Formation", FormationType.Spearmen, true, tuning);
            formation.transform.position = Vector3.zero;
            yield return null;

            formation.IssueMove(new Vector3(5f, 0f, 5f));
            yield return new WaitForSeconds(0.25f);
            Assert.That(formation.Members.Any(member =>
                    Vector3.Distance(member.WorldPosition, member.AssignedSlotWorldPosition) > 0.15f), Is.True,
                "Members must retain their own world positions while the command anchor moves and turns.");

            yield return new WaitForSeconds(2.5f);
            Assert.That(formation.Members.All(member =>
                    Vector3.Distance(member.WorldPosition, member.AssignedSlotWorldPosition) < 0.45f), Is.True,
                "Members must naturally catch their assigned slots after the formation stops.");

            Object.Destroy(formation.gameObject);
            Object.Destroy(tuning);
        }

        [UnityTest]
        public IEnumerator FormationMember_RoutesAroundCarvedObstacleWithoutLeavingNavMesh()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var formation = economy.DeployFriendlyForAutomation(FormationType.Spearmen,
                new Vector3(0f, 0f, 10f));
            yield return null;
            var member = formation.Members[0];
            var destination = member.AssignedSlotWorldPosition;
            var startPosition = member.WorldPosition + Vector3.left * 6f;
            var blocker = CreateRouteBlocker("Member Route Blocker", new Vector3(-4.7f, 1f, 10f),
                new Vector3(1.5f, 2f, 4f));
            var blockerBounds = blocker.GetComponent<Collider>().bounds;
            var route = new NavMeshPath();
            var groundedStart = new Vector3(startPosition.x, 0f, startPosition.z);
            var groundedDestination = new Vector3(destination.x, 0f, destination.z);

            yield return new WaitForSeconds(1f);
            var sampledStart = NavMesh.SamplePosition(groundedStart, out var start, 0.35f, NavMesh.AllAreas);
            var sampledEnd = NavMesh.SamplePosition(groundedDestination, out var end, 0.35f, NavMesh.AllAreas);
            var calculated = sampledStart && sampledEnd &&
                             NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, route);
            Assert.That(sampledStart, Is.True, $"No NavMesh near member start {groundedStart}.");
            Assert.That(sampledEnd, Is.True, $"No NavMesh near member slot {groundedDestination}.");
            Assert.That(calculated, Is.True, "The carved obstacle must leave a route around its sides.");
            Assert.That(route.status, Is.EqualTo(NavMeshPathStatus.PathComplete));
            Assert.That(route.corners.Length, Is.GreaterThan(2),
                $"The carved obstacle must force a detour, but the route had {route.corners.Length} corners.");
            member.TeleportBy(Vector3.left * 6f);

            var greatestDetour = 0f;
            var previousPosition = member.WorldPosition;
            var deadline = Time.realtimeSinceStartup + 5f;
            while (Vector3.Distance(member.WorldPosition, destination) > 0.45f &&
                   Time.realtimeSinceStartup < deadline)
            {
                var position = member.WorldPosition;
                var frameDisplacement = Vector3.Distance(position, previousPosition);
                var maximumFrameDisplacement = formation.MoveSpeed * 1.3f * Time.deltaTime + 0.025f;
                Assert.That(frameDisplacement, Is.LessThanOrEqualTo(maximumFrameDisplacement),
                    $"A member must traverse path corners at its bounded movement speed. " +
                    $"Moved={frameDisplacement:0.000}, maximum={maximumFrameDisplacement:0.000}.");
                AssertSweptSegmentOutsideBounds(previousPosition, position, blockerBounds);
                Assert.That(blockerBounds.Contains(new Vector3(position.x, blockerBounds.center.y, position.z)),
                    Is.False, "A member must never step through the carved obstacle.");
                var groundedPosition = new Vector3(position.x, 0f, position.z);
                Assert.That(NavMesh.SamplePosition(groundedPosition, out var walkable, 0.1f, NavMesh.AllAreas), Is.True,
                    "Every independently steered member step must remain on the NavMesh.");
                Assert.That(Vector3.Distance(groundedPosition,
                    new Vector3(walkable.position.x, 0f, walkable.position.z)), Is.LessThan(0.1f));
                greatestDetour = Mathf.Max(greatestDetour, Mathf.Abs(position.z - destination.z));
                previousPosition = position;
                yield return null;
            }

            Assert.That(Vector3.Distance(member.WorldPosition, destination), Is.LessThanOrEqualTo(0.45f),
                $"The member must follow the valid route and regroup on the far side. " +
                $"Final={member.WorldPosition}, destination={destination}, detour={greatestDetour:0.00}.");
            Assert.That(greatestDetour, Is.GreaterThan(1.8f),
                "The member must visibly detour around the blocker rather than crossing its footprint.");
            Object.Destroy(blocker);
        }

        [UnityTest]
        public IEnumerator BunchedFormationMembers_KeepMovingAtObstacleEdgeAndRegroup()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var formation = economy.DeployFriendlyForAutomation(FormationType.Spearmen,
                new Vector3(0f, 0f, 10f));
            yield return null;

            var edgeMember = formation.Members[0];
            var trailingMembers = new[] { formation.Members[1], formation.Members[2] };
            var blocker = CreateRouteBlocker("Bunched Member Route Blocker", new Vector3(-4.7f, 1f, 10f),
                new Vector3(1.5f, 2f, 4f));
            var blockerBounds = blocker.GetComponent<Collider>().bounds;
            yield return new WaitForSeconds(1f);

            Assert.That(NavMesh.SamplePosition(new Vector3(-5.55f, 0f, 10f), out var sampledEdgeStart,
                1f, NavMesh.AllAreas), Is.True);
            var edgeStart = new Vector3(sampledEdgeStart.position.x, edgeMember.WorldPosition.y,
                sampledEdgeStart.position.z);
            edgeMember.TeleportBy(edgeStart - edgeMember.WorldPosition);
            for (var index = 0; index < trailingMembers.Length; index++)
            {
                var offset = new Vector3(-0.65f, 0f, index == 0 ? -0.08f : 0.08f);
                Assert.That(NavMesh.SamplePosition(sampledEdgeStart.position + offset,
                    out var sampledTrailingStart, 0.2f, NavMesh.AllAreas), Is.True);
                var trailingStart = new Vector3(sampledTrailingStart.position.x,
                    trailingMembers[index].WorldPosition.y, sampledTrailingStart.position.z);
                trailingMembers[index].TeleportBy(trailingStart - trailingMembers[index].WorldPosition);
                Assert.That(Vector3.Distance(edgeMember.WorldPosition, trailingMembers[index].WorldPosition),
                    Is.LessThan(0.85f),
                    "The regression requires bunched members close enough to apply separation steering.");
            }

            var previousEdgePosition = edgeMember.WorldPosition;
            var forcedBunchDeadline = Time.realtimeSinceStartup + 0.75f;
            while (Time.realtimeSinceStartup < forcedBunchDeadline)
            {
                yield return null;
                AssertSweptSegmentOutsideBounds(previousEdgePosition, edgeMember.WorldPosition, blockerBounds);
                previousEdgePosition = edgeMember.WorldPosition;
                for (var index = 0; index < trailingMembers.Length; index++)
                {
                    var offset = new Vector3(-0.65f, 0f, index == 0 ? -0.08f : 0.08f);
                    var desiredTrailing = new Vector3(edgeMember.WorldPosition.x + offset.x, 0f,
                        edgeMember.WorldPosition.z + offset.z);
                    Assert.That(NavMesh.SamplePosition(desiredTrailing, out var sampledTrailing,
                        0.2f, NavMesh.AllAreas), Is.True);
                    var pinnedPosition = new Vector3(sampledTrailing.position.x,
                        trailingMembers[index].WorldPosition.y, sampledTrailing.position.z);
                    trailingMembers[index].TeleportBy(pinnedPosition - trailingMembers[index].WorldPosition);
                }
            }
            Assert.That(Vector3.Distance(edgeMember.WorldPosition, edgeStart), Is.GreaterThan(0.3f),
                "A sustained inward separation force must not suppress progress along the authoritative path.");

            var trackedMembers = new[] { edgeMember, trailingMembers[0], trailingMembers[1] };
            var starts = trackedMembers.Select(member => member.WorldPosition).ToArray();
            var previousPositions = trackedMembers.Select(member => member.WorldPosition).ToArray();
            var greatestProgress = new float[trackedMembers.Length];
            var deadline = Time.realtimeSinceStartup + 6f;
            while (trackedMembers.Any(member =>
                       Vector3.Distance(member.WorldPosition, member.AssignedSlotWorldPosition) > 0.45f) &&
                   Time.realtimeSinceStartup < deadline)
            {
                for (var index = 0; index < trackedMembers.Length; index++)
                {
                    var member = trackedMembers[index];
                    var position = member.WorldPosition;
                    var frameDisplacement = Vector3.Distance(position, previousPositions[index]);
                    var maximumFrameDisplacement = formation.MoveSpeed * 1.3f * Time.deltaTime + 0.025f;
                    Assert.That(frameDisplacement, Is.LessThanOrEqualTo(maximumFrameDisplacement),
                        $"A bunched member must remain speed bounded. " +
                        $"Moved={frameDisplacement:0.000}, maximum={maximumFrameDisplacement:0.000}.");
                    AssertSweptSegmentOutsideBounds(previousPositions[index], position, blockerBounds);
                    var groundedPosition = new Vector3(position.x, 0f, position.z);
                    Assert.That(NavMesh.SamplePosition(groundedPosition, out var walkable, 0.1f,
                        NavMesh.AllAreas), Is.True,
                        "Separation-steered members must remain on the NavMesh at an obstacle edge.");
                    Assert.That(Vector3.Distance(groundedPosition, walkable.position), Is.LessThan(0.1f));
                    greatestProgress[index] = Mathf.Max(greatestProgress[index],
                        Vector3.Distance(position, starts[index]));
                    previousPositions[index] = position;
                }
                yield return null;
            }

            Assert.That(greatestProgress[0], Is.GreaterThan(1f),
                "An obstacle-edge member must not let a zero-progress separation step suppress its path step.");
            Assert.That(greatestProgress[1], Is.GreaterThan(1f));
            Assert.That(greatestProgress[2], Is.GreaterThan(1f),
                "Nearby members must also make forward progress after the bunch releases.");
            Assert.That(trackedMembers.All(member =>
                    Vector3.Distance(member.WorldPosition, member.AssignedSlotWorldPosition) <= 0.45f), Is.True,
                $"All bunched members must eventually regroup. " +
                $"Edge={edgeMember.WorldPosition}/{edgeMember.AssignedSlotWorldPosition}, " +
                $"trailing-a={trailingMembers[0].WorldPosition}/{trailingMembers[0].AssignedSlotWorldPosition}, " +
                $"trailing-b={trailingMembers[1].WorldPosition}/{trailingMembers[1].AssignedSlotWorldPosition}.");
            Object.Destroy(blocker);
        }

        [UnityTest]
        public IEnumerator FormationMember_UsesReachableFallbackWhenItsSlotIsInsideStructureFootprint()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var formation = economy.DeployFriendlyForAutomation(FormationType.Spearmen,
                new Vector3(0f, 0f, 10f));
            yield return null;

            var member = formation.Members[0];
            var blockedSlot = member.AssignedSlotWorldPosition;
            var blocker = CreateRouteBlocker("Blocked Formation Slot",
                new Vector3(blockedSlot.x, 1f, blockedSlot.z), new Vector3(3f, 2f, 3f));
            var blockerBounds = blocker.GetComponent<Collider>().bounds;
            yield return new WaitForSeconds(1f);

            member.TeleportBy(Vector3.left * 5f);
            var displacedPosition = member.WorldPosition;
            var deadline = Time.realtimeSinceStartup + 5f;
            while ((Vector3.Distance(member.WorldPosition, member.NavigationDestination) > 0.45f ||
                    Vector3.Distance(member.WorldPosition, displacedPosition) < 2f) &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.That(Vector3.Distance(member.WorldPosition, displacedPosition), Is.GreaterThan(2f),
                "A member must not freeze when its exact slot is carved out of the NavMesh.");
            Assert.That(Vector3.Distance(member.NavigationDestination, blockedSlot), Is.LessThanOrEqualTo(3.05f),
                "The fallback destination must stay near the obstructed formation slot.");
            Assert.That(member.NavigationDestination.x, Is.LessThan(blockedSlot.x),
                "A displaced member should use a reachable fallback on its side of the blocked slot.");
            Assert.That(Vector3.Distance(member.WorldPosition, member.NavigationDestination),
                Is.LessThanOrEqualTo(0.45f),
                $"The member must consider the projected reachable destination arrived. " +
                $"Position={member.WorldPosition}, navigation={member.NavigationDestination}, slot={blockedSlot}.");
            Assert.That(blockerBounds.Contains(new Vector3(member.WorldPosition.x,
                blockerBounds.center.y, member.WorldPosition.z)), Is.False);
            Assert.That(NavMesh.SamplePosition(new Vector3(member.WorldPosition.x, 0f, member.WorldPosition.z),
                out var walkable, 0.1f, NavMesh.AllAreas), Is.True);
            Assert.That(Vector3.Distance(new Vector3(member.WorldPosition.x, 0f, member.WorldPosition.z),
                walkable.position), Is.LessThan(0.1f));

            Object.Destroy(blocker);
            yield return new WaitForSeconds(1f);
            deadline = Time.realtimeSinceStartup + 5f;
            while (Vector3.Distance(member.WorldPosition, member.AssignedSlotWorldPosition) > 0.45f &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.That(Vector3.Distance(member.WorldPosition, member.AssignedSlotWorldPosition),
                Is.LessThanOrEqualTo(0.45f),
                "The member must reclaim its exact slot after the structure obstruction clears.");
        }

        [UnityTest]
        public IEnumerator FormationMember_RefreshesFallbackAsBlockedSlotDriftsIncrementally()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var formation = economy.DeployFriendlyForAutomation(FormationType.Spearmen,
                new Vector3(0f, 0f, 10f));
            yield return null;

            var member = formation.Members[3];
            var initialSlot = member.AssignedSlotWorldPosition;
            var blocker = CreateRouteBlocker("Drifting Blocked Formation Slot",
                new Vector3(initialSlot.x, 1f, initialSlot.z), new Vector3(2f, 2f, 8f));
            var blockerBounds = new Bounds(blocker.transform.position, blocker.transform.localScale);
            yield return new WaitForSeconds(1f);

            member.TeleportBy(Vector3.left * 5f);
            var settleDeadline = Time.realtimeSinceStartup + 5f;
            while (Vector3.Distance(member.WorldPosition, member.NavigationDestination) > 0.45f &&
                   Time.realtimeSinceStartup < settleDeadline)
                yield return null;
            var initialFallback = member.NavigationDestination;

            for (var step = 0; step < 60; step++)
            {
                formation.transform.position += Vector3.forward * 0.02f;
                yield return null;
            }
            yield return new WaitForSeconds(0.5f);

            var driftingSlot = member.AssignedSlotWorldPosition;
            Assert.That(blockerBounds.Contains(new Vector3(driftingSlot.x,
                blockerBounds.center.y, driftingSlot.z)), Is.True,
                $"The regression must keep the requested slot carved while it moves incrementally. " +
                $"Slot={driftingSlot}, bounds={blockerBounds}.");
            Assert.That(Vector3.Distance(member.NavigationDestination, initialFallback), Is.GreaterThan(0.4f),
                $"A fallback must be resampled after cumulative slot drift instead of remaining stale. " +
                $"Initial={initialFallback}, current={member.NavigationDestination}, slot={driftingSlot}.");

            for (var step = 0; step < 180; step++)
            {
                formation.transform.position += Vector3.forward * 0.02f;
                yield return null;
            }
            var reformDeadline = Time.realtimeSinceStartup + 5f;
            while (Vector3.Distance(member.WorldPosition, member.AssignedSlotWorldPosition) > 0.45f &&
                   Time.realtimeSinceStartup < reformDeadline)
                yield return null;

            Assert.That(blockerBounds.Contains(new Vector3(member.AssignedSlotWorldPosition.x,
                blockerBounds.center.y, member.AssignedSlotWorldPosition.z)), Is.False,
                "The formation anchor must finish clear of the still-carved structure.");
            Assert.That(Vector3.Distance(member.WorldPosition, member.AssignedSlotWorldPosition),
                Is.LessThanOrEqualTo(0.45f),
                "The member must reform once incremental anchor movement carries its slot into open ground.");
            Object.Destroy(blocker);
        }

        [UnityTest]
        public IEnumerator MemberCasualty_DiesAtItsPositionAndSurvivorsCloseRanksSmoothly()
        {
            var tuning = ScriptableObject.CreateInstance<EconomyTuning>();
            var casualties = 0;
            var formation = CreateFormationForTest("Casualty Formation", FormationType.Archers, false, tuning,
                onCasualty: amount => casualties += amount);
            formation.transform.position = Vector3.zero;
            yield return null;
            var casualty = formation.Members[0];
            casualty.TeleportBy(Vector3.right * 3f);
            var survivor = formation.Members[1];
            var survivorPosition = survivor.WorldPosition;

            formation.ApplyDeterministicHit(casualty, FormationType.Cavalry,
                casualty.WorldPosition - Vector3.forward);
            Assert.That(formation.MemberCount, Is.EqualTo(7));
            Assert.That(casualty.IsAlive, Is.False);
            Assert.That(survivor.SlotIndex, Is.EqualTo(0));
            Assert.That(survivor.WorldPosition, Is.EqualTo(survivorPosition),
                "Closing ranks must not teleport a survivor into the casualty's slot.");
            var initialGap = Vector3.Distance(survivor.WorldPosition, survivor.AssignedSlotWorldPosition);
            yield return new WaitForSeconds(0.5f);
            Assert.That(Vector3.Distance(survivor.WorldPosition, survivor.AssignedSlotWorldPosition),
                Is.LessThan(initialGap));
            Assert.That(casualties, Is.EqualTo(1));

            Object.Destroy(formation.gameObject);
            Object.Destroy(tuning);
        }

        [UnityTest]
        public IEnumerator Cavalry_MovesFasterStopsAndWinsItsArcherCounterFight()
        {
            var tuning = ScriptableObject.CreateInstance<EconomyTuning>();
            var cavalry = CreateFormationForTest("Moving Cavalry", FormationType.Cavalry, true, tuning);
            var spearmen = CreateFormationForTest("Moving Spearmen", FormationType.Spearmen, true, tuning);
            var archers = CreateFormationForTest("Target Archers", FormationType.Archers, false, tuning);
            cavalry.transform.position = new Vector3(-2f, 0f, 0f);
            spearmen.transform.position = new Vector3(2f, 0f, 0f);
            archers.transform.position = new Vector3(-2f, 0f, 12f);
            cavalry.IssueMove(new Vector3(-2f, 0f, 20f));
            spearmen.IssueMove(new Vector3(2f, 0f, 20f));

            yield return new WaitForSeconds(0.5f);
            Assert.That(cavalry.transform.position.z, Is.GreaterThan(spearmen.transform.position.z + 0.7f));
            Assert.That(Vector3.Angle(cavalry.transform.forward, Vector3.forward), Is.LessThan(3f));
            cavalry.IssueStop();
            var stoppedPosition = cavalry.transform.position;
            yield return new WaitForSeconds(0.2f);
            Assert.That(cavalry.transform.position, Is.EqualTo(stoppedPosition));
            Assert.That(cavalry.CurrentOrder, Is.EqualTo(FormationOrder.Idle));

            Assert.That(cavalry.IssueFocus(archers), Is.True);
            yield return WaitUntil(() => archers.MemberCount == 0);
            Assert.That(cavalry.MemberCount, Is.GreaterThanOrEqualTo(4));

            Object.Destroy(cavalry.gameObject);
            Object.Destroy(spearmen.gameObject);
            Object.Destroy(archers.gameObject);
            Object.Destroy(tuning);
        }

        [UnityTest]
        public IEnumerator MoveOrder_OpposingFrontlineBlocksDirectPassageAndReleasesLaterally()
        {
            var tuning = ScriptableObject.CreateInstance<EconomyTuning>();
            FormationAgent defender = null;
            var attacker = CreateFormationForTest("Frontline mover", FormationType.Spearmen, true, tuning,
                () => new[] { defender });
            defender = CreateFormationForTest("Frontline blocker", FormationType.Spearmen, false, tuning);
            attacker.transform.position = Vector3.zero;
            defender.transform.position = new Vector3(0f, 0f, 4f);

            attacker.IssueMove(new Vector3(0f, 0f, 8f));
            yield return WaitUntil(() => attacker.IsFrontlineBlocked);
            var blockedPosition = attacker.transform.position;
            yield return new WaitForSeconds(0.25f);

            Assert.That(attacker.transform.position.z, Is.LessThan(defender.transform.position.z));
            Assert.That(Vector3.Distance(attacker.transform.position, blockedPosition), Is.LessThan(0.05f));
            Assert.That(attacker.CurrentOrder, Is.EqualTo(FormationOrder.Move));

            attacker.IssueMove(attacker.transform.position + Vector3.right * 4f);
            yield return new WaitForSeconds(0.35f);

            Assert.That(attacker.IsFrontlineBlocked, Is.False);
            Assert.That(attacker.transform.position.x, Is.GreaterThan(blockedPosition.x + 0.5f));
            Object.Destroy(attacker.gameObject);
            Object.Destroy(defender.gameObject);
            Object.Destroy(tuning);
        }

        [UnityTest]
        public IEnumerator MoveOrder_AlliedFormationDoesNotCreateRigidFrontline()
        {
            var tuning = ScriptableObject.CreateInstance<EconomyTuning>();
            FormationAgent ally = null;
            var mover = CreateFormationForTest("Allied mover", FormationType.Spearmen, true, tuning,
                () => new[] { ally });
            ally = CreateFormationForTest("Allied soft obstacle", FormationType.Archers, true, tuning);
            mover.transform.position = Vector3.zero;
            ally.transform.position = new Vector3(0f, 0f, 2f);

            mover.IssueMove(new Vector3(0f, 0f, 5f));
            yield return new WaitForSeconds(0.8f);

            Assert.That(mover.IsFrontlineBlocked, Is.False);
            Assert.That(mover.transform.position.z, Is.GreaterThan(ally.transform.position.z));
            Object.Destroy(mover.gameObject);
            Object.Destroy(ally.gameObject);
            Object.Destroy(tuning);
        }

        [UnityTest]
        public IEnumerator ControlGroupHotkeys_BareDigitsRecallOnlyAndModifiedDigitsAssign()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var firstWorker = economy.Workers[0];
            var secondWorker = economy.Workers[1];
            var keyboard = InputSystem.AddDevice<Keyboard>();
            keyboard.MakeCurrent();

            economy.SelectOnly(firstWorker);
            PressControlGroupHotkey(economy, keyboard, Key.Digit2);
            Assert.That(economy.ControlGroupSize(2), Is.Zero,
                "A bare digit must not assign the current selection to an empty group.");
            Assert.That(firstWorker.IsSelected, Is.True);

            PressControlGroupHotkey(economy, keyboard, Key.LeftCtrl, Key.Digit1);
            Assert.That(economy.ControlGroupSize(1), Is.EqualTo(1));

            economy.SelectOnly(secondWorker);
            PressControlGroupHotkey(economy, keyboard, Key.LeftCtrl);
            PressControlGroupHotkey(economy, keyboard, Key.Digit1);
            Assert.That(economy.ControlGroupSize(1), Is.EqualTo(1),
                "A bare recall after another modifier action must not overwrite the populated group.");
            Assert.That(firstWorker.IsSelected, Is.True);
            Assert.That(secondWorker.IsSelected, Is.False);

            economy.SelectOnly(secondWorker);
            QueueCoalescedKeyboardChord(keyboard, Key.LeftCtrl, Key.Digit3);
            InvokePrivateMethod(economy, "HandleControlGroupInput");
            Assert.That(economy.ControlGroupSize(3), Is.EqualTo(1),
                "A complete modified chord received within one input update must still assign the group.");

            economy.SelectOnly(firstWorker);
            QueueCoalescedKeyboardChord(keyboard, Key.Digit3);
            InvokePrivateMethod(economy, "HandleControlGroupInput");
            Assert.That(secondWorker.IsSelected, Is.True,
                "A complete bare digit received within one input update must recall the assigned group.");
            Assert.That(firstWorker.IsSelected, Is.False);

            InputSystem.RemoveDevice(keyboard);
        }

        [UnityTest]
        public IEnumerator SelectionInput_DragShiftAndDoubleClickUpdatesVisibleSelection()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var mouse = InputSystem.AddDevice<Mouse>();
            var keyboard = InputSystem.AddDevice<Keyboard>();
            mouse.MakeCurrent();
            keyboard.MakeCurrent();
            InputSystem.QueueStateEvent(mouse, new MouseState
            {
                position = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)
            });
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            var workerScreenPositions = economy.Workers
                .Select(worker => (Vector2)Camera.main.WorldToScreenPoint(worker.transform.position))
                .ToArray();
            var dragStart = new Vector2(workerScreenPositions.Min(position => position.x) - 20f,
                workerScreenPositions.Min(position => position.y) - 20f);
            var dragEnd = new Vector2(workerScreenPositions.Max(position => position.x) + 20f,
                workerScreenPositions.Max(position => position.y) + 20f);
            Assert.That(dragStart.y, Is.GreaterThan(Screen.height * 0.16f));
            Assert.That(dragEnd.y, Is.LessThan(Screen.height * 0.9f));

            yield return DragBattlefieldSelection(economy, mouse, dragStart, dragEnd);
            Assert.That(economy.Workers.All(worker => worker.IsSelected), Is.True,
                "Dragging across the starting workers must show every selection ring.");

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftShift));
            InputSystem.Update();
            var firstWorker = economy.Workers[0];
            var workerCollider = firstWorker.GetComponentInChildren<Collider>();
            var workerClick = (Vector2)Camera.main.WorldToScreenPoint(workerCollider.bounds.center);
            yield return PressMouseButton(economy, mouse, workerClick, MouseButton.Left,
                "HandleSelectionInput");
            Assert.That(firstWorker.IsSelected, Is.False,
                "Shift-clicking a selected worker must visibly remove it from the selection.");
            Assert.That(economy.Workers.Skip(1).All(worker => worker.IsSelected), Is.True);
            yield return PressMouseButton(economy, mouse, workerClick, MouseButton.Left,
                "HandleSelectionInput");
            Assert.That(firstWorker.IsSelected, Is.True,
                "Shift-clicking the worker again must visibly add it back.");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();

            Assert.That(economy.TryPlaceHouse(firstWorker, VisibleHouseSite), Is.True);
            yield return WaitUntil(() => economy.PopulationCapacity == 20);
            economy.CreditSuppliesForAutomation(800);
            Assert.That(economy.TryQueueFormation(FormationType.Archers), Is.True);
            Assert.That(economy.TryQueueFormation(FormationType.Archers), Is.True);
            yield return WaitUntil(() => economy.FriendlyFormations.Count == 2);
            var firstFormation = economy.FriendlyFormations[0];
            var secondFormation = economy.FriendlyFormations[1];
            Assert.That(firstFormation.GetComponent<NavMeshAgent>().Warp(new Vector3(-4f, 0f, 1f)), Is.True);
            Assert.That(secondFormation.GetComponent<NavMeshAgent>().Warp(new Vector3(4f, 0f, 1f)), Is.True);
            Physics.SyncTransforms();
            var formationCollider = firstFormation.GetComponentInChildren<Collider>();
            var formationClick = (Vector2)Camera.main.WorldToScreenPoint(formationCollider.bounds.center);

            yield return PressMouseButton(economy, mouse, formationClick, MouseButton.Left,
                "HandleSelectionInput");
            Assert.That(firstFormation.IsSelected, Is.True);
            Assert.That(secondFormation.IsSelected, Is.False);
            yield return PressMouseButton(economy, mouse, formationClick, MouseButton.Left,
                "HandleSelectionInput");

            Assert.That(economy.SelectedFormations, Is.EquivalentTo(new[] { firstFormation, secondFormation }));
            Assert.That(firstFormation.IsSelected && secondFormation.IsSelected, Is.True,
                "Double-clicking a formation must show selection rings on every visible formation of its type.");
            Assert.That(GameObject.Find("Selection").GetComponent<Text>().text, Does.Contain("2 FORMATIONS"));
            InputSystem.RemoveDevice(mouse);
            InputSystem.RemoveDevice(keyboard);
        }

        [UnityTest]
        public IEnumerator SelectionAndOrderInput_CoalescedClicksAndHudOriginDragRemainLossless()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var mouse = InputSystem.AddDevice<Mouse>();
            mouse.MakeCurrent();
            InputSystem.QueueStateEvent(mouse, new MouseState());
            InputSystem.Update();

            var worker = economy.Workers[0];
            economy.SelectOnly(worker);
            var destination = new Vector3(6f, 0f, 1f);
            var destinationClick = (Vector2)Camera.main.WorldToScreenPoint(destination);
            QueueCoalescedClick(mouse, destinationClick, MouseButton.Left);
            InvokePrivateMethod(economy, "HandleSelectionInput");
            Assert.That(worker.IsSelected, Is.False,
                "A complete click received within one input update must still clear the previous selection.");

            economy.SelectOnly(worker);
            QueueCoalescedClick(mouse, destinationClick, MouseButton.Right);
            InvokePrivateMethod(economy, "HandleOrderInput");
            Assert.That(worker.CurrentActivity, Is.EqualTo(WorkerAgent.Activity.Moving),
                "A complete right click received within one input update must still issue its order.");

            var hudOrigin = (Vector2)GameObject.Find("Build House").GetComponent<RectTransform>().position;
            var battlefieldEnd = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = hudOrigin }.WithButton(MouseButton.Left));
            InputSystem.Update();
            InvokePrivateMethod(economy, "HandleSelectionInput");
            InputSystem.QueueStateEvent(mouse, new MouseState { position = battlefieldEnd }.WithButton(MouseButton.Left));
            InputSystem.Update();
            InvokePrivateMethod(economy, "HandleSelectionInput");
            InputSystem.QueueStateEvent(mouse, new MouseState { position = battlefieldEnd });
            InputSystem.Update();
            InvokePrivateMethod(economy, "HandleSelectionInput");

            Assert.That(worker.IsSelected, Is.True,
                "A press that starts on the HUD must remain consumed after the pointer enters the battlefield.");
            InputSystem.RemoveDevice(mouse);
        }

        [UnityTest]
        public IEnumerator AttackMoveMode_CoalescedActivationClicksAreConsumed()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            Assert.That(economy.TryPlaceHouse(economy.Workers[0], VisibleHouseSite), Is.True);
            yield return WaitUntil(() => economy.PopulationCapacity == 20);
            economy.CreditSuppliesForAutomation(400);
            Assert.That(economy.TryQueueFormation(FormationType.Cavalry), Is.True);
            yield return WaitUntil(() => economy.FriendlyFormations.Count == 1);

            var formation = economy.FriendlyFormations[0];
            economy.SelectOnly(formation);
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var mouse = InputSystem.AddDevice<Mouse>();
            keyboard.MakeCurrent();
            mouse.MakeCurrent();
            var groundClick = (Vector2)Camera.main.WorldToScreenPoint(new Vector3(4f, 0f, 6f));

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = groundClick }.WithButton(MouseButton.Left));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = groundClick });
            InputSystem.Update();
            Assert.That(Keyboard.current, Is.SameAs(keyboard));
            Assert.That(keyboard.fKey.isPressed, Is.True);
            InvokePrivateMethod(economy, "Update");
            Assert.That(formation.CurrentOrder, Is.EqualTo(FormationOrder.AttackMove),
                GameObject.Find("Order").GetComponent<Text>().text);
            Assert.That(formation.IsSelected, Is.True,
                "The click that resolves attack-move must not leak into normal selection.");

            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.QueueStateEvent(mouse, new MouseState { position = groundClick });
            InputSystem.Update();
            InvokePrivateMethod(economy, "Update");
            formation.IssueStop();
            economy.SelectOnly(formation);

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = groundClick }.WithButton(MouseButton.Right));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = groundClick });
            InputSystem.Update();
            InvokePrivateMethod(economy, "Update");
            Assert.That(formation.CurrentOrder, Is.EqualTo(FormationOrder.Idle),
                "The click that cancels attack-move must not leak into a normal contextual order.");
            Assert.That(formation.IsSelected, Is.True);

            InputSystem.RemoveDevice(mouse);
            InputSystem.RemoveDevice(keyboard);
        }

        [UnityTest]
        public IEnumerator QueuedInput_ExecutesInterleavedCommandsInEventOrder()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            Assert.That(economy.TryPlaceHouse(economy.Workers[0], VisibleHouseSite), Is.True);
            yield return WaitUntil(() => economy.PopulationCapacity == 20);
            economy.CreditSuppliesForAutomation(400);
            Assert.That(economy.TryQueueFormation(FormationType.Cavalry), Is.True);
            yield return WaitUntil(() => economy.FriendlyFormations.Count == 1);

            var formation = economy.FriendlyFormations[0];
            economy.SelectOnly(formation);
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var mouse = InputSystem.AddDevice<Mouse>();
            keyboard.MakeCurrent();
            mouse.MakeCurrent();
            var destinationClick = (Vector2)Camera.main.WorldToScreenPoint(new Vector3(6f, 0f, 6f));

            InputSystem.QueueStateEvent(mouse, new MouseState { position = destinationClick }
                .WithButton(MouseButton.Right));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = destinationClick });
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.G));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            InvokePrivateMethod(economy, "Update");
            Assert.That(formation.CurrentOrder, Is.EqualTo(FormationOrder.Idle),
                "A Stop received after a move must remain the final command.");

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.G));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.QueueStateEvent(mouse, new MouseState { position = destinationClick }
                .WithButton(MouseButton.Right));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = destinationClick });
            InputSystem.Update();
            InvokePrivateMethod(economy, "Update");
            Assert.That(formation.CurrentOrder, Is.EqualTo(FormationOrder.Move),
                "A move received after Stop must remain the final command.");

            formation.IssueStop();
            economy.SelectOnly(formation);
            var worker = economy.Workers[1];
            var workerCollider = worker.GetComponentInChildren<Collider>();
            var workerClick = (Vector2)Camera.main.WorldToScreenPoint(workerCollider.bounds.center);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = destinationClick }
                .WithButton(MouseButton.Right));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = destinationClick });
            InputSystem.QueueStateEvent(mouse, new MouseState { position = workerClick }
                .WithButton(MouseButton.Left));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = workerClick });
            InputSystem.Update();
            InvokePrivateMethod(economy, "Update");

            Assert.That(formation.CurrentOrder, Is.EqualTo(FormationOrder.Move),
                "The earlier order must target the selection that existed when it was received.");
            Assert.That(worker.IsSelected, Is.True);
            Assert.That(formation.IsSelected, Is.False);

            InputSystem.RemoveDevice(mouse);
            InputSystem.RemoveDevice(keyboard);
        }

        [UnityTest]
        public IEnumerator PlacementMode_CoalescedHotkeyAndClickPlacesAtThePressPosition()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            economy.SelectOnly(economy.Workers[0]);
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var mouse = InputSystem.AddDevice<Mouse>();
            keyboard.MakeCurrent();
            mouse.MakeCurrent();
            var placementClick = (Vector2)Camera.main.WorldToScreenPoint(VisibleHouseSite);

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.H));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.QueueStateEvent(mouse, new MouseState { position = placementClick }
                .WithButton(MouseButton.Left));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = placementClick });
            InputSystem.Update();
            InvokePrivateMethod(economy, "Update");

            Assert.That(economy.Houses, Has.Count.EqualTo(1));
            Assert.That(economy.IsBuildingPlacementActive, Is.False);
            Assert.That(economy.Supplies, Is.Zero);

            InputSystem.RemoveDevice(mouse);
            InputSystem.RemoveDevice(keyboard);
        }

        [UnityTest]
        public IEnumerator FormationGroups_PreserveLayoutRecallAndApplyCommandsTogether()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            Assert.That(economy.TryPlaceHouse(economy.Workers[0], VisibleHouseSite), Is.True);
            yield return WaitUntil(() => economy.PopulationCapacity == 20);
            economy.CreditSuppliesForAutomation(800);
            Assert.That(economy.TryQueueFormation(FormationType.Archers), Is.True);
            Assert.That(economy.TryQueueFormation(FormationType.Cavalry), Is.True);
            yield return WaitUntil(() => economy.FriendlyFormations.Count == 2);
            var archers = economy.FriendlyFormations.Single(formation => formation.Type == FormationType.Archers);
            var cavalry = economy.FriendlyFormations.Single(formation => formation.Type == FormationType.Cavalry);
            Assert.That(archers.GetComponent<UnityEngine.AI.NavMeshAgent>().isOnNavMesh, Is.True);
            Assert.That(cavalry.GetComponent<UnityEngine.AI.NavMeshAgent>().isOnNavMesh, Is.True);
            archers.GetComponent<UnityEngine.AI.NavMeshAgent>().Warp(new Vector3(-4f, 0f, 0f));
            cavalry.GetComponent<UnityEngine.AI.NavMeshAgent>().Warp(new Vector3(4f, 0f, 0f));
            economy.SelectFormationsForAutomation(new[] { archers, cavalry });
            economy.AssignControlGroup(1);
            Assert.That(economy.ControlGroupSize(1), Is.EqualTo(2));
            economy.SelectHisar();
            Assert.That(economy.SelectedFormations, Is.Empty);

            Assert.That(economy.RecallControlGroup(1), Is.True);
            Assert.That(economy.SelectedFormations, Is.EquivalentTo(new[] { archers, cavalry }));

            economy.IssueMoveForSelected(new Vector3(0f, 0f, 10f));
            Assert.That(archers.CurrentOrder, Is.EqualTo(FormationOrder.Move));
            Assert.That(cavalry.CurrentOrder, Is.EqualTo(FormationOrder.Move));
            Assert.That(cavalry.Destination.x - archers.Destination.x, Is.EqualTo(8f).Within(0.01f));
            yield return new WaitForSeconds(0.25f);
            economy.StopSelectedFormations();
            var archersStoppedAt = archers.transform.position;
            var cavalryStoppedAt = cavalry.transform.position;
            yield return new WaitForSeconds(0.25f);
            Assert.That(archers.CurrentOrder, Is.EqualTo(FormationOrder.Idle));
            Assert.That(cavalry.CurrentOrder, Is.EqualTo(FormationOrder.Idle));
            Assert.That(Vector3.Distance(archersStoppedAt, archers.transform.position), Is.LessThan(0.15f));
            Assert.That(Vector3.Distance(cavalryStoppedAt, cavalry.transform.position), Is.LessThan(0.15f));
            Assert.That(GameObject.Find("Attack Move").GetComponentInChildren<UnityEngine.UI.Text>().text,
                Does.Contain("[F]"));
            Assert.That(GameObject.Find("Stop Formations").GetComponentInChildren<UnityEngine.UI.Text>().text,
                Does.Contain("[G]"));
        }

        [UnityTest]
        public IEnumerator MixedSelection_GroundMoveCommandMovesWorkersAndFormations()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            Assert.That(economy.TryPlaceHouse(economy.Workers[0], VisibleHouseSite), Is.True);
            yield return WaitUntil(() => economy.PopulationCapacity == 20);
            economy.CreditSuppliesForAutomation(400);
            Assert.That(economy.TryQueueFormation(FormationType.Archers), Is.True);
            yield return WaitUntil(() => economy.FriendlyFormations.Count == 1);
            var worker = economy.Workers[1];
            var formation = economy.FriendlyFormations[0];
            economy.SelectOnly(worker);
            InvokePrivateMethod(economy, "AddSelectedFormation", formation);
            var destination = new Vector3(-15f, 0f, 10f);
            economy.IssueMoveForSelected(destination);

            Assert.That(worker.CurrentActivity, Is.EqualTo(WorkerAgent.Activity.Moving));
            Assert.That(formation.CurrentOrder, Is.EqualTo(FormationOrder.Move));
        }

        [UnityTest]
        public IEnumerator MixedSelection_CommandLayersRemainAvailableAndTargetingModesStayExclusive()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            Assert.That(economy.TryPlaceHouse(economy.Workers[0], VisibleHouseSite), Is.True);
            yield return WaitUntil(() => economy.PopulationCapacity == 20);
            economy.CreditSuppliesForAutomation(500);
            Assert.That(economy.TryQueueFormation(FormationType.Cavalry), Is.True);
            yield return WaitUntil(() => economy.FriendlyFormations.Count == 1);

            var worker = economy.Workers[1];
            var formation = economy.FriendlyFormations[0];
            economy.SelectOnly(worker);
            InvokePrivateMethod(economy, "AddSelectedFormation", formation);
            InvokePrivateMethod(economy, "UpdateHud");

            var houseButton = GameObject.Find("Build House").GetComponent<Button>();
            var storehouseRect = GameObject.Find("Build Storehouse").GetComponent<RectTransform>();
            var watchtowerRect = GameObject.Find("Build Watchtower").GetComponent<RectTransform>();
            var attackButton = GameObject.Find("Attack Move").GetComponent<Button>();
            var attackRect = attackButton.GetComponent<RectTransform>();
            var stopRect = GameObject.Find("Stop Formations").GetComponent<RectTransform>();
            Assert.That(houseButton.gameObject.activeInHierarchy, Is.True);
            Assert.That(attackButton.gameObject.activeInHierarchy, Is.True);
            Assert.That(storehouseRect.anchorMin.y, Is.GreaterThan(attackRect.anchorMax.y));
            Assert.That(watchtowerRect.anchorMin.y, Is.GreaterThan(stopRect.anchorMax.y),
                "Worker and formation commands must occupy distinct rows for a mixed selection.");

            attackButton.onClick.Invoke();
            Assert.That(economy.IsAttackMoveTargetingActive, Is.True);
            houseButton.onClick.Invoke();
            Assert.That(economy.IsBuildingPlacementActive, Is.False,
                "Building placement must not start while attack-move is targeting.");
            Assert.That(economy.IsAttackMoveTargetingActive, Is.True);

            var keyboard = InputSystem.AddDevice<Keyboard>();
            var mouse = InputSystem.AddDevice<Mouse>();
            keyboard.MakeCurrent();
            mouse.MakeCurrent();
            var groundClick = (Vector2)Camera.main.WorldToScreenPoint(new Vector3(4f, 0f, 6f));
            QueueCoalescedClick(mouse, groundClick, MouseButton.Right);
            InvokePrivateMethod(economy, "Update");
            Assert.That(economy.IsAttackMoveTargetingActive, Is.False);

            houseButton.onClick.Invoke();
            Assert.That(economy.IsBuildingPlacementActive, Is.True);
            attackButton.onClick.Invoke();
            Assert.That(economy.IsAttackMoveTargetingActive, Is.False,
                "Attack-move must not start while building placement is targeting.");
            Assert.That(economy.IsBuildingPlacementActive, Is.True);

            QueueCoalescedKeyboardChord(keyboard, Key.Escape);
            InvokePrivateMethod(economy, "Update");
            Assert.That(economy.IsBuildingPlacementActive, Is.False);

            QueueCoalescedKeyboardChord(keyboard, Key.H);
            InvokePrivateMethod(economy, "Update");
            Assert.That(economy.IsBuildingPlacementActive, Is.True,
                "Mixed selections must retain worker building hotkeys.");
            Assert.That(economy.IsAttackMoveTargetingActive, Is.False);

            InputSystem.RemoveDevice(mouse);
            InputSystem.RemoveDevice(keyboard);
        }

        [UnityTest]
        public IEnumerator MixedSelection_BuildingUsesAnIdleWorkerRegardlessOfSelectionOrder()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var busyWorker = economy.Workers[0];
            var idleWorker = economy.Workers[1];
            economy.CreditSuppliesForAutomation(400);
            Assert.That(economy.TryQueueFormation(FormationType.Cavalry), Is.True);
            yield return WaitUntil(() => economy.FriendlyFormations.Count == 1);
            Assert.That(economy.TryPlaceHouse(busyWorker, VisibleHouseSite), Is.True);
            economy.CreditSuppliesForAutomation(100);
            Assert.That(busyWorker.CurrentConstruction, Is.Not.Null);
            Assert.That(idleWorker.CurrentConstruction, Is.Null);

            economy.SelectOnly(busyWorker);
            InvokePrivateMethod(economy, "AddSelection", idleWorker);
            InvokePrivateMethod(economy, "AddSelectedFormation", economy.FriendlyFormations[0]);
            InvokePrivateMethod(economy, "UpdateHud");
            var houseButton = GameObject.Find("Build House").GetComponent<Button>();
            Assert.That(houseButton.interactable, Is.True,
                "An idle selected worker must keep building actions available even when selected after a busy worker.");

            houseButton.onClick.Invoke();
            Assert.That(economy.IsBuildingPlacementActive, Is.True);
            Assert.That(GetPrivateField<WorkerAgent>(economy, "placementWorker"), Is.SameAs(idleWorker));
        }

        [UnityTest]
        public IEnumerator MinimapCentering_TracksTheRequestedGroundPointAtEveryZoomLevel()
        {
            yield return LoadEconomy();
            var controller = Object.FindAnyObjectByType<RtsCameraController>();
            var camera = controller.GetComponent<Camera>();
            var ground = new Plane(Vector3.up, Vector3.zero);
            var target = new Vector3(3f, 0f, 8f);

            foreach (var height in new[] { 10f, 22f })
            {
                var position = controller.transform.position;
                position.y = height;
                controller.transform.position = position;
                controller.CenterOn(target);
                var centerRay = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
                Assert.That(ground.Raycast(centerRay, out var distance), Is.True);
                var focusedPoint = centerRay.GetPoint(distance);
                Assert.That(focusedPoint.x, Is.EqualTo(target.x).Within(0.05f));
                Assert.That(focusedPoint.z, Is.EqualTo(target.z).Within(0.05f),
                    $"Minimap focus drifted at camera height {height}.");
            }
        }

        [UnityTest]
        public IEnumerator FogAndMinimap_HideMobilesRememberStaticsAndNavigateExploredGround()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var fog = economy.FogOfWar;
            Assert.That(fog, Is.Not.Null);
            Assert.That(GameObject.Find("Battlefield Fog"), Is.Not.Null);
            Assert.That(GameObject.Find("Fog Minimap"), Is.Not.Null);
            Assert.That(fog.StateAt(new Vector3(0f, 0f, 25f)), Is.EqualTo(FogState.Unexplored));

            var scout = new GameObject("Fog test scout");
            scout.transform.position = new Vector3(0f, 0f, 8f);
            fog.RegisterFriendly(scout.transform);
            var mobile = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            mobile.name = "Fog test mobile";
            mobile.transform.position = new Vector3(2f, 0f, 8f);
            fog.RegisterHostileMobile(mobile);
            var remembered = GameObject.CreatePrimitive(PrimitiveType.Cube);
            remembered.name = "Fog test remembered building";
            remembered.transform.position = new Vector3(-2f, 0f, 8f);
            fog.RegisterHostileStatic(remembered);
            fog.RefreshNow();

            Assert.That(fog.StateAt(mobile.transform.position), Is.EqualTo(FogState.Visible));
            Assert.That(mobile.GetComponent<Renderer>().enabled, Is.True);
            Assert.That(mobile.GetComponent<Collider>().enabled, Is.True);
            var visibleEnemyMarker = fog.MinimapColorAt(mobile.transform.position);
            Assert.That(visibleEnemyMarker.r, Is.GreaterThan(visibleEnemyMarker.b));
            var rememberedColor = remembered.GetComponent<Renderer>().material.color;

            scout.transform.position = new Vector3(-20f, 0f, -10f);
            fog.RefreshNow();

            Assert.That(fog.StateAt(mobile.transform.position), Is.EqualTo(FogState.Explored));
            Assert.That(mobile.GetComponent<Renderer>().enabled, Is.False);
            Assert.That(mobile.GetComponent<Collider>().enabled, Is.False);
            Assert.That(remembered.GetComponent<Renderer>().enabled, Is.True);
            Assert.That(remembered.GetComponent<Collider>().enabled, Is.False);
            Assert.That(remembered.GetComponent<Renderer>().material.color.maxColorComponent,
                Is.LessThan(rememberedColor.maxColorComponent));
            var hiddenEnemyMarker = fog.MinimapColorAt(mobile.transform.position);
            Assert.That(hiddenEnemyMarker.r, Is.LessThan(visibleEnemyMarker.r));

            yield return null;
            var selectedWorker = economy.Workers[0];
            economy.SelectOnly(selectedWorker);
            economy.AssignControlGroup(1);
            economy.SelectHisar();
            Assert.That(economy.RecallControlGroup(1), Is.True);

            var clickHandler = GameObject.Find("Fog Minimap").GetComponent<MinimapClickHandler>();
            var minimapRect = clickHandler.GetComponent<RectTransform>();
            var pointer = new PointerEventData(EventSystem.current) { position = minimapRect.position };
            var mouse = InputSystem.AddDevice<Mouse>();
            mouse.MakeCurrent();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer.position }
                .WithButton(MouseButton.Left));
            InputSystem.Update();
            InvokePrivateMethod(economy, "HandleSelectionInput");
            ExecuteEvents.Execute(clickHandler.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer.position });
            InputSystem.Update();
            InvokePrivateMethod(economy, "HandleSelectionInput");
            InputSystem.RemoveDevice(mouse);

            var cameraController = Object.FindAnyObjectByType<RtsCameraController>();
            Assert.That(cameraController.LastRequestedCenter.x, Is.EqualTo(0f).Within(1f));
            Assert.That(cameraController.LastRequestedCenter.z, Is.EqualTo(8f).Within(1f));
            Assert.That(selectedWorker.IsSelected, Is.True,
                "Minimap navigation must not leak into battlefield selection and clear the recalled group.");
            Assert.That(economy.ControlGroupSize(1), Is.EqualTo(1));

            Object.Destroy(scout);
            Object.Destroy(mobile);
            Object.Destroy(remembered);
        }

        [UnityTest]
        public IEnumerator FogStaticMemory_RequiresTheTargetToHaveBeenSeen()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var fog = economy.FogOfWar;
            var targetPosition = new Vector3(0f, 0f, 8f);
            var scout = new GameObject("Static memory scout");
            scout.transform.position = targetPosition;
            fog.RegisterFriendly(scout.transform);
            fog.RefreshNow();
            scout.transform.position = new Vector3(-20f, 0f, -10f);
            fog.RefreshNow();
            Assert.That(fog.StateAt(targetPosition), Is.EqualTo(FogState.Explored));

            var hiddenBuilding = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hiddenBuilding.name = "Never seen hostile building";
            hiddenBuilding.transform.position = targetPosition;
            fog.RegisterHostileStatic(hiddenBuilding);

            Assert.That(hiddenBuilding.GetComponent<Renderer>().enabled, Is.False,
                "Explored ground must not reveal a hostile building that was never observed.");
            Assert.That(hiddenBuilding.GetComponent<Collider>().enabled, Is.False);

            scout.transform.position = targetPosition;
            fog.RefreshNow();
            Assert.That(hiddenBuilding.GetComponent<Renderer>().enabled, Is.True);
            Assert.That(hiddenBuilding.GetComponent<Collider>().enabled, Is.True);
            var visibleColor = hiddenBuilding.GetComponent<Renderer>().material.color;

            scout.transform.position = new Vector3(-20f, 0f, -10f);
            fog.RefreshNow();
            Assert.That(hiddenBuilding.GetComponent<Renderer>().enabled, Is.True,
                "A previously seen static target should remain as a stale silhouette.");
            Assert.That(hiddenBuilding.GetComponent<Collider>().enabled, Is.False);
            Assert.That(hiddenBuilding.GetComponent<Renderer>().material.color.maxColorComponent,
                Is.LessThan(visibleColor.maxColorComponent));

            Object.Destroy(scout);
            Object.Destroy(hiddenBuilding);
        }

        [UnityTest]
        public IEnumerator HostileBuildingCompletion_RefreshesItsVisibleFogPalette()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var building = economy.EnemyBuildings.Single(candidate => !candidate.IsComplete);
            var scout = new GameObject("Hostile construction palette scout");
            scout.transform.position = building.transform.position;
            economy.FogOfWar.RegisterFriendly(scout.transform);
            economy.FogOfWar.RefreshNow();
            var originalTimeScale = Time.timeScale;

            try
            {
                Assert.That(economy.FogOfWar.IsCurrentlyVisible(building), Is.True);
                Time.timeScale = 20f;
                yield return WaitUntil(() => building.IsComplete);
                Time.timeScale = 0f;
                economy.FogOfWar.RefreshNow();

                var expected = new Color(0.72f, 0.16f, 0.07f);
                var visibleRenderers = building.GetComponentsInChildren<Renderer>(true)
                    .Where(itemRenderer => itemRenderer.GetComponent<BuildingSelectionRing>() == null)
                    .ToArray();
                Assert.That(visibleRenderers, Is.Not.Empty);
                foreach (var itemRenderer in visibleRenderers)
                {
                    var actual = itemRenderer.material.color;
                    Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.02f));
                    Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.02f));
                    Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.02f),
                        "A visible completed Alazhan building must retain its red faction palette.");
                }
            }
            finally
            {
                Time.timeScale = originalTimeScale;
                Object.Destroy(scout);
            }
        }

        [UnityTest]
        public IEnumerator HostileBuildingCompletion_RemainsStaleWhileExploredUntilRevealedAgain()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var building = economy.EnemyBuildings.Single(candidate => !candidate.IsComplete);
            var scout = new GameObject("Stale construction palette scout");
            scout.transform.position = building.transform.position;
            economy.FogOfWar.RegisterFriendly(scout.transform);
            economy.FogOfWar.RefreshNow();
            var renderers = building.GetComponentsInChildren<Renderer>(true)
                .Where(itemRenderer => itemRenderer.GetComponent<BuildingSelectionRing>() == null)
                .ToArray();
            var originalTimeScale = Time.timeScale;

            try
            {
                Assert.That(economy.FogOfWar.IsCurrentlyVisible(building), Is.True);
                scout.transform.position = new Vector3(-20f, 0f, -10f);
                economy.FogOfWar.RefreshNow();
                Assert.That(economy.FogOfWar.StateAt(building.transform.position), Is.EqualTo(FogState.Explored));
                var rememberedFoundationColors = renderers.Select(itemRenderer => itemRenderer.material.color).ToArray();

                Time.timeScale = 20f;
                yield return WaitUntil(() => building.IsComplete);
                Time.timeScale = 0f;
                economy.FogOfWar.RefreshNow();

                for (var index = 0; index < renderers.Length; index++)
                    Assert.That(Vector4.Distance(renderers[index].material.color,
                            rememberedFoundationColors[index]), Is.LessThan(0.01f),
                        "Explored fog must retain the last-seen foundation palette after unseen completion.");

                scout.transform.position = building.transform.position;
                economy.FogOfWar.RefreshNow();
                var expected = new Color(0.72f, 0.16f, 0.07f);
                foreach (var itemRenderer in renderers)
                {
                    var actual = itemRenderer.material.color;
                    Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.02f));
                    Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.02f));
                    Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.02f),
                        "Revealing the completed building must update its remembered red palette.");
                }
            }
            finally
            {
                Time.timeScale = originalTimeScale;
                Object.Destroy(scout);
            }
        }

        [UnityTest]
        public IEnumerator ManualWorkerAndStructureFocus_SurviveIncomingFormationFire()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var tuning = GetPrivateField<EconomyTuning>(economy, "tuning");
            var workerFocused = CreateFormationForTest("Worker-focused formation",
                FormationType.Archers, true, tuning);
            var workerAttackers = CreateFormationForTest("Incoming worker-focus attackers",
                FormationType.Spearmen, false, tuning);
            var structureFocused = CreateFormationForTest("Structure-focused formation",
                FormationType.Archers, true, tuning);
            var structureAttackers = CreateFormationForTest("Incoming structure-focus attackers",
                FormationType.Spearmen, false, tuning);
            workerFocused.transform.position = new Vector3(100f, 0f, 100f);
            workerAttackers.transform.position = new Vector3(98f, 0f, 100f);
            structureFocused.transform.position = new Vector3(100f, 0f, 120f);
            structureAttackers.transform.position = new Vector3(98f, 0f, 120f);
            var structureObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            structureObject.name = "Manual focus structure";
            structureObject.transform.position = new Vector3(100f, 0f, 125f);
            var structure = structureObject.AddComponent<ConstructibleBuilding>();
            structure.Initialize(BuildingType.Storehouse, 0.1f, 100, Color.red, _ => { }, false);
            structure.Advance(0.1f);

            try
            {
                var worker = economy.EnemyWorkers.First(candidate => candidate.IsAlive);
                Assert.That(workerFocused.IssueFocus(worker), Is.True);
                Assert.That(workerAttackers.IssueFocus(workerFocused), Is.True);
                var workerFocusedHealth = workerFocused.TotalMemberHealth;
                Assert.That(workerAttackers.ExecuteAttackVolley(workerFocused), Is.True);
                yield return null;

                Assert.That(workerFocused.TotalMemberHealth, Is.LessThan(workerFocusedHealth));
                Assert.That(workerFocused.WorkerTarget, Is.SameAs(worker));
                Assert.That(workerFocused.Target, Is.Null,
                    "Incoming formation fire must not replace a manual worker focus order.");
                Assert.That(workerFocused.CurrentOrder, Is.EqualTo(FormationOrder.Focus));

                Assert.That(structureFocused.IssueFocus(structure), Is.True);
                Assert.That(structureAttackers.IssueFocus(structureFocused), Is.True);
                var structureFocusedHealth = structureFocused.TotalMemberHealth;
                Assert.That(structureAttackers.ExecuteAttackVolley(structureFocused), Is.True);
                yield return null;

                Assert.That(structureFocused.TotalMemberHealth, Is.LessThan(structureFocusedHealth));
                Assert.That(ReferenceEquals(structureFocused.StructureTarget, structure), Is.True);
                Assert.That(structureFocused.Target, Is.Null,
                    "Incoming formation fire must not replace a manual structure focus order.");
                Assert.That(structureFocused.CurrentOrder, Is.EqualTo(FormationOrder.Focus));
            }
            finally
            {
                Object.Destroy(workerFocused.gameObject);
                Object.Destroy(workerAttackers.gameObject);
                Object.Destroy(structureFocused.gameObject);
                Object.Destroy(structureAttackers.gameObject);
                Object.Destroy(structureObject);
            }
        }

        [UnityTest]
        public IEnumerator HostileFocus_RequiresSideVisionAndDropsAfterTheTargetRetreats()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            Assert.That(economy.TryPlaceHouse(economy.Workers[0], VisibleHouseSite), Is.True);
            yield return WaitUntil(() => economy.PopulationCapacity == 20);
            economy.CreditSuppliesForAutomation(800);
            Assert.That(economy.TryQueueFormation(FormationType.Cavalry), Is.True);
            Assert.That(economy.TryQueueFormation(FormationType.Spearmen), Is.True);
            yield return WaitUntil(() => economy.FriendlyFormations.Count == 2);
            economy.DeployEnemyForAutomation(FormationType.Archers, new Vector3(0f, 0f, 17f));

            var scout = economy.FriendlyFormations.Single(formation => formation.Type == FormationType.Cavalry);
            var remoteAttacker = economy.FriendlyFormations.Single(formation => formation.Type == FormationType.Spearmen);
            var hostile = economy.EnemyFormations[0];
            Assert.That(scout.GetComponent<NavMeshAgent>().Warp(new Vector3(0f, 0f, 8f)), Is.True);
            Assert.That(remoteAttacker.GetComponent<NavMeshAgent>().Warp(new Vector3(-5f, 0f, -5f)), Is.True);
            Assert.That(hostile.GetComponent<NavMeshAgent>().Warp(new Vector3(0f, 0f, 17f)), Is.True);
            economy.FogOfWar.RefreshNow();
            Assert.That(economy.FogOfWar.IsCurrentlyVisible(hostile), Is.True,
                "The nearby scout should reveal the hostile to the player side.");

            Assert.That(remoteAttacker.IssueFocus(hostile), Is.True);
            Assert.That(hostile.Target, Is.Null,
                "A scout revealing a hostile must not reveal a remote attacker to the hostile side.");

            var remoteHealth = remoteAttacker.TotalMemberHealth;
            yield return WaitUntil(() => hostile.Target == remoteAttacker);
            yield return WaitUntil(() => remoteAttacker.TotalMemberHealth < remoteHealth);
            Assert.That(hostile.LastAttackMemberCount, Is.GreaterThan(0),
                "The hostile must retry retaliation after the focused attacker enters its vision and return fire.");
            remoteAttacker.IssueStop();
            hostile.IssueStop();
            Assert.That(remoteAttacker.GetComponent<NavMeshAgent>().Warp(new Vector3(-5f, 0f, -5f)), Is.True);
            Assert.That(hostile.GetComponent<NavMeshAgent>().Warp(new Vector3(0f, 0f, 17f)), Is.True);
            economy.FogOfWar.RefreshNow();

            Assert.That(scout.IssueFocus(hostile), Is.True);
            Assert.That(hostile.Target, Is.SameAs(scout));
            scout.IssueMove(new Vector3(-5f, 0f, -5f));
            Assert.That(scout.GetComponent<NavMeshAgent>().Warp(new Vector3(-5f, 0f, -5f)), Is.True);
            yield return WaitUntil(() => hostile.Target == null);

            var stoppedPosition = hostile.transform.position;
            yield return new WaitForSeconds(0.2f);
            Assert.That(hostile.CurrentOrder, Is.EqualTo(FormationOrder.Idle));
            Assert.That(Vector3.Distance(stoppedPosition, hostile.transform.position), Is.LessThan(0.15f),
                "The hostile must stop instead of following a moving target outside current sight.");
        }

        [UnityTest]
        public IEnumerator AttackMove_ResumesItsRouteAfterTransientRetaliationTargetIsRemoved()
        {
            var tuning = ScriptableObject.CreateInstance<EconomyTuning>();
            var movingFormation = CreateFormationForTest("Attack-move retaliation defender",
                FormationType.Spearmen, true, tuning);
            var hostile = CreateFormationForTest("Transient retaliation attacker",
                FormationType.Archers, false, tuning);
            movingFormation.transform.position = Vector3.zero;
            hostile.transform.position = Vector3.left * 8f;
            var destination = Vector3.forward * 12f;

            try
            {
                movingFormation.IssueAttackMove(destination);
                yield return new WaitForSeconds(0.1f);
                Assert.That(movingFormation.transform.position.z, Is.GreaterThan(0f),
                    "The formation must be following its attack-move route before retaliation begins.");

                Assert.That(hostile.IssueFocus(movingFormation), Is.True);
                Assert.That(movingFormation.Target, Is.SameAs(hostile));
                Assert.That(movingFormation.CurrentOrder, Is.EqualTo(FormationOrder.AttackMove));
                Assert.That(movingFormation.HasDestination, Is.True);
                Assert.That(movingFormation.Destination, Is.EqualTo(destination));

                Object.Destroy(hostile.gameObject);
                yield return null;
                var resumeDistance = Vector3.Distance(movingFormation.transform.position, destination);
                yield return new WaitForSeconds(0.2f);

                Assert.That(movingFormation.Target, Is.Null);
                Assert.That(movingFormation.CurrentOrder, Is.EqualTo(FormationOrder.AttackMove));
                Assert.That(movingFormation.HasDestination, Is.True);
                Assert.That(Vector3.Distance(movingFormation.transform.position, destination),
                    Is.LessThan(resumeDistance - 0.25f),
                    "The formation must resume progress toward its original attack-move destination.");
            }
            finally
            {
                Object.Destroy(movingFormation.gameObject);
                if (hostile != null) Object.Destroy(hostile.gameObject);
                Object.Destroy(tuning);
            }
        }

        [UnityTest]
        public IEnumerator AttackMove_RevealsAndAcquiresTheNearestHostileThroughFog()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            Assert.That(economy.TryPlaceHouse(economy.Workers[0], VisibleHouseSite), Is.True);
            yield return WaitUntil(() => economy.PopulationCapacity == 20);
            economy.CreditSuppliesForAutomation(400);
            Assert.That(economy.TryQueueFormation(FormationType.Cavalry), Is.True);
            yield return WaitUntil(() => economy.FriendlyFormations.Count == 1);
            economy.DeployEnemyForAutomation(FormationType.Archers, new Vector3(0f, 0f, 26f));
            var cavalry = economy.FriendlyFormations[0];
            var archers = economy.EnemyFormations[0];
            economy.FogOfWar.RefreshNow();
            Assert.That(archers.Type, Is.EqualTo(FormationType.Archers));
            Assert.That(economy.FogOfWar.IsCurrentlyVisible(archers), Is.False);
            Assert.That(archers.GetComponentsInChildren<Renderer>(true).Any(item => item.enabled), Is.False);
            var orderText = GameObject.Find("Order").GetComponent<Text>();
            Assert.That(orderText.text, Does.Not.Contain("ENEMY ARCHERS SIGHTED"),
                "Training feedback must not identify an enemy that remains hidden by fog.");

            economy.SelectOnly(cavalry);
            economy.IssueAttackMoveForSelected(new Vector3(0f, 0f, 22f));
            yield return WaitUntil(() => economy.FogOfWar.IsCurrentlyVisible(archers));
            yield return null;
            Assert.That(orderText.text, Does.Contain("ENEMY ARCHERS SIGHTED"),
                "The first current-vision reveal should identify the hostile formation.");
            Assert.That(cavalry.Target, Is.SameAs(archers));
            Assert.That(archers.Target, Is.SameAs(cavalry));
            var hostilePosition = archers.transform.position;
            yield return WaitUntil(() => economy.EnemyFormations.Count == 0);

            Assert.That(cavalry.MemberCount, Is.GreaterThanOrEqualTo(4));
            Assert.That(cavalry.transform.position.z, Is.GreaterThan(0f));
            Assert.That(economy.FogOfWar.MinimapColorAt(hostilePosition).r, Is.LessThan(0.9f));
        }

        [UnityTest]
        public IEnumerator OpponentEconomy_GathersBuildsAndTrainsWithoutHiddenGrants()
        {
            yield return SceneManager.LoadSceneAsync(HarnessContract.SceneName, LoadSceneMode.Single);
            yield return null;
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var originalTimeScale = Time.timeScale;
            try
            {
                Time.timeScale = 20f;
                yield return WaitUntil(() => economy.EnemyBuildings.Any(building => building.IsComplete) &&
                                             economy.EnemyFormations.Count >= 2);
                Time.timeScale = 0f;

                var tuning = GetPrivateField<EconomyTuning>(economy, "tuning");
                var opponent = GetPrivateField<ScriptedOpponentController>(economy, "opponent");
                var gathered = economy.Caches.Concat(economy.OpponentCaches)
                    .Sum(cache => tuning.cacheSupplies - cache.Remaining);
                var carried = economy.EnemyWorkers.Sum(worker => worker.CarriedSupplies);
                var formationsPaidFor = economy.EnemyFormations.Count + opponent.ProductionQueueCount;
                var spent = economy.EnemyBuildings.Count * tuning.houseCost +
                            formationsPaidFor * tuning.formationCost;
                Assert.That(economy.EnemyBuildings.All(building => building.Type == BuildingType.House), Is.True);
                Assert.That(tuning.startingSupplies + gathered,
                    Is.EqualTo(economy.OpponentSupplies + carried + spent),
                    "The opponent's wallet and spending must reconcile to finite gathered Supplies.");
                Assert.That(economy.OpponentPopulationUsed,
                    Is.EqualTo(StartingEconomyController.WorkerCount +
                               formationsPaidFor * tuning.formationPopulation));
                Assert.That(economy.OpponentPopulationCapacity,
                    Is.EqualTo(tuning.startingPopulationCap + economy.EnemyBuildings.Count(building =>
                        building.IsComplete) * tuning.housePopulationCapacity));
                Assert.That(economy.EnemyWorkers.Count(worker => worker.IsAlive), Is.EqualTo(4));
                Assert.That(economy.EnemyFormations.Select(formation => formation.Type),
                    Does.Contain(FormationType.Cavalry));
                Assert.That(economy.CurrentMatchSummary.hostileSuppliesGathered, Is.EqualTo(gathered - carried));
            }
            finally
            {
                Time.timeScale = originalTimeScale;
            }
        }

        [UnityTest]
        public IEnumerator OpponentEconomy_RecoveryFirstKeepsMonotonicPaidFormationProofAfterLoss()
        {
            yield return SceneManager.LoadSceneAsync(HarnessContract.SceneName, LoadSceneMode.Single);
            yield return null;
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var tuning = GetPrivateField<EconomyTuning>(economy, "tuning");
            var originalTimeScale = Time.timeScale;
            try
            {
                Time.timeScale = 20f;
                economy.SetOpponentEnabledForAutomation(false);
                economy.SetOpponentTargetsAvailableForAutomation(false);
                economy.CreditOpponentSuppliesForAutomation(tuning.storehouseCost);
                Assert.That(economy.TriggerOpponentRouteFailureForAutomation(), Is.True);
                economy.SetOpponentEnabledForAutomation(true);

                yield return WaitUntil(() => economy.EnemyBuildings.Any(building =>
                    building.Type == BuildingType.Storehouse && building.IsComplete));
                yield return WaitUntil(() => economy.CurrentMatchSummary.hostileSuppliesGathered > 0);
                yield return WaitUntil(() => economy.OpponentFormationsProduced >= 1);

                var firstFormation = economy.EnemyFormations.First();
                while (firstFormation.MemberCount > 0) firstFormation.ApplyFixedDamage(int.MaxValue);
                yield return null;
                yield return WaitUntil(() => economy.OpponentFormationsProduced >= 2);
                Time.timeScale = 0f;

                Assert.That(economy.OpponentFormationsProduced, Is.GreaterThan(economy.EnemyFormations.Count),
                    "Completed production must remain monotonic after the first paid formation is lost.");
                Assert.That(SmokeVerificationRules.HasFairOpponentEconomy(
                    economy.OpponentFormationsProduced,
                    economy.EnemyBuildings.Count(building => building.IsComplete),
                    economy.OpponentPopulationCapacity,
                    economy.CurrentMatchSummary.hostileSuppliesGathered), Is.True);

                var gathered = economy.Caches.Concat(economy.OpponentCaches)
                    .Sum(cache => tuning.cacheSupplies - cache.Remaining);
                var carried = economy.EnemyWorkers.Sum(worker => worker.CarriedSupplies);
                var buildingSpend = economy.EnemyBuildings.Sum(building => building.Type switch
                {
                    BuildingType.House => tuning.houseCost,
                    BuildingType.Storehouse => tuning.storehouseCost,
                    _ => tuning.watchtowerCost
                });
                var formationCommitments = economy.OpponentFormationsProduced +
                                           economy.OpponentProductionQueueCount;
                Assert.That(tuning.startingSupplies + tuning.storehouseCost + gathered,
                    Is.EqualTo(economy.OpponentSupplies + carried + buildingSpend +
                               formationCommitments * tuning.formationCost),
                    "Recovery credit must be fully consumed by the real Storehouse; formations remain paid from finite gathering.");
                Assert.That(economy.OpponentPopulationUsed,
                    Is.EqualTo(StartingEconomyController.WorkerCount +
                               (economy.EnemyFormations.Count + economy.OpponentProductionQueueCount) *
                               tuning.formationPopulation));
            }
            finally
            {
                Time.timeScale = originalTimeScale;
            }
        }

        [UnityTest]
        public IEnumerator OpponentScript_TransitionsThroughProbePressureAndFinalAssault()
        {
            yield return SceneManager.LoadSceneAsync(HarnessContract.SceneName, LoadSceneMode.Single);
            yield return null;
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            Time.timeScale = 20f;
            yield return WaitUntil(() => economy.EnemyFormations.Count >= 2);
            Time.timeScale = 0f;

            economy.AdvanceMatchClockForAutomation(Mathf.Max(0f, 180f - economy.MatchElapsedSeconds));
            yield return null;
            Assert.That(economy.OpponentPhase, Is.EqualTo(AiPhase.Probe));
            Assert.That(economy.EnemyFormations.Single(formation => formation.Type == FormationType.Cavalry)
                .CurrentOrder, Is.EqualTo(FormationOrder.AttackMove));

            economy.AdvanceMatchClockForAutomation(Mathf.Max(0f, 360f - economy.MatchElapsedSeconds));
            yield return null;
            Assert.That(economy.OpponentPhase, Is.EqualTo(AiPhase.Pressure));
            Assert.That(economy.EnemyFormations.All(formation =>
                formation.CurrentOrder == FormationOrder.AttackMove), Is.True);

            economy.AdvanceMatchClockForAutomation(Mathf.Max(0f, 600f - economy.MatchElapsedSeconds));
            yield return null;
            Assert.That(economy.OpponentPhase, Is.EqualTo(AiPhase.FinalAssault));
            Assert.That(economy.EnemyFormations.All(formation =>
                ReferenceEquals(formation.StructureTarget, economy.FriendlyHisar)), Is.True);
            Assert.That(economy.CurrentMatchSummary.probeAttackSeconds, Is.GreaterThanOrEqualTo(180f));
            Assert.That(economy.CurrentMatchSummary.pressureAttackSeconds, Is.GreaterThanOrEqualTo(360f));
            Assert.That(economy.CurrentMatchSummary.finalAssaultSeconds, Is.GreaterThanOrEqualTo(600f));
            Time.timeScale = 1f;
        }

        [UnityTest]
        public IEnumerator OpponentScript_DefenseAcrossProbeBoundaryRecordsAttackOnlyWhenDispatched()
        {
            yield return SceneManager.LoadSceneAsync(HarnessContract.SceneName, LoadSceneMode.Single);
            yield return null;
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var originalTimeScale = Time.timeScale;

            try
            {
                Time.timeScale = 20f;
                yield return WaitUntil(() => economy.EnemyFormations.Any(formation =>
                    formation.Type == FormationType.Cavalry));
                Time.timeScale = 0f;
                Assert.That(economy.CurrentMatchSummary.probeAttackSeconds, Is.LessThan(0f));

                var threat = economy.DeployFriendlyForAutomation(FormationType.Spearmen,
                    economy.EnemyHisar.transform.position + Vector3.back * 6f);
                yield return null;
                yield return null;
                Assert.That(economy.OpponentIsDefending, Is.True);

                economy.AdvanceMatchClockForAutomation(Mathf.Max(0f, 180f - economy.MatchElapsedSeconds));
                yield return null;

                Assert.That(economy.OpponentPhase, Is.EqualTo(AiPhase.Probe));
                Assert.That(economy.CurrentMatchSummary.probeAttackSeconds, Is.LessThan(0f),
                    "Crossing the probe boundary during defense must not record an attack that did not depart.");
                Assert.That(economy.EnemyFormations.Any(formation => formation.Target == threat), Is.True);

                Assert.That(threat.GetComponent<NavMeshAgent>().Warp(new Vector3(0f, 0f, -4f)), Is.True);
                yield return null;
                yield return null;

                Assert.That(economy.OpponentIsDefending, Is.False);
                Assert.That(economy.CurrentMatchSummary.probeAttackSeconds,
                    Is.GreaterThanOrEqualTo(180f));
                Assert.That(economy.EnemyFormations.Single(formation => formation.Type == FormationType.Cavalry)
                    .CurrentOrder, Is.EqualTo(FormationOrder.AttackMove));
                var dispatchedAt = economy.CurrentMatchSummary.probeAttackSeconds;
                yield return null;
                Assert.That(economy.CurrentMatchSummary.probeAttackSeconds, Is.EqualTo(dispatchedAt),
                    "A phase's attack callback must be emitted only once.");
            }
            finally
            {
                Time.timeScale = originalTimeScale;
            }
        }

        [UnityTest]
        public IEnumerator OpponentScript_RecallsVisibleFormationsForEarlyBaseDefenseThenResumes()
        {
            yield return SceneManager.LoadSceneAsync(HarnessContract.SceneName, LoadSceneMode.Single);
            yield return null;
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var originalTimeScale = Time.timeScale;
            try
            {
                Time.timeScale = 20f;
                yield return WaitUntil(() => economy.EnemyFormations.Count >= 1);
                Time.timeScale = 0f;
                for (var index = 0; index < economy.EnemyWorkers.Count; index++)
                    Assert.That(economy.EnemyWorkers[index].GetComponent<NavMeshAgent>()
                        .Warp(new Vector3(14f + index, 0f, 12f)), Is.True);
                for (var index = 0; index < economy.EnemyBuildings.Count; index++)
                    economy.EnemyBuildings[index].transform.position = new Vector3(10f + index * 2f, 0f, 14f);
                for (var index = 0; index < economy.EnemyFormations.Count; index++)
                {
                    economy.EnemyFormations[index].IssueStop();
                    Assert.That(economy.EnemyFormations[index].GetComponent<NavMeshAgent>()
                        .Warp(economy.EnemyHisar.transform.position + Vector3.right * (8f + index * 2f)), Is.True);
                }

                var threat = economy.DeployFriendlyForAutomation(FormationType.Spearmen,
                    economy.EnemyHisar.transform.position + Vector3.left * 11.5f);
                yield return null;
                yield return null;

                Assert.That(economy.OpponentIsDefending, Is.False,
                    "A nearby formation outside the AI side's shared sight radius must not trigger defense.");
                Assert.That(economy.EnemyFormations.All(formation => formation.Target != threat), Is.True);

                Assert.That(threat.GetComponent<NavMeshAgent>()
                    .Warp(economy.EnemyHisar.transform.position + Vector3.back * 6f), Is.True);
                yield return null;
                yield return null;

                Assert.That(economy.OpponentIsDefending, Is.True);
                Assert.That(economy.EnemyFormations.Any(formation => formation.Target == threat), Is.True,
                    "The same threat must be recalled against once the AI side currently sees it.");

                Assert.That(threat.GetComponent<NavMeshAgent>().Warp(new Vector3(0f, 0f, -4f)), Is.True);
                yield return null;
                yield return null;
                Assert.That(economy.OpponentIsDefending, Is.False);
                Assert.That(economy.EnemyFormations.All(formation => formation.Target != threat), Is.True);
            }
            finally
            {
                Time.timeScale = originalTimeScale;
            }
        }

        [UnityTest]
        public IEnumerator OpponentScript_DefendsAVisibleRemoteWorkerThenResumesItsPhase()
        {
            yield return SceneManager.LoadSceneAsync(HarnessContract.SceneName, LoadSceneMode.Single);
            yield return null;
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var originalTimeScale = Time.timeScale;

            try
            {
                Time.timeScale = 20f;
                yield return WaitUntil(() => economy.EnemyFormations.Any(formation =>
                    formation.Type == FormationType.Cavalry));
                Time.timeScale = 0f;

                var defendedWorker = economy.EnemyWorkers.First(worker => worker.CurrentConstruction == null);
                Assert.That(defendedWorker.GetComponent<NavMeshAgent>().Warp(new Vector3(-15f, 0f, 12f)), Is.True);
                foreach (var worker in economy.EnemyWorkers.Where(worker => worker != defendedWorker))
                    Assert.That(worker.GetComponent<NavMeshAgent>().Warp(new Vector3(17f, 0f, 24f)), Is.True);
                for (var index = 0; index < economy.EnemyBuildings.Count; index++)
                    economy.EnemyBuildings[index].transform.position = new Vector3(15f + index, 0f, 24f);
                foreach (var formation in economy.EnemyFormations)
                {
                    formation.IssueStop();
                    Assert.That(formation.GetComponent<NavMeshAgent>()
                        .Warp(economy.EnemyHisar.transform.position + Vector3.left * 8f), Is.True);
                }

                var threat = economy.DeployFriendlyForAutomation(FormationType.Spearmen,
                    defendedWorker.transform.position + Vector3.left * 4f);
                economy.FogOfWar.RefreshNow();
                Assert.That(economy.FogOfWar.IsCurrentlyVisible(defendedWorker), Is.True);
                Assert.That(threat.IssueFocus(defendedWorker), Is.True);
                yield return null;
                yield return null;

                Assert.That(Vector3.Distance(threat.transform.position, economy.EnemyHisar.transform.position),
                    Is.GreaterThan(12f));
                Assert.That(economy.EnemyBuildings.All(building =>
                    Vector3.Distance(threat.transform.position, building.transform.position) > 10f), Is.True);
                Assert.That(economy.OpponentIsDefending, Is.True,
                    "An attack visible to a remote living worker must trigger the same defensive recall.");
                Assert.That(economy.EnemyFormations.Any(formation => formation.Target == threat), Is.True);

                economy.AdvanceMatchClockForAutomation(Mathf.Max(0f, 180f - economy.MatchElapsedSeconds));
                yield return null;
                Assert.That(economy.OpponentPhase, Is.EqualTo(AiPhase.Probe));
                Assert.That(economy.CurrentMatchSummary.probeAttackSeconds, Is.LessThan(0f));

                threat.IssueStop();
                Assert.That(threat.GetComponent<NavMeshAgent>().Warp(new Vector3(0f, 0f, -4f)), Is.True);
                yield return null;
                yield return null;

                Assert.That(economy.OpponentIsDefending, Is.False);
                Assert.That(economy.CurrentMatchSummary.probeAttackSeconds, Is.GreaterThanOrEqualTo(180f));
                Assert.That(economy.EnemyFormations.Single(formation => formation.Type == FormationType.Cavalry)
                    .CurrentOrder, Is.EqualTo(FormationOrder.AttackMove));
            }
            finally
            {
                Time.timeScale = originalTimeScale;
            }
        }

        [UnityTest]
        public IEnumerator StructureCombat_ResultRestartDefeatAndTelemetryCompleteTheMatchLoop()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var attackers = economy.DeployFriendlyForAutomation(FormationType.Cavalry,
                economy.EnemyHisar.transform.position + Vector3.back * 7f);
            economy.FogOfWar.RefreshNow();
            Assert.That(economy.FogOfWar.IsCurrentlyVisible(economy.EnemyHisar), Is.True);
            economy.SelectOnly(attackers);
            var mouse = InputSystem.AddDevice<Mouse>();
            mouse.MakeCurrent();
            Camera.main.GetComponent<RtsCameraController>().CenterOn(economy.EnemyHisar.transform.position);
            yield return null;
            var hisarCollider = economy.EnemyHisar.GetComponentInChildren<Collider>();
            var hisarClick = (Vector2)Camera.main.WorldToScreenPoint(hisarCollider.bounds.center);
            yield return PressMouseButton(economy, mouse, hisarClick, MouseButton.Right, "HandleOrderInput");
            Assert.That(ReferenceEquals(attackers.StructureTarget, economy.EnemyHisar), Is.True,
                "A real contextual right click must focus the visible hostile Hisar.");
            InputSystem.RemoveDevice(mouse);
            Time.timeScale = 20f;
            yield return WaitUntil(() => economy.Outcome == MatchOutcome.Victory);

            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(attackers.CurrentOrder, Is.EqualTo(FormationOrder.Idle));
            Assert.That(GameObject.Find("Match Result").activeInHierarchy, Is.True);
            Assert.That(GameObject.Find("Match Result Title").GetComponent<Text>().text, Is.EqualTo("VICTORY"));
            Assert.That(Resources.FindObjectsOfTypeAll<GameObject>().Single(item => item.name == "Top Bar").activeSelf,
                Is.False);
            Assert.That(GameObject.Find("Restart Match").GetComponent<Button>().interactable, Is.True);
            Assert.That(GameObject.Find("Quit Match").GetComponent<Button>().interactable, Is.True);
            Assert.That(System.IO.File.Exists(economy.MatchSummaryPath), Is.True);
            Assert.That(System.IO.File.Exists(economy.MatchEventLogPath), Is.True);
            var victorySummary = JsonUtility.FromJson<MatchSummary>(
                System.IO.File.ReadAllText(economy.MatchSummaryPath));
            Assert.That(victorySummary.outcome, Is.EqualTo(MatchOutcome.Victory.ToString()));
            Assert.That(victorySummary.destroyedHisar, Is.EqualTo(StartingEconomyController.EnemyHisarObjectName));

            var previous = economy;
            economy.RestartMatch();
            yield return WaitUntil(() => Object.FindAnyObjectByType<StartingEconomyController>() != previous);
            economy = Object.FindAnyObjectByType<StartingEconomyController>();
            economy.SetOpponentEnabledForAutomation(false);
            Assert.That(economy.Outcome, Is.EqualTo(MatchOutcome.InProgress));
            Assert.That(economy.Supplies, Is.EqualTo(economy.StartingSupplies));
            Assert.That(economy.FriendlyFormations, Is.Empty);
            Assert.That(economy.FogOfWar.StateAt(economy.EnemyHisar.transform.position),
                Is.EqualTo(FogState.Unexplored));

            var invaders = economy.DeployEnemyForAutomation(FormationType.Cavalry,
                economy.FriendlyHisar.transform.position + Vector3.forward * 7f);
            Assert.That(invaders.IssueFocus(economy.FriendlyHisar), Is.True);
            Time.timeScale = 20f;
            yield return WaitUntil(() => economy.Outcome == MatchOutcome.Defeat);
            Assert.That(GameObject.Find("Match Result Title").GetComponent<Text>().text, Is.EqualTo("DEFEAT"));
            GameObject.Find("Quit Match").GetComponent<Button>().onClick.Invoke();
            Assert.That(economy.QuitRequested, Is.True);
            Time.timeScale = 1f;
        }

        [UnityTest]
        public IEnumerator StructuralVolley_DamagesACompletedBuildingAtTheStandardizedReducedRate()
        {
            var tuning = ScriptableObject.CreateInstance<EconomyTuning>();
            var attackers = CreateFormationForTest("Structure attackers", FormationType.Spearmen, true, tuning);
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var building = root.AddComponent<ConstructibleBuilding>();
            building.Initialize(BuildingType.Storehouse, 0.1f, 100, Color.red, _ => { }, false);
            building.Advance(0.1f);
            try
            {
                Assert.That(attackers.ExecuteStructuralVolley(building), Is.True);
                Assert.That(building.Health, Is.EqualTo(100 - MatchRules.StructuralVolleyDamage(8,
                    tuning.structuralDamage)));
                yield return null;
            }
            finally
            {
                Object.Destroy(attackers.gameObject);
                Object.Destroy(root);
                Object.Destroy(tuning);
            }
        }

        [UnityTest]
        public IEnumerator HisarSharedQueue_TrainsAWorkerThroughTheRuntimeHud()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            economy.SelectHisar();
            yield return null;
            var workerButton = GameObject.Find("Train Worker").GetComponent<Button>();
            Assert.That(workerButton.gameObject.activeInHierarchy, Is.True);
            Assert.That(workerButton.GetComponentInChildren<Text>().text, Does.Contain("[Q]"));

            workerButton.onClick.Invoke();
            Assert.That(economy.Supplies, Is.Zero);
            Assert.That(economy.PopulationUsed, Is.EqualTo(5));
            Assert.That(economy.ProductionQueueCount, Is.EqualTo(1));
            yield return WaitUntil(() => economy.Workers.Count == 5);

            Assert.That(economy.Workers.Last().IsAlive, Is.True);
            Assert.That(economy.CurrentMatchSummary.friendlyEntitiesProduced, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator TrainedWorker_CrossSideFallbackUsesOnlyVisibleNeutralCaches()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            economy.SelectHisar();
            GameObject.Find("Train Worker").GetComponent<Button>().onClick.Invoke();
            yield return WaitUntil(() => economy.Workers.Count == 5);

            foreach (var enemyWorker in economy.EnemyWorkers) enemyWorker.Suspend();
            foreach (var startingCache in economy.Caches) startingCache.TakeBatch(int.MaxValue);
            var depletedCache = economy.OpponentCaches[0];
            var fallbackCache = economy.OpponentCaches[1];
            depletedCache.Initialize(10);
            var depletedScout = new GameObject("Trained worker depleted-cache scout");
            depletedScout.transform.position = depletedCache.transform.position;
            economy.FogOfWar.RegisterFriendly(depletedScout.transform);
            economy.FogOfWar.RefreshNow();

            var worker = economy.Workers.Last();
            Assert.That(GetPrivateField<IReadOnlyList<ResourceCache>>(worker, "knownCaches").Count,
                Is.EqualTo(4), "A trained Worker must receive the shared neutral-cache catalog.");
            Assert.That(economy.FogOfWar.StateAt(depletedCache.transform.position), Is.EqualTo(FogState.Visible));
            Assert.That(economy.FogOfWar.StateAt(fallbackCache.transform.position), Is.Not.EqualTo(FogState.Visible));

            var originalTimeScale = Time.timeScale;
            GameObject fallbackScout = null;
            try
            {
                Time.timeScale = 4f;
                worker.IssueGather(depletedCache);
                yield return WaitUntil(() => depletedCache.Remaining == 0 &&
                                             worker.CurrentActivity == WorkerAgent.Activity.Idle);
                Assert.That(fallbackCache.Remaining, Is.EqualTo(400),
                    "A trained Worker must not retarget to an unseen cross-side cache.");
                Assert.That(GetPrivateField<ResourceCache>(worker, "targetCache"), Is.Null);

                fallbackScout = new GameObject("Trained worker fallback-cache scout");
                fallbackScout.transform.position = fallbackCache.transform.position;
                economy.FogOfWar.RegisterFriendly(fallbackScout.transform);
                economy.FogOfWar.RefreshNow();
                Assert.That(economy.FogOfWar.StateAt(fallbackCache.transform.position), Is.EqualTo(FogState.Visible));

                depletedCache.Initialize(10);
                worker.IssueGather(depletedCache);
                yield return WaitUntil(() => depletedCache.Remaining == 0 &&
                    GetPrivateField<ResourceCache>(worker, "targetCache") == fallbackCache);
                Assert.That(worker.CurrentActivity, Is.Not.EqualTo(WorkerAgent.Activity.Idle));
            }
            finally
            {
                Time.timeScale = originalTimeScale;
                Object.Destroy(depletedScout);
                if (fallbackScout != null) Object.Destroy(fallbackScout);
            }
        }

        [UnityTest]
        public IEnumerator OpponentFailedGatherRoute_BuildsUsesAndCanLoseARealStorehouse()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var tuning = GetPrivateField<EconomyTuning>(economy, "tuning");
            var originalFallbackRadius = tuning.cacheFallbackRadius;
            var recoveryCache = economy.Caches[0];
            var scout = economy.DeployEnemyForAutomation(FormationType.Cavalry, recoveryCache.transform.position);
            var worker = economy.EnemyWorkers.First(candidate => candidate.CurrentConstruction == null);
            var opponent = GetPrivateField<ScriptedOpponentController>(economy, "opponent");
            var originalTimeScale = Time.timeScale;
            try
            {
                tuning.cacheFallbackRadius = 5f;
                foreach (var cache in economy.Caches.Concat(economy.OpponentCaches))
                    if (cache != recoveryCache) cache.TakeBatch(int.MaxValue);
                Assert.That(worker.CarriedSupplies, Is.Zero);
                Assert.That(GetPrivateField<ResourceCache>(worker, "targetCache"), Is.Not.EqualTo(recoveryCache));
                economy.CreditOpponentSuppliesForAutomation(tuning.storehouseCost);
                var suppliesBefore = economy.OpponentSupplies;
                worker.IssueGather(null);

                Assert.That(opponent.IsStorehouseRecoveryRequested, Is.True);
                Assert.That(opponent.RecoveryCache, Is.SameAs(recoveryCache));
                economy.SetOpponentEnabledForAutomation(true);
                Time.timeScale = 1f;
                yield return WaitUntil(() => economy.EnemyBuildings.Any(building =>
                    building.Type == BuildingType.Storehouse));
                var storehouse = economy.EnemyBuildings.Single(building => building.Type == BuildingType.Storehouse);
                Assert.That(economy.OpponentSupplies, Is.EqualTo(suppliesBefore - tuning.storehouseCost),
                    "Recovery must spend the mirrored Storehouse cost rather than grant a structure.");
                Assert.That(economy.EnemyWorkers.Count(candidate =>
                    ReferenceEquals(candidate.CurrentConstruction, storehouse)), Is.EqualTo(1),
                    "Exactly one hostile Worker must supply construction labor.");

                yield return WaitUntil(() => storehouse.IsComplete);
                Assert.That(economy.CurrentMatchSummary.hostileBuildingsConstructed, Is.GreaterThanOrEqualTo(1));
                var gatheredBeforeRecovery = economy.CurrentMatchSummary.hostileSuppliesGathered;
                yield return WaitUntil(() => economy.CurrentMatchSummary.hostileSuppliesGathered >
                                             gatheredBeforeRecovery);
                Assert.That(economy.EnemyWorkers.Any(candidate =>
                        Vector3.Distance(candidate.LastDropOffPoint, storehouse.DropOffPoint) < 0.1f), Is.True,
                    "The hostile Worker must use the completed Storehouse as its nearest real drop-off.");

                var additionalWorker = economy.EnemyWorkers.First(candidate => candidate != worker &&
                    candidate.CurrentConstruction == null && candidate.CarriedSupplies == 0);
                additionalWorker.IssueGather(null);
                yield return null;
                Assert.That(economy.EnemyBuildings.Count(building => building.Type == BuildingType.Storehouse),
                    Is.EqualTo(1), "A completed recovery Storehouse must be reused rather than duplicated.");
                Assert.That(additionalWorker.TargetCache, Is.SameAs(recoveryCache));

                economy.SetOpponentEnabledForAutomation(false);
                var suppliesBeforeDestruction = economy.OpponentSupplies;
                Assert.That(storehouse.ApplyStructuralDamage(storehouse.Health), Is.True);
                Assert.That(economy.OpponentSupplies, Is.EqualTo(suppliesBeforeDestruction),
                    "Enemy destruction must not refund the Storehouse.");
                Assert.That(opponent.IsStorehouseRecoveryRequested, Is.True,
                    "Losing an economy-critical Storehouse must reopen recovery without a hidden replacement.");
            }
            finally
            {
                tuning.cacheFallbackRadius = originalFallbackRadius;
                Time.timeScale = originalTimeScale;
                Object.Destroy(scout.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator FunctionalAudioAndUnderAttackPing_AreProceduralThrottledFogAwareAndDoNotMoveCamera()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var audio = economy.GameplayAudio;
            var cameraPosition = Camera.main.transform.position;
            Assert.That(audio, Is.Not.Null);
            Assert.That(audio.HasAllFunctionalCues, Is.True);
            Assert.That(audio.GetComponent<AudioSource>().clip, Is.Null,
                "Procedural cues must not depend on an imported external clip.");

            economy.SelectOnly(economy.Workers[0]);
            economy.IssueMoveForSelected(new Vector3(2f, 0f, 1f));
            Assert.That(audio.CountFor(GameplayCue.Selection), Is.GreaterThan(0));
            Assert.That(audio.CountFor(GameplayCue.Order), Is.GreaterThan(0));

            economy.CreditSuppliesForAutomation(100);
            Assert.That(economy.TryPlaceHouse(economy.Workers[1], VisibleHouseSite), Is.True);
            yield return WaitUntil(() => economy.PopulationCapacity == 20);
            Assert.That(audio.CountFor(GameplayCue.Construction), Is.GreaterThan(0));

            economy.SelectHisar();
            Assert.That(economy.TryQueueWorker(), Is.True);
            yield return WaitUntil(() => economy.Workers.Count == 5);
            Assert.That(audio.CountFor(GameplayCue.Production), Is.GreaterThan(0));

            var friendly = economy.DeployFriendlyForAutomation(FormationType.Spearmen, new Vector3(0f, 0f, 2f));
            var hostile = economy.DeployEnemyForAutomation(FormationType.Archers, new Vector3(0f, 0f, 4f));
            economy.FogOfWar.RefreshNow();
            Assert.That(friendly.ExecuteAttackVolley(hostile), Is.True);
            Assert.That(audio.CountFor(GameplayCue.Attack), Is.GreaterThan(0));

            var warningsBefore = economy.UnderAttackWarningCount;
            friendly.ApplyFixedDamage(1);
            var warningCount = economy.UnderAttackWarningCount;
            Assert.That(warningCount, Is.EqualTo(warningsBefore + 1));
            Assert.That(audio.CountFor(GameplayCue.Hit), Is.GreaterThan(0));
            Assert.That(audio.CountFor(GameplayCue.Warning), Is.GreaterThan(0));
            Assert.That(economy.FogOfWar.IsAttackPingVisible, Is.True);
            Assert.That(Vector3.Distance(economy.FogOfWar.LastAttackPingPosition, friendly.transform.position),
                Is.LessThan(0.1f));
            friendly.ApplyFixedDamage(1);
            Assert.That(economy.UnderAttackWarningCount, Is.EqualTo(warningCount),
                "Repeated damage inside the warning window must not spam cues or pings.");
            Assert.That(Camera.main.transform.position, Is.EqualTo(cameraPosition),
                "Under-attack feedback must never steal the camera.");

            economy.FogOfWar.RefreshNow();
            var hiddenPosition = economy.OpponentCaches[0].transform.position;
            Assert.That(economy.FogOfWar.StateAt(hiddenPosition), Is.EqualTo(FogState.Unexplored));
            Assert.That(economy.FogOfWar.ShowAttackPing(hiddenPosition), Is.False,
                "A minimap ping must not reveal unexplored terrain.");

            economy.DestroyHisarForAutomation(true);
            Assert.That(audio.CountFor(GameplayCue.Victory), Is.EqualTo(1));
            Time.timeScale = 1f;
        }

        [UnityTest]
        public IEnumerator HisarContextualRally_RightClickCacheAndTerrainDispatchesWorkerAndFormation()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var mouse = InputSystem.AddDevice<Mouse>();
            var keyboard = InputSystem.AddDevice<Keyboard>();
            mouse.MakeCurrent();
            keyboard.MakeCurrent();
            try
            {
                var visibleCache = economy.Caches[0];
                economy.SelectHisar();
                var cacheClick = (Vector2)Camera.main.WorldToScreenPoint(
                    visibleCache.GetComponent<Collider>().bounds.center);
                yield return PressMouseButton(economy, mouse, cacheClick, MouseButton.Right, "HandleOrderInput");
                Assert.That(economy.HisarRallyCache, Is.SameAs(visibleCache));
                Assert.That(GameObject.Find("Hisar Rally Point"), Is.Not.Null);
                Assert.That(GameObject.Find("Selection").GetComponent<Text>().text, Does.Contain("CARAVAN CACHE"));

                GameObject.Find("Train Worker").GetComponent<Button>().onClick.Invoke();
                yield return WaitUntil(() => economy.Workers.Count == 5);
                var trainedWorker = economy.Workers.Last();
                Assert.That(GetPrivateField<ResourceCache>(trainedWorker, "targetCache"), Is.SameAs(visibleCache));
                Assert.That(trainedWorker.CurrentActivity, Is.Not.EqualTo(WorkerAgent.Activity.Idle));

                economy.CreditSuppliesForAutomation(100);
                Assert.That(economy.TryPlaceHouse(economy.Workers[1], VisibleHouseSite), Is.True);
                yield return WaitUntil(() => economy.PopulationCapacity == 20);
                economy.CreditSuppliesForAutomation(400);
                economy.SelectHisar();
                var terrainPoint = new Vector3(12f, 0f, 9f);
                var terrainClick = (Vector2)Camera.main.WorldToScreenPoint(terrainPoint);
                yield return PressMouseButton(economy, mouse, terrainClick, MouseButton.Right, "HandleOrderInput");
                Assert.That(economy.HisarRallyCache, Is.Null);
                Assert.That(economy.HisarRallyPoint, Is.Not.Null);

                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S));
                InputSystem.Update();
                InvokePrivateMethod(economy, "HandleBuildInput");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
                InvokePrivateMethod(economy, "HandleBuildInput");
                yield return WaitUntil(() => economy.FriendlyFormations.Count == 1);
                var trainedFormation = economy.FriendlyFormations.Single();
                Assert.That(trainedFormation.CurrentOrder, Is.EqualTo(FormationOrder.Move));
                Assert.That(Vector3.Distance(trainedFormation.Destination, economy.HisarRallyPoint.Value),
                    Is.LessThan(0.1f));

                var hiddenCache = economy.OpponentCaches[0];
                Assert.That(economy.FogOfWar.StateAt(hiddenCache.transform.position), Is.EqualTo(FogState.Unexplored));
                Assert.That(economy.SetHisarRallyForAutomation(hiddenCache.transform.position, hiddenCache), Is.False,
                    "The Hisar must reject an unseen neutral-cache rally target.");
                Assert.That(economy.HisarRallyCache, Is.Null);
            }
            finally
            {
                if (Mouse.current == mouse) mouse.MakeCurrent();
                InputSystem.RemoveDevice(mouse);
                InputSystem.RemoveDevice(keyboard);
            }
        }

        [UnityTest]
        public IEnumerator Hud_ShowsPopulationAndClickableBuildCommands()
        {
            yield return LoadEconomy();

            Assert.That(GameObject.Find("Population").GetComponent<UnityEngine.UI.Text>().text,
                Is.EqualTo("POPULATION   4 / 12"));
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            economy.SelectOnly(economy.Workers[0]);
            Assert.That(GameObject.Find("Build House").GetComponent<UnityEngine.UI.Button>(), Is.Not.Null);
            Assert.That(GameObject.Find("EventSystem").GetComponents<MonoBehaviour>()
                .Any(component => component.GetType().Name == "InputSystemUIInputModule"), Is.True);
            Assert.That(GameObject.Find("Build House").GetComponentInChildren<UnityEngine.UI.Text>().text,
                Does.Contain("[H]"));
            Assert.That(GameObject.Find("Build Storehouse").GetComponentInChildren<UnityEngine.UI.Text>().text,
                Does.Contain("200 [R]"));
            Assert.That(GameObject.Find("Build Watchtower").GetComponentInChildren<UnityEngine.UI.Text>().text,
                Does.Contain("300 [T]"));
            Assert.That(GameObject.Find("Cancel Build").GetComponentInChildren<UnityEngine.UI.Text>().text,
                Does.Contain("[X]"));

            economy.SelectHisar();
            yield return null;
            Assert.That(GameObject.Find("Train Spearmen").GetComponentInChildren<UnityEngine.UI.Text>().text,
                Does.Contain("[S]"));
            Assert.That(GameObject.Find("Train Archers").GetComponentInChildren<UnityEngine.UI.Text>().text,
                Does.Contain("[A]"));
            Assert.That(GameObject.Find("Train Cavalry").GetComponentInChildren<UnityEngine.UI.Text>().text,
                Does.Contain("[C]"));

            economy.SelectOnly(economy.Workers[0]);
            GameObject.Find("Build House").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.That(economy.IsHousePlacementActive, Is.True);
        }

        private static IEnumerator LoadEconomy()
        {
            yield return SceneManager.LoadSceneAsync(HarnessContract.SceneName, LoadSceneMode.Single);
            yield return null;
            Object.FindAnyObjectByType<StartingEconomyController>()?.SetOpponentEnabledForAutomation(false);
        }

        private static IEnumerator WaitUntil(System.Func<bool> condition)
        {
            var deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, $"Condition did not become true within {TimeoutSeconds} seconds.");
        }

        private static int PlacementPreviewCount() =>
            GameObject.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .Count(item => item.name.EndsWith("Placement Preview"));

        private static void SetPrivateField<T>(object target, string fieldName, T value) =>
            target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(target, value);

        private static T GetPrivateField<T>(object target, string fieldName) =>
            (T)target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(target);

        private static void InvokePrivateMethod(object target, string methodName, params object[] arguments) =>
            target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(target, arguments);

        private static void PressControlGroupHotkey(StartingEconomyController economy, Keyboard keyboard,
            params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            InputSystem.Update();
            InvokePrivateMethod(economy, "HandleControlGroupInput");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            InvokePrivateMethod(economy, "HandleControlGroupInput");
        }

        private static void QueueCoalescedKeyboardChord(Keyboard keyboard, params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
        }

        private static IEnumerator PressMouseButton(StartingEconomyController economy, Mouse mouse, Vector2 position,
            MouseButton button, string handler)
        {
            var buttonControl = button == MouseButton.Left ? mouse.leftButton : mouse.rightButton;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(button));
            InputSystem.Update();
            Assert.That(Mouse.current, Is.SameAs(mouse));
            Assert.That(buttonControl.isPressed, Is.True);
            InvokePrivateMethod(economy, handler);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            InputSystem.Update();
            Assert.That(buttonControl.isPressed, Is.False);
            InvokePrivateMethod(economy, handler);
            yield break;
        }

        private static void QueueCoalescedClick(Mouse mouse, Vector2 position, MouseButton button)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(button));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            InputSystem.Update();
            Assert.That(Mouse.current, Is.SameAs(mouse));
            Assert.That(button == MouseButton.Left ? mouse.leftButton.isPressed : mouse.rightButton.isPressed,
                Is.False);
        }

        private static IEnumerator DragBattlefieldSelection(StartingEconomyController economy, Mouse mouse,
            Vector2 start, Vector2 end)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = start }.WithButton(MouseButton.Left));
            InputSystem.Update();
            Assert.That(Mouse.current, Is.SameAs(mouse));
            Assert.That(mouse.leftButton.isPressed, Is.True);
            InvokePrivateMethod(economy, "HandleSelectionInput");
            InputSystem.QueueStateEvent(mouse, new MouseState { position = end }.WithButton(MouseButton.Left));
            InputSystem.Update();
            InvokePrivateMethod(economy, "HandleSelectionInput");
            InputSystem.QueueStateEvent(mouse, new MouseState { position = end });
            InputSystem.Update();
            Assert.That(mouse.leftButton.isPressed, Is.False);
            InvokePrivateMethod(economy, "HandleSelectionInput");
            yield break;
        }

        private static FormationAgent CreateFormationForTest(string name, FormationType type, bool friendly,
            EconomyTuning tuning, System.Func<IEnumerable<FormationAgent>> availableHostiles = null,
            System.Action<int> onCasualty = null, System.Action<Vector3> onAttack = null)
        {
            var root = new GameObject(name);
            root.transform.position = new Vector3(100f, 0f, 100f);
            var formation = root.AddComponent<FormationAgent>();
            formation.Initialize(type, friendly, tuning, onCasualty, availableHostiles: availableHostiles,
                onAttack: onAttack);
            return formation;
        }

        private static GameObject CreateRouteBlocker(string name, Vector3 position, Vector3 scale)
        {
            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.name = name;
            blocker.transform.position = position;
            blocker.transform.localScale = scale;
            var obstacle = blocker.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.carving = true;
            obstacle.carveOnlyStationary = false;
            return blocker;
        }

        private static void AssertSweptSegmentOutsideBounds(Vector3 start, Vector3 end, Bounds bounds)
        {
            var distance = Vector3.Distance(start, end);
            var samples = Mathf.Max(1, Mathf.CeilToInt(distance / 0.02f));
            for (var sample = 0; sample <= samples; sample++)
            {
                var position = Vector3.Lerp(start, end, sample / (float)samples);
                var footprintPosition = new Vector3(position.x, bounds.center.y, position.z);
                Assert.That(bounds.Contains(footprintPosition), Is.False,
                    $"The swept member step must not cross the carved obstacle at {footprintPosition}.");
            }
        }
    }
}
