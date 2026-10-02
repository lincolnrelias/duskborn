using System;
using System.Collections.Generic;
using UnityEngine;

namespace Duskborn.Gameplay.World
{
    /// <summary>
    /// Spatial occupancy types on the world map.
    /// </summary>
    public enum OccupancyType
    {
        Resource_Solid,   // Solid physical obstacle (tree trunk, rock, chest, workbench).
        Resource_Canopy,  // Tree canopy shade / influence area (no physical blocking, encourages foliage).
        Combat_Clearing,  // Open clearing preserved for combat and movement.
        Player_Sanctuary  // Player's starting central sanctuary.
    }

    /// <summary>
    /// Node or area occupancy record on the XZ plane.
    /// </summary>
    [Serializable]
    public struct OccupancyEntry
    {
        public Vector2 positionXZ;
        public float solidRadius;
        public float canopyRadius;
        public OccupancyType type;

        public OccupancyEntry(Vector2 pos, float solid, float canopy, OccupancyType type)
        {
            this.positionXZ = pos;
            this.solidRadius = solid;
            this.canopyRadius = canopy;
            this.type = type;
        }
    }

    /// <summary>
    /// Spatial Coordinator (Spatial Occupancy & Clustering Coordinator).
    /// Index solid physical obstacles, tree canopies, and preserved clearings
    /// using a fast in-memory 2D spatial grid for O(1) queries.
    /// </summary>
    public class SpatialOccupancyMap
    {
        private const float DefaultCellSize = 6.0f;
        private readonly float _cellSize;
        private readonly List<OccupancyEntry> _entries = new List<OccupancyEntry>();
        private readonly Dictionary<Vector2Int, List<int>> _grid = new Dictionary<Vector2Int, List<int>>();
        private readonly List<int> _clearings = new List<int>();

        public IReadOnlyList<OccupancyEntry> Entries => _entries;
        public int Count => _entries.Count;

        public SpatialOccupancyMap(float cellSize = DefaultCellSize)
        {
            _cellSize = Mathf.Max(1.0f, cellSize);
        }

        public void Clear()
        {
            _entries.Clear();
            _grid.Clear();
            _clearings.Clear();
        }

        private Vector2Int WorldToCell(float x, float z)
        {
            return new Vector2Int(Mathf.FloorToInt(x / _cellSize), Mathf.FloorToInt(z / _cellSize));
        }

        /// <summary>
        /// Register an object or resource in the occupancy map.
        /// </summary>
        public void Register(Vector3 worldPos, float solidRadius, float canopyRadius = 0f, OccupancyType type = OccupancyType.Resource_Solid)
        {
            Register(new Vector2(worldPos.x, worldPos.z), solidRadius, canopyRadius, type);
        }

        /// <summary>
        /// Register an object or resource in the 2D occupancy map.
        /// </summary>
        public void Register(Vector2 positionXZ, float solidRadius, float canopyRadius = 0f, OccupancyType type = OccupancyType.Resource_Solid)
        {
            int index = _entries.Count;
            OccupancyEntry entry = new OccupancyEntry(positionXZ, solidRadius, canopyRadius, type);
            _entries.Add(entry);

            if (type == OccupancyType.Combat_Clearing || type == OccupancyType.Player_Sanctuary)
            {
                _clearings.Add(index);
            }

            float maxRadius = Mathf.Max(solidRadius, canopyRadius);
            if (maxRadius <= 0.001f) maxRadius = 0.5f;

            Vector2Int minCell = WorldToCell(positionXZ.x - maxRadius, positionXZ.y - maxRadius);
            Vector2Int maxCell = WorldToCell(positionXZ.x + maxRadius, positionXZ.y + maxRadius);

            for (int cz = minCell.y; cz <= maxCell.y; cz++)
            {
                for (int cx = minCell.x; cx <= maxCell.x; cx++)
                {
                    Vector2Int cellCoord = new Vector2Int(cx, cz);
                    if (!_grid.TryGetValue(cellCoord, out var list))
                    {
                        list = new List<int>(4);
                        _grid[cellCoord] = list;
                    }
                    list.Add(index);
                }
            }
        }

