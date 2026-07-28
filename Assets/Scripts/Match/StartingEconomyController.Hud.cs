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
        private void UpdateHud()
        {
            if (suppliesText == null) return;
            suppliesText.text = $"SUPPLIES   {Supplies}";
            if (populationText != null) populationText.text = $"POPULATION   {PopulationUsed} / {PopulationCapacity}";
            selectionText.text = selectedFormations.Count > 0
                ? $"{selectedFormations.Count} FORMATION{(selectedFormations.Count == 1 ? string.Empty : "S")}\n" +
                  string.Join("  |  ", selectedFormations.GroupBy(formation => formation.Type)
                      .Select(group => $"{group.Key}: {group.Count()}")) + "\n" +
                  (selectedFormations.Count == 1
                      ? $"FACING {selectedFormations[0].FacingLabel}  |  " +
                        (selectedFormations[0].IsFrontlineBlocked
                            ? "FRONTLINE BLOCKED"
                            : selectedFormations[0].IsTurning
                            ? $"TURNING {selectedFormations[0].TurnProgress:P0}"
                            : "READY")
                      : $"FACING: {selectedFormations.Count(formation => formation.IsTurning)} TURNING  |  " +
                        $"{selectedFormations.Count(formation => formation.IsFrontlineBlocked)} BLOCKED")
                : selectedBuilding != null
                    ? $"{selectedBuilding.Type.ToString().ToUpperInvariant()}\n" +
                      $"HEALTH {selectedBuilding.Health} / {selectedBuilding.MaxHealth}"
                : hisarSelected
                    ? $"KARASUNGUR HISAR\nSHARED QUEUE  |  RALLY: " +
                      (hisarRallyCache != null ? hisarRallyCache.name.ToUpperInvariant() :
                          hisarRallyPoint.HasValue ? "TERRAIN" : "DEFAULT")
                    : selectedWorkers.Count == 0
                        ? "No selection"
                        : $"{selectedWorkers.Count} WORKER{(selectedWorkers.Count == 1 ? string.Empty : "S")}\n" +
                          string.Join("  |  ", selectedWorkers.GroupBy(worker => worker.CurrentActivity)
                              .Select(group => $"{group.Key}: {group.Count()}"));
            queueText.text = productionQueue.Active.HasValue
                ? $"QUEUE: {productionQueue.Active.Value.ToString().ToUpperInvariant()} {productionQueue.Progress:P0}  +{productionQueue.Count - 1}"
                : "QUEUE: EMPTY";
            var canBuild = placementWorker == null && !awaitingAttackMove &&
                           selectedWorkers.Any(worker => worker.CurrentConstruction == null);
            buildHouseButton.interactable = canBuild && Supplies >= tuning.houseCost;
            buildStorehouseButton.interactable = canBuild && Supplies >= tuning.storehouseCost;
            buildWatchtowerButton.interactable = canBuild && Supplies >= tuning.watchtowerCost;
            cancelBuildButton.interactable = selectedWorkers.Any(worker => worker.CurrentConstruction != null);
            buildHouseButton.gameObject.SetActive(selectedWorkers.Count > 0);
            buildStorehouseButton.gameObject.SetActive(selectedWorkers.Count > 0);
            buildWatchtowerButton.gameObject.SetActive(selectedWorkers.Count > 0);
            cancelBuildButton.gameObject.SetActive(selectedWorkers.Count > 0);
            demolishButton.gameObject.SetActive(selectedBuilding != null);
            demolishButton.GetComponentInChildren<Text>().text = demolitionCandidate == selectedBuilding
                ? "CONFIRM DEMOLISH [X]"
                : "DEMOLISH [X]";
            trainSpearmenButton.gameObject.SetActive(hisarSelected);
            trainWorkerButton.gameObject.SetActive(hisarSelected);
            trainArchersButton.gameObject.SetActive(hisarSelected);
            trainCavalryButton.gameObject.SetActive(hisarSelected);
            cancelTrainingButton.gameObject.SetActive(hisarSelected);
            var canTrain = Supplies >= tuning.formationCost && PopulationCapacity - PopulationUsed >= tuning.formationPopulation;
            trainWorkerButton.interactable = Supplies >= tuning.workerCost && PopulationCapacity - PopulationUsed >= 1;
            trainSpearmenButton.interactable = canTrain;
            trainArchersButton.interactable = canTrain;
            trainCavalryButton.interactable = canTrain;
            cancelTrainingButton.interactable = productionQueue.Count > 0;
            attackMoveButton.gameObject.SetActive(selectedFormations.Count > 0);
            stopFormationsButton.gameObject.SetActive(selectedFormations.Count > 0);
            attackMoveButton.interactable = !awaitingAttackMove && placementWorker == null;
            stopFormationsButton.interactable = selectedFormations.Count > 0;
        }

        private void HandleBuildInput()
        {
            HandleQueuedInput();
            UpdatePlacementPreview();
        }

        private void UpdatePlacementPreview()
        {
            if (placementWorker == null || Mouse.current == null) return;
            UpdatePlacementPreview(Mouse.current.position.ReadValue());
        }

        private void UpdatePlacementPreview(Vector2 pointerPosition)
        {
            if (IsPointerOverHud(pointerPosition))
            {
                placementValid = false;
                SetOrderFeedback($"Place {placementType} on the battlefield");
                return;
            }
            if (Physics.Raycast(worldCamera.ScreenPointToRay(pointerPosition), out var hit, 200f))
            {
                placementPosition = HousePlacementRules.Snap(hit.point);
                placementPreview.transform.position = placementPosition;
                placementValid = CanPlaceBuilding(placementWorker, placementPosition, out var reason);
                TintPreview(placementValid ? new Color(0.2f, 0.8f, 0.35f, 0.55f) : new Color(0.9f, 0.2f, 0.15f, 0.55f));
                SetOrderFeedback(placementValid ? $"Place {placementType} - left click" : reason);
            }
            else
            {
                placementValid = false;
            }
        }

        private void TryPlaceBuildingAtPointer(Vector2 pointerPosition)
        {
            if (placementWorker == null || IsPointerOverHud(pointerPosition)) return;
            UpdatePlacementPreview(pointerPosition);
            if (!placementValid) return;
            var worker = placementWorker;
            var position = placementPosition;
            var type = placementType;
            EndBuildingPlacement(null);
            TryPlaceBuilding(worker, type, position);
        }

        private void BeginBuildingPlacement(BuildingType type)
        {
            if (awaitingAttackMove)
            {
                SetOrderFeedback("Cancel attack-move before placing a building");
                return;
            }
            if (placementWorker != null)
            {
                SetOrderFeedback($"Finish or cancel {placementType} placement first");
                return;
            }
            if (selectedWorkers.Count == 0)
            {
                SetOrderFeedback("Select a worker to build");
                return;
            }
            var cost = BuildingCost(type);
            if (Supplies < cost)
            {
                SetOrderFeedback($"Need {cost} Supplies");
                return;
            }
            var worker = selectedWorkers.FirstOrDefault(candidate => candidate.CurrentConstruction == null);
            if (worker == null)
            {
                SetOrderFeedback("Selected workers are already constructing");
                return;
            }
            if (placementPreview != null) Destroy(placementPreview);
            CancelSelectionGesture();
            placementWorker = worker;
            placementType = type;
            placementPreview = CreateBuildingVisual(type, $"{type} Placement Preview", Vector3.zero);
            foreach (var itemCollider in placementPreview.GetComponentsInChildren<Collider>())
                itemCollider.enabled = false;
            TintPreview(new Color(0.9f, 0.2f, 0.15f, 0.55f));
            placementValid = false;
            SetOrderFeedback($"Place {type} - left click / right click cancel");
        }

        private void EndBuildingPlacement(string feedback)
        {
            if (placementPreview != null) Destroy(placementPreview);
            placementPreview = null;
            placementWorker = null;
            placementValid = false;
            if (!string.IsNullOrEmpty(feedback)) SetOrderFeedback(feedback);
        }

        private void BeginAttackMoveTargeting()
        {
            if (selectedFormations.Count == 0) return;
            if (placementWorker != null)
            {
                SetOrderFeedback($"Finish or cancel {placementType} placement first");
                return;
            }
            CancelSelectionGesture();
            awaitingAttackMove = true;
            SetOrderFeedback("Attack-move - left click ground / right click cancel");
        }

        private void TryIssueAttackMoveAtPointer(Vector2 pointer)
        {
            if (!awaitingAttackMove || IsPointerOverHud(pointer)) return;
            if (!Physics.Raycast(worldCamera.ScreenPointToRay(pointer), out var hit, 200f)) return;
            IssueFormationGroupOrder(hit.point, true);
            CreateOrderMarker(hit.point, new Color(1f, 0.55f, 0.12f));
            awaitingAttackMove = false;
        }

        private void HandleControlGroupInput()
        {
            HandleQueuedInput();
        }

        private void IssueFormationGroupOrder(Vector3 destination, bool attackMove)
        {
            var live = selectedFormations.Where(formation => formation != null && formation.MemberCount > 0).ToList();
            if (live.Count == 0) return;
            var center = Vector3.zero;
            foreach (var formation in live) center += formation.transform.position;
            center /= live.Count;
            foreach (var formation in live)
            {
                var offset = formation.transform.position - center;
                var formationDestination = destination + new Vector3(offset.x, 0f, offset.z);
                if (attackMove) formation.IssueAttackMove(formationDestination);
                else formation.IssueMove(formationDestination);
            }
            SetOrderFeedback($"{(attackMove ? "Attack-move" : "Move")} - {live.Count} formation(s)");
            if (!attackMove) CreateOrderMarker(destination, new Color(0.2f, 0.78f, 1f));
            PlayCue(GameplayCue.Order);
        }

        private static bool IsPointerOverHud(Vector2 screenPosition)
        {
            if (EventSystem.current == null) return false;
            var pointer = new PointerEventData(EventSystem.current) { position = screenPosition };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            return hits.Any(hit => hit.gameObject.GetComponentInParent<Canvas>() != null);
        }

    }
}
