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
    public sealed class StartingEconomyController : MonoBehaviour
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
        public MatchOutcome Outcome => matchDirector?.Outcome ?? MatchOutcome.InProgress;
        public float MatchElapsedSeconds => matchDirector?.ElapsedSeconds ?? 0f;
        public AiPhase OpponentPhase => opponent?.Phase ?? AiPhase.Preparing;
        public bool OpponentIsDefending => opponent != null && opponent.IsDefending;
        public int OpponentSupplies => opponent?.Wallet?.Supplies ?? 0;
        public int OpponentPopulationUsed => opponent?.Population?.Used ?? 0;
        public int OpponentPopulationCapacity => opponent?.Population?.Capacity ?? 0;
        public IReadOnlyList<ResourceCache> OpponentCaches => enemyCaches;
        public MatchSummary CurrentMatchSummary => telemetry?.Summary;
        public bool QuitRequested { get; private set; }
        public string MatchSummaryPath => telemetry?.SummaryPath;
        public string MatchEventLogPath => telemetry?.EventLogPath;
        public IReadOnlyList<ResourceCache> Caches { get; private set; }
        public string LastEconomyNotification { get; private set; }

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
            UpdateHud();
        }

        public void IssueGatherForSmoke(ResourceCache cache)
        {
            var worker = workers.First();
            SelectOnly(worker);
            worker.IssueGather(cache);
            SetOrderFeedback("Gathering Supplies");
        }

        public void CreditSuppliesForAutomation(int amount)
        {
            wallet.Deposit(amount);
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
            SetOrderFeedback("Hisar selected - train a formation");
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
        }

        public void IssueAttackMoveForSelected(Vector3 destination) => IssueFormationGroupOrder(destination, true);

        public void StopSelectedFormations()
        {
            foreach (var formation in selectedFormations) formation.IssueStop();
            awaitingAttackMove = false;
            SetOrderFeedback($"Stop - {selectedFormations.Count} formation(s)");
        }

        public bool IssueFocusForSmoke(FormationAgent friendly, FormationAgent hostile)
        {
            SelectOnly(friendly);
            var issued = friendly.IssueFocus(hostile);
            if (issued) SetOrderFeedback($"Focus {hostile.Type}");
            return issued;
        }

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

        private bool TryPlaceBuilding(WorkerAgent worker, BuildingType type, Vector3 position)
        {
            if (worker == null) return false;
            var snapped = HousePlacementRules.Snap(position);
            if (!CanPlaceBuilding(worker, snapped, out var reason))
            {
                SetOrderFeedback(reason);
                return false;
            }
            var cost = BuildingCost(type);
            if (!wallet.TrySpend(cost))
            {
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

        private void BuildNavMesh()
        {
            var ground = GameObject.Find("Bootstrap Ground");
            navMeshSurface = ground.GetComponent<NavMeshSurface>() ?? ground.AddComponent<NavMeshSurface>();
            navMeshSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            navMeshSurface.collectObjects = CollectObjects.All;
            navMeshSurface.BuildNavMesh();
        }

        private Hisar CreateHisar(bool friendly)
        {
            var root = new GameObject(friendly ? HisarObjectName : EnemyHisarObjectName);
            root.transform.SetPositionAndRotation(new Vector3(0f, 0f, friendly ? -8f : 26f), Quaternion.identity);
            var factionColor = friendly ? new Color(0.08f, 0.24f, 0.55f) : new Color(0.62f, 0.1f, 0.05f);
            CreatePrimitive(PrimitiveType.Cube, "Hisar Keep", root.transform,
                new Vector3(0f, 1.4f, 0f), new Vector3(5f, 2.8f, 4f), factionColor);
            CreatePrimitive(friendly ? PrimitiveType.Cylinder : PrimitiveType.Cube,
                friendly ? "Black Falcon Marker" : "Living Flame Marker", root.transform,
                new Vector3(0f, 3.15f, 0f), new Vector3(1.2f, 0.15f, 1.2f),
                friendly ? new Color(0.03f, 0.05f, 0.08f) : new Color(1f, 0.45f, 0.08f));
            var obstacle = root.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = new Vector3(0f, 1.5f, 0f);
            obstacle.size = new Vector3(5.2f, 3f, 4.2f);
            obstacle.carving = true;
            var result = root.AddComponent<Hisar>();
            result.Initialize(friendly, tuning.hisarHealth, HandleHisarDestroyed);
            return result;
        }

        private ResourceCache CreateCache(int index, Vector3 position)
        {
            var root = new GameObject($"{CachePrefix} {index}");
            root.transform.position = position;
            var cache = root.AddComponent<ResourceCache>();
            cache.Initialize(tuning.cacheSupplies);
            for (var i = 0; i < 5; i++)
            {
                var offset = new Vector3((i % 3 - 1) * 0.65f, i % 2 * 0.22f, (i / 3 - 0.5f) * 0.65f);
                CreatePrimitive(PrimitiveType.Cube, $"Supply Bundle {i + 1}", root.transform, offset,
                    new Vector3(0.55f, 0.45f, 0.55f), new Color(0.72f, 0.49f, 0.2f));
            }
            var collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.35f, 0f);
            collider.size = new Vector3(2.8f, 1.4f, 2f);
            return cache;
        }

        private void CreateWorkers()
        {
            var positions = new[]
            {
                new Vector3(-2.2f, 0f, -4f), new Vector3(-0.7f, 0f, -4f),
                new Vector3(0.7f, 0f, -4f), new Vector3(2.2f, 0f, -4f)
            };
            for (var i = 0; i < WorkerCount; i++)
                workers.Add(CreateWorker(true, i, positions[i], wallet, hisar, Caches));
        }

        private WorkerAgent CreateWorker(bool friendly, int slot, Vector3 position, EconomyWallet sideWallet,
            Hisar home, IReadOnlyList<ResourceCache> knownCaches)
        {
            var workerObject = new GameObject($"{(friendly ? "Karasungur" : "Alazhan")} Worker {slot + 1}");
            workerObject.transform.position = position;
            CreatePrimitive(PrimitiveType.Capsule, "Worker Body", workerObject.transform,
                new Vector3(0f, 0.9f, 0f), new Vector3(0.72f, 0.9f, 0.72f),
                friendly ? new Color(0.12f, 0.42f, 0.92f) : new Color(0.78f, 0.16f, 0.08f));
            var navAgent = workerObject.AddComponent<NavMeshAgent>();
            navAgent.radius = 0.36f;
            navAgent.height = 1.8f;
            navAgent.avoidancePriority = (friendly ? 40 : 70) + slot;
            CreatePrimitive(PrimitiveType.Cylinder, "Selection Ring", workerObject.transform,
                new Vector3(0f, 0.04f, 0f), new Vector3(1.25f, 0.025f, 1.25f),
                friendly ? new Color(0.2f, 0.78f, 1f) : new Color(1f, 0.35f, 0.1f));
            CreatePrimitive(PrimitiveType.Cube, friendly ? "Worker Diamond" : "Worker Square", workerObject.transform,
                new Vector3(0f, 2.05f, 0f), new Vector3(0.32f, 0.32f, 0.32f), Color.white).transform.rotation =
                friendly ? Quaternion.Euler(0f, 45f, 45f) : Quaternion.identity;
            CreatePrimitive(PrimitiveType.Cube, "Carried Supplies", workerObject.transform,
                new Vector3(0f, 1.35f, -0.62f), new Vector3(0.42f, 0.42f, 0.42f),
                new Color(0.95f, 0.68f, 0.2f));
            var worker = workerObject.AddComponent<WorkerAgent>();
            worker.Initialize(tuning, sideWallet, home, knownCaches, slot,
                friendly ? NotifyEconomyState : null,
                friendly ? FindNearestDropOff : null,
                friendly ? IsCurrentlyVisible : _ => true,
                friendly,
                amount => telemetry.RecordSupplies(friendly, amount, MatchElapsedSeconds),
                HandleWorkerDestroyed);
            if (fogOfWar != null)
            {
                if (friendly) fogOfWar.RegisterFriendly(worker.transform);
                else fogOfWar.RegisterHostileMobile(worker.gameObject);
            }
            return worker;
        }

        private void CreateHud()
        {
            var canvasObject = new GameObject("RTS HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform);
            hudCanvas = canvasObject.GetComponent<Canvas>();
            hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            CreatePanel(hudCanvas.transform, "Top Bar", new Vector2(0.02f, 0.91f), new Vector2(0.34f, 0.98f));
            CreatePanel(hudCanvas.transform, "Controls Bar", new Vector2(0.64f, 0.91f), new Vector2(0.98f, 0.98f));
            suppliesText = CreateText(hudCanvas.transform, "Supplies", new Vector2(0.035f, 0.925f), new Vector2(0.32f, 0.97f), 28, TextAnchor.MiddleLeft);
            populationText = CreateText(hudCanvas.transform, "Population", new Vector2(0.20f, 0.925f), new Vector2(0.40f, 0.97f), 28, TextAnchor.MiddleLeft);
            CreateText(hudCanvas.transform, "Controls", new Vector2(0.66f, 0.915f), new Vector2(0.96f, 0.975f), 14, TextAnchor.MiddleRight).text =
                "LEFT CLICK / DRAG Select   SHIFT Modify   CTRL+1-9 Group   1-9 Recall\nRIGHT CLICK Order   WASD / EDGE / MIDDLE DRAG Pan   WHEEL Zoom";
            CreatePanel(hudCanvas.transform, "Bottom Panel", new Vector2(0.02f, 0.025f), new Vector2(0.98f, 0.15f));
            selectionText = CreateText(hudCanvas.transform, "Selection", new Vector2(0.04f, 0.045f), new Vector2(0.34f, 0.13f), 22, TextAnchor.MiddleLeft);
            orderText = CreateText(hudCanvas.transform, "Order", new Vector2(0.35f, 0.075f), new Vector2(0.57f, 0.13f), 20, TextAnchor.MiddleCenter);
            queueText = CreateText(hudCanvas.transform, "Production Queue", new Vector2(0.35f, 0.04f), new Vector2(0.57f, 0.075f), 15, TextAnchor.MiddleCenter);
            buildHouseButton = CreateButton(hudCanvas.transform, "Build House", new Vector2(0.58f, 0.085f), new Vector2(0.68f, 0.135f),
                $"HOUSE {tuning.houseCost} [H]", () => BeginBuildingPlacement(BuildingType.House));
            buildStorehouseButton = CreateButton(hudCanvas.transform, "Build Storehouse", new Vector2(0.68f, 0.085f), new Vector2(0.78f, 0.135f),
                $"STOREHOUSE {tuning.storehouseCost} [R]", () => BeginBuildingPlacement(BuildingType.Storehouse));
            buildWatchtowerButton = CreateButton(hudCanvas.transform, "Build Watchtower", new Vector2(0.78f, 0.085f), new Vector2(0.88f, 0.135f),
                $"WATCHTOWER {tuning.watchtowerCost} [T]", () => BeginBuildingPlacement(BuildingType.Watchtower));
            cancelBuildButton = CreateButton(hudCanvas.transform, "Cancel Build", new Vector2(0.88f, 0.085f), new Vector2(0.98f, 0.135f),
                "CANCEL BUILD  [X]", CancelSelectedConstruction);
            demolishButton = CreateButton(hudCanvas.transform, "Demolish Building", new Vector2(0.78f, 0.05f), new Vector2(0.98f, 0.125f),
                "DEMOLISH [X]", () => RequestDemolition());
            trainWorkerButton = CreateButton(hudCanvas.transform, "Train Worker", new Vector2(0.58f, 0.05f), new Vector2(0.66f, 0.125f),
                $"WORKER {tuning.workerCost} [Q]", () => TryQueueWorker());
            trainSpearmenButton = CreateButton(hudCanvas.transform, "Train Spearmen", new Vector2(0.66f, 0.05f), new Vector2(0.74f, 0.125f),
                $"SPEARMEN {tuning.formationCost} [S]", () => TryQueueFormation(FormationType.Spearmen));
            trainArchersButton = CreateButton(hudCanvas.transform, "Train Archers", new Vector2(0.74f, 0.05f), new Vector2(0.82f, 0.125f),
                $"ARCHERS {tuning.formationCost} [A]", () => TryQueueFormation(FormationType.Archers));
            trainCavalryButton = CreateButton(hudCanvas.transform, "Train Cavalry", new Vector2(0.82f, 0.05f), new Vector2(0.90f, 0.125f),
                $"CAVALRY {tuning.formationCost} [C]", () => TryQueueFormation(FormationType.Cavalry));
            cancelTrainingButton = CreateButton(hudCanvas.transform, "Cancel Training", new Vector2(0.90f, 0.05f), new Vector2(0.98f, 0.125f),
                "CANCEL [X]", () => CancelActiveTraining());
            attackMoveButton = CreateButton(hudCanvas.transform, "Attack Move", new Vector2(0.68f, 0.03f), new Vector2(0.83f, 0.08f),
                "ATTACK-MOVE [F]", BeginAttackMoveTargeting);
            stopFormationsButton = CreateButton(hudCanvas.transform, "Stop Formations", new Vector2(0.83f, 0.03f), new Vector2(0.98f, 0.08f),
                "STOP [G]", StopSelectedFormations);

            var boxObject = new GameObject("Selection Box", typeof(RectTransform), typeof(Image));
            boxObject.transform.SetParent(hudCanvas.transform, false);
            selectionBox = boxObject.GetComponent<Image>();
            selectionBox.color = new Color(0.15f, 0.65f, 1f, 0.22f);
            selectionBoxTransform = boxObject.GetComponent<RectTransform>();
            selectionBox.gameObject.SetActive(false);

            var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
            eventSystemObject.transform.SetParent(transform);
            eventSystemObject.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            CreateResultOverlay();
        }

        private void CreateResultOverlay()
        {
            resultOverlay = new GameObject("Match Result", typeof(RectTransform), typeof(Image));
            resultOverlay.transform.SetParent(hudCanvas.transform, false);
            var rect = resultOverlay.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            resultOverlay.GetComponent<Image>().color = new Color(0.015f, 0.025f, 0.04f, 0.94f);
            resultTitleText = CreateText(resultOverlay.transform, "Match Result Title",
                new Vector2(0.25f, 0.60f), new Vector2(0.75f, 0.72f), 52, TextAnchor.MiddleCenter);
            resultElapsedText = CreateText(resultOverlay.transform, "Match Elapsed",
                new Vector2(0.30f, 0.50f), new Vector2(0.70f, 0.58f), 28, TextAnchor.MiddleCenter);
            restartButton = CreateButton(resultOverlay.transform, "Restart Match",
                new Vector2(0.34f, 0.37f), new Vector2(0.49f, 0.45f), "RESTART", RestartMatch);
            quitButton = CreateButton(resultOverlay.transform, "Quit Match",
                new Vector2(0.51f, 0.37f), new Vector2(0.66f, 0.45f), "QUIT", RequestQuit);
            resultOverlay.SetActive(false);
        }

        private void HandleHisarDestroyed(Hisar destroyed)
        {
            if (destroyed == null) return;
            FinishMatch(destroyed.IsFriendly ? MatchOutcome.Defeat : MatchOutcome.Victory, destroyed.name);
        }

        private void FinishMatch(MatchOutcome outcome, string destroyedHisar)
        {
            if (!matchDirector.Complete(outcome)) return;
            ClearSelection();
            queuedInputs.Clear();
            foreach (var formation in friendlyFormations.Concat(enemyFormations).Where(item => item != null))
                formation.IssueStop();
            foreach (var worker in workers.Where(item => item != null)) worker.Suspend();
            opponent?.Suspend();
            var cameraController = worldCamera.GetComponent<RtsCameraController>();
            if (cameraController != null) cameraController.enabled = false;
            telemetry.Complete(outcome, MatchElapsedSeconds, destroyedHisar);
            telemetry.Write(Path.Combine(Application.persistentDataPath, "AshesOfRum", "Matches"));
            foreach (Transform child in hudCanvas.transform)
                child.gameObject.SetActive(child.gameObject == resultOverlay);
            resultOverlay.SetActive(true);
            resultTitleText.text = outcome == MatchOutcome.Victory ? "VICTORY" : "DEFEAT";
            var minutes = Mathf.FloorToInt(MatchElapsedSeconds / 60f);
            var seconds = Mathf.FloorToInt(MatchElapsedSeconds % 60f);
            resultElapsedText.text = $"ELAPSED   {minutes:00}:{seconds:00}";
            restartButton.interactable = true;
            quitButton.interactable = true;
            Time.timeScale = 0f;
        }

        public void RestartMatch()
        {
            if (!matchDirector.IsComplete) return;
            Time.timeScale = 1f;
            SceneManager.LoadScene(HarnessContract.SceneName, LoadSceneMode.Single);
        }

        public void RequestQuit()
        {
            if (!matchDirector.IsComplete) return;
            QuitRequested = true;
            Application.Quit(0);
        }

        public void RequestQuitForAutomation()
        {
            if (matchDirector.IsComplete) QuitRequested = true;
        }

        public void AdvanceMatchClockForAutomation(float seconds) => matchDirector.Advance(seconds);

        public void SetOpponentEnabledForAutomation(bool active)
        {
            if (opponent != null) opponent.enabled = active;
        }

        public void SetOpponentTargetsAvailableForAutomation(bool available) =>
            opponentTargetsAvailable = available;

        public void DestroyHisarForAutomation(bool hostile)
        {
            var target = hostile ? enemyHisar : hisar;
            target?.ApplyStructuralDamage(target.Health);
        }

        private void CreateFogOfWar()
        {
            fogOfWar = gameObject.AddComponent<FogOfWarSystem>();
            fogOfWar.Initialize(tuning.sightRadius, worldCamera.GetComponent<RtsCameraController>(), hudCanvas.transform);
            fogOfWar.HostileFirstRevealed += HandleHostileFirstRevealed;
            fogOfWar.RegisterFriendly(hisar.transform);
            foreach (var worker in workers) fogOfWar.RegisterFriendly(worker.transform);
            fogOfWar.RegisterHostileStatic(enemyHisar.gameObject);
            fogOfWar.RefreshNow();
        }

        private void CreateOpponent()
        {
            opponent = gameObject.AddComponent<ScriptedOpponentController>();
            opponent.Initialize(tuning, enemyHisar, hisar, enemyWorkers, enemyFormations, enemyBuildings,
                enemyCaches, CreateEnemyWorkerForOpponent,
                type => CreateFormation(type, false,
                    new Vector3(-5f + enemyFormations.Count * 5f, 0f, 22f)),
                CreateOpponentBuilding,
                () => friendlyFormations,
                () => MatchElapsedSeconds,
                (phase, elapsed) =>
                {
                    telemetry.RecordAiAttack(phase, elapsed);
                    SetOrderFeedback(phase switch
                    {
                        AiPhase.Probe => "Alazhan Cavalry probe is moving",
                        AiPhase.Pressure => "Alazhan pressure force is moving",
                        _ => "Alazhan final assault is moving"
                    });
                },
                (friendly, detail) => telemetry.RecordEntityProduced(friendly, detail, MatchElapsedSeconds),
                (friendly, detail) => telemetry.RecordBuildingConstructed(friendly, detail, MatchElapsedSeconds));
            opponent.StartEconomy();
        }

        private WorkerAgent CreateEnemyWorkerForOpponent(int slot)
        {
            var column = slot % WorkerCount;
            var row = slot / WorkerCount;
            var position = new Vector3(-2.2f + column * 1.45f, 0f, 22f - row * 1.3f);
            return CreateWorker(false, slot, position, opponent.Wallet, enemyHisar, enemyCaches);
        }

        private ConstructibleBuilding CreateOpponentBuilding(BuildingType type, Vector3 position)
        {
            var root = CreateBuildingVisual(type, $"Alazhan {type} {enemyBuildings.Count + 1}", position, false);
            var obstacle = root.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = new Vector3(0f, 1f, 0f);
            obstacle.size = new Vector3(4f, 2f, 4f);
            obstacle.carving = true;
            var building = type == BuildingType.House
                ? root.AddComponent<HouseBuilding>()
                : root.AddComponent<ConstructibleBuilding>();
            building.Initialize(type, BuildingDuration(type), tuning.buildingHealth,
                type == BuildingType.Watchtower ? new Color(0.58f, 0.09f, 0.04f) : new Color(0.72f, 0.16f, 0.07f),
                DestroyOpponentBuilding, false);
            enemyBuildings.Add(building);
            fogOfWar?.RegisterHostileStatic(root);
            if (type == BuildingType.Watchtower)
                root.AddComponent<WatchtowerAttack>().Initialize(tuning, () => friendlyFormations);
            return building;
        }

        private void DestroyOpponentBuilding(ConstructibleBuilding building)
        {
            if (building == null) return;
            opponent?.NotifyBuildingDestroyed(building);
            enemyBuildings.Remove(building);
            fogOfWar?.UnregisterHostile(building.gameObject);
            telemetry.RecordBuildingDestroyed(false, building.Type.ToString(), MatchElapsedSeconds);
            Destroy(building.gameObject, 0.25f);
        }

        private void HandleWorkerDestroyed(WorkerAgent worker)
        {
            if (worker == null) return;
            var orphanedConstruction = worker.CurrentConstruction;
            if (worker.IsFriendly)
            {
                workers.Remove(worker);
                selectedWorkers.Remove(worker);
                if (population.Used > 0) population.Release(1);
                if (orphanedConstruction != null && !orphanedConstruction.IsComplete)
                {
                    RemoveBuildingFromLists(orphanedConstruction);
                    Destroy(orphanedConstruction.gameObject);
                    SetOrderFeedback($"{orphanedConstruction.Type} abandoned - builder lost, no refund");
                }
            }
            else
            {
                enemyWorkers.Remove(worker);
                if (opponent?.Population != null && opponent.Population.Used > 0) opponent.Population.Release(1);
                fogOfWar?.UnregisterHostile(worker.gameObject);
                if (orphanedConstruction != null && !orphanedConstruction.IsComplete)
                {
                    opponent?.NotifyConstructionAbandoned(orphanedConstruction);
                    enemyBuildings.Remove(orphanedConstruction);
                    fogOfWar?.UnregisterHostile(orphanedConstruction.gameObject);
                    Destroy(orphanedConstruction.gameObject);
                }
            }
            telemetry.RecordEntityLost(worker.IsFriendly, "Worker", MatchElapsedSeconds);
        }

        private void HandleSelectionInput()
        {
            HandleQueuedInput();
            UpdateSelectionGesture();
        }

        private void UpdateSelectionGesture()
        {
            var mouse = Mouse.current;
            if (!selecting) return;
            if (mouse != null && mouse.leftButton.isPressed)
            {
                UpdateSelectionBox(selectionStart, mouse.position.ReadValue());
                return;
            }
            selecting = false;
            selectionBox.gameObject.SetActive(false);
        }

        private void BeginSelection(PointerButtonTransition transition)
        {
            if (selecting)
            {
                selecting = false;
                selectionBox.gameObject.SetActive(false);
            }
            var position = transition.Position;
            if (position.y < Screen.height * 0.16f ||
                position.y > Screen.height * 0.9f || IsPointerOverHud(position)) return;
            selecting = true;
            selectionStart = position;
            selectionBox.gameObject.SetActive(true);
            UpdateSelectionBox(selectionStart, position);
        }

        private void CancelSelectionGesture()
        {
            if (!selecting) return;
            selecting = false;
            selectionBox.gameObject.SetActive(false);
        }

        private void CompleteSelection(PointerButtonTransition transition)
        {
            if (!selecting) return;
            selecting = false;
            selectionBox.gameObject.SetActive(false);
            if (placementWorker != null || awaitingAttackMove || IsPointerOverHud(transition.Position)) return;
            ApplySelection(selectionStart, transition.Position, transition.Modify);
        }

        private void HandleOrderInput()
        {
            HandleQueuedInput();
        }

        private void ApplyOrder(Vector2 position)
        {
            if (selectedWorkers.Count == 0 && selectedFormations.Count == 0) return;
            if (!Physics.Raycast(worldCamera.ScreenPointToRay(position), out var hit, 200f)) return;
            var hostile = hit.collider.GetComponentInParent<FormationAgent>();
            if (hostile != null && !hostile.IsFriendly && selectedFormations.Count > 0)
            {
                foreach (var formation in selectedFormations) formation.IssueFocus(hostile);
                SetOrderFeedback($"Focus {hostile.Type} - {selectedFormations.Count} formation(s)");
                CreateOrderMarker(hostile.transform.position, new Color(1f, 0.22f, 0.1f));
                return;
            }

            var hostileWorker = hit.collider.GetComponentInParent<WorkerAgent>();
            if (hostileWorker != null && !hostileWorker.IsFriendly && selectedFormations.Count > 0)
            {
                foreach (var formation in selectedFormations) formation.IssueFocus(hostileWorker);
                SetOrderFeedback($"Focus worker - {selectedFormations.Count} formation(s)");
                CreateOrderMarker(hostileWorker.transform.position, new Color(1f, 0.22f, 0.1f));
                return;
            }

            ICombatStructure hostileStructure = hit.collider.GetComponentInParent<Hisar>();
            hostileStructure ??= hit.collider.GetComponentInParent<ConstructibleBuilding>();
            if (hostileStructure != null && !hostileStructure.IsFriendly && hostileStructure.IsAttackable &&
                selectedFormations.Count > 0)
            {
                foreach (var formation in selectedFormations) formation.IssueFocus(hostileStructure);
                SetOrderFeedback($"Focus structure - {selectedFormations.Count} formation(s)");
                CreateOrderMarker(hostileStructure.TargetComponent.transform.position, new Color(1f, 0.22f, 0.1f));
                return;
            }

            var cache = hit.collider.GetComponentInParent<ResourceCache>();
            if (cache != null && selectedWorkers.Count > 0 && IsCurrentlyVisible(cache.transform.position))
            {
                if (selectedFormations.Count > 0) IssueFormationGroupOrder(hit.point, false);
                var availableWorkers = selectedWorkers.Where(worker => worker.CurrentConstruction == null).ToList();
                if (availableWorkers.Count == 0)
                {
                    SetOrderFeedback("Cancel construction before issuing another order");
                    return;
                }
                foreach (var worker in availableWorkers) worker.IssueGather(cache);
                SetOrderFeedback($"Gather {cache.name}");
                CreateOrderMarker(cache.transform.position, new Color(0.95f, 0.68f, 0.2f));
                return;
            }
            IssueMoveForSelected(hit.point);
        }

        private void QueueInputEvent(InputEventPtr eventPtr, InputDevice device)
        {
            if (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>()) return;
            if (device is Mouse mouse) QueuePointerEvent(eventPtr, mouse);
            else if (device is Keyboard keyboard) QueueControlGroupEvent(eventPtr, keyboard);
        }

        private void QueuePointerEvent(InputEventPtr eventPtr, Mouse mouse)
        {
            var position = mouse.position.ReadValue();
            if (mouse.position.ReadValueFromEvent(eventPtr, out var eventPosition)) position = eventPosition;
            if (mouse.leftButton.ReadValueFromEvent(eventPtr, out var leftValue))
            {
                var leftPressed = leftValue >= InputSystem.settings.defaultButtonPressPoint;
                if (!mouse.leftButton.isPressed && leftPressed)
                    queuedInputs.Enqueue(QueuedInput.Pointer(InputCommand.LeftPressed, position));
                else if (mouse.leftButton.isPressed && !leftPressed)
                    queuedInputs.Enqueue(QueuedInput.Pointer(InputCommand.LeftReleased, position,
                        Keyboard.current?.shiftKey.isPressed == true));
            }
            if (mouse.rightButton.ReadValueFromEvent(eventPtr, out var rightValue) &&
                !mouse.rightButton.isPressed && rightValue >= InputSystem.settings.defaultButtonPressPoint)
                queuedInputs.Enqueue(QueuedInput.Pointer(InputCommand.RightPressed, position));
        }

        private void QueueControlGroupEvent(InputEventPtr eventPtr, Keyboard keyboard)
        {
            var assigning = IsPressedInEvent(keyboard.leftCtrlKey, eventPtr) ||
                            IsPressedInEvent(keyboard.rightCtrlKey, eventPtr) ||
                            IsPressedInEvent(keyboard.leftMetaKey, eventPtr) ||
                            IsPressedInEvent(keyboard.rightMetaKey, eventPtr);
            for (var index = 0; index < ControlGroupKeys.Length; index++)
            {
                var key = keyboard[ControlGroupKeys[index]];
                if (!key.ReadValueFromEvent(eventPtr, out var value) || key.isPressed ||
                    value < InputSystem.settings.defaultButtonPressPoint) continue;
                queuedInputs.Enqueue(QueuedInput.ControlGroup(index + 1, assigning));
                break;
            }
            QueueKeyPress(eventPtr, keyboard, Key.Escape);
            QueueKeyPress(eventPtr, keyboard, Key.F);
            QueueKeyPress(eventPtr, keyboard, Key.G);
            QueueKeyPress(eventPtr, keyboard, Key.H);
            QueueKeyPress(eventPtr, keyboard, Key.R);
            QueueKeyPress(eventPtr, keyboard, Key.T);
            QueueKeyPress(eventPtr, keyboard, Key.X);
            QueueKeyPress(eventPtr, keyboard, Key.S);
            QueueKeyPress(eventPtr, keyboard, Key.A);
            QueueKeyPress(eventPtr, keyboard, Key.C);
            QueueKeyPress(eventPtr, keyboard, Key.Q);
        }

        private void QueueKeyPress(InputEventPtr eventPtr, Keyboard keyboard, Key key)
        {
            if (WasPressedInEvent(keyboard[key], eventPtr)) queuedInputs.Enqueue(QueuedInput.KeyPress(key));
        }

        private static bool IsPressedInEvent(ButtonControl button, InputEventPtr eventPtr)
        {
            return button.ReadValueFromEvent(eventPtr, out var value)
                ? value >= InputSystem.settings.defaultButtonPressPoint
                : button.isPressed;
        }

        private static bool WasPressedInEvent(ButtonControl button, InputEventPtr eventPtr)
        {
            return button.ReadValueFromEvent(eventPtr, out var value) && !button.isPressed &&
                   value >= InputSystem.settings.defaultButtonPressPoint;
        }

        private void HandleQueuedInput()
        {
            while (queuedInputs.Count > 0)
            {
                var input = queuedInputs.Dequeue();
                switch (input.Command)
                {
                    case InputCommand.LeftPressed:
                    case InputCommand.LeftReleased:
                    case InputCommand.RightPressed:
                        HandlePointerCommand(input);
                        break;
                    case InputCommand.KeyPressed:
                        HandleKeyCommand(input.Key);
                        break;
                    case InputCommand.ControlGroupPressed:
                        if (placementWorker == null && !awaitingAttackMove)
                        {
                            if (input.Assigning) AssignControlGroup(input.Number);
                            else RecallControlGroup(input.Number);
                        }
                        break;
                }
            }
        }

        private void HandlePointerCommand(QueuedInput input)
        {
            if (input.Command == InputCommand.RightPressed)
            {
                CancelSelectionGesture();
                if (placementWorker != null)
                {
                    EndBuildingPlacement($"{placementType} placement cancelled");
                    return;
                }
                if (awaitingAttackMove)
                {
                    awaitingAttackMove = false;
                    SetOrderFeedback("Attack-move cancelled");
                    return;
                }
                if (!IsPointerOverHud(input.Position)) ApplyOrder(input.Position);
                return;
            }

            if (input.Command == InputCommand.LeftReleased)
            {
                if (placementWorker == null && !awaitingAttackMove)
                    CompleteSelection(new PointerButtonTransition(false, input.Position, input.Modify));
                return;
            }

            if (placementWorker != null)
            {
                TryPlaceBuildingAtPointer(input.Position);
                return;
            }
            if (awaitingAttackMove)
            {
                CancelSelectionGesture();
                TryIssueAttackMoveAtPointer(input.Position);
                return;
            }
            BeginSelection(new PointerButtonTransition(true, input.Position, false));
        }

        private void HandleKeyCommand(Key key)
        {
            if (key == Key.Escape)
            {
                CancelSelectionGesture();
                if (placementWorker != null) EndBuildingPlacement($"{placementType} placement cancelled");
                else if (awaitingAttackMove)
                {
                    awaitingAttackMove = false;
                    SetOrderFeedback("Attack-move cancelled");
                }
                return;
            }
            if (placementWorker != null) return;
            if (hisarSelected)
            {
                if (key == Key.Q) TryQueueWorker();
                else if (key == Key.S) TryQueueFormation(FormationType.Spearmen);
                else if (key == Key.A) TryQueueFormation(FormationType.Archers);
                else if (key == Key.C) TryQueueFormation(FormationType.Cavalry);
                else if (key == Key.X) CancelActiveTraining();
                return;
            }
            if (selectedBuilding != null)
            {
                if (key == Key.X) RequestDemolition();
                return;
            }
            if (selectedFormations.Count > 0)
            {
                if (key == Key.F) BeginAttackMoveTargeting();
                else if (key == Key.G) StopSelectedFormations();
            }
            if (selectedWorkers.Count > 0)
            {
                if (key == Key.H) BeginBuildingPlacement(BuildingType.House);
                else if (key == Key.R) BeginBuildingPlacement(BuildingType.Storehouse);
                else if (key == Key.T) BeginBuildingPlacement(BuildingType.Watchtower);
                else if (key == Key.X) CancelSelectedConstruction();
            }
        }

        private void ApplySelection(Vector2 start, Vector2 end, bool modify)
        {
            if (!modify) ClearSelection();
            var dragRect = ScreenRect(start, end);
            if (Vector2.Distance(start, end) < 8f)
            {
                if (Physics.Raycast(worldCamera.ScreenPointToRay(end), out var hit, 200f))
                {
                    var clickedFormation = hit.collider.GetComponentInParent<FormationAgent>();
                    if (clickedFormation != null && clickedFormation.IsFriendly)
                    {
                        if (modify && selectedFormations.Remove(clickedFormation)) clickedFormation.SetSelected(false);
                        else if (clickedFormation == lastClickedFormation &&
                                 Time.unscaledTime - lastFormationClickTime <= 0.35f)
                            SelectVisibleFormationsOfType(clickedFormation.Type);
                        else AddSelectedFormation(clickedFormation);
                        lastClickedFormation = clickedFormation;
                        lastFormationClickTime = Time.unscaledTime;
                        SetOrderFeedback($"Selected {selectedFormations.Count} formation(s)");
                        return;
                    }
                    var clickedBuilding = hit.collider.GetComponentInParent<ConstructibleBuilding>();
                    if (clickedBuilding != null && clickedBuilding.IsFriendly && clickedBuilding.IsComplete &&
                        !clickedBuilding.IsDestroyed)
                    {
                        selectedBuilding = clickedBuilding;
                        clickedBuilding.SetSelected(true);
                        SetOrderFeedback($"Selected {clickedBuilding.Type}");
                        return;
                    }
                    var clickedHisar = hit.collider.GetComponentInParent<Hisar>();
                    if (clickedHisar != null && clickedHisar.IsFriendly)
                    {
                        hisarSelected = true;
                        SetOrderFeedback("Hisar selected - train a formation");
                        return;
                    }
                    var worker = hit.collider.GetComponentInParent<WorkerAgent>();
                    if (worker != null && worker.IsFriendly) ToggleSelection(worker, modify);
                }
            }
            else
            {
                foreach (var worker in workers)
                {
                    var screen = worldCamera.WorldToScreenPoint(worker.transform.position);
                    if (screen.z > 0f && dragRect.Contains(screen)) AddSelection(worker);
                }
                foreach (var formation in friendlyFormations)
                {
                    var screen = worldCamera.WorldToScreenPoint(formation.transform.position);
                    if (screen.z <= 0f || !dragRect.Contains(screen)) continue;
                    AddSelectedFormation(formation);
                }
            }
            if (selectedFormations.Count == 0 && !hisarSelected)
                SetOrderFeedback(selectedWorkers.Count == 0 ? "No selection" : $"Selected {selectedWorkers.Count} worker(s)");
        }

        private void ToggleSelection(WorkerAgent worker, bool modify)
        {
            if (modify && selectedWorkers.Remove(worker)) worker.SetSelected(false);
            else AddSelection(worker);
        }

        private void AddSelection(WorkerAgent worker)
        {
            if (selectedWorkers.Contains(worker)) return;
            selectedWorkers.Add(worker);
            worker.SetSelected(true);
        }

        private void AddSelectedFormation(FormationAgent formation)
        {
            if (formation == null || !formation.IsFriendly || selectedFormations.Contains(formation)) return;
            selectedFormations.Add(formation);
            formation.SetSelected(true);
        }

        private void SelectVisibleFormationsOfType(FormationType type)
        {
            foreach (var formation in friendlyFormations)
            {
                if (formation.Type != type) continue;
                var viewport = worldCamera.WorldToViewportPoint(formation.transform.position);
                if (viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f)
                    AddSelectedFormation(formation);
            }
        }

        private void ClearSelection()
        {
            foreach (var worker in selectedWorkers) worker.SetSelected(false);
            selectedWorkers.Clear();
            foreach (var formation in selectedFormations) formation.SetSelected(false);
            selectedFormations.Clear();
            if (selectedBuilding != null) selectedBuilding.SetSelected(false);
            selectedBuilding = null;
            demolitionCandidate = null;
            hisarSelected = false;
            awaitingAttackMove = false;
        }

        private void UpdateHud()
        {
            if (suppliesText == null) return;
            suppliesText.text = $"SUPPLIES   {Supplies}";
            if (populationText != null) populationText.text = $"POPULATION   {PopulationUsed} / {PopulationCapacity}";
            selectionText.text = selectedFormations.Count > 0
                ? $"{selectedFormations.Count} FORMATION{(selectedFormations.Count == 1 ? string.Empty : "S")}\n" +
                  string.Join("  |  ", selectedFormations.GroupBy(formation => formation.Type)
                      .Select(group => $"{group.Key}: {group.Count()}"))
                : selectedBuilding != null
                    ? $"{selectedBuilding.Type.ToString().ToUpperInvariant()}\n" +
                      $"HEALTH {selectedBuilding.Health} / {selectedBuilding.MaxHealth}"
                : hisarSelected
                    ? "KARASUNGUR HISAR\nSHARED PRODUCTION QUEUE"
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
        }

        private static bool IsPointerOverHud(Vector2 screenPosition)
        {
            if (EventSystem.current == null) return false;
            var pointer = new PointerEventData(EventSystem.current) { position = screenPosition };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            return hits.Any(hit => hit.gameObject.GetComponentInParent<Canvas>() != null);
        }

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
            var obstacle = root.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = new Vector3(0f, 1f, 0f);
            obstacle.size = new Vector3(4f, 2f, 4f);
            obstacle.carving = true;
            var building = type == BuildingType.House
                ? root.AddComponent<HouseBuilding>()
                : root.AddComponent<ConstructibleBuilding>();
            var completeColor = type switch
            {
                BuildingType.House => new Color(0.12f, 0.38f, 0.82f),
                BuildingType.Storehouse => new Color(0.16f, 0.46f, 0.7f),
                _ => new Color(0.08f, 0.32f, 0.66f)
            };
            building.Initialize(type, BuildingDuration(type), tuning.buildingHealth, completeColor,
                DestroyCompletedBuilding);
            fogOfWar?.RegisterFriendly(root.transform);
            if (type == BuildingType.Watchtower)
                root.AddComponent<WatchtowerAttack>().Initialize(tuning,
                    () => enemyFormations.Where(formation => fogOfWar == null || fogOfWar.IsCurrentlyVisible(formation)));
            return building;
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

        private void DestroyCompletedBuilding(ConstructibleBuilding building)
        {
            var type = building.Type;
            if (type == BuildingType.House) population.RemoveCapacity(tuning.housePopulationCapacity);
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

        private void CompleteProduction(ProductionItem item)
        {
            if (item == ProductionItem.Worker)
            {
                var slot = workers.Count == 0 ? 0 : workers.Count;
                var position = new Vector3(-2.2f + slot % WorkerCount * 1.45f, 0f,
                    -4f + slot / WorkerCount * 1.3f);
                var worker = CreateWorker(true, slot, position, wallet, hisar, Caches);
                workers.Add(worker);
                telemetry.RecordEntityProduced(true, ProductionItem.Worker.ToString(), MatchElapsedSeconds);
                SetOrderFeedback("Worker ready");
                return;
            }
            CompleteFormation(item.ToFormationType());
        }

        private void CompleteFormation(FormationType type)
        {
            var friendly = CreateFormation(type, true, new Vector3(-5f + friendlyFormations.Count * 5f, 0f, -1f));
            friendlyFormations.Add(friendly);
            telemetry.RecordEntityProduced(true, type.ToString(), MatchElapsedSeconds);
            SetOrderFeedback($"{type} ready - {friendly.MemberCount} members");
        }

        private FormationAgent CreateFormation(FormationType type, bool friendly, Vector3 position,
            bool trackPopulation = true)
        {
            var root = new GameObject($"{(friendly ? "Karasungur" : "Alazhan")} {type} Formation");
            root.transform.position = position;
            var navAgent = root.AddComponent<NavMeshAgent>();
            navAgent.radius = 0.9f;
            navAgent.height = 2f;
            navAgent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            navAgent.avoidancePriority = friendly ? 40 + friendlyFormations.Count : 60 + enemyFormations.Count;
            var formation = root.AddComponent<FormationAgent>();
            formation.Initialize(type, friendly, tuning,
                amount =>
                {
                    if (!trackPopulation) return;
                    var ledger = friendly ? population : opponent?.Population;
                    if (ledger != null && amount <= ledger.Used) ledger.Release(amount);
                },
                destroyed =>
                {
                    friendlyFormations.Remove(destroyed);
                    enemyFormations.Remove(destroyed);
                    selectedFormations.Remove(destroyed);
                    if (!destroyed.IsFriendly) fogOfWar?.UnregisterHostile(destroyed.gameObject);
                    telemetry.RecordEntityLost(destroyed.IsFriendly, destroyed.Type.ToString(), MatchElapsedSeconds);
                    SetOrderFeedback(destroyed.IsFriendly ? "Friendly formation lost" : "Enemy formation defeated");
                },
                friendly ? () => enemyFormations : () => friendlyFormations,
                friendly ? candidate => fogOfWar == null || fogOfWar.IsCurrentlyVisible(candidate) :
                IsCurrentlyVisibleToHostileSide,
                friendly ? EnemyCombatWorkers : () => workers,
                friendly ? candidate => fogOfWar == null || fogOfWar.IsCurrentlyVisible(candidate) :
                IsCurrentlyVisibleToHostileSide,
                friendly ? EnemyCombatStructures : FriendlyCombatStructures,
                friendly ? structure => fogOfWar == null || fogOfWar.IsCurrentlyVisible(structure.TargetComponent) :
                IsCurrentlyVisibleToHostileSide);
            if (friendly) fogOfWar?.RegisterFriendly(root.transform);
            else fogOfWar?.RegisterHostileMobile(root);
            return formation;
        }

        private IEnumerable<ICombatStructure> FriendlyCombatStructures()
        {
            if (hisar != null && !hisar.IsDestroyed) yield return hisar;
            foreach (var building in buildings)
                if (building != null && !building.IsDestroyed) yield return building;
        }

        private IEnumerable<ICombatStructure> EnemyCombatStructures()
        {
            if (!opponentTargetsAvailable) yield break;
            if (enemyHisar != null && !enemyHisar.IsDestroyed) yield return enemyHisar;
            foreach (var building in enemyBuildings)
                if (building != null && !building.IsDestroyed) yield return building;
        }

        private IEnumerable<WorkerAgent> EnemyCombatWorkers() =>
            opponentTargetsAvailable ? enemyWorkers : Enumerable.Empty<WorkerAgent>();

        private bool IsCurrentlyVisibleToHostileSide(FormationAgent candidate)
        {
            if (candidate == null || candidate.MemberCount == 0) return false;
            return IsCurrentlyVisibleToHostileSide(candidate.transform.position);
        }

        private bool IsCurrentlyVisibleToHostileSide(WorkerAgent candidate)
        {
            if (candidate == null || !candidate.IsAlive) return false;
            return IsCurrentlyVisibleToHostileSide(candidate.transform.position);
        }

        private bool IsCurrentlyVisibleToHostileSide(ICombatStructure candidate)
        {
            if (candidate == null || candidate.TargetComponent == null || !candidate.IsAttackable) return false;
            if (candidate.TargetComponent == hisar) return true;
            return IsCurrentlyVisibleToHostileSide(candidate.TargetComponent.transform.position);
        }

        private bool IsCurrentlyVisibleToHostileSide(Vector3 position)
        {
            var sightRadiusSquared = tuning.sightRadius * tuning.sightRadius;
            if (enemyHisar != null && !enemyHisar.IsDestroyed &&
                (enemyHisar.transform.position - position).sqrMagnitude <= sightRadiusSquared) return true;
            if (enemyWorkers.Any(observer => observer != null && observer.IsAlive &&
                    (observer.transform.position - position).sqrMagnitude <= sightRadiusSquared)) return true;
            if (enemyBuildings.Any(observer => observer != null && !observer.IsDestroyed &&
                    (observer.transform.position - position).sqrMagnitude <= sightRadiusSquared)) return true;
            return enemyFormations.Any(observer => observer != null && observer.MemberCount > 0 &&
                (observer.transform.position - position).sqrMagnitude <= sightRadiusSquared);
        }

        private void HandleHostileFirstRevealed(GameObject target)
        {
            var formation = target == null ? null : target.GetComponent<FormationAgent>();
            if (formation != null && !formation.IsFriendly)
                SetOrderFeedback($"Enemy {formation.Type} sighted");
            telemetry.RecordFirstContact(MatchElapsedSeconds);
        }

        private sealed class ControlGroup
        {
            public ControlGroup(IEnumerable<WorkerAgent> workers, IEnumerable<FormationAgent> formations)
            {
                Workers = workers.ToList();
                Formations = formations.ToList();
            }

            public List<WorkerAgent> Workers { get; }
            public List<FormationAgent> Formations { get; }
        }

        private readonly struct PointerButtonTransition
        {
            public PointerButtonTransition(bool pressed, Vector2 position, bool modify)
            {
                Pressed = pressed;
                Position = position;
                Modify = modify;
            }

            public bool Pressed { get; }
            public Vector2 Position { get; }
            public bool Modify { get; }
        }

        private enum InputCommand
        {
            LeftPressed,
            LeftReleased,
            RightPressed,
            KeyPressed,
            ControlGroupPressed
        }

        private readonly struct QueuedInput
        {
            private QueuedInput(InputCommand command, Vector2 position, bool modify, Key key, int number,
                bool assigning)
            {
                Command = command;
                Position = position;
                Modify = modify;
                Key = key;
                Number = number;
                Assigning = assigning;
            }

            public InputCommand Command { get; }
            public Vector2 Position { get; }
            public bool Modify { get; }
            public Key Key { get; }
            public int Number { get; }
            public bool Assigning { get; }

            public static QueuedInput Pointer(InputCommand command, Vector2 position, bool modify = false) =>
                new(command, position, modify, Key.None, 0, false);

            public static QueuedInput KeyPress(Key key) =>
                new(InputCommand.KeyPressed, default, false, key, 0, false);

            public static QueuedInput ControlGroup(int number, bool assigning) =>
                new(InputCommand.ControlGroupPressed, default, false, Key.None, number, assigning);
        }

        private void TintPreview(Color color)
        {
            foreach (var itemRenderer in placementPreview.GetComponentsInChildren<Renderer>())
                itemRenderer.material.color = color;
        }

        private void SetOrderFeedback(string message)
        {
            if (orderText != null) orderText.text = message.ToUpperInvariant();
        }

        private void NotifyEconomyState(string message)
        {
            LastEconomyNotification = message;
            SetOrderFeedback(message);
        }

        private static GameObject CreatePrimitive(PrimitiveType type, string name, Transform parent,
            Vector3 localPosition, Vector3 scale, Color color)
        {
            var result = GameObject.CreatePrimitive(type);
            result.name = name;
            if (parent != null) result.transform.SetParent(parent, false);
            result.transform.localPosition = localPosition;
            result.transform.localScale = scale;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = new Material(shader) { color = color };
            result.GetComponent<Renderer>().sharedMaterial = material;
            return result;
        }

        private static void CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0.025f, 0.04f, 0.065f, 0.9f);
        }

        private static Text CreateText(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            int fontSize, TextAnchor alignment)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.color = new Color(0.92f, 0.9f, 0.82f);
            text.alignment = alignment;
            return text;
        }

        private static Button CreateButton(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            string label, UnityEngine.Events.UnityAction onClick)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            buttonObject.GetComponent<Image>().color = new Color(0.08f, 0.22f, 0.38f, 0.95f);
            var button = buttonObject.GetComponent<Button>();
            button.onClick.AddListener(onClick);
            CreateText(buttonObject.transform, "Label", Vector2.zero, Vector2.one, 16, TextAnchor.MiddleCenter).text = label;
            return button;
        }

        private static Rect ScreenRect(Vector2 start, Vector2 end)
        {
            return Rect.MinMaxRect(Mathf.Min(start.x, end.x), Mathf.Min(start.y, end.y),
                Mathf.Max(start.x, end.x), Mathf.Max(start.y, end.y));
        }

        private void UpdateSelectionBox(Vector2 start, Vector2 end)
        {
            var rect = ScreenRect(start, end);
            selectionBoxTransform.position = rect.center;
            selectionBoxTransform.sizeDelta = rect.size;
        }

        private static Vector3 FormationOffset(int index, int count)
        {
            var columns = Mathf.CeilToInt(Mathf.Sqrt(count));
            return new Vector3((index % columns - (columns - 1) * 0.5f) * 1.1f, 0f, index / columns * 1.1f);
        }

        private static void CreateOrderMarker(Vector3 position, Color color)
        {
            var marker = CreatePrimitive(PrimitiveType.Cylinder, "Order Marker", null,
                position + Vector3.up * 0.05f, new Vector3(0.65f, 0.025f, 0.65f), color);
            Object.Destroy(marker.GetComponent<Collider>());
            Object.Destroy(marker, 0.8f);
        }
    }
}
