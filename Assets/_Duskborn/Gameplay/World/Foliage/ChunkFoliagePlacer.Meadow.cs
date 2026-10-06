using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Duskborn.Gameplay.World.Foliage
{
    public partial class ChunkFoliagePlacer
    {
        // Small tiles allow normal frustum culling without one GameObject per blade.
        private const float MeadowTileSize = 8f;
        private readonly List<Vector3> meadowVertices = new List<Vector3>();
        private readonly List<Vector3> meadowNormals = new List<Vector3>();
        private readonly List<Vector2> meadowUVs = new List<Vector2>();
        private readonly List<Vector4> meadowRoots = new List<Vector4>();
        private readonly List<Color> meadowColors = new List<Color>();
        private readonly List<int> meadowTriangles = new List<int>();
        // Macro density and hue vary over tens of meters; a world-aligned 1 m lattice is sufficient.
        // Reused per tile, with identical samples on both sides of a chunk/tile boundary.
        private readonly float[] meadowPatchLattice = new float[100];
        private readonly Color[] meadowTipLattice = new Color[100];

        private IEnumerator BuildContinuousMeadow(LowPolyTerrainConfig config, int seed,
            float minX, float maxX, float minZ, float maxZ, float halfMapX, float halfMapZ,
            float centerRadiusSqr, SpatialOccupancyMap occupancy, GenerationBudget budget)
        {
            float area = (maxX - minX) * (maxZ - minZ);
            float bladesPerSquareMeter = Mathf.Clamp(grassTuftsPerChunk / 1800f * meadowBladesPerSquareMeter, 1f, 64f);
            int bladeBudget = Mathf.Max(1, Mathf.RoundToInt(area * bladesPerSquareMeter));
            if (!Application.isPlaying) bladeBudget = Mathf.Min(bladeBudget, 90000);
            float spacing = Mathf.Sqrt((maxX - minX) * (maxZ - minZ) / bladeBudget);
            int size = config.chunkSize;
            float[,] heights;
            bool[,] sampled = null;
            if (_terrainChunk == null || !_terrainChunk.TryGetFoliageHeightMap(config, seed, out heights))
            {
                // Saved terrain has no runtime height cache. Sample only tiles the camera actually needs.
                heights = new float[size + 1, size + 1];
                sampled = new bool[size + 1, size + 1];
            }

            var holder = new GameObject("Foliage_MeadowBatch");
            holder.transform.SetParent(transform, false);
            int tilesX = Mathf.CeilToInt((maxX - minX) / MeadowTileSize);
            int tilesZ = Mathf.CeilToInt((maxZ - minZ) / MeadowTileSize);
            if (Application.isPlaying)
            {
                var streamingBudget = MeadowTileStreamer.BuildBudget;
                holder.AddComponent<MeadowTileStreamer>().Initialize(tilesX, tilesZ, minX, minZ, MeadowTileSize,
                    (tx, tz) => BuildMeadowTile(tx, tz, holder, spacing, heights, size, config, seed,
                        minX, maxX, minZ, maxZ, centerRadiusSqr, occupancy, streamingBudget, sampled));
                yield break;
            }
            for (int tz = 0; tz < tilesZ; tz++)
            for (int tx = 0; tx < tilesX; tx++)
            {
                var tile = BuildMeadowTile(tx, tz, holder, spacing, heights, size, config, seed,
                    minX, maxX, minZ, maxZ, centerRadiusSqr, occupancy, budget, sampled);
                while (tile.MoveNext()) yield return tile.Current;
            }
        }

        private IEnumerator BuildMeadowTile(int tx, int tz, GameObject holder, float spacing, float[,] heights,
            int size, LowPolyTerrainConfig config, int seed, float minX, float maxX, float minZ, float maxZ,
            float centerRadiusSqr, SpatialOccupancyMap occupancy, GenerationBudget budget, bool[,] sampled)
        {
            // Runtime tiles share the coordinator's budget, including work on earlier tiles this frame.
            if (!Application.isPlaying) budget?.Reset();
            // Reuse tile buffers while streaming; five vertices and three opaque triangles per blade.
            var verts = meadowVertices; var normals = meadowNormals; var uvs = meadowUVs;
            var roots = meadowRoots; var colors = meadowColors; var tris = meadowTriangles;
            verts.Clear(); normals.Clear(); uvs.Clear(); roots.Clear(); colors.Clear(); tris.Clear();
            float left = minX + tx * MeadowTileSize;
            float right = Mathf.Min(maxX, left + MeadowTileSize);
            float bottom = minZ + tz * MeadowTileSize;
            float top = Mathf.Min(maxZ, bottom + MeadowTileSize);
            if (sampled != null)
            {
                int x0 = Mathf.Clamp(Mathf.FloorToInt((left - minX) / config.cellSize), 0, size);
                int z0 = Mathf.Clamp(Mathf.FloorToInt((bottom - minZ) / config.cellSize), 0, size);
                int x1 = Mathf.Clamp(Mathf.CeilToInt((right - minX) / config.cellSize), 0, size);
                int z1 = Mathf.Clamp(Mathf.CeilToInt((top - minZ) / config.cellSize), 0, size);
                float halfX = config.chunksX * size * config.cellSize * 0.5f;
                float halfZ = config.chunksZ * size * config.cellSize * 0.5f;
                for (int z = z0; z <= z1; z++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        if (sampled[x, z]) continue;
                        heights[x, z] = TerrainNoise.SampleHeight(minX + x * config.cellSize, minZ + z * config.cellSize, config, seed, halfX, halfZ);
                        sampled[x, z] = true;
                    }
                    if (budget != null && budget.ShouldYield()) { yield return null; budget.Reset(); }
                }
            }
            Matrix4x4 worldToLocal = transform.worldToLocalMatrix;
            Quaternion normalRotation = Quaternion.Inverse(transform.rotation);
            int latticeX = Mathf.FloorToInt(left), latticeZ = Mathf.FloorToInt(bottom);
            int latticeWidth = Mathf.CeilToInt(right) - latticeX + 1;
            int latticeDepth = Mathf.CeilToInt(top) - latticeZ + 1;
            for (int z = 0; z < latticeDepth; z++)
            for (int x = 0; x < latticeWidth; x++)
            {
                int index = z * latticeWidth + x;
                meadowPatchLattice[index] = EvaluateMeadowPatch(latticeX + x, latticeZ + z, seed);
                meadowTipLattice[index] = EvaluateFoliageTipColor(latticeX + x, latticeZ + z, 0f, config);
            }


            // Global cells and cell hashes preserve placement across tile/chunk seams and generation order.
            for (int gz = Mathf.FloorToInt(bottom / spacing); gz < Mathf.CeilToInt(top / spacing); gz++)
            {
                for (int gx = Mathf.FloorToInt(left / spacing); gx < Mathf.CeilToInt(right / spacing); gx++)
                {
                    float x = (gx + 0.5f + (MeadowGrassGeometry.Hash(gx, gz, seed, 0) - 0.5f) * 0.84f) * spacing;
                    float z = (gz + 0.5f + (MeadowGrassGeometry.Hash(gx, gz, seed, 1) - 0.5f) * 0.84f) * spacing;
                    if (x < left || x >= right || z < bottom || z >= top) continue;
                    if (x * x + z * z < centerRadiusSqr * 0.75f) continue;
                    Vector2 point = new Vector2(x, z);
                    if (occupancy != null && occupancy.IsSolidOccupied(point, 0.24f)) continue;

                    float clearingDensity = 1f;
                    float clearingHeight = 1f;
                    float canopy = 0f;
                    if (occupancy != null)
                    {
                        if (occupancy.IsInClearing(point, out OccupancyType type))
                        {
                            clearingDensity = type == OccupancyType.Player_Sanctuary ? 0.15f : 0.40f;
                            clearingHeight = type == OccupancyType.Player_Sanctuary ? 0.40f : 0.55f;
                        }
                        occupancy.IsUnderCanopy(point, out canopy);
                    }

                    int cx = Mathf.Clamp(Mathf.FloorToInt((x - minX) / config.cellSize), 0, size - 1);
                    int cz = Mathf.Clamp(Mathf.FloorToInt((z - minZ) / config.cellSize), 0, size - 1);
                    float u = (x - minX) / config.cellSize - cx;
                    float v = (z - minZ) / config.cellSize - cz;
                    float ground = MeadowGrassGeometry.SampleTriangle(heights[cx, cz], heights[cx + 1, cz],
                        heights[cx, cz + 1], heights[cx + 1, cz + 1], u, v, config.cellSize, out Vector3 normal);
                    float slope = Vector3.Angle(normal, Vector3.up);
                    float minHeight = config.waterLevel + waterClearance;
                    if (ground <= minHeight || ground >= config.heightMultiplier * 0.72f ||
                        slope >= config.steepSlopeThreshold * 0.85f) continue;

                    float slopeCoverage = 1f - Mathf.InverseLerp(14f, config.steepSlopeThreshold * 0.85f, slope);
                    float shoreline = Mathf.InverseLerp(minHeight, minHeight + 0.8f, ground);
                    float elevation = 1f - Mathf.InverseLerp(config.heightMultiplier * 0.55f, config.heightMultiplier * 0.72f, ground);
                    int lx = Mathf.FloorToInt(x) - latticeX, lz = Mathf.FloorToInt(z) - latticeZ;
                    int li = lz * latticeWidth + lx;
                    float fx = x - Mathf.Floor(x), fz = z - Mathf.Floor(z);
                    float patch = Mathf.Lerp(Mathf.Lerp(meadowPatchLattice[li], meadowPatchLattice[li + 1], fx),
                        Mathf.Lerp(meadowPatchLattice[li + latticeWidth], meadowPatchLattice[li + latticeWidth + 1], fx), fz);
                    float meadow = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(macroDensityThreshold - 0.12f,
                        macroDensityThreshold + 0.08f, patch));
                    float density = meadow * slopeCoverage * shoreline * elevation * clearingDensity;
                    if (MeadowGrassGeometry.Hash(gx, gz, seed, 2) >= density) continue;

                    // Blend from the actual rendered triangle's biome color, including facet AO.
                    float faceHeight = u + v <= 1f
                        ? (heights[cx, cz] + heights[cx + 1, cz] + heights[cx, cz + 1]) / 3f
                        : (heights[cx + 1, cz] + heights[cx, cz + 1] + heights[cx + 1, cz + 1]) / 3f;
                    Color rootColor = EvaluateTerrainColor(faceHeight, slope, normal, config);
                    rootColor *= 1f - Mathf.Clamp01(slope / 90f) * config.facetAOIntensity;
                    Color fieldTip = Color.Lerp(Color.Lerp(meadowTipLattice[li], meadowTipLattice[li + 1], fx),
                        Color.Lerp(meadowTipLattice[li + latticeWidth], meadowTipLattice[li + latticeWidth + 1], fx), fz);
                    if (canopy > 0.08f) fieldTip = Color.Lerp(fieldTip, new Color(0.18f, 0.52f, 0.22f), canopy * 0.65f);
                    Color tipColor = Color.Lerp(rootColor, fieldTip, 0.65f);
                    float height = meadowHeight * clearingHeight * Mathf.Lerp(0.65f, 1f, meadow) *
                        Mathf.Lerp(0.86f, 1.14f, MeadowGrassGeometry.Hash(gx, gz, seed, 3));
                    float width = Mathf.Clamp(spacing * 0.85f, 0.12f, 0.26f);
                    float angle = MeadowGrassGeometry.Hash(gx, gz, seed, 4) * Mathf.PI * 2f;
                    Vector3 side = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    Vector3 lean = new Vector3(-side.z, 0f, side.x) * height * 0.24f;
                    Vector3 rootWS = new Vector3(x, ground - 0.015f, z);
                    Vector3 rootOS = worldToLocal.MultiplyPoint3x4(rootWS);
                    Vector3 bladeNormal = (normalRotation * Vector3.Lerp(normal, Vector3.up, 0.75f)).normalized;
                    int first = verts.Count;
                    for (int vertex = 0; vertex < 5; vertex++)
                    {
                        float t = vertex < 2 ? 0f : vertex < 4 ? 0.5f : 1f;
                        float lateral = vertex == 4 ? 0f : (vertex % 2 == 0 ? -0.5f : 0.5f) * width * (1f - t * 0.65f);
                        Vector3 offset = side * lateral;
                        // Place the complete blade base on its terrain plane, then curve upright.
                        offset.y = -(normal.x * offset.x + normal.z * offset.z) / Mathf.Max(normal.y, 0.1f);
                        Vector3 position = rootWS + offset + Vector3.up * (height * t) + lean * (t * t);
                        verts.Add(worldToLocal.MultiplyPoint3x4(position));
                        normals.Add(bladeNormal);
                        uvs.Add(new Vector2(vertex == 4 ? 0.5f : vertex % 2, t));
                        roots.Add(new Vector4(rootOS.x, rootOS.y, rootOS.z, 1f));
                        Color color = Color.Lerp(rootColor, tipColor, t);
                        color.a = t * t;
                        colors.Add(color);
                    }
                    tris.Add(first); tris.Add(first + 2); tris.Add(first + 1);
                    tris.Add(first + 1); tris.Add(first + 2); tris.Add(first + 3);
                    tris.Add(first + 2); tris.Add(first + 4); tris.Add(first + 3);
                }
                if (budget != null && budget.ShouldYield()) { yield return null; budget.Reset(); }
            }
            if (verts.Count > 0)
            {
                CreateBatchGameObject($"Meadow_{tx}_{tz}", verts, normals, uvs, colors, tris, GetMeadowMaterial(), roots);
                Transform tile = transform.Find($"Meadow_{tx}_{tz}");
                Mesh mesh = tile.GetComponent<MeshFilter>().sharedMesh;
                tile.SetParent(holder.transform, false);
                if (Application.isPlaying) ownedBatchMeshes.Remove(mesh);
                // Dense ground cover receives tree shadows; sparse accents still cast shadows.
                tile.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                if (Application.isPlaying) mesh.UploadMeshData(true);
            }
        }

        private Material meadowMaterial;
        private Material meadowSourceMaterial;

        private Material GetMeadowMaterial()
        {
            if (meadowMaterial != null && meadowSourceMaterial == grassMaterial) return meadowMaterial;
            if (meadowMaterial != null)
            {
                if (Application.isPlaying) Destroy(meadowMaterial);
                else DestroyImmediate(meadowMaterial);
            }
            meadowSourceMaterial = grassMaterial;
            meadowMaterial = new Material(grassMaterial) { name = grassMaterial.name + " (Meadow)" };
            meadowMaterial.SetFloat("_MeadowEnabled", 1f);
            return meadowMaterial;
        }

        private void ReleaseBatchMeshes()
        {
            foreach (Mesh mesh in ownedBatchMeshes)
            {
                if (mesh == null) continue;
                if (Application.isPlaying) Destroy(mesh);
                else DestroyImmediate(mesh);
            }
            ownedBatchMeshes.Clear();
            templateData.Clear();
            // Explicit clear also runs in edit-mode CLI fixtures, where Unity may not invoke OnDestroy.
            if (meadowMaterial != null)
            {
                if (Application.isPlaying) Destroy(meadowMaterial);
                else DestroyImmediate(meadowMaterial);
            }
            meadowMaterial = null;
            meadowSourceMaterial = null;
        }

        private void TrackTemplateMeshes(params Mesh[] meshes) => ownedBatchMeshes.AddRange(meshes);

        private void ReleaseTemplateMeshes(params Mesh[] meshes)
        {
            foreach (Mesh mesh in meshes)
            {
                if (mesh == null) continue;
                ownedBatchMeshes.Remove(mesh);
                templateData.Remove(mesh);
                if (Application.isPlaying) Destroy(mesh);
                else DestroyImmediate(mesh);
            }
        }

        private void OnDestroy()
        {
            ReleaseBatchMeshes();
            if (Application.isPlaying)
            {
                if (fallbackGrassMaterial != null) Destroy(fallbackGrassMaterial);
                if (fallbackBushMaterial != null) Destroy(fallbackBushMaterial);
            }
            else
            {
                if (fallbackGrassMaterial != null) DestroyImmediate(fallbackGrassMaterial);
                if (fallbackBushMaterial != null) DestroyImmediate(fallbackBushMaterial);
            }
        }
    }

    public static class MeadowGrassGeometry
    {
        // Local hashing never consumes gameplay RNG or Unity's global Random state.
        public static float Hash(int x, int z, int seed, int channel)
        {
            unchecked
            {
                uint h = (uint)x * 73856093u ^ (uint)z * 19349663u ^ (uint)seed * 83492791u ^ (uint)channel * 2654435761u;
                h ^= h >> 16; h *= 2246822519u; h ^= h >> 13; h *= 3266489917u; h ^= h >> 16;
                return (h & 0x00ffffffu) / 16777216f;
            }
        }

        // Match TerrainChunk's diagonal exactly; bilinear/noise sampling can float above its flat faces.
        public static float SampleTriangle(float h00, float h10, float h01, float h11,
            float u, float v, float cellSize, out Vector3 normal)
        {
            float dx, dz, height;
            if (u + v <= 1f)
            {
                dx = h10 - h00; dz = h01 - h00;
                height = h00 + dx * u + dz * v;
            }
            else
            {
                dx = h11 - h01; dz = h11 - h10;
                height = h11 + (h01 - h11) * (1f - u) + (h10 - h11) * (1f - v);
            }
            normal = new Vector3(-dx / cellSize, 1f, -dz / cellSize).normalized;
            return height;
        }
    }
}
