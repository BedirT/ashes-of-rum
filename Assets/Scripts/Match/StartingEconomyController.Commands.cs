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
