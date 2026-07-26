using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AshesOfRum
{
    public sealed class FormationAgent : MonoBehaviour
    {
        private readonly List<GameObject> members = new();
        private EconomyTuning tuning;
        private FormationAgent target;
        private Action<int> casualtyCallback;
        private Action<FormationAgent> destroyedCallback;
        private int frontMemberHealth;
        private float attackRemaining;

        public FormationType Type { get; private set; }
        public bool IsFriendly { get; private set; }
        public int MemberCount => members.Count;
        public bool IsSelected { get; private set; }
        public FormationAgent Target => target;

        public void Initialize(FormationType type, bool friendly, EconomyTuning combatTuning,
            Action<int> onCasualty = null, Action<FormationAgent> onDestroyed = null)
        {
            Type = type;
            IsFriendly = friendly;
            tuning = combatTuning;
            casualtyCallback = onCasualty;
            destroyedCallback = onDestroyed;
            frontMemberHealth = tuning.memberHealth;
            for (var i = 0; i < 8; i++) members.Add(CreateMember(i));
        }

        public void SetSelected(bool selected)
        {
            IsSelected = selected;
            foreach (var ring in GetComponentsInChildren<FormationSelectionRing>(true))
                ring.gameObject.SetActive(selected);
        }

        public bool IssueFocus(FormationAgent hostile)
        {
            if (hostile == null || hostile.IsFriendly == IsFriendly || hostile.MemberCount == 0) return false;
            target = hostile;
            if (hostile.target == null) hostile.target = this;
            return true;
        }

        public void ApplyDeterministicHit(FormationType attackerType)
        {
            if (members.Count == 0) return;
            frontMemberHealth -= CombatRules.Damage(attackerType, Type, tuning.baseDamage, tuning.counterMultiplier);
            if (frontMemberHealth > 0) return;
            var casualty = members[0];
            members.RemoveAt(0);
            Destroy(casualty);
            casualtyCallback?.Invoke(1);
            frontMemberHealth = tuning.memberHealth;
            ReForm();
            if (members.Count != 0) return;
            target = null;
            destroyedCallback?.Invoke(this);
            Destroy(gameObject, 0.25f);
        }

        private void Update()
        {
            if (target == null || target.MemberCount == 0) return;
            var delta = target.transform.position - transform.position;
            delta.y = 0f;
            var range = Type == FormationType.Archers ? 7f : 1.7f;
            if (delta.sqrMagnitude > range * range)
            {
                transform.position += delta.normalized * (tuning.footSpeed * Time.deltaTime);
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(delta.normalized), 360f * Time.deltaTime);
                return;
            }

            if (delta.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(delta.normalized), 360f * Time.deltaTime);
            attackRemaining -= Time.deltaTime;
            if (attackRemaining > 0f) return;
            attackRemaining = tuning.attackSeconds;
            if (Type == FormationType.Archers) StartCoroutine(FireArrow(target));
            else target.ApplyDeterministicHit(Type);
        }

        private IEnumerator FireArrow(FormationAgent intendedTarget)
        {
            var arrow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            arrow.name = "Arrow";
            arrow.transform.localScale = new Vector3(0.06f, 0.35f, 0.06f);
            Destroy(arrow.GetComponent<Collider>());
            arrow.GetComponent<Renderer>().material.color = new Color(0.95f, 0.75f, 0.25f);
            var start = transform.position + Vector3.up * 1.4f;
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
            member.GetComponent<Renderer>().material.color = IsFriendly
                ? new Color(0.1f, 0.38f, 0.9f)
                : new Color(0.8f, 0.16f, 0.08f);

            var marker = GameObject.CreatePrimitive(IsFriendly ? PrimitiveType.Sphere : PrimitiveType.Cube);
            marker.name = IsFriendly ? "Black Falcon Diamond" : "Living Flame Square";
            marker.transform.SetParent(member.transform, false);
            marker.transform.localPosition = new Vector3(0f, 1.25f, 0f);
            marker.transform.localScale = Vector3.one * 0.35f;
            marker.GetComponent<Renderer>().material.color = Color.white;
            Destroy(marker.GetComponent<Collider>());

            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Formation Selection Ring";
            ring.transform.SetParent(member.transform, false);
            ring.transform.localPosition = new Vector3(0f, -1f, 0f);
            ring.transform.localScale = new Vector3(1.25f, 0.03f, 1.25f);
            ring.GetComponent<Renderer>().material.color = new Color(0.2f, 0.85f, 1f);
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
    }

    public sealed class FormationSelectionRing : MonoBehaviour { }
}