        /// <summary>
        /// Register a clearing or protected zone.
        /// </summary>
        public void RegisterClearing(Vector2 centerXZ, float radius, OccupancyType type = OccupancyType.Combat_Clearing)
        {
            Register(centerXZ, radius, 0f, type);
        }

        /// <summary>
        /// Return true if the supplied point is within the solid radius of any resource / obstacle.
        /// plus the specified clearance margin.
        /// </summary>
        public bool IsSolidOccupied(Vector2 xz, float clearanceRadius = 0f)
        {
            Vector2Int minCell = WorldToCell(xz.x - clearanceRadius - 3.0f, xz.y - clearanceRadius - 3.0f);
            Vector2Int maxCell = WorldToCell(xz.x + clearanceRadius + 3.0f, xz.y + clearanceRadius + 3.0f);

            for (int cz = minCell.y; cz <= maxCell.y; cz++)
            {
                for (int cx = minCell.x; cx <= maxCell.x; cx++)
                {
                    if (!_grid.TryGetValue(new Vector2Int(cx, cz), out var list))
                        continue;

                    for (int i = 0; i < list.Count; i++)
                    {
                        OccupancyEntry entry = _entries[list[i]];
                        if (entry.type != OccupancyType.Resource_Solid)
                            continue;

                        float allowedDist = entry.solidRadius + clearanceRadius;
                        float dx = entry.positionXZ.x - xz.x;
                        float dz = entry.positionXZ.y - xz.y;
                        if (dx * dx + dz * dz < allowedDist * allowedDist)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Return whether the supplied point is under one or more tree canopies and its shade weight (0 to 1).
        /// </summary>
        public bool IsUnderCanopy(Vector2 xz, out float canopyWeight)
        {
            canopyWeight = 0f;
            Vector2Int minCell = WorldToCell(xz.x - 5.0f, xz.y - 5.0f);
            Vector2Int maxCell = WorldToCell(xz.x + 5.0f, xz.y + 5.0f);

            bool under = false;

            for (int cz = minCell.y; cz <= maxCell.y; cz++)
            {
                for (int cx = minCell.x; cx <= maxCell.x; cx++)
                {
                    if (!_grid.TryGetValue(new Vector2Int(cx, cz), out var list))
                        continue;

                    for (int i = 0; i < list.Count; i++)
                    {
                        OccupancyEntry entry = _entries[list[i]];
                        if (entry.canopyRadius <= 0.01f)
                            continue;

                        float dx = entry.positionXZ.x - xz.x;
                        float dz = entry.positionXZ.y - xz.y;
                        float distSq = dx * dx + dz * dz;
                        float rSq = entry.canopyRadius * entry.canopyRadius;

                        if (distSq < rSq)
                        {
                            under = true;
                            float dist = Mathf.Sqrt(distSq);
                            float w = Mathf.Clamp01(1f - (dist / entry.canopyRadius));
                            if (w > canopyWeight) canopyWeight = w;
                        }
                    }
                }
            }

            return under;
        }

        /// <summary>
        /// Return true if the point is inside a protected clearing (Sanctuary or Combat Clearing).
        /// </summary>
        public bool IsInClearing(Vector2 xz, out OccupancyType clearingType)
        {
            clearingType = OccupancyType.Combat_Clearing;

            for (int i = 0; i < _clearings.Count; i++)
            {
                OccupancyEntry cl = _entries[_clearings[i]];
                float dx = cl.positionXZ.x - xz.x;
                float dz = cl.positionXZ.y - xz.y;
                if (dx * dx + dz * dz < cl.solidRadius * cl.solidRadius)
                {
                    clearingType = cl.type;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Return true if the point is inside any protected clearing.
        /// </summary>
        public bool IsInClearing(Vector2 xz)
        {
            return IsInClearing(xz, out _);
        }

        /// <summary>
        /// Validate whether a new prop can be placed while maintaining distance from other solid nodes and clearings.
        /// </summary>
        public bool CanPlaceProp(Vector2 xz, float solidRadius, float requiredSpacing)
        {
            if (IsInClearing(xz))
                return false;

            return !IsSolidOccupied(xz, requiredSpacing);
        }
    }
}
