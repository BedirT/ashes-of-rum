using UnityEngine;

namespace AshesOfRum
{
    [CreateAssetMenu(menuName = "Ashes of Rum/Economy Tuning")]
    public sealed class EconomyTuning : ScriptableObject
    {
        [Min(0)] public int startingSupplies = 100;
        [Min(1)] public int cacheSupplies = 200;
        [Min(1)] public int gatherBatch = 10;
        [Min(0.05f)] public float gatherSeconds = 0.75f;
        [Min(0.1f)] public float workerSpeed = 5f;
    }
}
