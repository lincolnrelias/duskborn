using UnityEngine;

namespace Duskborn.UI
{
    public static class WorldMapProjection
    {
        // Terrain chunks start at -chunks/2 (integer division), including even grids.
        public static Rect TerrainBounds(LowPolyTerrainConfig config)
        {
            float chunk = config.chunkSize * config.cellSize;
            return new Rect(-(config.chunksX / 2) * chunk, -(config.chunksZ / 2) * chunk,
                config.chunksX * chunk, config.chunksZ * chunk);
        }

        public static Vector2 Project(Vector3 position, Vector2 center, float radius) =>
            (new Vector2(position.x, position.z) - center) / Mathf.Max(1f, radius);

        public static Vector2 ClampMarker(Vector2 point, float margin = .92f) =>
            Vector2.ClampMagnitude(point, margin);
    }
}
