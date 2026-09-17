using System.Collections.Generic;
using UnityEngine;
using Duskborn.Core;
using Duskborn.Gameplay.World;
using Unity.AI.Navigation;

namespace Duskborn.Gameplay.World.Foliage
{
    /// <summary>
    /// Níveis de pré-definição de densidade de vegetação procedural.
    /// </summary>
    public enum FoliageDensityPreset
    {
        Custom,
        Low,           // 400 tufos de grama, 12 arbustos
        Medium,        // 950 tufos de grama, 20 arbustos
        High,          // 1800 tufos de grama, 32 arbustos
        Ultra_Genshin, // 3200 tufos de grama, 48 arbustos
        Cinematic_Lush // 5000 tufos de grama, 64 arbustos
    }

    /// <summary>
    /// Gerenciador de folhagem procedural de alta densidade por chunk (estilo Genshin Impact / Zelda: BotW).
    /// Gera tufos de grama, tufos viçosos densos, flores silvestres e arbustos/samambaias respeitando
    /// a topografia do terreno, ondas de densidade macro (meadow fields), mapas de ocupação física (SpatialOccupancyMap)
    /// e clareiras de combate, consolidando toda a geometria em malhas unificadas por chunk (1 draw call para toda a grama,
    /// 1 draw call para os arbustos, executado 100% na GPU com suporte a sombras e animação de vento).
    /// </summary>
    [RequireComponent(typeof(TerrainChunk))]
    public class ChunkFoliagePlacer : MonoBehaviour
    {
        [Header("Materiais de Folhagem")]
        [Tooltip("Material para os tufos de grama e flores (shader Duskborn/StylizedFoliage).")]
        [SerializeField] private Material grassMaterial;

        [Tooltip("Material para os arbustos estilizados (shader Duskborn/StylizedFoliage).")]
        [SerializeField] private Material bushMaterial;

        [Header("Configuração de Densidade (Escala Genshin / Zelda)")]
        [Tooltip("Perfil pré-definido de densidade de vegetação.")]
        [SerializeField] private FoliageDensityPreset densityPreset = FoliageDensityPreset.High;

        [Tooltip("Quantidade de tufos de grama gerados por chunk.")]
        [Range(0, 6000)]
        [SerializeField] private int grassTuftsPerChunk = 1800;

        [Tooltip("Quantidade de arbustos gerados por chunk (desativado por padrão).")]
        [Range(0, 100)]
        [SerializeField] private int bushesPerChunk = 0;

        [Header("Distribuição de Arquétipos de Grama")]
        [Tooltip("Proporção de grama carpete densa para cobertura contínua de solo (Dense Carpet Grass).")]
        [Range(0f, 1f)]
        [SerializeField] private float carpetGrassRatio = 0.38f;

        [Tooltip("Proporção de tufos de grama alta e viçosa anime (Lush Grass Clumps).")]
        [Range(0f, 1f)]
        [SerializeField] private float lushGrassRatio = 0.26f;

        [Tooltip("Proporção de tufos de flores silvestres em colônias (Wildflowers).")]
        [Range(0f, 1f)]
        [SerializeField] private float wildflowerRatio = 0.16f;

        [Tooltip("Proporção de grama fina de pradaria nas orlas e transições (Prairie Grass).")]
        [Range(0f, 1f)]
        [SerializeField] private float prairieGrassRatio = 0.14f;

        [Tooltip("Proporção de juncos de várzea em depressões úmidas e margens (Reed Grass).")]
        [Range(0f, 1f)]
        [SerializeField] private float reedGrassRatio = 0.06f;

        [Header("Distribuição de Arquétipos de Arbustos")]
        [Tooltip("Proporção de samambaias/arbustos em leque (Fern Bushes).")]
        [Range(0f, 1f)]
        [SerializeField] private float fernBushRatio = 0.30f;

