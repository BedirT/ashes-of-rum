using System.Collections.Generic;
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

        public static bool PreservesRoute(IReadOnlyList<Vector3> housePositions, Vector3 candidate)
        {
            const int columns = 29;
            const int rows = 21;
            const float cellSize = 1.5f;
            var blocked = new bool[columns, rows];
            for (var x = 0; x < columns; x++)
            for (var z = 0; z < rows; z++)
            {
                var point = new Vector3(MinX + x * cellSize, 0f, MinZ + z * cellSize);
                blocked[x, z] = IsBlocked(point, candidate);
                if (blocked[x, z]) continue;
                for (var i = 0; i < housePositions.Count; i++)
                {
                    if (!IsBlocked(point, housePositions[i])) continue;
                    blocked[x, z] = true;
                    break;
                }
            }

            var open = new Queue<Vector2Int>();
            var visited = new bool[columns, rows];
            for (var x = 0; x < columns; x++)
            {
                if (blocked[x, 0]) continue;
                visited[x, 0] = true;
                open.Enqueue(new Vector2Int(x, 0));
            }

            var directions = new[]
            {
                new Vector2Int(1, 0), new Vector2Int(-1, 0),
                new Vector2Int(0, 1), new Vector2Int(0, -1)
            };
            while (open.Count > 0)
            {
                var cell = open.Dequeue();
                if (cell.y == rows - 1) return true;
                foreach (var direction in directions)
                {
                    var next = cell + direction;
                    if (next.x < 0 || next.x >= columns || next.y < 0 || next.y >= rows) continue;
                    if (blocked[next.x, next.y] || visited[next.x, next.y]) continue;
                    visited[next.x, next.y] = true;
                    open.Enqueue(next);
                }
            }
            return false;
        }

        private static bool IsBlocked(Vector3 cell, Vector3 house) =>
            Mathf.Abs(cell.x - house.x) <= HouseHalfSize &&
            Mathf.Abs(cell.z - house.z) <= HouseHalfSize;
    }
}
