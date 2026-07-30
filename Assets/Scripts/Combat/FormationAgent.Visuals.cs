using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;


namespace AshesOfRum
{
    public sealed partial class FormationAgent : MonoBehaviour
    {
        private IEnumerator FireArrow(FormationMemberAgent attacker, FormationAgent intendedTarget,
            FormationMemberAgent intendedMember)
        {
            attacker?.ShowAttack();
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

        private IEnumerator FireArrow(FormationMemberAgent attacker, WorkerAgent intendedTarget)
        {
            attacker?.ShowAttack();
            var arrow = CreateArrow();
            var start = attacker.WorldPosition + Vector3.up * 1.4f;
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

        private IEnumerator FireArrow(FormationMemberAgent attacker, ICombatStructure intendedTarget)
        {
            attacker?.ShowAttack();
            var arrow = CreateArrow();
            var start = attacker.WorldPosition + Vector3.up * 1.4f;
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
            var authoredPrefab = Resources.Load<GameObject>("Presentation/ArcherArrowProjectile");
            if (authoredPrefab != null) return Instantiate(authoredPrefab);
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
                projectile.rotation = Quaternion.FromToRotation(
                    projectile.GetComponent<AuthoredArrowProjectile>() == null ? Vector3.up : Vector3.forward,
                    direction.normalized);
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
            ArcherMemberPresentation archerPresentation = null;
            if (Type == FormationType.Archers)
            {
                var authoredPrefab = Resources.Load<GameObject>("Presentation/ArcherMember");
                if (authoredPrefab != null)
                {
                    Destroy(memberRenderer);
                    memberRenderer = null;
                    var authored = Instantiate(authoredPrefab, member.transform);
                    authored.name = "Authored Archer";
                    authored.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                    authored.transform.localScale = new Vector3(1f / 0.7f, 1f / 0.85f, 1f / 0.7f);
                    archerPresentation = authored.GetComponent<ArcherMemberPresentation>();
                    archerPresentation.Initialize(IsFriendly ? FriendlyColor : HostileColor, transform.position.y);
                }
            }
            member.AddComponent<FormationMemberVisual>().Initialize(memberRenderer, archerPresentation);
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
}
