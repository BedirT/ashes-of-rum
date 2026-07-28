using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AshesOfRum.Tests
{
    public sealed partial class StartingEconomyPlayModeTests
    {
        [UnityTest]
        public IEnumerator StructureCombat_ResultRestartDefeatAndTelemetryCompleteTheMatchLoop()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var attackers = economy.DeployFriendlyForAutomation(FormationType.Cavalry,
                economy.EnemyHisar.transform.position + Vector3.back * 7f);
            economy.FogOfWar.RefreshNow();
            Assert.That(economy.FogOfWar.IsCurrentlyVisible(economy.EnemyHisar), Is.True);
            economy.SelectOnly(attackers);
            var mouse = InputSystem.AddDevice<Mouse>();
            mouse.MakeCurrent();
            Camera.main.GetComponent<RtsCameraController>().CenterOn(economy.EnemyHisar.transform.position);
            yield return null;
            var hisarCollider = economy.EnemyHisar.GetComponentInChildren<Collider>();
            var hisarClick = (Vector2)Camera.main.WorldToScreenPoint(hisarCollider.bounds.center);
            yield return PressMouseButton(economy, mouse, hisarClick, MouseButton.Right, "HandleOrderInput");
            Assert.That(ReferenceEquals(attackers.StructureTarget, economy.EnemyHisar), Is.True,
                "A real contextual right click must focus the visible hostile Hisar.");
            InputSystem.RemoveDevice(mouse);
            Time.timeScale = 20f;
            yield return WaitUntil(() => economy.Outcome == MatchOutcome.Victory);

            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(attackers.CurrentOrder, Is.EqualTo(FormationOrder.Idle));
            Assert.That(GameObject.Find("Match Result").activeInHierarchy, Is.True);
            Assert.That(GameObject.Find("Match Result Title").GetComponent<Text>().text, Is.EqualTo("VICTORY"));
            Assert.That(Resources.FindObjectsOfTypeAll<GameObject>().Single(item => item.name == "Top Bar").activeSelf,
                Is.False);
            Assert.That(GameObject.Find("Restart Match").GetComponent<Button>().interactable, Is.True);
            Assert.That(GameObject.Find("Quit Match").GetComponent<Button>().interactable, Is.True);
            Assert.That(System.IO.File.Exists(economy.MatchSummaryPath), Is.True);
            Assert.That(System.IO.File.Exists(economy.MatchEventLogPath), Is.True);
            var victorySummary = JsonUtility.FromJson<MatchSummary>(
                System.IO.File.ReadAllText(economy.MatchSummaryPath));
            Assert.That(victorySummary.outcome, Is.EqualTo(MatchOutcome.Victory.ToString()));
            Assert.That(victorySummary.destroyedHisar, Is.EqualTo(StartingEconomyController.EnemyHisarObjectName));

            var previous = economy;
            economy.RestartMatch();
            yield return WaitUntil(() => Object.FindAnyObjectByType<StartingEconomyController>() != previous);
            economy = Object.FindAnyObjectByType<StartingEconomyController>();
            economy.SetOpponentEnabledForAutomation(false);
            Assert.That(economy.Outcome, Is.EqualTo(MatchOutcome.InProgress));
            Assert.That(economy.Supplies, Is.EqualTo(economy.StartingSupplies));
            Assert.That(economy.FriendlyFormations, Is.Empty);
            Assert.That(economy.FogOfWar.StateAt(economy.EnemyHisar.transform.position),
                Is.EqualTo(FogState.Unexplored));

            var invaders = economy.DeployEnemyForAutomation(FormationType.Cavalry,
                economy.FriendlyHisar.transform.position + Vector3.forward * 7f);
            Assert.That(invaders.IssueFocus(economy.FriendlyHisar), Is.True);
            Time.timeScale = 20f;
            yield return WaitUntil(() => economy.Outcome == MatchOutcome.Defeat);
            Assert.That(GameObject.Find("Match Result Title").GetComponent<Text>().text, Is.EqualTo("DEFEAT"));
            GameObject.Find("Quit Match").GetComponent<Button>().onClick.Invoke();
            Assert.That(economy.QuitRequested, Is.True);
            Time.timeScale = 1f;
        }

        [UnityTest]
        public IEnumerator StructuralVolley_DamagesACompletedBuildingAtTheStandardizedReducedRate()
        {
            var tuning = ScriptableObject.CreateInstance<EconomyTuning>();
            var attackers = CreateFormationForTest("Structure attackers", FormationType.Spearmen, true, tuning);
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var building = root.AddComponent<ConstructibleBuilding>();
            building.Initialize(BuildingType.Storehouse, 0.1f, 100, Color.red, _ => { }, false);
            building.Advance(0.1f);
            try
            {
                Assert.That(attackers.ExecuteStructuralVolley(building), Is.True);
                Assert.That(building.Health, Is.EqualTo(100 - MatchRules.StructuralVolleyDamage(8,
                    tuning.structuralDamage)));
                yield return null;
            }
            finally
            {
                Object.Destroy(attackers.gameObject);
                Object.Destroy(root);
                Object.Destroy(tuning);
            }
        }

        [UnityTest]
        public IEnumerator HisarSharedQueue_TrainsAWorkerThroughTheRuntimeHud()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            economy.SelectHisar();
            yield return null;
            var workerButton = GameObject.Find("Train Worker").GetComponent<Button>();
            Assert.That(workerButton.gameObject.activeInHierarchy, Is.True);
            Assert.That(workerButton.GetComponentInChildren<Text>().text, Does.Contain("[Q]"));

            workerButton.onClick.Invoke();
            Assert.That(economy.Supplies, Is.Zero);
            Assert.That(economy.PopulationUsed, Is.EqualTo(5));
            Assert.That(economy.ProductionQueueCount, Is.EqualTo(1));
            yield return WaitUntil(() => economy.Workers.Count == 5);

            Assert.That(economy.Workers.Last().IsAlive, Is.True);
            Assert.That(economy.CurrentMatchSummary.friendlyEntitiesProduced, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator TrainedWorker_CrossSideFallbackUsesOnlyVisibleNeutralCaches()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            economy.SelectHisar();
            GameObject.Find("Train Worker").GetComponent<Button>().onClick.Invoke();
            yield return WaitUntil(() => economy.Workers.Count == 5);

            foreach (var enemyWorker in economy.EnemyWorkers) enemyWorker.Suspend();
            foreach (var startingCache in economy.Caches) startingCache.TakeBatch(int.MaxValue);
            var depletedCache = economy.OpponentCaches[0];
            var fallbackCache = economy.OpponentCaches[1];
            depletedCache.Initialize(10);
            var depletedScout = new GameObject("Trained worker depleted-cache scout");
            depletedScout.transform.position = depletedCache.transform.position;
            economy.FogOfWar.RegisterFriendly(depletedScout.transform);
            economy.FogOfWar.RefreshNow();

            var worker = economy.Workers.Last();
            Assert.That(GetPrivateField<IReadOnlyList<ResourceCache>>(worker, "knownCaches").Count,
                Is.EqualTo(4), "A trained Worker must receive the shared neutral-cache catalog.");
            Assert.That(economy.FogOfWar.StateAt(depletedCache.transform.position), Is.EqualTo(FogState.Visible));
            Assert.That(economy.FogOfWar.StateAt(fallbackCache.transform.position), Is.Not.EqualTo(FogState.Visible));

            var originalTimeScale = Time.timeScale;
            GameObject fallbackScout = null;
            try
            {
                Time.timeScale = 4f;
                worker.IssueGather(depletedCache);
                yield return WaitUntil(() => depletedCache.Remaining == 0 &&
                                             worker.CurrentActivity == WorkerAgent.Activity.Idle);
                Assert.That(fallbackCache.Remaining, Is.EqualTo(400),
                    "A trained Worker must not retarget to an unseen cross-side cache.");
                Assert.That(GetPrivateField<ResourceCache>(worker, "targetCache"), Is.Null);

                fallbackScout = new GameObject("Trained worker fallback-cache scout");
                fallbackScout.transform.position = fallbackCache.transform.position;
                economy.FogOfWar.RegisterFriendly(fallbackScout.transform);
                economy.FogOfWar.RefreshNow();
                Assert.That(economy.FogOfWar.StateAt(fallbackCache.transform.position), Is.EqualTo(FogState.Visible));

                depletedCache.Initialize(10);
                worker.IssueGather(depletedCache);
                yield return WaitUntil(() => depletedCache.Remaining == 0 &&
                    GetPrivateField<ResourceCache>(worker, "targetCache") == fallbackCache);
                Assert.That(worker.CurrentActivity, Is.Not.EqualTo(WorkerAgent.Activity.Idle));
            }
            finally
            {
                Time.timeScale = originalTimeScale;
                Object.Destroy(depletedScout);
                if (fallbackScout != null) Object.Destroy(fallbackScout);
            }
        }

        [UnityTest]
        public IEnumerator OpponentFailedGatherRoute_BuildsUsesAndCanLoseARealStorehouse()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var tuning = GetPrivateField<EconomyTuning>(economy, "tuning");
            var originalFallbackRadius = tuning.cacheFallbackRadius;
            var recoveryCache = economy.Caches[0];
            var scout = economy.DeployEnemyForAutomation(FormationType.Cavalry, recoveryCache.transform.position);
            var worker = economy.EnemyWorkers.First(candidate => candidate.CurrentConstruction == null);
            var opponent = GetPrivateField<ScriptedOpponentController>(economy, "opponent");
            var originalTimeScale = Time.timeScale;
            try
            {
                tuning.cacheFallbackRadius = 5f;
                foreach (var cache in economy.Caches.Concat(economy.OpponentCaches))
                    if (cache != recoveryCache) cache.TakeBatch(int.MaxValue);
                Assert.That(worker.CarriedSupplies, Is.Zero);
                Assert.That(GetPrivateField<ResourceCache>(worker, "targetCache"), Is.Not.EqualTo(recoveryCache));
                economy.CreditOpponentSuppliesForAutomation(tuning.storehouseCost);
                var suppliesBefore = economy.OpponentSupplies;
                worker.IssueGather(null);

                Assert.That(opponent.IsStorehouseRecoveryRequested, Is.True);
                Assert.That(opponent.RecoveryCache, Is.SameAs(recoveryCache));
                economy.SetOpponentEnabledForAutomation(true);
                Time.timeScale = 1f;
                yield return WaitUntil(() => economy.EnemyBuildings.Any(building =>
                    building.Type == BuildingType.Storehouse));
                var storehouse = economy.EnemyBuildings.Single(building => building.Type == BuildingType.Storehouse);
                Assert.That(economy.OpponentSupplies, Is.EqualTo(suppliesBefore - tuning.storehouseCost),
                    "Recovery must spend the mirrored Storehouse cost rather than grant a structure.");
                Assert.That(economy.EnemyWorkers.Count(candidate =>
                    ReferenceEquals(candidate.CurrentConstruction, storehouse)), Is.EqualTo(1),
                    "Exactly one hostile Worker must supply construction labor.");

                yield return WaitUntil(() => storehouse.IsComplete);
                Assert.That(economy.CurrentMatchSummary.hostileBuildingsConstructed, Is.GreaterThanOrEqualTo(1));
                var gatheredBeforeRecovery = economy.CurrentMatchSummary.hostileSuppliesGathered;
                yield return WaitUntil(() => economy.CurrentMatchSummary.hostileSuppliesGathered >
                                             gatheredBeforeRecovery);
                Assert.That(economy.EnemyWorkers.Any(candidate =>
                        Vector3.Distance(candidate.LastDropOffPoint, storehouse.DropOffPoint) < 0.1f), Is.True,
                    "The hostile Worker must use the completed Storehouse as its nearest real drop-off.");

                var additionalWorker = economy.EnemyWorkers.First(candidate => candidate != worker &&
                    candidate.CurrentConstruction == null && candidate.CarriedSupplies == 0);
                additionalWorker.IssueGather(null);
                yield return null;
                Assert.That(economy.EnemyBuildings.Count(building => building.Type == BuildingType.Storehouse),
                    Is.EqualTo(1), "A completed recovery Storehouse must be reused rather than duplicated.");
                Assert.That(additionalWorker.TargetCache, Is.SameAs(recoveryCache));

                economy.SetOpponentEnabledForAutomation(false);
                var suppliesBeforeDestruction = economy.OpponentSupplies;
                Assert.That(storehouse.ApplyStructuralDamage(storehouse.Health), Is.True);
                Assert.That(economy.OpponentSupplies, Is.EqualTo(suppliesBeforeDestruction),
                    "Enemy destruction must not refund the Storehouse.");
                Assert.That(opponent.IsStorehouseRecoveryRequested, Is.True,
                    "Losing an economy-critical Storehouse must reopen recovery without a hidden replacement.");
            }
            finally
            {
                tuning.cacheFallbackRadius = originalFallbackRadius;
                Time.timeScale = originalTimeScale;
                Object.Destroy(scout.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator FunctionalAudioAndUnderAttackPing_AreProceduralThrottledFogAwareAndDoNotMoveCamera()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var audio = economy.GameplayAudio;
            var cameraPosition = Camera.main.transform.position;
            Assert.That(audio, Is.Not.Null);
            Assert.That(audio.HasAllFunctionalCues, Is.True);
            Assert.That(audio.GetComponent<AudioSource>().clip, Is.Null,
                "Procedural cues must not depend on an imported external clip.");

            economy.SelectOnly(economy.Workers[0]);
            economy.IssueMoveForSelected(new Vector3(2f, 0f, 1f));
            Assert.That(audio.CountFor(GameplayCue.Selection), Is.GreaterThan(0));
            Assert.That(audio.CountFor(GameplayCue.Order), Is.GreaterThan(0));

            economy.CreditSuppliesForAutomation(100);
            Assert.That(economy.TryPlaceHouse(economy.Workers[1], VisibleHouseSite), Is.True);
            yield return WaitUntil(() => economy.PopulationCapacity == 20);
            Assert.That(audio.CountFor(GameplayCue.Construction), Is.GreaterThan(0));

            economy.SelectHisar();
            Assert.That(economy.TryQueueWorker(), Is.True);
            yield return WaitUntil(() => economy.Workers.Count == 5);
            Assert.That(audio.CountFor(GameplayCue.Production), Is.GreaterThan(0));

            var friendly = economy.DeployFriendlyForAutomation(FormationType.Spearmen, new Vector3(0f, 0f, 2f));
            var hostile = economy.DeployEnemyForAutomation(FormationType.Archers, new Vector3(0f, 0f, 4f));
            economy.FogOfWar.RefreshNow();
            Assert.That(friendly.ExecuteAttackVolley(hostile), Is.True);
            Assert.That(audio.CountFor(GameplayCue.Attack), Is.GreaterThan(0));

            var warningsBefore = economy.UnderAttackWarningCount;
            friendly.ApplyFixedDamage(1);
            var warningCount = economy.UnderAttackWarningCount;
            Assert.That(warningCount, Is.EqualTo(warningsBefore + 1));
            Assert.That(audio.CountFor(GameplayCue.Hit), Is.GreaterThan(0));
            Assert.That(audio.CountFor(GameplayCue.Warning), Is.GreaterThan(0));
            Assert.That(economy.FogOfWar.IsAttackPingVisible, Is.True);
            Assert.That(Vector3.Distance(economy.FogOfWar.LastAttackPingPosition, friendly.transform.position),
                Is.LessThan(0.1f));
            friendly.ApplyFixedDamage(1);
            Assert.That(economy.UnderAttackWarningCount, Is.EqualTo(warningCount),
                "Repeated damage inside the warning window must not spam cues or pings.");
            Assert.That(Camera.main.transform.position, Is.EqualTo(cameraPosition),
                "Under-attack feedback must never steal the camera.");

            economy.FogOfWar.RefreshNow();
            var hiddenPosition = economy.OpponentCaches[0].transform.position;
            Assert.That(economy.FogOfWar.StateAt(hiddenPosition), Is.EqualTo(FogState.Unexplored));
            Assert.That(economy.FogOfWar.ShowAttackPing(hiddenPosition), Is.False,
                "A minimap ping must not reveal unexplored terrain.");

            economy.DestroyHisarForAutomation(true);
            Assert.That(audio.CountFor(GameplayCue.Victory), Is.EqualTo(1));
            Time.timeScale = 1f;
        }

        [UnityTest]
        public IEnumerator HisarContextualRally_RightClickCacheAndTerrainDispatchesWorkerAndFormation()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var mouse = InputSystem.AddDevice<Mouse>();
            var keyboard = InputSystem.AddDevice<Keyboard>();
            mouse.MakeCurrent();
            keyboard.MakeCurrent();
            try
            {
                var visibleCache = economy.Caches[0];
                economy.SelectHisar();
                var cacheClick = (Vector2)Camera.main.WorldToScreenPoint(
                    visibleCache.GetComponent<Collider>().bounds.center);
                yield return PressMouseButton(economy, mouse, cacheClick, MouseButton.Right, "HandleOrderInput");
                Assert.That(economy.HisarRallyCache, Is.SameAs(visibleCache));
                Assert.That(GameObject.Find("Hisar Rally Point"), Is.Not.Null);
                Assert.That(GameObject.Find("Selection").GetComponent<Text>().text, Does.Contain("CARAVAN CACHE"));

                GameObject.Find("Train Worker").GetComponent<Button>().onClick.Invoke();
                yield return WaitUntil(() => economy.Workers.Count == 5);
                var trainedWorker = economy.Workers.Last();
                Assert.That(GetPrivateField<ResourceCache>(trainedWorker, "targetCache"), Is.SameAs(visibleCache));
                Assert.That(trainedWorker.CurrentActivity, Is.Not.EqualTo(WorkerAgent.Activity.Idle));

                economy.CreditSuppliesForAutomation(100);
                Assert.That(economy.TryPlaceHouse(economy.Workers[1], VisibleHouseSite), Is.True);
                yield return WaitUntil(() => economy.PopulationCapacity == 20);
                economy.CreditSuppliesForAutomation(400);
                economy.SelectHisar();
                var terrainPoint = new Vector3(12f, 0f, 9f);
                var terrainClick = (Vector2)Camera.main.WorldToScreenPoint(terrainPoint);
                yield return PressMouseButton(economy, mouse, terrainClick, MouseButton.Right, "HandleOrderInput");
                Assert.That(economy.HisarRallyCache, Is.Null);
                Assert.That(economy.HisarRallyPoint, Is.Not.Null);

                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S));
                InputSystem.Update();
                InvokePrivateMethod(economy, "HandleBuildInput");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
                InvokePrivateMethod(economy, "HandleBuildInput");
                yield return WaitUntil(() => economy.FriendlyFormations.Count == 1);
                var trainedFormation = economy.FriendlyFormations.Single();
                Assert.That(trainedFormation.CurrentOrder, Is.EqualTo(FormationOrder.Move));
                Assert.That(Vector3.Distance(trainedFormation.Destination, economy.HisarRallyPoint.Value),
                    Is.LessThan(0.1f));

                var hiddenCache = economy.OpponentCaches[0];
                Assert.That(economy.FogOfWar.StateAt(hiddenCache.transform.position), Is.EqualTo(FogState.Unexplored));
                Assert.That(economy.SetHisarRallyForAutomation(hiddenCache.transform.position, hiddenCache), Is.False,
                    "The Hisar must reject an unseen neutral-cache rally target.");
                Assert.That(economy.HisarRallyCache, Is.Null);
            }
            finally
            {
                if (Mouse.current == mouse) mouse.MakeCurrent();
                InputSystem.RemoveDevice(mouse);
                InputSystem.RemoveDevice(keyboard);
            }
        }

        [UnityTest]
        public IEnumerator Hud_ShowsPopulationAndClickableBuildCommands()
        {
            yield return LoadEconomy();

            Assert.That(GameObject.Find("Population").GetComponent<UnityEngine.UI.Text>().text,
                Is.EqualTo("POPULATION   4 / 12"));
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            economy.SelectOnly(economy.Workers[0]);
            Assert.That(GameObject.Find("Build House").GetComponent<UnityEngine.UI.Button>(), Is.Not.Null);
            Assert.That(GameObject.Find("EventSystem").GetComponents<MonoBehaviour>()
                .Any(component => component.GetType().Name == "InputSystemUIInputModule"), Is.True);
            Assert.That(GameObject.Find("Build House").GetComponentInChildren<UnityEngine.UI.Text>().text,
                Does.Contain("[H]"));
            Assert.That(GameObject.Find("Build Storehouse").GetComponentInChildren<UnityEngine.UI.Text>().text,
                Does.Contain("200 [R]"));
            Assert.That(GameObject.Find("Build Watchtower").GetComponentInChildren<UnityEngine.UI.Text>().text,
                Does.Contain("300 [T]"));
            Assert.That(GameObject.Find("Cancel Build").GetComponentInChildren<UnityEngine.UI.Text>().text,
                Does.Contain("[X]"));

            economy.SelectHisar();
            yield return null;
            Assert.That(GameObject.Find("Train Spearmen").GetComponentInChildren<UnityEngine.UI.Text>().text,
                Does.Contain("[S]"));
            Assert.That(GameObject.Find("Train Archers").GetComponentInChildren<UnityEngine.UI.Text>().text,
                Does.Contain("[A]"));
            Assert.That(GameObject.Find("Train Cavalry").GetComponentInChildren<UnityEngine.UI.Text>().text,
                Does.Contain("[C]"));

            economy.SelectOnly(economy.Workers[0]);
            GameObject.Find("Build House").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.That(economy.IsHousePlacementActive, Is.True);
        }

    }
}
