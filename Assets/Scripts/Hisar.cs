using System;
using System.Collections;
using UnityEngine;

namespace AshesOfRum
{
    public sealed class Hisar : MonoBehaviour, ICombatStructure
    {
        private Action<Hisar> destroyedCallback;
        private Renderer[] renderers;
        private Color[] restingColors;
        private Coroutine hitRoutine;

        public Vector3 DropOffPoint => transform.position + transform.forward * -3.2f;
        public Component TargetComponent => this;
        public bool IsFriendly { get; private set; }
        public bool IsAttackable => !IsDestroyed;
        public bool IsDestroyed { get; private set; }
        public int Health { get; private set; }
        public int MaxHealth { get; private set; }
        public Vector3 AimPoint => transform.position + Vector3.up * 1.5f;
        public float CombatRadius => 3f;

        public void Initialize(bool friendly, int maximumHealth, Action<Hisar> onDestroyed)
        {
            IsFriendly = friendly;
            MaxHealth = Mathf.Max(1, maximumHealth);
            Health = MaxHealth;
            destroyedCallback = onDestroyed;
            renderers = GetComponentsInChildren<Renderer>();
            restingColors = new Color[renderers.Length];
            for (var index = 0; index < renderers.Length; index++)
                restingColors[index] = renderers[index].sharedMaterial.color;
        }

        public bool ApplyStructuralDamage(int amount)
        {
            if (IsDestroyed || amount <= 0) return false;
            Health = Mathf.Max(0, Health - amount);
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

        private IEnumerator FlashHit()
        {
            for (var index = 0; index < renderers.Length; index++)
                if (renderers[index] != null)
                    renderers[index].sharedMaterial.color = Color.Lerp(restingColors[index], Color.white, 0.7f);
            yield return new WaitForSeconds(0.16f);
            if (!IsDestroyed)
                for (var index = 0; index < renderers.Length; index++)
                    if (renderers[index] != null) renderers[index].sharedMaterial.color = restingColors[index];
            hitRoutine = null;
        }
    }
}
