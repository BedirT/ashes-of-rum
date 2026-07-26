using UnityEngine;

namespace AshesOfRum
{
    public sealed class ResourceCache : MonoBehaviour
    {
        public int Remaining { get; private set; }

        public Vector3 GetGatherPoint(int slot)
        {
            var angle = Mathf.Repeat(slot, 4) * Mathf.PI * 0.5f;
            return transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 1.45f;
        }

        public void Initialize(int supplies)
        {
            Remaining = Mathf.Max(0, supplies);
        }

        public int TakeBatch(int requested)
        {
            var taken = Mathf.Min(Mathf.Max(0, requested), Remaining);
            Remaining -= taken;
            if (Remaining == 0)
            {
                foreach (var renderer in GetComponentsInChildren<Renderer>())
                    renderer.material.color = new Color(0.24f, 0.22f, 0.19f);
            }
            return taken;
        }
    }
}
