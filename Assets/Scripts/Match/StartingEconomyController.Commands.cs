using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AshesOfRum
{
    public sealed partial class StartingEconomyController
    {
        public bool TrySelectWorkersForCommand(IEnumerable<WorkerAgent> requestedWorkers,
            out string rejectionCode)
        {
            if (Outcome != MatchOutcome.InProgress)
                return RejectCommand("match_complete", "The match is complete", out rejectionCode);
            var requested = requestedWorkers?.Distinct().ToList();
            if (requested == null || requested.Count == 0)
                return RejectCommand("no_actors", "Select at least one Worker", out rejectionCode);
            if (requested.Any(worker => worker == null || !worker.IsFriendly || !worker.IsAlive ||
                                        !workers.Contains(worker)))
                return RejectCommand("invalid_actor", "A selected Worker is unavailable", out rejectionCode);

            ClearSelection();
            foreach (var worker in requested) AddSelection(worker);
            SetOrderFeedback($"Selected {requested.Count} worker(s)");
            UpdateHud();
            rejectionCode = null;
            return true;
        }

        public bool TryIssueWorkerMoveCommand(Vector3 destination, out string rejectionCode)
        {
            if (Outcome != MatchOutcome.InProgress)
                return RejectCommand("match_complete", "The match is complete", out rejectionCode);
            if (selectedWorkers.Count == 0)
                return RejectCommand("no_selection", "Select a Worker first", out rejectionCode);
            if (!IsFinite(destination) || destination.x < FogOfWarSystem.MinX ||
                destination.x > FogOfWarSystem.MaxX || destination.z < FogOfWarSystem.MinZ ||
                destination.z > FogOfWarSystem.MaxZ)
                return RejectCommand("invalid_position", "Move target is outside the battlefield", out rejectionCode);
            var available = selectedWorkers.Where(worker => worker.CurrentConstruction == null).ToList();
            if (available.Count == 0)
                return RejectCommand("actors_busy", "Cancel construction before issuing another order",
                    out rejectionCode);
            for (var index = 0; index < available.Count; index++)
            {
                var workerDestination = destination + FormationOffset(index, available.Count);
                if (!available[index].CanReach(workerDestination))
                    return RejectCommand("unreachable", "Every selected Worker must be able to reach its position",
                        out rejectionCode);
            }

            IssueMoveForSelected(destination);
            rejectionCode = null;
            return true;
        }

        public bool TrySelectFormationsForCommand(IEnumerable<FormationAgent> requestedFormations,
            out string rejectionCode)
        {
            if (Outcome != MatchOutcome.InProgress)
                return RejectCommand("match_complete", "The match is complete", out rejectionCode);
            var requested = requestedFormations?.Distinct().ToList();
            if (requested == null || requested.Count == 0)
                return RejectCommand("no_actors", "Select at least one formation", out rejectionCode);
            if (requested.Any(formation => formation == null || !formation.IsFriendly ||
                                           formation.MemberCount == 0 || !friendlyFormations.Contains(formation)))
                return RejectCommand("invalid_actor", "A selected formation is unavailable", out rejectionCode);

            ClearSelection();
            foreach (var formation in requested) AddSelectedFormation(formation);
            SetOrderFeedback($"Selected {requested.Count} formation(s)");
            UpdateHud();
            rejectionCode = null;
            return true;
        }

        public bool TryIssueFormationMoveCommand(Vector3 destination, out string rejectionCode)
        {
            if (!TryValidateFormationOrder(destination, out rejectionCode)) return false;
            IssueMoveForSelected(destination);
            rejectionCode = null;
            return true;
        }

        public bool TryIssueAttackMoveCommand(Vector3 destination, out string rejectionCode)
        {
            if (!TryValidateFormationOrder(destination, out rejectionCode)) return false;
            IssueAttackMoveForSelected(destination);
            rejectionCode = null;
            return true;
        }

        public bool TryIssueStopCommand(out string rejectionCode)
        {
            if (Outcome != MatchOutcome.InProgress)
                return RejectCommand("match_complete", "The match is complete", out rejectionCode);
            if (selectedFormations.All(formation => formation == null || formation.MemberCount == 0))
                return RejectCommand("no_selection", "Select a formation first", out rejectionCode);
            StopSelectedFormations();
            rejectionCode = null;
            return true;
        }

        public bool TryIssueFocusCommand(FormationAgent target, out string rejectionCode)
        {
            if (!TryValidateFocusTarget(target, target != null && target.MemberCount > 0 && !target.IsFriendly,
                    out rejectionCode)) return false;
            foreach (var formation in selectedFormations) formation.IssueFocus(target);
            SetOrderFeedback($"Focus {target.Type} - {selectedFormations.Count} formation(s)");
            CompleteFocusCommand(target.transform.position, out rejectionCode);
            return true;
        }

        public bool TryIssueFocusCommand(WorkerAgent target, out string rejectionCode)
        {
            if (!TryValidateFocusTarget(target, target != null && target.IsAlive && !target.IsFriendly,
                    out rejectionCode)) return false;
            foreach (var formation in selectedFormations) formation.IssueFocus(target);
            SetOrderFeedback($"Focus worker - {selectedFormations.Count} formation(s)");
            CompleteFocusCommand(target.transform.position, out rejectionCode);
            return true;
        }

        public bool TryIssueFocusCommand(ICombatStructure target, out string rejectionCode)
        {
            var component = target?.TargetComponent;
            if (!TryValidateFocusTarget(component, target != null && target.IsAttackable && !target.IsFriendly,
                    out rejectionCode)) return false;
            foreach (var formation in selectedFormations) formation.IssueFocus(target);
            SetOrderFeedback($"Focus structure - {selectedFormations.Count} formation(s)");
            CompleteFocusCommand(component.transform.position, out rejectionCode);
            return true;
        }

        private bool TryValidateFormationOrder(Vector3 destination, out string rejectionCode)
        {
            if (Outcome != MatchOutcome.InProgress)
                return RejectCommand("match_complete", "The match is complete", out rejectionCode);
            var live = selectedFormations.Where(formation => formation != null && formation.MemberCount > 0)
                .ToList();
            if (live.Count == 0)
                return RejectCommand("no_selection", "Select a formation first", out rejectionCode);
            if (!IsFinite(destination) || destination.x < FogOfWarSystem.MinX ||
                destination.x > FogOfWarSystem.MaxX || destination.z < FogOfWarSystem.MinZ ||
                destination.z > FogOfWarSystem.MaxZ)
                return RejectCommand("invalid_position", "Order target is outside the battlefield", out rejectionCode);
            var center = Vector3.zero;
            foreach (var formation in live) center += formation.transform.position;
            center /= live.Count;
            foreach (var formation in live)
            {
                var offset = formation.transform.position - center;
                if (!formation.CanReach(destination + new Vector3(offset.x, 0f, offset.z)))
                    return RejectCommand("unreachable", "Every selected formation must reach its position",
                        out rejectionCode);
            }
            rejectionCode = null;
            return true;
        }

        private bool TryValidateFocusTarget(Component target, bool validHostile, out string rejectionCode)
        {
            if (Outcome != MatchOutcome.InProgress)
                return RejectCommand("match_complete", "The match is complete", out rejectionCode);
            if (selectedFormations.All(formation => formation == null || formation.MemberCount == 0))
                return RejectCommand("no_selection", "Select a formation first", out rejectionCode);
            if (target == null || !validHostile)
                return RejectCommand("unknown_target", "Combat target is unavailable", out rejectionCode);
            if (!IsCurrentlyVisible(target.transform.position))
                return RejectCommand("target_not_visible", "Combat target must be currently visible",
                    out rejectionCode);
            rejectionCode = null;
            return true;
        }

        private void CompleteFocusCommand(Vector3 position, out string rejectionCode)
        {
            PlayCue(GameplayCue.Order);
            CreateOrderMarker(position, new Color(1f, 0.22f, 0.1f));
            rejectionCode = null;
        }

        public bool TryIssueGatherCommand(ResourceCache cache, out string rejectionCode)
        {
            if (Outcome != MatchOutcome.InProgress)
                return RejectCommand("match_complete", "The match is complete", out rejectionCode);
            if (cache == null)
                return RejectCommand("unknown_target", "Supply cache is unavailable", out rejectionCode);
            if (selectedWorkers.Count == 0)
                return RejectCommand("no_selection", "Select a Worker first", out rejectionCode);
            if (!IsCurrentlyVisible(cache.transform.position))
                return RejectCommand("target_not_visible", "Supply cache must be currently visible",
                    out rejectionCode);
            var available = selectedWorkers.Where(worker => worker.CurrentConstruction == null).ToList();
            if (available.Count == 0)
                return RejectCommand("actors_busy", "Cancel construction before issuing another order",
                    out rejectionCode);
            if (available.Any(worker => !worker.CanReachGatherPoint(cache)))
                return RejectCommand("unreachable", "Every selected Worker must reach its Supply cache position",
                    out rejectionCode);

            IssueGatherForSelected(cache, available);
            rejectionCode = null;
            return true;
        }

        public bool TryIssueBuildCommand(BuildingType type, Vector3 position, out string rejectionCode)
        {
            if (Outcome != MatchOutcome.InProgress)
                return RejectCommand("match_complete", "The match is complete", out rejectionCode);
            if (selectedWorkers.Count == 0)
                return RejectCommand("no_selection", "Select a Worker first", out rejectionCode);
            if (type is not BuildingType.House and not BuildingType.Storehouse and not BuildingType.Watchtower)
                return RejectCommand("unsupported_building", "Building type is unavailable", out rejectionCode);
            var worker = selectedWorkers.FirstOrDefault(candidate => candidate.CurrentConstruction == null);
            if (worker == null)
                return RejectCommand("actors_busy", "Selected Workers are already constructing", out rejectionCode);
            return TryPlaceBuilding(worker, type, position, out rejectionCode);
        }

        public bool TryIssueTrainCommand(FormationType type, out string rejectionCode)
        {
            if (Outcome != MatchOutcome.InProgress)
                return RejectCommand("match_complete", "The match is complete", out rejectionCode);
            if (type is not FormationType.Spearmen and not FormationType.Archers and not FormationType.Cavalry)
                return RejectCommand("unsupported_formation", "Formation type is unavailable", out rejectionCode);
            if (!hisarSelected)
                return RejectCommand("no_selection", "Select the Hisar before training", out rejectionCode);
            if (Supplies < tuning.formationCost)
                return RejectCommand("insufficient_supplies", $"Need {tuning.formationCost} Supplies",
                    out rejectionCode);
            if (PopulationCapacity - PopulationUsed < tuning.formationPopulation)
                return RejectCommand("population_blocked",
                    $"Population blocked - need {tuning.formationPopulation} free", out rejectionCode);
            if (!TryQueueFormation(type))
                return RejectCommand("training_rejected", "Formation could not be queued", out rejectionCode);

            rejectionCode = null;
            return true;
        }

        public bool TrySelectHisarForCommand(out string rejectionCode)
        {
            if (Outcome != MatchOutcome.InProgress)
                return RejectCommand("match_complete", "The match is complete", out rejectionCode);
            SelectHisar();
            rejectionCode = null;
            return true;
        }

        public bool TrySelectBuildingForCommand(ConstructibleBuilding building, out string rejectionCode)
        {
            if (Outcome != MatchOutcome.InProgress)
                return RejectCommand("match_complete", "The match is complete", out rejectionCode);
            if (building == null || building.IsDestroyed || !buildings.Contains(building))
                return RejectCommand("unknown_target", "Building is unavailable", out rejectionCode);
            if (!building.IsComplete)
                return RejectCommand("building_incomplete", "Only completed buildings can be selected",
                    out rejectionCode);
            SelectOnly(building);
            rejectionCode = null;
            return true;
        }

        public bool TryIssueTrainWorkerCommand(out string rejectionCode)
        {
            if (Outcome != MatchOutcome.InProgress)
                return RejectCommand("match_complete", "The match is complete", out rejectionCode);
            if (!hisarSelected)
                return RejectCommand("no_selection", "Select the Hisar before training", out rejectionCode);
            if (Supplies < tuning.workerCost)
                return RejectCommand("insufficient_supplies", $"Need {tuning.workerCost} Supplies",
                    out rejectionCode);
            if (PopulationCapacity - PopulationUsed < 1)
                return RejectCommand("population_blocked", "Population blocked - need 1 free", out rejectionCode);
            if (!TryQueueWorker())
                return RejectCommand("training_rejected", "Worker could not be queued", out rejectionCode);
            rejectionCode = null;
            return true;
        }

        public bool TryIssueCancelProductionCommand(out string rejectionCode)
        {
            if (Outcome != MatchOutcome.InProgress)
                return RejectCommand("match_complete", "The match is complete", out rejectionCode);
            if (!hisarSelected)
                return RejectCommand("no_selection", "Select the Hisar before cancelling training",
                    out rejectionCode);
            if (productionQueue.Active == null)
                return RejectCommand("queue_empty", "There is no active training to cancel", out rejectionCode);
            CancelActiveTraining();
            rejectionCode = null;
            return true;
        }

        public bool TryIssueCancelConstructionCommand(ConstructibleBuilding building, out string rejectionCode)
        {
            if (Outcome != MatchOutcome.InProgress)
                return RejectCommand("match_complete", "The match is complete", out rejectionCode);
            if (building == null || building.IsDestroyed || !buildings.Contains(building))
                return RejectCommand("unknown_target", "Construction is unavailable", out rejectionCode);
            if (building.IsComplete)
                return RejectCommand("building_complete", "Completed buildings cannot be cancelled",
                    out rejectionCode);
            var builder = workers.FirstOrDefault(worker => ReferenceEquals(worker.CurrentConstruction, building));
            if (builder == null)
                return RejectCommand("no_builder", "Construction has no assigned Worker", out rejectionCode);
            if (!selectedWorkers.Contains(builder))
                return RejectCommand("no_selection", "Select the assigned Worker before cancelling construction",
                    out rejectionCode);
            if (!CancelConstruction(builder))
                return RejectCommand("cancellation_rejected", "Construction could not be cancelled",
                    out rejectionCode);
            rejectionCode = null;
            return true;
        }

        public bool TryIssueRequestDemolitionCommand(ConstructibleBuilding building, out string rejectionCode)
        {
            if (!TryValidateDemolitionTarget(building, out rejectionCode)) return false;
            if (selectedBuilding != building)
                return RejectCommand("no_selection", "Select this building before requesting demolition",
                    out rejectionCode);
            if (demolitionCandidate == building)
            {
                rejectionCode = null;
                return true;
            }
            RequestDemolition();
            rejectionCode = null;
            return true;
        }

        public bool TryIssueConfirmDemolitionCommand(ConstructibleBuilding building, out string rejectionCode)
        {
            if (!TryValidateDemolitionTarget(building, out rejectionCode)) return false;
            if (selectedBuilding != building || demolitionCandidate != building)
                return RejectCommand("confirmation_required", "Request demolition before confirming",
                    out rejectionCode);
            if (!RequestDemolition())
                return RejectCommand("demolition_rejected", "Building could not be demolished", out rejectionCode);
            rejectionCode = null;
            return true;
        }

        public bool TryIssueSetRallyCommand(Vector3 position, ResourceCache cache, out string rejectionCode)
        {
            if (Outcome != MatchOutcome.InProgress)
                return RejectCommand("match_complete", "The match is complete", out rejectionCode);
            if (!hisarSelected)
                return RejectCommand("no_selection", "Select the Hisar before setting its rally",
                    out rejectionCode);
            if (!IsFinite(position) || position.x < FogOfWarSystem.MinX || position.x > FogOfWarSystem.MaxX ||
                position.z < FogOfWarSystem.MinZ || position.z > FogOfWarSystem.MaxZ)
                return RejectCommand("invalid_position", "Rally target is outside the battlefield",
                    out rejectionCode);
            if (cache != null)
            {
                if (!allCaches.Contains(cache))
                    return RejectCommand("unknown_target", "Supply cache is unavailable", out rejectionCode);
                if (cache.Remaining <= 0)
                    return RejectCommand("cache_exhausted", "Supply cache is exhausted", out rejectionCode);
                if (!IsCurrentlyVisible(cache.transform.position))
                    return RejectCommand("target_not_visible", "Rally cache must be currently visible",
                        out rejectionCode);
            }
            if (!TrySetHisarRally(position, cache))
                return RejectCommand("rally_rejected", "Rally target is unavailable", out rejectionCode);
            rejectionCode = null;
            return true;
        }

        private bool TryValidateDemolitionTarget(ConstructibleBuilding building, out string rejectionCode)
        {
            if (Outcome != MatchOutcome.InProgress)
                return RejectCommand("match_complete", "The match is complete", out rejectionCode);
            if (building == null || building.IsDestroyed || !buildings.Contains(building))
                return RejectCommand("unknown_target", "Building is unavailable", out rejectionCode);
            if (!building.IsComplete)
                return RejectCommand("building_incomplete", "Only completed buildings can be demolished",
                    out rejectionCode);
            rejectionCode = null;
            return true;
        }

        private void IssueGatherForSelected(ResourceCache cache, IEnumerable<WorkerAgent> availableWorkers)
        {
            foreach (var worker in availableWorkers) worker.IssueGather(cache);
            SetOrderFeedback($"Gather {cache.name}");
            PlayCue(GameplayCue.Order);
            CreateOrderMarker(cache.transform.position, new Color(0.95f, 0.68f, 0.2f));
        }

        private bool RejectCommand(string code, string feedback, out string rejectionCode)
        {
            rejectionCode = code;
            SetOrderFeedback(feedback);
            return false;
        }

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    }
}
