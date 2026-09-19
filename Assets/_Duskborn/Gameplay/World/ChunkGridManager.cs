using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Duskborn.Gameplay.World;
using Duskborn.UI;
using Duskborn.Core;
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
        FishNet.Component.Spawning.PlayerSpawner.IsWorldReadyChecker = () => Instance == null || Instance.IsWorldReady;
        EnsureNavMeshSurface();
        if (propsPlacer == null)
        {
            propsPlacer = GetComponent<WorldPropsPlacer>();
            if (propsPlacer == null)
            {
                propsPlacer = gameObject.AddComponent<WorldPropsPlacer>();
            }
        }

        // Se o terreno já foi gerado na cena pelo Editor e não viemos do Menu Principal, pula o carregamento
        CheckAndApplyExistingTerrain();
    }

    private void OnDestroy()
    {
        FishNet.Component.Spawning.PlayerSpawner.IsWorldReadyChecker = null;
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
        EnsureFoliageMaterials();
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
    public bool generateAtmosphereFX = false;

    [Header("Vegetação Estilizada (Stylized Foliage)")]
    [Tooltip("Se ativado, gera automaticamente tufos de grama e arbustos com cores mescladas ao relevo por chunk.")]
    public bool generateFoliage = true;

    [Tooltip("Material para os tufos de grama (shader Duskborn/StylizedFoliage).")]
    public Material foliageGrassMaterial;

    [Tooltip("Material para os arbustos estilizados (shader Duskborn/StylizedFoliage).")]
    public Material foliageBushMaterial;

    [Tooltip("Nível de densidade da vegetação procedural (estilo Genshin / Zelda).")]
    public Duskborn.Gameplay.World.Foliage.FoliageDensityPreset foliageDensityPreset = Duskborn.Gameplay.World.Foliage.FoliageDensityPreset.High;

    [Header("Navegação & AI (NavMesh)")]
    [Tooltip("Referência opcional ao NavMeshSurface. Se vazio, buscará automaticamente neste GameObject ou na cena.")]
    public NavMeshSurface navMeshSurface;

    [Header("Carregamento Assíncrono & Pipeline")]
    [Tooltip("Se ativado, exibe a tela de carregamento procedural estilizada inspirada no Minecraft durante a geração.")]
    public bool showLoadingScreen = true;

    [Tooltip("Se ativado, sempre limpa chunks antigos da cena e força uma nova geração procedural com tela de carregamento ao dar Play.")]
    public bool forceRegenerateOnPlay = false;

    [Tooltip("Se ativado, quando o jogo for iniciado diretamente nesta cena no Editor e o terreno já tiver sido gerado, pula completamente o carregamento para testes instantâneos.")]
    public bool skipLoadingIfTerrainExists = true;

    [Tooltip("Orçamento máximo de processamento por quadro em milissegundos (ex: 8ms para 60+ FPS constante).")]
    [Range(2f, 20f)] public float frameBudgetMs = 8f;

    public bool IsWorldReady { get; private set; } = false;
    public bool IsGenerating { get; private set; } = false;

    public event System.Action OnWorldGenerationComplete;
    public event System.Action<float, string, string> OnWorldGenerationProgress;

    private readonly Dictionary<Vector2Int, TerrainChunk> loadedChunks = new Dictionary<Vector2Int, TerrainChunk>();

    private void Start()
    {
        if (config == null) return;

        // Se já foi inicializado instantaneamente no Awake através do terreno existente, não faz nada
        if (IsWorldReady) return;

        // Tenta novamente caso algum chunk tenha sido registrado ou criado entre Awake e Start
        if (CheckAndApplyExistingTerrain())
        {
            return;
        }

        // Consome a flag do menu principal caso tenha vindo por lá
        if (Duskborn.UI.MainMenuController.LoadedFromMainMenu)
        {
            Duskborn.UI.MainMenuController.LoadedFromMainMenu = false;
        }

        StartCoroutine(GenerateGridAsync());
    }

    /// <summary>
    /// Busca chunks de terreno pré-existentes na cena com múltiplas estratégias defensivas.
    /// </summary>
    public bool TryFindExistingChunks(out List<TerrainChunk> foundChunks)
    {
        foundChunks = new List<TerrainChunk>();

        // 1. Filhos diretos ou indiretos deste GameObject
        var childChunks = GetComponentsInChildren<TerrainChunk>(true);
        if (childChunks != null && childChunks.Length > 0)
        {
            foreach (var c in childChunks)
            {
                if (c != null && !foundChunks.Contains(c))
                {
                    foundChunks.Add(c);
                }
            }
        }

        // 2. Chunks em qualquer lugar da cena ativa (caso estejam na raiz ou sob outro parent)
        var allChunks = FindObjectsByType<TerrainChunk>(FindObjectsInactive.Include);
        if (allChunks != null && allChunks.Length > 0)
        {
            foreach (var c in allChunks)
            {
                if (c != null && !foundChunks.Contains(c))
                {
                    foundChunks.Add(c);
                }
            }
        }

        // 3. Fallback: procurar GameObjects com prefixo "Chunk_" que sejam filhos deste transform
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child != null && child.name.StartsWith("Chunk_"))
            {
                TerrainChunk tc = child.GetComponent<TerrainChunk>();
                if (tc == null)
                {
                    tc = child.gameObject.AddComponent<TerrainChunk>();
                }
                if (!foundChunks.Contains(tc))
                {
                    foundChunks.Add(tc);
                }
            }
        }

        return foundChunks.Count > 0;
    }

    /// <summary>
    /// Verifica se o terreno existente na cena deve ser aproveitado, ignorando tela de carregamento e geração procedural.
    /// </summary>
    private bool CheckAndApplyExistingTerrain()
    {
        if (forceRegenerateOnPlay)
        {
            return false;
        }

        if (!skipLoadingIfTerrainExists)
        {
            return false;
        }

        if (Duskborn.UI.MainMenuController.LoadedFromMainMenu)
        {
            return false;
        }

        if (TryFindExistingChunks(out var chunks) && chunks.Count > 0)
        {
            UseExistingSceneTerrain(chunks);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Utiliza o terreno pré-gerado existente na cena do Editor, pulando a tela de carregamento e ativando o mundo instantaneamente.
    /// </summary>
    private void UseExistingSceneTerrain(IEnumerable<TerrainChunk> existingChunks)
    {
        loadedChunks.Clear();
        foreach (var chunk in existingChunks)
        {
            if (chunk != null)
            {
                loadedChunks[chunk.ChunkCoord] = chunk;
            }
        }

        if (generateWaterPlane && transform.Find("WaterPlane") == null)
        {
            GenerateWaterPlane();
        }

        if (generateAtmosphereFX && transform.Find("WorldAtmosphere") == null)
        {
            EnsureAtmosphereFX();
        }
        else if (!generateAtmosphereFX)
        {
            RemoveAtmosphereFX();
        }

        if (propsPlacer != null)
        {
            propsPlacer.EnsureSpawnPointsReady(propsConfig);
        }

        // Garante que nenhuma tela de carregamento permaneça ativa ou visível
        var loadingUIs = FindObjectsByType<WorldLoadingScreenUI>(FindObjectsInactive.Include);
        foreach (var ui in loadingUIs)
        {
            if (ui != null)
            {
                ui.gameObject.SetActive(false);
            }
        }

        IsGenerating = false;
        IsWorldReady = true;
        OnWorldGenerationComplete?.Invoke();

        Debug.Log($"[ChunkGridManager] Terreno pré-gerado detectado na cena ({loadedChunks.Count} chunks). Carregamento e geração ignorados para teste instantâneo no Editor.");
    }

    private System.Collections.IEnumerator InitExistingSceneTerrainAsync(TerrainChunk[] existingChunks)
    {
        IsGenerating = true;
        IsWorldReady = false;

        WorldLoadingScreenUI loadingUI = null;
        if (showLoadingScreen && Application.isPlaying)
        {
            loadingUI = WorldLoadingScreenUI.EnsureInstance();
            loadingUI.Show("Despertando o Crepúsculo...", "Sincronizando santuário e terras ancestrais...");
        }

        GenerationBudget budget = new GenerationBudget(frameBudgetMs);

        // Se os chunks existem mas os props ainda não foram gerados, gera-os de forma assíncrona
        if (propsPlacer != null && propsConfig != null && propsPlacer.PropsCount == 0)
        {
            yield return propsPlacer.PlaceWorldPropsAsync(config, propsConfig, ActivePropsSeed, budget, (p, detail) =>
            {
                if (loadingUI != null) loadingUI.UpdateProgress(Mathf.Lerp(0.1f, 0.45f, p), "Semeando Florestas e Jazidas", detail);
            });
        }
        else if (propsPlacer != null)
        {
            propsPlacer.EnsureSpawnPointsReady(propsConfig);
        }

        if (generateFoliage)
        {
            EnsureFoliageMaterials();
            SpatialOccupancyMap occupancyMap = propsPlacer != null ? propsPlacer.OccupancyMap : null;

            for (int i = 0; i < existingChunks.Length; i++)
            {
                var chunk = existingChunks[i];
                if (chunk == null) continue;

                var placer = chunk.GetComponent<Duskborn.Gameplay.World.Foliage.ChunkFoliagePlacer>()
                    ?? chunk.gameObject.AddComponent<Duskborn.Gameplay.World.Foliage.ChunkFoliagePlacer>();

                placer.SetMaterials(foliageGrassMaterial, foliageBushMaterial);
                placer.SetDensityPreset(foliageDensityPreset);

                yield return placer.GenerateFoliageAsync(config, ActiveSeed, occupancyMap, budget);

                float p = Mathf.Lerp(0.45f, 0.95f, (float)(i + 1) / existingChunks.Length);
                if (loadingUI != null) loadingUI.UpdateProgress(p, "Tecendo a Relva e Flores Silvestres", $"Cultivando folhagem sagrada (Chunk {i + 1}/{existingChunks.Length})...");
            }
        }

        IsGenerating = false;
        IsWorldReady = true;
        OnWorldGenerationComplete?.Invoke();

        if (loadingUI != null)
        {
            loadingUI.CompleteAndFadeOut();
        }
    }

    /// <summary>
    /// Pipeline assíncrono de geração de mundo completo em 6 fases com frame-budgeting e tela de carregamento inspirada em Minecraft.
    /// </summary>
    public System.Collections.IEnumerator GenerateGridAsync(System.Action<float, string, string> onProgress = null, System.Action onComplete = null)
    {
        if (config == null)
        {
            Debug.LogError("[ChunkGridManager] Nenhuma configuração atribuída!");
            yield break;
        }

        IsGenerating = true;
        IsWorldReady = false;

        WorldLoadingScreenUI loadingUI = null;
        if (showLoadingScreen && Application.isPlaying)
        {
            loadingUI = WorldLoadingScreenUI.EnsureInstance();
            loadingUI.Show("Construindo Mundo...", "Inicializando sementes do terreno...");
        }

        void Report(float progress, string stage, string detail)
        {
            onProgress?.Invoke(progress, stage, detail);
            OnWorldGenerationProgress?.Invoke(progress, stage, detail);
            if (loadingUI != null)
            {
                loadingUI.UpdateProgress(progress, stage, detail);
            }
        }

        GenerationBudget budget = new GenerationBudget(frameBudgetMs);

        Report(0.02f, "Evocando o Crepúsculo", "Consagrando o ermo e limpando vestígios...");
        ClearGrid();
        if (budget.ShouldYield()) yield return null;

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
        int totalChunks = config.chunksX * config.chunksZ;
        int chunkCounter = 0;

        // FASE 1: Geração de Chunks de Relevo e Colisões (0.05 a 0.38)
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
                chunk.Initialize(coord, config, activeSeed, autoGenerateMesh: false);

                Mesh mesh = chunk.BuildMesh();
                // Pré-cozinha a malha no PhysX para que o colisor não congele o frame
                Physics.BakeMesh(mesh.GetInstanceID(), false);
                chunk.ApplyMesh(mesh);

                loadedChunks[coord] = chunk;
                chunkCounter++;

                float p = Mathf.Lerp(0.05f, 0.38f, (float)chunkCounter / totalChunks);
                Report(p, "Talhando o Relevo dos Titãs", $"Forjando relevo fractal e abismos (Chunk {chunkCounter}/{totalChunks})...");

                if (budget.ShouldYield())
                {
                    yield return null;
                }
            }
        }

        // FASE 2: Superfície de Água Estilizada & Atmosfera (0.38 a 0.44)
        Report(0.40f, "Canalizando Águas e Névoas", "Evocando plano de água ancestral e partículas do crepúsculo...");
        if (generateWaterPlane)
        {
            GenerateWaterPlane();
        }
        else
        {
            RemoveWaterPlane();
        }

        if (generateAtmosphereFX)
        {
            EnsureAtmosphereFX();
        }
        else
        {
            RemoveAtmosphereFX();
        }
        if (budget.ShouldYield()) yield return null;

        // FASE 3: Recursos e Estruturas Procedurais (0.44 a 0.68)
        if (propsPlacer == null)
        {
            propsPlacer = GetComponent<WorldPropsPlacer>() ?? gameObject.AddComponent<WorldPropsPlacer>();
        }

        if (propsPlacer != null && propsConfig != null)
        {
            yield return propsPlacer.PlaceWorldPropsAsync(config, propsConfig, activePropsSeed, budget, (propP, detail) =>
            {
                float p = Mathf.Lerp(0.44f, 0.68f, propP);
                Report(p, "Semeando Florestas e Jazidas", detail);
            });
        }

        // FASE 4: Vegetação e Folhagem Estilizada (0.68 a 0.86)
        if (generateFoliage)
        {
            EnsureFoliageMaterials();
            var chunksForFoliage = GetComponentsInChildren<TerrainChunk>(true);
            SpatialOccupancyMap occupancyMap = propsPlacer != null ? propsPlacer.OccupancyMap : null;

            for (int i = 0; i < chunksForFoliage.Length; i++)
            {
                var chunk = chunksForFoliage[i];
                if (chunk == null) continue;

                var placer = chunk.GetComponent<Duskborn.Gameplay.World.Foliage.ChunkFoliagePlacer>()
                    ?? chunk.gameObject.AddComponent<Duskborn.Gameplay.World.Foliage.ChunkFoliagePlacer>();

                placer.SetMaterials(foliageGrassMaterial, foliageBushMaterial);
                placer.SetDensityPreset(foliageDensityPreset);

                yield return placer.GenerateFoliageAsync(config, activeSeed, occupancyMap, budget);

                float p = Mathf.Lerp(0.68f, 0.86f, (float)(i + 1) / chunksForFoliage.Length);
                Report(p, "Tecendo a Relva e Flores Silvestres", $"Cultivando folhagem sagrada (Chunk {i + 1}/{chunksForFoliage.Length})...");
            }
        }
        else
        {
            ClearFoliage();
        }

        // FASE 5: Malha de Navegação (NavMesh Assíncrono) (0.86 a 0.96)
        yield return RebuildNavMeshAsync((navP, detail) =>
        {
            float p = Mathf.Lerp(0.86f, 0.96f, navP);
            Report(p, "Consagrando Linhas de Navegação", detail);
        });

        // FASE 6: Finalização & Pronto para Lançamento (0.96 a 1.0)
        Report(1.0f, "O Crepúsculo se Revela!", "Santuário ativo. Liberando entrada dos guerreiros...");
        if (propsPlacer != null)
        {
            propsPlacer.EnsureSpawnPointsReady(propsConfig);
        }

        IsGenerating = false;
        IsWorldReady = true;

        OnWorldGenerationComplete?.Invoke();
        onComplete?.Invoke();

        Debug.Log($"[ChunkGridManager] Geração assíncrona da grid {config.chunksX}x{config.chunksZ} concluída com sucesso! (Seed={activeSeed}, PropsSeed={activePropsSeed})");

        if (loadingUI != null)
        {
            loadingUI.CompleteAndFadeOut();
        }
    }

    [ContextMenu("Regerar Grid de Terreno")]
    public void GenerateGrid()
    {
        if (Application.isPlaying)
        {
            StartCoroutine(GenerateGridAsync());
            return;
        }

#if UNITY_EDITOR
        try
        {
            UnityEditor.EditorUtility.DisplayProgressBar("Gerando Terreno Duskborn", "Limpando geometria prévia...", 0.05f);
            ClearGrid();

            if (config == null)
            {
                Debug.LogError("[ChunkGridManager] Nenhuma configuração atribuída!");
                return;
            }

            IsGenerating = true;
            IsWorldReady = false;

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
            int totalChunks = config.chunksX * config.chunksZ;
            int chunkCounter = 0;

            for (int cz = startZ; cz < startZ + config.chunksZ; cz++)
            {
                for (int cx = startX; cx < startX + config.chunksX; cx++)
                {
                    chunkCounter++;
                    float p = Mathf.Lerp(0.1f, 0.5f, (float)chunkCounter / Mathf.Max(1, totalChunks));
                    UnityEditor.EditorUtility.DisplayProgressBar("Gerando Terreno Duskborn", $"Gerando Relevo Low-Poly ({chunkCounter}/{totalChunks})...", p);

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
            UnityEditor.EditorUtility.DisplayProgressBar("Gerando Terreno Duskborn", "Configurando Água e Atmosfera...", 0.55f);
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
            UnityEditor.EditorUtility.DisplayProgressBar("Gerando Terreno Duskborn", "Distribuindo Recursos e Props Procedurais...", 0.7f);
            GenerateProps(activePropsSeed);

            // 5. Folhagem Estilizada (Grama e Arbustos Procedurais respeitando o SpatialOccupancyMap)
            if (generateFoliage)
            {
                UnityEditor.EditorUtility.DisplayProgressBar("Gerando Terreno Duskborn", "Plantando Folhagem e Grama...", 0.85f);
                GenerateFoliage(activeSeed);
            }
            else
            {
                ClearFoliage();
            }

            // 6. Baka o NavMesh englobando a malha do terreno e os colliders dos props
            UnityEditor.EditorUtility.DisplayProgressBar("Gerando Terreno Duskborn", "Calculando NavMesh de Navegação...", 0.95f);
            RebuildNavMesh();

            IsGenerating = false;
            IsWorldReady = true;
            OnWorldGenerationComplete?.Invoke();

            Debug.Log($"[ChunkGridManager] Grid {config.chunksX}x{config.chunksZ} gerada com sucesso com TerrainSeed={activeSeed}, PropsSeed={activePropsSeed} ({loadedChunks.Count} chunks).");
        }
        finally
        {
            UnityEditor.EditorUtility.ClearProgressBar();
        }
#else
        ClearGrid();

        if (config == null)
        {
            Debug.LogError("[ChunkGridManager] Nenhuma configuração atribuída!");
            return;
        }

        IsGenerating = true;
        IsWorldReady = false;

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

        IsGenerating = false;
        IsWorldReady = true;
        OnWorldGenerationComplete?.Invoke();

        Debug.Log($"[ChunkGridManager] Grid {config.chunksX}x{config.chunksZ} gerada com sucesso com TerrainSeed={activeSeed}, PropsSeed={activePropsSeed} ({loadedChunks.Count} chunks).");
#endif
    }

    private void EnsureFoliageMaterials()
    {
        #if UNITY_EDITOR
        if (foliageGrassMaterial == null)
        {
            foliageGrassMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/_Duskborn/Art/Materials/M_Foliage_Grass.mat");
        }
        if (foliageBushMaterial == null)
        {
            foliageBushMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/_Duskborn/Art/Materials/M_Foliage_Bush.mat");
        }
        #endif
        if (foliageGrassMaterial == null)
        {
            foliageGrassMaterial = Resources.Load<Material>("Materials/M_Foliage_Grass");
        }
        if (foliageBushMaterial == null)
        {
            foliageBushMaterial = Resources.Load<Material>("Materials/M_Foliage_Bush");
        }
    }

    [ContextMenu("Regerar Apenas Folhagem")]
    public void RegenerateFoliageOnly()
    {
        EnsureFoliageMaterials();
        GenerateFoliage(ActiveSeed);
    }

    public void GenerateFoliage(int seed)
    {
        if (config == null) return;
        EnsureFoliageMaterials();

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
            placer.SetDensityPreset(foliageDensityPreset);

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
        EnvironmentVisualBootstrapper.EnsureVisualPipeline();
        Transform existing = transform.Find("WorldAtmosphere");
        if (existing == null)
        {
            var atmo = Object.FindAnyObjectByType<WorldAtmosphereController>();
            if (atmo != null)
            {
                atmo.transform.parent = transform;
            }
            else
            {
                GameObject atmoGO = new GameObject("WorldAtmosphere");
                atmoGO.transform.parent = transform;
                atmoGO.AddComponent<WorldAtmosphereController>();
            }
        }
    }

    public void RemoveAtmosphereFX()
    {
        EnvironmentVisualBootstrapper.RemoveWorldAtmosphere();
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

    /// <summary>
    /// Baka o NavMesh de forma assíncrona em worker threads para não travar a main thread.
    /// </summary>
    public System.Collections.IEnumerator RebuildNavMeshAsync(System.Action<float, string> onProgress = null)
    {
        EnsureNavMeshSurface();

        if (navMeshSurface == null)
        {
            onProgress?.Invoke(1.0f, "Nenhum NavMeshSurface disponível.");
            yield break;
        }

        onProgress?.Invoke(0.15f, "Coletando fontes de colisão e malhas...");
        yield return null;

        if (navMeshSurface.navMeshData == null)
        {
            navMeshSurface.navMeshData = new NavMeshData();
        }

        AsyncOperation op = navMeshSurface.UpdateNavMesh(navMeshSurface.navMeshData);
        if (op != null)
        {
            while (!op.isDone)
            {
                onProgress?.Invoke(Mathf.Lerp(0.25f, 0.95f, op.progress), $"Processando malha AI em background ({Mathf.RoundToInt(op.progress * 100)}%)...");
                yield return null;
            }
        }
        else
        {
            navMeshSurface.BuildNavMesh();
        }

        onProgress?.Invoke(1.0f, "Malha de navegação concluída.");
        Debug.Log("[ChunkGridManager] NavMesh assíncrono concluído com sucesso.");
    }
}
