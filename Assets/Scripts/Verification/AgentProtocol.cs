using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace AshesOfRum
{
    public static class AgentProtocol
    {
        public const int SchemaVersion = 1;
        public const string Perspective = "player";

        public static AgentScript LoadScript(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new InvalidOperationException("Agent script does not exist.");
            var script = JsonUtility.FromJson<AgentScript>(File.ReadAllText(path));
            if (script == null) throw new InvalidOperationException("Agent script is invalid JSON.");
            if (script.schemaVersion != SchemaVersion)
                throw new InvalidOperationException($"Unsupported agent schema {script.schemaVersion}.");
            if (string.IsNullOrWhiteSpace(script.scenario))
                throw new InvalidOperationException("Agent script needs a scenario name.");
            if (script.steps == null || script.steps.Length == 0)
                throw new InvalidOperationException("Agent script needs at least one step.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var step in script.steps)
            {
                if (step == null || string.IsNullOrWhiteSpace(step.id) || string.IsNullOrWhiteSpace(step.action))
                    throw new InvalidOperationException("Every agent step needs an id and action.");
                if (!ids.Add(step.id)) throw new InvalidOperationException($"Duplicate agent step id: {step.id}.");
            }
            if (script.steps[^1].action != "quit")
                throw new InvalidOperationException("Agent script must end with a quit step.");
            return script;
        }

        public static string Sha256(string value)
        {
            using var algorithm = SHA256.Create();
            var bytes = algorithm.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
            return string.Concat(bytes.Select(item => item.ToString("x2")));
        }

        public static string Sha256File(string path)
        {
            using var algorithm = SHA256.Create();
            using var stream = File.OpenRead(path);
            return string.Concat(algorithm.ComputeHash(stream).Select(item => item.ToString("x2")));
        }
    }

    [Serializable]
    public sealed class AgentScript
    {
        public int schemaVersion;
        public string scenario;
        public AgentScriptStep[] steps;
    }

    [Serializable]
    public sealed class AgentScriptStep
    {
        public string id;
        public string action;
        public string[] actorIds;
        public string targetId;
        public string buildingType;
        public float x;
        public float z;
        public string condition;
        public int value;
        public float timeoutSeconds;
        public string checkpoint;
    }

    [Serializable]
    public sealed class AgentVector3
    {
        public float x;
        public float y;
        public float z;

        public static AgentVector3 From(Vector3 value) => new()
        {
            x = Quantize(value.x),
            y = Quantize(value.y),
            z = Quantize(value.z)
        };

        private static float Quantize(float value) => Mathf.Round(value * 1000f) / 1000f;
    }

    [Serializable]
    public sealed class AgentWorkerState
    {
        public string id;
        public string side;
        public bool selected;
        public bool alive;
        public int health;
        public int carriedSupplies;
        public string activity;
        public string targetCacheId;
        public AgentVector3 position;
    }

    [Serializable]
    public sealed class AgentCacheState
    {
        public string id;
        public int remainingSupplies;
        public string fogState;
        public AgentVector3 position;
    }

    [Serializable]
    public sealed class AgentBuildingState
    {
        public string id;
        public string type;
        public bool complete;
        public int health;
        public int maxHealth;
        public float progress;
        public string[] assignedBuilderIds;
        public AgentVector3 position;
    }

    [Serializable]
    public sealed class AgentCameraState
    {
        public AgentVector3 position;
        public AgentVector3 rotationEuler;
        public float fieldOfView;
    }

    [Serializable]
    public sealed class AgentMatchState
    {
        public int schemaVersion;
        public string buildSha;
        public string perspective;
        public int sequence;
        public int elapsedMilliseconds;
        public string outcome;
        public int supplies;
        public int startingSupplies;
        public int populationUsed;
        public int populationCapacity;
        public string economyNotification;
        public AgentWorkerState[] workers;
        public AgentCacheState[] visibleCaches;
        public AgentBuildingState[] buildings;
        public AgentCameraState camera;
        public string stateHash;
    }

    [Serializable]
    public sealed class AgentProtocolResponse
    {
        public int schemaVersion;
        public int sequence;
        public string requestId;
        public string action;
        public bool accepted;
        public string rejectionCode;
        public string checkpointStatePath;
        public string frameManifestPath;
        public string screenshotPath;
        public AgentMatchState state;
    }

    [Serializable]
    public sealed class AgentFrameManifest
    {
        public int schemaVersion;
        public string buildSha;
        public string checkpoint;
        public int sequence;
        public int elapsedMilliseconds;
        public int width;
        public int height;
        public string statePath;
        public string stateHash;
        public string screenshotPath;
        public string screenshotSha256;
        public AgentCameraState camera;
    }

    [Serializable]
    public sealed class AgentSessionResult
    {
        public int schemaVersion;
        public string buildSha;
        public bool passed;
        public string scenario;
        public int completedSteps;
        public string outputPath;
        public string[] checkpointManifests;
        public string error;
    }

    public sealed class AgentStateProjector
    {
        private readonly StartingEconomyController economy;
        private readonly string buildSha;
        private readonly Dictionary<WorkerAgent, string> workerIds = new();
        private readonly Dictionary<ResourceCache, string> cacheIds = new();
        private readonly Dictionary<ConstructibleBuilding, string> buildingIds = new();
        private int nextWorkerId = 1;
        private int nextCacheId = 1;
        private int nextBuildingId = 1;

        public AgentStateProjector(StartingEconomyController economyController, string verifiedBuildSha = "test")
        {
            economy = economyController ?? throw new ArgumentNullException(nameof(economyController));
            buildSha = verifiedBuildSha;
            SynchronizeIds();
        }

        public AgentMatchState Project(int sequence)
        {
            SynchronizeIds();
            economy.FogOfWar.RefreshNow();
            var visibleCaches = cacheIds
                .Where(pair => pair.Key != null &&
                               economy.FogOfWar.StateAt(pair.Key.transform.position) == FogState.Visible)
                .OrderBy(pair => pair.Value, StringComparer.Ordinal)
                .Select(pair => new AgentCacheState
                {
                    id = pair.Value,
                    remainingSupplies = pair.Key.Remaining,
                    fogState = FogState.Visible.ToString(),
                    position = AgentVector3.From(pair.Key.transform.position)
                }).ToArray();
            var state = new AgentMatchState
            {
                schemaVersion = AgentProtocol.SchemaVersion,
                buildSha = buildSha,
                perspective = AgentProtocol.Perspective,
                sequence = sequence,
                elapsedMilliseconds = Mathf.RoundToInt(economy.MatchElapsedSeconds * 1000f),
                outcome = economy.Outcome.ToString(),
                supplies = economy.Supplies,
                startingSupplies = economy.StartingSupplies,
                populationUsed = economy.PopulationUsed,
                populationCapacity = economy.PopulationCapacity,
                economyNotification = economy.LastEconomyNotification,
                workers = economy.Workers.Where(worker => worker != null)
                    .OrderBy(worker => workerIds[worker], StringComparer.Ordinal)
                    .Select(ProjectWorker).ToArray(),
                visibleCaches = visibleCaches,
                buildings = FriendlyBuildings()
                    .OrderBy(building => buildingIds[building], StringComparer.Ordinal)
                    .Select(ProjectBuilding).ToArray(),
                camera = ProjectCamera(Camera.main),
                stateHash = string.Empty
            };
            state.stateHash = AgentProtocol.Sha256(JsonUtility.ToJson(state));
            return state;
        }

        public bool TryResolveWorker(string id, out WorkerAgent worker)
        {
            SynchronizeIds();
            worker = workerIds.FirstOrDefault(pair => pair.Value == id).Key;
            return worker != null && economy.Workers.Contains(worker);
        }

        public bool TryResolveCache(string id, out ResourceCache cache)
        {
            SynchronizeIds();
            cache = cacheIds.FirstOrDefault(pair => pair.Value == id).Key;
            if (cache == null || economy.FogOfWar.StateAt(cache.transform.position) != FogState.Visible)
            {
                cache = null;
                return false;
            }
            return true;
        }

        public bool TryResolveBuilding(string id, out ConstructibleBuilding building)
        {
            SynchronizeIds();
            building = buildingIds.FirstOrDefault(pair => pair.Value == id).Key;
            return building != null && !building.IsDestroyed && FriendlyBuildings().Contains(building);
        }

        private AgentWorkerState ProjectWorker(WorkerAgent worker)
        {
            var targetId = worker.TargetCache != null && cacheIds.TryGetValue(worker.TargetCache, out var id) &&
                           economy.FogOfWar.StateAt(worker.TargetCache.transform.position) == FogState.Visible
                ? id
                : null;
            return new AgentWorkerState
            {
                id = workerIds[worker],
                side = "Karasungur",
                selected = worker.IsSelected,
                alive = worker.IsAlive,
                health = worker.Health,
                carriedSupplies = worker.CarriedSupplies,
                activity = worker.CurrentActivity.ToString(),
                targetCacheId = targetId,
                position = AgentVector3.From(worker.transform.position)
            };
        }

        private AgentBuildingState ProjectBuilding(ConstructibleBuilding building) => new()
        {
            id = buildingIds[building],
            type = building.Type.ToString(),
            complete = building.IsComplete,
            health = building.Health,
            maxHealth = building.MaxHealth,
            progress = Mathf.Round(building.Progress * 1000f) / 1000f,
            assignedBuilderIds = economy.Workers
                .Where(worker => worker != null && ReferenceEquals(worker.CurrentConstruction, building))
                .Select(worker => workerIds[worker])
                .OrderBy(id => id, StringComparer.Ordinal).ToArray(),
            position = AgentVector3.From(building.transform.position)
        };

        private IEnumerable<ConstructibleBuilding> FriendlyBuildings() =>
            economy.Houses.Cast<ConstructibleBuilding>()
                .Concat(economy.Storehouses)
                .Concat(economy.Watchtowers)
                .Where(building => building != null && !building.IsDestroyed)
                .Distinct();

        private void SynchronizeIds()
        {
            foreach (var worker in economy.Workers.Where(worker => worker != null)
                         .OrderBy(worker => worker.name, StringComparer.Ordinal))
                if (!workerIds.ContainsKey(worker)) workerIds.Add(worker, $"worker-{nextWorkerId++}");
            var caches = economy.Caches.Concat(economy.OpponentCaches).Where(cache => cache != null)
                .OrderBy(cache => cache.name, StringComparer.Ordinal);
            foreach (var cache in caches)
                if (!cacheIds.ContainsKey(cache)) cacheIds.Add(cache, $"cache-{nextCacheId++}");
            foreach (var building in FriendlyBuildings().OrderBy(building => building.name, StringComparer.Ordinal))
                if (!buildingIds.ContainsKey(building)) buildingIds.Add(building, $"building-{nextBuildingId++}");
        }

        private static AgentCameraState ProjectCamera(Camera camera) => camera == null ? null : new AgentCameraState
        {
            position = AgentVector3.From(camera.transform.position),
            rotationEuler = AgentVector3.From(camera.transform.eulerAngles),
            fieldOfView = Mathf.Round(camera.fieldOfView * 1000f) / 1000f
        };
    }

    public sealed class AgentCommandExecutor
    {
        private readonly StartingEconomyController economy;
        private readonly AgentStateProjector projector;

        public AgentCommandExecutor(StartingEconomyController economyController, AgentStateProjector stateProjector)
        {
            economy = economyController;
            projector = stateProjector;
        }

        public bool Execute(AgentScriptStep step, out string rejectionCode)
        {
            switch (step.action)
            {
                case "observe":
                    rejectionCode = null;
                    return true;
                case "select":
                    return SelectWorkers(step.actorIds, out rejectionCode);
                case "move":
                    return economy.TryIssueWorkerMoveCommand(new Vector3(step.x, 0f, step.z), out rejectionCode);
                case "gather":
                    if (!projector.TryResolveCache(step.targetId, out var cache))
                    {
                        rejectionCode = "unknown_target";
                        return false;
                    }
                    return economy.TryIssueGatherCommand(cache, out rejectionCode);
                case "build":
                    if (!string.Equals(step.buildingType, BuildingType.House.ToString(), StringComparison.Ordinal))
                    {
                        rejectionCode = "unsupported_building";
                        return false;
                    }
                    return economy.TryIssueBuildCommand(BuildingType.House,
                        new Vector3(step.x, 0f, step.z), out rejectionCode);
                default:
                    rejectionCode = "unsupported_action";
                    return false;
            }
        }

        private bool SelectWorkers(IEnumerable<string> actorIds, out string rejectionCode)
        {
            if (actorIds == null)
            {
                rejectionCode = "no_actors";
                return false;
            }
            var workers = new List<WorkerAgent>();
            foreach (var id in actorIds.Distinct(StringComparer.Ordinal))
            {
                if (!projector.TryResolveWorker(id, out var worker))
                {
                    rejectionCode = "unknown_actor";
                    return false;
                }
                workers.Add(worker);
            }
            return economy.TrySelectWorkersForCommand(workers, out rejectionCode);
        }
    }
}
