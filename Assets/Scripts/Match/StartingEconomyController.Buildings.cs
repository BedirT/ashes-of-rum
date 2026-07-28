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
        private void CancelSelectedConstruction()
        {
            var worker = selectedWorkers.FirstOrDefault(item => item.CurrentConstruction != null);
            if (worker == null)
            {
                SetOrderFeedback("No unfinished construction selected");
                return;
            }
            CancelConstruction(worker);
        }

        private bool CanPlaceBuilding(WorkerAgent worker, Vector3 position, out string reason)
        {
            if (worker.CurrentConstruction != null)
            {
                reason = "Invalid - worker is already constructing";
                return false;
            }
            if (!HousePlacementRules.IsInsidePlayableBounds(position))
            {
                reason = "Invalid - outside buildable ground";
                return false;
            }
            if (!IsCurrentlyVisible(position))
            {
                reason = "Invalid - terrain is not currently visible";
                return false;
            }
            var overlaps = Physics.OverlapBox(position + Vector3.up, new Vector3(2f, 0.9f, 2f));
            if (overlaps.Any(item => item.gameObject.name != "Bootstrap Ground"))
            {
                reason = "Invalid - position occupied";
                return false;
            }
            if (!worker.CanReach(position + Vector3.back * 2.4f))
            {
                reason = "Invalid - worker cannot reach";
                return false;
            }
            if (!PreservesNavMeshRoute(position))
            {
                reason = "Invalid - must preserve a route";
                return false;
            }
            reason = null;
            return true;
        }

        private bool IsCurrentlyVisible(Vector3 position) =>
            fogOfWar == null || fogOfWar.StateAt(position) == FogState.Visible;

        private bool PreservesNavMeshRoute(Vector3 candidatePosition)
        {
            if (candidatePosition == lastRouteCandidate && buildingRouteVersion == lastRouteVersion)
                return lastRouteResult;

            var candidate = new GameObject("Building Route Validation");
            candidate.transform.position = candidatePosition;
            var collider = candidate.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 1f, 0f);
            collider.size = new Vector3(4f, 2f, 4f);

            var ignoredColliders = workers.SelectMany(worker => worker.GetComponentsInChildren<Collider>())
                .Concat(Caches.SelectMany(cache => cache.GetComponentsInChildren<Collider>()))
                .Where(item => item.enabled)
                .ToList();
            foreach (var ignoredCollider in ignoredColliders) ignoredCollider.enabled = false;

            try
            {
                navMeshSurface.BuildNavMesh();
                var path = new NavMeshPath();
                lastRouteResult = TrySampleRouteEndpoint(RouteStart, out var start) &&
                                  TrySampleRouteEndpoint(RouteEnd, out var end) &&
                                  NavMesh.CalculatePath(start, end, NavMesh.AllAreas, path) &&
                                  path.status == NavMeshPathStatus.PathComplete;
                lastRouteCandidate = candidatePosition;
                lastRouteVersion = buildingRouteVersion;
                return lastRouteResult;
            }
            finally
            {
                DestroyImmediate(candidate);
                navMeshSurface.BuildNavMesh();
                foreach (var ignoredCollider in ignoredColliders) ignoredCollider.enabled = true;
            }
        }

        private static bool TrySampleRouteEndpoint(Vector3 position, out Vector3 sampledPosition)
        {
            if (NavMesh.SamplePosition(position, out var hit, 3f, NavMesh.AllAreas))
            {
                sampledPosition = hit.position;
                return true;
            }
            sampledPosition = default;
            return false;
        }

        private ConstructibleBuilding CreateBuilding(BuildingType type, Vector3 position)
        {
            var count = type == BuildingType.House ? houses.Count :
                type == BuildingType.Storehouse ? storehouses.Count : watchtowers.Count;
            var root = CreateBuildingVisual(type, $"{type} {count + 1}", position);
            var building = AddBuildingComponents(root, type);
            var completeColor = type switch
            {
                BuildingType.House => new Color(0.12f, 0.38f, 0.82f),
                BuildingType.Storehouse => new Color(0.16f, 0.46f, 0.7f),
                _ => new Color(0.08f, 0.32f, 0.66f)
            };
            building.Initialize(type, BuildingDuration(type), tuning.buildingHealth, completeColor,
                DestroyFriendlyBuilding, true, HandleFriendlyUnderAttack);
            AttachHealthBar(root, () => building.Health, () => building.MaxHealth,
                type == BuildingType.Watchtower ? 4.4f : 2.9f, true);
            fogOfWar?.RegisterFriendly(root.transform);
            if (type == BuildingType.Watchtower)
                root.AddComponent<WatchtowerAttack>().Initialize(tuning,
                    () => enemyFormations.Where(formation => fogOfWar == null || fogOfWar.IsCurrentlyVisible(formation)),
                    position => PlayWorldCue(GameplayCue.Attack, position, true));
            return building;
        }

        private static ConstructibleBuilding AddBuildingComponents(GameObject root, BuildingType type)
        {
            var obstacle = root.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = new Vector3(0f, 1f, 0f);
            obstacle.size = new Vector3(4f, 2f, 4f);
            obstacle.carving = true;
            return type == BuildingType.House
                ? root.AddComponent<HouseBuilding>()
                : root.AddComponent<ConstructibleBuilding>();
        }

        private static GameObject CreateBuildingVisual(BuildingType type, string name, Vector3 position,
            bool friendly = true)
        {
            var root = new GameObject(name);
            root.transform.position = position;
            var wallColor = friendly ? new Color(0.42f, 0.55f, 0.68f) : new Color(0.68f, 0.24f, 0.12f);
            var roofColor = friendly ? new Color(0.16f, 0.28f, 0.48f) : new Color(0.48f, 0.08f, 0.04f);
            if (type == BuildingType.House)
            {
                CreatePrimitive(PrimitiveType.Cube, "House Walls", root.transform,
                    new Vector3(0f, 0.8f, 0f), new Vector3(3.6f, 1.6f, 3.6f), wallColor);
                var roof = CreatePrimitive(PrimitiveType.Cylinder, "House Roof", root.transform,
                    new Vector3(0f, 1.9f, 0f), new Vector3(2.4f, 0.45f, 2.4f), roofColor);
                roof.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            }
            else if (type == BuildingType.Storehouse)
            {
                CreatePrimitive(PrimitiveType.Cube, "Storehouse Walls", root.transform,
                    new Vector3(0f, 0.75f, 0f), new Vector3(3.8f, 1.5f, 3.8f), wallColor);
                for (var i = -1; i <= 1; i++)
                    CreatePrimitive(PrimitiveType.Cube, $"Stored Supply {i + 2}", root.transform,
                        new Vector3(i * 0.8f, 1.75f, 0f), new Vector3(0.65f, 0.65f, 0.65f),
                        new Color(0.75f, 0.52f, 0.22f));
            }
            else
            {
                CreatePrimitive(PrimitiveType.Cylinder, "Watchtower Base", root.transform,
                    new Vector3(0f, 1.6f, 0f), new Vector3(1.5f, 1.6f, 1.5f), wallColor);
                CreatePrimitive(PrimitiveType.Cube, "Watchtower Platform", root.transform,
                    new Vector3(0f, 3.35f, 0f), new Vector3(3.2f, 0.5f, 3.2f), roofColor);
            }
            var ring = CreatePrimitive(PrimitiveType.Cylinder, "Building Selection Ring", root.transform,
                new Vector3(0f, 0.04f, 0f), new Vector3(4.4f, 0.025f, 4.4f),
                friendly ? new Color(0.2f, 0.78f, 1f) : new Color(1f, 0.35f, 0.1f));
            Destroy(ring.GetComponent<Collider>());
            ring.AddComponent<BuildingSelectionRing>();
            ring.SetActive(false);
            return root;
        }

        private void CompleteBuilding(ConstructibleBuilding building)
        {
            telemetry.RecordBuildingConstructed(true, building.Type.ToString(), MatchElapsedSeconds);
            PlayCue(GameplayCue.Construction);
            if (building.Type == BuildingType.House)
            {
                population.AddCapacity(tuning.housePopulationCapacity);
                SetOrderFeedback($"House complete - population cap {PopulationCapacity}");
            }
            else if (building.Type == BuildingType.Storehouse)
                SetOrderFeedback("Storehouse complete - Supply drop-off active");
            else
                SetOrderFeedback("Watchtower complete - guarding nearby ground");
        }

        private Vector3 FindNearestDropOff(Vector3 position)
        {
            var result = hisar.DropOffPoint;
            var nearestDistance = (result - position).sqrMagnitude;
            foreach (var storehouse in storehouses)
            {
                if (storehouse == null || !storehouse.IsComplete || storehouse.IsDestroyed) continue;
                var distance = (storehouse.DropOffPoint - position).sqrMagnitude;
                if (distance >= nearestDistance) continue;
                result = storehouse.DropOffPoint;
                nearestDistance = distance;
            }
            return result;
        }

        private Vector3 FindNearestEnemyDropOff(Vector3 position)
        {
            var result = enemyHisar.DropOffPoint;
            var nearestDistance = (result - position).sqrMagnitude;
            foreach (var storehouse in enemyBuildings)
            {
                if (storehouse == null || storehouse.Type != BuildingType.Storehouse ||
                    !storehouse.IsComplete || storehouse.IsDestroyed) continue;
                var distance = (storehouse.DropOffPoint - position).sqrMagnitude;
                if (distance >= nearestDistance) continue;
                result = storehouse.DropOffPoint;
                nearestDistance = distance;
            }
            return result;
        }

        private int BuildingCost(BuildingType type) => type switch
        {
            BuildingType.House => tuning.houseCost,
            BuildingType.Storehouse => tuning.storehouseCost,
            _ => tuning.watchtowerCost
        };

        private float BuildingDuration(BuildingType type) => type switch
        {
            BuildingType.House => tuning.houseBuildSeconds,
            BuildingType.Storehouse => tuning.storehouseBuildSeconds,
            _ => tuning.watchtowerBuildSeconds
        };

        private void DestroyFriendlyBuilding(ConstructibleBuilding building)
        {
            if (building == null) return;
            var type = building.Type;
            if (!building.IsComplete)
                foreach (var worker in workers.Where(worker => worker != null &&
                             ReferenceEquals(worker.CurrentConstruction, building)))
                    worker.CancelConstruction();
            if (building.IsComplete && type == BuildingType.House)
                population.RemoveCapacity(tuning.housePopulationCapacity);
            RemoveBuildingFromLists(building);
            if (selectedBuilding == building) selectedBuilding = null;
            demolitionCandidate = null;
            Destroy(building.gameObject, 0.25f);
            telemetry.RecordBuildingDestroyed(true, type.ToString(), MatchElapsedSeconds);
            SetOrderFeedback($"{type} {(building.WasDemolished ? "demolished" : "destroyed")} - no refund");
        }

        private void RemoveBuildingFromLists(ConstructibleBuilding building)
        {
            if (buildings.Remove(building)) buildingRouteVersion++;
            if (building is HouseBuilding house) houses.Remove(house);
            storehouses.Remove(building);
            watchtowers.Remove(building);
        }

    }
}
