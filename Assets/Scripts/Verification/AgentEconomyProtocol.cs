using System;
using UnityEngine;

namespace AshesOfRum
{
    [Serializable]
    public sealed class AgentHisarState
    {
        public string id;
        public int health;
        public int maxHealth;
        public bool selected;
        public bool hasRally;
        public AgentVector3 rallyPosition;
        public string rallyCacheId;
    }

    public sealed partial class AgentStateProjector
    {
        private AgentHisarState ProjectHisar()
        {
            var rally = economy.HisarRallyPoint;
            var rallyCache = economy.HisarRallyCache;
            var visibleRallyCacheId = rallyCache != null && rallyCache.Remaining > 0 &&
                                      economy.FogOfWar.StateAt(rallyCache.transform.position) == FogState.Visible &&
                                      cacheIds.TryGetValue(rallyCache, out var cacheId)
                ? cacheId
                : null;
            return new AgentHisarState
            {
                id = "hisar",
                health = economy.FriendlyHisar.Health,
                maxHealth = economy.FriendlyHisar.MaxHealth,
                selected = economy.FriendlyHisar.IsSelected,
                hasRally = rally.HasValue,
                rallyPosition = rally.HasValue ? AgentVector3.From(rally.Value) : null,
                rallyCacheId = visibleRallyCacheId
            };
        }
    }

    public sealed partial class AgentCommandExecutor
    {
        public static bool TryParseBuildingType(string value, out BuildingType type)
        {
            switch (value)
            {
                case nameof(BuildingType.House): type = BuildingType.House; return true;
                case nameof(BuildingType.Storehouse): type = BuildingType.Storehouse; return true;
                case nameof(BuildingType.Watchtower): type = BuildingType.Watchtower; return true;
                default: type = default; return false;
            }
        }

        public static bool TryParseProductionItem(string value, out ProductionItem item)
        {
            switch (value)
            {
                case nameof(ProductionItem.Worker): item = ProductionItem.Worker; return true;
                case nameof(ProductionItem.Spearmen): item = ProductionItem.Spearmen; return true;
                case nameof(ProductionItem.Archers): item = ProductionItem.Archers; return true;
                case nameof(ProductionItem.Cavalry): item = ProductionItem.Cavalry; return true;
                default: item = default; return false;
            }
        }

        public static bool TryParseFormationType(string value, out FormationType type)
        {
            switch (value)
            {
                case nameof(FormationType.Spearmen): type = FormationType.Spearmen; return true;
                case nameof(FormationType.Archers): type = FormationType.Archers; return true;
                case nameof(FormationType.Cavalry): type = FormationType.Cavalry; return true;
                default: type = default; return false;
            }
        }

        private bool ExecuteBuild(AgentScriptStep step, out string rejectionCode)
        {
            if (!TryParseBuildingType(step.buildingType, out var type))
            {
                rejectionCode = "unsupported_building";
                return false;
            }
            return economy.TryIssueBuildCommand(type, new Vector3(step.x, 0f, step.z), out rejectionCode);
        }

        private bool ExecuteTrain(AgentScriptStep step, out string rejectionCode)
        {
            if (!TryParseProductionItem(step.formationType, out var item))
            {
                rejectionCode = "unsupported_production";
                return false;
            }
            return item == ProductionItem.Worker
                ? economy.TryIssueTrainWorkerCommand(out rejectionCode)
                : economy.TryIssueTrainCommand(item.ToFormationType(), out rejectionCode);
        }

        private bool ExecuteCancelConstruction(AgentScriptStep step, out string rejectionCode)
        {
            if (!projector.TryResolveBuilding(step.targetId, out var building))
            {
                rejectionCode = "unknown_target";
                return false;
            }
            return economy.TryIssueCancelConstructionCommand(building, out rejectionCode);
        }

        private bool ExecuteDemolition(AgentScriptStep step, bool confirm, out string rejectionCode)
        {
            if (!projector.TryResolveBuilding(step.targetId, out var building))
            {
                rejectionCode = "unknown_target";
                return false;
            }
            return confirm
                ? economy.TryIssueConfirmDemolitionCommand(building, out rejectionCode)
                : economy.TryIssueRequestDemolitionCommand(building, out rejectionCode);
        }

        private bool ExecuteSetRally(AgentScriptStep step, out string rejectionCode)
        {
            if (string.IsNullOrWhiteSpace(step.targetId))
                return economy.TryIssueSetRallyCommand(new Vector3(step.x, 0f, step.z), null,
                    out rejectionCode);
            if (!projector.TryResolveCache(step.targetId, out var cache))
            {
                rejectionCode = "unknown_target";
                return false;
            }
            return economy.TryIssueSetRallyCommand(cache.transform.position, cache, out rejectionCode);
        }
    }
}
