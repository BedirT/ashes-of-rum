using UnityEngine;

namespace AshesOfRum
{
    public sealed class ResourceCache : MonoBehaviour
    {
        public static readonly Color AvailableColor = new(0.72f, 0.49f, 0.2f);
        private static readonly Color ExhaustedColor = new(0.24f, 0.22f, 0.19f);

        public int Remaining { get; private set; }

        public Vector3 GetGatherPoint(int slot)
        {
            var angle = Mathf.Repeat(slot, 4) * Mathf.PI * 0.5f;
            return transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 1.45f;
        }

        public void Initialize(int supplies)
        {
            Remaining = Mathf.Max(0, supplies);
            RefreshVisuals(Remaining > 0 ? AvailableColor : ExhaustedColor);
        }

        public int TakeBatch(int requested)
        {
            var taken = Mathf.Min(Mathf.Max(0, requested), Remaining);
            Remaining -= taken;
            if (Remaining == 0) RefreshVisuals(ExhaustedColor);
            return taken;
        }

        private void RefreshVisuals(Color color)
        {
            foreach (var itemRenderer in GetComponentsInChildren<Renderer>())
                itemRenderer.material.color = color;
            GetComponent<FogVisibilityTarget>()?.RefreshColors();
        }
    }
}
