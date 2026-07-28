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
        private void CompleteProduction(ProductionItem item)
        {
            if (item == ProductionItem.Worker)
            {
                var slot = workers.Count == 0 ? 0 : workers.Count;
                var position = new Vector3(-2.2f + slot % WorkerCount * 1.45f, 0f,
                    -4f + slot / WorkerCount * 1.3f);
                var worker = CreateWorker(true, slot, position, wallet, hisar, allCaches);
                workers.Add(worker);
                telemetry.RecordEntityProduced(true, ProductionItem.Worker.ToString(), MatchElapsedSeconds);
                SetOrderFeedback("Worker ready");
                PlayCue(GameplayCue.Production);
                ApplyHisarRally(worker);
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
            PlayCue(GameplayCue.Production);
            ApplyHisarRally(friendly);
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
                IsCurrentlyVisibleToHostileSide,
                friendly ? HandleFriendlyUnderAttack : position => PlayWorldCue(GameplayCue.Hit, position, false),
                position => PlayWorldCue(GameplayCue.Attack, position, friendly));
            AttachHealthBar(root, () => formation.TotalMemberHealth, () => formation.MaximumMemberHealth,
                3.25f, friendly);
            if (friendly) fogOfWar?.RegisterFriendly(root.transform);
            else fogOfWar?.RegisterHostileMobile(root);
            return formation;
        }

        private void AttachHealthBar(GameObject owner, System.Func<int> health, System.Func<int> maxHealth,
            float height, bool friendly)
        {
            var color = friendly ? new Color(0.12f, 0.58f, 1f) : new Color(0.95f, 0.22f, 0.08f);
            owner.AddComponent<WorldHealthBar>().Initialize(health, maxHealth, height, color,
                friendly ? null : () => owner != null &&
                    owner.GetComponent<FogVisibilityTarget>()?.State == FogState.Visible);
        }

        private void UpdateHealthBarHover()
        {
            WorldHealthBar next = null;
            var mouse = Mouse.current;
            if (mouse != null && worldCamera != null)
            {
                var position = mouse.position.ReadValue();
                if (!IsPointerOverHud(position) &&
                    Physics.Raycast(worldCamera.ScreenPointToRay(position), out var hit, 200f))
                    next = hit.collider.GetComponentInParent<WorldHealthBar>();
            }
            if (ReferenceEquals(next, hoveredHealthBar)) return;
            hoveredHealthBar?.SetHovered(false);
            hoveredHealthBar = next;
            hoveredHealthBar?.SetHovered(true);
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

        private bool TrySetHisarRally(Vector3 position, ResourceCache cache)
        {
            if (cache != null && (cache.Remaining <= 0 || !IsCurrentlyVisible(cache.transform.position))) return false;
            hisarRallyCache = cache;
            hisarRallyPoint = new Vector3(position.x, 0f, position.z);
            if (hisarRallyMarker != null) Destroy(hisarRallyMarker);
            hisarRallyMarker = CreatePrimitive(PrimitiveType.Cylinder, "Hisar Rally Point", null,
                hisarRallyPoint.Value + Vector3.up * 0.06f, new Vector3(1.25f, 0.025f, 1.25f),
                new Color(1f, 0.78f, 0.16f));
            Destroy(hisarRallyMarker.GetComponent<Collider>());
            SetOrderFeedback(cache != null ? $"Rally set - gather {cache.name}" : "Rally set - terrain");
            PlayCue(GameplayCue.Order);
            UpdateHud();
            return true;
        }

        private void ApplyHisarRally(WorkerAgent worker)
        {
            if (worker == null || !hisarRallyPoint.HasValue) return;
            if (hisarRallyCache != null && hisarRallyCache.Remaining > 0)
                worker.IssueGather(hisarRallyCache);
            else
                worker.IssueMove(hisarRallyPoint.Value);
        }

        private void ApplyHisarRally(FormationAgent formation)
        {
            if (formation != null && hisarRallyPoint.HasValue) formation.IssueMove(hisarRallyPoint.Value);
        }

        private void HandleFriendlyUnderAttack(Vector3 position)
        {
            PlayWorldCue(GameplayCue.Hit, position, true);
            if (Time.unscaledTime < nextUnderAttackCueAt) return;
            nextUnderAttackCueAt = Time.unscaledTime + UnderAttackCooldownSeconds;
            fogOfWar?.RefreshNow();
            if (fogOfWar == null || !fogOfWar.ShowAttackPing(position)) return;
            UnderAttackWarningCount++;
            PlayCue(GameplayCue.Warning);
            SetOrderFeedback("Under attack - check minimap ping");
        }

        private void PlayWorldCue(GameplayCue cue, Vector3 position, bool friendlySource)
        {
            if (!friendlySource && !IsCurrentlyVisible(position)) return;
            if (cue == GameplayCue.Hit)
            {
                if (Time.unscaledTime < nextHitCueAt) return;
                nextHitCueAt = Time.unscaledTime + HitCueCooldownSeconds;
            }
            PlayCue(cue);
        }

        private void PlayCue(GameplayCue cue) => gameplayAudio?.Play(cue);
    }
}
