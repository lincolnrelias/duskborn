using System.Collections.Generic;
using UnityEngine;
using Duskborn.Core;
using Duskborn.Gameplay.World;
using Unity.AI.Navigation;

namespace Duskborn.Gameplay.World.Foliage
{
    /// <summary>
    /// Procedural vegetation density preset levels.
    /// </summary>
    public enum FoliageDensityPreset
    {
        Custom,
        Low,           // 400 grass tufts, 12 shrubs.
        Medium,        // 950 grass tufts, 20 shrubs.
        High,          // 1800 grass tufts, 32 shrubs.
        Ultra_Genshin, // 3200 grass tufts, 48 shrubs.
        Cinematic_Lush // 5000 grass tufts, 64 shrubs.
    }

    /// <summary>
    /// High-density procedural foliage manager per chunk (Genshin Impact / Zelda: BotW style).
    /// Generate grass tufts, dense lush clumps, wildflowers, and shrubs / ferns while respecting
    /// terrain topography, macro density waves (meadow fields), physical occupancy maps (SpatialOccupancyMap),
    /// and combat clearings, consolidating all geometry into unified meshes per chunk (1 draw call for all grass,
    /// 1 draw call for shrubs, executed entirely on the GPU with shadows and wind animation support).
    /// </summary>
    [RequireComponent(typeof(TerrainChunk))]
    public class ChunkFoliagePlacer : MonoBehaviour
    {
        [Header("Foliage Materials")]
        [Tooltip("Grass tuft and flower material (Duskborn/StylizedFoliage shader).")]
        [SerializeField] private Material grassMaterial;

        [Tooltip("Stylized shrub material (Duskborn/StylizedFoliage shader).")]
        [SerializeField] private Material bushMaterial;

        [Header("Density Configuration (Genshin / Zelda Scale)")]
        [Tooltip("Preset vegetation density profile.")]
        [SerializeField] private FoliageDensityPreset densityPreset = FoliageDensityPreset.High;

        [Tooltip("Number of grass tufts generated per chunk.")]
        [Range(0, 6000)]
        [SerializeField] private int grassTuftsPerChunk = 1800;

        [Tooltip("Number of small rocks and pebbles generated per chunk.")]
        [Range(0, 500)]
        [SerializeField] private int littleRocksPerChunk = 85;

        [Tooltip("Number of shrubs generated per chunk (disabled by default).")]
        [Range(0, 100)]
        [SerializeField] private int bushesPerChunk = 0;

        [Header("Grass Archetype Distribution")]
        [Tooltip("Proportion of dense carpet grass for continuous ground coverage.")]
        [Range(0f, 1f)]
        [SerializeField] private float carpetGrassRatio = 0.32f;

        [Tooltip("Proportion of tall, lush anime grass clumps.")]
        [Range(0f, 1f)]
        [SerializeField] private float lushGrassRatio = 0.22f;

        [Tooltip("Proportion of wildflower tufts in colonies.")]
        [Range(0f, 1f)]
        [SerializeField] private float wildflowerRatio = 0.14f;

        [Tooltip("Proportion of fine prairie grass at edges and transitions.")]
        [Range(0f, 1f)]
        [SerializeField] private float prairieGrassRatio = 0.12f;

        [Tooltip("Proportion of reed grass in wet depressions and banks.")]
        [Range(0f, 1f)]
        [SerializeField] private float reedGrassRatio = 0.05f;

        [Header("Wild Weed Distribution (Weeds & Undergrowth)")]
        [Tooltip("Proportion of wild weed tufts with basal rosettes and flower buds.")]
        [Range(0f, 1f)]
        [SerializeField] private float wildWeedRatio = 0.10f;

        [Tooltip("Proportion of shade-tolerant, creeping broadleaf weeds.")]
        [Range(0f, 1f)]
        [SerializeField] private float broadleafWeedRatio = 0.08f;

        [Tooltip("Proportion of tall wild stalk weeds with wind-swaying plumes.")]
        [Range(0f, 1f)]
        [SerializeField] private float tallStalkRatio = 0.07f;

        [Tooltip("Proportion of small clover patches.")]
        [Range(0f, 1f)]
        [SerializeField] private float cloverPatchRatio = 0.06f;

        [Header("Shoreline and Water Ecology (Water Spots)")]
        [Tooltip("Proportion of shoreline water cattail and aquatic reed beds.")]
        [Range(0f, 1f)]
        [SerializeField] private float cattailBedRatio = 0.08f;

        [Tooltip("Height band above water level considered the aquatic margin / shoreline.")]
        [Range(0.1f, 2.5f)]
        [SerializeField] private float waterMarginBand = 0.85f;

        [Header("Little Rock Distribution")]
        [Tooltip("Proportion of pebble clusters.")]
        [Range(0f, 1f)]
        [SerializeField] private float pebbleClusterRatio = 0.42f;

        [Tooltip("Proportion of flat, smooth river stones along banks.")]
        [Range(0f, 1f)]
        [SerializeField] private float riverStoneRatio = 0.28f;

        [Tooltip("Proportion of individual faceted pebbles.")]
        [Range(0f, 1f)]
        [SerializeField] private float singlePebbleRatio = 0.18f;

        [Tooltip("Proportion of chipped slope rocks (scree / talus).")]
        [Range(0f, 1f)]
        [SerializeField] private float screeRockRatio = 0.12f;

        [Header("Shrub Archetype Distribution")]
        [Tooltip("Proportion of ferns / fan-shaped shrubs.")]
        [Range(0f, 1f)]
        [SerializeField] private float fernBushRatio = 0.30f;

        [Tooltip("Proportion of flowering shrubs with colorful berries / buds.")]
        [Range(0f, 1f)]
        [SerializeField] private float floweringBushRatio = 0.28f;

        [Tooltip("Proportion of creeping ground-cover shrubs anchoring rocks.")]
        [Range(0f, 1f)]
        [SerializeField] private float groundShrubRatio = 0.22f;

        [Header("Field Waves & Terrain (Macro Ecology)")]
        [Tooltip("Spatial frequency of macro meadow density noise.")]
        [SerializeField] private float macroNoiseScale = 0.024f;

        [Tooltip("Noise threshold below which the meadow becomes an open clearing / soft trail.")]
        [SerializeField] private float macroDensityThreshold = 0.30f;

        [Tooltip("Minimum offset above water to allow vegetation.")]
        [SerializeField] private float waterClearance = 0.65f;

        private TerrainChunk _terrainChunk;

        // Stylized wildflower palette (Genshin / Zelda anime palette).
        public static readonly Color[] WildflowerPalettes = new Color[]
        {
            new Color(0.24f, 0.58f, 0.98f, 1f), // Bellflower blue / Cornflower Azure.
            new Color(0.98f, 0.84f, 0.14f, 1f), // Dandelion yellow / Dandelion Gold.
            new Color(0.94f, 0.26f, 0.35f, 1f), // Poppy Crimson
            new Color(0.95f, 0.96f, 0.92f, 1f), // Mountain Daisy White
            new Color(0.72f, 0.48f, 0.95f, 1f)  // Lavender Purple / Wild Lavender
        };