        [Tooltip("Proporção de arbustos floridos com bagas/botões coloridos (Flowering Bushes).")]
        [Range(0f, 1f)]
        [SerializeField] private float floweringBushRatio = 0.28f;

        [Tooltip("Proporção de arbustos rasteiros de cobertura de solo para ancorar rochas (Ground Shrubs).")]
        [Range(0f, 1f)]
        [SerializeField] private float groundShrubRatio = 0.22f;

        [Header("Ondas de Campo & Relevo (Macro Ecology)")]
        [Tooltip("Frequência espacial do ruído macro de densidade de campina.")]
        [SerializeField] private float macroNoiseScale = 0.024f;

        [Tooltip("Limiar de ruído abaixo do qual a campina se torna clareira aberta / trilha suave.")]
        [SerializeField] private float macroDensityThreshold = 0.30f;

        [Tooltip("Deslocamento mínimo acima da água para permitir vegetação.")]
        [SerializeField] private float waterClearance = 0.65f;

        private TerrainChunk _terrainChunk;

        // Paleta de flores silvestres estilizadas (Genshin / Zelda anime palette)
        public static readonly Color[] WildflowerPalettes = new Color[]
        {
            new Color(0.24f, 0.58f, 0.98f, 1f), // Azul campânula / Cornflower Azure
            new Color(0.98f, 0.84f, 0.14f, 1f), // Amarelo dente-de-leão / Dandelion Gold
            new Color(0.94f, 0.26f, 0.35f, 1f), // Vermelho papoula / Poppy Crimson
            new Color(0.95f, 0.96f, 0.92f, 1f), // Branco margarida / Mountain Daisy
            new Color(0.72f, 0.48f, 0.95f, 1f)  // Roxo lavanda / Wild Lavender
        };

        public FoliageDensityPreset DensityPreset => densityPreset;
        public int GrassTuftsPerChunk => grassTuftsPerChunk;
        public int BushesPerChunk => bushesPerChunk;
        public float CarpetGrassRatio => carpetGrassRatio;
        public float LushGrassRatio => lushGrassRatio;
        public float WildflowerRatio => wildflowerRatio;
        public float PrairieGrassRatio => prairieGrassRatio;
        public float ReedGrassRatio => reedGrassRatio;
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

        public void SetFoliageCounts(int grassCount, int bushCount)
        {
            densityPreset = FoliageDensityPreset.Custom;
            grassTuftsPerChunk = Mathf.Max(0, grassCount);
            bushesPerChunk = Mathf.Max(0, bushCount);
        }

