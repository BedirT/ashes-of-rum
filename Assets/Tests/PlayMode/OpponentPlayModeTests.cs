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
        public IEnumerator ManualWorkerAndStructureFocus_SurviveIncomingFormationFire()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var tuning = GetPrivateField<EconomyTuning>(economy, "tuning");
            var workerFocused = CreateFormationForTest("Worker-focused formation",
                FormationType.Archers, true, tuning);
            var workerAttackers = CreateFormationForTest("Incoming worker-focus attackers",
                FormationType.Spearmen, false, tuning);
            var structureFocused = CreateFormationForTest("Structure-focused formation",
                FormationType.Archers, true, tuning);
            var structureAttackers = CreateFormationForTest("Incoming structure-focus attackers",
                FormationType.Spearmen, false, tuning);
            workerFocused.transform.position = new Vector3(100f, 0f, 100f);
            workerAttackers.transform.position = new Vector3(98f, 0f, 100f);
            structureFocused.transform.position = new Vector3(100f, 0f, 120f);
            structureAttackers.transform.position = new Vector3(98f, 0f, 120f);
            var structureObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            structureObject.name = "Manual focus structure";
            structureObject.transform.position = new Vector3(100f, 0f, 125f);
            var structure = structureObject.AddComponent<ConstructibleBuilding>();
            structure.Initialize(BuildingType.Storehouse, 0.1f, 100, Color.red, _ => { }, false);
            structure.Advance(0.1f);

            try
            {
                var worker = economy.EnemyWorkers.First(candidate => candidate.IsAlive);
                Assert.That(workerFocused.IssueFocus(worker), Is.True);
                Assert.That(workerAttackers.IssueFocus(workerFocused), Is.True);
                var workerFocusedHealth = workerFocused.TotalMemberHealth;
                Assert.That(workerAttackers.ExecuteAttackVolley(workerFocused), Is.True);
                yield return null;

                Assert.That(workerFocused.TotalMemberHealth, Is.LessThan(workerFocusedHealth));
                Assert.That(workerFocused.WorkerTarget, Is.SameAs(worker));
                Assert.That(workerFocused.Target, Is.Null,
                    "Incoming formation fire must not replace a manual worker focus order.");
                Assert.That(workerFocused.CurrentOrder, Is.EqualTo(FormationOrder.Focus));

                Assert.That(structureFocused.IssueFocus(structure), Is.True);
                Assert.That(structureAttackers.IssueFocus(structureFocused), Is.True);
                var structureFocusedHealth = structureFocused.TotalMemberHealth;
                Assert.That(structureAttackers.ExecuteAttackVolley(structureFocused), Is.True);
                yield return null;

                Assert.That(structureFocused.TotalMemberHealth, Is.LessThan(structureFocusedHealth));
                Assert.That(ReferenceEquals(structureFocused.StructureTarget, structure), Is.True);
                Assert.That(structureFocused.Target, Is.Null,
                    "Incoming formation fire must not replace a manual structure focus order.");
                Assert.That(structureFocused.CurrentOrder, Is.EqualTo(FormationOrder.Focus));
            }
            finally
            {
                Object.Destroy(workerFocused.gameObject);
                Object.Destroy(workerAttackers.gameObject);
                Object.Destroy(structureFocused.gameObject);
                Object.Destroy(structureAttackers.gameObject);
                Object.Destroy(structureObject);
            }
        }

        [UnityTest]
        public IEnumerator HostileFocus_RequiresSideVisionAndDropsAfterTheTargetRetreats()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            Assert.That(economy.TryPlaceHouse(economy.Workers[0], VisibleHouseSite), Is.True);
            yield return WaitUntil(() => economy.PopulationCapacity == 20);
            economy.CreditSuppliesForAutomation(800);
            Assert.That(economy.TryQueueFormation(FormationType.Cavalry), Is.True);
            Assert.That(economy.TryQueueFormation(FormationType.Spearmen), Is.True);
            yield return WaitUntil(() => economy.FriendlyFormations.Count == 2);
            economy.DeployEnemyForAutomation(FormationType.Archers, new Vector3(0f, 0f, 17f));

            var scout = economy.FriendlyFormations.Single(formation => formation.Type == FormationType.Cavalry);
            var remoteAttacker = economy.FriendlyFormations.Single(formation => formation.Type == FormationType.Spearmen);
            var hostile = economy.EnemyFormations[0];
            Assert.That(scout.GetComponent<NavMeshAgent>().Warp(new Vector3(0f, 0f, 8f)), Is.True);
            Assert.That(remoteAttacker.GetComponent<NavMeshAgent>().Warp(new Vector3(-5f, 0f, -5f)), Is.True);
            Assert.That(hostile.GetComponent<NavMeshAgent>().Warp(new Vector3(0f, 0f, 17f)), Is.True);
            economy.FogOfWar.RefreshNow();
            Assert.That(economy.FogOfWar.IsCurrentlyVisible(hostile), Is.True,
                "The nearby scout should reveal the hostile to the player side.");

            Assert.That(remoteAttacker.IssueFocus(hostile), Is.True);
            Assert.That(hostile.Target, Is.Null,
                "A scout revealing a hostile must not reveal a remote attacker to the hostile side.");

            var remoteHealth = remoteAttacker.TotalMemberHealth;
            yield return WaitUntil(() => hostile.Target == remoteAttacker);
            yield return WaitUntil(() => remoteAttacker.TotalMemberHealth < remoteHealth);
            Assert.That(hostile.LastAttackMemberCount, Is.GreaterThan(0),
                "The hostile must retry retaliation after the focused attacker enters its vision and return fire.");
            remoteAttacker.IssueStop();
            hostile.IssueStop();
            Assert.That(remoteAttacker.GetComponent<NavMeshAgent>().Warp(new Vector3(-5f, 0f, -5f)), Is.True);
            Assert.That(hostile.GetComponent<NavMeshAgent>().Warp(new Vector3(0f, 0f, 17f)), Is.True);
            economy.FogOfWar.RefreshNow();

            Assert.That(scout.IssueFocus(hostile), Is.True);
            Assert.That(hostile.Target, Is.SameAs(scout));
            scout.IssueMove(new Vector3(-5f, 0f, -5f));
            Assert.That(scout.GetComponent<NavMeshAgent>().Warp(new Vector3(-5f, 0f, -5f)), Is.True);
            yield return WaitUntil(() => hostile.Target == null);

            var stoppedPosition = hostile.transform.position;
            yield return new WaitForSeconds(0.2f);
            Assert.That(hostile.CurrentOrder, Is.EqualTo(FormationOrder.Idle));
            Assert.That(Vector3.Distance(stoppedPosition, hostile.transform.position), Is.LessThan(0.15f),
                "The hostile must stop instead of following a moving target outside current sight.");
        }

        [UnityTest]
        public IEnumerator AttackMove_ResumesItsRouteAfterTransientRetaliationTargetIsRemoved()
        {
            var tuning = ScriptableObject.CreateInstance<EconomyTuning>();
            var movingFormation = CreateFormationForTest("Attack-move retaliation defender",
                FormationType.Spearmen, true, tuning);
            var hostile = CreateFormationForTest("Transient retaliation attacker",
                FormationType.Archers, false, tuning);
            movingFormation.transform.position = Vector3.zero;
            hostile.transform.position = Vector3.left * 8f;
            var destination = Vector3.forward * 12f;

            try
            {
                movingFormation.IssueAttackMove(destination);
                yield return new WaitForSeconds(0.1f);
                Assert.That(movingFormation.transform.position.z, Is.GreaterThan(0f),
                    "The formation must be following its attack-move route before retaliation begins.");

                Assert.That(hostile.IssueFocus(movingFormation), Is.True);
                Assert.That(movingFormation.Target, Is.SameAs(hostile));
                Assert.That(movingFormation.CurrentOrder, Is.EqualTo(FormationOrder.AttackMove));
                Assert.That(movingFormation.HasDestination, Is.True);
                Assert.That(movingFormation.Destination, Is.EqualTo(destination));

                Object.Destroy(hostile.gameObject);
                yield return null;
                var resumeDistance = Vector3.Distance(movingFormation.transform.position, destination);
                yield return new WaitForSeconds(0.2f);

                Assert.That(movingFormation.Target, Is.Null);
                Assert.That(movingFormation.CurrentOrder, Is.EqualTo(FormationOrder.AttackMove));
                Assert.That(movingFormation.HasDestination, Is.True);
                Assert.That(Vector3.Distance(movingFormation.transform.position, destination),
                    Is.LessThan(resumeDistance - 0.25f),
                    "The formation must resume progress toward its original attack-move destination.");
            }
            finally
            {
                Object.Destroy(movingFormation.gameObject);
                if (hostile != null) Object.Destroy(hostile.gameObject);
                Object.Destroy(tuning);
            }
        }

        [UnityTest]
        public IEnumerator AttackMove_RevealsAndAcquiresTheNearestHostileThroughFog()
        {
            yield return LoadEconomy(NavigationSimulationSpeed);
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            Assert.That(economy.TryPlaceHouse(economy.Workers[0], VisibleHouseSite), Is.True);
            yield return WaitUntil(() => economy.PopulationCapacity == 20);
            economy.CreditSuppliesForAutomation(400);
            Assert.That(economy.TryQueueFormation(FormationType.Cavalry), Is.True);
            yield return WaitUntil(() => economy.FriendlyFormations.Count == 1);
            economy.DeployEnemyForAutomation(FormationType.Archers, new Vector3(0f, 0f, 26f));
            var cavalry = economy.FriendlyFormations[0];
            var archers = economy.EnemyFormations[0];
            economy.FogOfWar.RefreshNow();
            Assert.That(archers.Type, Is.EqualTo(FormationType.Archers));
            Assert.That(economy.FogOfWar.IsCurrentlyVisible(archers), Is.False);
            Assert.That(archers.GetComponentsInChildren<Renderer>(true).Any(item => item.enabled), Is.False);
            var orderText = GameObject.Find("Order").GetComponent<Text>();
            Assert.That(orderText.text, Does.Not.Contain("ENEMY ARCHERS SIGHTED"),
                "Training feedback must not identify an enemy that remains hidden by fog.");

            economy.SelectOnly(cavalry);
            economy.IssueAttackMoveForSelected(new Vector3(0f, 0f, 22f));
            yield return WaitUntil(() => economy.FogOfWar.IsCurrentlyVisible(archers));
            yield return null;
            Assert.That(orderText.text, Does.Contain("ENEMY ARCHERS SIGHTED"),
                "The first current-vision reveal should identify the hostile formation.");
            Assert.That(cavalry.Target, Is.SameAs(archers));
            Assert.That(archers.Target, Is.SameAs(cavalry));
            var hostilePosition = archers.transform.position;
            yield return WaitUntil(() => economy.EnemyFormations.Count == 0);

            Assert.That(cavalry.MemberCount, Is.GreaterThanOrEqualTo(4));
            Assert.That(cavalry.transform.position.z, Is.GreaterThan(0f));
            Assert.That(economy.FogOfWar.MinimapColorAt(hostilePosition).r, Is.LessThan(0.9f));
        }

        [UnityTest]
        public IEnumerator OpponentEconomy_GathersBuildsAndTrainsWithoutHiddenGrants()
        {
            yield return SceneManager.LoadSceneAsync(HarnessContract.SceneName, LoadSceneMode.Single);
            yield return null;
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var originalTimeScale = Time.timeScale;
            try
            {
                Time.timeScale = FastSimulationSpeed;
                yield return WaitUntil(() => economy.EnemyBuildings.Any(building => building.IsComplete) &&
                                             economy.EnemyFormations.Count >= 2);
                Time.timeScale = 0f;

                var tuning = GetPrivateField<EconomyTuning>(economy, "tuning");
                var opponent = GetPrivateField<ScriptedOpponentController>(economy, "opponent");
                var gathered = economy.Caches.Concat(economy.OpponentCaches)
                    .Sum(cache => tuning.cacheSupplies - cache.Remaining);
                var carried = economy.EnemyWorkers.Sum(worker => worker.CarriedSupplies);
                var formationsPaidFor = economy.EnemyFormations.Count + opponent.ProductionQueueCount;
                var spent = economy.EnemyBuildings.Count * tuning.houseCost +
                            formationsPaidFor * tuning.formationCost;
                Assert.That(economy.EnemyBuildings.All(building => building.Type == BuildingType.House), Is.True);
                Assert.That(tuning.startingSupplies + gathered,
                    Is.EqualTo(economy.OpponentSupplies + carried + spent),
                    "The opponent's wallet and spending must reconcile to finite gathered Supplies.");
                Assert.That(economy.OpponentPopulationUsed,
                    Is.EqualTo(StartingEconomyController.WorkerCount +
                               formationsPaidFor * tuning.formationPopulation));
                Assert.That(economy.OpponentPopulationCapacity,
                    Is.EqualTo(tuning.startingPopulationCap + economy.EnemyBuildings.Count(building =>
                        building.IsComplete) * tuning.housePopulationCapacity));
                Assert.That(economy.EnemyWorkers.Count(worker => worker.IsAlive), Is.EqualTo(4));
                Assert.That(economy.EnemyFormations.Select(formation => formation.Type),
                    Does.Contain(FormationType.Cavalry));
                Assert.That(economy.CurrentMatchSummary.hostileSuppliesGathered, Is.EqualTo(gathered - carried));
            }
            finally
            {
                Time.timeScale = originalTimeScale;
            }
        }

        [UnityTest]
        public IEnumerator OpponentEconomy_RecoveryFirstKeepsMonotonicPaidFormationProofAfterLoss()
        {
            yield return SceneManager.LoadSceneAsync(HarnessContract.SceneName, LoadSceneMode.Single);
            yield return null;
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var tuning = GetPrivateField<EconomyTuning>(economy, "tuning");
            var originalTimeScale = Time.timeScale;
            try
            {
                Time.timeScale = FastSimulationSpeed;
                economy.SetOpponentEnabledForAutomation(false);
                economy.SetOpponentTargetsAvailableForAutomation(false);
                economy.CreditOpponentSuppliesForAutomation(tuning.storehouseCost);
                Assert.That(economy.TriggerOpponentRouteFailureForAutomation(), Is.True);
                economy.SetOpponentEnabledForAutomation(true);

                yield return WaitUntil(() => economy.EnemyBuildings.Any(building =>
                    building.Type == BuildingType.Storehouse && building.IsComplete));
                yield return WaitUntil(() => economy.CurrentMatchSummary.hostileSuppliesGathered > 0);
                yield return WaitUntil(() => economy.OpponentFormationsProduced >= 1);

                var firstFormation = economy.EnemyFormations.First();
                while (firstFormation.MemberCount > 0) firstFormation.ApplyFixedDamage(int.MaxValue);
                yield return null;
                yield return WaitUntil(() => economy.OpponentFormationsProduced >= 2);
                Time.timeScale = 0f;

                Assert.That(economy.OpponentFormationsProduced, Is.GreaterThan(economy.EnemyFormations.Count),
                    "Completed production must remain monotonic after the first paid formation is lost.");
                Assert.That(SmokeVerificationRules.HasFairOpponentEconomy(
                    economy.OpponentFormationsProduced,
                    economy.EnemyBuildings.Count(building => building.IsComplete),
                    economy.OpponentPopulationCapacity,
                    economy.CurrentMatchSummary.hostileSuppliesGathered), Is.True);

                var gathered = economy.Caches.Concat(economy.OpponentCaches)
                    .Sum(cache => tuning.cacheSupplies - cache.Remaining);
                var carried = economy.EnemyWorkers.Sum(worker => worker.CarriedSupplies);
                var buildingSpend = economy.EnemyBuildings.Sum(building => building.Type switch
                {
                    BuildingType.House => tuning.houseCost,
                    BuildingType.Storehouse => tuning.storehouseCost,
                    _ => tuning.watchtowerCost
                });
                var formationCommitments = economy.OpponentFormationsProduced +
                                           economy.OpponentProductionQueueCount;
                Assert.That(tuning.startingSupplies + tuning.storehouseCost + gathered,
                    Is.EqualTo(economy.OpponentSupplies + carried + buildingSpend +
                               formationCommitments * tuning.formationCost),
                    "Recovery credit must be fully consumed by the real Storehouse; formations remain paid from finite gathering.");
                Assert.That(economy.OpponentPopulationUsed,
                    Is.EqualTo(StartingEconomyController.WorkerCount +
                               (economy.EnemyFormations.Count + economy.OpponentProductionQueueCount) *
                               tuning.formationPopulation));
            }
            finally
            {
                Time.timeScale = originalTimeScale;
            }
        }

        [UnityTest]
        public IEnumerator OpponentScript_TransitionsThroughProbePressureAndFinalAssault()
        {
            yield return SceneManager.LoadSceneAsync(HarnessContract.SceneName, LoadSceneMode.Single);
            yield return null;
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            Time.timeScale = FastSimulationSpeed;
            yield return WaitUntil(() => economy.EnemyFormations.Count >= 2);
            Time.timeScale = 0f;

            economy.AdvanceMatchClockForAutomation(Mathf.Max(0f, 180f - economy.MatchElapsedSeconds));
            yield return null;
            Assert.That(economy.OpponentPhase, Is.EqualTo(AiPhase.Probe));
            Assert.That(economy.EnemyFormations.Single(formation => formation.Type == FormationType.Cavalry)
                .CurrentOrder, Is.EqualTo(FormationOrder.AttackMove));

            economy.AdvanceMatchClockForAutomation(Mathf.Max(0f, 360f - economy.MatchElapsedSeconds));
            yield return null;
            Assert.That(economy.OpponentPhase, Is.EqualTo(AiPhase.Pressure));
            Assert.That(economy.EnemyFormations.All(formation =>
                formation.CurrentOrder == FormationOrder.AttackMove), Is.True);

            economy.AdvanceMatchClockForAutomation(Mathf.Max(0f, 600f - economy.MatchElapsedSeconds));
            yield return null;
            Assert.That(economy.OpponentPhase, Is.EqualTo(AiPhase.FinalAssault));
            Assert.That(economy.EnemyFormations.All(formation =>
                ReferenceEquals(formation.StructureTarget, economy.FriendlyHisar)), Is.True);
            Assert.That(economy.CurrentMatchSummary.probeAttackSeconds, Is.GreaterThanOrEqualTo(180f));
            Assert.That(economy.CurrentMatchSummary.pressureAttackSeconds, Is.GreaterThanOrEqualTo(360f));
            Assert.That(economy.CurrentMatchSummary.finalAssaultSeconds, Is.GreaterThanOrEqualTo(600f));
            Time.timeScale = 1f;
        }

        [UnityTest]
        public IEnumerator OpponentScript_DefenseAcrossProbeBoundaryRecordsAttackOnlyWhenDispatched()
        {
            yield return SceneManager.LoadSceneAsync(HarnessContract.SceneName, LoadSceneMode.Single);
            yield return null;
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var originalTimeScale = Time.timeScale;

            try
            {
                Time.timeScale = FastSimulationSpeed;
                yield return WaitUntil(() => economy.EnemyFormations.Any(formation =>
                    formation.Type == FormationType.Cavalry));
                Time.timeScale = 0f;
                Assert.That(economy.CurrentMatchSummary.probeAttackSeconds, Is.LessThan(0f));

                var threat = economy.DeployFriendlyForAutomation(FormationType.Spearmen,
                    economy.EnemyHisar.transform.position + Vector3.back * 6f);
                yield return null;
                yield return null;
                Assert.That(economy.OpponentIsDefending, Is.True);

                economy.AdvanceMatchClockForAutomation(Mathf.Max(0f, 180f - economy.MatchElapsedSeconds));
                yield return null;

                Assert.That(economy.OpponentPhase, Is.EqualTo(AiPhase.Probe));
                Assert.That(economy.CurrentMatchSummary.probeAttackSeconds, Is.LessThan(0f),
                    "Crossing the probe boundary during defense must not record an attack that did not depart.");
                Assert.That(economy.EnemyFormations.Any(formation => formation.Target == threat), Is.True);

                Assert.That(threat.GetComponent<NavMeshAgent>().Warp(new Vector3(0f, 0f, -4f)), Is.True);
                yield return null;
                yield return null;

                Assert.That(economy.OpponentIsDefending, Is.False);
                Assert.That(economy.CurrentMatchSummary.probeAttackSeconds,
                    Is.GreaterThanOrEqualTo(180f));
                Assert.That(economy.EnemyFormations.Single(formation => formation.Type == FormationType.Cavalry)
                    .CurrentOrder, Is.EqualTo(FormationOrder.AttackMove));
                var dispatchedAt = economy.CurrentMatchSummary.probeAttackSeconds;
                yield return null;
                Assert.That(economy.CurrentMatchSummary.probeAttackSeconds, Is.EqualTo(dispatchedAt),
                    "A phase's attack callback must be emitted only once.");
            }
            finally
            {
                Time.timeScale = originalTimeScale;
            }
        }

        [UnityTest]
        public IEnumerator OpponentScript_RecallsVisibleFormationsForEarlyBaseDefenseThenResumes()
        {
            yield return SceneManager.LoadSceneAsync(HarnessContract.SceneName, LoadSceneMode.Single);
            yield return null;
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var originalTimeScale = Time.timeScale;
            try
            {
                Time.timeScale = FastSimulationSpeed;
                yield return WaitUntil(() => economy.EnemyFormations.Count >= 1);
                Time.timeScale = 0f;
                for (var index = 0; index < economy.EnemyWorkers.Count; index++)
                    Assert.That(economy.EnemyWorkers[index].GetComponent<NavMeshAgent>()
                        .Warp(new Vector3(14f + index, 0f, 12f)), Is.True);
                for (var index = 0; index < economy.EnemyBuildings.Count; index++)
                    economy.EnemyBuildings[index].transform.position = new Vector3(10f + index * 2f, 0f, 14f);
                for (var index = 0; index < economy.EnemyFormations.Count; index++)
                {
                    economy.EnemyFormations[index].IssueStop();
                    Assert.That(economy.EnemyFormations[index].GetComponent<NavMeshAgent>()
                        .Warp(economy.EnemyHisar.transform.position + Vector3.right * (8f + index * 2f)), Is.True);
                }

                var threat = economy.DeployFriendlyForAutomation(FormationType.Spearmen,
                    economy.EnemyHisar.transform.position + Vector3.left * 11.5f);
                yield return null;
                yield return null;

                Assert.That(economy.OpponentIsDefending, Is.False,
                    "A nearby formation outside the AI side's shared sight radius must not trigger defense.");
                Assert.That(economy.EnemyFormations.All(formation => formation.Target != threat), Is.True);

                Assert.That(threat.GetComponent<NavMeshAgent>()
                    .Warp(economy.EnemyHisar.transform.position + Vector3.back * 6f), Is.True);
                yield return null;
                yield return null;

                Assert.That(economy.OpponentIsDefending, Is.True);
                Assert.That(economy.EnemyFormations.Any(formation => formation.Target == threat), Is.True,
                    "The same threat must be recalled against once the AI side currently sees it.");

                Assert.That(threat.GetComponent<NavMeshAgent>().Warp(new Vector3(0f, 0f, -4f)), Is.True);
                yield return null;
                yield return null;
                Assert.That(economy.OpponentIsDefending, Is.False);
                Assert.That(economy.EnemyFormations.All(formation => formation.Target != threat), Is.True);
            }
            finally
            {
                Time.timeScale = originalTimeScale;
            }
        }

        [UnityTest]
        public IEnumerator OpponentScript_DefendsAVisibleRemoteWorkerThenResumesItsPhase()
        {
            yield return SceneManager.LoadSceneAsync(HarnessContract.SceneName, LoadSceneMode.Single);
            yield return null;
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var originalTimeScale = Time.timeScale;

            try
            {
                Time.timeScale = FastSimulationSpeed;
                yield return WaitUntil(() => economy.EnemyFormations.Any(formation =>
                    formation.Type == FormationType.Cavalry));
                Time.timeScale = 0f;

                var defendedWorker = economy.EnemyWorkers.First(worker => worker.CurrentConstruction == null);
                Assert.That(defendedWorker.GetComponent<NavMeshAgent>().Warp(new Vector3(-15f, 0f, 12f)), Is.True);
                foreach (var worker in economy.EnemyWorkers.Where(worker => worker != defendedWorker))
                    Assert.That(worker.GetComponent<NavMeshAgent>().Warp(new Vector3(17f, 0f, 24f)), Is.True);
                for (var index = 0; index < economy.EnemyBuildings.Count; index++)
                    economy.EnemyBuildings[index].transform.position = new Vector3(15f + index, 0f, 24f);
                foreach (var formation in economy.EnemyFormations)
                {
                    formation.IssueStop();
                    Assert.That(formation.GetComponent<NavMeshAgent>()
                        .Warp(economy.EnemyHisar.transform.position + Vector3.left * 8f), Is.True);
                }

                var threat = economy.DeployFriendlyForAutomation(FormationType.Spearmen,
                    defendedWorker.transform.position + Vector3.left * 4f);
                economy.FogOfWar.RefreshNow();
                Assert.That(economy.FogOfWar.IsCurrentlyVisible(defendedWorker), Is.True);
                Assert.That(threat.IssueFocus(defendedWorker), Is.True);
                yield return null;
                yield return null;

                Assert.That(Vector3.Distance(threat.transform.position, economy.EnemyHisar.transform.position),
                    Is.GreaterThan(12f));
                Assert.That(economy.EnemyBuildings.All(building =>
                    Vector3.Distance(threat.transform.position, building.transform.position) > 10f), Is.True);
                Assert.That(economy.OpponentIsDefending, Is.True,
                    "An attack visible to a remote living worker must trigger the same defensive recall.");
                Assert.That(economy.EnemyFormations.Any(formation => formation.Target == threat), Is.True);

                economy.AdvanceMatchClockForAutomation(Mathf.Max(0f, 180f - economy.MatchElapsedSeconds));
                yield return null;
                Assert.That(economy.OpponentPhase, Is.EqualTo(AiPhase.Probe));
                Assert.That(economy.CurrentMatchSummary.probeAttackSeconds, Is.LessThan(0f));

                threat.IssueStop();
                Assert.That(threat.GetComponent<NavMeshAgent>().Warp(new Vector3(0f, 0f, -4f)), Is.True);
                yield return null;
                yield return null;

                Assert.That(economy.OpponentIsDefending, Is.False);
                Assert.That(economy.CurrentMatchSummary.probeAttackSeconds, Is.GreaterThanOrEqualTo(180f));
                Assert.That(economy.EnemyFormations.Single(formation => formation.Type == FormationType.Cavalry)
                    .CurrentOrder, Is.EqualTo(FormationOrder.AttackMove));
            }
            finally
            {
                Time.timeScale = originalTimeScale;
            }
        }

    }
}
