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
        public IEnumerator HisarQueue_TrainsFormationAndArchersWinReadableCounterFight()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            economy.CreditSuppliesForAutomation(300);
            economy.SelectHisar();
            yield return null;

            Assert.That(GameObject.Find("Train Archers").activeInHierarchy, Is.True);
            Assert.That(economy.TryQueueFormation(FormationType.Archers), Is.True);
            Assert.That(economy.Supplies, Is.Zero);
            Assert.That(economy.PopulationUsed, Is.EqualTo(12));
            Assert.That(economy.CancelActiveTraining(), Is.True);
            Assert.That(economy.Supplies, Is.EqualTo(400));
            Assert.That(economy.PopulationUsed, Is.EqualTo(4));
            Assert.That(economy.TryQueueFormation(FormationType.Archers), Is.True);

            yield return WaitUntil(() => economy.FriendlyFormations.Count == 1);
            economy.DeployEnemyForAutomation(FormationType.Spearmen, new Vector3(0f, 0f, 17f));
            var archers = economy.FriendlyFormations[0];
            var spearmen = economy.EnemyFormations[0];
            Assert.That(archers.MemberCount, Is.EqualTo(8));
            Assert.That(spearmen.MemberCount, Is.EqualTo(8));
            Assert.That(economy.FogOfWar.IsCurrentlyVisible(spearmen), Is.False);
            economy.SelectOnly(archers);
            economy.IssueAttackMoveForSelected(spearmen.transform.position);
            yield return WaitUntil(() => economy.FogOfWar.IsCurrentlyVisible(spearmen));

            yield return WaitUntil(() => economy.EnemyFormations.Count == 0);

            Assert.That(archers.MemberCount, Is.GreaterThanOrEqualTo(4));
            Assert.That(economy.PopulationUsed, Is.EqualTo(4 + archers.MemberCount));
            Assert.That(GameObject.Find("Order").GetComponent<UnityEngine.UI.Text>().text,
                Does.Contain("ENEMY FORMATION DEFEATED"));
        }

        [UnityTest]
        public IEnumerator FormationVisuals_UseSupportedUrpMaterialsForBodiesMarkersRingsAndArrows()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            economy.CreditSuppliesForAutomation(300);
            Assert.That(economy.TryQueueFormation(FormationType.Archers), Is.True);
            yield return WaitUntil(() => economy.FriendlyFormations.Count == 1);
            var archers = economy.FriendlyFormations[0];
            economy.DeployEnemyForAutomation(FormationType.Spearmen, new Vector3(0f, 0f, 17f));
            var spearmen = economy.EnemyFormations[0];

            Assert.That(archers.HasSupportedVisualMaterials(), Is.True);
            Assert.That(spearmen.HasSupportedVisualMaterials(), Is.True);
            var friendlyMarkers = archers.GetComponentsInChildren<Renderer>(true)
                .Where(itemRenderer => itemRenderer.name == "Black Falcon Diamond").ToArray();
            var hostileMarkers = spearmen.GetComponentsInChildren<Renderer>(true)
                .Where(itemRenderer => itemRenderer.name == "Living Flame Square").ToArray();
            Assert.That(friendlyMarkers, Has.Length.EqualTo(8));
            Assert.That(hostileMarkers, Has.Length.EqualTo(8));
            Assert.That(friendlyMarkers.All(itemRenderer => itemRenderer.transform.localRotation != Quaternion.identity),
                Is.True);
            Assert.That(hostileMarkers.All(itemRenderer => itemRenderer.transform.localRotation == Quaternion.identity),
                Is.True);
            Assert.That(archers.GetComponentInChildren<FormationFrontIndicator>(true), Is.Not.Null);
            Assert.That(spearmen.GetComponentInChildren<FormationFrontIndicator>(true), Is.Not.Null);

            Assert.That(archers.ExecuteAttackVolley(spearmen), Is.True);
            yield return null;
            var arrows = GameObject.FindObjectsByType<Renderer>(FindObjectsSortMode.None)
                .Where(itemRenderer => itemRenderer.name == "Arrow").ToArray();
            Assert.That(arrows, Has.Length.EqualTo(8));
            Assert.That(arrows.All(FormationAgent.UsesSupportedMaterial), Is.True);
            archers.ApplyFixedDamage(archers.MaximumMemberHealth);
            Assert.That(archers.GetComponentInChildren<FormationFrontIndicator>(true), Is.Not.Null,
                "The front indicator must survive member casualties.");
        }

        [UnityTest]
        public IEnumerator ArcherPresentation_DirectSpawnsBothFactionsAndExercisesEveryRuntimeState()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var archers = economy.DeployFriendlyForAutomation(FormationType.Archers,
                new Vector3(0f, 0f, 7f));
            var target = economy.DeployEnemyForAutomation(FormationType.Archers,
                new Vector3(0f, 0f, 15f));

            var visuals = archers.GetComponentsInChildren<FormationMemberVisual>();
            var hostileVisuals = target.GetComponentsInChildren<FormationMemberVisual>();
            Assert.That(visuals, Has.Length.EqualTo(8));
            Assert.That(hostileVisuals, Has.Length.EqualTo(8));
            Assert.That(visuals.All(visual => visual.HasAuthoredPresentation), Is.True);
            Assert.That(hostileVisuals.All(visual => visual.HasAuthoredPresentation), Is.True);
            Assert.That(archers.HasSupportedVisualMaterials(), Is.True);
            Assert.That(target.HasSupportedVisualMaterials(), Is.True);
            Assert.That(visuals.All(visual =>
                visual.CurrentAnimationState == ArcherMemberPresentation.IdleState), Is.True);
            Assert.That(archers.GetComponentsInChildren<Animator>(), Has.Length.EqualTo(8));
            Assert.That(archers.GetComponentsInChildren<Animator>()
                .All(animator => !animator.applyRootMotion), Is.True);

            for (var frame = 0; frame < 14; frame++) yield return null;
            Assert.That(archers.GetComponentsInChildren<ArcherMemberPresentation>()
                .All(presentation => Mathf.Abs(presentation.WorldBottomY - archers.transform.position.y) < 0.03f),
                Is.True, "Friendly animated feet must sit on the battlefield surface.");
            Assert.That(target.GetComponentsInChildren<ArcherMemberPresentation>()
                .All(presentation => Mathf.Abs(presentation.WorldBottomY - target.transform.position.y) < 0.03f),
                Is.True, "Hostile animated feet must sit on the battlefield surface.");

            archers.IssueMove(archers.transform.position + Vector3.forward * 4f);
            yield return WaitUntil(() => visuals.Any(visual =>
                visual.CurrentAnimationState == ArcherMemberPresentation.MoveState));

            var memberPositionsBeforeAttack = archers.Members.Select(member => member.WorldPosition).ToArray();
            Assert.That(archers.ExecuteAttackVolley(target), Is.True);
            yield return null;
            Assert.That(visuals.All(visual =>
                visual.CurrentAnimationState == ArcherMemberPresentation.AttackState), Is.True);
            Assert.That(GameObject.FindObjectsByType<AuthoredArrowProjectile>(FindObjectsSortMode.None),
                Has.Length.EqualTo(8));
            Assert.That(archers.Members.Select((member, index) =>
                    Vector3.Distance(member.WorldPosition, memberPositionsBeforeAttack[index]))
                .Max(), Is.LessThan(0.2f),
                "Authored clips must not drive member translation.");

            var casualty = archers.Members[0];
            casualty.ApplyDamage(1, FlankDirection.Side);
            Assert.That(casualty.GetComponent<FormationMemberVisual>().CurrentAnimationState,
                Is.EqualTo(ArcherMemberPresentation.HitState));
            yield return new WaitForSeconds(0.2f);

            archers.ApplyDeterministicHit(casualty, FormationType.Cavalry,
                casualty.WorldPosition + Vector3.forward);
            Assert.That(archers.MemberCount, Is.EqualTo(7),
                "The casualty must leave gameplay immediately.");
            Assert.That(casualty, Is.Not.Null,
                "The authored casualty presentation must remain long enough to show the fall.");
            Assert.That(casualty.GetComponent<FormationMemberVisual>().CurrentAnimationState,
                Is.EqualTo(ArcherMemberPresentation.DeathState));

            Object.Destroy(archers.gameObject);
            Object.Destroy(target.gameObject);
        }

        [UnityTest]
        public IEnumerator Combat_LivingMembersDriveOutputAndNonlethalHitsFlashTheMember()
        {
            var tuning = ScriptableObject.CreateInstance<EconomyTuning>();
            var fullAttackers = CreateFormationForTest("Full attackers", FormationType.Spearmen, true, tuning);
            var reducedAttackers = CreateFormationForTest("Reduced attackers", FormationType.Spearmen, true, tuning);
            var fullTarget = CreateFormationForTest("Full target", FormationType.Archers, false, tuning);
            var reducedTarget = CreateFormationForTest("Reduced target", FormationType.Archers, false, tuning);

            for (var i = 0; i < 7; i++) reducedAttackers.ApplyDeterministicHit(FormationType.Archers);
            Assert.That(reducedAttackers.MemberCount, Is.EqualTo(1));

            var fullHealthBefore = fullTarget.TotalMemberHealth;
            var reducedHealthBefore = reducedTarget.TotalMemberHealth;
            Assert.That(fullAttackers.ExecuteAttackVolley(fullTarget), Is.True);
            Assert.That(reducedAttackers.ExecuteAttackVolley(reducedTarget), Is.True);

            Assert.That(fullAttackers.LastAttackMemberCount, Is.EqualTo(8));
            Assert.That(reducedAttackers.LastAttackMemberCount, Is.EqualTo(1));
            Assert.That(fullHealthBefore - fullTarget.TotalMemberHealth, Is.EqualTo(80));
            Assert.That(reducedHealthBefore - reducedTarget.TotalMemberHealth, Is.EqualTo(10));
            Assert.That(fullTarget.MemberCount, Is.EqualTo(8), "A base-damage volley must be nonlethal per member.");
            Assert.That(fullTarget.GetComponentsInChildren<FormationMemberVisual>()
                .All(visual => visual.IsShowingHitFeedback), Is.True);
            Assert.That(reducedTarget.GetComponentsInChildren<FormationMemberVisual>()
                .Count(visual => visual.IsShowingHitFeedback), Is.EqualTo(1));

            yield return new WaitForSeconds(0.2f);
            Assert.That(fullTarget.GetComponentsInChildren<FormationMemberVisual>()
                .Any(visual => visual.IsShowingHitFeedback), Is.False);

            Object.Destroy(fullAttackers.gameObject);
            Object.Destroy(reducedAttackers.gameObject);
            Object.Destroy(fullTarget.gameObject);
            Object.Destroy(reducedTarget.gameObject);
            Object.Destroy(tuning);
        }

        [UnityTest]
        public IEnumerator MeleeAttackFeedback_FiresOnlyWhenAtLeastOneMemberStrikes()
        {
            var tuning = ScriptableObject.CreateInstance<EconomyTuning>();
            var attackCueCount = 0;
            var attackers = CreateFormationForTest("Cooldown attackers", FormationType.Spearmen, true, tuning,
                onAttack: _ => attackCueCount++);
            var target = CreateFormationForTest("Cooldown target", FormationType.Archers, false, tuning);

            var healthBefore = target.TotalMemberHealth;
            Assert.That(attackers.ExecuteAttackVolley(target), Is.True);
            Assert.That(attackers.LastAttackMemberCount, Is.EqualTo(8));
            Assert.That(attackCueCount, Is.EqualTo(1));
            var healthAfterStrike = target.TotalMemberHealth;
            Assert.That(healthAfterStrike, Is.LessThan(healthBefore));

            for (var frame = 0; frame < 5; frame++)
            {
                Assert.That(attackers.ExecuteAttackVolley(target), Is.True);
                Assert.That(attackers.LastAttackMemberCount, Is.Zero);
                yield return null;
            }

            Assert.That(target.TotalMemberHealth, Is.EqualTo(healthAfterStrike),
                "Cooldown-only frames must not apply another member strike.");
            Assert.That(attackCueCount, Is.EqualTo(1),
                "Cooldown-only frames must not emit formation attack feedback.");

            Object.Destroy(attackers.gameObject);
            Object.Destroy(target.gameObject);
            Object.Destroy(tuning);
        }

        [UnityTest]
        public IEnumerator Combat_ReorientationBlocksAttacksForFixedDurationAndHudShowsFacingState()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var tuning = GetPrivateField<EconomyTuning>(economy, "tuning");
            var attacker = economy.DeployFriendlyForAutomation(FormationType.Spearmen,
                new Vector3(0f, 0f, 7f));
            var defender = economy.DeployEnemyForAutomation(FormationType.Spearmen,
                new Vector3(0f, 0f, 8.5f));
            attacker.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            economy.FogOfWar.RefreshNow();
            economy.SelectOnly(attacker);
            Assert.That(attacker.IssueFocus(defender), Is.True);

            yield return null;
            var healthBefore = defender.TotalMemberHealth;
            Assert.That(attacker.IsTurning, Is.True);
            yield return null;
            Assert.That(GameObject.Find("Selection").GetComponent<UnityEngine.UI.Text>().text,
                Does.Contain("FACING").And.Contain("TURNING"));

            yield return new WaitForSeconds(tuning.reorientationSeconds * 0.75f);
            Assert.That(defender.TotalMemberHealth, Is.EqualTo(healthBefore),
                "The formation must not attack before its fixed reorientation completes.");
            yield return WaitUntil(() => defender.TotalMemberHealth < healthBefore);
            Assert.That(attacker.IsTurning, Is.False);
            Assert.That(Vector3.Angle(attacker.transform.forward,
                defender.transform.position - attacker.transform.position), Is.LessThan(3f));
            attacker.IssueStop();
            Assert.That(attacker.IsTurning, Is.False);
            Assert.That(attacker.CurrentOrder, Is.EqualTo(FormationOrder.Idle));
        }

        [UnityTest]
        public IEnumerator ArcherProjectile_ResolvesFlankAgainstDefenderFacingAtImpact()
        {
            var tuning = ScriptableObject.CreateInstance<EconomyTuning>();
            var archers = CreateFormationForTest("Flanking Archers", FormationType.Archers, true, tuning);
            var target = CreateFormationForTest("Turning Archers", FormationType.Archers, false, tuning);
            archers.transform.position = Vector3.zero;
            target.transform.position = new Vector3(0f, 0f, 6f);
            target.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            var healthBefore = target.TotalMemberHealth;

            Assert.That(archers.ExecuteAttackVolley(target), Is.True);
            yield return null;
            target.transform.rotation = Quaternion.identity;
            yield return new WaitForSeconds(tuning.projectileSeconds + 0.1f);

            var rearDamage = CombatRules.Damage(FormationType.Archers, FormationType.Archers,
                tuning.baseDamage, tuning.counterMultiplier, FlankDirection.Rear,
                tuning.sideDamageMultiplier, tuning.rearDamageMultiplier);
            Assert.That(healthBefore - target.TotalMemberHealth, Is.EqualTo(rearDamage * 8));
            Assert.That(target.LastReceivedFlank, Is.EqualTo(FlankDirection.Rear));
            Assert.That(target.GetComponentsInChildren<FormationMemberVisual>()
                .All(visual => visual.LastHitFlank == FlankDirection.Rear), Is.True,
                "Every projectile impact should display the stronger rear-hit reaction.");
            Object.Destroy(archers.gameObject);
            Object.Destroy(target.gameObject);
            Object.Destroy(tuning);
        }

        [UnityTest]
        public IEnumerator ArcherProjectiles_TrackAndDamageEightIndividualMembers()
        {
            var tuning = ScriptableObject.CreateInstance<EconomyTuning>();
            tuning.reorientationSeconds = 2f;
            var archers = CreateFormationForTest("Individual Archers", FormationType.Archers, true, tuning);
            var cavalry = CreateFormationForTest("Individual Cavalry", FormationType.Cavalry, false, tuning);
            archers.transform.position = Vector3.zero;
            cavalry.transform.position = new Vector3(4f, 0f, 6f);
            yield return null;
            var displacedTarget = cavalry.Members[0];
            displacedTarget.TeleportBy(Vector3.right * 2f);
            var healthBefore = cavalry.Members.ToDictionary(member => member.Identity, member => member.Health);
            archers.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            Assert.That(archers.IssueFocus(cavalry), Is.True);
            yield return new WaitForSeconds(0.3f);

            foreach (var archer in archers.Members)
            {
                var expectedTarget = cavalry.Members[archer.Identity % cavalry.MemberCount];
                Assert.That(archer.AttackTarget, Is.SameAs(expectedTarget));
                Assert.That(Vector3.Angle(archer.transform.forward,
                        expectedTarget.WorldPosition - archer.WorldPosition), Is.LessThan(3f),
                    $"Archer {archer.Identity} must face the same soldier reserved for its projectile.");
            }

            Assert.That(archers.ExecuteAttackVolley(cavalry), Is.True);
            yield return WaitUntil(() => archers.ProjectileHitsLanded == 8);

            Assert.That(cavalry.Members, Has.Count.EqualTo(8));
            Assert.That(cavalry.Members.All(member => member.ProjectileImpactCount == 1), Is.True,
                "A volley must reserve one visible projectile for each living target soldier.");
            Assert.That(archers.ProjectileHitsLanded, Is.EqualTo(8));
            Assert.That(cavalry.Members.All(member => member.Health < healthBefore[member.Identity]), Is.True,
                "Projectile damage must belong to the soldier the arrow visibly reaches.");
            Assert.That(cavalry.Members.All(member =>
                    Vector3.Distance(member.LastProjectileImpactPosition, member.WorldPosition) < 0.35f), Is.True,
                "Each arrow must finish at its moving member target rather than the formation center.");

            Object.Destroy(archers.gameObject);
            Object.Destroy(cavalry.gameObject);
            Object.Destroy(tuning);
        }

        [UnityTest]
        public IEnumerator FormationMembers_FlowBehindTheAnchorAndRegroupWithoutTeleporting()
        {
            var tuning = ScriptableObject.CreateInstance<EconomyTuning>();
            var formation = CreateFormationForTest("Flowing Formation", FormationType.Spearmen, true, tuning);
            formation.transform.position = Vector3.zero;
            yield return null;

            formation.IssueMove(new Vector3(5f, 0f, 5f));
            yield return new WaitForSeconds(0.25f);
            Assert.That(formation.Members.Any(member =>
                    Vector3.Distance(member.WorldPosition, member.AssignedSlotWorldPosition) > 0.15f), Is.True,
                "Members must retain their own world positions while the command anchor moves and turns.");

            yield return new WaitForSeconds(2.5f);
            Assert.That(formation.Members.All(member =>
                    Vector3.Distance(member.WorldPosition, member.AssignedSlotWorldPosition) < 0.45f), Is.True,
                "Members must naturally catch their assigned slots after the formation stops.");

            Object.Destroy(formation.gameObject);
            Object.Destroy(tuning);
        }

        [UnityTest]
        public IEnumerator FormationMember_RoutesAroundCarvedObstacleWithoutLeavingNavMesh()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var formation = economy.DeployFriendlyForAutomation(FormationType.Spearmen,
                new Vector3(0f, 0f, 10f));
            yield return null;
            var member = formation.Members[0];
            var destination = member.AssignedSlotWorldPosition;
            var startPosition = member.WorldPosition + Vector3.left * 6f;
            var blocker = CreateRouteBlocker("Member Route Blocker", new Vector3(-4.7f, 1f, 10f),
                new Vector3(1.5f, 2f, 4f));
            var blockerBounds = blocker.GetComponent<Collider>().bounds;
            var route = new NavMeshPath();
            var groundedStart = new Vector3(startPosition.x, 0f, startPosition.z);
            var groundedDestination = new Vector3(destination.x, 0f, destination.z);

            yield return new WaitForSeconds(1f);
            var sampledStart = NavMesh.SamplePosition(groundedStart, out var start, 0.35f, NavMesh.AllAreas);
            var sampledEnd = NavMesh.SamplePosition(groundedDestination, out var end, 0.35f, NavMesh.AllAreas);
            var calculated = sampledStart && sampledEnd &&
                             NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, route);
            Assert.That(sampledStart, Is.True, $"No NavMesh near member start {groundedStart}.");
            Assert.That(sampledEnd, Is.True, $"No NavMesh near member slot {groundedDestination}.");
            Assert.That(calculated, Is.True, "The carved obstacle must leave a route around its sides.");
            Assert.That(route.status, Is.EqualTo(NavMeshPathStatus.PathComplete));
            Assert.That(route.corners.Length, Is.GreaterThan(2),
                $"The carved obstacle must force a detour, but the route had {route.corners.Length} corners.");
            member.TeleportBy(Vector3.left * 6f);

            var greatestDetour = 0f;
            var previousPosition = member.WorldPosition;
            var deadline = Time.realtimeSinceStartup + 5f;
            while (Vector3.Distance(member.WorldPosition, destination) > 0.45f &&
                   Time.realtimeSinceStartup < deadline)
            {
                var position = member.WorldPosition;
                var frameDisplacement = Vector3.Distance(position, previousPosition);
                var maximumFrameDisplacement = formation.MoveSpeed * 1.3f * Time.deltaTime + 0.025f;
                Assert.That(frameDisplacement, Is.LessThanOrEqualTo(maximumFrameDisplacement),
                    $"A member must traverse path corners at its bounded movement speed. " +
                    $"Moved={frameDisplacement:0.000}, maximum={maximumFrameDisplacement:0.000}.");
                AssertSweptSegmentOutsideBounds(previousPosition, position, blockerBounds);
                Assert.That(blockerBounds.Contains(new Vector3(position.x, blockerBounds.center.y, position.z)),
                    Is.False, "A member must never step through the carved obstacle.");
                var groundedPosition = new Vector3(position.x, 0f, position.z);
                Assert.That(NavMesh.SamplePosition(groundedPosition, out var walkable, 0.1f, NavMesh.AllAreas), Is.True,
                    "Every independently steered member step must remain on the NavMesh.");
                Assert.That(Vector3.Distance(groundedPosition,
                    new Vector3(walkable.position.x, 0f, walkable.position.z)), Is.LessThan(0.1f));
                greatestDetour = Mathf.Max(greatestDetour, Mathf.Abs(position.z - destination.z));
                previousPosition = position;
                yield return null;
            }

            Assert.That(Vector3.Distance(member.WorldPosition, destination), Is.LessThanOrEqualTo(0.45f),
                $"The member must follow the valid route and regroup on the far side. " +
                $"Final={member.WorldPosition}, destination={destination}, detour={greatestDetour:0.00}.");
            Assert.That(greatestDetour, Is.GreaterThan(1.8f),
                "The member must visibly detour around the blocker rather than crossing its footprint.");
            Object.Destroy(blocker);
        }

        [UnityTest]
        public IEnumerator BunchedFormationMembers_KeepMovingAtObstacleEdgeAndRegroup()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var formation = economy.DeployFriendlyForAutomation(FormationType.Spearmen,
                new Vector3(0f, 0f, 10f));
            yield return null;

            var edgeMember = formation.Members[0];
            var trailingMembers = new[] { formation.Members[1], formation.Members[2] };
            var blocker = CreateRouteBlocker("Bunched Member Route Blocker", new Vector3(-4.7f, 1f, 10f),
                new Vector3(1.5f, 2f, 4f));
            var blockerBounds = blocker.GetComponent<Collider>().bounds;
            yield return new WaitForSeconds(1f);

            Assert.That(NavMesh.SamplePosition(new Vector3(-5.55f, 0f, 10f), out var sampledEdgeStart,
                1f, NavMesh.AllAreas), Is.True);
            var edgeStart = new Vector3(sampledEdgeStart.position.x, edgeMember.WorldPosition.y,
                sampledEdgeStart.position.z);
            edgeMember.TeleportBy(edgeStart - edgeMember.WorldPosition);
            for (var index = 0; index < trailingMembers.Length; index++)
            {
                var offset = new Vector3(-0.65f, 0f, index == 0 ? -0.08f : 0.08f);
                Assert.That(NavMesh.SamplePosition(sampledEdgeStart.position + offset,
                    out var sampledTrailingStart, 0.2f, NavMesh.AllAreas), Is.True);
                var trailingStart = new Vector3(sampledTrailingStart.position.x,
                    trailingMembers[index].WorldPosition.y, sampledTrailingStart.position.z);
                trailingMembers[index].TeleportBy(trailingStart - trailingMembers[index].WorldPosition);
                Assert.That(Vector3.Distance(edgeMember.WorldPosition, trailingMembers[index].WorldPosition),
                    Is.LessThan(0.85f),
                    "The regression requires bunched members close enough to apply separation steering.");
            }

            var previousEdgePosition = edgeMember.WorldPosition;
            var forcedBunchDeadline = Time.realtimeSinceStartup + 0.75f;
            while (Time.realtimeSinceStartup < forcedBunchDeadline)
            {
                yield return null;
                AssertSweptSegmentOutsideBounds(previousEdgePosition, edgeMember.WorldPosition, blockerBounds);
                previousEdgePosition = edgeMember.WorldPosition;
                for (var index = 0; index < trailingMembers.Length; index++)
                {
                    var offset = new Vector3(-0.65f, 0f, index == 0 ? -0.08f : 0.08f);
                    var desiredTrailing = new Vector3(edgeMember.WorldPosition.x + offset.x, 0f,
                        edgeMember.WorldPosition.z + offset.z);
                    Assert.That(NavMesh.SamplePosition(desiredTrailing, out var sampledTrailing,
                        0.2f, NavMesh.AllAreas), Is.True);
                    var pinnedPosition = new Vector3(sampledTrailing.position.x,
                        trailingMembers[index].WorldPosition.y, sampledTrailing.position.z);
                    trailingMembers[index].TeleportBy(pinnedPosition - trailingMembers[index].WorldPosition);
                }
            }
            Assert.That(Vector3.Distance(edgeMember.WorldPosition, edgeStart), Is.GreaterThan(0.3f),
                "A sustained inward separation force must not suppress progress along the authoritative path.");

            var trackedMembers = new[] { edgeMember, trailingMembers[0], trailingMembers[1] };
            var starts = trackedMembers.Select(member => member.WorldPosition).ToArray();
            var previousPositions = trackedMembers.Select(member => member.WorldPosition).ToArray();
            var greatestProgress = new float[trackedMembers.Length];
            var deadline = Time.realtimeSinceStartup + 6f;
            while (trackedMembers.Any(member =>
                       Vector3.Distance(member.WorldPosition, member.AssignedSlotWorldPosition) > 0.45f) &&
                   Time.realtimeSinceStartup < deadline)
            {
                for (var index = 0; index < trackedMembers.Length; index++)
                {
                    var member = trackedMembers[index];
                    var position = member.WorldPosition;
                    var frameDisplacement = Vector3.Distance(position, previousPositions[index]);
                    var maximumFrameDisplacement = formation.MoveSpeed * 1.3f * Time.deltaTime + 0.025f;
                    Assert.That(frameDisplacement, Is.LessThanOrEqualTo(maximumFrameDisplacement),
                        $"A bunched member must remain speed bounded. " +
                        $"Moved={frameDisplacement:0.000}, maximum={maximumFrameDisplacement:0.000}.");
                    AssertSweptSegmentOutsideBounds(previousPositions[index], position, blockerBounds);
                    var groundedPosition = new Vector3(position.x, 0f, position.z);
                    Assert.That(NavMesh.SamplePosition(groundedPosition, out var walkable, 0.1f,
                        NavMesh.AllAreas), Is.True,
                        "Separation-steered members must remain on the NavMesh at an obstacle edge.");
                    Assert.That(Vector3.Distance(groundedPosition, walkable.position), Is.LessThan(0.1f));
                    greatestProgress[index] = Mathf.Max(greatestProgress[index],
                        Vector3.Distance(position, starts[index]));
                    previousPositions[index] = position;
                }
                yield return null;
            }

            Assert.That(greatestProgress[0], Is.GreaterThan(1f),
                "An obstacle-edge member must not let a zero-progress separation step suppress its path step.");
            Assert.That(greatestProgress[1], Is.GreaterThan(1f));
            Assert.That(greatestProgress[2], Is.GreaterThan(1f),
                "Nearby members must also make forward progress after the bunch releases.");
            Assert.That(trackedMembers.All(member =>
                    Vector3.Distance(member.WorldPosition, member.AssignedSlotWorldPosition) <= 0.45f), Is.True,
                $"All bunched members must eventually regroup. " +
                $"Edge={edgeMember.WorldPosition}/{edgeMember.AssignedSlotWorldPosition}, " +
                $"trailing-a={trailingMembers[0].WorldPosition}/{trailingMembers[0].AssignedSlotWorldPosition}, " +
                $"trailing-b={trailingMembers[1].WorldPosition}/{trailingMembers[1].AssignedSlotWorldPosition}.");
            Object.Destroy(blocker);
        }

        [UnityTest]
        public IEnumerator FormationMember_UsesReachableFallbackWhenItsSlotIsInsideStructureFootprint()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var formation = economy.DeployFriendlyForAutomation(FormationType.Spearmen,
                new Vector3(0f, 0f, 10f));
            yield return null;

            var member = formation.Members[0];
            var blockedSlot = member.AssignedSlotWorldPosition;
            var blocker = CreateRouteBlocker("Blocked Formation Slot",
                new Vector3(blockedSlot.x, 1f, blockedSlot.z), new Vector3(3f, 2f, 3f));
            var blockerBounds = blocker.GetComponent<Collider>().bounds;
            yield return new WaitForSeconds(1f);

            member.TeleportBy(Vector3.left * 5f);
            var displacedPosition = member.WorldPosition;
            var deadline = Time.realtimeSinceStartup + 5f;
            while ((Vector3.Distance(member.WorldPosition, member.NavigationDestination) > 0.45f ||
                    Vector3.Distance(member.WorldPosition, displacedPosition) < 2f) &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.That(Vector3.Distance(member.WorldPosition, displacedPosition), Is.GreaterThan(2f),
                "A member must not freeze when its exact slot is carved out of the NavMesh.");
            Assert.That(Vector3.Distance(member.NavigationDestination, blockedSlot), Is.LessThanOrEqualTo(3.05f),
                "The fallback destination must stay near the obstructed formation slot.");
            Assert.That(member.NavigationDestination.x, Is.LessThan(blockedSlot.x),
                "A displaced member should use a reachable fallback on its side of the blocked slot.");
            Assert.That(Vector3.Distance(member.WorldPosition, member.NavigationDestination),
                Is.LessThanOrEqualTo(0.45f),
                $"The member must consider the projected reachable destination arrived. " +
                $"Position={member.WorldPosition}, navigation={member.NavigationDestination}, slot={blockedSlot}.");
            Assert.That(blockerBounds.Contains(new Vector3(member.WorldPosition.x,
                blockerBounds.center.y, member.WorldPosition.z)), Is.False);
            Assert.That(NavMesh.SamplePosition(new Vector3(member.WorldPosition.x, 0f, member.WorldPosition.z),
                out var walkable, 0.1f, NavMesh.AllAreas), Is.True);
            Assert.That(Vector3.Distance(new Vector3(member.WorldPosition.x, 0f, member.WorldPosition.z),
                walkable.position), Is.LessThan(0.1f));

            Object.Destroy(blocker);
            yield return new WaitForSeconds(1f);
            deadline = Time.realtimeSinceStartup + 5f;
            while (Vector3.Distance(member.WorldPosition, member.AssignedSlotWorldPosition) > 0.45f &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.That(Vector3.Distance(member.WorldPosition, member.AssignedSlotWorldPosition),
                Is.LessThanOrEqualTo(0.45f),
                "The member must reclaim its exact slot after the structure obstruction clears.");
        }

        [UnityTest]
        public IEnumerator FormationMember_RefreshesFallbackAsBlockedSlotDriftsIncrementally()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var formation = economy.DeployFriendlyForAutomation(FormationType.Spearmen,
                new Vector3(0f, 0f, 10f));
            yield return null;

            var member = formation.Members[3];
            var initialSlot = member.AssignedSlotWorldPosition;
            var blocker = CreateRouteBlocker("Drifting Blocked Formation Slot",
                new Vector3(initialSlot.x, 1f, initialSlot.z), new Vector3(2f, 2f, 8f));
            var blockerBounds = new Bounds(blocker.transform.position, blocker.transform.localScale);
            yield return new WaitForSeconds(1f);

            member.TeleportBy(Vector3.left * 5f);
            var settleDeadline = Time.realtimeSinceStartup + 5f;
            while (Vector3.Distance(member.WorldPosition, member.NavigationDestination) > 0.45f &&
                   Time.realtimeSinceStartup < settleDeadline)
                yield return null;
            var initialFallback = member.NavigationDestination;

            for (var step = 0; step < 60; step++)
            {
                formation.transform.position += Vector3.forward * 0.02f;
                yield return null;
            }
            yield return new WaitForSeconds(0.5f);

            var driftingSlot = member.AssignedSlotWorldPosition;
            Assert.That(blockerBounds.Contains(new Vector3(driftingSlot.x,
                blockerBounds.center.y, driftingSlot.z)), Is.True,
                $"The regression must keep the requested slot carved while it moves incrementally. " +
                $"Slot={driftingSlot}, bounds={blockerBounds}.");
            Assert.That(Vector3.Distance(member.NavigationDestination, initialFallback), Is.GreaterThan(0.4f),
                $"A fallback must be resampled after cumulative slot drift instead of remaining stale. " +
                $"Initial={initialFallback}, current={member.NavigationDestination}, slot={driftingSlot}.");

            for (var step = 0; step < 180; step++)
            {
                formation.transform.position += Vector3.forward * 0.02f;
                yield return null;
            }
            var reformDeadline = Time.realtimeSinceStartup + 5f;
            while (Vector3.Distance(member.WorldPosition, member.AssignedSlotWorldPosition) > 0.45f &&
                   Time.realtimeSinceStartup < reformDeadline)
                yield return null;

            Assert.That(blockerBounds.Contains(new Vector3(member.AssignedSlotWorldPosition.x,
                blockerBounds.center.y, member.AssignedSlotWorldPosition.z)), Is.False,
                "The formation anchor must finish clear of the still-carved structure.");
            Assert.That(Vector3.Distance(member.WorldPosition, member.AssignedSlotWorldPosition),
                Is.LessThanOrEqualTo(0.45f),
                "The member must reform once incremental anchor movement carries its slot into open ground.");
            Object.Destroy(blocker);
        }

        [UnityTest]
        public IEnumerator MemberCasualty_DiesAtItsPositionAndSurvivorsCloseRanksSmoothly()
        {
            var tuning = ScriptableObject.CreateInstance<EconomyTuning>();
            var casualties = 0;
            var formation = CreateFormationForTest("Casualty Formation", FormationType.Archers, false, tuning,
                onCasualty: amount => casualties += amount);
            formation.transform.position = Vector3.zero;
            yield return null;
            var casualty = formation.Members[0];
            casualty.TeleportBy(Vector3.right * 3f);
            var survivor = formation.Members[1];
            var survivorPosition = survivor.WorldPosition;

            formation.ApplyDeterministicHit(casualty, FormationType.Cavalry,
                casualty.WorldPosition - Vector3.forward);
            Assert.That(formation.MemberCount, Is.EqualTo(7));
            Assert.That(casualty.IsAlive, Is.False);
            Assert.That(survivor.SlotIndex, Is.EqualTo(0));
            Assert.That(survivor.WorldPosition, Is.EqualTo(survivorPosition),
                "Closing ranks must not teleport a survivor into the casualty's slot.");
            var initialGap = Vector3.Distance(survivor.WorldPosition, survivor.AssignedSlotWorldPosition);
            yield return new WaitForSeconds(0.5f);
            Assert.That(Vector3.Distance(survivor.WorldPosition, survivor.AssignedSlotWorldPosition),
                Is.LessThan(initialGap));
            Assert.That(casualties, Is.EqualTo(1));

            Object.Destroy(formation.gameObject);
            Object.Destroy(tuning);
        }

        [UnityTest]
        public IEnumerator Cavalry_MovesFasterStopsAndWinsItsArcherCounterFight()
        {
            var tuning = ScriptableObject.CreateInstance<EconomyTuning>();
            var cavalry = CreateFormationForTest("Moving Cavalry", FormationType.Cavalry, true, tuning);
            var spearmen = CreateFormationForTest("Moving Spearmen", FormationType.Spearmen, true, tuning);
            var archers = CreateFormationForTest("Target Archers", FormationType.Archers, false, tuning);
            cavalry.transform.position = new Vector3(-2f, 0f, 0f);
            spearmen.transform.position = new Vector3(2f, 0f, 0f);
            archers.transform.position = new Vector3(-2f, 0f, 12f);
            cavalry.IssueMove(new Vector3(-2f, 0f, 20f));
            spearmen.IssueMove(new Vector3(2f, 0f, 20f));

            yield return new WaitForSeconds(0.5f);
            Assert.That(cavalry.transform.position.z, Is.GreaterThan(spearmen.transform.position.z + 0.7f));
            Assert.That(Vector3.Angle(cavalry.transform.forward, Vector3.forward), Is.LessThan(3f));
            cavalry.IssueStop();
            var stoppedPosition = cavalry.transform.position;
            yield return new WaitForSeconds(0.2f);
            Assert.That(cavalry.transform.position, Is.EqualTo(stoppedPosition));
            Assert.That(cavalry.CurrentOrder, Is.EqualTo(FormationOrder.Idle));

            Assert.That(cavalry.IssueFocus(archers), Is.True);
            yield return WaitUntil(() => archers.MemberCount == 0);
            Assert.That(cavalry.MemberCount, Is.GreaterThanOrEqualTo(4));

            Object.Destroy(cavalry.gameObject);
            Object.Destroy(spearmen.gameObject);
            Object.Destroy(archers.gameObject);
            Object.Destroy(tuning);
        }

        [UnityTest]
        public IEnumerator MoveOrder_OpposingFrontlineBlocksDirectPassageAndReleasesLaterally()
        {
            var tuning = ScriptableObject.CreateInstance<EconomyTuning>();
            FormationAgent defender = null;
            var attacker = CreateFormationForTest("Frontline mover", FormationType.Spearmen, true, tuning,
                () => new[] { defender });
            defender = CreateFormationForTest("Frontline blocker", FormationType.Spearmen, false, tuning);
            attacker.transform.position = Vector3.zero;
            defender.transform.position = new Vector3(0f, 0f, 4f);

            attacker.IssueMove(new Vector3(0f, 0f, 8f));
            yield return WaitUntil(() => attacker.IsFrontlineBlocked);
            var blockedPosition = attacker.transform.position;
            yield return new WaitForSeconds(0.25f);

            Assert.That(attacker.transform.position.z, Is.LessThan(defender.transform.position.z));
            Assert.That(Vector3.Distance(attacker.transform.position, blockedPosition), Is.LessThan(0.05f));
            Assert.That(attacker.CurrentOrder, Is.EqualTo(FormationOrder.Move));

            attacker.IssueMove(attacker.transform.position + Vector3.right * 4f);
            yield return new WaitForSeconds(0.35f);

            Assert.That(attacker.IsFrontlineBlocked, Is.False);
            Assert.That(attacker.transform.position.x, Is.GreaterThan(blockedPosition.x + 0.5f));
            Object.Destroy(attacker.gameObject);
            Object.Destroy(defender.gameObject);
            Object.Destroy(tuning);
        }

        [UnityTest]
        public IEnumerator MoveOrder_AlliedFormationDoesNotCreateRigidFrontline()
        {
            var tuning = ScriptableObject.CreateInstance<EconomyTuning>();
            FormationAgent ally = null;
            var mover = CreateFormationForTest("Allied mover", FormationType.Spearmen, true, tuning,
                () => new[] { ally });
            ally = CreateFormationForTest("Allied soft obstacle", FormationType.Archers, true, tuning);
            mover.transform.position = Vector3.zero;
            ally.transform.position = new Vector3(0f, 0f, 2f);

            mover.IssueMove(new Vector3(0f, 0f, 5f));
            yield return new WaitForSeconds(0.8f);

            Assert.That(mover.IsFrontlineBlocked, Is.False);
            Assert.That(mover.transform.position.z, Is.GreaterThan(ally.transform.position.z));
            Object.Destroy(mover.gameObject);
            Object.Destroy(ally.gameObject);
            Object.Destroy(tuning);
        }

    }
}
