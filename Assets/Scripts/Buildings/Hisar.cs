using System;
using System.Collections;
using UnityEngine;

namespace AshesOfRum
{
    public sealed class Hisar : MonoBehaviour, ICombatStructure
    {
        private Action<Hisar> destroyedCallback;
        private Action<Vector3> damagedCallback;
        private Renderer[] renderers;
        private Color[] restingColors;
        private int[] colorProperties;
        private Coroutine hitRoutine;

        private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");

        public Vector3 DropOffPoint => transform.position + transform.forward * -3.2f;
        public Component TargetComponent => this;
        public bool IsFriendly { get; private set; }
        public bool IsSelected { get; private set; }
        public bool IsAttackable => !IsDestroyed;
        public bool IsDestroyed { get; private set; }
        public int Health { get; private set; }
        public int MaxHealth { get; private set; }
        public Vector3 AimPoint => transform.position + Vector3.up * 1.5f;
        public float CombatRadius => 3f;

        public void Initialize(bool friendly, int maximumHealth, Action<Hisar> onDestroyed,
            Action<Vector3> onDamaged = null)
        {
            IsFriendly = friendly;
            MaxHealth = Mathf.Max(1, maximumHealth);
            Health = MaxHealth;
            destroyedCallback = onDestroyed;
            damagedCallback = onDamaged;
            renderers = GetComponentsInChildren<Renderer>();
            restingColors = new Color[renderers.Length];
            colorProperties = new int[renderers.Length];
            for (var index = 0; index < renderers.Length; index++)
            {
                var material = renderers[index].sharedMaterial;
                var property = material.HasProperty(BaseColorProperty) ? BaseColorProperty : ColorProperty;
                colorProperties[index] = property;
                restingColors[index] = material.HasProperty(property) ? material.GetColor(property) : Color.white;
            }
        }

        public bool ApplyStructuralDamage(int amount)
        {
            if (IsDestroyed || amount <= 0) return false;
            damagedCallback?.Invoke(transform.position);
            Health = Mathf.Max(0, Health - amount);
            GetComponent<WorldHealthBar>()?.RecordDamage();
            if (Health == 0)
            {
                IsDestroyed = true;
                destroyedCallback?.Invoke(this);
                return true;
            }
            if (hitRoutine != null) StopCoroutine(hitRoutine);
            hitRoutine = StartCoroutine(FlashHit());
            return false;
        }

        public void SetSelected(bool selected)
        {
            IsSelected = selected;
            GetComponent<WorldHealthBar>()?.SetSelected(selected);
        }

        private IEnumerator FlashHit()
        {
            var propertyBlock = new MaterialPropertyBlock();
            for (var index = 0; index < renderers.Length; index++)
                if (renderers[index] != null)
                {
                    renderers[index].GetPropertyBlock(propertyBlock);
                    propertyBlock.SetColor(colorProperties[index],
                        Color.Lerp(restingColors[index], Color.white, 0.7f));
                    renderers[index].SetPropertyBlock(propertyBlock);
                    propertyBlock.Clear();
                }
            yield return new WaitForSeconds(0.16f);
            if (!IsDestroyed)
                for (var index = 0; index < renderers.Length; index++)
                    if (renderers[index] != null)
                    {
                        renderers[index].GetPropertyBlock(propertyBlock);
                        propertyBlock.SetColor(colorProperties[index], restingColors[index]);
                        renderers[index].SetPropertyBlock(propertyBlock);
                        propertyBlock.Clear();
                    }
            hitRoutine = null;
        }
    }
}
