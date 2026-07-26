using UnityEngine;

namespace AshesOfRum
{
    public sealed class HouseBuilding : MonoBehaviour
    {
        private float buildSeconds;
        private float elapsed;
        private Renderer[] renderers;

        public bool IsComplete { get; private set; }
        public float Progress => buildSeconds <= 0f ? 0f : Mathf.Clamp01(elapsed / buildSeconds);
        public Vector3 BuildPoint => transform.position + Vector3.back * 2.4f;

        public void Initialize(float duration)
        {
            buildSeconds = Mathf.Max(0.1f, duration);
            renderers = GetComponentsInChildren<Renderer>();
            SetConstructionAppearance();
        }

        public bool Advance(float seconds)
        {
            if (IsComplete) return false;
            elapsed += Mathf.Max(0f, seconds);
            if (elapsed < buildSeconds) return false;
            IsComplete = true;
            foreach (var itemRenderer in renderers)
                itemRenderer.material.color = new Color(0.12f, 0.38f, 0.82f);
            return true;
        }

        private void SetConstructionAppearance()
        {
            foreach (var itemRenderer in renderers)
                itemRenderer.material.color = new Color(0.42f, 0.55f, 0.68f);
        }
    }
}