        // Botanical highlight palette (Cattails, Dandelions, Thistles).
        public static readonly Color CattailAccentColor = new Color(0.32f, 0.18f, 0.10f, 1.0f); // Brown stalk
        public static readonly Color DandelionAccentColor = new Color(0.96f, 0.82f, 0.14f, 1.0f); // Golden yellow.
        public static readonly Color ThistleAccentColor = new Color(0.78f, 0.52f, 0.88f, 1.0f); // Thistle lilac.

        public FoliageDensityPreset DensityPreset => densityPreset;
        public int GrassTuftsPerChunk => grassTuftsPerChunk;
        public int LittleRocksPerChunk => littleRocksPerChunk;
        public int BushesPerChunk => bushesPerChunk;
        public float CarpetGrassRatio => carpetGrassRatio;
        public float LushGrassRatio => lushGrassRatio;
        public float WildflowerRatio => wildflowerRatio;
        public float PrairieGrassRatio => prairieGrassRatio;
        public float ReedGrassRatio => reedGrassRatio;
        public float WildWeedRatio => wildWeedRatio;
        public float BroadleafWeedRatio => broadleafWeedRatio;
        public float TallStalkRatio => tallStalkRatio;
        public float CloverPatchRatio => cloverPatchRatio;
        public float CattailBedRatio => cattailBedRatio;
        public float WaterMarginBand => waterMarginBand;
        public float PebbleClusterRatio => pebbleClusterRatio;
        public float RiverStoneRatio => riverStoneRatio;
        public float SinglePebbleRatio => singlePebbleRatio;
        public float ScreeRockRatio => screeRockRatio;
        public float FernBushRatio => fernBushRatio;
        public float FloweringBushRatio => floweringBushRatio;
        public float GroundShrubRatio => groundShrubRatio;
        public float MacroNoiseScale => macroNoiseScale;
        public float MacroDensityThreshold => macroDensityThreshold;

        private void OnValidate()
        {
            if (densityPreset != FoliageDensityPreset.Custom)
            {
                ApplyDensityPreset();
            }
        }

        public void SetDensityPreset(FoliageDensityPreset preset)
        {
            densityPreset = preset;
            if (densityPreset != FoliageDensityPreset.Custom)
            {
                ApplyDensityPreset();
            }
        }

        public void SetFoliageCounts(int grassCount, int bushCount, int rockCount = -1)
        {
            densityPreset = FoliageDensityPreset.Custom;
            grassTuftsPerChunk = Mathf.Max(0, grassCount);
            bushesPerChunk = Mathf.Max(0, bushCount);
            if (rockCount >= 0) littleRocksPerChunk = Mathf.Max(0, rockCount);
        }

        public void ApplyDensityPreset()
        {
            switch (densityPreset)
            {
                case FoliageDensityPreset.Low:
                    grassTuftsPerChunk = 400;
                    littleRocksPerChunk = 20;
                    bushesPerChunk = 0;
                    break;
                case FoliageDensityPreset.Medium:
                    grassTuftsPerChunk = 950;
                    littleRocksPerChunk = 45;
                    bushesPerChunk = 0;
                    break;
                case FoliageDensityPreset.High:
                    grassTuftsPerChunk = 1800;
                    littleRocksPerChunk = 85;
                    bushesPerChunk = 0;
                    break;
                case FoliageDensityPreset.Ultra_Genshin:
                    grassTuftsPerChunk = 3200;
                    littleRocksPerChunk = 140;
                    bushesPerChunk = 0;
                    break;
                case FoliageDensityPreset.Cinematic_Lush:
                    grassTuftsPerChunk = 5000;
                    littleRocksPerChunk = 200;
                    bushesPerChunk = 0;
                    break;
            }
        }

        private void Awake()
        {
            _terrainChunk = GetComponent<TerrainChunk>();
            EnsureMaterials();
        }

        public void SetMaterials(Material grass, Material bush)
        {
            if (grass != null) grassMaterial = grass;
            if (bush != null) bushMaterial = bush;
        }

        public void EnsureMaterials()
        {
#if UNITY_EDITOR
            if (grassMaterial == null)
            {
                grassMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/_Duskborn/Art/Materials/M_Foliage_Grass.mat");
            }
            if (bushMaterial == null)
            {
                bushMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/_Duskborn/Art/Materials/M_Foliage_Bush.mat");
            }
#endif
            if (grassMaterial == null)
            {
                Shader s = Shader.Find("Duskborn/StylizedFoliage");
                if (s != null) grassMaterial = new Material(s);
            }
            if (bushMaterial == null)
            {
                Shader s = Shader.Find("Duskborn/StylizedFoliage");
                if (s != null) bushMaterial = new Material(s);
            }
        }

        public void ClearFoliage()
        {
            Transform existing = transform.Find("Foliage_GrassBatch");
            if (existing != null)
            {
                if (Application.isPlaying) Destroy(existing.gameObject);
                else DestroyImmediate(existing.gameObject);
            }

            Transform existingBushes = transform.Find("Foliage_BushBatch");
            if (existingBushes != null)
            {
                if (Application.isPlaying) Destroy(existingBushes.gameObject);
                else DestroyImmediate(existingBushes.gameObject);
            }
        }

        public void GenerateFoliage(LowPolyTerrainConfig config, int seed, SpatialOccupancyMap occupancyMap = null)
        {
            if (config == null) return;
            EnsureMaterials();
            ClearFoliage();

            if (densityPreset != FoliageDensityPreset.Custom)
            {
                ApplyDensityPreset();
            }

            if (_terrainChunk == null) _terrainChunk = GetComponent<TerrainChunk>();
            Vector2Int coord = _terrainChunk != null ? _terrainChunk.ChunkCoord : Vector2Int.zero;

            // Try resolving the occupancy map automatically if it was not injected.
            if (occupancyMap == null)
            {
                var gridMgr = GetComponentInParent<ChunkGridManager>();
                if (gridMgr == null) gridMgr = ChunkGridManager.Instance;
                if (gridMgr != null && gridMgr.propsPlacer != null)
                {
                    occupancyMap = gridMgr.propsPlacer.OccupancyMap;
                }
            }

            int chunkSeed = seed ^ (coord.x * 73856093) ^ (coord.y * 19349663);
            SeededRNG rng = new SeededRNG(chunkSeed);

            float chunkWorldLength = config.chunkSize * config.cellSize;
            float minX = coord.x * chunkWorldLength;
            float maxX = minX + chunkWorldLength;
            float minZ = coord.y * chunkWorldLength;
            float maxZ = minZ + chunkWorldLength;

            float halfMapX = (config.chunksX * config.chunkSize * config.cellSize) * 0.5f;
            float halfMapZ = (config.chunksZ * config.chunkSize * config.cellSize) * 0.5f;
            float centerRadiusSqr = (config.centralSanctuaryRadius * 0.9f) * (config.centralSanctuaryRadius * 0.9f);

            // 1. Generate consolidated grass and flower batch (1 draw call per chunk).
            BuildGrassBatch(config, seed, rng, minX, maxX, minZ, maxZ, halfMapX, halfMapZ, centerRadiusSqr, occupancyMap);

            // 2. Shrubs (disabled at the user's request due to visual artifacts).
            if (bushesPerChunk > 0)
            {
                BuildBushBatch(config, seed, rng, minX, maxX, minZ, maxZ, halfMapX, halfMapZ, centerRadiusSqr, occupancyMap);
            }
        }

