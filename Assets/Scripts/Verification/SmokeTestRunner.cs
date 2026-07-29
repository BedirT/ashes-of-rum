using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AshesOfRum
{
    public static class SmokeVerificationRules
    {
        public static bool HasFairOpponentEconomy(int hostileFormationsProduced, int completedHostileBuildings,
            int populationCapacity, int hostileSuppliesGathered) =>
            hostileFormationsProduced >= 2 && completedHostileBuildings > 0 && populationCapacity >= 20 &&
            hostileSuppliesGathered > 0;
    }

    public sealed class SmokeTestRunner : MonoBehaviour
    {
        private const float ScreenshotTimeoutSeconds = 15f;
        private const float EconomyTimeoutSeconds = 30f;
        private const float ConstructionTimeoutSeconds = 20f;
        private const float CombatTimeoutSeconds = 20f;

        [Serializable]
        private sealed class SmokeResult
        {
            public bool passed;
            public string scene;
            public string[] checks;
            public string error;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartWhenRequested()
        {
            if (!VerificationLaunchModeValidator.AllowsCurrentProcess()) return;
            if (!HasArgument("--smoke-test")) return;
            var runner = new GameObject("SmokeTestRunner");
            DontDestroyOnLoad(runner);
            runner.AddComponent<SmokeTestRunner>().StartCoroutine(Run());
        }

        private static IEnumerator Run()
        {
            var screenshotPath = GetArgumentValue("--smoke-screenshot");
            var healthScreenshotPath = GetArgumentValue("--smoke-health-screenshot");
            var graphical = !string.IsNullOrEmpty(screenshotPath);
            if (graphical) Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
            yield return null;
            yield return new WaitForEndOfFrame();

            var economy = FindAnyObjectByType<StartingEconomyController>();
            var economyStarted = economy != null && economy.Workers.Count == StartingEconomyController.WorkerCount;
            var opponentCachesHiddenByFog = false;
            var hiddenCacheDepletionStayedHidden = false;
            var opponentStorehouseRecovered = false;
            if (economyStarted)
            {
                economy.FogOfWar.RefreshNow();
                var hiddenCache = economy.OpponentCaches[0];
                var hiddenCacheRenderers = hiddenCache.GetComponentsInChildren<Renderer>(true);
                var hiddenCacheColliders = hiddenCache.GetComponentsInChildren<Collider>(true);
                opponentCachesHiddenByFog = economy.FogOfWar.StateAt(hiddenCache.transform.position) ==
                                             FogState.Unexplored &&
                                             hiddenCacheRenderers.All(item => !item.enabled) &&
                                             hiddenCacheColliders.All(item => !item.enabled) &&
                                             economy.CurrentMatchSummary.firstContactSeconds < 0f;
                var originalCacheSupplies = hiddenCache.Remaining;
                hiddenCache.TakeBatch(int.MaxValue);
                economy.FogOfWar.RefreshNow();
                hiddenCacheDepletionStayedHidden = hiddenCache.Remaining == 0 &&
                                                   hiddenCacheRenderers.All(item => !item.enabled) &&
                                                   hiddenCacheColliders.All(item => !item.enabled) &&
                                                   economy.CurrentMatchSummary.firstContactSeconds < 0f;
                hiddenCache.Initialize(originalCacheSupplies);
                economy.FogOfWar.RefreshNow();
                economy.SetOpponentEnabledForAutomation(false);
                economy.SetOpponentTargetsAvailableForAutomation(false);
                economy.CreditOpponentSuppliesForAutomation(200);
                var recoveryRequested = economy.TriggerOpponentRouteFailureForAutomation();
                economy.SetOpponentEnabledForAutomation(true);
                var recoveryDeadline = Time.realtimeSinceStartup + ConstructionTimeoutSeconds;
                while (!economy.EnemyBuildings.Any(building => building.Type == BuildingType.Storehouse &&
                                                                building.IsComplete) &&
                       Time.realtimeSinceStartup < recoveryDeadline)
                    yield return null;
                var recoveredStorehouse = economy.EnemyBuildings.FirstOrDefault(building =>
                    building.Type == BuildingType.Storehouse && building.IsComplete);
                if (recoveredStorehouse != null)
                {
                    while (!economy.EnemyWorkers.Any(worker => Vector3.Distance(worker.LastDropOffPoint,
                               recoveredStorehouse.DropOffPoint) < 0.1f) &&
                           Time.realtimeSinceStartup < recoveryDeadline)
                        yield return null;
                    opponentStorehouseRecovered = recoveryRequested &&
                        economy.CurrentMatchSummary.hostileSuppliesGathered > 0 &&
                        economy.EnemyWorkers.Any(worker => Vector3.Distance(worker.LastDropOffPoint,
                            recoveredStorehouse.DropOffPoint) < 0.1f);
                }
                economy.SetOpponentEnabledForAutomation(false);
                economy.IssueGatherForSmoke(economy.Caches[0]);
                var economyDeadline = Time.realtimeSinceStartup + EconomyTimeoutSeconds;
                while (economy.Supplies <= economy.StartingSupplies && Time.realtimeSinceStartup < economyDeadline)
                    yield return null;
            }
            var economyCompleted = economyStarted && economy.Supplies > economy.StartingSupplies;
            var houseStarted = economyCompleted &&
                               economy.TryPlaceHouse(economy.Workers[0], new Vector3(8f, 0f, -1f));
            if (houseStarted)
            {
                var constructionDeadline = Time.realtimeSinceStartup + ConstructionTimeoutSeconds;
                while (economy.PopulationCapacity == 12 && Time.realtimeSinceStartup < constructionDeadline)
                    yield return null;
            }
            var houseCompleted = houseStarted && economy.PopulationCapacity == 20 &&
                                 economy.Houses.Count == 1 && economy.Houses[0].IsComplete;
            var populationCapacityIncreased = economy.PopulationCapacity == 20;
            var storehouseStarted = false;
            var watchtowerStarted = false;
            var defensiveBuildingsCompleted = false;
            var storehouseDropOffUsed = false;
            var watchtowerFired = false;
            if (houseCompleted)
            {
                economy.CreditSuppliesForAutomation(500);
                storehouseStarted = economy.TryPlaceStorehouse(economy.Workers[1], new Vector3(12f, 0f, 6f));
                economy.FogOfWar.RefreshNow();
                watchtowerStarted = economy.TryPlaceWatchtower(economy.Workers[2], new Vector3(4f, 0f, 10f));
                var constructionDeadline = Time.realtimeSinceStartup + ConstructionTimeoutSeconds;
                while ((economy.Storehouses.Count == 0 || !economy.Storehouses[0].IsComplete ||
                        economy.Watchtowers.Count == 0 || !economy.Watchtowers[0].IsComplete) &&
                       Time.realtimeSinceStartup < constructionDeadline)
                    yield return null;
                defensiveBuildingsCompleted = storehouseStarted && watchtowerStarted &&
                                              economy.Storehouses.Count == 1 &&
                                              economy.Storehouses[0].IsComplete &&
                                              economy.Watchtowers.Count == 1 &&
                                              economy.Watchtowers[0].IsComplete;
                if (defensiveBuildingsCompleted)
                {
                    var storehouse = economy.Storehouses[0];
                    var suppliesBeforeDropOff = economy.Supplies;
                    var storehouseWorker = economy.Workers[1];
                    storehouseWorker.IssueGather(economy.Caches[1]);
                    var economyDeadline = Time.realtimeSinceStartup + EconomyTimeoutSeconds;
                    while ((storehouseWorker.CurrentActivity != WorkerAgent.Activity.Returning ||
                            storehouseWorker.CarriedSupplies == 0) &&
                           Time.realtimeSinceStartup < economyDeadline)
                        yield return null;
                    var returningToStorehouse = storehouseWorker.CarriedSupplies > 0 &&
                                                Vector3.Distance(storehouseWorker.LastDropOffPoint,
                                                    storehouse.DropOffPoint) < 0.1f;
                    while (storehouseWorker.CarriedSupplies > 0 &&
                           Time.realtimeSinceStartup < economyDeadline)
                        yield return null;
                    storehouseDropOffUsed = returningToStorehouse &&
                                            storehouseWorker.CarriedSupplies == 0 &&
                                            economy.Supplies > suppliesBeforeDropOff;
                }
            }
            var trainingStarted = false;
            var combatWon = false;
            var supportedFormationMaterials = false;
            var supportedArrowMaterial = false;
            var nonlethalHitFeedback = false;
            var cavalryTrained = false;
            var controlGroupRecalled = false;
            var formationGroupStopped = false;
            var hostileHiddenByFog = false;
            var hostileRevealedByMovement = false;
            var contactLostUnderFog = false;
            var cavalryCounterWon = false;
            var rearAttackAdvantage = false;
            var frontlineBlockedAndReleased = false;
            var formationRallyDispatched = false;
            var memberFlowObserved = false;
            var blockedMemberRecovered = false;
            var memberProjectileHitObserved = false;
            var memberCasualtyRegrouped = false;
            FormationAgent memberProjectileSource = null;
            if (defensiveBuildingsCompleted && storehouseDropOffUsed)
            {
                economy.CreditSuppliesForAutomation(400);
                economy.SetHisarRallyForAutomation(new Vector3(-8f, 0f, 2f));
                trainingStarted = economy.TryQueueFormation(FormationType.Archers);
                var combatDeadline = Time.realtimeSinceStartup + CombatTimeoutSeconds;
                while (economy.FriendlyFormations.Count == 0 && Time.realtimeSinceStartup < combatDeadline)
                    yield return null;
                if (economy.FriendlyFormations.Count > 0)
                {
                    economy.DeployEnemyForAutomation(FormationType.Spearmen, new Vector3(0f, 0f, 17f));
                    var friendly = economy.FriendlyFormations[0];
                    memberProjectileSource = friendly;
                    formationRallyDispatched = friendly.CurrentOrder == FormationOrder.Move &&
                                               Vector3.Distance(friendly.Destination,
                                                   economy.HisarRallyPoint ?? Vector3.zero) < 0.1f;
                    var hostile = economy.EnemyFormations[0];
                    var tower = economy.Watchtowers[0].GetComponent<WatchtowerAttack>();
                    supportedFormationMaterials = friendly.HasSupportedVisualMaterials() &&
                                                  hostile.HasSupportedVisualMaterials() &&
                                                  friendly.GetComponentInChildren<FormationFrontIndicator>() != null;
                    while (hostile.MemberCount == 8 && Time.realtimeSinceStartup < combatDeadline)
                        yield return null;
                    watchtowerFired = tower.ShotsFired > 0 && hostile.MemberCount < 8;
                    hostile.ApplyFixedDamage(1);
                    foreach (var visual in hostile.GetComponentsInChildren<FormationMemberVisual>())
                        nonlethalHitFeedback |= visual.IsShowingHitFeedback;
                    economy.IssueFocusForSmoke(friendly, hostile);
                }
                while (economy.EnemyFormations.Count > 0 && Time.realtimeSinceStartup < combatDeadline)
                {
                    memberProjectileHitObserved |= economy.EnemyFormations.Any(formation =>
                        formation.ProjectileHitsReceived > 0);
                    var arrow = GameObject.Find("Arrow");
                    if (arrow != null)
                        supportedArrowMaterial |= FormationAgent.UsesSupportedMaterial(arrow.GetComponent<Renderer>());
                    yield return null;
                }
                memberProjectileHitObserved |= memberProjectileSource != null &&
                                               memberProjectileSource.ProjectileHitsLanded > 0;
                combatWon = economy.FriendlyFormations.Count == 1 && economy.EnemyFormations.Count == 0 &&
                            economy.FriendlyFormations[0].MemberCount >= 4;
            }

            if (combatWon)
            {
                economy.CreditSuppliesForAutomation(400);
                var cavalryTrainingStarted = economy.TryQueueFormation(FormationType.Cavalry);
                var contestDeadline = Time.realtimeSinceStartup + CombatTimeoutSeconds;
                while (economy.FriendlyFormations.Count < 2 && Time.realtimeSinceStartup < contestDeadline)
                    yield return null;
                cavalryTrained = cavalryTrainingStarted && economy.FriendlyFormations.Count == 2 &&
                                  economy.FriendlyFormations.Any(formation =>
                                      formation.Type == FormationType.Cavalry && formation.MemberCount == 8);
                if (cavalryTrained)
                {
                    economy.DeployEnemyForAutomation(FormationType.Archers, new Vector3(0f, 0f, 26f));
                    economy.SelectFormationsForAutomation(economy.FriendlyFormations);
                    economy.AssignControlGroup(1);
                    economy.SelectHisar();
                    controlGroupRecalled = economy.RecallControlGroup(1) &&
                                           economy.SelectedFormations.Count == 2 &&
                                           economy.ControlGroupSize(1) == 2;

                    economy.IssueMoveForSelected(new Vector3(0f, 0f, 8f));
                    yield return new WaitForSeconds(0.35f);
                    memberFlowObserved = economy.SelectedFormations.SelectMany(formation => formation.Members)
                        .Any(member => Vector3.Distance(member.WorldPosition,
                            member.AssignedSlotWorldPosition) > 0.15f);
                    economy.StopSelectedFormations();
                    var stoppedPositions = economy.SelectedFormations
                        .Select(formation => formation.transform.position).ToArray();
                    yield return new WaitForSeconds(0.25f);
                    formationGroupStopped = economy.SelectedFormations.All(formation =>
                        formation.CurrentOrder == FormationOrder.Idle && !formation.HasDestination);
                    for (var index = 0; index < stoppedPositions.Length; index++)
                        formationGroupStopped &= Vector3.Distance(stoppedPositions[index],
                            economy.SelectedFormations[index].transform.position) < 0.15f;

                    var obstructionFormation = economy.SelectedFormations[0];
                    var obstructedMember = obstructionFormation.Members[0];
                    var obstructedSlot = obstructedMember.AssignedSlotWorldPosition;
                    var slotBlocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    slotBlocker.name = "Smoke Blocked Formation Slot";
                    slotBlocker.transform.position = new Vector3(obstructedSlot.x, 1f, obstructedSlot.z);
                    slotBlocker.transform.localScale = new Vector3(3f, 2f, 3f);
                    var blockerBounds = new Bounds(slotBlocker.transform.position, slotBlocker.transform.localScale);
                    var slotObstacle = slotBlocker.AddComponent<UnityEngine.AI.NavMeshObstacle>();
                    slotObstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
                    slotObstacle.carving = true;
                    slotObstacle.carveOnlyStationary = false;
                    yield return new WaitForSeconds(1f);
                    obstructedMember.TeleportBy(Vector3.left * 5f);
                    var displacedMemberPosition = obstructedMember.WorldPosition;
                    var blockedSlotDeadline = Time.realtimeSinceStartup + 5f;
                    while ((Vector3.Distance(obstructedMember.WorldPosition,
                                obstructedMember.NavigationDestination) > 0.45f ||
                            Vector3.Distance(obstructedMember.WorldPosition, displacedMemberPosition) < 2f) &&
                           Time.realtimeSinceStartup < blockedSlotDeadline)
                        yield return null;
                    var settlePosition = obstructedMember.WorldPosition;
                    var settleNavigation = obstructedMember.NavigationDestination;
                    var movedFromDisplacement = Vector3.Distance(settlePosition, displacedMemberPosition) > 2f;
                    var arrivedAtFallback = Vector3.Distance(settlePosition, settleNavigation) <= 0.45f;
                    var remainedOutsideObstacle = !blockerBounds.Contains(new Vector3(settlePosition.x,
                        blockerBounds.center.y, settlePosition.z));
                    var settledBesideObstacle = movedFromDisplacement && arrivedAtFallback && remainedOutsideObstacle;
                    Destroy(slotBlocker);
                    yield return new WaitForSeconds(1f);
                    blockedSlotDeadline = Time.realtimeSinceStartup + 5f;
                    while (Vector3.Distance(obstructedMember.WorldPosition,
                               obstructedMember.AssignedSlotWorldPosition) > 0.45f &&
                           Time.realtimeSinceStartup < blockedSlotDeadline)
                        yield return null;
                    blockedMemberRecovered = settledBesideObstacle &&
                        Vector3.Distance(obstructedMember.WorldPosition,
                            obstructedMember.AssignedSlotWorldPosition) <= 0.45f;
                    Debug.Log($"SMOKE_BLOCKED_MEMBER_STATE:settled={settledBesideObstacle}:" +
                              $"recovered={blockedMemberRecovered}:position={obstructedMember.WorldPosition}:" +
                              $"navigation={obstructedMember.NavigationDestination}:" +
                              $"slot={obstructedMember.AssignedSlotWorldPosition}:" +
                              $"displaced={displacedMemberPosition}:settlePosition={settlePosition}:" +
                              $"settleNavigation={settleNavigation}:moved={movedFromDisplacement}:" +
                              $"arrived={arrivedAtFallback}:outside={remainedOutsideObstacle}:" +
                              $"bounds={blockerBounds}");
                    contestDeadline = Time.realtimeSinceStartup + CombatTimeoutSeconds;

                    var hostileArcher = economy.EnemyFormations.Single(formation =>
                        formation.Type == FormationType.Archers);
                    economy.FogOfWar.RefreshNow();
                    hostileHiddenByFog = !economy.FogOfWar.IsCurrentlyVisible(hostileArcher);

                    economy.IssueMoveForSelected(new Vector3(0f, 0f, 18f));
                    while (!economy.FogOfWar.IsCurrentlyVisible(hostileArcher) &&
                           Time.realtimeSinceStartup < contestDeadline)
                        yield return null;
                    hostileRevealedByMovement = economy.FogOfWar.IsCurrentlyVisible(hostileArcher);

                    economy.IssueMoveForSelected(new Vector3(0f, 0f, 1f));
                    while (economy.FogOfWar.IsCurrentlyVisible(hostileArcher) &&
                           Time.realtimeSinceStartup < contestDeadline)
                        yield return null;
                    contactLostUnderFog = !economy.FogOfWar.IsCurrentlyVisible(hostileArcher) &&
                                          economy.FogOfWar.StateAt(hostileArcher.transform.position) ==
                                          FogState.Explored;

                    economy.IssueAttackMoveForSelected(new Vector3(0f, 0f, 26f));
                    while (economy.EnemyFormations.Count > 0 && Time.realtimeSinceStartup < contestDeadline)
                        yield return null;
                    cavalryCounterWon = economy.EnemyFormations.Count == 0 &&
                                        economy.FriendlyFormations.Any(formation =>
                                            formation.Type == FormationType.Cavalry && formation.MemberCount > 0);
                    if (cavalryCounterWon)
                    {
                        var flanker = economy.FriendlyFormations.First(formation =>
                            formation.Type == FormationType.Cavalry);
                        flanker.IssueStop();
                        var flankPosition = new Vector3(0f, 0f, 8f);
                        if (!flanker.GetComponent<UnityEngine.AI.NavMeshAgent>().Warp(flankPosition))
                            flanker.transform.position = flankPosition;
                        var frontTarget = economy.DeployEnemyForAutomation(FormationType.Spearmen,
                            new Vector3(0f, 0f, 10f));
                        var rearTarget = economy.DeployEnemyForAutomation(FormationType.Spearmen,
                            new Vector3(0f, 0f, 10f));
                        frontTarget.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                        rearTarget.transform.rotation = Quaternion.identity;
                        var frontHealth = frontTarget.TotalMemberHealth;
                        var rearHealth = rearTarget.TotalMemberHealth;
                        frontTarget.ApplyDeterministicHit(frontTarget.Members[0], FormationType.Cavalry,
                            flanker.transform.position);
                        rearTarget.ApplyDeterministicHit(rearTarget.Members[0], FormationType.Cavalry,
                            flanker.transform.position);
                        rearAttackAdvantage = rearHealth - rearTarget.TotalMemberHealth >
                                              frontHealth - frontTarget.TotalMemberHealth;
                        var casualty = frontTarget.Members[0];
                        var survivor = frontTarget.Members[1];
                        casualty.TeleportBy(Vector3.right * 2f);
                        var survivorPosition = survivor.WorldPosition;
                        frontTarget.ApplyDeterministicHit(casualty, FormationType.Archers,
                            casualty.WorldPosition - Vector3.forward);
                        var casualtyRemoved = !casualty.IsAlive;
                        var regroupDistance = Vector3.Distance(survivor.WorldPosition,
                            survivor.AssignedSlotWorldPosition);
                        yield return new WaitForSeconds(0.5f);
                        memberCasualtyRegrouped = casualtyRemoved && survivor.SlotIndex == 0 &&
                            Vector3.Distance(survivorPosition, survivor.AssignedSlotWorldPosition) > 0.01f &&
                            Vector3.Distance(survivor.WorldPosition, survivor.AssignedSlotWorldPosition) <
                            regroupDistance;
                        while (frontTarget.MemberCount > 0) frontTarget.ApplyFixedDamage(frontTarget.MaximumMemberHealth);
                        while (rearTarget.MemberCount > 0) rearTarget.ApplyFixedDamage(rearTarget.MaximumMemberHealth);

                        var frontlineTarget = economy.DeployEnemyForAutomation(FormationType.Spearmen,
                            new Vector3(0f, 0f, 12f));
                        economy.FogOfWar.RefreshNow();
                        economy.SelectOnly(flanker);
                        flanker.IssueMove(new Vector3(0f, 0f, 16f));
                        var frontlineDeadline = Time.realtimeSinceStartup + CombatTimeoutSeconds;
                        while (!flanker.IsFrontlineBlocked && Time.realtimeSinceStartup < frontlineDeadline)
                            yield return null;
                        var frontlinePosition = flanker.transform.position;
                        yield return null;
                        var blockedHud = GameObject.Find("Selection").GetComponent<UnityEngine.UI.Text>().text
                            .Contains("FRONTLINE BLOCKED");
                        flanker.IssueMove(frontlinePosition + Vector3.right * 4f);
                        while (flanker.transform.position.x < frontlinePosition.x + 0.75f &&
                               Time.realtimeSinceStartup < frontlineDeadline)
                            yield return null;
                        frontlineBlockedAndReleased = blockedHud && !flanker.IsFrontlineBlocked &&
                                                       flanker.transform.position.x >= frontlinePosition.x + 0.75f;
                        while (frontlineTarget.MemberCount > 0)
                            frontlineTarget.ApplyFixedDamage(frontlineTarget.MaximumMemberHealth);
                    }
                }
            }

            var fairOpponentEconomy = false;
            var probeStarted = false;
            var pressureStarted = false;
            var finalAssaultStarted = false;
            var enemyHisarDestroyed = false;
            var victoryResultShown = false;
            var telemetryWritten = false;
            var restartResetMatch = false;
            var friendlyHisarDestroyed = false;
            var defeatResultShown = false;
            var quitButtonReady = false;
            var workerRallyDispatched = false;
            var functionalAudioFeedback = false;
            var combatHealthBarsProvisioned = false;
            var enemyHisarHealthReadable = false;
            if (cavalryCounterWon)
            {
                for (var index = 0; index < economy.FriendlyFormations.Count; index++)
                {
                    var friendly = economy.FriendlyFormations[index];
                    friendly.IssueStop();
                    friendly.GetComponent<UnityEngine.AI.NavMeshAgent>()?.Warp(
                        new Vector3(-3f + index * 6f, 0f, 0f));
                }
                economy.FogOfWar.RefreshNow();
                economy.SetOpponentTargetsAvailableForAutomation(true);
                economy.SetOpponentEnabledForAutomation(true);
                Time.timeScale = 10f;
                var aiSimulationDeadline = economy.MatchElapsedSeconds + 360f;
                while (economy.OpponentFormationsProduced < 2 &&
                       economy.MatchElapsedSeconds < aiSimulationDeadline)
                    yield return null;
                var completedHostileBuildings = economy.EnemyBuildings.Count(building => building.IsComplete);
                fairOpponentEconomy = SmokeVerificationRules.HasFairOpponentEconomy(
                    economy.OpponentFormationsProduced, completedHostileBuildings,
                    economy.OpponentPopulationCapacity, economy.CurrentMatchSummary.hostileSuppliesGathered);
                Debug.Log($"SMOKE_OPPONENT_STATE:{DescribeOpponentState(economy)}");
                combatHealthBarsProvisioned = economy.FriendlyHisar.GetComponent<WorldHealthBar>() != null &&
                    economy.EnemyHisar.GetComponent<WorldHealthBar>() != null &&
                    economy.Workers.All(worker => worker.GetComponent<WorldHealthBar>() != null) &&
                    economy.EnemyWorkers.All(worker => worker.GetComponent<WorldHealthBar>() != null) &&
                    economy.FriendlyFormations.All(formation => formation.GetComponent<WorldHealthBar>() != null) &&
                    economy.EnemyFormations.All(formation => formation.GetComponent<WorldHealthBar>() != null) &&
                    economy.Houses.All(building => building.GetComponent<WorldHealthBar>() != null) &&
                    economy.Storehouses.All(building => building.GetComponent<WorldHealthBar>() != null) &&
                    economy.Watchtowers.All(building => building.GetComponent<WorldHealthBar>() != null) &&
                    economy.EnemyBuildings.All(building => building.GetComponent<WorldHealthBar>() != null);

                Time.timeScale = 0f;
                economy.AdvanceMatchClockForAutomation(Mathf.Max(0f, 180f - economy.MatchElapsedSeconds));
                yield return null;
                probeStarted = economy.OpponentPhase == AiPhase.Probe;
                economy.AdvanceMatchClockForAutomation(Mathf.Max(0f, 360f - economy.MatchElapsedSeconds));
                yield return null;
                pressureStarted = economy.OpponentPhase == AiPhase.Pressure;
                economy.AdvanceMatchClockForAutomation(Mathf.Max(0f, 600f - economy.MatchElapsedSeconds));
                yield return null;
                finalAssaultStarted = economy.OpponentPhase == AiPhase.FinalAssault;

                economy.SetOpponentEnabledForAutomation(false);
                foreach (var hostile in economy.EnemyFormations.ToArray())
                {
                    hostile.IssueStop();
                    while (hostile.MemberCount > 0) hostile.ApplyFixedDamage(hostile.MaximumMemberHealth);
                }
                while (economy.EnemyFormations.Count > 0) yield return null;
                economy.DeployFriendlyForAutomation(FormationType.Spearmen, new Vector3(0f, 0f, 18f));
                for (var index = 0; index < economy.FriendlyFormations.Count; index++)
                    economy.FriendlyFormations[index].GetComponent<UnityEngine.AI.NavMeshAgent>()?.Warp(
                        new Vector3(-3f + index * 6f, 0f, 18f));
                economy.FogOfWar.RefreshNow();
                foreach (var friendly in economy.FriendlyFormations)
                    friendly.IssueFocus(economy.EnemyHisar);
                Time.timeScale = 10f;
                var resultDeadline = Time.realtimeSinceStartup + CombatTimeoutSeconds;
                var healthFrameCaptured = false;
                while (economy.Outcome == MatchOutcome.InProgress && Time.realtimeSinceStartup < resultDeadline)
                {
                    var healthBar = economy.EnemyHisar.GetComponent<WorldHealthBar>();
                    enemyHisarHealthReadable |= healthBar != null && healthBar.IsVisible &&
                                                healthBar.FillFraction > 0f && healthBar.FillFraction < 1f;
                    if (graphical && enemyHisarHealthReadable && !healthFrameCaptured)
                    {
                        Time.timeScale = 0f;
                        ScreenCapture.CaptureScreenshot(healthScreenshotPath);
                        yield return new WaitForEndOfFrame();
                        var healthFrameDeadline = Time.realtimeSinceStartup + ScreenshotTimeoutSeconds;
                        while (!HasContent(healthScreenshotPath) && Time.realtimeSinceStartup < healthFrameDeadline)
                            yield return null;
                        healthFrameCaptured = HasContent(healthScreenshotPath);
                        Time.timeScale = 10f;
                    }
                    yield return null;
                }
                enemyHisarDestroyed = economy.EnemyHisar.IsDestroyed;
                victoryResultShown = economy.Outcome == MatchOutcome.Victory &&
                                     GameObject.Find("Match Result Title")?.GetComponent<UnityEngine.UI.Text>().text ==
                                     "VICTORY";
                telemetryWritten = HasContent(economy.MatchSummaryPath) && HasContent(economy.MatchEventLogPath);
                functionalAudioFeedback = economy.GameplayAudio.HasAllFunctionalCues &&
                                          economy.GameplayAudio.CountFor(GameplayCue.Selection) > 0 &&
                                          economy.GameplayAudio.CountFor(GameplayCue.Order) > 0 &&
                                          economy.GameplayAudio.CountFor(GameplayCue.Construction) > 0 &&
                                          economy.GameplayAudio.CountFor(GameplayCue.Production) > 0 &&
                                          economy.GameplayAudio.CountFor(GameplayCue.Attack) > 0 &&
                                          economy.GameplayAudio.CountFor(GameplayCue.Hit) > 0 &&
                                          economy.GameplayAudio.CountFor(GameplayCue.Victory) > 0;

                var completedEconomy = economy;
                economy.RestartMatch();
                var restartDeadline = Time.realtimeSinceStartup + ConstructionTimeoutSeconds;
                while ((economy == null || economy == completedEconomy) &&
                       Time.realtimeSinceStartup < restartDeadline)
                {
                    economy = FindAnyObjectByType<StartingEconomyController>();
                    yield return null;
                }
                restartResetMatch = economy != null && economy != completedEconomy &&
                                    economy.Outcome == MatchOutcome.InProgress &&
                                    economy.Supplies == economy.StartingSupplies &&
                                    economy.FriendlyFormations.Count == 0;
                if (restartResetMatch)
                {
                    economy.SetOpponentEnabledForAutomation(false);
                    var rallyCache = economy.Caches[0];
                    economy.SetHisarRallyForAutomation(rallyCache.transform.position, rallyCache);
                    economy.TryQueueWorker();
                    var rallyDeadline = Time.realtimeSinceStartup + EconomyTimeoutSeconds;
                    while (economy.Workers.Count == StartingEconomyController.WorkerCount &&
                           Time.realtimeSinceStartup < rallyDeadline) yield return null;
                    workerRallyDispatched = economy.Workers.Count == StartingEconomyController.WorkerCount + 1 &&
                        economy.Workers.Last().TargetCache == rallyCache &&
                        economy.Workers.Last().CurrentActivity != WorkerAgent.Activity.Idle;
                    var invaders = economy.DeployEnemyForAutomation(FormationType.Cavalry,
                        economy.FriendlyHisar.transform.position + Vector3.forward * 7f);
                    invaders.IssueFocus(economy.FriendlyHisar);
                    Time.timeScale = 10f;
                    resultDeadline = Time.realtimeSinceStartup + CombatTimeoutSeconds;
                    while (economy.Outcome == MatchOutcome.InProgress &&
                           Time.realtimeSinceStartup < resultDeadline) yield return null;
                    friendlyHisarDestroyed = economy.FriendlyHisar.IsDestroyed;
                    defeatResultShown = economy.Outcome == MatchOutcome.Defeat &&
                                        GameObject.Find("Match Result Title")?.GetComponent<UnityEngine.UI.Text>().text ==
                                        "DEFEAT";
                    var quitButton = GameObject.Find("Quit Match")?.GetComponent<UnityEngine.UI.Button>();
                    quitButtonReady = quitButton != null && quitButton.gameObject.activeInHierarchy &&
                                      quitButton.interactable;
                    functionalAudioFeedback &= economy.GameplayAudio.CountFor(GameplayCue.Warning) > 0 &&
                                               economy.GameplayAudio.CountFor(GameplayCue.Defeat) > 0 &&
                                               economy.FogOfWar.AttackPingCount > 0;
                }
            }
            yield return null;

            if (graphical)
            {
                var screenshotDirectory = Path.GetDirectoryName(screenshotPath);
                if (!string.IsNullOrEmpty(screenshotDirectory)) Directory.CreateDirectory(screenshotDirectory);
                ScreenCapture.CaptureScreenshot(screenshotPath);
                yield return new WaitForEndOfFrame();
                var deadline = Time.realtimeSinceStartup + ScreenshotTimeoutSeconds;
                while (!HasContent(screenshotPath) && Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                }
            }

            var checks = new[]
            {
                "Bootstrap scene loaded",
                "Required bootstrap objects available",
                "Development player running",
                "Starting economy available",
                "Worker gather deposit completed",
                "House construction completed",
                "Population capacity increased",
                "Storehouse construction completed",
                "Watchtower construction completed",
                "Nearest Storehouse drop-off used",
                "Watchtower automatically damaged a hostile formation",
                "Archer formation trained",
                "Formation visuals use supported faction materials",
                "Arrows use a supported material",
                "Nonlethal hits show visible feedback",
                "Counter fight won",
                "Cavalry formation trained",
                "Two-formation control group assigned and recalled",
                "Stop halts the selected formation group",
                "Hostile mobile formation starts hidden by fog",
                "Formation movement reveals hostile contact",
                "Moving away loses contact while preserving explored ground",
                "Cavalry wins its Archer counter fight",
                "Fair opponent economy builds and trains through real resources",
                "AI Cavalry probe begins at the configured phase",
                "AI mixed pressure begins at the configured phase",
                "AI final Hisar assault begins at the configured phase",
                "Karasungur formations destroy the Alazhan Hisar",
                "Victory result freezes the match",
                "Match summary and event log are written locally",
                "Restart creates a fresh match",
                "Alazhan formations destroy the Karasungur Hisar",
                "Defeat result freezes the match",
                "Shipped Quit button invokes process exit",
                "Unexplored opponent Supply caches are hidden by fog",
                "Hidden Supply-cache depletion does not leak through fog",
                "Opponent recovers a failed gathering route through a paid Worker-built Storehouse",
                "Hisar rally dispatches a formation to terrain and a Worker to a visible cache",
                "Procedural gameplay audio and fog-aware under-attack ping are functional",
                "Combat health bars cover both factions and expose Hisar damage",
                "Rear formation attack gains a deterministic flank advantage",
                "Opposing frontline blocks direct movement and releases laterally",
                "Formation soldiers move independently and close their assigned slots",
                "Formation soldiers settle beside blocked slots and reform when space opens",
                "Arrows visibly resolve against individual soldiers",
                "A soldier casualty leaves survivors to regroup without teleporting"
            }.Concat(graphical
                ? new[] { "1920x1080 window configured", "Graphical health-bar frame captured", "Graphical frame captured" }
                : Array.Empty<string>()).ToArray();
            var result = new SmokeResult
            {
                scene = SceneManager.GetActiveScene().name,
                checks = checks
            };

            try
            {
                Require(result.scene == HarnessContract.SceneName, checks[0]);
                Require(HarnessContract.HasRequiredObjects(name => GameObject.Find(name) != null), checks[1]);
                Require(Debug.isDebugBuild, checks[2]);
                Require(economyStarted, checks[3]);
                Require(economyCompleted, $"{checks[4]} within {EconomyTimeoutSeconds} seconds");
                Require(houseCompleted, $"{checks[5]} within {ConstructionTimeoutSeconds} seconds");
                Require(populationCapacityIncreased, checks[6]);
                Require(storehouseStarted && defensiveBuildingsCompleted, checks[7]);
                Require(watchtowerStarted && defensiveBuildingsCompleted, checks[8]);
                Require(storehouseDropOffUsed, checks[9]);
                Require(watchtowerFired, checks[10]);
                Require(trainingStarted, checks[11]);
                Require(supportedFormationMaterials, checks[12]);
                Require(supportedArrowMaterial, checks[13]);
                Require(nonlethalHitFeedback, checks[14]);
                Require(combatWon, $"{checks[15]} within {CombatTimeoutSeconds} seconds");
                Require(cavalryTrained, $"{checks[16]} within {CombatTimeoutSeconds} seconds");
                Require(controlGroupRecalled, checks[17]);
                Require(formationGroupStopped, checks[18]);
                Require(hostileHiddenByFog, checks[19]);
                Require(hostileRevealedByMovement, $"{checks[20]} within {CombatTimeoutSeconds} seconds");
                Require(contactLostUnderFog, $"{checks[21]} within {CombatTimeoutSeconds} seconds");
                Require(cavalryCounterWon, $"{checks[22]} within {CombatTimeoutSeconds} seconds");
                Require(fairOpponentEconomy, $"{checks[23]} within {EconomyTimeoutSeconds} seconds");
                Require(probeStarted, checks[24]);
                Require(pressureStarted, checks[25]);
                Require(finalAssaultStarted, checks[26]);
                Require(enemyHisarDestroyed, $"{checks[27]} within {CombatTimeoutSeconds} seconds");
                Require(victoryResultShown, checks[28]);
                Require(telemetryWritten, checks[29]);
                Require(restartResetMatch, checks[30]);
                Require(friendlyHisarDestroyed, $"{checks[31]} within {CombatTimeoutSeconds} seconds");
                Require(defeatResultShown, checks[32]);
                Require(quitButtonReady, checks[33]);
                Require(opponentCachesHiddenByFog, checks[34]);
                Require(hiddenCacheDepletionStayedHidden, checks[35]);
                Require(opponentStorehouseRecovered, checks[36]);
                Require(formationRallyDispatched && workerRallyDispatched, checks[37]);
                Require(functionalAudioFeedback, checks[38]);
                Require(combatHealthBarsProvisioned && enemyHisarHealthReadable, checks[39]);
                Require(rearAttackAdvantage, checks[40]);
                Require(frontlineBlockedAndReleased, checks[41]);
                Require(memberFlowObserved, checks[42]);
                Require(blockedMemberRecovered, checks[43]);
                Require(memberProjectileHitObserved, checks[44]);
                Require(memberCasualtyRegrouped, checks[45]);
                if (graphical)
                {
                    Require(Screen.width == 1920 && Screen.height == 1080, checks[46]);
                    Require(HasContent(healthScreenshotPath),
                        $"{checks[47]} within {ScreenshotTimeoutSeconds} seconds");
                    Require(HasContent(screenshotPath), $"{checks[48]} within {ScreenshotTimeoutSeconds} seconds");
                }

                result.passed = true;
            }
            catch (Exception exception)
            {
                result.error = exception.Message;
            }

            var outputPath = GetArgumentValue("--smoke-output")
                ?? Path.Combine(Application.persistentDataPath, "smoke-result.json");
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(outputPath, JsonUtility.ToJson(result, true));
            Debug.Log($"SMOKE_TEST:{(result.passed ? "PASS" : "FAIL")}:{outputPath}");
            yield return null;
            if (!result.passed)
            {
                Application.Quit(1);
                yield break;
            }

            var shippedQuitButton = GameObject.Find("Quit Match")?.GetComponent<UnityEngine.UI.Button>();
            Debug.Log("SMOKE_TEST:QUIT_BUTTON_INVOKED");
            shippedQuitButton.onClick.Invoke();
            while (true) yield return null;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static bool HasContent(string path) => File.Exists(path) && new FileInfo(path).Length > 0;

        private static string DescribeOpponentState(StartingEconomyController economy)
        {
            var formations = string.Join(",", economy.EnemyFormations.Select(formation =>
                $"{formation.Type}:{formation.MemberCount}:{formation.CurrentOrder}"));
            var workers = string.Join(",", economy.EnemyWorkers.Select(worker =>
                $"{worker.CurrentActivity}:carry={worker.CarriedSupplies}:cache={worker.TargetCache?.Remaining ?? -1}:" +
                $"build={worker.CurrentConstruction?.Type.ToString() ?? "none"}"));
            var buildings = string.Join(",", economy.EnemyBuildings.Select(building =>
                $"{building.Type}:complete={building.IsComplete}:progress={building.Progress:0.00}"));
            var caches = string.Join(",", economy.Caches.Concat(economy.OpponentCaches)
                .Select(cache => cache.Remaining));
            return $"formations=[{formations}] formationsProduced={economy.OpponentFormationsProduced} " +
                   $"entitiesProduced={economy.CurrentMatchSummary.hostileEntitiesProduced} " +
                   $"queue={economy.OpponentProductionQueueCount} workers=[{workers}] " +
                   $"wallet={economy.OpponentSupplies} caches=[{caches}] buildings=[{buildings}] " +
                   $"population={economy.OpponentPopulationUsed}/{economy.OpponentPopulationCapacity} " +
                   $"gathered={economy.CurrentMatchSummary.hostileSuppliesGathered}";
        }

        private static bool HasArgument(string name) => Array.IndexOf(Environment.GetCommandLineArgs(), name) >= 0;

        private static string GetArgumentValue(string name)
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
