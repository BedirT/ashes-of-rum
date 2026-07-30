using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;


namespace AshesOfRum
{
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
        private FormationMemberVisual visual;
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
            visual = GetComponent<FormationMemberVisual>();
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
            visual?.ShowHit(flank);
        }

        public bool DetachForDeath()
        {
            transform.SetParent(null, true);
            transform.position = worldPosition;
            owner = null;
            return visual?.ShowDeath() == true;
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
            var positionBeforeMovement = worldPosition;
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
            visual?.SetMoving(
                (worldPosition - positionBeforeMovement).sqrMagnitude > 0.000001f);
        }

        public void BeginAttackCooldown(float seconds) => attackRemaining = Mathf.Max(0f, seconds);

        public void ShowAttack() => visual?.ShowAttack();

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
}