        /// <summary>
        /// Asynchronous chunk foliage generation spread across multiple frames for consistent 60+ FPS.
        /// </summary>
        public System.Collections.IEnumerator GenerateFoliageAsync(
            LowPolyTerrainConfig config,
            int seed,
            SpatialOccupancyMap occupancyMap = null,
            GenerationBudget budget = null)
        {
            if (config == null) yield break;
            EnsureMaterials();
            ClearFoliage();

            if (densityPreset != FoliageDensityPreset.Custom)
            {
                ApplyDensityPreset();
            }

            if (_terrainChunk == null) _terrainChunk = GetComponent<TerrainChunk>();
            Vector2Int coord = _terrainChunk != null ? _terrainChunk.ChunkCoord : Vector2Int.zero;

            if (occupancyMap == null)
            {
                var gridMgr = GetComponentInParent<ChunkGridManager>();
                if (gridMgr == null) gridMgr = ChunkGridManager.Instance;
                if (gridMgr != null && gridMgr.propsPlacer != null)
                {
                    occupancyMap = gridMgr.propsPlacer.OccupancyMap;
                }
            }

            int chunkSeed = seed ^ (coord.x * 73856093) ^ (coord.y * 19349663);
            SeededRNG rng = new SeededRNG(chunkSeed);

            float chunkWorldLength = config.chunkSize * config.cellSize;
            float minX = coord.x * chunkWorldLength;
            float maxX = minX + chunkWorldLength;
            float minZ = coord.y * chunkWorldLength;
            float maxZ = minZ + chunkWorldLength;

            float halfMapX = (config.chunksX * config.chunkSize * config.cellSize) * 0.5f;
            float halfMapZ = (config.chunksZ * config.chunkSize * config.cellSize) * 0.5f;
            float centerRadiusSqr = (config.centralSanctuaryRadius * 0.9f) * (config.centralSanctuaryRadius * 0.9f);

            budget ??= new GenerationBudget(8f);

            // 1. Generate consolidated grass and flower batch asynchronously.
            yield return BuildGrassBatchRoutine(config, seed, rng, minX, maxX, minZ, maxZ, halfMapX, halfMapZ, centerRadiusSqr, occupancyMap, budget);

            // 2. Shrubs
            if (bushesPerChunk > 0)
            {
                BuildBushBatch(config, seed, rng, minX, maxX, minZ, maxZ, halfMapX, halfMapZ, centerRadiusSqr, occupancyMap);
                if (budget.ShouldYield()) yield return null;
            }
        }

        private float EvaluateMeadowPatch(float worldX, float worldZ, int activeSeed)
        {
            float n1 = Mathf.PerlinNoise((worldX + activeSeed * 19.31f) * macroNoiseScale, (worldZ + activeSeed * 37.73f) * macroNoiseScale);
            float warpX = Mathf.PerlinNoise((worldX + 124.7f) * 0.042f, (worldZ + 78.3f) * 0.042f) * 6f;
            float warpZ = Mathf.PerlinNoise((worldX - 89.2f) * 0.042f, (worldZ + 214.6f) * 0.042f) * 6f;
            float n2 = Mathf.PerlinNoise(((worldX + warpX) + activeSeed * 11.1f) * (macroNoiseScale * 1.75f), ((worldZ + warpZ) + activeSeed * 29.4f) * (macroNoiseScale * 1.75f));
            return n1 * 0.65f + n2 * 0.35f;
        }

        private float EvaluateWeedPatch(float worldX, float worldZ, int activeSeed)
        {
            float w1 = Mathf.PerlinNoise((worldX + activeSeed * 43.17f + 312.4f) * 0.035f, (worldZ + activeSeed * 59.83f + 149.2f) * 0.035f);
            float w2 = Mathf.PerlinNoise((worldX - 173.5f) * 0.075f, (worldZ + 381.1f) * 0.075f);
            return w1 * 0.70f + w2 * 0.30f;
        }

        private Color EvaluateRockColor(float worldX, float worldZ, float groundY, float slope, LowPolyTerrainConfig config)
        {
            float stoneNoise = Mathf.PerlinNoise((worldX + 283.1f) * 0.022f, (worldZ + 419.7f) * 0.022f);

            // 1. Water margin / beach pebbles.
            if (groundY <= config.waterLevel + waterMarginBand + 0.35f)
            {
                Color wetSlate = new Color(0.30f, 0.33f, 0.38f);
                Color smoothGray = new Color(0.44f, 0.46f, 0.48f);
                Color sandPebble = new Color(0.56f, 0.52f, 0.44f);

                if (stoneNoise > 0.55f) return Color.Lerp(smoothGray, sandPebble, (stoneNoise - 0.55f) / 0.45f);
                return Color.Lerp(wetSlate, smoothGray, stoneNoise / 0.55f);
            }

            // 2. Steep rocky slopes / cliff gravel.
            if (slope > 18f)
            {
                Color cliffStone = config.cliffColor;
                Color darkGranite = new Color(0.38f, 0.37f, 0.40f);
                return Color.Lerp(cliffStone, darkGranite, stoneNoise * 0.5f);
            }

            // 3. Open field / plain.
            Color granite = new Color(0.48f, 0.47f, 0.46f);
            Color mossyStone = new Color(0.38f, 0.45f, 0.35f);
            Color slate = new Color(0.40f, 0.42f, 0.46f);

            if (stoneNoise > 0.60f)
            {
                return Color.Lerp(granite, mossyStone, (stoneNoise - 0.60f) / 0.40f);
            }
            return Color.Lerp(slate, granite, stoneNoise / 0.60f);
        }

        private void BuildGrassBatch(
            LowPolyTerrainConfig config,
            int activeSeed,
            SeededRNG rng,
            float minX, float maxX, float minZ, float maxZ,
            float halfMapX, float halfMapZ,
            float centerRadiusSqr,
            SpatialOccupancyMap occupancyMap)
        {
            var routine = BuildGrassBatchRoutine(config, activeSeed, rng, minX, maxX, minZ, maxZ, halfMapX, halfMapZ, centerRadiusSqr, occupancyMap, null);
            while (routine.MoveNext()) { }
        }

