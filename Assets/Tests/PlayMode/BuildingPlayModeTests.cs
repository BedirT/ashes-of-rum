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

    }
}
