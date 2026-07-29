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
        public const string ControllerObjectName = "Starting Economy";
        public const string HisarObjectName = "Karasungur Hisar";
        public const string EnemyHisarObjectName = "Alazhan Hisar";
        public const string CachePrefix = "Caravan Cache";
        public const int WorkerCount = 4;

        private readonly List<WorkerAgent> workers = new();
        private readonly List<WorkerAgent> selectedWorkers = new();
        private readonly List<HouseBuilding> houses = new();
        private readonly List<ConstructibleBuilding> storehouses = new();
        private readonly List<ConstructibleBuilding> watchtowers = new();
        private readonly List<ConstructibleBuilding> buildings = new();
        private readonly List<FormationAgent> friendlyFormations = new();
        private readonly List<FormationAgent> enemyFormations = new();
        private readonly List<WorkerAgent> enemyWorkers = new();
        private readonly List<ConstructibleBuilding> enemyBuildings = new();
        private readonly List<FormationAgent> selectedFormations = new();
        private readonly Dictionary<int, ControlGroup> controlGroups = new();
        private readonly Queue<QueuedInput> queuedInputs = new();
        [SerializeField] private EconomyTuning tuning;
        private EconomyWallet wallet;
        private Hisar hisar;
        private Hisar enemyHisar;
        private Camera worldCamera;
        private Text suppliesText;
        private Text populationText;
        private Text selectionText;
        private Text orderText;
        private Vector2 selectionStart;
        private bool selecting;
        private Image selectionBox;
        private RectTransform selectionBoxTransform;
        private PopulationLedger population;
        private Button buildHouseButton;
        private Button buildStorehouseButton;
        private Button buildWatchtowerButton;
        private Button cancelBuildButton;
        private Button demolishButton;
        private Button trainSpearmenButton;
        private Button trainWorkerButton;
        private Button trainArchersButton;
        private Button trainCavalryButton;
        private Button cancelTrainingButton;
        private Button attackMoveButton;
        private Button stopFormationsButton;
        private Text queueText;
        private Canvas hudCanvas;
        private HisarProductionQueue productionQueue;
        private ScriptedOpponentController opponent;
        private IReadOnlyList<ResourceCache> enemyCaches;
        private IReadOnlyList<ResourceCache> allCaches;
        private MatchDirector matchDirector;
        private MatchTelemetry telemetry;
        private GameObject resultOverlay;
        private Text resultTitleText;
        private Text resultElapsedText;
        private Button restartButton;
        private Button quitButton;
        private FogOfWarSystem fogOfWar;
        private ConstructibleBuilding selectedBuilding;
        private bool hisarSelected;
        private GameObject placementPreview;
        private WorkerAgent placementWorker;
        private BuildingType placementType;
        private bool placementValid;
        private Vector3 placementPosition;
        private NavMeshSurface navMeshSurface;
        private Vector3 lastRouteCandidate = new(float.PositiveInfinity, 0f, float.PositiveInfinity);
        private int buildingRouteVersion;
        private int lastRouteVersion = -1;
        private bool lastRouteResult;
        private ConstructibleBuilding demolitionCandidate;
        private bool awaitingAttackMove;
        private bool opponentTargetsAvailable = true;
        private FormationAgent lastClickedFormation;
        private float lastFormationClickTime = float.NegativeInfinity;
        private GameplayAudio gameplayAudio;
        private float nextUnderAttackCueAt = float.NegativeInfinity;
        private float nextHitCueAt = float.NegativeInfinity;
        private Vector3? hisarRallyPoint;
        private ResourceCache hisarRallyCache;
        private GameObject hisarRallyMarker;
        private WorldHealthBar hoveredHealthBar;

        private const float UnderAttackCooldownSeconds = 4f;
        private const float HitCueCooldownSeconds = 0.08f;

        private static readonly Key[] ControlGroupKeys =
        {
            Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5,
            Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9
        };

        private static readonly Vector3 RouteStart = new(0f, 0f, -4f);
        private static readonly Vector3 RouteEnd = new(0f, 0f, 25f);

        public int Supplies => wallet?.Supplies ?? 0;
        public int StartingSupplies => tuning?.startingSupplies ?? 0;
        public int PopulationUsed => population?.Used ?? 0;
        public int PopulationCapacity => population?.Capacity ?? 0;
        public bool IsHousePlacementActive => placementWorker != null;
        public bool IsBuildingPlacementActive => placementWorker != null;
        public bool IsAttackMoveTargetingActive => awaitingAttackMove;
        public IReadOnlyList<WorkerAgent> Workers => workers;
        public IReadOnlyList<HouseBuilding> Houses => houses;
        public IReadOnlyList<ConstructibleBuilding> Storehouses => storehouses;
        public IReadOnlyList<ConstructibleBuilding> Watchtowers => watchtowers;
        public IReadOnlyList<FormationAgent> FriendlyFormations => friendlyFormations;
        public IReadOnlyList<FormationAgent> EnemyFormations => enemyFormations;
        public IReadOnlyList<WorkerAgent> EnemyWorkers => enemyWorkers;
        public IReadOnlyList<ConstructibleBuilding> EnemyBuildings => enemyBuildings;
        public Hisar FriendlyHisar => hisar;
        public Hisar EnemyHisar => enemyHisar;
        public IReadOnlyList<FormationAgent> SelectedFormations => selectedFormations;
        public FogOfWarSystem FogOfWar => fogOfWar;
        public int ProductionQueueCount => productionQueue?.Count ?? 0;
        public ProductionItem? ActiveProductionItem => productionQueue?.Active;
        public float ProductionQueueProgress => productionQueue?.Progress ?? 0f;
        public MatchOutcome Outcome => matchDirector?.Outcome ?? MatchOutcome.InProgress;
        public float MatchElapsedSeconds => matchDirector?.ElapsedSeconds ?? 0f;
        public AiPhase OpponentPhase => opponent?.Phase ?? AiPhase.Preparing;
        public bool OpponentIsDefending => opponent != null && opponent.IsDefending;
        public int OpponentSupplies => opponent?.Wallet?.Supplies ?? 0;
        public int OpponentPopulationUsed => opponent?.Population?.Used ?? 0;
        public int OpponentPopulationCapacity => opponent?.Population?.Capacity ?? 0;
        public int OpponentProductionQueueCount => opponent?.ProductionQueueCount ?? 0;
        public int OpponentFormationsProduced => opponent?.CompletedFormationCount ?? 0;
        public IReadOnlyList<ResourceCache> OpponentCaches => enemyCaches;
        public MatchSummary CurrentMatchSummary => telemetry?.Summary;
        public bool QuitRequested { get; private set; }
        public string MatchSummaryPath => telemetry?.SummaryPath;
        public string MatchEventLogPath => telemetry?.EventLogPath;
        public IReadOnlyList<ResourceCache> Caches { get; private set; }
        public string LastEconomyNotification { get; private set; }
        public GameplayAudio GameplayAudio => gameplayAudio;
        public int UnderAttackWarningCount { get; private set; }
        public Vector3? HisarRallyPoint => hisarRallyPoint;
        public ResourceCache HisarRallyCache => hisarRallyCache;

        public void Configure(EconomyTuning economyTuning) => tuning = economyTuning;

        private void Awake()
        {
            if (tuning == null)
            {
                Debug.LogError("Starting economy requires EconomyTuning.");
                enabled = false;
                return;
            }

            Time.timeScale = 1f;
            worldCamera = Camera.main;
            wallet = new EconomyWallet(tuning.startingSupplies);
            population = new PopulationLedger(WorkerCount, tuning.startingPopulationCap, tuning.hardPopulationCap);
            productionQueue = new HisarProductionQueue(wallet, population, tuning);
            matchDirector = new MatchDirector();
            telemetry = new MatchTelemetry();
            gameplayAudio = new GameObject("Gameplay Audio", typeof(AudioSource), typeof(GameplayAudio))
                .GetComponent<GameplayAudio>();
            gameplayAudio.transform.SetParent(transform, false);
            gameplayAudio.Initialize();
            if (FindAnyObjectByType<AudioListener>() == null) worldCamera.gameObject.AddComponent<AudioListener>();
            BuildNavMesh();
            hisar = CreateHisar(true);
            enemyHisar = CreateHisar(false);
            Caches = new[]
            {
                CreateCache(1, new Vector3(-7f, 0.65f, 4f)),
                CreateCache(2, new Vector3(8f, 0.65f, 6f))
            };
            enemyCaches = new[]
            {
                CreateCache(3, new Vector3(7f, 0.65f, 14f)),
                CreateCache(4, new Vector3(-8f, 0.65f, 12f))
            };
            allCaches = Caches.Concat(enemyCaches).ToArray();
            CreateWorkers();
            CreateHud();
            InputSystem.onEvent += QueueInputEvent;
            CreateFogOfWar();
            CreateOpponent();
            SetOrderFeedback("Ready - select workers to begin");
        }

        private void Update()
        {
            UpdateHud();
            UpdateHealthBarHover();
            if (matchDirector.IsComplete)
            {
                queuedInputs.Clear();
                return;
            }
            matchDirector.Advance(Time.deltaTime);
            HandleQueuedInput();
            UpdateSelectionGesture();
            UpdatePlacementPreview();
            var completed = productionQueue.Advance(Time.deltaTime);
            if (completed.HasValue) CompleteProduction(completed.Value);
        }

        private void OnDestroy()
        {
            if (fogOfWar != null) fogOfWar.HostileFirstRevealed -= HandleHostileFirstRevealed;
            InputSystem.onEvent -= QueueInputEvent;
        }

        public void SelectOnly(WorkerAgent worker)
        {
            ClearSelection();
            if (worker == null) return;
            selectedWorkers.Add(worker);
            worker.SetSelected(true);
            PlayCue(GameplayCue.Selection);
            UpdateHud();
        }

        public void IssueGatherForSmoke(ResourceCache cache)
        {
            var worker = workers.First();
            SelectOnly(worker);
            worker.IssueGather(cache);
            SetOrderFeedback("Gathering Supplies");
            PlayCue(GameplayCue.Order);
        }

        public void CreditSuppliesForAutomation(int amount)
        {
            wallet.Deposit(amount);
        }

        public void CreditOpponentSuppliesForAutomation(int amount) => opponent?.Wallet.Deposit(amount);

        public bool TriggerOpponentRouteFailureForAutomation()
        {
            var worker = enemyWorkers.FirstOrDefault(candidate => candidate != null && candidate.IsAlive &&
                                                                  candidate.CarriedSupplies == 0 &&
                                                                  candidate.CurrentConstruction == null);
            if (worker == null || opponent == null) return false;
            var fallbackRadius = tuning.cacheFallbackRadius;
            tuning.cacheFallbackRadius = 0.1f;
            worker.IssueGather(null);
            tuning.cacheFallbackRadius = fallbackRadius;
            return opponent.IsStorehouseRecoveryRequested;
        }

        public bool TryQueueFormation(FormationType type)
        {
            if (!productionQueue.TryEnqueueFormation(type))
            {
                SetOrderFeedback(Supplies < tuning.formationCost
                    ? $"Need {tuning.formationCost} Supplies"
                    : $"Population blocked - need {tuning.formationPopulation} free");
                return false;
            }
            SetOrderFeedback($"{type} queued");
            PlayCue(GameplayCue.Order);
            return true;
        }

        public bool TryQueueWorker()
        {
            if (!productionQueue.TryEnqueueWorker())
            {
                SetOrderFeedback(Supplies < tuning.workerCost
                    ? $"Need {tuning.workerCost} Supplies"
                    : "Population blocked - need 1 free");
                return false;
            }
            SetOrderFeedback("Worker queued");
            PlayCue(GameplayCue.Order);
            return true;
        }

        public bool CancelActiveTraining()
        {
            var active = productionQueue.Active;
            if (!productionQueue.CancelActive()) return false;
            var refunded = active == ProductionItem.Worker ? tuning.workerCost : tuning.formationCost;
            SetOrderFeedback($"Training cancelled - {refunded} Supplies refunded");
            return true;
        }

        public void SelectHisar()
        {
            ClearSelection();
            hisarSelected = true;
            hisar.SetSelected(true);
            SetOrderFeedback("Hisar selected - train or right-click to set rally");
            PlayCue(GameplayCue.Selection);
            UpdateHud();
        }

        public void SelectOnly(FormationAgent formation)
        {
            ClearSelection();
            if (formation == null || !formation.IsFriendly) return;
            AddSelectedFormation(formation);
            UpdateHud();
        }

        public void SelectFormationsForAutomation(IEnumerable<FormationAgent> formations)
        {
            ClearSelection();
            foreach (var formation in formations) AddSelectedFormation(formation);
            UpdateHud();
        }

        public void AssignControlGroup(int number)
        {
            if (number < 1 || number > 9) return;
            controlGroups[number] = new ControlGroup(selectedWorkers, selectedFormations);
            SetOrderFeedback($"Control group {number} assigned - {selectedWorkers.Count} workers, " +
                             $"{selectedFormations.Count} formations");
        }

        public bool RecallControlGroup(int number)
        {
            if (!controlGroups.TryGetValue(number, out var group))
            {
                SetOrderFeedback($"Control group {number} is empty");
                return false;
            }
            ClearSelection();
            foreach (var worker in group.Workers.Where(worker => worker != null && workers.Contains(worker)))
                AddSelection(worker);
            foreach (var formation in group.Formations.Where(formation => formation != null &&
                         formation.MemberCount > 0 && friendlyFormations.Contains(formation)))
                AddSelectedFormation(formation);
            SetOrderFeedback($"Control group {number} recalled - {selectedWorkers.Count} workers, " +
                             $"{selectedFormations.Count} formations");
            UpdateHud();
            return selectedWorkers.Count > 0 || selectedFormations.Count > 0;
        }

        public int ControlGroupSize(int number) => controlGroups.TryGetValue(number, out var group)
            ? group.Workers.Count(worker => worker != null && workers.Contains(worker)) +
              group.Formations.Count(formation => formation != null && formation.MemberCount > 0 &&
                                                  friendlyFormations.Contains(formation))
            : 0;

        public void IssueMoveForSelected(Vector3 destination)
        {
            if (selectedFormations.Count > 0) IssueFormationGroupOrder(destination, false);
            if (selectedWorkers.Count == 0) return;
            var availableWorkers = selectedWorkers.Where(worker => worker.CurrentConstruction == null).ToList();
            if (availableWorkers.Count == 0)
            {
                SetOrderFeedback("Cancel construction before issuing another order");
                return;
            }
            for (var i = 0; i < availableWorkers.Count; i++)
            {
                var offset = FormationOffset(i, availableWorkers.Count);
                availableWorkers[i].IssueMove(destination + offset);
            }
            SetOrderFeedback(selectedFormations.Count > 0
                ? $"Move - {selectedFormations.Count} formation(s), {availableWorkers.Count} worker(s)"
                : "Move");
            if (selectedFormations.Count == 0)
                CreateOrderMarker(destination, new Color(0.2f, 0.78f, 1f));
            PlayCue(GameplayCue.Order);
        }

        public void IssueAttackMoveForSelected(Vector3 destination) => IssueFormationGroupOrder(destination, true);

        public void StopSelectedFormations()
        {
            foreach (var formation in selectedFormations) formation.IssueStop();
            awaitingAttackMove = false;
            SetOrderFeedback($"Stop - {selectedFormations.Count} formation(s)");
            PlayCue(GameplayCue.Order);
        }

        public bool IssueFocusForSmoke(FormationAgent friendly, FormationAgent hostile)
        {
            SelectOnly(friendly);
            var issued = friendly.IssueFocus(hostile);
            if (issued) SetOrderFeedback($"Focus {hostile.Type}");
            if (issued) PlayCue(GameplayCue.Order);
            return issued;
        }

        public bool SetHisarRallyForAutomation(Vector3 position, ResourceCache cache = null)
            => TrySetHisarRally(position, cache);

        public FormationAgent DeployEnemyForAutomation(FormationType type, Vector3 position)
        {
            var enemy = CreateFormation(type, false, position, false);
            enemyFormations.Add(enemy);
            return enemy;
        }

        public FormationAgent DeployFriendlyForAutomation(FormationType type, Vector3 position)
        {
            population.TryReserve(tuning.formationPopulation);
            var friendly = CreateFormation(type, true, position);
            friendlyFormations.Add(friendly);
            return friendly;
        }

        public bool TryPlaceHouse(WorkerAgent worker, Vector3 position)
            => TryPlaceBuilding(worker, BuildingType.House, position);

        public bool TryPlaceStorehouse(WorkerAgent worker, Vector3 position)
            => TryPlaceBuilding(worker, BuildingType.Storehouse, position);

        public bool TryPlaceWatchtower(WorkerAgent worker, Vector3 position)
            => TryPlaceBuilding(worker, BuildingType.Watchtower, position);

        public void SelectOnly(ConstructibleBuilding building)
        {
            ClearSelection();
            if (building == null || !building.IsComplete || building.IsDestroyed) return;
            selectedBuilding = building;
            building.SetSelected(true);
            SetOrderFeedback($"{building.Type} selected");
            PlayCue(GameplayCue.Selection);
            UpdateHud();
        }

        public bool RequestDemolition()
        {
            if (selectedBuilding == null || !selectedBuilding.IsComplete || selectedBuilding.IsDestroyed)
                return false;
            if (demolitionCandidate != selectedBuilding)
            {
                demolitionCandidate = selectedBuilding;
                SetOrderFeedback($"Confirm demolish {selectedBuilding.Type} - no refund");
                UpdateHud();
                return false;
            }

            var building = selectedBuilding;
            demolitionCandidate = null;
            return building.Demolish();
        }

        private bool TryPlaceBuilding(WorkerAgent worker, BuildingType type, Vector3 position) =>
            TryPlaceBuilding(worker, type, position, out _);

        private bool TryPlaceBuilding(WorkerAgent worker, BuildingType type, Vector3 position,
            out string rejectionCode)
        {
            if (worker == null)
            {
                rejectionCode = "invalid_actor";
                return false;
            }
            if (!IsFinite(position))
            {
                rejectionCode = "invalid_position";
                SetOrderFeedback("Invalid - outside buildable ground");
                return false;
            }
            var snapped = HousePlacementRules.Snap(position);
            if (!CanPlaceBuilding(worker, snapped, out rejectionCode, out var reason))
            {
                SetOrderFeedback(reason);
                return false;
            }
            var cost = BuildingCost(type);
            if (!wallet.TrySpend(cost))
            {
                rejectionCode = "insufficient_supplies";
                SetOrderFeedback($"Need {cost} Supplies");
                return false;
            }

            var building = CreateBuilding(type, snapped);
            buildings.Add(building);
            buildingRouteVersion++;
            if (type == BuildingType.House) houses.Add((HouseBuilding)building);
            else if (type == BuildingType.Storehouse) storehouses.Add(building);
            else watchtowers.Add(building);
            worker.IssueConstruct(building, CompleteBuilding);
            SetOrderFeedback($"{type} placed - {cost} Supplies spent");
            rejectionCode = null;
            return true;
        }

        public bool CancelConstruction(WorkerAgent worker)
        {
            var building = worker?.CurrentConstruction;
            if (building == null || !worker.CancelConstruction()) return false;
            RemoveBuildingFromLists(building);
            Destroy(building.gameObject);
            var cost = BuildingCost(building.Type);
            wallet.Refund(cost);
            SetOrderFeedback($"{building.Type} cancelled - {cost} Supplies refunded");
            return true;
        }

    }
}
