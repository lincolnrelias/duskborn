using System;
using System.Collections.Generic;
using UnityEngine;

namespace Duskborn.Gameplay.World
{
    /// <summary>
    /// Tipos de ocupação espacial no mapa do mundo.
    /// </summary>
    public enum OccupancyType
    {
        Resource_Solid,   // Obstáculo físico sólido (tronco de árvore, rocha, baú, bancada)
        Resource_Canopy,  // Área sombreada/influência da copa da árvore (sem bloqueio físico, estimula folhagem)
        Combat_Clearing,  // Clareira aberta preservada para combate e movimentação
        Player_Sanctuary  // Santuário central inicial do jogador
    }

    /// <summary>
    /// Registro de ocupação de um nó ou área no plano XZ.
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
    /// Coordenador Espacial Compartilhado (Spatial Occupancy & Clustering Coordinator).
    /// Indexa obstáculos físicos sólidos, copas de árvores e clareiras preservadas
    /// usando uma grade espacial 2D rápida em memória para consultas O(1).
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
        /// Registra um objeto ou recurso no mapa de ocupação.
        /// </summary>
        public void Register(Vector3 worldPos, float solidRadius, float canopyRadius = 0f, OccupancyType type = OccupancyType.Resource_Solid)
        {
            Register(new Vector2(worldPos.x, worldPos.z), solidRadius, canopyRadius, type);
        }

        /// <summary>
        /// Registra um objeto ou recurso no mapa de ocupação 2D.
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
        /// Registra uma clareira ou zona protegida.
        /// </summary>
        public void RegisterClearing(Vector2 centerXZ, float radius, OccupancyType type = OccupancyType.Combat_Clearing)
        {
            Register(centerXZ, radius, 0f, type);
        }

        /// <summary>
        /// Retorna verdadeiro se o ponto fornecido estiver dentro do raio sólido de qualquer recurso/obstáculo
        /// acrescido da margem de clearance especificada.
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
        /// Retorna se o ponto fornecido está sob a copa de uma ou mais árvores e o peso de sombreamento (0 a 1).
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
        /// Retorna verdadeiro se o ponto estiver contido em uma clareira protegida (Santuário ou Clareira de Combate).
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
        /// Retorna verdadeiro se o ponto estiver contido em qualquer clareira protegida.
        /// </summary>
        public bool IsInClearing(Vector2 xz)
        {
            return IsInClearing(xz, out _);
        }

        /// <summary>
        /// Valida se um novo prop pode ser posicionado mantendo distância dos demais nós sólidos e das clareiras.
        /// </summary>
        public bool CanPlaceProp(Vector2 xz, float solidRadius, float requiredSpacing)
        {
            if (IsInClearing(xz))
                return false;

            return !IsSolidOccupied(xz, requiredSpacing);
        }
    }
}
