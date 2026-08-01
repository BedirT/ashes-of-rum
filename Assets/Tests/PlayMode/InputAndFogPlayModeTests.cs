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
                Time.timeScale = FastSimulationSpeed;
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

                Time.timeScale = FastSimulationSpeed;
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

    }
}
