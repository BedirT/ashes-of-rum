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
        public IEnumerator StorehouseConstruction_CancelsRefundsCompletesAndBecomesNearestDropOff()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var worker = economy.Workers[0];
            economy.CreditSuppliesForAutomation(100);

            Assert.That(economy.TryPlaceStorehouse(worker, new Vector3(12f, 0f, 6f)), Is.True);
            Assert.That(economy.Supplies, Is.Zero);
            Assert.That(economy.CancelConstruction(worker), Is.True);
            yield return null;
            Assert.That(economy.Supplies, Is.EqualTo(200));
            Assert.That(economy.Storehouses, Is.Empty);

            Assert.That(economy.TryPlaceStorehouse(worker, new Vector3(12f, 0f, 6f)), Is.True);
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
            Assert.That(economy.TryPlaceStorehouse(economy.Workers[0], new Vector3(12f, 0f, 6f)), Is.True);
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
            Assert.That(economy.TryPlaceHouse(economy.Workers[0], new Vector3(12f, 0f, -1f)), Is.True);
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

            yield return WaitUntil(() => economy.FriendlyFormations.Count == 1 && economy.EnemyFormations.Count == 1);
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

            Assert.That(archers.ExecuteAttackVolley(spearmen), Is.True);
            yield return null;
            var arrows = GameObject.FindObjectsByType<Renderer>(FindObjectsSortMode.None)
                .Where(itemRenderer => itemRenderer.name == "Arrow").ToArray();
            Assert.That(arrows, Has.Length.EqualTo(8));
            Assert.That(arrows.All(FormationAgent.UsesSupportedMaterial), Is.True);
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

            InputSystem.RemoveDevice(keyboard);
        }

        [UnityTest]
        public IEnumerator FormationGroups_PreserveLayoutRecallAndApplyCommandsTogether()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            Assert.That(economy.TryPlaceHouse(economy.Workers[0], new Vector3(12f, 0f, -1f)), Is.True);
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
            Assert.That(economy.TryPlaceHouse(economy.Workers[0], new Vector3(12f, 0f, -1f)), Is.True);
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
        public IEnumerator HostileFocus_RequiresSideVisionAndDropsAfterTheTargetRetreats()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            Assert.That(economy.TryPlaceHouse(economy.Workers[0], new Vector3(12f, 0f, -1f)), Is.True);
            yield return WaitUntil(() => economy.PopulationCapacity == 20);
            economy.CreditSuppliesForAutomation(800);
            Assert.That(economy.TryQueueFormation(FormationType.Cavalry), Is.True);
            Assert.That(economy.TryQueueFormation(FormationType.Spearmen), Is.True);
            yield return WaitUntil(() => economy.FriendlyFormations.Count == 2 &&
                                         economy.EnemyFormations.Count == 1);

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
            remoteAttacker.IssueStop();

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
        public IEnumerator AttackMove_RevealsAndAcquiresTheNearestHostileThroughFog()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            Assert.That(economy.TryPlaceHouse(economy.Workers[0], new Vector3(12f, 0f, -1f)), Is.True);
            yield return WaitUntil(() => economy.PopulationCapacity == 20);
            economy.CreditSuppliesForAutomation(400);
            Assert.That(economy.TryQueueFormation(FormationType.Cavalry), Is.True);
            yield return WaitUntil(() => economy.FriendlyFormations.Count == 1 && economy.EnemyFormations.Count == 1);
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

        private static FormationAgent CreateFormationForTest(string name, FormationType type, bool friendly,
            EconomyTuning tuning)
        {
            var root = new GameObject(name);
            root.transform.position = new Vector3(100f, 0f, 100f);
            var formation = root.AddComponent<FormationAgent>();
            formation.Initialize(type, friendly, tuning);
            return formation;
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
