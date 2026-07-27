using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AshesOfRum
{
    public static class ScriptedOpponentEconomyRules
    {
        public static bool CanStartStorehouseRecovery(bool routeFailed, bool constructionInProgress,
            int supplies, int storehouseCost) => routeFailed && !constructionInProgress && supplies >= storehouseCost;
    }

    public sealed class ScriptedOpponentController : MonoBehaviour
    {
        private static readonly FormationType[] BuildOrder =
        {
            FormationType.Cavalry,
            FormationType.Spearmen,
            FormationType.Archers
        };

        private EconomyTuning tuning;
        private Hisar ownHisar;
        private Hisar playerHisar;
        private IList<WorkerAgent> workers;
        private IList<FormationAgent> formations;
        private IList<ConstructibleBuilding> buildings;
        private IReadOnlyList<ResourceCache> caches;
        private IReadOnlyList<ResourceCache> startingCaches;
        private Func<Vector3, bool> cacheVisibility;
        private Func<int, WorkerAgent> createWorker;
        private Func<FormationType, FormationAgent> createFormation;
        private Func<BuildingType, Vector3, ConstructibleBuilding> createBuilding;
        private Func<IEnumerable<FormationAgent>> playerFormationProvider;
        private Func<FormationAgent, bool> playerFormationVisibility;
        private Func<float> elapsedProvider;
        private Action<AiPhase, float> attackStarted;
        private Action<bool, string> entityProduced;
        private Action<bool, string> buildingConstructed;
        private HisarProductionQueue productionQueue;
        private int nextFormationIndex;
        private int nextWorkerSlot = StartingEconomyController.WorkerCount;
        private int completedHouses;
        private int completedStorehouses;
        private bool houseConstructionInProgress;
        private bool storehouseConstructionInProgress;
        private bool storehouseRecoveryRequested;
        private WorkerAgent recoveryWorker;
        private ResourceCache recoveryCache;
        private bool workerQueued;
        private bool started;
        private bool suspended;
        private readonly HashSet<AiPhase> dispatchedAttacks = new();

        public EconomyWallet Wallet { get; private set; }
        public PopulationLedger Population { get; private set; }
        public AiPhase Phase { get; private set; } = AiPhase.Preparing;
        public bool IsDefending { get; private set; }
        public int ProductionQueueCount => productionQueue?.Count ?? 0;
        public bool IsStorehouseRecoveryRequested => storehouseRecoveryRequested;
        public bool IsStorehouseConstructionInProgress => storehouseConstructionInProgress;
        public ResourceCache RecoveryCache => recoveryCache;

        public void Initialize(EconomyTuning economyTuning, Hisar home, Hisar hostileHisar,
            IList<WorkerAgent> workerList, IList<FormationAgent> formationList,
            IList<ConstructibleBuilding> buildingList, IReadOnlyList<ResourceCache> knownCaches,
            IReadOnlyList<ResourceCache> homeCaches,
            Func<int, WorkerAgent> workerFactory, Func<FormationType, FormationAgent> formationFactory,
            Func<BuildingType, Vector3, ConstructibleBuilding> buildingFactory,
            Func<IEnumerable<FormationAgent>> hostileFormationProvider,
            Func<FormationAgent, bool> isHostileFormationVisible, Func<Vector3, bool> isCacheVisible,
            Func<float> matchElapsedProvider,
            Action<AiPhase, float> onAttackStarted, Action<bool, string> onEntityProduced,
            Action<bool, string> onBuildingConstructed)
        {
            tuning = economyTuning;
            ownHisar = home;
            playerHisar = hostileHisar;
            workers = workerList;
            formations = formationList;
            buildings = buildingList;
            caches = knownCaches;
            startingCaches = homeCaches;
            cacheVisibility = isCacheVisible;
            createWorker = workerFactory;
            createFormation = formationFactory;
            createBuilding = buildingFactory;
            playerFormationProvider = hostileFormationProvider;
            playerFormationVisibility = isHostileFormationVisible;
            elapsedProvider = matchElapsedProvider;
            attackStarted = onAttackStarted;
            entityProduced = onEntityProduced;
            buildingConstructed = onBuildingConstructed;
            Wallet = new EconomyWallet(tuning.startingSupplies);
            Population = new PopulationLedger(StartingEconomyController.WorkerCount,
                tuning.startingPopulationCap, tuning.hardPopulationCap);
            productionQueue = new HisarProductionQueue(Wallet, Population, tuning);
        }

        public void StartEconomy()
        {
            if (started) return;
            started = true;
            for (var index = 0; index < StartingEconomyController.WorkerCount; index++)
            {
                var worker = createWorker(index);
                workers.Add(worker);
                worker.IssueGather(startingCaches[index % startingCaches.Count]);
            }
            TryStartHouse();
        }

        public void Suspend()
        {
            suspended = true;
            foreach (var formation in formations.Where(item => item != null)) formation.IssueStop();
            foreach (var worker in workers.Where(item => item != null)) worker.Suspend();
        }

        public void NotifyBuildingDestroyed(ConstructibleBuilding building)
        {
            if (building == null) return;
            if (!building.IsComplete)
            {
                NotifyConstructionAbandoned(building);
                return;
            }
            if (building.Type == BuildingType.House && completedHouses > 0)
            {
                completedHouses--;
                Population.RemoveCapacity(tuning.housePopulationCapacity);
            }
            else if (building.Type == BuildingType.Storehouse)
            {
                if (completedStorehouses > 0) completedStorehouses--;
                storehouseRecoveryRequested = true;
                recoveryCache = FindRecoveryCache(recoveryWorker);
            }
        }

        public void NotifyConstructionAbandoned(ConstructibleBuilding building)
        {
            if (building == null) return;
            if (building.Type == BuildingType.House) houseConstructionInProgress = false;
            else if (building.Type == BuildingType.Storehouse)
            {
                storehouseConstructionInProgress = false;
                storehouseRecoveryRequested = true;
            }
        }

        public void NotifyGatheringRouteFailed(WorkerAgent worker)
        {
            if (worker == null || !worker.IsAlive) return;
            recoveryWorker = worker;
            recoveryCache = FindRecoveryCache(worker);
            if (completedStorehouses > 0 && recoveryCache != null)
            {
                storehouseRecoveryRequested = false;
                worker.IssueGather(recoveryCache);
                return;
            }
            storehouseRecoveryRequested = recoveryCache != null;
        }

        private void Update()
        {
            if (!started || suspended || ownHisar == null || ownHisar.IsDestroyed) return;
            var completed = productionQueue.Advance(Time.deltaTime);
            if (completed.HasValue) CompleteProduction(completed.Value);
            MaintainEconomy();
            UpdateDefense();
            UpdatePhase();
        }

        private void MaintainEconomy()
        {
            if (!workerQueued && LivingWorkerCount < StartingEconomyController.WorkerCount)
            {
                workerQueued = productionQueue.TryEnqueueWorker();
                if (workerQueued) return;
            }

            if (storehouseRecoveryRequested)
            {
                if (TryStartStorehouse()) return;
                if (storehouseConstructionInProgress) return;
            }

            if (completedHouses == 0 || Population.Used > Population.Capacity)
            {
                TryStartHouse();
                return;
            }
            if (nextFormationIndex >= BuildOrder.Length) return;
            if (Population.Capacity - Population.Used < tuning.formationPopulation)
            {
                TryStartHouse();
                return;
            }
            if (productionQueue.TryEnqueueFormation(BuildOrder[nextFormationIndex])) nextFormationIndex++;
        }

        private int LivingWorkerCount => workers.Count(worker => worker != null && worker.IsAlive);

        private bool TryStartHouse()
        {
            if (houseConstructionInProgress || Wallet.Supplies < tuning.houseCost) return false;
            var worker = workers.FirstOrDefault(candidate => candidate != null && candidate.IsAlive &&
                                                             candidate.CurrentConstruction == null);
            if (worker == null || !Wallet.TrySpend(tuning.houseCost)) return false;
            var site = completedHouses == 0 ? new Vector3(7f, 0f, 21f) : new Vector3(-7f, 0f, 21f);
            var house = createBuilding(BuildingType.House, site);
            if (house == null)
            {
                Wallet.Refund(tuning.houseCost);
                return false;
            }
            houseConstructionInProgress = true;
            worker.IssueConstruct(house, CompleteHouse);
            return true;
        }

        private bool TryStartStorehouse()
        {
            if (!ScriptedOpponentEconomyRules.CanStartStorehouseRecovery(storehouseRecoveryRequested,
                    storehouseConstructionInProgress, Wallet.Supplies, tuning.storehouseCost)) return false;
            recoveryWorker = recoveryWorker != null && recoveryWorker.IsAlive &&
                             recoveryWorker.CurrentConstruction == null
                ? recoveryWorker
                : workers.FirstOrDefault(candidate => candidate != null && candidate.IsAlive &&
                                                      candidate.CurrentConstruction == null);
            recoveryCache = FindRecoveryCache(recoveryWorker);
            if (recoveryWorker == null || recoveryCache == null || !Wallet.TrySpend(tuning.storehouseCost)) return false;

            var outward = recoveryCache.transform.position.x >= 0f ? Vector3.right : Vector3.left;
            var site = HousePlacementRules.Snap(recoveryCache.transform.position + outward * 4f);
            if (!recoveryWorker.CanReach(site))
            {
                Wallet.Refund(tuning.storehouseCost);
                return false;
            }
            var storehouse = createBuilding(BuildingType.Storehouse, site);
            if (storehouse == null)
            {
                Wallet.Refund(tuning.storehouseCost);
                return false;
            }
            storehouseConstructionInProgress = true;
            recoveryWorker.IssueConstruct(storehouse, CompleteStorehouse);
            return true;
        }

        private ResourceCache FindRecoveryCache(WorkerAgent worker)
        {
            if (worker == null || caches == null) return null;
            return caches.Where(cache => cache != null && cache.Remaining > 0 &&
                                         (cacheVisibility == null || cacheVisibility(cache.transform.position)))
                .OrderBy(cache => (cache.transform.position - worker.transform.position).sqrMagnitude)
                .FirstOrDefault();
        }

        private void CompleteStorehouse(ConstructibleBuilding storehouse)
        {
            storehouseConstructionInProgress = false;
            if (storehouse == null || !storehouse.IsComplete)
            {
                storehouseRecoveryRequested = true;
                return;
            }
            buildingConstructed?.Invoke(false, BuildingType.Storehouse.ToString());
            completedStorehouses++;
            storehouseRecoveryRequested = false;
            if (recoveryWorker != null && recoveryWorker.IsAlive && recoveryCache != null && recoveryCache.Remaining > 0)
                recoveryWorker.IssueGather(recoveryCache);
        }

        private void CompleteHouse(ConstructibleBuilding house)
        {
            if (house == null || !house.IsComplete) return;
            houseConstructionInProgress = false;
            completedHouses++;
            Population.AddCapacity(tuning.housePopulationCapacity);
            buildingConstructed?.Invoke(false, BuildingType.House.ToString());
        }

        private void CompleteProduction(ProductionItem item)
        {
            if (item == ProductionItem.Worker)
            {
                workerQueued = false;
                var worker = createWorker(nextWorkerSlot++);
                workers.Add(worker);
                var cache = startingCaches.FirstOrDefault(candidate => candidate != null && candidate.Remaining > 0) ??
                            caches.FirstOrDefault(candidate => candidate != null && candidate.Remaining > 0 &&
                                (cacheVisibility == null || cacheVisibility(candidate.transform.position)));
                if (cache != null) worker.IssueGather(cache);
                entityProduced?.Invoke(false, ProductionItem.Worker.ToString());
                return;
            }
            var formation = createFormation(item.ToFormationType());
            formations.Add(formation);
            entityProduced?.Invoke(false, item.ToString());
            if (ApplyCurrentPhaseOrder(formation)) RecordCurrentPhaseAttack();
        }

        private void UpdatePhase()
        {
            var elapsed = elapsedProvider();
            var current = MatchRules.PhaseAt(elapsed, tuning.aiProbeSeconds, tuning.aiPressureSeconds,
                tuning.aiFinalAssaultSeconds);
            if (current == Phase) return;
            Phase = current;
            if (!IsDefending) ApplyCurrentPhaseOrders();
        }

        private void UpdateDefense()
        {
            var threat = playerFormationProvider()
                .Where(candidate => candidate != null && candidate.MemberCount > 0 &&
                                    (playerFormationVisibility == null || playerFormationVisibility(candidate)))
                .OrderBy(candidate => (candidate.transform.position - ownHisar.transform.position).sqrMagnitude)
                .FirstOrDefault(candidate => IsThreateningBase(candidate.transform.position));
            if (threat != null)
            {
                IsDefending = true;
                foreach (var formation in formations.Where(candidate => candidate != null &&
                             candidate.MemberCount > 0 &&
                             (candidate.transform.position - ownHisar.transform.position).sqrMagnitude <= 400f))
                    formation.IssueFocus(threat);
                return;
            }
            if (!IsDefending) return;
            IsDefending = false;
            ApplyCurrentPhaseOrders();
        }

        private bool IsThreateningBase(Vector3 position)
        {
            if ((position - ownHisar.transform.position).sqrMagnitude <= 144f) return true;
            if (buildings.Any(building => building != null && !building.IsDestroyed &&
                                          (position - building.transform.position).sqrMagnitude <= 100f)) return true;
            return workers.Any(worker => worker != null && worker.IsAlive &&
                                         (position - worker.transform.position).sqrMagnitude <= 100f);
        }

        private void ApplyCurrentPhaseOrders()
        {
            var orderDispatched = false;
            foreach (var formation in formations.Where(item => item != null && item.MemberCount > 0))
                orderDispatched |= ApplyCurrentPhaseOrder(formation);
            if (orderDispatched) RecordCurrentPhaseAttack();
        }

        private bool ApplyCurrentPhaseOrder(FormationAgent formation)
        {
            if (formation == null || IsDefending) return false;
            switch (Phase)
            {
                case AiPhase.Probe:
                    if (formation.Type != FormationType.Cavalry) return false;
                    formation.IssueAttackMove(new Vector3(0f, 0f, -1f));
                    return true;
                case AiPhase.Pressure:
                    formation.IssueAttackMove(new Vector3(0f, 0f, -4f));
                    return true;
                case AiPhase.FinalAssault:
                    return formation.IssueFocus(playerHisar);
                default:
                    return false;
            }
        }

        private void RecordCurrentPhaseAttack()
        {
            if (Phase == AiPhase.Preparing || !dispatchedAttacks.Add(Phase)) return;
            attackStarted?.Invoke(Phase, elapsedProvider());
        }
    }
}
