using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Duskborn.Gameplay.World;
using Unity.AI.Navigation;

[RequireComponent(typeof(NavMeshSurface))]
public class ChunkGridManager : MonoBehaviour
{
    [Header("Configuração Ativa")]
    [Tooltip("Asset ScriptableObject contendo todos os parâmetros da grade e geração do relevo.")]
    public LowPolyTerrainConfig config;

    [Header("Props & Recursos (World Props)")]
    [Tooltip("Configuração procedural de árvores, pedras, ferro, baús e clareira central.")]
    public WorldPropsConfig propsConfig;

    [Tooltip("Referência opcional ao WorldPropsPlacer. Se vazio, buscará automaticamente neste GameObject.")]
    public WorldPropsPlacer propsPlacer;

    [Header("Controle de Semente (Seed)")]
    [Tooltip("Se ativado, gera uma nova semente numérica aleatória a cada clique de geração.")]
    public bool useRandomSeed = false;

    [Tooltip("Semente específica utilizada para gerar o relevo quando 'useRandomSeed' estiver desativado.")]
    public int customSeed = 4242;

    [Tooltip("Semente específica utilizada para os props. Se 0, utiliza a mesma semente do relevo.")]
    public int propsSeed = 0;

    public static ChunkGridManager Instance { get; private set; }

    public int ActiveSeed => customSeed;
    public int ActivePropsSeed => propsSeed != 0 ? propsSeed : customSeed;