        private System.Collections.IEnumerator BuildGrassBatchRoutine(
            LowPolyTerrainConfig config,
            int activeSeed,
            SeededRNG rng,
            float minX, float maxX, float minZ, float maxZ,
            float halfMapX, float halfMapZ,
            float centerRadiusSqr,
            SpatialOccupancyMap occupancyMap,
            GenerationBudget budget)
        {
            if ((grassTuftsPerChunk <= 0 && littleRocksPerChunk <= 0) || grassMaterial == null) yield break;

            // 1. Grass and Wildflower Archetypes.
            Mesh carpetMesh = FoliageMeshUtility.CreateDenseCarpetMesh(bladeCount: 18, radius: 0.85f, height: 0.90f, baseWidth: 0.28f);
            Mesh lushMesh = FoliageMeshUtility.CreateLushGrassClumpMesh(bladeCount: 15, radius: 0.75f, height: 1.30f, baseWidth: 0.22f);
            Mesh prairieMesh = FoliageMeshUtility.CreatePrairieGrassMesh(bladeCount: 10, radius: 0.55f, height: 0.70f, baseWidth: 0.14f);
            Mesh reedMesh = FoliageMeshUtility.CreateReedGrassMesh(bladeCount: 8, height: 1.45f, baseWidth: 0.12f);
            Mesh flowerMesh = FoliageMeshUtility.CreateWildflowerTuftMesh(bladeCount: 14, flowerCount: 4, radius: 0.75f, height: 0.90f, flowerHeight: 1.15f);

            // 2. Wild Weed Archetypes (Weeds & Undergrowth).
            Mesh wildWeedMesh = FoliageMeshUtility.CreateWildWeedTuftMesh(leafCount: 6, radius: 0.48f, height: 0.42f);
            Mesh broadleafMesh = FoliageMeshUtility.CreateBroadleafWeedMesh(leafCount: 5, radius: 0.42f, height: 0.22f);
            Mesh tallStalkMesh = FoliageMeshUtility.CreateTallStalkWeedMesh(stalkCount: 4, height: 1.35f, baseWidth: 0.10f);
            Mesh cloverMesh = FoliageMeshUtility.CreateCloverPatchMesh(cloverCount: 7, radius: 0.38f);

            // 3. Shoreline and Water Archetypes (Water Spots).
            Mesh cattailBedMesh = FoliageMeshUtility.CreateWaterCattailBedMesh(reedCount: 10, cattailCount: 3, radius: 0.65f, height: 1.55f);

            // 4. Little Rock Archetypes.
            Mesh singlePebbleMesh = FoliageMeshUtility.CreateLittlePebbleMesh(radius: 0.18f, height: 0.12f);
            Mesh pebbleClusterMesh = FoliageMeshUtility.CreatePebbleClusterMesh(pebbleCount: 4, radius: 0.42f);
            Mesh riverStoneMesh = FoliageMeshUtility.CreateRiverStoneMesh(radiusX: 0.30f, radiusZ: 0.20f, height: 0.08f);
            Mesh screeRockMesh = FoliageMeshUtility.CreateScreeRockMesh(size: 0.26f);

            List<Vector3> combinedVerts = new List<Vector3>();
            List<Vector3> combinedNormals = new List<Vector3>();
            List<Vector2> combinedUVs = new List<Vector2>();
            List<Color> combinedColors = new List<Color>();
            List<int> combinedTris = new List<int>();

            float chunkWorldLength = maxX - minX;

            if (grassTuftsPerChunk > 0)
            {
                // Hexagonal distribution (triangular packing) to eliminate grid patterns and visual channels.
                float hexAreaPerPoint = (chunkWorldLength * chunkWorldLength) / (float)grassTuftsPerChunk;
                float stepX = Mathf.Sqrt(hexAreaPerPoint / 0.8660254f);
                float stepZ = stepX * 0.8660254f;

                int gridResX = Mathf.CeilToInt(chunkWorldLength / stepX) + 1;
                int gridResZ = Mathf.CeilToInt(chunkWorldLength / stepZ) + 1;

                float maxFoliageH = config.heightMultiplier * 0.72f;
                float minGroundH = config.waterLevel + waterClearance;

                // Traverse the hexagonal grid with organic stratified sampling.
                for (int gz = 0; gz <= gridResZ; gz++)
                {
                    float rowOffset = (gz % 2 == 1) ? stepX * 0.5f : 0f;

                    for (int gx = -1; gx <= gridResX; gx++)
                    {
                        // Sample with organic jitter of ±42% of spacing.
                        float jitterX = rng.Range(-0.42f, 0.42f) * stepX;
                        float jitterZ = rng.Range(-0.42f, 0.42f) * stepZ;
                        float worldX = minX + gx * stepX + rowOffset + jitterX;
                        float worldZ = minZ + gz * stepZ + jitterZ;

                        // Strictly constrain to chunk bounds.
                        if (worldX < minX || worldX > maxX || worldZ < minZ || worldZ > maxZ) continue;

                        Vector2 worldXZ = new Vector2(worldX, worldZ);

                        float distSqr = worldX * worldX + worldZ * worldZ;
                        if (distSqr < centerRadiusSqr * 0.75f) continue;

                        // 1. Check SpatialOccupancyMap.
                        float canopyWeight = 0f;
                        bool inSanctuaryClearing = false;
                        bool inCombatClearing = false;

                        if (occupancyMap != null)
                        {
                            if (occupancyMap.IsSolidOccupied(worldXZ, clearanceRadius: 0.18f))
                            {
                                continue;
                            }

                            if (occupancyMap.IsInClearing(worldXZ, out OccupancyType clType))
                            {
                                if (clType == OccupancyType.Player_Sanctuary)
                                {
                                    inSanctuaryClearing = true;
                                    if (rng.Range(0f, 1f) > 0.18f) continue;
                                }
                                else if (clType == OccupancyType.Combat_Clearing)
                                {
                                    inCombatClearing = true;
                                    if (rng.Range(0f, 1f) > 0.35f) continue;
                                }
                            }

                            occupancyMap.IsUnderCanopy(worldXZ, out canopyWeight);
                        }

                        // 2. Sample relief, valley concavity, and slope.
                        float groundY = TerrainNoise.SampleHeight(worldX, worldZ, config, activeSeed, halfMapX, halfMapZ);
                        if (groundY < minGroundH || groundY > maxFoliageH) continue;

                        const float eps = 0.35f;
                        float hX1 = TerrainNoise.SampleHeight(worldX + eps, worldZ, config, activeSeed, halfMapX, halfMapZ);
                        float hX0 = TerrainNoise.SampleHeight(worldX - eps, worldZ, config, activeSeed, halfMapX, halfMapZ);
                        float hZ1 = TerrainNoise.SampleHeight(worldX, worldZ + eps, config, activeSeed, halfMapX, halfMapZ);
                        float hZ0 = TerrainNoise.SampleHeight(worldX, worldZ - eps, config, activeSeed, halfMapX, halfMapZ);

                        Vector3 normalWS = new Vector3(-(hX1 - hX0) / (2f * eps), 1f, -(hZ1 - hZ0) / (2f * eps)).normalized;
                        float slope = Vector3.Angle(normalWS, Vector3.up);

                        // Strict rejection on steep rocky slopes.
                        if (slope > config.steepSlopeThreshold * 0.85f) continue;

                        float slopeFactor = slope <= 14f ? 1.0f : Mathf.Clamp01((config.steepSlopeThreshold * 0.85f - slope) / (config.steepSlopeThreshold * 0.85f - 14f));

                        // Elevation factor (lush plains and valleys, sparse peaks).
                        float elevFactor = 1.0f;
                        float midMountain = config.heightMultiplier * 0.55f;
                        if (groundY > midMountain)
                        {
                            elevFactor = Mathf.Clamp01(1.0f - (groundY - midMountain) / (config.heightMultiplier * 0.20f));
                        }
                        else if (groundY < config.waterLevel + 1.2f)
                        {
                            elevFactor = Mathf.Clamp01((groundY - minGroundH) / 0.8f);
                        }

                        if (slopeFactor * elevFactor < 0.12f) continue;

                        // Concavity (depressions collecting moisture and forming greener carpets).
                        float concavity = ((hX1 + hX0 + hZ1 + hZ0) * 0.25f) - groundY;

                        // 3. Meadow Patch & Weed Colony Field.
                        float patchDensity = EvaluateMeadowPatch(worldX, worldZ, activeSeed);
                        float patchThreshold = macroDensityThreshold;
                        float edgeWidth = 0.10f;
                        float patchWeight = Mathf.Clamp01((patchDensity - (patchThreshold - edgeWidth * 0.5f)) / edgeWidth);

                        float weedColony = EvaluateWeedPatch(worldX, worldZ, activeSeed);

                        // Outside meadows: dirt trails with resilient ground cover.
                        if (patchWeight < 0.05f)
                        {
                            if (rng.Range(0f, 1f) > 0.05f * elevFactor) continue;
                        }

                        // 4. Ecological Archetype Selection.
                        Mesh chosenMesh;
                        bool hasAccent = false;
                        Color accentColor = Color.white;

                        bool isInsideMeadow = patchWeight >= 0.30f;
                        bool isWaterMargin = (groundY <= config.waterLevel + waterMarginBand + 0.35f);

                        if (isWaterMargin)
                        {
                            // Aquatic shoreline band and wet floodplain (Water Spots).
                            float waterRoll = rng.Range(0f, 1f);
                            if (waterRoll < cattailBedRatio * 3.5f)
                            {
                                chosenMesh = cattailBedMesh;
                                hasAccent = true;
                                accentColor = CattailAccentColor;
                            }
                            else if (waterRoll < (cattailBedRatio + reedGrassRatio) * 3.5f)
                            {
                                chosenMesh = reedMesh;
                            }
                            else if (waterRoll < 0.70f)
                            {
                                chosenMesh = broadleafMesh; // Broadleaf riverside plant.
                            }
                            else
                            {
                                chosenMesh = carpetMesh;
                            }
                        }
                        else if (!isInsideMeadow)
                        {
                            // Outside meadows: trails and edges.
                            float trailRoll = rng.Range(0f, 1f);
                            if (trailRoll < wildWeedRatio * 1.8f)
                            {
                                chosenMesh = wildWeedMesh;
                                hasAccent = true;
                                accentColor = (rng.Range(0f, 1f) < 0.65f) ? DandelionAccentColor : ThistleAccentColor;
                            }
                            else if (trailRoll < (wildWeedRatio + tallStalkRatio) * 1.5f)
                            {
                                chosenMesh = tallStalkMesh;
                            }
                            else
                            {
                                chosenMesh = prairieMesh;
                            }
                        }
                        else
                        {
                            // Inside meadows.
                            if (canopyWeight > 0.15f)
                            {
                                // Shade vegetation under tree canopies.
                                float canopyRoll = rng.Range(0f, 1f);
                                if (canopyRoll < 0.42f)
                                {
                                    chosenMesh = broadleafMesh;
                                }
                                else if (canopyRoll < 0.75f)
                                {
                                    chosenMesh = cloverMesh;
                                }
                                else
                                {
                                    chosenMesh = carpetMesh;
                                }
                            }
                            else if (weedColony > 0.48f && !inCombatClearing && !inSanctuaryClearing)
                            {
                                // Wild weed and plume colony.
                                float weedRoll = rng.Range(0f, 1f);
                                if (weedRoll < 0.38f)
                                {
                                    chosenMesh = wildWeedMesh;
                                    hasAccent = true;
                                    accentColor = (rng.Range(0f, 1f) < 0.60f) ? DandelionAccentColor : ThistleAccentColor;
                                }
                                else if (weedRoll < 0.65f)
                                {
                                    chosenMesh = tallStalkMesh;
                                }
                                else if (weedRoll < 0.85f)
                                {
                                    chosenMesh = cloverMesh;
                                }
                                else
                                {
                                    chosenMesh = broadleafMesh;
                                }
                            }
                            else
                            {
                                // Main lush meadow (Genshin / Zelda).
                                float roll = rng.Range(0f, 1f);
                                if (roll < wildflowerRatio && !inCombatClearing && !inSanctuaryClearing)
                                {
                                    chosenMesh = flowerMesh;
                                    hasAccent = true;
                                    float flowerColonyNoise = Mathf.PerlinNoise((worldX + activeSeed * 5.1f) * 0.016f, (worldZ + activeSeed * 7.7f) * 0.016f);
                                    int colonyIndex = Mathf.Clamp(Mathf.FloorToInt(flowerColonyNoise * WildflowerPalettes.Length), 0, WildflowerPalettes.Length - 1);
                                    accentColor = (rng.Range(0f, 1f) < 0.85f)
                                        ? WildflowerPalettes[colonyIndex]
                                        : WildflowerPalettes[rng.Range(0, WildflowerPalettes.Length)];
                                }
                                else if (roll < (wildflowerRatio + cloverPatchRatio) && !inCombatClearing && !inSanctuaryClearing)
                                {
                                    chosenMesh = cloverMesh;
                                }
                                else if (roll < (wildflowerRatio + cloverPatchRatio + lushGrassRatio * 0.40f) && !inCombatClearing && !inSanctuaryClearing)
                                {
                                    chosenMesh = lushMesh;
                                }
                                else
                                {
                                    chosenMesh = carpetMesh;
                                }
                            }
                        }

                        Color terrainColor = EvaluateTerrainColor(groundY, slope, normalWS, config);
                        if (canopyWeight > 0.05f)
                        {
                            terrainColor = Color.Lerp(terrainColor, config.deepGrassColor, canopyWeight * 0.45f);
                        }

                        // Continuous tip color in world space (no random per-tuft jitter).
                        Color tipColor = EvaluateFoliageTipColor(worldX, worldZ, canopyWeight, config);

                        // Base scale with smoothstep transition at the meadow edge for organic blending.
                        float edgeScale = isInsideMeadow ? Mathf.SmoothStep(0.65f, 1.15f, patchWeight) : 0.65f;
                        float scale = edgeScale * (1.0f + rng.Range(-0.06f, 0.06f));

                        if (chosenMesh == lushMesh) scale *= 1.10f;
                        if (chosenMesh == carpetMesh) scale *= 1.08f;
                        if (chosenMesh == cattailBedMesh) scale *= 1.05f;
                        if (canopyWeight > 0.1f) scale *= (1.0f + canopyWeight * 0.15f);
                        if (inCombatClearing) scale *= 0.70f;

                        float yRot = rng.Range(0f, 360f);
                        Quaternion rot = Quaternion.Euler(0f, yRot, 0f);

                        Vector3 instanceLocalPos = transform.InverseTransformPoint(new Vector3(worldX, groundY, worldZ));

                        AppendMeshInstance(
                            chosenMesh,
                            instanceLocalPos,
                            rot,
                            scale,
                            terrainColor,
                            tipColor,
                            normalWS,
                            true, // isGrass
                            hasAccent,
                            accentColor,
                            combinedVerts,
                            combinedNormals,
                            combinedUVs,
                            combinedColors,
                            combinedTris
                        );
                    }

                    if (budget != null && budget.ShouldYield())
                    {
                        yield return null;
                    }
                }
            }

            // 5. Little Rock and Pebble Clustering / Scattering Pass.
            if (littleRocksPerChunk > 0)
            {
                int rocksToPlace = littleRocksPerChunk;
                int placedRocks = 0;
                int maxAttempts = rocksToPlace * 4;

                for (int a = 0; a < maxAttempts && placedRocks < rocksToPlace; a++)
                {
                    float rx = rng.Range(minX + 0.5f, maxX - 0.5f);
                    float rz = rng.Range(minZ + 0.5f, maxZ - 0.5f);
                    Vector2 rXZ = new Vector2(rx, rz);

                    float distSqr = rx * rx + rz * rz;
                    if (distSqr < centerRadiusSqr * 0.6f) continue;

                    if (occupancyMap != null)
                    {
                        if (occupancyMap.IsSolidOccupied(rXZ, clearanceRadius: 0.15f)) continue;
                    }

                    float ry = TerrainNoise.SampleHeight(rx, rz, config, activeSeed, halfMapX, halfMapZ);
                    // Rocks can sit at the water margin (up to waterLevel + 0.05m).
                    if (ry < config.waterLevel + 0.05f || ry > (config.heightMultiplier * 0.75f) + 6f) continue;

                    const float eps = 0.35f;
                    float hX1 = TerrainNoise.SampleHeight(rx + eps, rz, config, activeSeed, halfMapX, halfMapZ);
                    float hX0 = TerrainNoise.SampleHeight(rx - eps, rz, config, activeSeed, halfMapX, halfMapZ);
                    float hZ1 = TerrainNoise.SampleHeight(rx, rz + eps, config, activeSeed, halfMapX, halfMapZ);
                    float hZ0 = TerrainNoise.SampleHeight(rx, rz - eps, config, activeSeed, halfMapX, halfMapZ);

                    Vector3 rNormal = new Vector3(-(hX1 - hX0) / (2f * eps), 1f, -(hZ1 - hZ0) / (2f * eps)).normalized;
                    float rSlope = Vector3.Angle(rNormal, Vector3.up);

                    // Reject only near-vertical cliffs (> 70°).
                    if (rSlope > 70f) continue;

                    // Select rock archetype according to ecological context.
                    Mesh rockMesh;
                    bool isWaterEdge = (ry <= config.waterLevel + waterMarginBand + 0.20f);

                    if (isWaterEdge)
                    {
                        // Smooth river stones at the water's edge.
                        rockMesh = (rng.Range(0f, 1f) < 0.65f) ? riverStoneMesh : pebbleClusterMesh;
                    }
                    else if (rSlope > 18f)
                    {
                        // Chipped gravel on slopes and cliff bases.
                        rockMesh = (rng.Range(0f, 1f) < 0.68f) ? screeRockMesh : singlePebbleMesh;
                    }
                    else
                    {
                        // Field pebbles and clusters.
                        rockMesh = (rng.Range(0f, 1f) < pebbleClusterRatio) ? pebbleClusterMesh : singlePebbleMesh;
                    }

                    Color stoneCol = EvaluateRockColor(rx, rz, ry, rSlope, config);
                    float rScale = rng.Range(0.85f, 1.35f);
                    float rRotY = rng.Range(0f, 360f);
                    Quaternion rRot = Quaternion.Euler(0f, rRotY, 0f);

                    Vector3 rLocalPos = transform.InverseTransformPoint(new Vector3(rx, ry, rz));

                    AppendMeshInstance(
                        rockMesh,
                        rLocalPos,
                        rRot,
                        rScale,
                        Color.white,
                        Color.white,
                        rNormal,
                        false, // isGrass = false
                        false,
                        Color.white,
                        combinedVerts,
                        combinedNormals,
                        combinedUVs,
                        combinedColors,
                        combinedTris,
                        isRock: true,
                        rockColor: stoneCol
                    );

                    placedRocks++;

                    if (budget != null && placedRocks % 20 == 0 && budget.ShouldYield())
                    {
                        yield return null;
                    }
                }
            }

            if (combinedVerts.Count > 0)
            {
                CreateBatchGameObject("Foliage_GrassBatch", combinedVerts, combinedNormals, combinedUVs, combinedColors, combinedTris, grassMaterial);
            }
        }

