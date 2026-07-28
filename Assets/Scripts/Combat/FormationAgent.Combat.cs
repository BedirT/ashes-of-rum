using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;


namespace AshesOfRum
{
    public sealed partial class FormationAgent : MonoBehaviour
    {
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

    }
}
