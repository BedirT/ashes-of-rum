using UnityEngine;

namespace AshesOfRum
{
    public static class HousePlacementRules
    {
        public const float GridSize = 1f;
        private const float MinX = -21f;
        private const float MaxX = 21f;
        private const float MinZ = -4f;
        private const float MaxZ = 25f;
        private const float HouseHalfSize = 2.1f;

        public static Vector3 Snap(Vector3 position) => new(
            Mathf.Round(position.x / GridSize) * GridSize,
            0f,
            Mathf.Round(position.z / GridSize) * GridSize);

        public static bool IsInsidePlayableBounds(Vector3 position) =>
            position.x - HouseHalfSize >= MinX &&
            position.x + HouseHalfSize <= MaxX &&
            position.z - HouseHalfSize >= MinZ &&
            position.z + HouseHalfSize <= MaxZ;

    }
}