        private void BuildBushBatch(
            LowPolyTerrainConfig config,
            int activeSeed,
            SeededRNG rng,
            float minX, float maxX, float minZ, float maxZ,
            float halfMapX, float halfMapZ,
            float centerRadiusSqr,
            SpatialOccupancyMap occupancyMap)
        {
            if (bushesPerChunk <= 0 || bushMaterial == null) return;

            Mesh puffBushMesh = FoliageMeshUtility.CreateBushMesh(lobes: 4, baseRadius: 0.65f, height: 1.0f);
            Mesh floweringBushMesh = FoliageMeshUtility.CreateFloweringBushMesh(lobes: 4, flowerCount: 12, baseRadius: 0.65f, height: 1.0f);
            Mesh fernBushMesh = FoliageMeshUtility.CreateFernBushMesh(frondCount: 7, radius: 0.85f, height: 0.65f);
            Mesh groundShrubMesh = FoliageMeshUtility.CreateGroundShrubMesh(lobes: 5, radius: 0.95f, height: 0.45f);

            List<Vector3> combinedVerts = new List<Vector3>();
            List<Vector3> combinedNormals = new List<Vector3>();
            List<Vector2> combinedUVs = new List<Vector2>();
            List<Color> combinedColors = new List<Color>();
            List<int> combinedTris = new List<int>();

            int placedCount = 0;
            float maxFoliageH = config.heightMultiplier * 0.70f;
            float minGroundH = config.waterLevel + waterClearance + 0.35f;

            // Cluster into natural thickets / groves (2 to 4 shrubs per cluster).
            int thicketCount = Mathf.Max(1, Mathf.RoundToInt(bushesPerChunk / 2.8f));
            int attemptsPerThicket = 15;

            for (int t = 0; t < thicketCount && placedCount < bushesPerChunk; t++)
            {
                Vector2 thicketCenter = Vector2.zero;
                bool foundCenter = false;

                for (int a = 0; a < attemptsPerThicket; a++)
                {
                    float candX = rng.Range(minX + 1.2f, maxX - 1.2f);
                    float candZ = rng.Range(minZ + 1.2f, maxZ - 1.2f);
                    Vector2 candXZ = new Vector2(candX, candZ);

                    float distSqr = candX * candX + candZ * candZ;
                    if (distSqr < centerRadiusSqr) continue;

                    if (occupancyMap != null)
                    {
                        if (occupancyMap.IsSolidOccupied(candXZ, clearanceRadius: 0.55f)) continue;
                        if (occupancyMap.IsInClearing(candXZ, out _)) continue;
                    }

                    float candY = TerrainNoise.SampleHeight(candX, candZ, config, activeSeed, halfMapX, halfMapZ);
                    if (candY < minGroundH || candY > maxFoliageH) continue;

                    const float eps = 0.35f;
                    float hX1 = TerrainNoise.SampleHeight(candX + eps, candZ, config, activeSeed, halfMapX, halfMapZ);
                    float hX0 = TerrainNoise.SampleHeight(candX - eps, candZ, config, activeSeed, halfMapX, halfMapZ);
                    float hZ1 = TerrainNoise.SampleHeight(candX, candZ + eps, config, activeSeed, halfMapX, halfMapZ);
                    float hZ0 = TerrainNoise.SampleHeight(candX, candZ - eps, config, activeSeed, halfMapX, halfMapZ);
                    Vector3 norm = new Vector3(-(hX1 - hX0) / (2f * eps), 1f, -(hZ1 - hZ0) / (2f * eps)).normalized;
                    float slp = Vector3.Angle(norm, Vector3.up);
                    if (slp > config.steepSlopeThreshold * 0.75f) continue;

                    thicketCenter = candXZ;
                    foundCenter = true;
                    break;
                }

                if (!foundCenter) continue;

                // Spawn cluster / thicket shrubs around the center.
                int bushesInThisThicket = rng.Range(2, 5);
                float thicketRadius = rng.Range(1.2f, 2.4f);

                for (int b = 0; b < bushesInThisThicket && placedCount < bushesPerChunk; b++)
                {
                    float angle = rng.Range(0f, Mathf.PI * 2f);
                    float r = (b == 0) ? 0f : thicketRadius * Mathf.Sqrt(rng.Range(0.15f, 1f));
                    float worldX = thicketCenter.x + Mathf.Cos(angle) * r;
                    float worldZ = thicketCenter.y + Mathf.Sin(angle) * r;
                    Vector2 worldXZ = new Vector2(worldX, worldZ);

                    if (worldX < minX || worldX > maxX || worldZ < minZ || worldZ > maxZ) continue;

                    float canopyWeight = 0f;
                    if (occupancyMap != null)
                    {
                        if (occupancyMap.IsSolidOccupied(worldXZ, clearanceRadius: 0.40f)) continue;
                        if (occupancyMap.IsInClearing(worldXZ, out _)) continue;
                        occupancyMap.IsUnderCanopy(worldXZ, out canopyWeight);
                    }

                    float groundY = TerrainNoise.SampleHeight(worldX, worldZ, config, activeSeed, halfMapX, halfMapZ);
                    if (groundY < minGroundH || groundY > maxFoliageH) continue;

                    const float eps = 0.35f;
                    float hX1 = TerrainNoise.SampleHeight(worldX + eps, worldZ, config, activeSeed, halfMapX, halfMapZ);
                    float hX0 = TerrainNoise.SampleHeight(worldX - eps, worldZ, config, activeSeed, halfMapX, halfMapZ);
                    float hZ1 = TerrainNoise.SampleHeight(worldX, worldZ + eps, config, activeSeed, halfMapX, halfMapZ);
                    float hZ0 = TerrainNoise.SampleHeight(worldX, worldZ - eps, config, activeSeed, halfMapX, halfMapZ);

                    Vector3 normalWS = new Vector3(-(hX1 - hX0) / (2f * eps), 1f, -(hZ1 - hZ0) / (2f * eps)).normalized;
                    float slope = Vector3.Angle(normalWS, Vector3.up);
                    if (slope > config.steepSlopeThreshold * 0.75f) continue;

                    // Select shrub archetype.
                    Mesh chosenMesh;
                    bool hasAccent = false;
                    Color accentColor = Color.white;

                    float archRoll = rng.Range(0f, 1f);
                    if (canopyWeight > 0.25f || archRoll < fernBushRatio)
                    {
                        chosenMesh = fernBushMesh;
                    }
                    else if (slope > 18f || archRoll < (fernBushRatio + groundShrubRatio))
                    {
                        chosenMesh = groundShrubMesh;
                    }
                    else if (archRoll < (fernBushRatio + groundShrubRatio + floweringBushRatio))
                    {
                        chosenMesh = floweringBushMesh;
                        hasAccent = true;

                        float flowerColonyNoise = Mathf.PerlinNoise((worldX + activeSeed * 5.1f) * 0.016f, (worldZ + activeSeed * 7.7f) * 0.016f);
                        int colonyIndex = Mathf.Clamp(Mathf.FloorToInt(flowerColonyNoise * WildflowerPalettes.Length), 0, WildflowerPalettes.Length - 1);
                        accentColor = WildflowerPalettes[colonyIndex];
                    }
                    else
                    {
                        chosenMesh = puffBushMesh;
                    }

                    Color terrainColor = EvaluateTerrainColor(groundY, slope, normalWS, config);
                    if (canopyWeight > 0.05f)
                    {
                        terrainColor = Color.Lerp(terrainColor, config.deepGrassColor, canopyWeight * 0.45f);
                    }

                    Color tipColor;
                    if (chosenMesh == fernBushMesh)
                    {
                        tipColor = Color.Lerp(new Color(0.20f, 0.55f, 0.22f), new Color(0.38f, 0.75f, 0.25f), rng.Range(0.2f, 0.8f));
                    }
                    else if (chosenMesh == groundShrubMesh)
                    {
                        tipColor = Color.Lerp(new Color(0.18f, 0.48f, 0.20f), new Color(0.32f, 0.65f, 0.22f), rng.Range(0.2f, 0.8f));
                    }
                    else
                    {
                        tipColor = Color.Lerp(new Color(0.22f, 0.52f, 0.20f), new Color(0.42f, 0.72f, 0.24f), rng.Range(0.2f, 0.8f));
                    }

                    float scale = rng.Range(0.85f, 1.45f);
                    if (chosenMesh == groundShrubMesh) scale *= 1.15f;
                    float yRot = rng.Range(0f, 360f);
                    Quaternion rot = Quaternion.Euler(0f, yRot, 0f);

                    Vector3 instanceLocalPos = transform.InverseTransformPoint(new Vector3(worldX, groundY, worldZ));

                    AppendMeshInstance(
                        chosenMesh,
                        instanceLocalPos,
                        rot,
                        scale,
                        terrainColor,
                        tipColor,
                        normalWS,
                        false, // isGrass = false
                        hasAccent,
                        accentColor,
                        combinedVerts,
                        combinedNormals,
                        combinedUVs,
                        combinedColors,
                        combinedTris
                    );

                    placedCount++;
                }
            }

            if (combinedVerts.Count > 0)
            {
                CreateBatchGameObject("Foliage_BushBatch", combinedVerts, combinedNormals, combinedUVs, combinedColors, combinedTris, bushMaterial);
            }
        }

