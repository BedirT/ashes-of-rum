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
            if (script.steps[^1].action is not ("end_session" or "quit"))
                throw new InvalidOperationException("Agent script must end with an end_session or result-gated quit step.");
            var checkpoints = new HashSet<string>(StringComparer.Ordinal);
            foreach (var step in script.steps.Where(item => item.action == "capture"))
                if (string.IsNullOrWhiteSpace(step.checkpoint) || !checkpoints.Add(step.checkpoint))
                    throw new InvalidOperationException("Every capture needs a unique checkpoint name.");
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
        public string formationType;
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
        public bool selected;
        public bool complete;
        public int health;
        public int maxHealth;
        public float progress;
        public string[] assignedBuilderIds;
        public AgentVector3 position;
    }

    [Serializable]
    public sealed class AgentProductionState
    {
        public int count;
        public string activeItem;
        public float progress;
    }

    [Serializable]
    public sealed class AgentFormationState
    {
        public string id;
        public string type;
        public bool selected;
        public int memberCount;
        public int totalHealth;
        public int maxHealth;
        public string order;
        public string targetId;
        public bool hasDestination;
        public AgentVector3 destination;
        public float facingDegrees;
        public AgentVector3 position;
    }

    [Serializable]
    public sealed class AgentHostileFormationState
    {
        public string id;
        public string type;
        public int memberCount;
        public int totalHealth;
        public int maxHealth;
        public string order;
        public AgentVector3 position;
    }

    [Serializable]
    public sealed class AgentHostileWorkerState
    {
        public string id;
        public bool alive;
        public int health;
        public int maxHealth;
        public string activity;
        public AgentVector3 position;
    }

    [Serializable]
    public sealed class AgentHostileStructureState
    {
        public string id;
        public string type;
        public bool complete;
        public int health;
        public int maxHealth;
        public string fogState;
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
        public AgentHisarState hisar;
        public AgentProductionState production;
        public AgentFormationState[] formations;
        public AgentHostileFormationState[] visibleHostileFormations;
        public AgentHostileWorkerState[] visibleHostileWorkers;
        public AgentHostileStructureState[] visibleHostileStructures;
        public AgentMapState map;
        public AgentCameraState camera;
        public string stateHash;
    }

    [Serializable]
    public sealed class AgentProtocolResponse
    {
        public int schemaVersion;
        public string sessionId;
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

    public sealed partial class AgentStateProjector
    {
        private sealed class HostileStructureMemory
        {
            public Component component;
            public AgentHostileStructureState state;
        }

        private readonly StartingEconomyController economy;
        private readonly string buildSha;
        private readonly Dictionary<WorkerAgent, string> workerIds = new();
        private readonly Dictionary<ResourceCache, string> cacheIds = new();
        private readonly Dictionary<ConstructibleBuilding, string> buildingIds = new();
        private readonly Dictionary<FormationAgent, string> formationIds = new();
        private readonly Dictionary<FormationAgent, string> hostileFormationIds = new();
        private readonly Dictionary<WorkerAgent, string> hostileWorkerIds = new();
        private readonly Dictionary<Component, string> hostileStructureIds = new();
        private readonly Dictionary<string, HostileStructureMemory> hostileStructureMemory = new();
        private int nextWorkerId = 1;
        private int nextCacheId = 1;
        private int nextBuildingId = 1;
        private int nextFormationId = 1;
        private int nextHostileFormationId = 1;
        private int nextHostileWorkerId = 1;
        private int nextHostileStructureId = 1;

        public AgentStateProjector(StartingEconomyController economyController, string verifiedBuildSha = "test")
        {
            economy = economyController ?? throw new ArgumentNullException(nameof(economyController));
            buildSha = verifiedBuildSha;
            SynchronizeIds();
        }

        public AgentMatchState Project(int sequence)
        {
            economy.FogOfWar.RefreshNow();
            SynchronizeIds();
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
                hisar = ProjectHisar(),
                production = new AgentProductionState
                {
                    count = economy.ProductionQueueCount,
                    activeItem = economy.ActiveProductionItem?.ToString(),
                    progress = Mathf.Round(economy.ProductionQueueProgress * 1000f) / 1000f
                },
                formations = economy.FriendlyFormations.Where(formation => formation != null &&
                        formation.MemberCount > 0)
                    .OrderBy(formation => formationIds[formation], StringComparer.Ordinal)
                    .Select(ProjectFormation).ToArray(),
                visibleHostileFormations = economy.EnemyFormations.Where(IsCurrentlyVisible)
                    .OrderBy(formation => hostileFormationIds[formation], StringComparer.Ordinal)
                    .Select(ProjectHostileFormation).ToArray(),
                visibleHostileWorkers = economy.EnemyWorkers.Where(IsCurrentlyVisible)
                    .OrderBy(worker => hostileWorkerIds[worker], StringComparer.Ordinal)
                    .Select(ProjectHostileWorker).ToArray(),
                visibleHostileStructures = ProjectKnownHostileStructures(),
                map = ProjectMap(),
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

        public bool IsKnownBuildingObjectDestroyed(string id)
        {
            SynchronizeIds();
            var known = buildingIds.FirstOrDefault(pair => pair.Value == id);
            return string.Equals(known.Value, id, StringComparison.Ordinal) && known.Key == null;
        }

        public bool TryResolveFormation(string id, out FormationAgent formation)
        {
            SynchronizeIds();
            formation = formationIds.FirstOrDefault(pair => pair.Value == id).Key;
            return formation != null && formation.MemberCount > 0 &&
                   economy.FriendlyFormations.Contains(formation);
        }

        public bool TryResolveVisibleHostileFormation(string id, out FormationAgent formation)
        {
            SynchronizeIds();
            formation = hostileFormationIds.FirstOrDefault(pair => pair.Value == id).Key;
            return IsCurrentlyVisible(formation);
        }

        public bool TryResolveVisibleHostileWorker(string id, out WorkerAgent worker)
        {
            SynchronizeIds();
            worker = hostileWorkerIds.FirstOrDefault(pair => pair.Value == id).Key;
            return IsCurrentlyVisible(worker);
        }

        public bool IsHostileWorkerDamagedInCurrentVision(string id)
        {
            SynchronizeIds();
            var worker = hostileWorkerIds.FirstOrDefault(pair => pair.Value == id).Key;
            return worker != null && worker.Health < worker.MaxHealth &&
                   economy.FogOfWar.StateAt(worker.transform.position) == FogState.Visible;
        }

        public bool IsVisibleHostileWorkerInFocusRange(string id)
        {
            SynchronizeIds();
            var worker = hostileWorkerIds.FirstOrDefault(pair => pair.Value == id).Key;
            if (!IsCurrentlyVisible(worker) || worker.CurrentActivity != WorkerAgent.Activity.Gathering)
                return false;
            return economy.SelectedFormations.Any(formation => formation != null && formation.MemberCount > 0 &&
                (formation.transform.position - worker.transform.position).sqrMagnitude <= 16f);
        }

        public bool TryResolveVisibleHostileStructure(string id, out ICombatStructure structure)
        {
            SynchronizeIds();
            var component = hostileStructureIds.FirstOrDefault(pair => pair.Value == id).Key;
            structure = component as ICombatStructure;
            return structure != null && IsCurrentlyVisible(structure);
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
            selected = building.IsSelected,
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

        private AgentFormationState ProjectFormation(FormationAgent formation) => new()
        {
            id = formationIds[formation],
            type = formation.Type.ToString(),
            selected = formation.IsSelected,
            memberCount = formation.MemberCount,
            totalHealth = formation.TotalMemberHealth,
            maxHealth = formation.MaximumMemberHealth,
            order = formation.CurrentOrder.ToString(),
            targetId = ResolveTargetId(formation),
            hasDestination = formation.HasDestination,
            destination = AgentVector3.From(formation.Destination),
            facingDegrees = Mathf.Round(formation.FacingDegrees * 1000f) / 1000f,
            position = AgentVector3.From(formation.transform.position)
        };

        private AgentHostileFormationState ProjectHostileFormation(FormationAgent formation) => new()
        {
            id = hostileFormationIds[formation],
            type = formation.Type.ToString(),
            memberCount = formation.MemberCount,
            totalHealth = formation.TotalMemberHealth,
            maxHealth = formation.MaximumMemberHealth,
            order = formation.CurrentOrder.ToString(),
            position = AgentVector3.From(formation.transform.position)
        };

        private AgentHostileWorkerState ProjectHostileWorker(WorkerAgent worker) => new()
        {
            id = hostileWorkerIds[worker],
            alive = worker.IsAlive,
            health = worker.Health,
            maxHealth = worker.MaxHealth,
            activity = worker.CurrentActivity.ToString(),
            position = AgentVector3.From(worker.transform.position)
        };

        private AgentHostileStructureState ProjectHostileStructure(ICombatStructure structure, FogState fogState) => new()
        {
            id = hostileStructureIds[structure.TargetComponent],
            type = structure is Hisar ? "Hisar" : ((ConstructibleBuilding)structure).Type.ToString(),
            complete = structure is Hisar || ((ConstructibleBuilding)structure).IsComplete,
            health = structure.Health,
            maxHealth = structure.MaxHealth,
            fogState = fogState.ToString(),
            position = AgentVector3.From(structure.TargetComponent.transform.position)
        };

        private AgentHostileStructureState[] ProjectKnownHostileStructures()
        {
            foreach (var structure in HostileStructures().Where(IsCurrentlyVisible))
            {
                var component = structure.TargetComponent;
                var id = hostileStructureIds[component];
                hostileStructureMemory[id] = new HostileStructureMemory
                {
                    component = component,
                    state = ProjectHostileStructure(structure, FogState.Visible)
                };
            }

            foreach (var id in hostileStructureMemory.Keys.ToArray())
            {
                var memory = hostileStructureMemory[id];
                var position = new Vector3(memory.state.position.x, memory.state.position.y, memory.state.position.z);
                if (economy.FogOfWar.StateAt(position) != FogState.Visible) continue;
                if (memory.component != null && memory.component is ICombatStructure structure &&
                    structure.IsAttackable) continue;
                hostileStructureMemory.Remove(id);
            }

            return hostileStructureMemory
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => CopyHostileStructure(pair.Value.state,
                    economy.FogOfWar.StateAt(new Vector3(pair.Value.state.position.x,
                        pair.Value.state.position.y, pair.Value.state.position.z)) == FogState.Visible
                        ? FogState.Visible
                        : FogState.Explored))
                .ToArray();
        }

        private static AgentHostileStructureState CopyHostileStructure(AgentHostileStructureState source,
            FogState fogState) => new()
        {
            id = source.id,
            type = source.type,
            complete = source.complete,
            health = source.health,
            maxHealth = source.maxHealth,
            fogState = fogState.ToString(),
            position = new AgentVector3 { x = source.position.x, y = source.position.y, z = source.position.z }
        };

        private string ResolveTargetId(FormationAgent formation)
        {
            if (formation.Target != null && hostileFormationIds.TryGetValue(formation.Target, out var formationId) &&
                IsCurrentlyVisible(formation.Target)) return formationId;
            if (formation.WorkerTarget != null && hostileWorkerIds.TryGetValue(formation.WorkerTarget, out var workerId) &&
                IsCurrentlyVisible(formation.WorkerTarget)) return workerId;
            var component = formation.StructureTarget?.TargetComponent;
            return component != null && hostileStructureIds.TryGetValue(component, out var structureId) &&
                   IsCurrentlyVisible(formation.StructureTarget) ? structureId : null;
        }

        private IEnumerable<ICombatStructure> HostileStructures()
        {
            if (economy.EnemyHisar != null && !economy.EnemyHisar.IsDestroyed) yield return economy.EnemyHisar;
            foreach (var building in economy.EnemyBuildings)
                if (building != null && !building.IsDestroyed) yield return building;
        }

        private bool IsCurrentlyVisible(FormationAgent formation) => formation != null &&
            formation.MemberCount > 0 && economy.EnemyFormations.Contains(formation) &&
            economy.FogOfWar.IsCurrentlyVisible(formation);

        private bool IsCurrentlyVisible(WorkerAgent worker) => worker != null && worker.IsAlive &&
            economy.EnemyWorkers.Contains(worker) && economy.FogOfWar.IsCurrentlyVisible(worker);

        private bool IsCurrentlyVisible(ICombatStructure structure) => structure != null && structure.IsAttackable &&
            structure.TargetComponent != null && economy.FogOfWar.IsCurrentlyVisible(structure.TargetComponent);

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
            foreach (var formation in economy.FriendlyFormations.Where(formation => formation != null &&
                         formation.MemberCount > 0).OrderBy(formation => formation.name, StringComparer.Ordinal))
                if (!formationIds.ContainsKey(formation)) formationIds.Add(formation,
                    $"formation-{nextFormationId++}");
            foreach (var formation in economy.EnemyFormations.Where(IsCurrentlyVisible)
                         .OrderBy(formation => formation.name, StringComparer.Ordinal))
                if (!hostileFormationIds.ContainsKey(formation)) hostileFormationIds.Add(formation,
                    $"hostile-formation-{nextHostileFormationId++}");
            foreach (var worker in economy.EnemyWorkers.Where(IsCurrentlyVisible)
                         .OrderBy(worker => worker.name, StringComparer.Ordinal))
                if (!hostileWorkerIds.ContainsKey(worker)) hostileWorkerIds.Add(worker,
                    $"hostile-worker-{nextHostileWorkerId++}");
            foreach (var structure in HostileStructures().Where(IsCurrentlyVisible)
                         .OrderBy(structure => structure.TargetComponent.name, StringComparer.Ordinal))
                if (!hostileStructureIds.ContainsKey(structure.TargetComponent))
                    hostileStructureIds.Add(structure.TargetComponent,
                        structure is Hisar ? "enemy-hisar" : $"hostile-structure-{nextHostileStructureId++}");
        }

        private static AgentCameraState ProjectCamera(Camera camera) => camera == null ? null : new AgentCameraState
        {
            position = AgentVector3.From(camera.transform.position),
            rotationEuler = AgentVector3.From(camera.transform.eulerAngles),
            fieldOfView = Mathf.Round(camera.fieldOfView * 1000f) / 1000f
        };
    }

    public sealed partial class AgentCommandExecutor
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
                    return SelectActors(step.actorIds, out rejectionCode);
                case "move":
                    var destination = new Vector3(step.x, 0f, step.z);
                    return economy.SelectedFormations.Count > 0
                        ? economy.TryIssueFormationMoveCommand(destination, out rejectionCode)
                        : economy.TryIssueWorkerMoveCommand(destination, out rejectionCode);
                case "attack_move":
                    return economy.TryIssueAttackMoveCommand(new Vector3(step.x, 0f, step.z), out rejectionCode);
                case "stop":
                    return economy.TryIssueStopCommand(out rejectionCode);
                case "focus":
                    if (projector.TryResolveVisibleHostileFormation(step.targetId, out var hostileFormation))
                        return economy.TryIssueFocusCommand(hostileFormation, out rejectionCode);
                    if (projector.TryResolveVisibleHostileWorker(step.targetId, out var hostileWorker))
                        return economy.TryIssueFocusCommand(hostileWorker, out rejectionCode);
                    if (projector.TryResolveVisibleHostileStructure(step.targetId, out var hostileStructure))
                        return economy.TryIssueFocusCommand(hostileStructure, out rejectionCode);
                    rejectionCode = "unknown_target";
                    return false;
                case "gather":
                    if (!projector.TryResolveCache(step.targetId, out var cache))
                    {
                        rejectionCode = "unknown_target";
                        return false;
                    }
                    return economy.TryIssueGatherCommand(cache, out rejectionCode);
                case "build":
                    return ExecuteBuild(step, out rejectionCode);
                case "train":
                    return ExecuteTrain(step, out rejectionCode);
                case "select_hisar":
                    return economy.TrySelectHisarForCommand(out rejectionCode);
                case "select_building":
                    return ExecuteSelectBuilding(step, out rejectionCode);
                case "cancel_construction":
                    return ExecuteCancelConstruction(step, out rejectionCode);
                case "cancel_production":
                    return economy.TryIssueCancelProductionCommand(out rejectionCode);
                case "request_demolition":
                    return ExecuteDemolition(step, false, out rejectionCode);
                case "confirm_demolition":
                    return ExecuteDemolition(step, true, out rejectionCode);
                case "set_rally":
                    return ExecuteSetRally(step, out rejectionCode);
                case "center_camera":
                    return ExecuteCenterCamera(step, out rejectionCode);
                default:
                    rejectionCode = "unsupported_action";
                    return false;
            }
        }

        private bool SelectActors(IEnumerable<string> actorIds, out string rejectionCode)
        {
            if (actorIds == null)
            {
                rejectionCode = "no_actors";
                return false;
            }
            var workers = new List<WorkerAgent>();
            var formations = new List<FormationAgent>();
            foreach (var id in actorIds.Distinct(StringComparer.Ordinal))
            {
                if (projector.TryResolveWorker(id, out var worker))
                {
                    workers.Add(worker);
                    continue;
                }
                if (projector.TryResolveFormation(id, out var formation))
                {
                    formations.Add(formation);
                    continue;
                }
                rejectionCode = "unknown_actor";
                return false;
            }
            if (workers.Count > 0 && formations.Count > 0)
            {
                rejectionCode = "mixed_actor_types";
                return false;
            }
            return formations.Count > 0
                ? economy.TrySelectFormationsForCommand(formations, out rejectionCode)
                : economy.TrySelectWorkersForCommand(workers, out rejectionCode);
        }
    }
}
