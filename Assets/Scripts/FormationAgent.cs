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
        private System.Func<IEnumerable<FormationAgent>> hostileProvider;
        private System.Func<FormationAgent, bool> visibilityPredicate;
        private Action<int> casualtyCallback;
        private Action<FormationAgent> destroyedCallback;
        private NavMeshAgent navAgent;
        private int nextHitMemberIndex;
        private float attackRemaining;
        private Vector3 destination;
        private bool hasDestination;

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
        public int LastAttackMemberCount { get; private set; }

        public void Initialize(FormationType type, bool friendly, EconomyTuning combatTuning,
            Action<int> onCasualty = null, Action<FormationAgent> onDestroyed = null,
            System.Func<IEnumerable<FormationAgent>> availableHostiles = null,
            System.Func<FormationAgent, bool> isTargetVisible = null)
        {
            Type = type;
            IsFriendly = friendly;
            tuning = combatTuning;
            casualtyCallback = onCasualty;
            destroyedCallback = onDestroyed;
            hostileProvider = availableHostiles;
            visibilityPredicate = isTargetVisible;
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
        }

        public void SetSelected(bool selected)
        {
            IsSelected = selected;
            foreach (var ring in GetComponentsInChildren<FormationSelectionRing>(true))
                ring.gameObject.SetActive(selected);
        }

        public bool IssueFocus(FormationAgent hostile)
        {
            if (!IsValidTarget(hostile)) return false;
            target = hostile;
            hasDestination = false;
            CurrentOrder = FormationOrder.Focus;
            StartNavigation(hostile.transform.position);
            if (hostile.target == null)
            {
                hostile.target = this;
                hostile.CurrentOrder = FormationOrder.Focus;
            }
            return true;
        }

        public void IssueMove(Vector3 position)
        {
            target = null;
            destination = Grounded(position);
            hasDestination = true;
            CurrentOrder = FormationOrder.Move;
            StartNavigation(destination);
        }

        public void IssueAttackMove(Vector3 position)
        {
            target = null;
            destination = Grounded(position);
            hasDestination = true;
            CurrentOrder = FormationOrder.AttackMove;
            StartNavigation(destination);
        }

        public void IssueStop()
        {
            target = null;
            hasDestination = false;
            CurrentOrder = FormationOrder.Idle;
            StopNavigation();
        }

        public void ApplyDeterministicHit(FormationType attackerType)
        {
            ApplyFixedDamage(CombatRules.Damage(attackerType, Type, tuning.baseDamage, tuning.counterMultiplier));
        }

        public void ApplyFixedDamage(int damage)
        {
            if (members.Count == 0 || damage <= 0) return;
            var hitIndex = nextHitMemberIndex % members.Count;
            memberHealth[hitIndex] -= damage;
            members[hitIndex].GetComponent<FormationMemberVisual>().ShowHit();
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
            StopNavigation();
            destroyedCallback?.Invoke(this);
            Destroy(gameObject, 0.25f);
        }

        private void Update()
        {
            if (!IsValidTarget(target))
            {
                target = null;
                if (CurrentOrder == FormationOrder.Focus)
                {
                    CurrentOrder = FormationOrder.Idle;
                    StopNavigation();
                }
            }
            if (CurrentOrder == FormationOrder.AttackMove && target == null)
            {
                target = FindNearestVisibleHostile();
                if (target != null && target.target == null)
                {
                    target.target = this;
                    target.CurrentOrder = FormationOrder.Focus;
                }
            }
            if (target != null)
            {
                MoveOrAttackTarget();
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

        private void MoveOrAttackTarget()
        {
            var delta = target.transform.position - transform.position;
            delta.y = 0f;
            var range = Type == FormationType.Archers ? 7f : 2.2f;
            if (delta.sqrMagnitude > range * range)
            {
                MoveAndFace(delta);
                return;
            }

            StopNavigation();
            if (delta.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(delta.normalized), 360f * Time.deltaTime);
            attackRemaining -= Time.deltaTime;
            if (attackRemaining > 0f) return;
            attackRemaining = tuning.attackSeconds;
            ExecuteAttackVolley(target);
        }

        private void MoveAndFace(Vector3 delta)
        {
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

        private bool IsValidTarget(FormationAgent candidate) => candidate != null &&
            candidate.IsFriendly != IsFriendly && candidate.MemberCount > 0 &&
            (visibilityPredicate == null || visibilityPredicate(candidate));

        private static Vector3 Grounded(Vector3 position) => new(position.x, 0f, position.z);

        public bool ExecuteAttackVolley(FormationAgent intendedTarget)
        {
            if (intendedTarget == null || intendedTarget.IsFriendly == IsFriendly ||
                intendedTarget.MemberCount == 0 || members.Count == 0) return false;

            LastAttackMemberCount = members.Count;
            var attackers = members.ToArray();
            foreach (var attacker in attackers)
            {
                if (Type == FormationType.Archers) StartCoroutine(FireArrow(attacker.transform.position, intendedTarget));
                else intendedTarget.ApplyDeterministicHit(Type);
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
            if (intendedTarget != null) intendedTarget.ApplyDeterministicHit(Type);
            Destroy(arrow);
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

    public sealed class FormationMemberVisual : MonoBehaviour
    {
        private const float HitSeconds = 0.16f;
        private Renderer memberRenderer;
        private Color restingColor;
        private Vector3 restingScale;
        private Coroutine hitRoutine;

        public bool IsShowingHitFeedback { get; private set; }

        public void Initialize(Renderer targetRenderer)
        {
            memberRenderer = targetRenderer;
            restingColor = targetRenderer.sharedMaterial.color;
            restingScale = transform.localScale;
        }

        public void ShowHit()
        {
            if (hitRoutine != null) StopCoroutine(hitRoutine);
            hitRoutine = StartCoroutine(Flash());
        }

        private IEnumerator Flash()
        {
            IsShowingHitFeedback = true;
            memberRenderer.sharedMaterial.color = Color.Lerp(restingColor, Color.white, 0.8f);
            transform.localScale = restingScale * 1.12f;
            yield return new WaitForSeconds(HitSeconds);
            memberRenderer.sharedMaterial.color = restingColor;
            transform.localScale = restingScale;
            IsShowingHitFeedback = false;
            hitRoutine = null;
        }
    }
}
