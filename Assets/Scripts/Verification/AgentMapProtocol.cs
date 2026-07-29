using System;
using System.Text;

namespace AshesOfRum
{
    [Serializable]
    public sealed class AgentMapState
    {
        public float minX;
        public float maxX;
        public float minZ;
        public float maxZ;
        public float cellSize;
        public int columns;
        public int rows;
        public string fogEncoding;
        public string fogRle;
    }

    public sealed partial class AgentStateProjector
    {
        private AgentMapState ProjectMap()
        {
            var map = economy.FogOfWar.Map;
            var rle = new StringBuilder(map.Columns * map.Rows / 2);
            var previous = FogCode(map.StateAt(0, 0));
            var count = 0;
            for (var row = 0; row < map.Rows; row++)
            for (var column = 0; column < map.Columns; column++)
            {
                var current = FogCode(map.StateAt(column, row));
                if (current == previous)
                {
                    count++;
                    continue;
                }
                rle.Append(count).Append(previous);
                previous = current;
                count = 1;
            }
            rle.Append(count).Append(previous);
            return new AgentMapState
            {
                minX = map.MinX,
                maxX = map.MaxX,
                minZ = map.MinZ,
                maxZ = map.MaxZ,
                cellSize = map.CellSize,
                columns = map.Columns,
                rows = map.Rows,
                fogEncoding = "row-major-count-state-U-unexplored-E-explored-V-visible",
                fogRle = rle.ToString()
            };
        }

        private static char FogCode(FogState state) => state switch
        {
            FogState.Visible => 'V',
            FogState.Explored => 'E',
            _ => 'U'
        };
    }
}
