using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace AshesOfRum
{
    public sealed class FormationAgent : MonoBehaviour
    {
        private readonly List<GameObject> members = new();
        private readonly List<int> memberHealth = new();
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

        private static readonly Color FriendlyColor = new(0.1f, 0.38f, 0.9f);
        private static readonly Color HostileColor = new(0.8f, 0.16f, 0.08f);
        private static readonly Color SelectionColor = new(0.2f, 0.85f, 1f);
        private static readonly Color ArrowColor = new(0.95f, 0.75f, 0.25f);
        private const string SupportedShaderName = "Universal Render Pipeline/Lit";

        public FormationType Type { get; private set; }
        public bool IsFriendly { get; private set; }
        public int MemberCount => members.Count;
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
                foreach (var health in memberHealth) total += health;
                return total;
            }
        }
        public int MaximumMemberHealth => tuning == null ? 0 : tuning.memberHealth * 8;
        public int LastAttackMemberCount { get; private set; }
        public bool IsTurning { get; private set; }
        public float TurnProgress => IsTurning ? Mathf.Clamp01(turnElapsed / tuning.reorientationSeconds) : 1f;
        public float FacingDegrees => Mathf.Repeat(transform.eulerAngles.y, 360f);
        public string FacingLabel => $"{FacingCardinal(FacingDegrees)} {FacingDegrees:0} deg";
        public FlankDirection LastReceivedFlank { get; private set; }

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
                memberHealth.Add(tuning.memberHealth);
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
            CurrentOrder = FormationOrder.Idle;
            StopNavigation();
        }

        public void ApplyDeterministicHit(FormationType attackerType)
        {
            ApplyDeterministicHit(attackerType, transform.position + transform.forward);
        }

        public void ApplyDeterministicHit(FormationType attackerType, Vector3 attackerPosition)
        {
            var incoming = attackerPosition - transform.position;
            var flank = CombatRules.ClassifyFlank(transform.forward, incoming);
            ApplyFixedDamage(CombatRules.Damage(attackerType, Type, tuning.baseDamage, tuning.counterMultiplier,
                flank, tuning.sideDamageMultiplier, tuning.rearDamageMultiplier), flank);
        }

        public void ApplyFixedDamage(int damage) => ApplyFixedDamage(damage, FlankDirection.Front);

        private void ApplyFixedDamage(int damage, FlankDirection flank)
        {
            if (members.Count == 0 || damage <= 0) return;
            LastReceivedFlank = flank;
            damagedCallback?.Invoke(transform.position);
            GetComponent<WorldHealthBar>()?.RecordDamage();
            var hitIndex = nextHitMemberIndex % members.Count;
            memberHealth[hitIndex] -= damage;
            members[hitIndex].GetComponent<FormationMemberVisual>().ShowHit(flank);
            if (memberHealth[hitIndex] > 0)
            {
                nextHitMemberIndex = (hitIndex + 1) % members.Count;
                return;
            }

            var casualty = members[hitIndex];
            members.RemoveAt(hitIndex);
            memberHealth.RemoveAt(hitIndex);
            Destroy(casualty);
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
            if (!IsValidTarget(target))
                target = null;
            if (!IsValidWorkerTarget(workerTarget)) workerTarget = null;
            if (!IsValidStructureTarget(StructureTarget)) structureTargetComponent = null;
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
            ExecuteAttackVolley(target);
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

            LastAttackMemberCount = members.Count;
            attackCallback?.Invoke(transform.position);
            var attackers = members.ToArray();
            foreach (var attacker in attackers)
            {
                if (Type == FormationType.Archers) StartCoroutine(FireArrow(attacker.transform.position, intendedTarget));
                else intendedTarget.ApplyDeterministicHit(Type, transform.position);
            }
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
                    StartCoroutine(FireArrow(attacker.transform.position, intendedTarget));
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
                    StartCoroutine(FireArrow(attacker.transform.position, intendedTarget));
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

        private IEnumerator FireArrow(Vector3 memberPosition, FormationAgent intendedTarget)
        {
            var arrow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            arrow.name = "Arrow";
            arrow.transform.localScale = new Vector3(0.06f, 0.35f, 0.06f);
            Destroy(arrow.GetComponent<Collider>());
            AssignSupportedMaterial(arrow.GetComponent<Renderer>(), ArrowColor);
            var start = memberPosition + Vector3.up * 1.4f;
            var elapsed = 0f;
            while (elapsed < tuning.projectileSeconds && intendedTarget != null)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / tuning.projectileSeconds);
                var end = intendedTarget.transform.position + Vector3.up;
                arrow.transform.position = Vector3.Lerp(start, end, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 1.5f);
                yield return null;
            }
            if (intendedTarget != null) intendedTarget.ApplyDeterministicHit(Type, memberPosition);
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

        private GameObject CreateMember(int index)
        {
            var member = GameObject.CreatePrimitive(Type == FormationType.Cavalry ? PrimitiveType.Cube : PrimitiveType.Capsule);
            member.name = $"{Type} Member {index + 1}";
            member.transform.SetParent(transform, false);
            member.transform.localPosition = Slot(index);
            member.transform.localScale = Type == FormationType.Cavalry
                ? new Vector3(0.8f, 1.1f, 1.25f)
                : new Vector3(0.7f, 0.85f, 0.7f);
            var memberRenderer = member.GetComponent<Renderer>();
            AssignSupportedMaterial(memberRenderer, IsFriendly ? FriendlyColor : HostileColor);
            member.AddComponent<FormationMemberVisual>().Initialize(memberRenderer);

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
            return member;
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
            for (var i = 0; i < members.Count; i++) members[i].transform.localPosition = Slot(i);
        }

        private static Vector3 Slot(int index) =>
            new((index % 4 - 1.5f) * 1.15f, 0.85f, -(index / 4) * 1.35f);

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
