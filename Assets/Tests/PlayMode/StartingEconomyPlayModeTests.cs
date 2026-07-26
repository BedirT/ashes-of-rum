using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AshesOfRum.Tests
{
    public sealed class StartingEconomyPlayModeTests
    {
        private const float TimeoutSeconds = 15f;

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
            Assert.That(economy.Caches[0].Remaining, Is.EqualTo(190));
            Assert.That(worker.IsSelected, Is.True);
            Assert.That(worker.CurrentActivity, Is.EqualTo(WorkerAgent.Activity.GoingToCache));
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
            Assert.That(cache.Remaining, Is.EqualTo(190));
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
            yield return WaitUntil(() => secondCache.Remaining == 190);

            Assert.That(economy.Supplies, Is.EqualTo(economy.StartingSupplies + 10));
            Assert.That(firstCache.Remaining, Is.EqualTo(190));
            Assert.That(worker.CarriedSupplies, Is.EqualTo(10));
            Assert.That(worker.CurrentActivity, Is.EqualTo(WorkerAgent.Activity.Returning));
        }

        [UnityTest]
        public IEnumerator ReturningWorker_ExhaustedCacheOrderDepositsAndFallsBack()
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
            worker.IssueGather(exhaustedCache);
            yield return WaitUntil(() => economy.Supplies == economy.StartingSupplies + 20);

            Assert.That(availableCache.Remaining, Is.EqualTo(180));
            Assert.That(exhaustedCache.Remaining, Is.Zero);
            Assert.That(worker.CarriedSupplies, Is.Zero);
            Assert.That(worker.CurrentActivity, Is.EqualTo(WorkerAgent.Activity.GoingToCache));
            Assert.That(economy.LastEconomyNotification, Is.Null);
        }

        [UnityTest]
        public IEnumerator DepletedCache_MultipleWorkersRetargetToNearbyAvailableCache()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var depletedCache = economy.Caches[0];
            var fallbackCache = economy.Caches[1];
            depletedCache.Initialize(10);
            var firstWorker = economy.Workers[0];
            var secondWorker = economy.Workers[1];

            firstWorker.IssueGather(depletedCache);
            secondWorker.IssueGather(depletedCache);
            yield return WaitUntil(() => depletedCache.Remaining == 0);
            yield return WaitUntil(() => firstWorker.CurrentActivity != WorkerAgent.Activity.Idle &&
                                         secondWorker.CurrentActivity != WorkerAgent.Activity.Idle &&
                                         fallbackCache.Remaining < 200);

            Assert.That(firstWorker.CurrentActivity, Is.Not.EqualTo(WorkerAgent.Activity.Idle));
            Assert.That(secondWorker.CurrentActivity, Is.Not.EqualTo(WorkerAgent.Activity.Idle));
            Assert.That(economy.LastEconomyNotification, Is.Null);
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
        public IEnumerator HouseConstruction_SpendsCompletesRaisesCapacityAndResumesGathering()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var worker = economy.Workers[0];
            var cache = economy.Caches[0];
            worker.IssueGather(cache);
            yield return null;

            Assert.That(economy.TryPlaceHouse(worker, new Vector3(12f, 0f, -1f)), Is.True);
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

            Assert.That(economy.TryPlaceHouse(worker, new Vector3(12f, 0f, -1f)), Is.True);
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
            Assert.That(worker.CanReach(new Vector3(0f, 0f, 20f)), Is.True,
                "The worker must be able to navigate through the final open gap before placement.");

            Assert.That(economy.TryPlaceHouse(worker, new Vector3(0f, 0f, 10f)), Is.False);

            Assert.That(economy.Supplies, Is.EqualTo(economy.StartingSupplies));
            Assert.That(economy.Houses, Is.Empty);
            Assert.That(GameObject.Find("Order").GetComponent<UnityEngine.UI.Text>().text,
                Does.Contain("MUST PRESERVE A ROUTE"));
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

            yield return WaitUntil(() => economy.FriendlyFormations.Count == 1 && economy.EnemyFormations.Count == 1);
            var archers = economy.FriendlyFormations[0];
            var spearmen = economy.EnemyFormations[0];
            Assert.That(archers.MemberCount, Is.EqualTo(8));
            Assert.That(spearmen.MemberCount, Is.EqualTo(8));
            Assert.That(economy.IssueFocusForSmoke(archers, spearmen), Is.True);

            yield return WaitUntil(() => economy.EnemyFormations.Count == 0);

            Assert.That(archers.MemberCount, Is.GreaterThanOrEqualTo(4));
            Assert.That(economy.PopulationUsed, Is.EqualTo(4 + archers.MemberCount));
            Assert.That(GameObject.Find("Order").GetComponent<UnityEngine.UI.Text>().text,
                Does.Contain("ENEMY FORMATION DEFEATED"));
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
        }

        private static IEnumerator WaitUntil(System.Func<bool> condition)
        {
            var deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, $"Condition did not become true within {TimeoutSeconds} seconds.");
        }

        private static void CreateRouteBlocker(string name, Vector3 position, Vector3 scale)
        {
            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.name = name;
            blocker.transform.position = position;
            blocker.transform.localScale = scale;
            var obstacle = blocker.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.carving = true;
            obstacle.carveOnlyStationary = false;
        }
    }
}
