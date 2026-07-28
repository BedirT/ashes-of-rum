using System;
using System.Collections.Generic;
using UnityEngine;

namespace AshesOfRum
{
    public enum FogState
    {
        Unexplored,
        Explored,
        Visible
    }

    public sealed class FogOfWarMap
    {
        private readonly bool[] explored;
        private readonly bool[] visible;

        public FogOfWarMap(float minX, float maxX, float minZ, float maxZ, float cellSize)
        {
            if (maxX <= minX || maxZ <= minZ) throw new ArgumentOutOfRangeException(nameof(maxX));
            if (cellSize <= 0f) throw new ArgumentOutOfRangeException(nameof(cellSize));
            MinX = minX;
            MaxX = maxX;
            MinZ = minZ;
            MaxZ = maxZ;
            CellSize = cellSize;
            Columns = Mathf.CeilToInt((maxX - minX) / cellSize);
            Rows = Mathf.CeilToInt((maxZ - minZ) / cellSize);
            explored = new bool[Columns * Rows];
            visible = new bool[Columns * Rows];
        }

        public float MinX { get; }
        public float MaxX { get; }
        public float MinZ { get; }
        public float MaxZ { get; }
        public float CellSize { get; }
        public int Columns { get; }
        public int Rows { get; }

        public void UpdateVisibility(IEnumerable<Vector3> sources, float radius)
        {
            if (radius <= 0f) throw new ArgumentOutOfRangeException(nameof(radius));
            Array.Clear(visible, 0, visible.Length);
            var radiusSquared = radius * radius;
            foreach (var source in sources)
            {
                var minColumn = Mathf.Max(0, Mathf.FloorToInt((source.x - radius - MinX) / CellSize));
                var maxColumn = Mathf.Min(Columns - 1, Mathf.FloorToInt((source.x + radius - MinX) / CellSize));
                var minRow = Mathf.Max(0, Mathf.FloorToInt((source.z - radius - MinZ) / CellSize));
                var maxRow = Mathf.Min(Rows - 1, Mathf.FloorToInt((source.z + radius - MinZ) / CellSize));
                for (var row = minRow; row <= maxRow; row++)
                for (var column = minColumn; column <= maxColumn; column++)
                {
                    var center = CellCenter(column, row);
                    var deltaX = center.x - source.x;
                    var deltaZ = center.z - source.z;
                    if (deltaX * deltaX + deltaZ * deltaZ > radiusSquared) continue;
                    var index = Index(column, row);
                    visible[index] = true;
                    explored[index] = true;
                }
            }
        }

        public FogState StateAt(Vector3 position)
        {
            if (!TryCell(position, out var column, out var row)) return FogState.Unexplored;
            var index = Index(column, row);
            return visible[index] ? FogState.Visible : explored[index] ? FogState.Explored : FogState.Unexplored;
        }

        public FogState StateAt(int column, int row)
        {
            var index = Index(column, row);
            return visible[index] ? FogState.Visible : explored[index] ? FogState.Explored : FogState.Unexplored;
        }

        public Vector3 CellCenter(int column, int row) => new(
            MinX + (column + 0.5f) * CellSize,
            0f,
            MinZ + (row + 0.5f) * CellSize);

        public Vector2 WorldToUv(Vector3 position) => new(
            Mathf.InverseLerp(MinX, MaxX, position.x),
            Mathf.InverseLerp(MinZ, MaxZ, position.z));

        public Vector3 UvToWorld(Vector2 uv) => new(
            Mathf.Lerp(MinX, MaxX, Mathf.Clamp01(uv.x)),
            0f,
            Mathf.Lerp(MinZ, MaxZ, Mathf.Clamp01(uv.y)));

        private bool TryCell(Vector3 position, out int column, out int row)
        {
            column = Mathf.FloorToInt((position.x - MinX) / CellSize);
            row = Mathf.FloorToInt((position.z - MinZ) / CellSize);
            return column >= 0 && column < Columns && row >= 0 && row < Rows;
        }

        private int Index(int column, int row)
        {
            if (column < 0 || column >= Columns || row < 0 || row >= Rows)
                throw new ArgumentOutOfRangeException();
            return row * Columns + column;
        }
    }
}
