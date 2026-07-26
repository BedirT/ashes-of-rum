using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AshesOfRum
{
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
        private bool houseConstructionInProgress;
        private bool workerQueued;
        private bool started;
        private bool suspended;
        private readonly HashSet<AiPhase> dispatchedAttacks = new();

        public EconomyWallet Wallet { get; private set; }
        public PopulationLedger Population { get; private set; }
        public AiPhase Phase { get; private set; } = AiPhase.Preparing;
        public bool IsDefending { get; private set; }
        public int ProductionQueueCount => productionQueue?.Count ?? 0;

        public void Initialize(EconomyTuning economyTuning, Hisar home, Hisar hostileHisar,
            IList<WorkerAgent> workerList, IList<FormationAgent> formationList,
            IList<ConstructibleBuilding> buildingList, IReadOnlyList<ResourceCache> knownCaches,
            Func<int, WorkerAgent> workerFactory, Func<FormationType, FormationAgent> formationFactory,
            Func<BuildingType, Vector3, ConstructibleBuilding> buildingFactory,
            Func<IEnumerable<FormationAgent>> hostileFormationProvider,
            Func<FormationAgent, bool> isHostileFormationVisible, Func<float> matchElapsedProvider,
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
                worker.IssueGather(caches[index % caches.Count]);
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
            if (building.Type != BuildingType.House || completedHouses <= 0) return;
            completedHouses--;
            Population.RemoveCapacity(tuning.housePopulationCapacity);
        }

        public void NotifyConstructionAbandoned(ConstructibleBuilding building)
        {
            if (building != null && building.Type == BuildingType.House) houseConstructionInProgress = false;
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
                worker.IssueGather(caches[nextWorkerSlot % caches.Count]);
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
            return buildings.Any(building => building != null && !building.IsDestroyed &&
                                              (position - building.transform.position).sqrMagnitude <= 100f);
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
