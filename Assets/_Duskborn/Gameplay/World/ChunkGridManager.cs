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
    [Header("Active Configuration")]
    [Tooltip("ScriptableObject asset containing all grid and terrain generation parameters.")]
    public LowPolyTerrainConfig config;

    [Header("Props & Resources (World Props)")]
    [Tooltip("Procedural configuration for trees, rocks, iron, chests, and the central clearing.")]
    public WorldPropsConfig propsConfig;

    [Tooltip("Optional WorldPropsPlacer reference. Automatically searches this GameObject when empty.")]
    public WorldPropsPlacer propsPlacer;

    [Header("Seed Control")]
    [Tooltip("When enabled, generate a new random numeric seed on every generation click.")]
    public bool useRandomSeed = false;

    [Tooltip("Specific seed used for terrain generation when 'useRandomSeed' is disabled.")]
    public int customSeed = 4242;

    [Tooltip("Specific props seed. Uses the terrain seed when 0.")]
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

        // If terrain was already generated in the Editor scene and we did not arrive from the Main Menu, skip loading.
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

    [Header("Rendering & Material")]
    [Tooltip("Material supporting vertex colors (e.g. M_TerrainLowPoly using Duskborn/LowPolyTerrainVertexColor).")]
    public Material vertexColorMaterial;

    [Header("Stylized Water (Low-Poly Water)")]
    [Tooltip("When enabled, automatically create and position a stylized water plane at 'waterLevel'.")]
    public bool generateWaterPlane = true;

    [Tooltip("Fine water plane height adjustment (added to config waterLevel). Allows real-time visual height calibration.")]
    public float waterHeightOffset = 0f;

    [Tooltip("Stylized water material (e.g. M_WaterLowPoly using Duskborn/LowPolyWater).")]
    public Material waterMaterial;

    [Tooltip("Water plane subdivisions (quads per side for low-poly facet animation and relief).")]
    [Range(16, 128)] public int waterSubdivisions = 64;

    public float EffectiveWaterLevel => config != null ? (config.waterLevel + waterHeightOffset) : waterHeightOffset;

    [Header("World Atmosphere & FX")]
    [Tooltip("When enabled, create or update a WorldAtmosphereController object for organic atmospheric particles.")]
    public bool generateAtmosphereFX = false;

    [Header("Stylized Vegetation (Stylized Foliage)")]
    [Tooltip("When enabled, automatically generate grass tufts and shrubs with terrain-blended colors per chunk.")]
    public bool generateFoliage = true;

    [Tooltip("Grass tuft material (Duskborn/StylizedFoliage shader).")]
    public Material foliageGrassMaterial;

    [Tooltip("Stylized shrub material (Duskborn/StylizedFoliage shader).")]
    public Material foliageBushMaterial;

    [Tooltip("Procedural vegetation density level (Genshin / Zelda style).")]
    public Duskborn.Gameplay.World.Foliage.FoliageDensityPreset foliageDensityPreset = Duskborn.Gameplay.World.Foliage.FoliageDensityPreset.High;

    [Header("Navigation & AI (NavMesh)")]
    [Tooltip("Optional NavMeshSurface reference. Automatically searches this GameObject or the scene when empty.")]
    public NavMeshSurface navMeshSurface;

    [Header("Asynchronous Loading & Pipeline")]
    [Tooltip("When enabled, display the Minecraft-inspired stylized procedural loading screen during generation.")]
    public bool showLoadingScreen = true;

    [Tooltip("When enabled, always clear old scene chunks and force new procedural generation with a loading screen when starting play.")]
    public bool forceRegenerateOnPlay = false;

    [Tooltip("When enabled, skip loading entirely for instant tests if the game starts directly in this Editor scene with terrain already generated.")]
    public bool skipLoadingIfTerrainExists = true;

    [Tooltip("Maximum processing budget per frame in milliseconds (e.g. 8ms for consistent 60+ FPS).")]
    [Range(2f, 20f)] public float frameBudgetMs = 8f;

    public bool IsWorldReady { get; private set; } = false;
    public bool IsGenerating { get; private set; } = false;

    public event System.Action OnWorldGenerationComplete;
    public event System.Action<float, string, string> OnWorldGenerationProgress;

    private readonly Dictionary<Vector2Int, TerrainChunk> loadedChunks = new Dictionary<Vector2Int, TerrainChunk>();

    private void Start()
    {
        if (config == null) return;

        // Do nothing if Awake already initialized instantly using existing terrain.
        if (IsWorldReady) return;

        // Retry if a chunk was registered or created between Awake and Start.
        if (CheckAndApplyExistingTerrain())
        {
            return;
        }

        // Consume the main menu flag if arriving from there.
        if (Duskborn.UI.MainMenuController.LoadedFromMainMenu)
        {
            Duskborn.UI.MainMenuController.LoadedFromMainMenu = false;
        }

        StartCoroutine(GenerateGridAsync());
    }

    /// <summary>
    /// Find existing scene terrain chunks using multiple defensive strategies.
    /// </summary>
    public bool TryFindExistingChunks(out List<TerrainChunk> foundChunks)
    {
        foundChunks = new List<TerrainChunk>();

        // 1. Direct or indirect children of this GameObject.
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

        // 2. Chunks anywhere in the active scene (at root or under another parent).
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

        // 3. Fallback: find GameObjects with the "Chunk_" prefix that are children of this transform.
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
    /// Check whether existing scene terrain should be reused, skipping the loading screen and procedural generation.
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
    /// Use pregenerated terrain from the Editor scene, skipping the loading screen and activating the world instantly.
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

        // Ensure no loading screen remains active or visible.
        var loadingUIs = FindObjectsByType<WorldLoadingScreenUI>(FindObjectsInactive.Include);
        foreach (var ui in loadingUIs)
        {
            if (ui != null)
            {
                ui.gameObject.SetActive(false);
            }
        }

        // Scene terrain can survive commit cleanup while its generated NavMesh does not.
        // Restore navigation before allowing players or wave spawns into this world.
        EnsureRuntimeNavigation();
        IsGenerating = false;
        IsWorldReady = true;
        OnWorldGenerationComplete?.Invoke();

        Debug.Log($"[ChunkGridManager] Pregenerated terrain detected in scene ({loadedChunks.Count} chunks). Loading and generation skipped for instant Editor testing.");
    }

    private System.Collections.IEnumerator InitExistingSceneTerrainAsync(TerrainChunk[] existingChunks)
    {
        IsGenerating = true;
        IsWorldReady = false;

        WorldLoadingScreenUI loadingUI = null;
        if (Application.isPlaying && (showLoadingScreen || WorldLoadingScreenUI.IsBlockingGameplay))
        {
            loadingUI = WorldLoadingScreenUI.EnsureInstance();
            loadingUI.Show("Awakening the Twilight...", "Synchronizing the sanctuary and ancestral lands...");
        }

        GenerationBudget budget = new GenerationBudget(frameBudgetMs);

        // If chunks exist but props have not been generated, generate them asynchronously.
        if (propsPlacer != null && propsConfig != null && propsPlacer.PropsCount == 0)
        {
            yield return propsPlacer.PlaceWorldPropsAsync(config, propsConfig, ActivePropsSeed, budget, (p, detail) =>
            {
                if (loadingUI != null) loadingUI.UpdateProgress(Mathf.Lerp(0.1f, 0.45f, p), "Seeding Forests and Deposits", detail);
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
                if (loadingUI != null) loadingUI.UpdateProgress(p, "Weaving Grass and Wildflowers", $"Growing sacred foliage (Chunk {i + 1}/{existingChunks.Length})...");
            }
        }

        if (navMeshSurface == null || navMeshSurface.navMeshData == null)
            yield return RebuildNavMeshAsync();
        else
            EnsureRuntimeNavigation();
        IsGenerating = false;
        IsWorldReady = true;
        OnWorldGenerationComplete?.Invoke();

        if (loadingUI != null)
        {
            loadingUI.CompleteAndFadeOut();
        }
    }

    /// <summary>
    /// Complete asynchronous world generation pipeline in 6 phases with frame budgeting and a Minecraft-inspired loading screen.
    /// </summary>
    public System.Collections.IEnumerator GenerateGridAsync(System.Action<float, string, string> onProgress = null, System.Action onComplete = null)
    {
        if (config == null)
        {
            Debug.LogError("[ChunkGridManager] No configuration assigned!");
            yield break;
        }

        IsGenerating = true;
        IsWorldReady = false;

        WorldLoadingScreenUI loadingUI = null;
        if (Application.isPlaying && (showLoadingScreen || WorldLoadingScreenUI.IsBlockingGameplay))
        {
            loadingUI = WorldLoadingScreenUI.EnsureInstance();
            loadingUI.Show("Building World...", "Initializing terrain seeds...");
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

        Report(0.02f, "Summoning the Twilight", "Consecrating the wilderness and clearing remnants...");
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

        // PHASE 1: Terrain Chunk and Collision Generation (0.05 to 0.38).
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
                // Precook the mesh in PhysX so the collider does not freeze the frame.
                Physics.BakeMesh(mesh.GetInstanceID(), false);
                chunk.ApplyMesh(mesh);

                loadedChunks[coord] = chunk;
                chunkCounter++;

                float p = Mathf.Lerp(0.05f, 0.38f, (float)chunkCounter / totalChunks);
                Report(p, "Carving the Titans' Terrain", $"Forging fractal terrain and ravines (Chunk {chunkCounter}/{totalChunks})...");

                if (budget.ShouldYield())
                {
                    yield return null;
                }
            }
        }

        // PHASE 2: Stylized Water Surface & Atmosphere (0.38 to 0.44).
        Report(0.40f, "Channeling Waters and Mists", "Summoning the ancestral water plane and twilight particles...");
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

        // PHASE 3: Procedural Resources and Structures (0.44 to 0.68).
        if (propsPlacer == null)
        {
            propsPlacer = GetComponent<WorldPropsPlacer>() ?? gameObject.AddComponent<WorldPropsPlacer>();
        }

        if (propsPlacer != null && propsConfig != null)
        {
            yield return propsPlacer.PlaceWorldPropsAsync(config, propsConfig, activePropsSeed, budget, (propP, detail) =>
            {
                float p = Mathf.Lerp(0.44f, 0.68f, propP);
                Report(p, "Seeding Forests and Deposits", detail);
            });
        }

        // PHASE 4: Stylized Vegetation and Foliage (0.68 to 0.86).
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
                Report(p, "Weaving Grass and Wildflowers", $"Growing sacred foliage (Chunk {i + 1}/{chunksForFoliage.Length})...");
            }
        }
        else
        {
            ClearFoliage();
        }

        // PHASE 5: Navigation Mesh (Asynchronous NavMesh) (0.86 to 0.96).
        yield return RebuildNavMeshAsync((navP, detail) =>
        {
            float p = Mathf.Lerp(0.86f, 0.96f, navP);
            Report(p, "Consecrating Navigation Paths", detail);
        });

        // PHASE 6: Finalization & Ready for Launch (0.96 to 1.0).
        Report(1.0f, "The Twilight Reveals Itself!", "Sanctuary active. Opening the way for warriors...");
        if (propsPlacer != null)
        {
            propsPlacer.EnsureSpawnPointsReady(propsConfig);
        }

        IsGenerating = false;
        IsWorldReady = true;

        OnWorldGenerationComplete?.Invoke();
        onComplete?.Invoke();

        Debug.Log($"[ChunkGridManager] Asynchronous generation of {config.chunksX}x{config.chunksZ} grid completed successfully! (Seed={activeSeed}, PropsSeed={activePropsSeed})");

        if (loadingUI != null)
        {
            loadingUI.CompleteAndFadeOut();
        }
    }

    [ContextMenu("Regenerate Terrain Grid")]
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
            UnityEditor.EditorUtility.DisplayProgressBar("Generating Duskborn Terrain", "Clearing previous geometry...", 0.05f);
            ClearGrid();

            if (config == null)
            {
                Debug.LogError("[ChunkGridManager] No configuration assigned!");
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
                    UnityEditor.EditorUtility.DisplayProgressBar("Generating Duskborn Terrain", $"Generating Low-Poly Terrain ({chunkCounter}/{totalChunks})...", p);

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

            // 2. Stylized Water Plane (Low-Poly Water).
            UnityEditor.EditorUtility.DisplayProgressBar("Generating Duskborn Terrain", "Configuring Water and Atmosphere...", 0.55f);
            if (generateWaterPlane)
            {
                GenerateWaterPlane();
            }
            else
            {
                RemoveWaterPlane();
            }

            // 3. Atmosphere & Ambient Particles (daytime pollen / nighttime fireflies).
            if (generateAtmosphereFX)
            {
                EnsureAtmosphereFX();
            }
            else
            {
                RemoveAtmosphereFX();
            }

            // 4. Spawn resource nodes, chests, and clearings (populate SpatialOccupancyMap).
            UnityEditor.EditorUtility.DisplayProgressBar("Generating Duskborn Terrain", "Distributing Procedural Resources and Props...", 0.7f);
            GenerateProps(activePropsSeed);

            // 5. Stylized Foliage (Procedural Grass and Shrubs respecting SpatialOccupancyMap)
            if (generateFoliage)
            {
                UnityEditor.EditorUtility.DisplayProgressBar("Generating Duskborn Terrain", "Planting Foliage and Grass...", 0.85f);
                GenerateFoliage(activeSeed);
            }
            else
            {
                ClearFoliage();
            }

            // 6. Bake NavMesh including terrain meshes and prop colliders.
            UnityEditor.EditorUtility.DisplayProgressBar("Generating Duskborn Terrain", "Calculating Navigation NavMesh...", 0.95f);
            RebuildNavMesh();

            IsGenerating = false;
            IsWorldReady = true;
            OnWorldGenerationComplete?.Invoke();

            Debug.Log($"[ChunkGridManager] {config.chunksX}x{config.chunksZ} grid generated successfully with TerrainSeed={activeSeed}, PropsSeed={activePropsSeed} ({loadedChunks.Count} chunks).");
        }
        finally
        {
            UnityEditor.EditorUtility.ClearProgressBar();
        }
