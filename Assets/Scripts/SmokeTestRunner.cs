using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AshesOfRum
{
    public sealed class SmokeTestRunner : MonoBehaviour
    {
        private const float ScreenshotTimeoutSeconds = 15f;
        private const float EconomyTimeoutSeconds = 20f;
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
            if (!HasArgument("--smoke-test")) return;
            var runner = new GameObject("SmokeTestRunner");
            DontDestroyOnLoad(runner);
            runner.AddComponent<SmokeTestRunner>().StartCoroutine(Run());
        }

        private static IEnumerator Run()
        {
            var screenshotPath = GetArgumentValue("--smoke-screenshot");
            var graphical = !string.IsNullOrEmpty(screenshotPath);
            if (graphical) Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
            yield return null;
            yield return new WaitForEndOfFrame();

            var economy = FindAnyObjectByType<StartingEconomyController>();
            var economyStarted = economy != null && economy.Workers.Count == StartingEconomyController.WorkerCount;
            var opponentCachesHiddenByFog = false;
            var hiddenCacheDepletionStayedHidden = false;
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
            if (defensiveBuildingsCompleted && storehouseDropOffUsed)
            {
                economy.CreditSuppliesForAutomation(400);
                trainingStarted = economy.TryQueueFormation(FormationType.Archers);
                var combatDeadline = Time.realtimeSinceStartup + CombatTimeoutSeconds;
                while (economy.FriendlyFormations.Count == 0 && Time.realtimeSinceStartup < combatDeadline)
                    yield return null;
                if (economy.FriendlyFormations.Count > 0)
                {
                    economy.DeployEnemyForAutomation(FormationType.Spearmen, new Vector3(0f, 0f, 17f));
                    var friendly = economy.FriendlyFormations[0];
                    var hostile = economy.EnemyFormations[0];
                    var tower = economy.Watchtowers[0].GetComponent<WatchtowerAttack>();
                    supportedFormationMaterials = friendly.HasSupportedVisualMaterials() &&
                                                  hostile.HasSupportedVisualMaterials();
                    while (hostile.MemberCount == 8 && Time.realtimeSinceStartup < combatDeadline)
                        yield return null;
                    watchtowerFired = tower.ShotsFired > 0 && hostile.MemberCount < 8;
                    hostile.ApplyDeterministicHit(FormationType.Spearmen);
                    foreach (var visual in hostile.GetComponentsInChildren<FormationMemberVisual>())
                        nonlethalHitFeedback |= visual.IsShowingHitFeedback;
                    economy.IssueFocusForSmoke(friendly, hostile);
                }
                while (economy.EnemyFormations.Count > 0 && Time.realtimeSinceStartup < combatDeadline)
                {
                    var arrow = GameObject.Find("Arrow");
                    if (arrow != null)
                        supportedArrowMaterial |= FormationAgent.UsesSupportedMaterial(arrow.GetComponent<Renderer>());
                    yield return null;
                }
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
                    economy.StopSelectedFormations();
                    var stoppedPositions = economy.SelectedFormations
                        .Select(formation => formation.transform.position).ToArray();
                    yield return new WaitForSeconds(0.25f);
                    formationGroupStopped = economy.SelectedFormations.All(formation =>
                        formation.CurrentOrder == FormationOrder.Idle && !formation.HasDestination);
                    for (var index = 0; index < stoppedPositions.Length; index++)
                        formationGroupStopped &= Vector3.Distance(stoppedPositions[index],
                            economy.SelectedFormations[index].transform.position) < 0.15f;

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
            var quitActionAvailable = false;
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
                var aiDeadline = Time.realtimeSinceStartup + EconomyTimeoutSeconds;
                while (economy.EnemyFormations.Count < 2 && Time.realtimeSinceStartup < aiDeadline)
                    yield return null;
                fairOpponentEconomy = economy.EnemyFormations.Count >= 2 &&
                                      economy.EnemyBuildings.Any(building => building.IsComplete) &&
                                      economy.OpponentPopulationUsed == 20 &&
                                      economy.OpponentPopulationCapacity == 20;

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
                foreach (var hostile in economy.EnemyFormations) hostile.IssueStop();
                for (var index = 0; index < economy.FriendlyFormations.Count; index++)
                    economy.FriendlyFormations[index].GetComponent<UnityEngine.AI.NavMeshAgent>()?.Warp(
                        new Vector3(-3f + index * 6f, 0f, 18f));
                economy.FogOfWar.RefreshNow();
                foreach (var friendly in economy.FriendlyFormations)
                    friendly.IssueFocus(economy.EnemyHisar);
                Time.timeScale = 10f;
                var resultDeadline = Time.realtimeSinceStartup + CombatTimeoutSeconds;
                while (economy.Outcome == MatchOutcome.InProgress && Time.realtimeSinceStartup < resultDeadline)
                    yield return null;
                enemyHisarDestroyed = economy.EnemyHisar.IsDestroyed;
                victoryResultShown = economy.Outcome == MatchOutcome.Victory &&
                                     GameObject.Find("Match Result Title")?.GetComponent<UnityEngine.UI.Text>().text ==
                                     "VICTORY";
                telemetryWritten = HasContent(economy.MatchSummaryPath) && HasContent(economy.MatchEventLogPath);

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
                    economy.RequestQuitForAutomation();
                    quitActionAvailable = economy.QuitRequested;
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

            var checks = graphical
                ? new[]
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
                    "Quit action is available from the result",
                    "Unexplored opponent Supply caches are hidden by fog",
                    "Hidden Supply-cache depletion does not leak through fog",
                    "1920x1080 window configured",
                    "Graphical frame captured"
                }
                : new[]
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
                    "Quit action is available from the result",
                    "Unexplored opponent Supply caches are hidden by fog",
                    "Hidden Supply-cache depletion does not leak through fog"
                };
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
                Require(quitActionAvailable, checks[33]);
                Require(opponentCachesHiddenByFog, checks[34]);
                Require(hiddenCacheDepletionStayedHidden, checks[35]);
                if (graphical)
                {
                    Require(Screen.width == 1920 && Screen.height == 1080, checks[36]);
                    Require(HasContent(screenshotPath), $"{checks[37]} within {ScreenshotTimeoutSeconds} seconds");
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
            Application.Quit(result.passed ? 0 : 1);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static bool HasContent(string path) => File.Exists(path) && new FileInfo(path).Length > 0;

        private static bool HasArgument(string name) => Array.IndexOf(Environment.GetCommandLineArgs(), name) >= 0;

        private static string GetArgumentValue(string name)
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
