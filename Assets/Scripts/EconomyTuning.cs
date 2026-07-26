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
        [Min(0.1f)] public float cacheFallbackRadius = 20f;
        [Min(1)] public int houseCost = 100;
        [Min(0.1f)] public float houseBuildSeconds = 3f;
        [Min(1)] public int startingPopulationCap = 12;
        [Min(1)] public int housePopulationCapacity = 8;
        [Min(1)] public int hardPopulationCap = 60;
    }
}