        private void AppendMeshInstance(
            Mesh templateMesh,
            Vector3 localPos,
            Quaternion rot,
            float scale,
            Color terrainColor,
            Color tipColor,
            Vector3 surfaceNormal,
            bool isGrass,
            bool hasAccent,
            Color accentColor,
            List<Vector3> combinedVerts,
            List<Vector3> combinedNormals,
            List<Vector2> combinedUVs,
            List<Color> combinedColors,
            List<int> combinedTris,
            bool isRock = false,
            Color rockColor = default)
        {
            Vector3[] baseVerts = templateMesh.vertices;
            Vector3[] baseNormals = templateMesh.normals;
            Vector2[] baseUVs = templateMesh.uv;
            Color[] baseColors = templateMesh.colors;
            int[] baseTris = templateMesh.triangles;

            int vertCount = baseVerts.Length;
            int triCount = baseTris.Length;
            int startVertIndex = combinedVerts.Count;

            // Convert terrain surface normal to chunk local space.
            Vector3 localSurfaceNormal = transform.InverseTransformDirection(surfaceNormal).normalized;
            Vector3 grassAlignedNormal = Vector3.Lerp(localSurfaceNormal, Vector3.up, 0.40f).normalized;

            for (int v = 0; v < vertCount; v++)
            {
                Vector3 lv = rot * (baseVerts[v] * scale) + localPos;

                Vector3 ln;
                if (isRock)
                {
                    // Rocks strictly preserve their faceted geometric normals for low-poly cel shading.
                    ln = rot * baseNormals[v];
                }
                else if (isGrass)
                {
                    // Align predominantly with terrain normal / Vector3.up (Genshin / Zelda style).
                    Vector3 baseRotNormal = rot * baseNormals[v];
                    ln = (baseVerts[v].y > 0.45f)
                        ? Vector3.Lerp(grassAlignedNormal, baseRotNormal, 0.12f).normalized
                        : grassAlignedNormal;
                }
                else
                {
                    // Shrubs preserve spherical anime puff normals.
                    ln = rot * baseNormals[v];
                }

                combinedVerts.Add(lv);
                combinedNormals.Add(ln);

                float rawU = baseUVs[v].x;
                float rawV = baseUVs[v].y;

                if (isRock)
                {
                    // Rocks: rock color with base contact AO and strictly 0.0 alpha (zero shader wind).
                    combinedUVs.Add(new Vector2(0.5f, 0.0f));
                    Color stoneCol = (rockColor != default) ? rockColor : new Color(0.48f, 0.47f, 0.46f);
                    float contactAO = Mathf.Lerp(0.70f, 1.0f, Mathf.Clamp01(baseVerts[v].y / 0.12f));
                    stoneCol.r *= contactAO;
                    stoneCol.g *= contactAO;
                    stoneCol.b *= contactAO;
                    stoneCol.a = 0.0f; // Strictly zero wind
                    combinedColors.Add(stoneCol);
                }
                else if (hasAccent && rawU >= 2.0f)
                {
                    combinedUVs.Add(new Vector2(rawU - 2.0f, rawV));
                    Color petColor = accentColor;
                    petColor.a = baseColors != null && v < baseColors.Length ? baseColors[v].a : 1.0f;
                    combinedColors.Add(petColor);
                }
                else
                {
                    combinedUVs.Add(baseUVs[v]);
                    float hFactor = rawV;
                    Color vertColor = Color.Lerp(terrainColor, tipColor, hFactor * 0.88f);
                    vertColor.a = baseColors != null && v < baseColors.Length ? baseColors[v].a : hFactor;
                    combinedColors.Add(vertColor);
                }
            }

            for (int t = 0; t < triCount; t++)
            {
                combinedTris.Add(startVertIndex + baseTris[t]);
            }
        }

