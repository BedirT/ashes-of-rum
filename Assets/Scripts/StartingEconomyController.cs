using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace AshesOfRum
{
    public sealed class StartingEconomyController : MonoBehaviour
    {
        public const string ControllerObjectName = "Starting Economy";
        public const string HisarObjectName = "Karasungur Hisar";
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
        private readonly List<FormationAgent> selectedFormations = new();
        private readonly Dictionary<int, ControlGroup> controlGroups = new();
        private readonly Queue<PointerButtonTransition> selectionTransitions = new();
        private readonly Queue<PointerPress> orderPresses = new();
        [SerializeField] private EconomyTuning tuning;
        private EconomyWallet wallet;
        private Hisar hisar;
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
        private Button trainArchersButton;
        private Button trainCavalryButton;
        private Button cancelTrainingButton;
        private Button attackMoveButton;
        private Button stopFormationsButton;
        private Text queueText;
        private Canvas hudCanvas;
        private FormationProductionQueue productionQueue;
        private FogOfWarSystem fogOfWar;
        private ConstructibleBuilding selectedBuilding;
        private bool hisarSelected;
        private bool firstEnemyDeployed;
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
        private FormationAgent lastClickedFormation;
        private float lastFormationClickTime = float.NegativeInfinity;
        private bool controlGroupKeyHandled;

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
        public IReadOnlyList<WorkerAgent> Workers => workers;
        public IReadOnlyList<HouseBuilding> Houses => houses;
        public IReadOnlyList<ConstructibleBuilding> Storehouses => storehouses;
        public IReadOnlyList<ConstructibleBuilding> Watchtowers => watchtowers;
        public IReadOnlyList<FormationAgent> FriendlyFormations => friendlyFormations;
        public IReadOnlyList<FormationAgent> EnemyFormations => enemyFormations;
        public IReadOnlyList<FormationAgent> SelectedFormations => selectedFormations;
        public FogOfWarSystem FogOfWar => fogOfWar;
        public int ProductionQueueCount => productionQueue?.Count ?? 0;
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

            worldCamera = Camera.main;
            wallet = new EconomyWallet(tuning.startingSupplies);
            population = new PopulationLedger(WorkerCount, tuning.startingPopulationCap, tuning.hardPopulationCap);
            productionQueue = new FormationProductionQueue(wallet, population, tuning.formationCost,
                tuning.formationPopulation, tuning.formationTrainSeconds);
            BuildNavMesh();
            hisar = CreateHisar();
            Caches = new[]
            {
                CreateCache(1, new Vector3(-7f, 0.65f, 4f)),
                CreateCache(2, new Vector3(8f, 0.65f, 6f))
            };
            CreateWorkers();
            CreateHud();
            InputSystem.onEvent += QueuePointerEvent;
            CreateFogOfWar();
            SetOrderFeedback("Ready - select workers to begin");
        }

        private void Update()
        {
            UpdateHud();
            HandleBuildInput();
            HandleControlGroupInput();
            HandleFormationCommandInput();
            HandleSelectionInput();
            HandleOrderInput();
            var completed = productionQueue.Advance(Time.deltaTime);
            if (completed.HasValue) CompleteFormation(completed.Value);
        }

        private void OnDestroy()
        {
            if (fogOfWar != null) fogOfWar.HostileFirstRevealed -= HandleHostileFirstRevealed;
            InputSystem.onEvent -= QueuePointerEvent;
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
            if (!productionQueue.TryEnqueue(type))
            {
                SetOrderFeedback(Supplies < tuning.formationCost
                    ? $"Need {tuning.formationCost} Supplies"
                    : $"Population blocked - need {tuning.formationPopulation} free");
                return false;
            }
            SetOrderFeedback($"{type} queued");
            return true;
        }

        public bool CancelActiveTraining()
        {
            if (!productionQueue.CancelActive()) return false;
            SetOrderFeedback($"Training cancelled - {tuning.formationCost} Supplies refunded");
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
            var enemy = CreateFormation(type, false, position);
            enemyFormations.Add(enemy);
            return enemy;
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

        private Hisar CreateHisar()
        {
            var root = new GameObject(HisarObjectName);
            root.transform.SetPositionAndRotation(new Vector3(0f, 0f, -8f), Quaternion.identity);
            CreatePrimitive(PrimitiveType.Cube, "Hisar Keep", root.transform,
                new Vector3(0f, 1.4f, 0f), new Vector3(5f, 2.8f, 4f), new Color(0.08f, 0.24f, 0.55f));
            CreatePrimitive(PrimitiveType.Cylinder, "Black Falcon Marker", root.transform,
                new Vector3(0f, 3.15f, 0f), new Vector3(1.2f, 0.15f, 1.2f), new Color(0.03f, 0.05f, 0.08f));
            var obstacle = root.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = new Vector3(0f, 1.5f, 0f);
            obstacle.size = new Vector3(5.2f, 3f, 4.2f);
            obstacle.carving = true;
            return root.AddComponent<Hisar>();
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
            {
                var workerObject = new GameObject($"Worker {i + 1}");
                workerObject.transform.position = positions[i];
                CreatePrimitive(PrimitiveType.Capsule, "Worker Body", workerObject.transform,
                    new Vector3(0f, 0.9f, 0f), new Vector3(0.72f, 0.9f, 0.72f), new Color(0.12f, 0.42f, 0.92f));
                var navAgent = workerObject.AddComponent<NavMeshAgent>();
                navAgent.radius = 0.36f;
                navAgent.height = 1.8f;
                navAgent.avoidancePriority = 40 + i;
                CreatePrimitive(PrimitiveType.Cylinder, "Selection Ring", workerObject.transform,
                    new Vector3(0f, 0.04f, 0f), new Vector3(1.25f, 0.025f, 1.25f), new Color(0.2f, 0.78f, 1f));
                CreatePrimitive(PrimitiveType.Cube, "Worker Shape Marker", workerObject.transform,
                    new Vector3(0f, 2.05f, 0f), new Vector3(0.32f, 0.32f, 0.32f), Color.white).transform.rotation = Quaternion.Euler(0f, 45f, 45f);
                CreatePrimitive(PrimitiveType.Cube, "Carried Supplies", workerObject.transform,
                    new Vector3(0f, 1.35f, -0.62f), new Vector3(0.42f, 0.42f, 0.42f), new Color(0.95f, 0.68f, 0.2f));
                var worker = workerObject.AddComponent<WorkerAgent>();
                worker.Initialize(tuning, wallet, hisar, Caches, i, NotifyEconomyState, FindNearestDropOff,
                    IsCurrentlyVisible);
                workers.Add(worker);
            }
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
            buildHouseButton = CreateButton(hudCanvas.transform, "Build House", new Vector2(0.58f, 0.05f), new Vector2(0.68f, 0.125f),
                $"HOUSE {tuning.houseCost} [H]", () => BeginBuildingPlacement(BuildingType.House));
            buildStorehouseButton = CreateButton(hudCanvas.transform, "Build Storehouse", new Vector2(0.68f, 0.05f), new Vector2(0.78f, 0.125f),
                $"STOREHOUSE {tuning.storehouseCost} [R]", () => BeginBuildingPlacement(BuildingType.Storehouse));
            buildWatchtowerButton = CreateButton(hudCanvas.transform, "Build Watchtower", new Vector2(0.78f, 0.05f), new Vector2(0.88f, 0.125f),
                $"WATCHTOWER {tuning.watchtowerCost} [T]", () => BeginBuildingPlacement(BuildingType.Watchtower));
            cancelBuildButton = CreateButton(hudCanvas.transform, "Cancel Build", new Vector2(0.88f, 0.05f), new Vector2(0.98f, 0.125f),
                "CANCEL BUILD  [X]", CancelSelectedConstruction);
            demolishButton = CreateButton(hudCanvas.transform, "Demolish Building", new Vector2(0.78f, 0.05f), new Vector2(0.98f, 0.125f),
                "DEMOLISH [X]", () => RequestDemolition());
            trainSpearmenButton = CreateButton(hudCanvas.transform, "Train Spearmen", new Vector2(0.58f, 0.05f), new Vector2(0.68f, 0.125f),
                $"SPEARMEN {tuning.formationCost} [S]", () => TryQueueFormation(FormationType.Spearmen));
            trainArchersButton = CreateButton(hudCanvas.transform, "Train Archers", new Vector2(0.68f, 0.05f), new Vector2(0.78f, 0.125f),
                $"ARCHERS {tuning.formationCost} [A]", () => TryQueueFormation(FormationType.Archers));
            trainCavalryButton = CreateButton(hudCanvas.transform, "Train Cavalry", new Vector2(0.78f, 0.05f), new Vector2(0.88f, 0.125f),
                $"CAVALRY {tuning.formationCost} [C]", () => TryQueueFormation(FormationType.Cavalry));
            cancelTrainingButton = CreateButton(hudCanvas.transform, "Cancel Training", new Vector2(0.88f, 0.05f), new Vector2(0.98f, 0.125f),
                "CANCEL [X]", () => CancelActiveTraining());
            attackMoveButton = CreateButton(hudCanvas.transform, "Attack Move", new Vector2(0.68f, 0.05f), new Vector2(0.83f, 0.125f),
                "ATTACK-MOVE [F]", BeginAttackMoveTargeting);
            stopFormationsButton = CreateButton(hudCanvas.transform, "Stop Formations", new Vector2(0.83f, 0.05f), new Vector2(0.98f, 0.125f),
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
        }

        private void CreateFogOfWar()
        {
            fogOfWar = gameObject.AddComponent<FogOfWarSystem>();
            fogOfWar.Initialize(tuning.sightRadius, worldCamera.GetComponent<RtsCameraController>(), hudCanvas.transform);
            fogOfWar.HostileFirstRevealed += HandleHostileFirstRevealed;
            fogOfWar.RegisterFriendly(hisar.transform);
            foreach (var worker in workers) fogOfWar.RegisterFriendly(worker.transform);
            fogOfWar.RefreshNow();
        }

        private void HandleSelectionInput()
        {
            while (selectionTransitions.Count > 0)
            {
                var transition = selectionTransitions.Dequeue();
                if (transition.Pressed) BeginSelection(transition);
                else CompleteSelection(transition);
            }

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
            if (transition.Blocked || position.y < Screen.height * 0.16f ||
                position.y > Screen.height * 0.9f || IsPointerOverHud(position)) return;
            selecting = true;
            selectionStart = position;
            selectionBox.gameObject.SetActive(true);
            UpdateSelectionBox(selectionStart, position);
        }

        private void CompleteSelection(PointerButtonTransition transition)
        {
            if (!selecting) return;
            selecting = false;
            selectionBox.gameObject.SetActive(false);
            if (transition.Blocked || placementWorker != null || awaitingAttackMove ||
                IsPointerOverHud(transition.Position)) return;
            ApplySelection(selectionStart, transition.Position, transition.Modify);
        }

        private void HandleOrderInput()
        {
            while (orderPresses.Count > 0)
            {
                var press = orderPresses.Dequeue();
                if (press.Blocked || placementWorker != null || awaitingAttackMove ||
                    IsPointerOverHud(press.Position)) continue;
                ApplyOrder(press.Position);
            }
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

        private void QueuePointerEvent(InputEventPtr eventPtr, InputDevice device)
        {
            if (device is not Mouse mouse ||
                (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>())) return;

            var position = mouse.position.ReadValue();
            if (mouse.position.ReadValueFromEvent(eventPtr, out var eventPosition)) position = eventPosition;
            var blocked = placementWorker != null || awaitingAttackMove;
            if (mouse.leftButton.ReadValueFromEvent(eventPtr, out var leftValue))
            {
                var leftPressed = leftValue >= InputSystem.settings.defaultButtonPressPoint;
                if (!mouse.leftButton.isPressed && leftPressed)
                    selectionTransitions.Enqueue(new PointerButtonTransition(true, position, false, blocked));
                else if (mouse.leftButton.isPressed && !leftPressed)
                    selectionTransitions.Enqueue(new PointerButtonTransition(false, position,
                        Keyboard.current?.shiftKey.isPressed == true, blocked));
            }
            if (mouse.rightButton.ReadValueFromEvent(eventPtr, out var rightValue) &&
                !mouse.rightButton.isPressed && rightValue >= InputSystem.settings.defaultButtonPressPoint)
                orderPresses.Enqueue(new PointerPress(position, blocked));
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
                    if (clickedBuilding != null && clickedBuilding.IsComplete && !clickedBuilding.IsDestroyed)
                    {
                        selectedBuilding = clickedBuilding;
                        clickedBuilding.SetSelected(true);
                        SetOrderFeedback($"Selected {clickedBuilding.Type}");
                        return;
                    }
                    if (hit.collider.GetComponentInParent<Hisar>() != null)
                    {
                        hisarSelected = true;
                        SetOrderFeedback("Hisar selected - train a formation");
                        return;
                    }
                    var worker = hit.collider.GetComponentInParent<WorkerAgent>();
                    if (worker != null) ToggleSelection(worker, modify);
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
            buildHouseButton.interactable = placementWorker == null && selectedWorkers.Count > 0 && Supplies >= tuning.houseCost &&
                                            selectedWorkers[0].CurrentConstruction == null;
            buildStorehouseButton.interactable = placementWorker == null && selectedWorkers.Count > 0 && Supplies >= tuning.storehouseCost &&
                                                 selectedWorkers[0].CurrentConstruction == null;
            buildWatchtowerButton.interactable = placementWorker == null && selectedWorkers.Count > 0 && Supplies >= tuning.watchtowerCost &&
                                                 selectedWorkers[0].CurrentConstruction == null;
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
            trainArchersButton.gameObject.SetActive(hisarSelected);
            trainCavalryButton.gameObject.SetActive(hisarSelected);
            cancelTrainingButton.gameObject.SetActive(hisarSelected);
            var canTrain = Supplies >= tuning.formationCost && PopulationCapacity - PopulationUsed >= tuning.formationPopulation;
            trainSpearmenButton.interactable = canTrain;
            trainArchersButton.interactable = canTrain;
            trainCavalryButton.interactable = canTrain;
            cancelTrainingButton.interactable = productionQueue.Count > 0;
            attackMoveButton.gameObject.SetActive(selectedFormations.Count > 0);
            stopFormationsButton.gameObject.SetActive(selectedFormations.Count > 0);
            attackMoveButton.interactable = !awaitingAttackMove;
            stopFormationsButton.interactable = selectedFormations.Count > 0;
        }

        private void HandleBuildInput()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null || mouse == null) return;

            if (placementWorker == null)
            {
                if (hisarSelected)
                {
                    if (keyboard.sKey.wasPressedThisFrame) TryQueueFormation(FormationType.Spearmen);
                    if (keyboard.aKey.wasPressedThisFrame) TryQueueFormation(FormationType.Archers);
                    if (keyboard.cKey.wasPressedThisFrame) TryQueueFormation(FormationType.Cavalry);
                    if (keyboard.xKey.wasPressedThisFrame) CancelActiveTraining();
                    return;
                }
                if (selectedBuilding != null)
                {
                    if (keyboard.xKey.wasPressedThisFrame) RequestDemolition();
                    return;
                }
                if (selectedFormations.Count > 0)
                {
                    if (keyboard.fKey.wasPressedThisFrame) BeginAttackMoveTargeting();
                    if (keyboard.gKey.wasPressedThisFrame) StopSelectedFormations();
                    return;
                }
                if (keyboard.hKey.wasPressedThisFrame) BeginBuildingPlacement(BuildingType.House);
                if (keyboard.rKey.wasPressedThisFrame) BeginBuildingPlacement(BuildingType.Storehouse);
                if (keyboard.tKey.wasPressedThisFrame) BeginBuildingPlacement(BuildingType.Watchtower);
                if (keyboard.xKey.wasPressedThisFrame) CancelSelectedConstruction();
                return;
            }

            if (keyboard.escapeKey.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame)
            {
                EndBuildingPlacement($"{placementType} placement cancelled");
                return;
            }
            var pointerPosition = mouse.position.ReadValue();
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
            if (!mouse.leftButton.wasPressedThisFrame || !placementValid) return;
            var worker = placementWorker;
            var position = placementPosition;
            var type = placementType;
            EndBuildingPlacement(null);
            TryPlaceBuilding(worker, type, position);
        }

        private void BeginBuildingPlacement(BuildingType type)
        {
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
            if (selectedWorkers[0].CurrentConstruction != null)
            {
                SetOrderFeedback("Worker is already constructing");
                return;
            }
            if (placementPreview != null) Destroy(placementPreview);
            placementWorker = selectedWorkers[0];
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
            awaitingAttackMove = true;
            SetOrderFeedback("Attack-move - left click ground / right click cancel");
        }

        private bool HandleFormationCommandInput()
        {
            if (!awaitingAttackMove) return false;
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            if (mouse == null) return true;
            if (mouse.rightButton.wasPressedThisFrame || keyboard?.escapeKey.wasPressedThisFrame == true)
            {
                awaitingAttackMove = false;
                SetOrderFeedback("Attack-move cancelled");
                return true;
            }
            if (!mouse.leftButton.wasPressedThisFrame) return true;
            var pointer = mouse.position.ReadValue();
            if (IsPointerOverHud(pointer)) return true;
            if (!Physics.Raycast(worldCamera.ScreenPointToRay(pointer), out var hit, 200f)) return true;
            IssueFormationGroupOrder(hit.point, true);
            CreateOrderMarker(hit.point, new Color(1f, 0.55f, 0.12f));
            awaitingAttackMove = false;
            return true;
        }

        private void HandleControlGroupInput()
        {
            if (placementWorker != null || awaitingAttackMove) return;
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            var assigning = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed ||
                            keyboard.leftMetaKey.isPressed || keyboard.rightMetaKey.isPressed;
            var anyNumberHeld = false;
            for (var index = 0; index < ControlGroupKeys.Length; index++)
            {
                var key = keyboard[ControlGroupKeys[index]];
                anyNumberHeld |= key.isPressed;
                if ((!key.wasPressedThisFrame && !key.isPressed) || controlGroupKeyHandled) continue;
                var groupNumber = index + 1;
                if (assigning) AssignControlGroup(groupNumber);
                else RecallControlGroup(groupNumber);
                controlGroupKeyHandled = true;
                return;
            }
            if (!anyNumberHeld) controlGroupKeyHandled = false;
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

        private static GameObject CreateBuildingVisual(BuildingType type, string name, Vector3 position)
        {
            var root = new GameObject(name);
            root.transform.position = position;
            if (type == BuildingType.House)
            {
                CreatePrimitive(PrimitiveType.Cube, "House Walls", root.transform,
                    new Vector3(0f, 0.8f, 0f), new Vector3(3.6f, 1.6f, 3.6f), new Color(0.42f, 0.55f, 0.68f));
                var roof = CreatePrimitive(PrimitiveType.Cylinder, "House Roof", root.transform,
                    new Vector3(0f, 1.9f, 0f), new Vector3(2.4f, 0.45f, 2.4f), new Color(0.16f, 0.28f, 0.48f));
                roof.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            }
            else if (type == BuildingType.Storehouse)
            {
                CreatePrimitive(PrimitiveType.Cube, "Storehouse Walls", root.transform,
                    new Vector3(0f, 0.75f, 0f), new Vector3(3.8f, 1.5f, 3.8f), new Color(0.42f, 0.55f, 0.68f));
                for (var i = -1; i <= 1; i++)
                    CreatePrimitive(PrimitiveType.Cube, $"Stored Supply {i + 2}", root.transform,
                        new Vector3(i * 0.8f, 1.75f, 0f), new Vector3(0.65f, 0.65f, 0.65f),
                        new Color(0.75f, 0.52f, 0.22f));
            }
            else
            {
                CreatePrimitive(PrimitiveType.Cylinder, "Watchtower Base", root.transform,
                    new Vector3(0f, 1.6f, 0f), new Vector3(1.5f, 1.6f, 1.5f), new Color(0.42f, 0.55f, 0.68f));
                CreatePrimitive(PrimitiveType.Cube, "Watchtower Platform", root.transform,
                    new Vector3(0f, 3.35f, 0f), new Vector3(3.2f, 0.5f, 3.2f), new Color(0.16f, 0.28f, 0.48f));
            }
            var ring = CreatePrimitive(PrimitiveType.Cylinder, "Building Selection Ring", root.transform,
                new Vector3(0f, 0.04f, 0f), new Vector3(4.4f, 0.025f, 4.4f), new Color(0.2f, 0.78f, 1f));
            Destroy(ring.GetComponent<Collider>());
            ring.AddComponent<BuildingSelectionRing>();
            ring.SetActive(false);
            return root;
        }

        private void CompleteBuilding(ConstructibleBuilding building)
        {
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
            SetOrderFeedback($"{type} {(building.WasDemolished ? "demolished" : "destroyed")} - no refund");
        }

        private void RemoveBuildingFromLists(ConstructibleBuilding building)
        {
            if (buildings.Remove(building)) buildingRouteVersion++;
            if (building is HouseBuilding house) houses.Remove(house);
            storehouses.Remove(building);
            watchtowers.Remove(building);
        }

        private void CompleteFormation(FormationType type)
        {
            var friendly = CreateFormation(type, true, new Vector3(-5f + friendlyFormations.Count * 5f, 0f, -1f));
            friendlyFormations.Add(friendly);
            SetOrderFeedback($"{type} ready - {friendly.MemberCount} members");
            if (firstEnemyDeployed) return;
            firstEnemyDeployed = true;
            var enemyType = type switch
            {
                FormationType.Cavalry => FormationType.Archers,
                FormationType.Spearmen => FormationType.Cavalry,
                _ => FormationType.Spearmen
            };
            var enemy = CreateFormation(enemyType, false, new Vector3(0f, 0f, 17f));
            enemyFormations.Add(enemy);
        }

        private FormationAgent CreateFormation(FormationType type, bool friendly, Vector3 position)
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
                friendly ? amount => population.Release(amount) : null,
                destroyed =>
                {
                    friendlyFormations.Remove(destroyed);
                    enemyFormations.Remove(destroyed);
                    selectedFormations.Remove(destroyed);
                    if (!destroyed.IsFriendly) fogOfWar?.UnregisterHostile(destroyed.gameObject);
                    SetOrderFeedback(destroyed.IsFriendly ? "Friendly formation lost" : "Enemy formation defeated");
                },
                friendly ? () => enemyFormations : () => friendlyFormations,
                friendly ? candidate => fogOfWar == null || fogOfWar.IsCurrentlyVisible(candidate) :
                IsCurrentlyVisibleToHostileSide);
            if (friendly) fogOfWar?.RegisterFriendly(root.transform);
            else fogOfWar?.RegisterHostileMobile(root);
            return formation;
        }

        private bool IsCurrentlyVisibleToHostileSide(FormationAgent candidate)
        {
            if (candidate == null || candidate.MemberCount == 0) return false;
            var sightRadiusSquared = tuning.sightRadius * tuning.sightRadius;
            return enemyFormations.Any(observer => observer != null && observer.MemberCount > 0 &&
                (observer.transform.position - candidate.transform.position).sqrMagnitude <= sightRadiusSquared);
        }

        private void HandleHostileFirstRevealed(GameObject target)
        {
            var formation = target == null ? null : target.GetComponent<FormationAgent>();
            if (formation == null || formation.IsFriendly) return;
            SetOrderFeedback($"Enemy {formation.Type} sighted");
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
            public PointerButtonTransition(bool pressed, Vector2 position, bool modify, bool blocked)
            {
                Pressed = pressed;
                Position = position;
                Modify = modify;
                Blocked = blocked;
            }

            public bool Pressed { get; }
            public Vector2 Position { get; }
            public bool Modify { get; }
            public bool Blocked { get; }
        }

        private readonly struct PointerPress
        {
            public PointerPress(Vector2 position, bool blocked)
            {
                Position = position;
                Blocked = blocked;
            }

            public Vector2 Position { get; }
            public bool Blocked { get; }
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
