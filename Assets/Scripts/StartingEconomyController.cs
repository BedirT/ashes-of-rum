using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
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
        private Button cancelBuildButton;
        private GameObject placementPreview;
        private WorkerAgent placementWorker;
        private bool placementValid;
        private Vector3 placementPosition;

        public int Supplies => wallet?.Supplies ?? 0;
        public int StartingSupplies => tuning?.startingSupplies ?? 0;
        public int PopulationUsed => population?.Used ?? 0;
        public int PopulationCapacity => population?.Capacity ?? 0;
        public bool IsHousePlacementActive => placementWorker != null;
        public IReadOnlyList<WorkerAgent> Workers => workers;
        public IReadOnlyList<HouseBuilding> Houses => houses;
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
            BuildNavMesh();
            hisar = CreateHisar();
            Caches = new[]
            {
                CreateCache(1, new Vector3(-7f, 0.65f, 4f)),
                CreateCache(2, new Vector3(8f, 0.65f, 6f))
            };
            CreateWorkers();
            CreateHud();
            SetOrderFeedback("Ready - select workers to begin");
        }

        private void Update()
        {
            UpdateHud();
            HandleBuildInput();
            HandleSelectionInput();
            HandleOrderInput();
        }

        public void SelectOnly(WorkerAgent worker)
        {
            ClearSelection();
            if (worker == null) return;
            selectedWorkers.Add(worker);
            worker.SetSelected(true);
        }

        public void IssueGatherForSmoke(ResourceCache cache)
        {
            var worker = workers.First();
            SelectOnly(worker);
            worker.IssueGather(cache);
            SetOrderFeedback("Gathering Supplies");
        }

        public bool TryPlaceHouse(WorkerAgent worker, Vector3 position)
        {
            if (worker == null) return false;
            var snapped = HousePlacementRules.Snap(position);
            if (!CanPlaceHouse(worker, snapped, out var reason))
            {
                SetOrderFeedback(reason);
                return false;
            }
            if (!wallet.TrySpend(tuning.houseCost))
            {
                SetOrderFeedback($"Need {tuning.houseCost} Supplies");
                return false;
            }

            var house = CreateHouse(snapped);
            houses.Add(house);
            worker.IssueConstruct(house, CompleteHouse);
            SetOrderFeedback($"House placed - {tuning.houseCost} Supplies spent");
            return true;
        }

        public bool CancelConstruction(WorkerAgent worker)
        {
            var building = worker?.CurrentConstruction;
            if (building == null || !worker.CancelConstruction()) return false;
            houses.Remove(building);
            Destroy(building.gameObject);
            wallet.Refund(tuning.houseCost);
            SetOrderFeedback($"House cancelled - {tuning.houseCost} Supplies refunded");
            return true;
        }

        private void BuildNavMesh()
        {
            var ground = GameObject.Find("Bootstrap Ground");
            var surface = ground.GetComponent<NavMeshSurface>() ?? ground.AddComponent<NavMeshSurface>();
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.collectObjects = CollectObjects.All;
            surface.BuildNavMesh();
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
                worker.Initialize(tuning, wallet, hisar, Caches, i, NotifyEconomyState);
                workers.Add(worker);
            }
        }

        private void CreateHud()
        {
            var canvasObject = new GameObject("RTS HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            CreatePanel(canvas.transform, "Top Bar", new Vector2(0.02f, 0.91f), new Vector2(0.34f, 0.98f));
            suppliesText = CreateText(canvas.transform, "Supplies", new Vector2(0.035f, 0.925f), new Vector2(0.32f, 0.97f), 28, TextAnchor.MiddleLeft);
            populationText = CreateText(canvas.transform, "Population", new Vector2(0.20f, 0.925f), new Vector2(0.40f, 0.97f), 28, TextAnchor.MiddleLeft);
            CreatePanel(canvas.transform, "Bottom Panel", new Vector2(0.02f, 0.025f), new Vector2(0.98f, 0.15f));
            selectionText = CreateText(canvas.transform, "Selection", new Vector2(0.04f, 0.045f), new Vector2(0.34f, 0.13f), 22, TextAnchor.MiddleLeft);
            orderText = CreateText(canvas.transform, "Order", new Vector2(0.35f, 0.075f), new Vector2(0.57f, 0.13f), 20, TextAnchor.MiddleCenter);
            buildHouseButton = CreateButton(canvas.transform, "Build House", new Vector2(0.59f, 0.05f), new Vector2(0.70f, 0.125f),
                "BUILD HOUSE  [H]", BeginHousePlacement);
            cancelBuildButton = CreateButton(canvas.transform, "Cancel Build", new Vector2(0.71f, 0.05f), new Vector2(0.82f, 0.125f),
                "CANCEL BUILD  [X]", CancelSelectedConstruction);
            CreateText(canvas.transform, "Controls", new Vector2(0.83f, 0.045f), new Vector2(0.96f, 0.13f), 16, TextAnchor.MiddleRight).text =
                "LEFT CLICK / DRAG  Select\nSHIFT  Add/remove   RIGHT CLICK  Move / Gather\nWASD / EDGE / MIDDLE DRAG  Pan   WHEEL  Zoom";

            var boxObject = new GameObject("Selection Box", typeof(RectTransform), typeof(Image));
            boxObject.transform.SetParent(canvas.transform, false);
            selectionBox = boxObject.GetComponent<Image>();
            selectionBox.color = new Color(0.15f, 0.65f, 1f, 0.22f);
            selectionBoxTransform = boxObject.GetComponent<RectTransform>();
            selectionBox.gameObject.SetActive(false);

            var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
            eventSystemObject.transform.SetParent(transform);
            eventSystemObject.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        private void HandleSelectionInput()
        {
            if (placementWorker != null) return;
            var mouse = Mouse.current;
            if (mouse == null) return;
            var position = mouse.position.ReadValue();
            if (!selecting && (position.y < Screen.height * 0.16f || position.y > Screen.height * 0.9f)) return;
            if (mouse.leftButton.wasPressedThisFrame)
            {
                selecting = true;
                selectionStart = position;
                selectionBox.gameObject.SetActive(true);
            }
            if (selecting && mouse.leftButton.isPressed) UpdateSelectionBox(selectionStart, position);
            if (!selecting || !mouse.leftButton.wasReleasedThisFrame) return;
            selecting = false;
            selectionBox.gameObject.SetActive(false);
            ApplySelection(selectionStart, position, Keyboard.current?.shiftKey.isPressed == true);
        }

        private void HandleOrderInput()
        {
            if (placementWorker != null) return;
            var mouse = Mouse.current;
            if (mouse == null || !mouse.rightButton.wasPressedThisFrame || selectedWorkers.Count == 0) return;
            var availableWorkers = selectedWorkers.Where(worker => worker.CurrentConstruction == null).ToList();
            if (availableWorkers.Count == 0)
            {
                SetOrderFeedback("Cancel construction before issuing another order");
                return;
            }
            if (!Physics.Raycast(worldCamera.ScreenPointToRay(mouse.position.ReadValue()), out var hit, 200f)) return;
            var cache = hit.collider.GetComponentInParent<ResourceCache>();
            if (cache != null)
            {
                foreach (var worker in availableWorkers) worker.IssueGather(cache);
                SetOrderFeedback($"Gather {cache.name}");
                CreateOrderMarker(cache.transform.position, new Color(0.95f, 0.68f, 0.2f));
                return;
            }
            for (var i = 0; i < availableWorkers.Count; i++)
            {
                var offset = FormationOffset(i, availableWorkers.Count);
                availableWorkers[i].IssueMove(hit.point + offset);
            }
            SetOrderFeedback("Move");
            CreateOrderMarker(hit.point, new Color(0.2f, 0.78f, 1f));
        }

        private void ApplySelection(Vector2 start, Vector2 end, bool modify)
        {
            if (!modify) ClearSelection();
            var dragRect = ScreenRect(start, end);
            if (Vector2.Distance(start, end) < 8f)
            {
                if (Physics.Raycast(worldCamera.ScreenPointToRay(end), out var hit, 200f))
                {
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
            }
            SetOrderFeedback(selectedWorkers.Count == 0 ? "No workers selected" : $"Selected {selectedWorkers.Count} worker(s)");
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

        private void ClearSelection()
        {
            foreach (var worker in selectedWorkers) worker.SetSelected(false);
            selectedWorkers.Clear();
        }

        private void UpdateHud()
        {
            if (suppliesText == null) return;
            suppliesText.text = $"SUPPLIES   {Supplies}";
            if (populationText != null) populationText.text = $"POPULATION   {PopulationUsed} / {PopulationCapacity}";
            selectionText.text = selectedWorkers.Count == 0
                ? "No workers selected"
                : $"{selectedWorkers.Count} WORKER{(selectedWorkers.Count == 1 ? string.Empty : "S")}\n" +
                  string.Join("  |  ", selectedWorkers.GroupBy(worker => worker.CurrentActivity)
                      .Select(group => $"{group.Key}: {group.Count()}"));
            buildHouseButton.interactable = selectedWorkers.Count > 0 && Supplies >= tuning.houseCost &&
                                            selectedWorkers[0].CurrentConstruction == null;
            cancelBuildButton.interactable = selectedWorkers.Any(worker => worker.CurrentConstruction != null);
        }

        private void HandleBuildInput()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null || mouse == null) return;

            if (placementWorker == null)
            {
                if (keyboard.hKey.wasPressedThisFrame) BeginHousePlacement();
                if (keyboard.xKey.wasPressedThisFrame) CancelSelectedConstruction();
                return;
            }

            if (keyboard.escapeKey.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame)
            {
                EndHousePlacement("House placement cancelled");
                return;
            }
            if (Physics.Raycast(worldCamera.ScreenPointToRay(mouse.position.ReadValue()), out var hit, 200f))
            {
                placementPosition = HousePlacementRules.Snap(hit.point);
                placementPreview.transform.position = placementPosition;
                placementValid = CanPlaceHouse(placementWorker, placementPosition, out var reason);
                TintPreview(placementValid ? new Color(0.2f, 0.8f, 0.35f, 0.55f) : new Color(0.9f, 0.2f, 0.15f, 0.55f));
                SetOrderFeedback(placementValid ? "Place House - left click" : reason);
            }
            if (!mouse.leftButton.wasPressedThisFrame || !placementValid) return;
            var worker = placementWorker;
            var position = placementPosition;
            EndHousePlacement(null);
            TryPlaceHouse(worker, position);
        }

        private void BeginHousePlacement()
        {
            if (selectedWorkers.Count == 0)
            {
                SetOrderFeedback("Select a worker to build");
                return;
            }
            if (Supplies < tuning.houseCost)
            {
                SetOrderFeedback($"Need {tuning.houseCost} Supplies");
                return;
            }
            if (selectedWorkers[0].CurrentConstruction != null)
            {
                SetOrderFeedback("Worker is already constructing");
                return;
            }
            placementWorker = selectedWorkers[0];
            placementPreview = CreateHouseVisual("House Placement Preview", Vector3.zero);
            foreach (var itemCollider in placementPreview.GetComponentsInChildren<Collider>())
                itemCollider.enabled = false;
            TintPreview(new Color(0.9f, 0.2f, 0.15f, 0.55f));
            placementValid = false;
            SetOrderFeedback("Place House - left click / right click cancel");
        }

        private void EndHousePlacement(string feedback)
        {
            if (placementPreview != null) Destroy(placementPreview);
            placementPreview = null;
            placementWorker = null;
            placementValid = false;
            if (!string.IsNullOrEmpty(feedback)) SetOrderFeedback(feedback);
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

        private bool CanPlaceHouse(WorkerAgent worker, Vector3 position, out string reason)
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
            if (!worker.CanReach(position + Vector3.back * 2.4f))
            {
                reason = "Invalid - worker cannot reach";
                return false;
            }
            var overlaps = Physics.OverlapBox(position + Vector3.up, new Vector3(2f, 0.9f, 2f));
            if (overlaps.Any(item => item.gameObject.name != "Bootstrap Ground" &&
                                     item.GetComponentInParent<WorkerAgent>() == null))
            {
                reason = "Invalid - position occupied";
                return false;
            }
            if (!HousePlacementRules.PreservesRoute(houses.Select(house => house.transform.position).ToList(), position))
            {
                reason = "Invalid - must preserve a route";
                return false;
            }
            reason = null;
            return true;
        }

        private HouseBuilding CreateHouse(Vector3 position)
        {
            var root = CreateHouseVisual($"House {houses.Count + 1}", position);
            var obstacle = root.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = new Vector3(0f, 1f, 0f);
            obstacle.size = new Vector3(4f, 2f, 4f);
            obstacle.carving = true;
            var house = root.AddComponent<HouseBuilding>();
            house.Initialize(tuning.houseBuildSeconds);
            return house;
        }

        private static GameObject CreateHouseVisual(string name, Vector3 position)
        {
            var root = new GameObject(name);
            root.transform.position = position;
            CreatePrimitive(PrimitiveType.Cube, "House Walls", root.transform,
                new Vector3(0f, 0.8f, 0f), new Vector3(3.6f, 1.6f, 3.6f), new Color(0.42f, 0.55f, 0.68f));
            var roof = CreatePrimitive(PrimitiveType.Cylinder, "House Roof", root.transform,
                new Vector3(0f, 1.9f, 0f), new Vector3(2.4f, 0.45f, 2.4f), new Color(0.16f, 0.28f, 0.48f));
            roof.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            return root;
        }

        private void CompleteHouse(HouseBuilding house)
        {
            population.AddCapacity(tuning.housePopulationCapacity);
            SetOrderFeedback($"House complete - population cap {PopulationCapacity}");
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