#else
        ClearGrid();

        if (config == null)
        {
            Debug.LogError("[ChunkGridManager] No configuration assigned!");
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

        // 2. Stylized Water Plane (Low-Poly Water).
        if (generateWaterPlane)
        {
            GenerateWaterPlane();
        }
        else
        {
            RemoveWaterPlane();
        }

        // 3. Atmosphere & Ambient Particles (daytime pollen / nighttime fireflies).
        if (generateAtmosphereFX)
        {
            EnsureAtmosphereFX();
        }
        else
        {
            RemoveAtmosphereFX();
        }

        // 4. Spawn resource nodes, chests, and clearings (populate SpatialOccupancyMap).
        GenerateProps(activePropsSeed);

        // 5. Stylized Foliage (Procedural Grass and Shrubs respecting SpatialOccupancyMap)
        if (generateFoliage)
        {
            GenerateFoliage(activeSeed);
        }
        else
        {
            ClearFoliage();
        }

        // 6. Bake NavMesh including terrain meshes and prop colliders.
        RebuildNavMesh();

        IsGenerating = false;
        IsWorldReady = true;
        OnWorldGenerationComplete?.Invoke();

        Debug.Log($"[ChunkGridManager] {config.chunksX}x{config.chunksZ} grid generated successfully with TerrainSeed={activeSeed}, PropsSeed={activePropsSeed} ({loadedChunks.Count} chunks).");
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

    [ContextMenu("Regenerate Foliage Only")]
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

    [ContextMenu("Clear Foliage")]
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

    [ContextMenu("Regenerate Props Only")]
    public void RegeneratePropsOnly()
    {
        RegeneratePropsOnly(useRandomSeed);
    }

    public void RegeneratePropsOnly(bool forceRandomSeed)
    {
        // 1. Check for scene terrain chunks. Generate the complete grid if none exist.
        var existingChunks = GetComponentsInChildren<TerrainChunk>(true);
        if (existingChunks == null || existingChunks.Length == 0)
        {
            Debug.LogWarning("[ChunkGridManager] No terrain chunk found for prop placement. Generating complete terrain...");
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

        // If foliage is enabled, regenerate it to fit precisely around the new props and trees.
        if (generateFoliage)
        {
            GenerateFoliage(ActiveSeed);
        }

        RebuildNavMesh();

        Debug.Log($"[ChunkGridManager] Props and foliage successfully regenerated on the same terrain with PropsSeed={activePropsSeed}.");
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

    [ContextMenu("Regenerate Water Plane")]
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

                // Triangle 1 (clockwise viewed from above / +Y normal).
                triangles[ti++] = current;
                triangles[ti++] = next;
                triangles[ti++] = next + 1;

                // Triangle 2 (clockwise viewed from above / +Y normal).
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
    /// Ensure a valid, correctly configured NavMeshSurface component exists on this GameObject.
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

            // Exclude nonwalkable or dynamic entity layers (Player, Enemies, UI, TransparentFX, Water).
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

    public void EnsureRuntimeNavigation()
    {
        var surface = EnsureNavMeshSurface();
        if (surface == null) return;
        Physics.SyncTransforms();
        if (surface.navMeshData == null)
            RebuildNavMesh();
        else
            surface.AddData();
    }

    [ContextMenu("Rebuild NavMesh")]
    public void RebuildNavMesh()
    {
        EnsureNavMeshSurface();

        if (navMeshSurface != null)
        {
            navMeshSurface.BuildNavMesh();
            Debug.Log("[ChunkGridManager] NavMeshSurface baked successfully.");
        }
        else
        {
            Debug.LogWarning("[ChunkGridManager] No NavMeshSurface found to bake NavMesh.");
        }
    }

    /// <summary>
    /// Bake NavMesh asynchronously on worker threads to avoid blocking the main thread.
    /// </summary>
    public System.Collections.IEnumerator RebuildNavMeshAsync(System.Action<float, string> onProgress = null)
    {
        EnsureNavMeshSurface();

        if (navMeshSurface == null)
        {
            onProgress?.Invoke(1.0f, "No NavMeshSurface available.");
            yield break;
        }

        onProgress?.Invoke(0.15f, "Collecting collision sources and meshes...");
        yield return null;

        if (navMeshSurface.navMeshData == null)
        {
            // UpdateNavMesh builds in the data's coordinate frame, while AddData
            // installs it at the surface transform. Match BuildNavMeshData's
            // origin so translated terrain does not receive the offset twice.
            navMeshSurface.navMeshData = new NavMeshData(navMeshSurface.agentTypeID)
            {
                position = navMeshSurface.transform.position,
                rotation = navMeshSurface.transform.rotation
            };
        }

        AsyncOperation op = navMeshSurface.UpdateNavMesh(navMeshSurface.navMeshData);
        if (op != null)
        {
            while (!op.isDone)
            {
                onProgress?.Invoke(Mathf.Lerp(0.25f, 0.95f, op.progress), $"Processing AI mesh in the background ({Mathf.RoundToInt(op.progress * 100)}%)...");
                yield return null;
            }
        }
        else
        {
            navMeshSurface.BuildNavMesh();
        }

        // UpdateNavMesh populates data but does not register newly created data for queries.
        // Without AddData, SamplePosition rejects every wave spawn on a fresh world.
        EnsureRuntimeNavigation();
        onProgress?.Invoke(1.0f, "Navigation mesh complete.");
        Debug.Log("[ChunkGridManager] Asynchronous NavMesh completed successfully.");
    }
}