        private Color EvaluateFoliageTipColor(float worldX, float worldZ, float canopyWeight, LowPolyTerrainConfig config)
        {
            // Continuous hue field in world coordinates (50-meter macro scale).
            float hueField = Mathf.PerlinNoise((worldX + 512.4f) * 0.018f, (worldZ + 841.7f) * 0.018f);

            Color sunnyGreen = new Color(0.48f, 0.82f, 0.24f);
            Color springGreen = new Color(0.35f, 0.72f, 0.22f);
            Color goldenGreen = new Color(0.55f, 0.78f, 0.26f);

            Color baseTip;
            if (hueField > 0.55f)
            {
                float t = (hueField - 0.55f) / 0.45f;
                baseTip = Color.Lerp(springGreen, sunnyGreen, t);
            }
            else
            {
                float t = hueField / 0.55f;
                baseTip = Color.Lerp(goldenGreen, springGreen, t);
            }

            if (canopyWeight > 0.08f)
            {
                Color shadeJade = new Color(0.18f, 0.52f, 0.22f);
                baseTip = Color.Lerp(baseTip, shadeJade, canopyWeight * 0.65f);
            }

            return baseTip;
        }

        private void CreateBatchGameObject(
            string holderName,
            List<Vector3> verts,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<Color> colors,
            List<int> tris,
            Material material)
        {
            GameObject holder = new GameObject(holderName);
            holder.transform.parent = transform;
            holder.transform.localPosition = Vector3.zero;
            holder.transform.localRotation = Quaternion.identity;
            holder.transform.localScale = Vector3.one;

            NavMeshModifier navMod = holder.AddComponent<NavMeshModifier>();
            navMod.ignoreFromBuild = true;

            MeshFilter mf = holder.AddComponent<MeshFilter>();
            MeshRenderer mr = holder.AddComponent<MeshRenderer>();

            Mesh combinedMesh = new Mesh();
            Vector2Int coord = _terrainChunk != null ? _terrainChunk.ChunkCoord : Vector2Int.zero;
            combinedMesh.name = $"{holderName}_{coord.x}_{coord.y}";
            combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            combinedMesh.SetVertices(verts);
            combinedMesh.SetNormals(normals);
            combinedMesh.SetUVs(0, uvs);
            combinedMesh.SetColors(colors);
            combinedMesh.SetTriangles(tris, 0);
            combinedMesh.RecalculateBounds();

            mf.sharedMesh = combinedMesh;
            mr.sharedMaterial = material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;
        }

        private Color EvaluateTerrainColor(float height, float slopeAngle, Vector3 normal, LowPolyTerrainConfig config)
        {
            if (height <= config.waterLevel + 1.0f)
            {
                float sandBlend = Mathf.Clamp01((height - config.waterLevel) / 1.0f);
                return Color.Lerp(config.sandColor, config.grassColor, sandBlend);
            }
            if (slopeAngle >= config.steepSlopeThreshold)
            {
                return config.cliffColor;
            }
            if (height >= config.heightMultiplier * 0.62f)
            {
                float rockBlend = Mathf.Clamp01((height - config.heightMultiplier * 0.62f) / (config.heightMultiplier * 0.23f));
                return Color.Lerp(config.grassColor, config.rockColor, rockBlend);
            }

            float valleyBlend = Mathf.Clamp01(height / (config.heightMultiplier * 0.45f));
            return Color.Lerp(config.deepGrassColor, config.grassColor, valleyBlend);
        }
    }
}
