using System;
using System.Collections;
using UnityEngine;

namespace AshesOfRum
{
    public class ConstructibleBuilding : MonoBehaviour, ICombatStructure
    {
        private float buildSeconds;
        private float elapsed;
        private Renderer[] renderers;
        private Color completeColor;
        private Action<ConstructibleBuilding> destroyedCallback;
        private Action<Vector3> damagedCallback;
        private Coroutine hitRoutine;
        private static readonly Color ConstructionColor = new(0.42f, 0.55f, 0.68f);

        public BuildingType Type { get; private set; }
        public Component TargetComponent => this;
        public bool IsFriendly { get; private set; } = true;
        public bool IsAttackable => !IsDestroyed;
        public bool IsComplete { get; private set; }
        public bool IsDestroyed { get; private set; }
        public bool WasDemolished { get; private set; }
        public bool IsSelected { get; private set; }
        public int Health { get; private set; }
        public int MaxHealth { get; private set; }
        public float Progress => buildSeconds <= 0f ? 0f : Mathf.Clamp01(elapsed / buildSeconds);
        public Vector3 BuildPoint => transform.position + Vector3.back * 2.4f;
        public Vector3 DropOffPoint => transform.position + Vector3.back * 2.4f;
        public Vector3 AimPoint => transform.position + Vector3.up * 1.5f;
        public float CombatRadius => 2.4f;

        public void Initialize(BuildingType buildingType, float duration, int maximumHealth, Color finishedColor,
            Action<ConstructibleBuilding> onDestroyed, bool friendly = true, Action<Vector3> onDamaged = null)
        {
            Type = buildingType;
            IsFriendly = friendly;
            buildSeconds = Mathf.Max(0.1f, duration);
            MaxHealth = Mathf.Max(1, maximumHealth);
            Health = MaxHealth;
            completeColor = finishedColor;
            destroyedCallback = onDestroyed;
            damagedCallback = onDamaged;
            renderers = GetComponentsInChildren<Renderer>();
            SetColor(ConstructionColor);
            SetSelected(false);
        }

        public bool Advance(float seconds)
        {
            if (IsComplete || IsDestroyed) return false;
            elapsed += Mathf.Max(0f, seconds);
            if (elapsed < buildSeconds) return false;
            IsComplete = true;
            SetColor(completeColor);
            return true;
        }

        public void SetSelected(bool selected)
        {
            IsSelected = selected;
            var ring = transform.Find("Building Selection Ring");
            if (ring != null) ring.gameObject.SetActive(selected);
            GetComponent<WorldHealthBar>()?.SetSelected(selected);
        }

        public bool ApplyDamage(int amount)
        {
            if (IsDestroyed || amount <= 0) return false;
            damagedCallback?.Invoke(transform.position);
            Health = Mathf.Max(0, Health - amount);
            GetComponent<WorldHealthBar>()?.RecordDamage();
            if (Health == 0)
            {
                DestroyBuilding();
                return true;
            }
            if (hitRoutine != null) StopCoroutine(hitRoutine);
            hitRoutine = StartCoroutine(FlashHit());
            return false;
        }

        public bool ApplyStructuralDamage(int amount) => ApplyDamage(amount);

        public bool Demolish()
        {
            if (!IsComplete || IsDestroyed) return false;
            WasDemolished = true;
            Health = 0;
            DestroyBuilding();
            return true;
        }

        private void DestroyBuilding()
        {
            if (IsDestroyed) return;
            IsDestroyed = true;
            destroyedCallback?.Invoke(this);
        }

        private IEnumerator FlashHit()
        {
            var baseColor = IsComplete ? completeColor : ConstructionColor;
            SetColor(Color.Lerp(baseColor, Color.white, 0.75f));
            yield return new WaitForSeconds(0.16f);
            if (!IsDestroyed) SetColor(IsComplete ? completeColor : ConstructionColor);
            hitRoutine = null;
        }

        private void SetColor(Color color)
        {
            if (renderers == null) return;
            foreach (var itemRenderer in renderers)
            {
                if (itemRenderer.GetComponent<BuildingSelectionRing>() == null)
                    itemRenderer.material.color = color;
            }
            GetComponent<FogVisibilityTarget>()?.RefreshColors();
        }
    }

    public sealed class BuildingSelectionRing : MonoBehaviour { }
}
