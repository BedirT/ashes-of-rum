using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;


namespace AshesOfRum
{
    public sealed partial class FormationAgent : MonoBehaviour
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
        public float TurnDirectionDegrees => IsTurning
            ? Vector3.SignedAngle(turnStartRotation * Vector3.forward,
                turnTargetRotation * Vector3.forward, Vector3.up)
            : 0f;
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

        public bool CanReach(Vector3 targetPosition)
        {
            if (navAgent == null || !navAgent.isOnNavMesh) return false;
            var path = new NavMeshPath();
            return NavMesh.CalculatePath(transform.position, Grounded(targetPosition), NavMesh.AllAreas, path) &&
                   path.status == NavMeshPathStatus.PathComplete;
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
            if (!member.DetachForDeath()) Destroy(member.gameObject);
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

    }
}
