using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AshesOfRum
{
    public sealed partial class StartingEconomyController : MonoBehaviour
    {
        private void HandleSelectionInput()
        {
            HandleQueuedInput();
            UpdateSelectionGesture();
        }

        private void UpdateSelectionGesture()
        {
            var mouse = Mouse.current;
            if (!selecting) return;
            if (mouse != null && mouse.leftButton.isPressed)
            {
                UpdateSelectionBox(selectionStart, mouse.position.ReadValue());
                return;
            }
            selecting = false;
            selectionBox.gameObject.SetActive(false);
        }

        private void BeginSelection(PointerButtonTransition transition)
        {
            if (selecting)
            {
                selecting = false;
                selectionBox.gameObject.SetActive(false);
            }
            var position = transition.Position;
            if (position.y < Screen.height * 0.16f ||
                position.y > Screen.height * 0.9f || IsPointerOverHud(position)) return;
            selecting = true;
            selectionStart = position;
            selectionBox.gameObject.SetActive(true);
            UpdateSelectionBox(selectionStart, position);
        }

        private void CancelSelectionGesture()
        {
            if (!selecting) return;
            selecting = false;
            selectionBox.gameObject.SetActive(false);
        }

        private void CompleteSelection(PointerButtonTransition transition)
        {
            if (!selecting) return;
            selecting = false;
            selectionBox.gameObject.SetActive(false);
            if (placementWorker != null || awaitingAttackMove || IsPointerOverHud(transition.Position)) return;
            ApplySelection(selectionStart, transition.Position, transition.Modify);
        }

        private void HandleOrderInput()
        {
            HandleQueuedInput();
        }

        private void ApplyOrder(Vector2 position)
        {
            if (selectedWorkers.Count == 0 && selectedFormations.Count == 0 && !hisarSelected) return;
            if (!Physics.Raycast(worldCamera.ScreenPointToRay(position), out var hit, 200f)) return;
            if (hisarSelected)
            {
                var rallyCache = hit.collider.GetComponentInParent<ResourceCache>();
                if (rallyCache != null)
                {
                    if (IsCurrentlyVisible(rallyCache.transform.position))
                        TrySetHisarRally(rallyCache.transform.position, rallyCache);
                    else
                        SetOrderFeedback("Rally cache must be currently visible");
                    return;
                }
                TrySetHisarRally(hit.point, null);
                return;
            }
            var hostile = hit.collider.GetComponentInParent<FormationAgent>();
            if (hostile != null && !hostile.IsFriendly && selectedFormations.Count > 0)
            {
                foreach (var formation in selectedFormations) formation.IssueFocus(hostile);
                SetOrderFeedback($"Focus {hostile.Type} - {selectedFormations.Count} formation(s)");
                PlayCue(GameplayCue.Order);
                CreateOrderMarker(hostile.transform.position, new Color(1f, 0.22f, 0.1f));
                return;
            }

            var hostileWorker = hit.collider.GetComponentInParent<WorkerAgent>();
            if (hostileWorker != null && !hostileWorker.IsFriendly && selectedFormations.Count > 0)
            {
                foreach (var formation in selectedFormations) formation.IssueFocus(hostileWorker);
                SetOrderFeedback($"Focus worker - {selectedFormations.Count} formation(s)");
                PlayCue(GameplayCue.Order);
                CreateOrderMarker(hostileWorker.transform.position, new Color(1f, 0.22f, 0.1f));
                return;
            }

            ICombatStructure hostileStructure = hit.collider.GetComponentInParent<Hisar>();
            hostileStructure ??= hit.collider.GetComponentInParent<ConstructibleBuilding>();
            if (hostileStructure != null && !hostileStructure.IsFriendly && hostileStructure.IsAttackable &&
                selectedFormations.Count > 0)
            {
                foreach (var formation in selectedFormations) formation.IssueFocus(hostileStructure);
                SetOrderFeedback($"Focus structure - {selectedFormations.Count} formation(s)");
                PlayCue(GameplayCue.Order);
                CreateOrderMarker(hostileStructure.TargetComponent.transform.position, new Color(1f, 0.22f, 0.1f));
                return;
            }

            var cache = hit.collider.GetComponentInParent<ResourceCache>();
            if (cache != null && selectedWorkers.Count > 0 && IsCurrentlyVisible(cache.transform.position))
            {
                if (selectedFormations.Count > 0) IssueFormationGroupOrder(hit.point, false);
                var availableWorkers = selectedWorkers.Where(worker => worker.CurrentConstruction == null).ToList();
                if (availableWorkers.Count == 0)
                {
                    SetOrderFeedback("Cancel construction before issuing another order");
                    return;
                }
                foreach (var worker in availableWorkers) worker.IssueGather(cache);
                SetOrderFeedback($"Gather {cache.name}");
                PlayCue(GameplayCue.Order);
                CreateOrderMarker(cache.transform.position, new Color(0.95f, 0.68f, 0.2f));
                return;
            }
            IssueMoveForSelected(hit.point);
        }

        private void QueueInputEvent(InputEventPtr eventPtr, InputDevice device)
        {
            if (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>()) return;
            if (device is Mouse mouse) QueuePointerEvent(eventPtr, mouse);
            else if (device is Keyboard keyboard) QueueControlGroupEvent(eventPtr, keyboard);
        }

        private void QueuePointerEvent(InputEventPtr eventPtr, Mouse mouse)
        {
            var position = mouse.position.ReadValue();
            if (mouse.position.ReadValueFromEvent(eventPtr, out var eventPosition)) position = eventPosition;
            if (mouse.leftButton.ReadValueFromEvent(eventPtr, out var leftValue))
            {
                var leftPressed = leftValue >= InputSystem.settings.defaultButtonPressPoint;
                if (!mouse.leftButton.isPressed && leftPressed)
                    queuedInputs.Enqueue(QueuedInput.Pointer(InputCommand.LeftPressed, position));
                else if (mouse.leftButton.isPressed && !leftPressed)
                    queuedInputs.Enqueue(QueuedInput.Pointer(InputCommand.LeftReleased, position,
                        Keyboard.current?.shiftKey.isPressed == true));
            }
            if (mouse.rightButton.ReadValueFromEvent(eventPtr, out var rightValue) &&
                !mouse.rightButton.isPressed && rightValue >= InputSystem.settings.defaultButtonPressPoint)
                queuedInputs.Enqueue(QueuedInput.Pointer(InputCommand.RightPressed, position));
        }

        private void QueueControlGroupEvent(InputEventPtr eventPtr, Keyboard keyboard)
        {
            var assigning = IsPressedInEvent(keyboard.leftCtrlKey, eventPtr) ||
                            IsPressedInEvent(keyboard.rightCtrlKey, eventPtr) ||
                            IsPressedInEvent(keyboard.leftMetaKey, eventPtr) ||
                            IsPressedInEvent(keyboard.rightMetaKey, eventPtr);
            for (var index = 0; index < ControlGroupKeys.Length; index++)
            {
                var key = keyboard[ControlGroupKeys[index]];
                if (!key.ReadValueFromEvent(eventPtr, out var value) || key.isPressed ||
                    value < InputSystem.settings.defaultButtonPressPoint) continue;
                queuedInputs.Enqueue(QueuedInput.ControlGroup(index + 1, assigning));
                break;
            }
            QueueKeyPress(eventPtr, keyboard, Key.Escape);
            QueueKeyPress(eventPtr, keyboard, Key.F);
            QueueKeyPress(eventPtr, keyboard, Key.G);
            QueueKeyPress(eventPtr, keyboard, Key.H);
            QueueKeyPress(eventPtr, keyboard, Key.R);
            QueueKeyPress(eventPtr, keyboard, Key.T);
            QueueKeyPress(eventPtr, keyboard, Key.X);
            QueueKeyPress(eventPtr, keyboard, Key.S);
            QueueKeyPress(eventPtr, keyboard, Key.A);
            QueueKeyPress(eventPtr, keyboard, Key.C);
            QueueKeyPress(eventPtr, keyboard, Key.Q);
        }

        private void QueueKeyPress(InputEventPtr eventPtr, Keyboard keyboard, Key key)
        {
            if (WasPressedInEvent(keyboard[key], eventPtr)) queuedInputs.Enqueue(QueuedInput.KeyPress(key));
        }

        private static bool IsPressedInEvent(ButtonControl button, InputEventPtr eventPtr)
        {
            return button.ReadValueFromEvent(eventPtr, out var value)
                ? value >= InputSystem.settings.defaultButtonPressPoint
                : button.isPressed;
        }

        private static bool WasPressedInEvent(ButtonControl button, InputEventPtr eventPtr)
        {
            return button.ReadValueFromEvent(eventPtr, out var value) && !button.isPressed &&
                   value >= InputSystem.settings.defaultButtonPressPoint;
        }

        private void HandleQueuedInput()
        {
            while (queuedInputs.Count > 0)
            {
                var input = queuedInputs.Dequeue();
                switch (input.Command)
                {
                    case InputCommand.LeftPressed:
                    case InputCommand.LeftReleased:
                    case InputCommand.RightPressed:
                        HandlePointerCommand(input);
                        break;
                    case InputCommand.KeyPressed:
                        HandleKeyCommand(input.Key);
                        break;
                    case InputCommand.ControlGroupPressed:
                        if (placementWorker == null && !awaitingAttackMove)
                        {
                            if (input.Assigning) AssignControlGroup(input.Number);
                            else RecallControlGroup(input.Number);
                        }
                        break;
                }
            }
        }

        private void HandlePointerCommand(QueuedInput input)
        {
            if (input.Command == InputCommand.RightPressed)
            {
                CancelSelectionGesture();
                if (placementWorker != null)
                {
                    EndBuildingPlacement($"{placementType} placement cancelled");
                    return;
                }
                if (awaitingAttackMove)
                {
                    awaitingAttackMove = false;
                    SetOrderFeedback("Attack-move cancelled");
                    return;
                }
                if (!IsPointerOverHud(input.Position)) ApplyOrder(input.Position);
                return;
            }

            if (input.Command == InputCommand.LeftReleased)
            {
                if (placementWorker == null && !awaitingAttackMove)
                    CompleteSelection(new PointerButtonTransition(false, input.Position, input.Modify));
                return;
            }

            if (placementWorker != null)
            {
                TryPlaceBuildingAtPointer(input.Position);
                return;
            }
            if (awaitingAttackMove)
            {
                CancelSelectionGesture();
                TryIssueAttackMoveAtPointer(input.Position);
                return;
            }
            BeginSelection(new PointerButtonTransition(true, input.Position, false));
        }

        private void HandleKeyCommand(Key key)
        {
            if (key == Key.Escape)
            {
                CancelSelectionGesture();
                if (placementWorker != null) EndBuildingPlacement($"{placementType} placement cancelled");
                else if (awaitingAttackMove)
                {
                    awaitingAttackMove = false;
                    SetOrderFeedback("Attack-move cancelled");
                }
                return;
            }
            if (placementWorker != null) return;
            if (hisarSelected)
            {
                if (key == Key.Q) TryQueueWorker();
                else if (key == Key.S) TryQueueFormation(FormationType.Spearmen);
                else if (key == Key.A) TryQueueFormation(FormationType.Archers);
                else if (key == Key.C) TryQueueFormation(FormationType.Cavalry);
                else if (key == Key.X) CancelActiveTraining();
                return;
            }
            if (selectedBuilding != null)
            {
                if (key == Key.X) RequestDemolition();
                return;
            }
            if (selectedFormations.Count > 0)
            {
                if (key == Key.F) BeginAttackMoveTargeting();
                else if (key == Key.G) StopSelectedFormations();
            }
            if (selectedWorkers.Count > 0)
            {
                if (key == Key.H) BeginBuildingPlacement(BuildingType.House);
                else if (key == Key.R) BeginBuildingPlacement(BuildingType.Storehouse);
                else if (key == Key.T) BeginBuildingPlacement(BuildingType.Watchtower);
                else if (key == Key.X) CancelSelectedConstruction();
            }
        }

        private void ApplySelection(Vector2 start, Vector2 end, bool modify)
        {
            if (!modify) ClearSelection();
            var dragRect = ScreenRect(start, end);
            if (Vector2.Distance(start, end) < 8f)
            {
                if (Physics.Raycast(worldCamera.ScreenPointToRay(end), out var hit, 200f))
                {
                    var clickedFormation = hit.collider.GetComponentInParent<FormationAgent>();
                    if (clickedFormation != null && clickedFormation.IsFriendly)
                    {
                        if (modify && selectedFormations.Remove(clickedFormation)) clickedFormation.SetSelected(false);
                        else if (clickedFormation == lastClickedFormation &&
                                 Time.unscaledTime - lastFormationClickTime <= 0.35f)
                            SelectVisibleFormationsOfType(clickedFormation.Type);
                        else AddSelectedFormation(clickedFormation);
                        lastClickedFormation = clickedFormation;
                        lastFormationClickTime = Time.unscaledTime;
                        SetOrderFeedback($"Selected {selectedFormations.Count} formation(s)");
                        return;
                    }
                    var clickedBuilding = hit.collider.GetComponentInParent<ConstructibleBuilding>();
                    if (clickedBuilding != null && clickedBuilding.IsFriendly && clickedBuilding.IsComplete &&
                        !clickedBuilding.IsDestroyed)
                    {
                        selectedBuilding = clickedBuilding;
                        clickedBuilding.SetSelected(true);
                        SetOrderFeedback($"Selected {clickedBuilding.Type}");
                        return;
                    }
                    var clickedHisar = hit.collider.GetComponentInParent<Hisar>();
                    if (clickedHisar != null && clickedHisar.IsFriendly)
                    {
                        hisarSelected = true;
                        clickedHisar.SetSelected(true);
                        SetOrderFeedback("Hisar selected - train or right-click to set rally");
                        PlayCue(GameplayCue.Selection);
                        return;
                    }
                    var worker = hit.collider.GetComponentInParent<WorkerAgent>();
                    if (worker != null && worker.IsFriendly) ToggleSelection(worker, modify);
                }
            }
            else
            {
                foreach (var worker in workers)
                {
                    var screen = worldCamera.WorldToScreenPoint(worker.transform.position);
                    if (screen.z > 0f && dragRect.Contains(screen)) AddSelection(worker);
                }
                foreach (var formation in friendlyFormations)
                {
                    var screen = worldCamera.WorldToScreenPoint(formation.transform.position);
                    if (screen.z <= 0f || !dragRect.Contains(screen)) continue;
                    AddSelectedFormation(formation);
                }
            }
            if (selectedFormations.Count == 0 && !hisarSelected)
                SetOrderFeedback(selectedWorkers.Count == 0 ? "No selection" : $"Selected {selectedWorkers.Count} worker(s)");
        }

        private void ToggleSelection(WorkerAgent worker, bool modify)
        {
            if (modify && selectedWorkers.Remove(worker)) worker.SetSelected(false);
            else AddSelection(worker);
        }

        private void AddSelection(WorkerAgent worker)
        {
            if (selectedWorkers.Contains(worker)) return;
            selectedWorkers.Add(worker);
            worker.SetSelected(true);
            PlayCue(GameplayCue.Selection);
        }

        private void AddSelectedFormation(FormationAgent formation)
        {
            if (formation == null || !formation.IsFriendly || selectedFormations.Contains(formation)) return;
            selectedFormations.Add(formation);
            formation.SetSelected(true);
            PlayCue(GameplayCue.Selection);
        }

        private void SelectVisibleFormationsOfType(FormationType type)
        {
            foreach (var formation in friendlyFormations)
            {
                if (formation.Type != type) continue;
                var viewport = worldCamera.WorldToViewportPoint(formation.transform.position);
                if (viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f)
                    AddSelectedFormation(formation);
            }
        }

        private void ClearSelection()
        {
            foreach (var worker in selectedWorkers) worker.SetSelected(false);
            selectedWorkers.Clear();
            foreach (var formation in selectedFormations) formation.SetSelected(false);
            selectedFormations.Clear();
            if (selectedBuilding != null) selectedBuilding.SetSelected(false);
            selectedBuilding = null;
            demolitionCandidate = null;
            hisarSelected = false;
            hisar?.SetSelected(false);
            awaitingAttackMove = false;
        }

    }
}
