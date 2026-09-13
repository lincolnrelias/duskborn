using System.Collections.Generic;
using UnityEngine;
using Duskborn.Core;
using Unity.AI.Navigation;

namespace Duskborn.Gameplay.World.Foliage
{
    /// <summary>
    /// Gerenciador de folhagem procedurais por chunk.
    /// Gera tufos de grama e arbustos estilizados respeitando biomas,
    /// mesclando a cor da raiz perfeitamente à cor do terreno poligonal,
    /// e combinando os tufos em um mesh consolidado por chunk para desempenho ultra-alto (1 draw call por chunk).
    /// </summary>
    [RequireComponent(typeof(TerrainChunk))]
    public class ChunkFoliagePlacer : MonoBehaviour
    {
        [Header("Materiais de Folhagem")]
        [Tooltip("Material para os tufos de grama (shader Duskborn/StylizedFoliage).")]
        [SerializeField] private Material grassMaterial;

        [Tooltip("Material para os arbustos estilizados (shader Duskborn/StylizedFoliage).")]
        [SerializeField] private Material bushMaterial;

        [Header("Densidade por Chunk")]
        [Tooltip("Quantidade de tufos de grama gerados por chunk.")]
        [Range(0, 100)]
        [SerializeField] private int grassTuftsPerChunk = 36;

        [Tooltip("Quantidade de arbustos gerados por chunk.")]
        [Range(0, 20)]
        [SerializeField] private int bushesPerChunk = 6;

        [Header("Controle de Bioma e Filtros")]
        [Tooltip("Deslocamento mínimo acima da água para permitir vegetação.")]
        [SerializeField] private float waterClearance = 0.65f;

        private TerrainChunk _terrainChunk;
        private GameObject _grassHolder;
        private GameObject _bushHolder;

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

            // 1. Gera e Combina Tufos de Grama (Grass Batch)
            if (grassTuftsPerChunk > 0 && grassMaterial != null)
            {
                Mesh grassBaseMesh = FoliageMeshUtility.CreateGrassTuftMesh(bladeCount: 5, height: 0.95f, baseWidth: 0.16f);
                BuildFoliageBatch(
                    "Foliage_GrassBatch",
                    grassBaseMesh,
                    grassMaterial,
                    grassTuftsPerChunk,
                    config,
                    seed,
                    rng,
                    minX, maxX, minZ, maxZ,
                    halfMapX, halfMapZ,
                    centerRadiusSqr,
                    minScale: 0.75f, maxScale: 1.35f,
                    isGrass: true,
                    occupancyMap: occupancyMap
                );
            }

            // 2. Gera e Combina Arbustos Estilizados (Bush Batch)
            if (bushesPerChunk > 0 && bushMaterial != null)
            {
                Mesh bushBaseMesh = FoliageMeshUtility.CreateBushMesh(lobes: 4, baseRadius: 0.65f, height: 1.0f);
                BuildFoliageBatch(
                    "Foliage_BushBatch",
                    bushBaseMesh,
                    bushMaterial,
                    bushesPerChunk,
                    config,
                    seed,
                    rng,
                    minX, maxX, minZ, maxZ,
                    halfMapX, halfMapZ,
                    centerRadiusSqr,
                    minScale: 0.85f, maxScale: 1.45f,
                    isGrass: false,
                    occupancyMap: occupancyMap
                );
            }
        }

