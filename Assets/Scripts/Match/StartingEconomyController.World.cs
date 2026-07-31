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
            root.transform.SetPositionAndRotation(new Vector3(0f, 0f, friendly ? -8f : 26f),
                friendly ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity);
            HisarPresentation.Create(root.transform, HisarBuildState.Complete, friendly);
            var collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, HisarPresentation.FootprintSize.y * 0.5f, 0f);
            collider.size = HisarPresentation.FootprintSize;
            CreatePrimitive(friendly ? PrimitiveType.Cylinder : PrimitiveType.Cube,
                friendly ? "Black Falcon Marker" : "Living Flame Marker", root.transform,
                new Vector3(0f, 4f, 0f), new Vector3(1.2f, 0.15f, 1.2f),
                friendly ? new Color(0.03f, 0.05f, 0.08f) : new Color(1f, 0.45f, 0.08f));
            var obstacle = root.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = collider.center;
            obstacle.size = collider.size;
            obstacle.carving = true;
            var result = root.AddComponent<Hisar>();
            result.Initialize(friendly, tuning.hisarHealth, HandleHisarDestroyed,
                friendly ? HandleFriendlyUnderAttack : position => PlayWorldCue(GameplayCue.Hit, position, false));
            AttachHealthBar(root, () => result.Health, () => result.MaxHealth, 4.25f, friendly);
            return result;
        }

        private ResourceCache CreateCache(int index, Vector3 position)
        {
            var root = new GameObject($"{CachePrefix} {index}");
            root.transform.position = position;
            var cache = root.AddComponent<ResourceCache>();
            for (var i = 0; i < 5; i++)
            {
                var offset = new Vector3((i % 3 - 1) * 0.65f, i % 2 * 0.22f, (i / 3 - 0.5f) * 0.65f);
                CreatePrimitive(PrimitiveType.Cube, $"Supply Bundle {i + 1}", root.transform, offset,
                    new Vector3(0.55f, 0.45f, 0.55f), ResourceCache.AvailableColor);
            }
            var collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.35f, 0f);
            collider.size = new Vector3(2.8f, 1.4f, 2f);
            cache.Initialize(tuning.cacheSupplies);
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
                workers.Add(CreateWorker(true, i, positions[i], wallet, hisar, allCaches));
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
                friendly ? FindNearestDropOff : FindNearestEnemyDropOff,
                friendly ? IsCurrentlyVisible : IsCurrentlyVisibleToHostileSide,
                friendly,
                amount => telemetry.RecordSupplies(friendly, amount, MatchElapsedSeconds),
                HandleWorkerDestroyed,
                friendly ? HandleFriendlyUnderAttack : position => PlayWorldCue(GameplayCue.Hit, position, false),
                friendly ? null : worker => opponent?.NotifyGatheringRouteFailed(worker));
            AttachHealthBar(workerObject, () => worker.Health, () => worker.MaxHealth, 2.55f, friendly);
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
            PlayCue(outcome == MatchOutcome.Victory ? GameplayCue.Victory : GameplayCue.Defeat);
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
            Debug.Log("MATCH_QUIT:REQUESTED");
            Application.Quit(0);
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
            foreach (var cache in allCaches) fogOfWar.RegisterNeutralStatic(cache.gameObject);
            fogOfWar.RegisterHostileStatic(enemyHisar.gameObject);
            fogOfWar.RefreshNow();
        }

        private void CreateOpponent()
        {
            opponent = gameObject.AddComponent<ScriptedOpponentController>();
            opponent.Initialize(tuning, enemyHisar, hisar, enemyWorkers, enemyFormations, enemyBuildings,
                allCaches, enemyCaches, CreateEnemyWorkerForOpponent,
                type => CreateFormation(type, false,
                    new Vector3(-5f + enemyFormations.Count * 5f, 0f, 22f)),
                CreateOpponentBuilding,
                () => friendlyFormations,
                IsCurrentlyVisibleToHostileSide,
                IsCurrentlyVisibleToHostileSide,
                () => MatchElapsedSeconds,
                (phase, elapsed) =>
                {
                    telemetry.RecordAiAttack(phase, elapsed);
                    PlayCue(GameplayCue.Warning);
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
            return CreateWorker(false, slot, position, opponent.Wallet, enemyHisar, allCaches);
        }

        private ConstructibleBuilding CreateOpponentBuilding(BuildingType type, Vector3 position)
        {
            var root = CreateBuildingVisual(type, $"Alazhan {type} {enemyBuildings.Count + 1}", position, false);
            var building = AddBuildingComponents(root, type);
            building.Initialize(type, BuildingDuration(type), tuning.buildingHealth,
                type == BuildingType.Watchtower ? new Color(0.58f, 0.09f, 0.04f) : new Color(0.72f, 0.16f, 0.07f),
                DestroyOpponentBuilding, false, position => PlayWorldCue(GameplayCue.Hit, position, false));
            AttachHealthBar(root, () => building.Health, () => building.MaxHealth,
                type == BuildingType.Watchtower ? 4.4f : 2.9f, false);
            enemyBuildings.Add(building);
            fogOfWar?.RegisterHostileStatic(root);
            if (type == BuildingType.Watchtower)
                root.AddComponent<WatchtowerAttack>().Initialize(tuning, () => friendlyFormations,
                    position => PlayWorldCue(GameplayCue.Attack, position, false));
            return building;
        }

        private void DestroyOpponentBuilding(ConstructibleBuilding building)
        {
            if (building == null) return;
            if (!building.IsComplete)
                foreach (var worker in enemyWorkers.Where(worker => worker != null &&
                             ReferenceEquals(worker.CurrentConstruction, building)))
                    worker.CancelConstruction();
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
            var constructionAbandoned = orphanedConstruction != null && !orphanedConstruction.IsComplete &&
                                        !orphanedConstruction.IsDestroyed &&
                                        orphanedConstruction.ApplyStructuralDamage(orphanedConstruction.Health);
            if (worker.IsFriendly)
            {
                workers.Remove(worker);
                selectedWorkers.Remove(worker);
                if (population.Used > 0) population.Release(1);
                if (constructionAbandoned)
                    SetOrderFeedback($"{orphanedConstruction.Type} abandoned - builder lost, no refund");
            }
            else
            {
                enemyWorkers.Remove(worker);
                if (opponent?.Population != null && opponent.Population.Used > 0) opponent.Population.Release(1);
                fogOfWar?.UnregisterHostile(worker.gameObject);
            }
            telemetry.RecordEntityLost(worker.IsFriendly, "Worker", MatchElapsedSeconds);
        }

    }
}