    private void Awake()
    {
        Instance = this;
        EnsureNavMeshSurface();
        if (propsPlacer == null)
        {
            propsPlacer = GetComponent<WorldPropsPlacer>();
            if (propsPlacer == null)
            {
                propsPlacer = gameObject.AddComponent<WorldPropsPlacer>();
            }
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnValidate()
    {
        if (navMeshSurface == null)
        {
            navMeshSurface = GetComponent<NavMeshSurface>();
        }
        if (propsPlacer == null)
        {
            propsPlacer = GetComponent<WorldPropsPlacer>();
        }
#if UNITY_EDITOR
        if (vertexColorMaterial == null)
        {
            vertexColorMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/_Duskborn/Art/Materials/M_TerrainLowPoly.mat");
        }
        if (waterMaterial == null)
        {
            waterMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/_Duskborn/Art/Materials/M_WaterLowPoly.mat");
        }
        if (foliageGrassMaterial == null)
        {
            foliageGrassMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/_Duskborn/Art/Materials/M_Foliage_Grass.mat");
        }
        if (foliageBushMaterial == null)
        {
            foliageBushMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/_Duskborn/Art/Materials/M_Foliage_Bush.mat");
        }
        UpdateWaterPlanePosition();
#endif
    }

    [Header("Renderização & Material")]
    [Tooltip("Material que suporte cores de vértice (ex: M_TerrainLowPoly com shader Duskborn/LowPolyTerrainVertexColor).")]
    public Material vertexColorMaterial;

    [Header("Água Estilizada (Low-Poly Water)")]
    [Tooltip("Se ativado, cria e posiciona automaticamente um plano de água estilizado no nível 'waterLevel'.")]
    public bool generateWaterPlane = true;

    [Tooltip("Ajuste fino de altura do plano de água (soma ao waterLevel do config). Permite calibrar a altura visual em tempo real.")]
    public float waterHeightOffset = 0f;

    [Tooltip("Material de água estilizada (ex: M_WaterLowPoly com shader Duskborn/LowPolyWater).")]
    public Material waterMaterial;

    [Tooltip("Subdivisões do plano de água (número de quads por lado para permitir animação e relevo das facetas low-poly).")]
    [Range(16, 128)] public int waterSubdivisions = 64;

    public float EffectiveWaterLevel => config != null ? (config.waterLevel + waterHeightOffset) : waterHeightOffset;

    [Header("Atmosfera & FX do Mundo")]
    [Tooltip("Se ativado, cria ou atualiza um objeto com WorldAtmosphereController para partículas atmosféricas orgânicas.")]
    public bool generateAtmosphereFX = true;

    [Header("Vegetação Estilizada (Stylized Foliage)")]
    [Tooltip("Se ativado, gera automaticamente tufos de grama e arbustos com cores mescladas ao relevo por chunk.")]
    public bool generateFoliage = true;

    [Tooltip("Material para os tufos de grama (shader Duskborn/StylizedFoliage).")]
    public Material foliageGrassMaterial;

    [Tooltip("Material para os arbustos estilizados (shader Duskborn/StylizedFoliage).")]
    public Material foliageBushMaterial;

    [Header("Navegação & AI (NavMesh)")]
    [Tooltip("Referência opcional ao NavMeshSurface. Se vazio, buscará automaticamente neste GameObject ou na cena.")]
    public NavMeshSurface navMeshSurface;

    private readonly Dictionary<Vector2Int, TerrainChunk> loadedChunks = new Dictionary<Vector2Int, TerrainChunk>();

    private void Start()
    {
        if (config == null) return;

        var existingChunks = GetComponentsInChildren<TerrainChunk>(true);
        if (existingChunks != null && existingChunks.Length > 0 && !useRandomSeed)
        {
            // O terreno já foi gerado na cena pelo Editor
            foreach (var chunk in existingChunks)
            {
                if (chunk != null)
                    loadedChunks[chunk.ChunkCoord] = chunk;
            }
            Debug.Log($"[ChunkGridManager] Terreno carregado da cena ({loadedChunks.Count} chunks).");

            if (generateWaterPlane && transform.Find("WaterPlane") == null)
            {
                GenerateWaterPlane();
            }

            if (generateAtmosphereFX && transform.Find("WorldAtmosphere") == null)
            {
                EnsureAtmosphereFX();
            }

            if (generateFoliage)
            {
                GenerateFoliage(ActiveSeed);
            }

            // Se os chunks existem mas os props ainda não foram gerados, gera-os
            if (propsPlacer != null && propsConfig != null && propsPlacer.PropsCount == 0)
            {
                GenerateProps(ActivePropsSeed);
            }
        }
        else
        {
            GenerateGrid();
        }
    }

    [ContextMenu("Regerar Grid de Terreno")]
    public void GenerateGrid()
    {
        ClearGrid();

        if (config == null)
        {
            Debug.LogError("[ChunkGridManager] Nenhuma configuração atribuída!");
            return;
        }

        if (useRandomSeed)
        {
            customSeed = Random.Range(1, 9999999);
            propsSeed = Random.Range(1, 9999999);
        }

        int activeSeed = customSeed;
        int activePropsSeed = propsSeed != 0 ? propsSeed : activeSeed;

        int startX = -config.chunksX / 2;
        int startZ = -config.chunksZ / 2;
        float chunkWorldLength = config.chunkSize * config.cellSize;

        for (int cz = startZ; cz < startZ + config.chunksZ; cz++)
        {
            for (int cx = startX; cx < startX + config.chunksX; cx++)
            {
                Vector2Int coord = new Vector2Int(cx, cz);
                Vector3 worldPos = new Vector3(cx * chunkWorldLength, 0f, cz * chunkWorldLength);

                GameObject chunkGO = new GameObject($"Chunk_{cx}_{cz}");
                chunkGO.transform.parent = transform;
                chunkGO.transform.position = worldPos;

                MeshRenderer renderer = chunkGO.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = vertexColorMaterial;

                TerrainChunk chunk = chunkGO.AddComponent<TerrainChunk>();
                chunk.Initialize(coord, config, activeSeed);

                loadedChunks[coord] = chunk;
            }
        }

        // 2. Plano de Água Estilizada (Low-Poly Water)
        if (generateWaterPlane)
        {
            GenerateWaterPlane();
        }
        else
        {
            RemoveWaterPlane();
        }

        // 3. Atmosfera & Partículas Ambientais (Pólen diurno / Vaga-lumes noturnos)
        if (generateAtmosphereFX)
        {
            EnsureAtmosphereFX();
        }
        else
        {
            RemoveAtmosphereFX();
        }

        // 4. Spawna nós de recursos, baús e clareiras (Popula o SpatialOccupancyMap)
        GenerateProps(activePropsSeed);

        // 5. Folhagem Estilizada (Grama e Arbustos Procedurais respeitando o SpatialOccupancyMap)
        if (generateFoliage)
        {
            GenerateFoliage(activeSeed);
        }
        else
        {
            ClearFoliage();
        }

        // 6. Baka o NavMesh englobando a malha do terreno e os colliders dos props
        RebuildNavMesh();
        Debug.Log($"[ChunkGridManager] Grid {config.chunksX}x{config.chunksZ} gerada com sucesso com TerrainSeed={activeSeed}, PropsSeed={activePropsSeed} ({loadedChunks.Count} chunks).");
    }

    [ContextMenu("Regerar Apenas Folhagem")]
    public void RegenerateFoliageOnly()
    {
        GenerateFoliage(ActiveSeed);
    }

    public void GenerateFoliage(int seed)
    {
        if (config == null) return;

        var existingChunks = GetComponentsInChildren<TerrainChunk>(true);
        if (existingChunks == null || existingChunks.Length == 0) return;

        SpatialOccupancyMap occupancyMap = propsPlacer != null ? propsPlacer.OccupancyMap : null;

        foreach (var chunk in existingChunks)
        {
            if (chunk == null) continue;

            var placer = chunk.GetComponent<Duskborn.Gameplay.World.Foliage.ChunkFoliagePlacer>();
            if (placer == null)
            {
                placer = chunk.gameObject.AddComponent<Duskborn.Gameplay.World.Foliage.ChunkFoliagePlacer>();
            }

            placer.SetMaterials(foliageGrassMaterial, foliageBushMaterial);

            if (generateFoliage)
            {
                placer.GenerateFoliage(config, seed, occupancyMap);
            }
            else
            {
                placer.ClearFoliage();
            }
        }
    }

    [ContextMenu("Limpar Folhagem")]
    public void ClearFoliage()
    {
        var existingChunks = GetComponentsInChildren<TerrainChunk>(true);
        if (existingChunks == null || existingChunks.Length == 0) return;

        foreach (var chunk in existingChunks)
        {
            if (chunk == null) continue;
            var placer = chunk.GetComponent<Duskborn.Gameplay.World.Foliage.ChunkFoliagePlacer>();
            if (placer != null)
            {
                placer.ClearFoliage();
            }
        }
    }

    [ContextMenu("Regerar Apenas Props")]
    public void RegeneratePropsOnly()
    {
        RegeneratePropsOnly(useRandomSeed);
    }

    public void RegeneratePropsOnly(bool forceRandomSeed)
    {
        // 1. Verifica se existem chunks de terreno na cena. Se não houver, gera a grid completa
        var existingChunks = GetComponentsInChildren<TerrainChunk>(true);
        if (existingChunks == null || existingChunks.Length == 0)
        {
            Debug.LogWarning("[ChunkGridManager] Nenhum chunk de terreno encontrado para posicionar props. Gerando o terreno completo...");
            GenerateGrid();
            return;
        }

        if (forceRandomSeed)
        {
            propsSeed = Random.Range(1, 9999999);
        }

        int activePropsSeed = propsSeed != 0 ? propsSeed : customSeed;

        ClearProps();
        GenerateProps(activePropsSeed);

        // Se a folhagem estiver ativada, regera para contornar perfeitamente os novos props e árvores
        if (generateFoliage)
        {
            GenerateFoliage(ActiveSeed);
        }

        RebuildNavMesh();

        Debug.Log($"[ChunkGridManager] Props e folhagem regerados com sucesso no mesmo relevo com PropsSeed={activePropsSeed}.");
    }

    private void GenerateProps(int activeSeed)
    {
        if (propsPlacer == null)
        {
            propsPlacer = GetComponent<WorldPropsPlacer>();
            if (propsPlacer == null)
            {
                propsPlacer = gameObject.AddComponent<WorldPropsPlacer>();
            }
        }

        if (propsPlacer != null && propsConfig != null)
        {
            propsPlacer.PlaceWorldProps(config, propsConfig, activeSeed);
        }
    }

    public void ClearProps()
    {
        if (propsPlacer == null)
        {
            propsPlacer = GetComponent<WorldPropsPlacer>();
            if (propsPlacer == null)
            {
                propsPlacer = gameObject.AddComponent<WorldPropsPlacer>();
            }
        }

        if (propsPlacer != null)
        {
            propsPlacer.ClearProps();
        }
    }

    public void ClearGrid()
    {
        ClearFoliage();
        ClearProps();
        loadedChunks.Clear();

        var existingChunks = GetComponentsInChildren<TerrainChunk>(true);
        for (int i = existingChunks.Length - 1; i >= 0; i--)
        {
            if (existingChunks[i] != null)
            {
                if (Application.isPlaying)
                    Destroy(existingChunks[i].gameObject);
                else
                    DestroyImmediate(existingChunks[i].gameObject);
            }
        }

        RemoveWaterPlane();
        RemoveAtmosphereFX();
    }

    [ContextMenu("Regerar Plano de Água")]
    public void GenerateWaterPlane()
    {
        if (!generateWaterPlane || config == null)
        {
            RemoveWaterPlane();
            return;
        }

        Transform existing = transform.Find("WaterPlane");
        GameObject waterGO;
        if (existing != null)
        {
            waterGO = existing.gameObject;
        }
        else
        {
            waterGO = new GameObject("WaterPlane");
            waterGO.transform.parent = transform;
        }

        waterGO.transform.position = new Vector3(0f, EffectiveWaterLevel, 0f);
        waterGO.transform.rotation = Quaternion.identity;
        waterGO.transform.localScale = Vector3.one;

        int waterLayer = LayerMask.NameToLayer("Water");
        if (waterLayer >= 0)
        {
            waterGO.layer = waterLayer;
        }

        NavMeshModifier navMod = waterGO.GetComponent<NavMeshModifier>();
        if (navMod == null) navMod = waterGO.AddComponent<NavMeshModifier>();
        navMod.ignoreFromBuild = true;

        MeshFilter mf = waterGO.GetComponent<MeshFilter>();
        if (mf == null) mf = waterGO.AddComponent<MeshFilter>();

        MeshRenderer mr = waterGO.GetComponent<MeshRenderer>();
        if (mr == null) mr = waterGO.AddComponent<MeshRenderer>();

        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = true;

        if (waterMaterial != null)
        {
            mr.sharedMaterial = waterMaterial;
        }
        else
        {
#if UNITY_EDITOR
            waterMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/_Duskborn/Art/Materials/M_WaterLowPoly.mat");
            if (waterMaterial != null)
            {
                mr.sharedMaterial = waterMaterial;
            }
#endif
            if (mr.sharedMaterial == null)
            {
                Shader waterShader = Shader.Find("Duskborn/LowPolyWater");
                if (waterShader != null)
                {
                    Material fallbackMat = new Material(waterShader);
                    fallbackMat.name = "M_WaterLowPoly_Runtime";
                    mr.sharedMaterial = fallbackMat;
                }
            }
        }

        float totalWidth = config.chunksX * config.chunkSize * config.cellSize * 1.5f;
        float totalLength = config.chunksZ * config.chunkSize * config.cellSize * 1.5f;

        mf.sharedMesh = CreateWaterPlaneMesh(totalWidth, totalLength, waterSubdivisions);
    }

    public void UpdateWaterPlanePosition()
    {
        Transform water = transform.Find("WaterPlane");
        if (water != null)
        {
            water.position = new Vector3(0f, EffectiveWaterLevel, 0f);
        }
    }

    public void RemoveWaterPlane()
    {
        Transform existing = transform.Find("WaterPlane");
        if (existing != null)
        {
            if (Application.isPlaying)
                Destroy(existing.gameObject);
            else
                DestroyImmediate(existing.gameObject);
        }
    }

    private Mesh CreateWaterPlaneMesh(float width, float length, int subdivisions)
    {
        Mesh mesh = new Mesh();
        mesh.name = "LowPolyWater_Mesh";

        int resX = Mathf.Clamp(subdivisions, 2, 120);
        int resZ = Mathf.Clamp(subdivisions, 2, 120);

        int vertCount = (resX + 1) * (resZ + 1);
        Vector3[] vertices = new Vector3[vertCount];
        Vector3[] normals = new Vector3[vertCount];
        Vector2[] uvs = new Vector2[vertCount];

        float halfW = width * 0.5f;
        float halfL = length * 0.5f;

        int vi = 0;
        for (int z = 0; z <= resZ; z++)
        {
            float tz = (float)z / resZ;
            float pz = Mathf.Lerp(-halfL, halfL, tz);

            for (int x = 0; x <= resX; x++)
            {
                float tx = (float)x / resX;
                float px = Mathf.Lerp(-halfW, halfW, tx);

                vertices[vi] = new Vector3(px, 0f, pz);
                normals[vi] = Vector3.up;
                uvs[vi] = new Vector2(tx, tz);
                vi++;
            }
        }

        int[] triangles = new int[resX * resZ * 6];
        int ti = 0;
        for (int z = 0; z < resZ; z++)
        {
            for (int x = 0; x < resX; x++)
            {
                int current = z * (resX + 1) + x;
                int next = current + resX + 1;

                // Triângulo 1 (sentido horário olhando de cima / normal +Y)
                triangles[ti++] = current;
                triangles[ti++] = next;
                triangles[ti++] = next + 1;

                // Triângulo 2 (sentido horário olhando de cima / normal +Y)
                triangles[ti++] = current;
                triangles[ti++] = next + 1;
                triangles[ti++] = current + 1;
            }
        }

        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    public void EnsureAtmosphereFX()
    {
        Transform existing = transform.Find("WorldAtmosphere");
        if (existing == null)
        {
            GameObject atmoGO = new GameObject("WorldAtmosphere");
            atmoGO.transform.parent = transform;
            atmoGO.AddComponent<WorldAtmosphereController>();
        }
    }

    public void RemoveAtmosphereFX()
    {
        Transform existing = transform.Find("WorldAtmosphere");
        if (existing != null)
        {
            if (Application.isPlaying)
                Destroy(existing.gameObject);
            else
                DestroyImmediate(existing.gameObject);
        }
    }

    /// <summary>
    /// Garante que haja um componente NavMeshSurface válido e devidamente configurado neste GameObject.
    /// </summary>
    public NavMeshSurface EnsureNavMeshSurface()
    {
        if (navMeshSurface == null)
        {
            navMeshSurface = GetComponent<NavMeshSurface>();
            if (navMeshSurface == null)
            {
                navMeshSurface = FindAnyObjectByType<NavMeshSurface>();
            }
            if (navMeshSurface == null)
            {
                navMeshSurface = gameObject.AddComponent<NavMeshSurface>();
            }
        }

        if (navMeshSurface != null)
        {
            navMeshSurface.collectObjects = CollectObjects.All;
            navMeshSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;

            // Exclui camadas não-andáveis ou de entidades dinâmicas (Player, Inimigos, UI, TransparentFX, Água)
            int excludeLayers = 0;
            string[] excludedLayerNames = { "Player", "Enemy", "UI", "TransparentFX", "Water" };
            foreach (string layerName in excludedLayerNames)
            {
                int layer = LayerMask.NameToLayer(layerName);
                if (layer >= 0)
                {
                    excludeLayers |= (1 << layer);
                }
            }
            navMeshSurface.layerMask = ~excludeLayers;
        }

        return navMeshSurface;
    }

    [ContextMenu("Rebuild NavMesh")]
    public void RebuildNavMesh()
    {
        EnsureNavMeshSurface();

        if (navMeshSurface != null)
        {
            navMeshSurface.BuildNavMesh();
            Debug.Log("[ChunkGridManager] NavMeshSurface baked com sucesso.");
        }
        else
        {
            Debug.LogWarning("[ChunkGridManager] Nenhum NavMeshSurface encontrado para assar o NavMesh.");
        }
    }
}