        private void BuildFoliageBatch(
            string holderName,
            Mesh templateMesh,
            Material material,
            int targetCount,
            LowPolyTerrainConfig config,
            int activeSeed,
            SeededRNG rng,
            float minX, float maxX, float minZ, float maxZ,
            float halfMapX, float halfMapZ,
            float centerRadiusSqr,
            float minScale, float maxScale,
            bool isGrass,
            SpatialOccupancyMap occupancyMap)
        {
            Vector3[] baseVerts = templateMesh.vertices;
            Vector3[] baseNormals = templateMesh.normals;
            Vector2[] baseUVs = templateMesh.uv;
            Color[] baseColors = templateMesh.colors;
            int[] baseTris = templateMesh.triangles;

            int vertTemplateCount = baseVerts.Length;
            int triTemplateCount = baseTris.Length;

            List<Vector3> combinedVerts = new List<Vector3>();
            List<Vector3> combinedNormals = new List<Vector3>();
            List<Vector2> combinedUVs = new List<Vector2>();
            List<Color> combinedColors = new List<Color>();
            List<int> combinedTris = new List<int>();

            int maxAttempts = targetCount * 8;
            int placedCount = 0;

            for (int attempt = 0; attempt < maxAttempts && placedCount < targetCount; attempt++)
            {
                float worldX = rng.Range(minX + 0.5f, maxX - 0.5f);
                float worldZ = rng.Range(minZ + 0.5f, maxZ - 0.5f);
                Vector2 worldXZ = new Vector2(worldX, worldZ);

                // Evita clareira central
                float distSqr = worldX * worldX + worldZ * worldZ;
                if (distSqr < centerRadiusSqr) continue;

                // 1. Verificação de Ocupação Espacial (SpatialOccupancyMap)
                float canopyWeight = 0f;
                if (occupancyMap != null)
                {
                    // Rejeita se estiver dentro do raio sólido de obstáculos físicos (troncos de árvores, rochas, etc.)
                    float clearance = isGrass ? 0.20f : 0.45f;
                    if (occupancyMap.IsSolidOccupied(worldXZ, clearance))
                    {
                        continue;
                    }

                    // Clareiras protegidas:
                    if (occupancyMap.IsInClearing(worldXZ, out OccupancyType clType))
                    {
                        if (clType == OccupancyType.Player_Sanctuary)
                        {
                            // Santuário: zero arbustos, grama muito esparsa
                            if (!isGrass) continue;
                            if (rng.Range(0f, 1f) > 0.25f) continue;
                        }
                        else if (clType == OccupancyType.Combat_Clearing)
                        {
                            // Arena de combate: zero arbustos obstrutivos, grama rasteira limpa
                            if (!isGrass) continue;
                            if (rng.Range(0f, 1f) > 0.5f) continue;
                        }
                    }

                    // Verificação de influência da copa das árvores
                    bool underCanopy = occupancyMap.IsUnderCanopy(worldXZ, out canopyWeight);

                    // Adensamento de vegetação sombreada:
                    // Arbustos preferem 50% mais as margens de copas de árvores do que descampados abertos
                    if (!underCanopy && !isGrass)
                    {
                        if (rng.Range(0f, 1f) > 0.65f) continue;
                    }
                }

                // Amostra altura exata do terreno
                float groundY = TerrainNoise.SampleHeight(worldX, worldZ, config, activeSeed, halfMapX, halfMapZ);

                // Filtro 1: Não spawnar debaixo d'água ou em praias muito baixas
                float minGroundH = config.waterLevel + waterClearance;
                if (groundY < minGroundH) continue;

                // Filtro 2: Não spawnar em picos nevados ou rocha pura
                float maxFoliageH = config.heightMultiplier * 0.72f;
                if (groundY > maxFoliageH) continue;

                // Amostra inclinação aproximada do terreno por gradiente finito
                const float eps = 0.35f;
                float hX1 = TerrainNoise.SampleHeight(worldX + eps, worldZ, config, activeSeed, halfMapX, halfMapZ);
                float hX0 = TerrainNoise.SampleHeight(worldX - eps, worldZ, config, activeSeed, halfMapX, halfMapZ);
                float hZ1 = TerrainNoise.SampleHeight(worldX, worldZ + eps, config, activeSeed, halfMapX, halfMapZ);
                float hZ0 = TerrainNoise.SampleHeight(worldX, worldZ - eps, config, activeSeed, halfMapX, halfMapZ);

                Vector3 normalWS = new Vector3(-(hX1 - hX0) / (2f * eps), 1f, -(hZ1 - hZ0) / (2f * eps)).normalized;
                float slope = Vector3.Angle(normalWS, Vector3.up);

                // Evita penhascos e paredões íngremes
                if (slope > config.steepSlopeThreshold * 0.82f) continue;

                // 2. Cor do Bioma Subjacente (Calculada da mesma forma que TerrainChunk)
                Color terrainColor = EvaluateTerrainColor(groundY, slope, normalWS, config);

                // Se estiver sob copa sombreada, escurece a base simulando terra úmida de floresta
                if (canopyWeight > 0.05f)
                {
                    terrainColor = Color.Lerp(terrainColor, config.deepGrassColor, canopyWeight * 0.45f);
                }

                // Rotação aleatória em Y e escala
                float scale = rng.Range(minScale, maxScale);
                if (canopyWeight > 0.1f)
                {
                    // Tufos sob copa são mais viçosos (+15% a +25%)
                    scale *= (1.0f + canopyWeight * 0.22f);
                }

                float yRot = rng.Range(0f, 360f);
                Quaternion rot = Quaternion.Euler(0f, yRot, 0f);

                // Posição local no chunk (já que o batch ficará como filho de transform do chunk)
                Vector3 instanceLocalPos = transform.InverseTransformPoint(new Vector3(worldX, groundY, worldZ));

                int startVertIndex = combinedVerts.Count;

                Color targetTipColor = canopyWeight > 0.1f
                    ? Color.Lerp(config.grassColor, config.deepGrassColor, canopyWeight * 0.35f)
                    : config.grassColor;

                // Copia e transforma vértices para o batch consolidado
                for (int v = 0; v < vertTemplateCount; v++)
                {
                    Vector3 localV = rot * (baseVerts[v] * scale) + instanceLocalPos;
                    Vector3 localN = rot * baseNormals[v];

                    combinedVerts.Add(localV);
                    combinedNormals.Add(localN);
                    combinedUVs.Add(baseUVs[v]);

                    // Mescla a cor de vértice: 
                    // Base do tufo (uv.y=0) recebe a cor do terreno local
                    // Topo (uv.y=1) recebe verde vibrante ou verde florestal sombreado
                    float hFactor = baseUVs[v].y;
                    Color vertColor = Color.Lerp(terrainColor, targetTipColor, hFactor * 0.85f);
                    vertColor.a = baseColors[v].a; // Peso do vento no canal Alpha

                    combinedColors.Add(vertColor);
                }

                for (int t = 0; t < triTemplateCount; t++)
                {
                    combinedTris.Add(startVertIndex + baseTris[t]);
                }

                placedCount++;
            }

            if (combinedVerts.Count == 0) return;

            // Cria o GameObject que segura a malha consolidada deste chunk
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
            combinedMesh.name = $"{holderName}_{_terrainChunk.ChunkCoord.x}_{_terrainChunk.ChunkCoord.y}";
            combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            combinedMesh.SetVertices(combinedVerts);
            combinedMesh.SetNormals(combinedNormals);
            combinedMesh.SetUVs(0, combinedUVs);
            combinedMesh.SetColors(combinedColors);
            combinedMesh.SetTriangles(combinedTris, 0);
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
