using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AshesOfRum
{
    [RequireComponent(typeof(ConstructibleBuilding))]
    public sealed class WatchtowerAttack : MonoBehaviour
    {
        private ConstructibleBuilding building;
        private Func<IEnumerable<FormationAgent>> targets;
        private float range;
        private float attackSeconds;
        private float projectileSeconds;
        private int damage;
        private float attackReadyAt;
        private Action<Vector3> attackCallback;

        public FormationAgent CurrentTarget { get; private set; }
        public int ShotsFired { get; private set; }

        public void Initialize(EconomyTuning tuning, Func<IEnumerable<FormationAgent>> targetProvider,
            Action<Vector3> onAttack = null)
        {
            building = GetComponent<ConstructibleBuilding>();
            targets = targetProvider;
            range = tuning.watchtowerRange;
            attackSeconds = tuning.watchtowerAttackSeconds;
            projectileSeconds = tuning.projectileSeconds;
            damage = tuning.watchtowerDamage;
            attackCallback = onAttack;
        }

        private void Update()
        {
            if (building == null || !building.IsComplete || building.IsDestroyed) return;
            CurrentTarget = FindNearestTarget();
            if (CurrentTarget == null || Time.time < attackReadyAt) return;
            attackReadyAt = Time.time + attackSeconds;
            ShotsFired++;
            attackCallback?.Invoke(transform.position);
            StartCoroutine(FireProjectile(CurrentTarget));
        }

        private FormationAgent FindNearestTarget()
        {
            var rangeSquared = range * range;
            return targets?.Invoke()
                .Where(target => target != null && target.IsFriendly != building.IsFriendly && target.MemberCount > 0)
                .Select(target => new
                {
                    Target = target,
                    Distance = (target.transform.position - transform.position).sqrMagnitude
                })
                .Where(candidate => candidate.Distance <= rangeSquared)
                .OrderBy(candidate => candidate.Distance)
                .ThenBy(candidate => candidate.Target.name)
                .Select(candidate => candidate.Target)
                .FirstOrDefault();
        }

        private IEnumerator FireProjectile(FormationAgent intendedTarget)
        {
            var projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projectile.name = "Watchtower Projectile";
            projectile.transform.localScale = Vector3.one * 0.25f;
            Destroy(projectile.GetComponent<Collider>());
            var renderer = projectile.GetComponent<Renderer>();
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            renderer.sharedMaterial = new Material(shader) { color = new Color(1f, 0.55f, 0.12f) };

            var start = transform.position + Vector3.up * 4.2f;
            var elapsed = 0f;
            while (elapsed < projectileSeconds && intendedTarget != null)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / projectileSeconds);
                var end = intendedTarget.transform.position + Vector3.up;
                projectile.transform.position = Vector3.Lerp(start, end, t) +
                                                Vector3.up * (Mathf.Sin(t * Mathf.PI) * 1.2f);
                yield return null;
            }
            if (intendedTarget != null) intendedTarget.ApplyFixedDamage(damage);
            Destroy(projectile);
        }
    }
}