        public void ApplyDensityPreset()
        {
            switch (densityPreset)
            {
                case FoliageDensityPreset.Low:
                    grassTuftsPerChunk = 400;
                    bushesPerChunk = 0;
                    break;
                case FoliageDensityPreset.Medium:
                    grassTuftsPerChunk = 950;
                    bushesPerChunk = 0;
                    break;
                case FoliageDensityPreset.High:
                    grassTuftsPerChunk = 1800;
                    bushesPerChunk = 0;
                    break;
                case FoliageDensityPreset.Ultra_Genshin:
                    grassTuftsPerChunk = 3200;
                    bushesPerChunk = 0;
                    break;
                case FoliageDensityPreset.Cinematic_Lush:
                    grassTuftsPerChunk = 5000;
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

            // Tenta resolver o mapa de ocupação automaticamente caso não tenha sido injetado
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

            // 1. Gera o lote consolidado de grama e flores (1 draw call por chunk)
            BuildGrassBatch(config, seed, rng, minX, maxX, minZ, maxZ, halfMapX, halfMapZ, centerRadiusSqr, occupancyMap);

            // 2. Arbustos (desativados a pedido do usuário por artefatos visuais)
            if (bushesPerChunk > 0)
            {
                BuildBushBatch(config, seed, rng, minX, maxX, minZ, maxZ, halfMapX, halfMapZ, centerRadiusSqr, occupancyMap);
            }
        }

        /// <summary>
        /// Geração assíncrona da folhagem do chunk fatiada em múltiplos frames para manter 60+ FPS constante.
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

            // 1. Gera o lote consolidado de grama e flores assincronamente
            yield return BuildGrassBatchRoutine(config, seed, rng, minX, maxX, minZ, maxZ, halfMapX, halfMapZ, centerRadiusSqr, occupancyMap, budget);

            // 2. Arbustos
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
            if (grassTuftsPerChunk <= 0 || grassMaterial == null) yield break;

            Mesh carpetMesh = FoliageMeshUtility.CreateDenseCarpetMesh(bladeCount: 18, radius: 0.85f, height: 0.90f, baseWidth: 0.28f);
            Mesh lushMesh = FoliageMeshUtility.CreateLushGrassClumpMesh(bladeCount: 15, radius: 0.75f, height: 1.30f, baseWidth: 0.22f);
            Mesh prairieMesh = FoliageMeshUtility.CreatePrairieGrassMesh(bladeCount: 10, radius: 0.55f, height: 0.70f, baseWidth: 0.14f);
            Mesh reedMesh = FoliageMeshUtility.CreateReedGrassMesh(bladeCount: 8, height: 1.45f, baseWidth: 0.12f);
            Mesh flowerMesh = FoliageMeshUtility.CreateWildflowerTuftMesh(bladeCount: 14, flowerCount: 4, radius: 0.75f, height: 0.90f, flowerHeight: 1.15f);

            List<Vector3> combinedVerts = new List<Vector3>();
            List<Vector3> combinedNormals = new List<Vector3>();
            List<Vector2> combinedUVs = new List<Vector2>();
            List<Color> combinedColors = new List<Color>();
            List<int> combinedTris = new List<int>();

            float chunkWorldLength = maxX - minX;
            // Distribuição em malha hexagonal (triangular packing) para eliminar padrões de grade e canais visuais
            // Área por ponto em malha hexagonal: Area = stepX * stepZ = stepX * (stepX * sin(60°)) = stepX^2 * 0.8660254
            float hexAreaPerPoint = (chunkWorldLength * chunkWorldLength) / (float)grassTuftsPerChunk;
            float stepX = Mathf.Sqrt(hexAreaPerPoint / 0.8660254f);
            float stepZ = stepX * 0.8660254f;

            int gridResX = Mathf.CeilToInt(chunkWorldLength / stepX) + 1;
            int gridResZ = Mathf.CeilToInt(chunkWorldLength / stepZ) + 1;

            float maxFoliageH = config.heightMultiplier * 0.72f;
            float minGroundH = config.waterLevel + waterClearance;

            // Percorre a malha hexagonal com amostragem orgânica estratificada
            for (int gz = 0; gz <= gridResZ; gz++)
            {
                float rowOffset = (gz % 2 == 1) ? stepX * 0.5f : 0f;

                for (int gx = -1; gx <= gridResX; gx++)
                {
                    // Amostragem com jitter orgânico de ±42% do espaçamento
                    // Elimina qualquer alinhamento cartesiano sem produzir aglomerações anômalas ou clareiras
                    float jitterX = rng.Range(-0.42f, 0.42f) * stepX;
                    float jitterZ = rng.Range(-0.42f, 0.42f) * stepZ;
                    float worldX = minX + gx * stepX + rowOffset + jitterX;
                    float worldZ = minZ + gz * stepZ + jitterZ;

                    // Restringe estritamente aos limites do chunk
                    if (worldX < minX || worldX > maxX || worldZ < minZ || worldZ > maxZ) continue;

                    Vector2 worldXZ = new Vector2(worldX, worldZ);

                    float distSqr = worldX * worldX + worldZ * worldZ;
                    if (distSqr < centerRadiusSqr * 0.75f) continue;

                    // 1. Verificação com SpatialOccupancyMap
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

                    // 2. Amostra relevo, concavidade de vale e inclinação
                    float groundY = TerrainNoise.SampleHeight(worldX, worldZ, config, activeSeed, halfMapX, halfMapZ);
                    if (groundY < minGroundH || groundY > maxFoliageH) continue;

                    const float eps = 0.35f;
                    float hX1 = TerrainNoise.SampleHeight(worldX + eps, worldZ, config, activeSeed, halfMapX, halfMapZ);
                    float hX0 = TerrainNoise.SampleHeight(worldX - eps, worldZ, config, activeSeed, halfMapX, halfMapZ);
                    float hZ1 = TerrainNoise.SampleHeight(worldX, worldZ + eps, config, activeSeed, halfMapX, halfMapZ);
                    float hZ0 = TerrainNoise.SampleHeight(worldX, worldZ - eps, config, activeSeed, halfMapX, halfMapZ);

                    Vector3 normalWS = new Vector3(-(hX1 - hX0) / (2f * eps), 1f, -(hZ1 - hZ0) / (2f * eps)).normalized;
                    float slope = Vector3.Angle(normalWS, Vector3.up);

                    // Rejeição estrita em encostas rochosas íngremes
                    if (slope > config.steepSlopeThreshold * 0.85f) continue;

                    float slopeFactor = slope <= 14f ? 1.0f : Mathf.Clamp01((config.steepSlopeThreshold * 0.85f - slope) / (config.steepSlopeThreshold * 0.85f - 14f));

                    // Fator de elevação (planícies e vales exuberantes, rarefeito nos cumes)
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

                    // Concavidade (depressões que acumulam umidade e formam tapetes mais verdes)
                    float concavity = ((hX1 + hX0 + hZ1 + hZ0) * 0.25f) - groundY;

                    // 3. Campo de Manchas de Campina (Meadow Patch Waves - Pintura Homogênea estilo Genshin / Zelda)
                    float patchDensity = EvaluateMeadowPatch(worldX, worldZ, activeSeed);
                    float patchThreshold = macroDensityThreshold;
                    float edgeWidth = 0.10f;
                    float patchWeight = Mathf.Clamp01((patchDensity - (patchThreshold - edgeWidth * 0.5f)) / edgeWidth);

                    // Fora das campinas: trilhas de terra limpas com tufos raros de pradaria
                    if (patchWeight < 0.05f)
                    {
                        if (rng.Range(0f, 1f) > 0.04f * elevFactor) continue;
                    }

                    // 4. Seleção de Arquétipo com foco em Cobertura Homogênea
                    Mesh chosenMesh;
                    bool hasAccent = false;
                    Color accentColor = Color.white;

                    bool isInsideMeadow = patchWeight >= 0.30f;

                    if (!isInsideMeadow)
                    {
                        chosenMesh = prairieMesh;
                    }
                    else
                    {
                        bool isWetland = (groundY < config.waterLevel + 1.6f || concavity > 0.15f);
                        if (isWetland && rng.Range(0f, 1f) < (reedGrassRatio * 2.5f))
                        {
                            chosenMesh = reedMesh;
                        }
                        else
                        {
                            float roll = rng.Range(0f, 1f);
                            if (roll < wildflowerRatio && !inCombatClearing && !inSanctuaryClearing)
                            {
                                chosenMesh = flowerMesh;
                                hasAccent = true;

                                // Colônias de flores com harmonia de cores espacial macro (estilo campinas floridas de anime)
                                float flowerColonyNoise = Mathf.PerlinNoise((worldX + activeSeed * 5.1f) * 0.016f, (worldZ + activeSeed * 7.7f) * 0.016f);
                                int colonyIndex = Mathf.Clamp(Mathf.FloorToInt(flowerColonyNoise * WildflowerPalettes.Length), 0, WildflowerPalettes.Length - 1);
                                accentColor = (rng.Range(0f, 1f) < 0.85f)
                                    ? WildflowerPalettes[colonyIndex]
                                    : WildflowerPalettes[rng.Range(0, WildflowerPalettes.Length)];
                            }
                            else if (roll < (wildflowerRatio + lushGrassRatio * 0.40f) && !inCombatClearing && !inSanctuaryClearing)
                            {
                                chosenMesh = lushMesh;
                            }
                            else
                            {
                                // Base homogênea predominante no estilo Genshin/Zelda: tapete contínuo denso
                                chosenMesh = carpetMesh;
                            }
                        }
                    }

                    Color terrainColor = EvaluateTerrainColor(groundY, slope, normalWS, config);
                    if (canopyWeight > 0.05f)
                    {
                        terrainColor = Color.Lerp(terrainColor, config.deepGrassColor, canopyWeight * 0.45f);
                    }

                    // Cor da ponta contínua no espaço de mundo (sem jitter aleatório por tufo)
                    Color tipColor = EvaluateFoliageTipColor(worldX, worldZ, canopyWeight, config);

                    // Escala base com transição suave (smoothstep) na borda da campina para fusão orgânica
                    float edgeScale = isInsideMeadow ? Mathf.SmoothStep(0.65f, 1.15f, patchWeight) : 0.65f;
                    float scale = edgeScale * (1.0f + rng.Range(-0.06f, 0.06f));

                    if (chosenMesh == lushMesh) scale *= 1.10f;
                    if (chosenMesh == carpetMesh) scale *= 1.08f;
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

            // Agrupamento em moitas / bosques naturais (clusters de 2 a 4 arbustos)
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

                // Spawna os arbustos do cluster/moita ao redor do centro
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

                    // Escolha do arquétipo de arbusto
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
            List<int> combinedTris)
        {
            Vector3[] baseVerts = templateMesh.vertices;
            Vector3[] baseNormals = templateMesh.normals;
            Vector2[] baseUVs = templateMesh.uv;
            Color[] baseColors = templateMesh.colors;
            int[] baseTris = templateMesh.triangles;

            int vertCount = baseVerts.Length;
            int triCount = baseTris.Length;
            int startVertIndex = combinedVerts.Count;

            // Converte a normal da superfície do relevo para o espaço local do chunk
            Vector3 localSurfaceNormal = transform.InverseTransformDirection(surfaceNormal).normalized;
            Vector3 grassAlignedNormal = Vector3.Lerp(localSurfaceNormal, Vector3.up, 0.40f).normalized;

            for (int v = 0; v < vertCount; v++)
            {
                Vector3 lv = rot * (baseVerts[v] * scale) + localPos;

                Vector3 ln;
                if (isGrass)
                {
                    // Alinha predominantemente com a normal do relevo / Vector3.up (estilo Genshin / Zelda)
                    Vector3 baseRotNormal = rot * baseNormals[v];
                    ln = (baseVerts[v].y > 0.45f)
                        ? Vector3.Lerp(grassAlignedNormal, baseRotNormal, 0.12f).normalized
                        : grassAlignedNormal;
                }
                else
                {
                    // Arbustos preservam normais esféricas anime de puff
                    ln = rot * baseNormals[v];
                }

                combinedVerts.Add(lv);
                combinedNormals.Add(ln);

                float rawU = baseUVs[v].x;
                float rawV = baseUVs[v].y;

                if (hasAccent && rawU >= 2.0f)
                {
                    combinedUVs.Add(new Vector2(rawU - 2.0f, rawV));
                    Color petColor = accentColor;
                    petColor.a = baseColors[v].a;
                    combinedColors.Add(petColor);
                }
                else
                {
                    combinedUVs.Add(baseUVs[v]);
                    float hFactor = rawV;
                    Color vertColor = Color.Lerp(terrainColor, tipColor, hFactor * 0.88f);
                    vertColor.a = baseColors[v].a;
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
            // Campo de matiz contínuo em coordenadas de mundo (escala macro de 50 metros)
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
