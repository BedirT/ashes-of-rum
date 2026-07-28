using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace AshesOfRum
{
    public static class FormationFrontlineRules
    {
        private const float MinimumApproachDot = 0.25f;

        public static bool Blocks(Vector3 moverPosition, Vector3 moveDirection, Vector3 opponentPosition,
            float radius)
        {
            moveDirection.y = 0f;
            var offset = opponentPosition - moverPosition;
            offset.y = 0f;
            if (moveDirection.sqrMagnitude <= 0.01f || offset.sqrMagnitude <= 0.01f ||
                offset.sqrMagnitude > radius * radius) return false;
            return Vector3.Dot(moveDirection.normalized, offset.normalized) >= MinimumApproachDot;
        }
    }

    public static class FormationMemberRules
    {
        public static Vector3 Slot(int index) =>
            new((index % 4 - 1.5f) * 1.15f, 0.85f, -(index / 4) * 1.35f);
    }

    public sealed class FormationAgent : MonoBehaviour
    {
        private readonly List<FormationMemberAgent> members = new();
        private EconomyTuning tuning;
        private FormationAgent target;
        private WorkerAgent workerTarget;
        private MonoBehaviour structureTargetComponent;
        private System.Func<IEnumerable<FormationAgent>> hostileProvider;
        private System.Func<IEnumerable<WorkerAgent>> hostileWorkerProvider;
        private System.Func<IEnumerable<ICombatStructure>> hostileStructureProvider;
        private System.Func<FormationAgent, bool> visibilityPredicate;
        private System.Func<WorkerAgent, bool> workerVisibilityPredicate;
        private System.Func<ICombatStructure, bool> structureVisibilityPredicate;
        private Action<int> casualtyCallback;
        private Action<FormationAgent> destroyedCallback;
        private Action<Vector3> damagedCallback;
        private Action<Vector3> attackCallback;
        private NavMeshAgent navAgent;
        private int nextHitMemberIndex;
        private float attackRemaining;
        private Vector3 destination;
        private bool hasDestination;
        private Quaternion turnStartRotation;
        private Quaternion turnTargetRotation;
        private float turnElapsed;
        private Vector3 lastAnchorPosition;

        private static readonly Color FriendlyColor = new(0.1f, 0.38f, 0.9f);
        private static readonly Color HostileColor = new(0.8f, 0.16f, 0.08f);
        private static readonly Color SelectionColor = new(0.2f, 0.85f, 1f);
        private static readonly Color ArrowColor = new(0.95f, 0.75f, 0.25f);
        private const string SupportedShaderName = "Universal Render Pipeline/Lit";

        public FormationType Type { get; private set; }
        public bool IsFriendly { get; private set; }
        public int MemberCount => members.Count;
        public IReadOnlyList<FormationMemberAgent> Members => members;
        public bool IsSelected { get; private set; }
        public FormationAgent Target => target;
        public WorkerAgent WorkerTarget => workerTarget;
        public ICombatStructure StructureTarget => structureTargetComponent as ICombatStructure;
        public FormationOrder CurrentOrder { get; private set; }
        public Vector3 Destination => destination;
        public bool HasDestination => hasDestination;
        public float MoveSpeed => Type == FormationType.Cavalry ? tuning.cavalrySpeed : tuning.footSpeed;
        public int TotalMemberHealth
        {
            get
            {
                var total = 0;
                foreach (var member in members) total += member.Health;
                return total;
            }
        }
        public int MaximumMemberHealth => tuning == null ? 0 : tuning.memberHealth * 8;
        public int LastAttackMemberCount { get; private set; }
        public bool IsTurning { get; private set; }
        public bool IsFrontlineBlocked { get; private set; }
        public float TurnProgress => IsTurning ? Mathf.Clamp01(turnElapsed / tuning.reorientationSeconds) : 1f;
        public float FacingDegrees => Mathf.Repeat(transform.eulerAngles.y, 360f);
        public string FacingLabel => $"{FacingCardinal(FacingDegrees)} {FacingDegrees:0} deg";
        public FlankDirection LastReceivedFlank { get; private set; }
        public int ProjectileHitsReceived { get; private set; }
        public int ProjectileHitsLanded { get; private set; }

        public void Initialize(FormationType type, bool friendly, EconomyTuning combatTuning,
            Action<int> onCasualty = null, Action<FormationAgent> onDestroyed = null,
            System.Func<IEnumerable<FormationAgent>> availableHostiles = null,
            System.Func<FormationAgent, bool> isTargetVisible = null,
            System.Func<IEnumerable<WorkerAgent>> availableHostileWorkers = null,
            System.Func<WorkerAgent, bool> isWorkerVisible = null,
            System.Func<IEnumerable<ICombatStructure>> availableHostileStructures = null,
            System.Func<ICombatStructure, bool> isStructureVisible = null,
            Action<Vector3> onDamaged = null, Action<Vector3> onAttack = null)
        {
            Type = type;
            IsFriendly = friendly;
            tuning = combatTuning;
            casualtyCallback = onCasualty;
            destroyedCallback = onDestroyed;
            hostileProvider = availableHostiles;
            visibilityPredicate = isTargetVisible;
            hostileWorkerProvider = availableHostileWorkers;
            workerVisibilityPredicate = isWorkerVisible;
            hostileStructureProvider = availableHostileStructures;
            structureVisibilityPredicate = isStructureVisible;
            damagedCallback = onDamaged;
            attackCallback = onAttack;
            lastAnchorPosition = transform.position;
            navAgent = GetComponent<NavMeshAgent>();
            if (navAgent != null)
            {
                navAgent.speed = MoveSpeed;
                navAgent.angularSpeed = 360f;
                navAgent.acceleration = 20f;
                navAgent.stoppingDistance = 0.15f;
                navAgent.updateRotation = false;
            }
            for (var i = 0; i < 8; i++)
            {
                members.Add(CreateMember(i));
            }
            CreateFrontIndicator(transform);
        }

        public void SetSelected(bool selected)
        {
            IsSelected = selected;
            foreach (var ring in GetComponentsInChildren<FormationSelectionRing>(true))
                ring.gameObject.SetActive(selected);
            GetComponent<WorldHealthBar>()?.SetSelected(selected);
        }

        public bool IssueFocus(FormationAgent hostile)
        {
            if (!IsValidTarget(hostile)) return false;
            target = hostile;
            workerTarget = null;
            structureTargetComponent = null;
            hasDestination = false;
            CurrentOrder = FormationOrder.Focus;
            StartNavigation(hostile.transform.position);
            hostile.TryRetaliate(this);
            return true;
        }

        public bool IssueFocus(WorkerAgent hostile)
        {
            if (!IsValidWorkerTarget(hostile)) return false;
            target = null;
            workerTarget = hostile;
            structureTargetComponent = null;
            hasDestination = false;
            CurrentOrder = FormationOrder.Focus;
            StartNavigation(hostile.transform.position);
            return true;
        }

        public bool IssueFocus(ICombatStructure hostile)
        {
            if (!IsValidStructureTarget(hostile)) return false;
            target = null;
            workerTarget = null;
            structureTargetComponent = hostile.TargetComponent as MonoBehaviour;
            hasDestination = false;
            CurrentOrder = FormationOrder.Focus;
            StartNavigation(hostile.TargetComponent.transform.position);
            return true;
        }

        public void IssueMove(Vector3 position)
        {
            target = null;
            workerTarget = null;
            structureTargetComponent = null;
            destination = Grounded(position);
            hasDestination = true;
            IsTurning = false;
            CurrentOrder = FormationOrder.Move;
            StartNavigation(destination);
        }

        public void IssueAttackMove(Vector3 position)
        {
            target = null;
            workerTarget = null;
            structureTargetComponent = null;
            destination = Grounded(position);
            hasDestination = true;
            IsTurning = false;
            CurrentOrder = FormationOrder.AttackMove;
            StartNavigation(destination);
        }

        public void IssueStop()
        {
            target = null;
            workerTarget = null;
            structureTargetComponent = null;
            hasDestination = false;
            IsTurning = false;
            IsFrontlineBlocked = false;
            CurrentOrder = FormationOrder.Idle;
            StopNavigation();
        }

        public void ApplyDeterministicHit(FormationType attackerType)
        {
            ApplyDeterministicHit(attackerType, transform.position + transform.forward);
        }

        public void ApplyDeterministicHit(FormationType attackerType, Vector3 attackerPosition)
        {
            if (members.Count == 0) return;
            ApplyDeterministicHit(members[nextHitMemberIndex % members.Count], attackerType, attackerPosition);
        }

        public void ApplyDeterministicHit(FormationMemberAgent member, FormationType attackerType,
            Vector3 attackerPosition)
        {
            if (member == null || !members.Contains(member)) return;
            var incoming = attackerPosition - member.WorldPosition;
            var flank = CombatRules.ClassifyFlank(transform.forward, incoming);
            ApplyDamageToMember(member, CombatRules.Damage(attackerType, Type, tuning.baseDamage,
                tuning.counterMultiplier, flank, tuning.sideDamageMultiplier, tuning.rearDamageMultiplier), flank);
        }

        public void ApplyFixedDamage(int damage) => ApplyFixedDamage(damage, FlankDirection.Front);

        private void ApplyFixedDamage(int damage, FlankDirection flank)
        {
            if (members.Count == 0 || damage <= 0) return;
            ApplyDamageToMember(members[nextHitMemberIndex % members.Count], damage, flank);
        }

        private void ApplyDamageToMember(FormationMemberAgent member, int damage, FlankDirection flank)
        {
            if (member == null || !members.Contains(member) || damage <= 0) return;
            LastReceivedFlank = flank;
            damagedCallback?.Invoke(transform.position);
            GetComponent<WorldHealthBar>()?.RecordDamage();
            var hitIndex = members.IndexOf(member);
            member.ApplyDamage(damage, flank);
            if (member.Health > 0)
            {
                nextHitMemberIndex = (hitIndex + 1) % members.Count;
                return;
            }

            members.RemoveAt(hitIndex);
            member.DetachForDeath();
            Destroy(member.gameObject);
            casualtyCallback?.Invoke(1);
            nextHitMemberIndex = members.Count == 0 ? 0 : hitIndex % members.Count;
            ReForm();
            if (members.Count != 0) return;
            target = null;
            workerTarget = null;
            structureTargetComponent = null;
            StopNavigation();
            destroyedCallback?.Invoke(this);
            Destroy(gameObject, 0.25f);
        }

        private void Update()
        {
            SynchronizeMembersForAnchorTeleport();
            IsFrontlineBlocked = false;
            if (!IsValidTarget(target))
                target = null;
            if (!IsValidWorkerTarget(workerTarget)) workerTarget = null;
            if (!IsValidStructureTarget(StructureTarget)) structureTargetComponent = null;
            UpdateMemberMovement();
            if (CurrentOrder == FormationOrder.Focus && !HasCombatTarget)
            {
                IsTurning = false;
                CurrentOrder = FormationOrder.Idle;
                StopNavigation();
            }
            if (CurrentOrder == FormationOrder.AttackMove) AcquireAttackMoveTarget();
            if (HasCombatTarget)
            {
                if (target != null) target.TryRetaliate(this);
                if (target != null) MoveOrAttackTarget();
                else if (workerTarget != null) MoveOrAttackWorker();
                else MoveOrAttackStructure();
                return;
            }
            if (!hasDestination) return;

            var delta = destination - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude <= 0.09f)
            {
                hasDestination = false;
                CurrentOrder = FormationOrder.Idle;
                StopNavigation();
                return;
            }
            MoveAndFace(delta);
        }

        private bool HasCombatTarget => target != null || workerTarget != null || StructureTarget != null;

        private void AcquireAttackMoveTarget()
        {
            if (target == null)
            {
                var formation = FindNearestVisibleHostile();
                if (formation != null)
                {
                    target = formation;
                    workerTarget = null;
                    structureTargetComponent = null;
                    target.TryRetaliate(this);
                    return;
                }
            }
            if (target != null || workerTarget != null) return;
            workerTarget = FindNearestVisibleWorker();
            if (workerTarget != null)
            {
                structureTargetComponent = null;
                return;
            }
            if (StructureTarget == null)
            {
                var structure = FindNearestVisibleStructure();
                structureTargetComponent = structure?.TargetComponent as MonoBehaviour;
            }
        }

        private void MoveOrAttackTarget()
        {
            var delta = target.transform.position - transform.position;
            delta.y = 0f;
            var range = AttackRange;
            var memberCanAttack = Type != FormationType.Archers && HasMemberInAttackRange(target);
            if (delta.sqrMagnitude > range * range && !memberCanAttack)
            {
                MoveAndFace(delta);
                return;
            }

            if (delta.sqrMagnitude <= range * range) StopNavigation();
            else MoveAndFace(delta);
            if (!FaceCombatTarget(delta)) return;
            if (Type != FormationType.Archers)
            {
                ExecuteAttackVolley(target);
                return;
            }
            attackRemaining -= Time.deltaTime;
            if (attackRemaining > 0f) return;
            attackRemaining = tuning.attackSeconds;
            ExecuteAttackVolley(target);
        }

        private bool HasMemberInAttackRange(FormationAgent intendedTarget)
        {
            var rangeSquared = AttackRange * AttackRange;
            foreach (var member in members)
            {
                var nearest = intendedTarget.SelectMemberTarget(member.WorldPosition, 0);
                if (nearest != null && (nearest.WorldPosition - member.WorldPosition).sqrMagnitude <= rangeSquared)
                    return true;
            }
            return false;
        }

        private void MoveOrAttackWorker()
        {
            var delta = workerTarget.transform.position - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude > AttackRange * AttackRange)
            {
                MoveAndFace(delta);
                return;
            }
            StopNavigation();
            if (!FaceCombatTarget(delta)) return;
            attackRemaining -= Time.deltaTime;
            if (attackRemaining > 0f) return;
            attackRemaining = tuning.attackSeconds;
            ExecuteAttackVolley(workerTarget);
        }

        private void MoveOrAttackStructure()
        {
            var structure = StructureTarget;
            if (structure == null) return;
            var delta = structure.TargetComponent.transform.position - transform.position;
            delta.y = 0f;
            var range = AttackRange + structure.CombatRadius;
            if (delta.sqrMagnitude > range * range)
            {
                MoveAndFace(delta);
                return;
            }
            StopNavigation();
            if (!FaceCombatTarget(delta)) return;
            attackRemaining -= Time.deltaTime;
            if (attackRemaining > 0f) return;
            attackRemaining = tuning.attackSeconds;
            ExecuteStructuralVolley(structure);
        }

        private float AttackRange => Type == FormationType.Archers ? 7f : 2.2f;

        private bool FaceCombatTarget(Vector3 delta)
        {
            if (delta.sqrMagnitude <= 0.01f)
            {
                IsTurning = false;
                return true;
            }
            var desired = Quaternion.LookRotation(delta.normalized);
            if (!IsTurning && Quaternion.Angle(transform.rotation, desired) <= 2f)
            {
                transform.rotation = desired;
                return true;
            }
            if (!IsTurning || Quaternion.Angle(turnTargetRotation, desired) > 5f)
            {
                turnStartRotation = transform.rotation;
                turnTargetRotation = desired;
                turnElapsed = 0f;
                IsTurning = true;
            }
            turnElapsed += Time.deltaTime;
            var progress = Mathf.Clamp01(turnElapsed / tuning.reorientationSeconds);
            transform.rotation = Quaternion.Slerp(turnStartRotation, turnTargetRotation, progress);
            if (progress < 1f) return false;
            transform.rotation = turnTargetRotation;
            IsTurning = false;
            return true;
        }

        private void MoveAndFace(Vector3 delta)
        {
            IsTurning = false;
            var direction = delta.normalized;
            if (IsBlockedByVisibleFrontline(direction))
            {
                IsFrontlineBlocked = true;
                StopNavigation();
                return;
            }
            if (CanUseNavigation())
            {
                navAgent.speed = MoveSpeed;
                navAgent.isStopped = false;
                navAgent.SetDestination(transform.position + delta);
                var facing = navAgent.desiredVelocity.sqrMagnitude > 0.01f
                    ? navAgent.desiredVelocity.normalized
                    : direction;
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(facing), 360f * Time.deltaTime);
                return;
            }
            var distance = Mathf.Min(delta.magnitude, MoveSpeed * Time.deltaTime);
            transform.position += direction * distance;
            transform.rotation = Quaternion.RotateTowards(transform.rotation,
                Quaternion.LookRotation(direction), 360f * Time.deltaTime);
        }

        private bool IsBlockedByVisibleFrontline(Vector3 direction)
        {
            if (hostileProvider == null) return false;
            foreach (var hostile in hostileProvider())
            {
                if (!IsValidTarget(hostile)) continue;
                if (hostile == target) continue;
                if (FormationFrontlineRules.Blocks(transform.position, direction, hostile.transform.position,
                        tuning.frontlineBlockRadius)) return true;
            }
            return false;
        }

        private void StartNavigation(Vector3 targetPosition)
        {
            if (!CanUseNavigation()) return;
            navAgent.speed = MoveSpeed;
            navAgent.isStopped = false;
            navAgent.SetDestination(Grounded(targetPosition));
        }

        private void StopNavigation()
        {
            if (!CanUseNavigation()) return;
            navAgent.isStopped = true;
            navAgent.ResetPath();
            navAgent.velocity = Vector3.zero;
            navAgent.nextPosition = transform.position;
        }

        private bool CanUseNavigation() => navAgent != null && navAgent.enabled && navAgent.isOnNavMesh;

        private FormationAgent FindNearestVisibleHostile()
        {
            if (hostileProvider == null) return null;
            FormationAgent nearest = null;
            var nearestDistance = tuning.sightRadius * tuning.sightRadius;
            foreach (var hostile in hostileProvider())
            {
                if (!IsValidTarget(hostile)) continue;
                var distance = (hostile.transform.position - transform.position).sqrMagnitude;
                if (distance > nearestDistance) continue;
                nearest = hostile;
                nearestDistance = distance;
            }
            return nearest;
        }

        private WorkerAgent FindNearestVisibleWorker()
        {
            if (hostileWorkerProvider == null) return null;
            WorkerAgent nearest = null;
            var nearestDistance = tuning.sightRadius * tuning.sightRadius;
            foreach (var hostile in hostileWorkerProvider())
            {
                if (!IsValidWorkerTarget(hostile)) continue;
                var distance = (hostile.transform.position - transform.position).sqrMagnitude;
                if (distance > nearestDistance) continue;
                nearest = hostile;
                nearestDistance = distance;
            }
            return nearest;
        }

        private ICombatStructure FindNearestVisibleStructure()
        {
            if (hostileStructureProvider == null) return null;
            ICombatStructure nearest = null;
            var nearestDistance = tuning.sightRadius * tuning.sightRadius;
            foreach (var hostile in hostileStructureProvider())
            {
                if (!IsValidStructureTarget(hostile)) continue;
                var distance = (hostile.TargetComponent.transform.position - transform.position).sqrMagnitude;
                if (distance > nearestDistance) continue;
                nearest = hostile;
                nearestDistance = distance;
            }
            return nearest;
        }

        private bool IsValidTarget(FormationAgent candidate) => candidate != null &&
            candidate.IsFriendly != IsFriendly && candidate.MemberCount > 0 &&
            (visibilityPredicate == null || visibilityPredicate(candidate));

        private bool IsValidWorkerTarget(WorkerAgent candidate) => candidate != null && candidate.IsAlive &&
            candidate.IsFriendly != IsFriendly &&
            (workerVisibilityPredicate == null || workerVisibilityPredicate(candidate));

        private bool IsValidStructureTarget(ICombatStructure candidate) => candidate != null &&
            candidate.TargetComponent != null && candidate.IsAttackable && candidate.IsFriendly != IsFriendly &&
            (structureVisibilityPredicate == null || structureVisibilityPredicate(candidate));

        private void TryRetaliate(FormationAgent attacker)
        {
            var hasExplicitFocus = CurrentOrder == FormationOrder.Focus && HasCombatTarget;
            if (target != null || hasExplicitFocus || !IsValidTarget(attacker)) return;
            var resumeAttackMove = CurrentOrder == FormationOrder.AttackMove && hasDestination;
            target = attacker;
            workerTarget = null;
            structureTargetComponent = null;
            if (resumeAttackMove)
            {
                StartNavigation(attacker.transform.position);
                return;
            }
            hasDestination = false;
            CurrentOrder = FormationOrder.Focus;
            StartNavigation(attacker.transform.position);
        }

        private static Vector3 Grounded(Vector3 position) => new(position.x, 0f, position.z);

        public bool ExecuteAttackVolley(FormationAgent intendedTarget)
        {
            if (intendedTarget == null || intendedTarget.IsFriendly == IsFriendly ||
                intendedTarget.MemberCount == 0 || members.Count == 0) return false;

            SynchronizeMembersForAnchorTeleport();
            intendedTarget.SynchronizeMembersForAnchorTeleport();
            LastAttackMemberCount = members.Count;
            var attackers = members.ToArray();
            var attackCount = 0;
            for (var i = 0; i < attackers.Length; i++)
            {
                var attacker = attackers[i];
                var intendedMember = Type == FormationType.Archers
                    ? attacker.AttackTarget
                    : intendedTarget.SelectMemberTarget(attacker.WorldPosition, 0);
                if (Type == FormationType.Archers &&
                    (intendedMember == null || !intendedTarget.members.Contains(intendedMember)))
                {
                    intendedMember = intendedTarget.SelectVolleyMember(attacker.Identity);
                    attacker.AssignAttackTarget(intendedMember);
                }
                if (intendedMember == null) continue;
                if (Type == FormationType.Archers)
                {
                    StartCoroutine(FireArrow(attacker, intendedTarget, intendedMember));
                    attackCount++;
                }
                else if (attacker.CanAttack &&
                         (intendedMember.WorldPosition - attacker.WorldPosition).sqrMagnitude <=
                         AttackRange * AttackRange)
                {
                    intendedTarget.ApplyDeterministicHit(intendedMember, Type, attacker.WorldPosition);
                    attacker.BeginAttackCooldown(tuning.attackSeconds);
                    attackCount++;
                }
            }
            LastAttackMemberCount = attackCount;
            if (attackCount > 0) attackCallback?.Invoke(transform.position);
            return true;
        }

        public bool ExecuteAttackVolley(WorkerAgent intendedTarget)
        {
            if (!IsValidWorkerTarget(intendedTarget) || members.Count == 0) return false;
            LastAttackMemberCount = members.Count;
            attackCallback?.Invoke(transform.position);
            var attackers = members.ToArray();
            foreach (var attacker in attackers)
            {
                if (Type == FormationType.Archers)
                    StartCoroutine(FireArrow(attacker.WorldPosition, intendedTarget));
                else
                    intendedTarget.ApplyFixedDamage(tuning.baseDamage);
            }
            return true;
        }

        public bool ExecuteStructuralVolley(ICombatStructure intendedTarget)
        {
            if (!IsValidStructureTarget(intendedTarget) || members.Count == 0) return false;
            LastAttackMemberCount = members.Count;
            attackCallback?.Invoke(transform.position);
            var attackers = members.ToArray();
            foreach (var attacker in attackers)
            {
                if (Type == FormationType.Archers)
                    StartCoroutine(FireArrow(attacker.WorldPosition, intendedTarget));
                else
                    intendedTarget.ApplyStructuralDamage(tuning.structuralDamage);
            }
            return true;
        }

        public bool HasSupportedVisualMaterials()
        {
            foreach (var itemRenderer in GetComponentsInChildren<Renderer>(true))
            {
                if (!UsesSupportedMaterial(itemRenderer)) return false;
                var expected = itemRenderer.GetComponent<FormationMemberVisual>() != null
                    ? (IsFriendly ? FriendlyColor : HostileColor)
                    : itemRenderer.GetComponent<FormationSelectionRing>() != null ? SelectionColor : Color.white;
                if (!Approximately(itemRenderer.sharedMaterial.color, expected)) return false;
            }
            return true;
        }

        public static bool UsesSupportedMaterial(Renderer itemRenderer) =>
            itemRenderer != null && itemRenderer.sharedMaterial != null &&
            itemRenderer.sharedMaterial.shader != null &&
            itemRenderer.sharedMaterial.shader.name == SupportedShaderName;

        private IEnumerator FireArrow(FormationMemberAgent attacker, FormationAgent intendedTarget,
            FormationMemberAgent intendedMember)
        {
            var arrow = CreateArrow();
            var attackerPosition = attacker == null ? transform.position : attacker.WorldPosition;
            var start = attackerPosition + Vector3.up * 1.4f;
            var targetPosition = intendedMember == null ? intendedTarget.transform.position : intendedMember.WorldPosition;
            var elapsed = 0f;
            while (elapsed < tuning.projectileSeconds && intendedTarget != null)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / tuning.projectileSeconds);
                if (intendedMember != null && intendedMember.IsAlive) targetPosition = intendedMember.WorldPosition;
                var end = targetPosition + Vector3.up;
                var nextPosition = Vector3.Lerp(start, end, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 1.5f);
                FaceProjectile(arrow.transform, nextPosition);
                arrow.transform.position = nextPosition;
                yield return null;
            }
            if (intendedTarget != null && intendedMember != null && intendedMember.IsAlive)
            {
                intendedMember.RecordProjectileImpact(targetPosition);
                ProjectileHitsLanded++;
                intendedTarget.ProjectileHitsReceived++;
                intendedTarget.ApplyDeterministicHit(intendedMember, Type, attackerPosition);
            }
            Destroy(arrow);
        }

        private IEnumerator FireArrow(Vector3 memberPosition, WorkerAgent intendedTarget)
        {
            var arrow = CreateArrow();
            var start = memberPosition + Vector3.up * 1.4f;
            var elapsed = 0f;
            while (elapsed < tuning.projectileSeconds && intendedTarget != null && intendedTarget.IsAlive)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / tuning.projectileSeconds);
                var end = intendedTarget.transform.position + Vector3.up;
                arrow.transform.position = Vector3.Lerp(start, end, t) +
                                           Vector3.up * (Mathf.Sin(t * Mathf.PI) * 1.5f);
                yield return null;
            }
            if (intendedTarget != null && intendedTarget.IsAlive) intendedTarget.ApplyFixedDamage(tuning.baseDamage);
            Destroy(arrow);
        }

        private IEnumerator FireArrow(Vector3 memberPosition, ICombatStructure intendedTarget)
        {
            var arrow = CreateArrow();
            var start = memberPosition + Vector3.up * 1.4f;
            var elapsed = 0f;
            while (elapsed < tuning.projectileSeconds && intendedTarget != null &&
                   intendedTarget.TargetComponent != null && intendedTarget.IsAttackable)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / tuning.projectileSeconds);
                arrow.transform.position = Vector3.Lerp(start, intendedTarget.AimPoint, t) +
                                           Vector3.up * (Mathf.Sin(t * Mathf.PI) * 1.5f);
                yield return null;
            }
            if (intendedTarget != null && intendedTarget.TargetComponent != null && intendedTarget.IsAttackable)
                intendedTarget.ApplyStructuralDamage(tuning.structuralDamage);
            Destroy(arrow);
        }

        private static GameObject CreateArrow()
        {
            var arrow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            arrow.name = "Arrow";
            arrow.transform.localScale = new Vector3(0.06f, 0.35f, 0.06f);
            Destroy(arrow.GetComponent<Collider>());
            AssignSupportedMaterial(arrow.GetComponent<Renderer>(), ArrowColor);
            return arrow;
        }

        private static void FaceProjectile(Transform projectile, Vector3 nextPosition)
        {
            var direction = nextPosition - projectile.position;
            if (direction.sqrMagnitude > 0.0001f)
                projectile.rotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);
        }

        private FormationMemberAgent CreateMember(int index)
        {
            var member = GameObject.CreatePrimitive(Type == FormationType.Cavalry ? PrimitiveType.Cube : PrimitiveType.Capsule);
            member.name = $"{Type} Member {index + 1}";
            member.transform.SetParent(transform, false);
            member.transform.localPosition = FormationMemberRules.Slot(index);
            member.transform.localScale = Type == FormationType.Cavalry
                ? new Vector3(0.8f, 1.1f, 1.25f)
                : new Vector3(0.7f, 0.85f, 0.7f);
            var memberRenderer = member.GetComponent<Renderer>();
            AssignSupportedMaterial(memberRenderer, IsFriendly ? FriendlyColor : HostileColor);
            member.AddComponent<FormationMemberVisual>().Initialize(memberRenderer);
            var memberAgent = member.AddComponent<FormationMemberAgent>();
            memberAgent.Initialize(this, index, tuning.memberHealth);

            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = IsFriendly ? "Black Falcon Diamond" : "Living Flame Square";
            marker.transform.SetParent(member.transform, false);
            marker.transform.localPosition = new Vector3(0f, 1.25f, 0f);
            marker.transform.localScale = Vector3.one * 0.35f;
            if (IsFriendly) marker.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
            AssignSupportedMaterial(marker.GetComponent<Renderer>(), Color.white);
            Destroy(marker.GetComponent<Collider>());

            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Formation Selection Ring";
            ring.transform.SetParent(member.transform, false);
            ring.transform.localPosition = new Vector3(0f, -1f, 0f);
            ring.transform.localScale = new Vector3(1.25f, 0.03f, 1.25f);
            AssignSupportedMaterial(ring.GetComponent<Renderer>(), SelectionColor);
            Destroy(ring.GetComponent<Collider>());
            ring.AddComponent<FormationSelectionRing>();
            ring.SetActive(false);
            return memberAgent;
        }

        private static void CreateFrontIndicator(Transform parent)
        {
            var indicator = GameObject.CreatePrimitive(PrimitiveType.Cube);
            indicator.name = "Formation Front Indicator";
            indicator.transform.SetParent(parent, false);
            indicator.transform.localPosition = new Vector3(0f, 0.08f, 1.6f);
            indicator.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            indicator.transform.localScale = new Vector3(0.32f, 0.06f, 0.32f);
            AssignSupportedMaterial(indicator.GetComponent<Renderer>(), Color.white);
            Destroy(indicator.GetComponent<Collider>());
            indicator.AddComponent<FormationFrontIndicator>();
        }

        private static string FacingCardinal(float degrees)
        {
            if (degrees < 45f || degrees >= 315f) return "N";
            if (degrees < 135f) return "E";
            if (degrees < 225f) return "S";
            return "W";
        }

        private void ReForm()
        {
            for (var i = 0; i < members.Count; i++) members[i].AssignSlot(i);
        }

        private void UpdateMemberMovement()
        {
            for (var i = 0; i < members.Count; i++)
            {
                var member = members[i];
                var destination = MemberSlotWorldPosition(member.SlotIndex);
                FormationMemberAgent attackTarget = null;
                if (target != null)
                {
                    attackTarget = Type == FormationType.Archers
                        ? target.SelectVolleyMember(member.Identity)
                        : target.SelectMemberTarget(member.WorldPosition, 0);
                    if (attackTarget != null)
                    {
                        if (Type != FormationType.Archers)
                        {
                            var offset = member.WorldPosition - attackTarget.WorldPosition;
                            offset.y = 0f;
                            if (offset.sqrMagnitude <= 0.01f) offset = -transform.forward;
                            destination = attackTarget.WorldPosition + offset.normalized * (AttackRange * 0.42f);
                        }
                    }
                }
                member.TickMovement(destination, MoveSpeed, members, attackTarget);
            }
        }

        private void SynchronizeMembersForAnchorTeleport()
        {
            var anchorDelta = transform.position - lastAnchorPosition;
            lastAnchorPosition = transform.position;
            if (anchorDelta.sqrMagnitude <= 4f) return;
            foreach (var member in members) member.TeleportBy(anchorDelta);
        }

        private FormationMemberAgent SelectMemberTarget(Vector3 attackerPosition, int offset)
        {
            if (members.Count == 0) return null;
            var ordered = new List<FormationMemberAgent>(members);
            ordered.Sort((left, right) =>
            {
                var distance = (left.WorldPosition - attackerPosition).sqrMagnitude
                    .CompareTo((right.WorldPosition - attackerPosition).sqrMagnitude);
                return distance != 0 ? distance : left.Identity.CompareTo(right.Identity);
            });
            return ordered[Mathf.Abs(offset) % ordered.Count];
        }

        private FormationMemberAgent SelectVolleyMember(int index) =>
            members.Count == 0 ? null : members[Mathf.Abs(index) % members.Count];

        internal Vector3 MemberSlotWorldPosition(int slotIndex) =>
            transform.TransformPoint(FormationMemberRules.Slot(slotIndex));

        private static void AssignSupportedMaterial(Renderer itemRenderer, Color color)
        {
            var shader = Shader.Find(SupportedShaderName);
            if (shader == null) throw new InvalidOperationException($"Required shader not found: {SupportedShaderName}");
            itemRenderer.sharedMaterial = new Material(shader) { color = color };
        }

        private static bool Approximately(Color actual, Color expected) =>
            Mathf.Abs(actual.r - expected.r) < 0.01f && Mathf.Abs(actual.g - expected.g) < 0.01f &&
            Mathf.Abs(actual.b - expected.b) < 0.01f && Mathf.Abs(actual.a - expected.a) < 0.01f;
    }

    public sealed class FormationMemberAgent : MonoBehaviour
    {
        private const float RepathSeconds = 0.18f;
        private const float SlotTolerance = 0.08f;
        private const float PathCornerAdvanceDistance = 0.02f;
        private const float PathCornerSteeringDistance = 0.15f;
        private const float SeparationRadius = 0.85f;
        private const float ExactSlotSampleRadius = 0.12f;
        private const float PreviousDestinationSampleRadius = 0.5f;
        private const float BlockedSlotSearchRadius = 3f;
        private const float BlockedSlotClearance = 0.2f;
        private const float BlockedSlotProbeStep = 0.5f;
        private NavMeshPath path;
        private NavMeshPath stepPath;
        private FormationAgent owner;
        private Vector3 worldPosition;
        private Quaternion worldRotation;
        private Vector3 pathDestination;
        private Vector3 reachableDestination;
        private Vector3 reachableDestinationRequest;
        private float repathRemaining;
        private int pathCorner;
        private float attackRemaining;
        private bool hasCompletePath;
        private bool hasReachableDestination;

        public int Identity { get; private set; }
        public int SlotIndex { get; private set; }
        public int Health { get; private set; }
        public bool IsAlive => owner != null && Health > 0;
        public Vector3 WorldPosition => worldPosition;
        public Vector3 AssignedSlotWorldPosition => owner == null ? worldPosition :
            owner.MemberSlotWorldPosition(SlotIndex);
        public Vector3 NavigationDestination => reachableDestination;
        public bool CanAttack => attackRemaining <= 0f;
        public FormationMemberAgent AttackTarget { get; private set; }
        public int ProjectileImpactCount { get; private set; }
        public Vector3 LastProjectileImpactPosition { get; private set; }

        public void Initialize(FormationAgent formation, int identity, int health)
        {
            owner = formation;
            path = new NavMeshPath();
            stepPath = new NavMeshPath();
            Identity = identity;
            SlotIndex = identity;
            Health = health;
            worldPosition = transform.position;
            worldRotation = transform.rotation;
            pathDestination = worldPosition;
            reachableDestination = worldPosition;
            reachableDestinationRequest = worldPosition;
            hasReachableDestination = true;
        }

        public void AssignSlot(int slotIndex) => SlotIndex = slotIndex;

        public void ApplyDamage(int damage, FlankDirection flank)
        {
            Health = Mathf.Max(0, Health - Mathf.Max(0, damage));
            GetComponent<FormationMemberVisual>()?.ShowHit(flank);
        }

        public void DetachForDeath()
        {
            transform.SetParent(null, true);
            transform.position = worldPosition;
            owner = null;
        }

        public void TeleportBy(Vector3 displacement)
        {
            worldPosition += displacement;
            pathDestination += displacement;
            reachableDestination += displacement;
            reachableDestinationRequest += displacement;
            transform.position = worldPosition;
            repathRemaining = 0f;
        }

        internal void AssignAttackTarget(FormationMemberAgent target) => AttackTarget = target;

        public void TickMovement(Vector3 desiredWorldPosition, float speed,
            IReadOnlyList<FormationMemberAgent> formationMembers, FormationMemberAgent attackTarget)
        {
            if (!IsAlive) return;
            AssignAttackTarget(attackTarget);
            attackRemaining -= Time.deltaTime;
            transform.position = worldPosition;
            transform.rotation = worldRotation;
            desiredWorldPosition.y = worldPosition.y;
            repathRemaining -= Time.deltaTime;
            if (repathRemaining <= 0f || (pathDestination - desiredWorldPosition).sqrMagnitude > 0.16f)
                RecalculatePath(desiredWorldPosition);

            var toDestination = reachableDestination - worldPosition;
            toDestination.y = 0f;
            var pathStep = PathStep();
            var pathDirection = pathStep.sqrMagnitude > 0.000001f ? pathStep.normalized : Vector3.zero;
            var direction = pathDirection;
            var separation = Separation(formationMembers);
            if (pathStep.magnitude > PathCornerSteeringDistance && separation.sqrMagnitude > 0.01f)
                direction = (direction + separation * 0.7f).normalized;

            if (toDestination.sqrMagnitude > SlotTolerance * SlotTolerance && direction.sqrMagnitude > 0.01f)
            {
                var catchup = Mathf.Clamp(toDestination.magnitude * 0.18f, 0f, speed * 0.3f);
                var distance = Mathf.Min((speed + catchup) * Time.deltaTime, pathStep.magnitude);
                if (TryResolveNavMeshStep(direction, pathDirection, distance, out var nextPosition))
                    worldPosition = nextPosition;
                worldPosition.y = transform.parent == null ? worldPosition.y : transform.parent.position.y + 0.85f;
                transform.position = worldPosition;
            }

            var facing = AttackTarget == null ? direction : AttackTarget.WorldPosition - worldPosition;
            facing.y = 0f;
            if (facing.sqrMagnitude > 0.01f)
                worldRotation = Quaternion.RotateTowards(worldRotation,
                    Quaternion.LookRotation(facing.normalized), 540f * Time.deltaTime);
            transform.rotation = worldRotation;
        }

        public void BeginAttackCooldown(float seconds) => attackRemaining = Mathf.Max(0f, seconds);

        public void RecordProjectileImpact(Vector3 position)
        {
            ProjectileImpactCount++;
            LastProjectileImpactPosition = position;
        }

        private void RecalculatePath(Vector3 destination)
        {
            repathRemaining = RepathSeconds + Identity * 0.007f;
            pathDestination = destination;
            pathCorner = 1;
            hasCompletePath = false;
            var start = Grounded(worldPosition);
            var end = Grounded(destination);
            if (!NavMesh.SamplePosition(start, out var sampledStart, 0.8f, NavMesh.AllAreas))
            {
                path.ClearCorners();
                return;
            }

            if (NavMesh.SamplePosition(end, out var exactEnd, ExactSlotSampleRadius, NavMesh.AllAreas) &&
                TrySetCompletePath(sampledStart.position, exactEnd.position, destination, true)) return;

            if ((reachableDestinationRequest - destination).sqrMagnitude <= 0.16f &&
                hasReachableDestination &&
                NavMesh.SamplePosition(Grounded(reachableDestination), out var previousEnd,
                    PreviousDestinationSampleRadius, NavMesh.AllAreas) &&
                TrySetCompletePath(sampledStart.position, previousEnd.position, destination, false)) return;

            var towardMember = start - end;
            if (towardMember.sqrMagnitude > 0.0001f)
            {
                towardMember.Normalize();
                for (var radius = BlockedSlotProbeStep; radius <= BlockedSlotSearchRadius;
                     radius += BlockedSlotProbeStep)
                {
                    var probe = end + towardMember * radius;
                    if (!NavMesh.SamplePosition(probe, out var sampledProbe, ExactSlotSampleRadius,
                            NavMesh.AllAreas)) continue;
                    var clearedProbe = sampledProbe.position + towardMember * BlockedSlotClearance;
                    if (NavMesh.SamplePosition(clearedProbe, out var clearedFallback, BlockedSlotClearance,
                            NavMesh.AllAreas) &&
                        TrySetCompletePath(sampledStart.position, clearedFallback.position, destination, true))
                        return;
                }
            }

            if (NavMesh.SamplePosition(end, out var fallbackEnd, BlockedSlotSearchRadius, NavMesh.AllAreas))
            {
                var awayFromBlockedSlot = Grounded(fallbackEnd.position) - end;
                if (awayFromBlockedSlot.sqrMagnitude > 0.0001f)
                {
                    var clearedEnd = fallbackEnd.position + awayFromBlockedSlot.normalized * BlockedSlotClearance;
                    if (NavMesh.SamplePosition(clearedEnd, out var clearedFallback, BlockedSlotClearance,
                            NavMesh.AllAreas) &&
                        TrySetCompletePath(sampledStart.position, clearedFallback.position, destination, true))
                        return;
                }
                if (TrySetCompletePath(sampledStart.position, fallbackEnd.position, destination, true)) return;
            }

            path.ClearCorners();
        }

        private bool TrySetCompletePath(Vector3 start, Vector3 end, Vector3 requestedDestination,
            bool rememberRequest)
        {
            if (!NavMesh.CalculatePath(start, end, NavMesh.AllAreas, path) ||
                path.status != NavMeshPathStatus.PathComplete) return false;
            reachableDestination = new Vector3(end.x, requestedDestination.y, end.z);
            if (rememberRequest) reachableDestinationRequest = requestedDestination;
            hasReachableDestination = true;
            hasCompletePath = true;
            return true;
        }

        private Vector3 PathStep()
        {
            if (!hasCompletePath || path == null) return Vector3.zero;
            var corners = path.corners;
            if (corners == null || corners.Length == 0) return Vector3.zero;
            while (pathCorner < corners.Length)
            {
                var cornerStep = Grounded(corners[pathCorner]) - Grounded(worldPosition);
                if (cornerStep.sqrMagnitude > PathCornerAdvanceDistance * PathCornerAdvanceDistance)
                    return cornerStep;
                pathCorner++;
            }
            return Vector3.zero;
        }

        private bool TryResolveNavMeshStep(Vector3 steeredDirection, Vector3 pathDirection, float distance,
            out Vector3 nextPosition)
        {
            var hasSeparationSteering = (steeredDirection - pathDirection).sqrMagnitude > 0.001f;
            if (!hasSeparationSteering) return TryNavMeshStep(pathDirection, distance, out nextPosition);
            if (TryNavMeshStep(steeredDirection, distance, out nextPosition) &&
                IsForwardStep(nextPosition, steeredDirection, distance)) return true;
            if (TryNavMeshStep(pathDirection, distance, out nextPosition)) return true;
            nextPosition = worldPosition;
            return false;
        }

        private bool IsForwardStep(Vector3 nextPosition, Vector3 direction, float distance)
        {
            var displacement = Grounded(nextPosition) - Grounded(worldPosition);
            var minimumForwardDistance = Mathf.Min(0.01f, distance * 0.1f);
            return Vector3.Dot(displacement, Grounded(direction).normalized) >= minimumForwardDistance;
        }

        private bool TryNavMeshStep(Vector3 direction, float distance, out Vector3 nextPosition)
        {
            nextPosition = worldPosition;
            if (!hasCompletePath || direction.sqrMagnitude <= 0.01f || distance <= 0f ||
                !NavMesh.SamplePosition(Grounded(worldPosition), out var sampledStart, 0.35f, NavMesh.AllAreas))
                return false;

            var groundedDirection = Grounded(direction).normalized;
            var candidate = sampledStart.position + groundedDirection * distance;
            if (!NavMesh.SamplePosition(candidate, out var sampledCandidate, 0.15f, NavMesh.AllAreas))
                return false;
            var actualDistance = Vector3.Distance(Grounded(worldPosition), Grounded(sampledCandidate.position));
            if (actualDistance > distance + 0.01f) return false;
            if (NavMesh.Raycast(sampledStart.position, sampledCandidate.position, out _, NavMesh.AllAreas) &&
                !HasDirectCompleteStep(sampledStart.position, sampledCandidate.position, actualDistance))
                return false;

            nextPosition = new Vector3(sampledCandidate.position.x, worldPosition.y, sampledCandidate.position.z);
            return true;
        }

        private bool HasDirectCompleteStep(Vector3 start, Vector3 end, float maximumDistance)
        {
            if (!NavMesh.CalculatePath(start, end, NavMesh.AllAreas, stepPath) ||
                stepPath.status != NavMeshPathStatus.PathComplete || stepPath.corners.Length > 2)
                return false;
            var corners = stepPath.corners;
            var distance = 0f;
            for (var i = 1; i < corners.Length; i++) distance += Vector3.Distance(corners[i - 1], corners[i]);
            return distance <= maximumDistance + 0.05f;
        }

        private Vector3 Separation(IReadOnlyList<FormationMemberAgent> formationMembers)
        {
            var result = Vector3.zero;
            foreach (var other in formationMembers)
            {
                if (other == null || other == this || !other.IsAlive) continue;
                var away = worldPosition - other.worldPosition;
                away.y = 0f;
                var distance = away.magnitude;
                if (distance >= SeparationRadius) continue;
                if (distance <= 0.01f)
                {
                    var angle = (Identity + 1) * 137.5f * Mathf.Deg2Rad;
                    away = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    distance = 0.01f;
                }
                result += away.normalized * ((SeparationRadius - distance) / SeparationRadius);
            }
            return result;
        }

        private static Vector3 Grounded(Vector3 position) => new(position.x, 0f, position.z);
    }

    public sealed class FormationSelectionRing : MonoBehaviour { }

    public sealed class FormationFrontIndicator : MonoBehaviour { }

    public sealed class FormationMemberVisual : MonoBehaviour
    {
        private const float HitSeconds = 0.16f;
        private Renderer memberRenderer;
        private Color restingColor;
        private Vector3 restingScale;
        private Coroutine hitRoutine;

        public bool IsShowingHitFeedback { get; private set; }
        public FlankDirection LastHitFlank { get; private set; }

        public void Initialize(Renderer targetRenderer)
        {
            memberRenderer = targetRenderer;
            restingColor = targetRenderer.sharedMaterial.color;
            restingScale = transform.localScale;
        }

        public void ShowHit(FlankDirection flank = FlankDirection.Front)
        {
            if (hitRoutine != null) StopCoroutine(hitRoutine);
            LastHitFlank = flank;
            hitRoutine = StartCoroutine(Flash(flank));
        }

        private IEnumerator Flash(FlankDirection flank)
        {
            IsShowingHitFeedback = true;
            var flashColor = flank switch
            {
                FlankDirection.Side => new Color(1f, 0.75f, 0.2f),
                FlankDirection.Rear => new Color(1f, 0.25f, 0.08f),
                _ => Color.white
            };
            var scale = flank switch
            {
                FlankDirection.Side => 1.2f,
                FlankDirection.Rear => 1.3f,
                _ => 1.12f
            };
            memberRenderer.sharedMaterial.color = Color.Lerp(restingColor, flashColor, 0.85f);
            transform.localScale = restingScale * scale;
            yield return new WaitForSeconds(HitSeconds);
            memberRenderer.sharedMaterial.color = restingColor;
            transform.localScale = restingScale;
            IsShowingHitFeedback = false;
            hitRoutine = null;
        }
    }
}
