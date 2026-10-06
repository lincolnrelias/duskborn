using System.Collections;
using System.Collections.Generic;
using FishNet;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;
using Duskborn.Core;
using Duskborn.Gameplay.Loot;
using Duskborn.Gameplay.Crafting;

namespace Duskborn.Gameplay.World
{
    [SelectionBase]
    public class WorldPropsPlacer : MonoBehaviour
    {
        [Header("Props & Spawn Hierarchy")]
        [Tooltip("Parent Transform grouping all instantiated props. Created automatically when null.")]
        [SerializeField] private Transform propsContainer;

        [Tooltip("Parent Transform for generated player spawn points. Created automatically when null.")]
        [SerializeField] private Transform spawnPointsContainer;

        public Transform PropsContainer => propsContainer;
        public Transform SpawnPointsContainer => spawnPointsContainer;
        public int PropsCount => propsContainer != null ? propsContainer.childCount : 0;

        private readonly List<Transform> _spawnPoints = new List<Transform>();
        public IReadOnlyList<Transform> SpawnPoints => _spawnPoints;

        private readonly List<Vector3> _placedPositions = new List<Vector3>();
        private readonly SpatialOccupancyMap _occupancyMap = new SpatialOccupancyMap();
        public SpatialOccupancyMap OccupancyMap => _occupancyMap;

        // Occupancy is runtime data, so saved props need it rebuilt before their grass is upgraded.
        public void RestoreFoliageOccupancy(LowPolyTerrainConfig terrainConfig, WorldPropsConfig propsConfig, int seed)
        {
            if (terrainConfig == null || propsConfig == null || propsContainer == null) return;
            _occupancyMap.Clear();
            _occupancyMap.RegisterClearing(Vector2.zero, propsConfig.centerClearingRadius, OccupancyType.Player_Sanctuary);
            Physics.SyncTransforms();
            // Central placement does not consume the props RNG; combat clearings are its first random pass.
            PlaceCombatClearings(terrainConfig, propsConfig, new SeededRNG(seed));
            var definitions = new List<PropDefinition> { propsConfig.treeProp, propsConfig.stoneProp,
                propsConfig.ironProp, propsConfig.fiberProp };
            if (propsConfig.extraProps != null) definitions.AddRange(propsConfig.extraProps);
            foreach (Transform prop in propsContainer)
            {
                PropDefinition match = null;
                foreach (PropDefinition definition in definitions)
                {
                    if (definition == null) continue;
                    bool matches = MatchesPrefabName(prop.name, definition.prefab);
                    if (!matches && definition.prefabVariations != null)
                        foreach (GameObject variant in definition.prefabVariations)
                            if (MatchesPrefabName(prop.name, variant)) { matches = true; break; }
                    if (matches) { match = definition; break; }
                }
                float solid = 1.5f, canopy = 0f;
                if (match != null)
                {
                    float scale = Mathf.Max(Mathf.Abs(prop.lossyScale.x), Mathf.Abs(prop.lossyScale.z));
                    solid = (match.solidRadius > 0.05f ? match.solidRadius : match.exclusionRadius * 0.5f) * scale;
                    canopy = match.canopyRadius * scale;
                }
                _occupancyMap.Register(prop.position, solid, canopy, OccupancyType.Resource_Solid);
            }
        }

        private static bool MatchesPrefabName(string instance, GameObject prefab)
        {
            return prefab != null && (instance == prefab.name || instance == prefab.name + "(Clone)");
        }

        private bool _isSubscribed;
        private ChunkGridManager _worldManager;

        private void Awake()
        {
            EnsureContainer();
            EnsureSpawnPointsContainer();
        }

        private void OnEnable()
        {
            SubscribeToNetworkEvents();
            SubscribeToWorldReady();

            if (Application.isPlaying)
            {
                StartCoroutine(FallbackOfflineActivationRoutine());
            }
        }

        private void Start()
        {
            SubscribeToNetworkEvents();
            SubscribeToWorldReady();

            EnsureSpawnPointsReady();

            // If the server started before or during Start, spawn props now.
            if (Application.isPlaying && InstanceFinder.ServerManager != null && InstanceFinder.ServerManager.Started)
            {
                SpawnAllPropsOnServer();
            }
        }

        public void EnsureSpawnPointsReady(WorldPropsConfig configToUse = null)
        {
            EnsureSpawnPointsContainer();

            if (_spawnPoints.Count > 0 && _spawnPoints[0] != null)
            {
                SyncWithPlayerSpawners(_spawnPoints.ToArray());
                return;
            }

            if (spawnPointsContainer != null && spawnPointsContainer.childCount > 0)
            {
                _spawnPoints.Clear();
                for (int i = 0; i < spawnPointsContainer.childCount; i++)
                {
                    Transform child = spawnPointsContainer.GetChild(i);
                    if (child != null)
                        _spawnPoints.Add(child);
                }
                if (_spawnPoints.Count > 0)
                {
                    SyncWithPlayerSpawners(_spawnPoints.ToArray());
                    return;
                }
            }

            WorldPropsConfig targetConfig = configToUse;
            if (targetConfig == null && ChunkGridManager.Instance != null)
            {
                targetConfig = ChunkGridManager.Instance.propsConfig;
            }

            float centerGroundY = 0f;
            if (RaycastGround(new Vector3(0f, 150f, 0f), out RaycastHit centerHit))
            {
                centerGroundY = centerHit.point.y;
            }

            SetupPlayerSpawnPoints(targetConfig, centerGroundY);
        }

        private void OnDisable()
        {
            UnsubscribeFromNetworkEvents();
            if (_worldManager != null)
                _worldManager.OnWorldGenerationComplete -= SpawnAllPropsOnServer;
            _worldManager = null;
        }

        private void SubscribeToWorldReady()
        {
            var manager = ChunkGridManager.Instance;
            if (_worldManager == manager) return;
            if (_worldManager != null)
                _worldManager.OnWorldGenerationComplete -= SpawnAllPropsOnServer;
            _worldManager = manager;
            if (_worldManager != null)
                _worldManager.OnWorldGenerationComplete += SpawnAllPropsOnServer;
        }

        private void SubscribeToNetworkEvents()
        {
            if (_isSubscribed) return;

            if (InstanceFinder.ServerManager != null)
            {
                InstanceFinder.ServerManager.OnServerConnectionState += HandleServerConnectionState;
            }

            if (InstanceFinder.ClientManager != null)
            {
                InstanceFinder.ClientManager.OnClientConnectionState += HandleClientConnectionState;
            }

            _isSubscribed = true;
        }

        private void UnsubscribeFromNetworkEvents()
        {
            if (!_isSubscribed) return;

            if (InstanceFinder.ServerManager != null)
            {
                InstanceFinder.ServerManager.OnServerConnectionState -= HandleServerConnectionState;
            }

            if (InstanceFinder.ClientManager != null)
            {
                InstanceFinder.ClientManager.OnClientConnectionState -= HandleClientConnectionState;
            }

            _isSubscribed = false;
        }

        private void HandleServerConnectionState(ServerConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started)
            {
                SpawnAllPropsOnServer();
            }
        }

        private void HandleClientConnectionState(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started)
            {
                // If connected as a remote client (not Host), clear local scene props
                // to receive only authoritative network instances synchronized by the Host.
                if (InstanceFinder.ServerManager == null || !InstanceFinder.ServerManager.Started)
                {
                    DuskLog.Log(LogChannel.World, "[WorldPropsPlacer] Connected as a remote CLIENT. Clearing local scene props.");
                    ClearProps();
                }
            }
        }

        /// <summary>
        /// Activate and spawn all NetworkObjects in propsContainer on the FishNet network.
        /// Called as soon as the server finishes initialization.
        /// </summary>
        public void SpawnAllPropsOnServer()
        {
            if (!Application.isPlaying) return;
            if (InstanceFinder.ServerManager == null || !InstanceFinder.ServerManager.Started) return;
            SubscribeToWorldReady();
            // The terrain pipeline owns placement until collisions, props and navigation
            // are complete. A server connection must not start a second placement pass.
            if (ChunkGridManager.Instance != null && !ChunkGridManager.Instance.IsWorldReady) return;

            EnsureContainer();
            if (propsContainer == null) return;

            NetworkObject[] nobs = propsContainer.GetComponentsInChildren<NetworkObject>(true);
            if (nobs == null || nobs.Length == 0)
            {
                // If no scene props exist, try generating them using ChunkGridManager settings.
                if (ChunkGridManager.Instance != null && ChunkGridManager.Instance.config != null && ChunkGridManager.Instance.propsConfig != null)
                {
                    DuskLog.Log(LogChannel.World, "[WorldPropsPlacer] No existing props in scene. Generating procedural props on the server...");
                    PlaceWorldProps(ChunkGridManager.Instance.config, ChunkGridManager.Instance.propsConfig, ChunkGridManager.Instance.ActivePropsSeed);
                }
                return;
            }

            // Cached scene props predate newly introduced deposits. Add missing crystal
            // families only on the host, using the same terrain/spacing placement path.
            var manager = ChunkGridManager.Instance;
            if (manager != null) EnsureElementalDeposits(manager.config, manager.propsConfig, manager.ActivePropsSeed);
            int spawnedCount = 0;
            for (int i = 0; i < nobs.Length; i++)
            {
                NetworkObject nob = nobs[i];
                if (nob == null || nob.IsSpawned) continue;

                // Reactivate the GameObject FishNet disabled before the server started.
                nob.gameObject.SetActive(true);
                InstanceFinder.ServerManager.Spawn(nob.gameObject);
                spawnedCount++;
            }

            DuskLog.Log(LogChannel.World, $"[WorldPropsPlacer] Server started: {spawnedCount} props activated and synchronized on the FishNet network.");
        }

        /// <summary>
        /// Reactivate all GameObjects in propsContainer locally (without networking).
        /// Useful fallback for offline Play Mode tests without FishNet network initialization.
        /// </summary>
        public void ActivateAllPropsLocally()
        {
            EnsureContainer();
            if (propsContainer == null) return;

            NetworkObject[] nobs = propsContainer.GetComponentsInChildren<NetworkObject>(true);
            for (int i = 0; i < nobs.Length; i++)
            {
                if (nobs[i] != null && !nobs[i].gameObject.activeSelf)
                {
                    nobs[i].gameObject.SetActive(true);
                }
            }
        }

        private IEnumerator FallbackOfflineActivationRoutine()
        {
            yield return new WaitForSeconds(0.6f);

            if (InstanceFinder.NetworkManager == null ||
                (!InstanceFinder.ServerManager.Started && !InstanceFinder.ClientManager.Started))
            {
                DuskLog.Log(LogChannel.World, "[WorldPropsPlacer] Inactive / offline network detected. Reactivating local props for test mode.");
                ActivateAllPropsLocally();
            }
        }

        public void PlaceWorldProps(LowPolyTerrainConfig terrainConfig, WorldPropsConfig propsConfig, int seed)
        {
            if (terrainConfig == null || propsConfig == null)
            {
                DuskLog.Warn(LogChannel.World, "[WorldPropsPlacer] Terrain or props configuration is null!");
                return;
            }

            EnsureContainer();
            ClearProps();
            _occupancyMap.Clear();

            // Ensure newly generated chunk colliders are synchronized with physics.
            Physics.SyncTransforms();

            SeededRNG rng = new SeededRNG(seed);
            _placedPositions.Clear();

            // 1. Central Clearing (Map Center and Sanctuary).
            PlaceCentralClearing(terrainConfig, propsConfig, rng);

            // 2. Preserved Combat Clearings (30-40% open area for kiting and battles).
            PlaceCombatClearings(terrainConfig, propsConfig, rng);

            // 3. Natural Resources (Resource Nodes) per Chunk with Clustering.
            PlaceResourceNodes(terrainConfig, propsConfig, rng);

            // 4. Chest Distribution with Distance Scaling.
            PlaceChests(terrainConfig, propsConfig, rng);

#if UNITY_EDITOR
            if (!Application.isPlaying && propsContainer != null)
            {
                NetworkObject[] nobs = propsContainer.GetComponentsInChildren<NetworkObject>(true);
                int countReserialized = 0;
                var reserializeMethod = typeof(NetworkObject).GetMethod("ReserializeEditorSetValues",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                for (int i = 0; i < nobs.Length; i++)
                {
                    if (nobs[i] != null)
                    {
                        reserializeMethod?.Invoke(nobs[i], new object[] { true, true });
                        UnityEditor.EditorUtility.SetDirty(nobs[i]);
                        countReserialized++;
                    }
                }
                DuskLog.Log(LogChannel.World, $"[WorldPropsPlacer] Serialized {countReserialized} NetworkObjects with valid SceneIds in the Editor.");
            }
#endif

            DuskLog.Log(LogChannel.World, $"[WorldPropsPlacer] Props generation complete! Total positions: {_placedPositions.Count}, Indexed occupancy: {_occupancyMap.Count}.");
        }

        /// <summary>
        /// Asynchronous props and resource placement respecting the frame budget for consistent 60+ FPS.
        /// </summary>
        public IEnumerator PlaceWorldPropsAsync(
            LowPolyTerrainConfig terrainConfig,
            WorldPropsConfig propsConfig,
            int seed,
            GenerationBudget budget = null,
            System.Action<float, string> onProgress = null)
        {
            if (terrainConfig == null || propsConfig == null)
            {
                DuskLog.Warn(LogChannel.World, "[WorldPropsPlacer] Terrain or props configuration is null!");
                yield break;
            }

            budget ??= new GenerationBudget(8f);

            EnsureContainer();
            ClearProps();
            _occupancyMap.Clear();

            Physics.SyncTransforms();

            SeededRNG rng = new SeededRNG(seed);
            _placedPositions.Clear();

            onProgress?.Invoke(0.05f, "Configuring Central Sanctuary and Spawns...");
            PlaceCentralClearing(terrainConfig, propsConfig, rng);
            if (budget.ShouldYield()) yield return null;

            onProgress?.Invoke(0.12f, "Preserving Combat Clearings...");
            PlaceCombatClearings(terrainConfig, propsConfig, rng);
            if (budget.ShouldYield()) yield return null;

            // 3. Natural Resources per Chunk with Time Slicing.
            yield return PlaceResourceNodesAsyncRoutine(terrainConfig, propsConfig, rng, budget, (p, detail) =>
            {
                onProgress?.Invoke(Mathf.Lerp(0.15f, 0.85f, p), detail);
            });

            // 4. Chest Distribution.
            onProgress?.Invoke(0.90f, "Distributing Treasure Chests...");
            PlaceChests(terrainConfig, propsConfig, rng);
            if (budget.ShouldYield()) yield return null;

#if UNITY_EDITOR
            if (!Application.isPlaying && propsContainer != null)
            {
                NetworkObject[] nobs = propsContainer.GetComponentsInChildren<NetworkObject>(true);
                int countReserialized = 0;
                var reserializeMethod = typeof(NetworkObject).GetMethod("ReserializeEditorSetValues",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                for (int i = 0; i < nobs.Length; i++)
                {
                    if (nobs[i] != null)
                    {
                        reserializeMethod?.Invoke(nobs[i], new object[] { true, true });
                        UnityEditor.EditorUtility.SetDirty(nobs[i]);
                        countReserialized++;
                    }
                }
                DuskLog.Log(LogChannel.World, $"[WorldPropsPlacer] Serialized {countReserialized} NetworkObjects with valid SceneIds in the Editor.");
            }
#endif

            onProgress?.Invoke(1.0f, $"Props complete ({_placedPositions.Count} objects).");
            DuskLog.Log(LogChannel.World, $"[WorldPropsPlacer] Asynchronous props generation complete! Total positions: {_placedPositions.Count}, Indexed occupancy: {_occupancyMap.Count}.");
        }

        private IEnumerator PlaceResourceNodesAsyncRoutine(
            LowPolyTerrainConfig terrainConfig,
            WorldPropsConfig propsConfig,
            SeededRNG rng,
            GenerationBudget budget,
            System.Action<float, string> onProgress)
        {
            List<PropDefinition> propDefs = new List<PropDefinition>();
            if (propsConfig.treeProp != null) propDefs.Add(propsConfig.treeProp);
            if (propsConfig.stoneProp != null) propDefs.Add(propsConfig.stoneProp);
            if (propsConfig.ironProp != null) propDefs.Add(propsConfig.ironProp);
            if (propsConfig.fiberProp != null) propDefs.Add(propsConfig.fiberProp);

            if (propsConfig.extraProps != null)
            {
                foreach (var extra in propsConfig.extraProps)
                {
                    if (extra != null) propDefs.Add(extra);
                }
            }

            if (propDefs.Count == 0) yield break;

            int startX = -terrainConfig.chunksX / 2;
            int startZ = -terrainConfig.chunksZ / 2;
            float chunkWorldLength = terrainConfig.chunkSize * terrainConfig.cellSize;
            float centerRadiusSqr = propsConfig.centerClearingRadius * propsConfig.centerClearingRadius;

            float halfMapX = (terrainConfig.chunksX * terrainConfig.chunkSize * terrainConfig.cellSize) * 0.5f;
            float halfMapZ = (terrainConfig.chunksZ * terrainConfig.chunkSize * terrainConfig.cellSize) * 0.5f;
            float mapRadius = Mathf.Min(halfMapX, halfMapZ);
            float maxBoundaryRadius = terrainConfig.boundaryType != LowPolyTerrainConfig.MapBoundaryType.None
                ? terrainConfig.GetPlayableBoundaryRadius(mapRadius) * 0.95f
                : (mapRadius * 0.95f);

            int totalChunks = terrainConfig.chunksX * terrainConfig.chunksZ;
            int currentChunkIndex = 0;

            for (int cz = startZ; cz < startZ + terrainConfig.chunksZ; cz++)
            {
                for (int cx = startX; cx < startX + terrainConfig.chunksX; cx++)
                {
                    float chunkMinX = cx * chunkWorldLength;
                    float chunkMaxX = chunkMinX + chunkWorldLength;
                    float chunkMinZ = cz * chunkWorldLength;
                    float chunkMaxZ = chunkMinZ + chunkWorldLength;

                    currentChunkIndex++;
                    float progress = (float)currentChunkIndex / totalChunks;
                    onProgress?.Invoke(progress, $"Scattering natural resources (Chunk {currentChunkIndex}/{totalChunks})...");

                    foreach (var propDef in propDefs)
                    {
                        if (propDef.prefab == null) continue;

                        var cluster = propDef.clusterSettings;
                        bool clusteringEnabled = cluster != null ? cluster.enableClustering : propDef.useClustering;

                        if (clusteringEnabled && cluster != null)
                        {
                            PlaceClusteredPropsForChunk(propDef, cluster, chunkMinX, chunkMaxX, chunkMinZ, chunkMaxZ,
                                centerRadiusSqr, maxBoundaryRadius, terrainConfig, rng);
                        }
                        else
                        {
                            PlaceIndividualPropsForChunk(propDef, chunkMinX, chunkMaxX, chunkMinZ, chunkMaxZ,
                                centerRadiusSqr, maxBoundaryRadius, terrainConfig, rng);
                        }

                        if (budget.ShouldYield())
                        {
                            yield return null;
                        }
                    }
                }
            }
        }

        private bool RaycastGround(Vector3 rayOrigin, out RaycastHit groundHit)
        {
            RaycastHit[] hits = Physics.RaycastAll(rayOrigin, Vector3.down, 300f);
            float closestDist = float.MaxValue;
            groundHit = default;
            bool found = false;

            for (int i = 0; i < hits.Length; i++)
            {
                // Prioritize terrain mesh hits (TerrainChunk or terrain MeshCollider).
                if (hits[i].collider.GetComponent<TerrainChunk>() != null || hits[i].collider is MeshCollider)
                {
                    if (hits[i].distance < closestDist)
                    {
                        closestDist = hits[i].distance;
                        groundHit = hits[i];
                        found = true;
                    }
                }
            }

            // Fallback to any collider if no explicit TerrainChunk is found.
            if (!found && hits.Length > 0)
            {
                for (int i = 0; i < hits.Length; i++)
                {
                    if (hits[i].distance < closestDist)
                    {
                        closestDist = hits[i].distance;
                        groundHit = hits[i];
                        found = true;
                    }
                }
            }

            return found;
        }

        private void PlaceCentralClearing(LowPolyTerrainConfig terrainConfig, WorldPropsConfig propsConfig, SeededRNG rng)
        {
            // Register the central Sanctuary in the occupancy map.
            _occupancyMap.RegisterClearing(Vector2.zero, propsConfig.centerClearingRadius, OccupancyType.Player_Sanctuary);

            Vector3 centerRayOrigin = new Vector3(0f, 150f, 0f);
            float centerGroundY = 0f;

            if (RaycastGround(centerRayOrigin, out RaycastHit centerHit))
            {
                centerGroundY = centerHit.point.y;
            }

            // A) Place / Update Player Spawn Points.
            SetupPlayerSpawnPoints(propsConfig, centerGroundY);

            // B) Place Crafting Stations (Workbench, Forge, Cauldron, Arcane Table).
            (GameObject prefab, Vector3 offset, float rotY)[] stationsToPlace = new[]
            {
                (propsConfig.workbenchPrefab ?? Resources.Load<GameObject>("Stations/Workbench"), new Vector3(2.5f, 0f, 1.5f), -45f),
                (propsConfig.forgePrefab ?? Resources.Load<GameObject>("Stations/Station_Forge"), new Vector3(3.5f, 0f, -2.0f), -135f),
                (propsConfig.cauldronPrefab ?? Resources.Load<GameObject>("Stations/Station_Cauldron"), new Vector3(-2.5f, 0f, 2.5f), 45f),
                (propsConfig.arcaneTablePrefab ?? Resources.Load<GameObject>("Stations/Station_ArcaneTable"), new Vector3(-3.0f, 0f, -2.0f), 135f)
            };

            foreach (var st in stationsToPlace)
            {
                if (st.prefab == null) continue;

                Vector3 stRayOrigin = new Vector3(st.offset.x, 150f, st.offset.z);
                Vector3 stPos = st.offset + Vector3.up * centerGroundY;

                if (RaycastGround(stRayOrigin, out RaycastHit stHit))
                {
                    stPos = stHit.point;
                }

                GameObject stGO = InstantiateProp(st.prefab, stPos, Quaternion.Euler(0f, st.rotY, 0f), Vector3.one);
                _placedPositions.Add(stPos);
                _occupancyMap.Register(stPos, solidRadius: 1.5f, canopyRadius: 0f, OccupancyType.Resource_Solid);
            }

            // C) Place Tier Test Resource Nodes (Common -> Uncommon -> Rare -> Epic -> Legendary).
            EnsureContainer();
            StaticTierTestNodes.SpawnNodes(propsContainer, new Vector3(0f, centerGroundY, 0f), pos =>
            {
                Vector3 rayOrigin = new Vector3(pos.x, 150f, pos.z);
                return RaycastGround(rayOrigin, out RaycastHit hit) ? hit.point : pos;
            });
        }

        private void PlaceCombatClearings(LowPolyTerrainConfig terrainConfig, WorldPropsConfig propsConfig, SeededRNG rng)
        {
            int startX = -terrainConfig.chunksX / 2;
            int startZ = -terrainConfig.chunksZ / 2;
            float chunkWorldLength = terrainConfig.chunkSize * terrainConfig.cellSize;
            float sanctuaryRadiusSqr = (propsConfig.centerClearingRadius + 5f) * (propsConfig.centerClearingRadius + 5f);

            float halfMapX = (terrainConfig.chunksX * terrainConfig.chunkSize * terrainConfig.cellSize) * 0.5f;
            float halfMapZ = (terrainConfig.chunksZ * terrainConfig.chunkSize * terrainConfig.cellSize) * 0.5f;
            float mapRadius = Mathf.Min(halfMapX, halfMapZ);
            float maxBoundaryRadius = terrainConfig.boundaryType != LowPolyTerrainConfig.MapBoundaryType.None
                ? terrainConfig.GetPlayableBoundaryRadius(mapRadius) * 0.88f
                : (mapRadius * 0.88f);

            for (int cz = startZ; cz < startZ + terrainConfig.chunksZ; cz++)
            {
                for (int cx = startX; cx < startX + terrainConfig.chunksX; cx++)
                {
                    // The central chunk already contains the starting Sanctuary.
                    if (cx == 0 && cz == 0) continue;

                    float chunkMinX = cx * chunkWorldLength;
                    float chunkMaxX = chunkMinX + chunkWorldLength;
                    float chunkMinZ = cz * chunkWorldLength;
                    float chunkMaxZ = chunkMinZ + chunkWorldLength;

                    // Place one open combat clearing per chunk to preserve combat arenas (30-40% of the map).
                    for (int attempt = 0; attempt < 16; attempt++)
                    {
                        float sampleX = rng.Range(chunkMinX + 4f, chunkMaxX - 4f);
                        float sampleZ = rng.Range(chunkMinZ + 4f, chunkMaxZ - 4f);
                        float distSq = sampleX * sampleX + sampleZ * sampleZ;

                        if (distSq < sanctuaryRadiusSqr || distSq > maxBoundaryRadius * maxBoundaryRadius)
                            continue;

                        Vector3 rayOrigin = new Vector3(sampleX, 150f, sampleZ);
                        if (!RaycastGround(rayOrigin, out RaycastHit hit))
                            continue;

                        if (hit.point.y <= terrainConfig.waterLevel + 0.5f)
                            continue;

                        float slope = Vector3.Angle(hit.normal, Vector3.up);
                        if (slope > 20f)
                            continue;

                        float arenaRadius = rng.Range(8.0f, 11.5f);
                        _occupancyMap.RegisterClearing(new Vector2(sampleX, sampleZ), arenaRadius, OccupancyType.Combat_Clearing);
                        break;
                    }
                }
            }
        }

        private void SetupPlayerSpawnPoints(WorldPropsConfig propsConfig, float groundY)
        {
            EnsureSpawnPointsContainer();
            ClearSpawnPoints();

            spawnPointsContainer.position = new Vector3(0f, groundY, 0f);

            int count = (propsConfig != null && propsConfig.playerSpawnPointsCount > 0)
                ? propsConfig.playerSpawnPointsCount
                : 5;
            float radius = (propsConfig != null && propsConfig.playerSpawnRadius > 0f)
                ? propsConfig.playerSpawnRadius
                : 6.5f;

            _spawnPoints.Clear();

            for (int i = 0; i < count; i++)
            {
                float angle = i * (360f / count) * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

                Vector3 rayOrigin = new Vector3(offset.x, 150f, offset.z);
                float pointGroundY = groundY;
                if (RaycastGround(rayOrigin, out RaycastHit hit))
                {
                    pointGroundY = hit.point.y;
                }

                Vector3 worldPos = new Vector3(offset.x, pointGroundY + 0.05f, offset.z);
                Vector3 lookDir = new Vector3(-offset.x, 0f, -offset.z).normalized;
                Quaternion rot = lookDir != Vector3.zero ? Quaternion.LookRotation(lookDir) : Quaternion.identity;

                GameObject spGO;
                if (propsConfig != null && propsConfig.playerSpawnPointPrefab != null)
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                    {
                        spGO = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(propsConfig.playerSpawnPointPrefab, spawnPointsContainer);
                        if (spGO == null)
                        {
                            spGO = Instantiate(propsConfig.playerSpawnPointPrefab, worldPos, rot, spawnPointsContainer);
                        }
                        spGO.transform.SetPositionAndRotation(worldPos, rot);
                        UnityEditor.Undo.RegisterCreatedObjectUndo(spGO, "Spawn Player Point");
                    }
                    else
#endif
                    {
                        spGO = Instantiate(propsConfig.playerSpawnPointPrefab, worldPos, rot, spawnPointsContainer);
                    }
                    spGO.name = $"spawn_point_{i + 1}";
                }
                else
                {
                    spGO = new GameObject($"spawn_point_{i + 1}");
                    spGO.transform.parent = spawnPointsContainer;
                    spGO.transform.SetPositionAndRotation(worldPos, rot);
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                    {
                        UnityEditor.Undo.RegisterCreatedObjectUndo(spGO, "Spawn Player Point");
                    }
#endif
                    var sp = spGO.AddComponent<PlayerSpawnPoint>();
                    sp.spawnIndex = i;
                }

                _spawnPoints.Add(spGO.transform);
                _occupancyMap.Register(worldPos, solidRadius: 1.0f, canopyRadius: 0f, OccupancyType.Player_Sanctuary);
            }

            SyncWithPlayerSpawners(_spawnPoints.ToArray());
            DuskLog.Log(LogChannel.World, $"[WorldPropsPlacer] {_spawnPoints.Count} player spawn points generated successfully with the terrain.");
        }

        public int EnsureElementalDeposits(LowPolyTerrainConfig terrainConfig, WorldPropsConfig propsConfig, int seed)
        {
            if (terrainConfig == null || propsConfig == null || propsConfig.extraProps == null) return 0;
            EnsureContainer();
            var present = new HashSet<Duskborn.Gameplay.Enchanting.RuneKind>();
            foreach (var crystal in propsContainer.GetComponentsInChildren<Duskborn.Effects.ElementalCrystalNodeVisual>(true))
                present.Add(crystal.Element);
            var missing = new List<PropDefinition>();
            foreach (var prop in propsConfig.extraProps)
            {
                if (prop == null || prop.prefab == null) continue;
                var visual = prop.prefab.GetComponent<Duskborn.Effects.ElementalCrystalNodeVisual>();
                if (visual != null && !present.Contains(visual.Element)) missing.Add(prop);
            }
            if (missing.Count == 0) return 0;
            _occupancyMap.RegisterClearing(Vector2.zero, propsConfig.centerClearingRadius, OccupancyType.Player_Sanctuary);
            // Rebuild physical occupancy for previously serialized scene props.
            foreach (Transform existing in propsContainer)
                _occupancyMap.Register(existing.position, 2f, 0f, OccupancyType.Resource_Solid);
            float length = terrainConfig.chunkSize * terrainConfig.cellSize;
            float radius = Mathf.Min(terrainConfig.chunksX, terrainConfig.chunksZ) * length * .5f;
            float boundary = (terrainConfig.boundaryType != LowPolyTerrainConfig.MapBoundaryType.None
                ? terrainConfig.GetPlayableBoundaryRadius(radius) : radius) * .95f;
            int before = propsContainer.childCount;
            foreach (var prop in missing)
            {
                int element = (int)prop.prefab.GetComponent<Duskborn.Effects.ElementalCrystalNodeVisual>().Element;
                var rng = new SeededRNG(seed ^ (0x435259 + element * 7919));
                for (int z = -terrainConfig.chunksZ / 2; z < -terrainConfig.chunksZ / 2 + terrainConfig.chunksZ; z++)
                    for (int x = -terrainConfig.chunksX / 2; x < -terrainConfig.chunksX / 2 + terrainConfig.chunksX; x++)
                        PlaceIndividualPropsForChunk(prop, x * length, (x + 1) * length, z * length, (z + 1) * length,
                            propsConfig.centerClearingRadius * propsConfig.centerClearingRadius, boundary, terrainConfig, rng);
            }
            return propsContainer.childCount - before;
        }

        private void PlaceResourceNodes(LowPolyTerrainConfig terrainConfig, WorldPropsConfig propsConfig, SeededRNG rng)
        {
            List<PropDefinition> propDefs = new List<PropDefinition>();
            if (propsConfig.treeProp != null) propDefs.Add(propsConfig.treeProp);
            if (propsConfig.stoneProp != null) propDefs.Add(propsConfig.stoneProp);
            if (propsConfig.ironProp != null) propDefs.Add(propsConfig.ironProp);
            if (propsConfig.fiberProp != null) propDefs.Add(propsConfig.fiberProp);

            if (propsConfig.extraProps != null)
            {
                foreach (var extra in propsConfig.extraProps)
                {
                    if (extra != null) propDefs.Add(extra);
                }
            }

            if (propDefs.Count == 0) return;

            int startX = -terrainConfig.chunksX / 2;
            int startZ = -terrainConfig.chunksZ / 2;
            float chunkWorldLength = terrainConfig.chunkSize * terrainConfig.cellSize;
            float centerRadiusSqr = propsConfig.centerClearingRadius * propsConfig.centerClearingRadius;

            float halfMapX = (terrainConfig.chunksX * terrainConfig.chunkSize * terrainConfig.cellSize) * 0.5f;
            float halfMapZ = (terrainConfig.chunksZ * terrainConfig.chunkSize * terrainConfig.cellSize) * 0.5f;
            float mapRadius = Mathf.Min(halfMapX, halfMapZ);
            float maxBoundaryRadius = terrainConfig.boundaryType != LowPolyTerrainConfig.MapBoundaryType.None
                ? terrainConfig.GetPlayableBoundaryRadius(mapRadius) * 0.95f
                : (mapRadius * 0.95f);

            for (int cz = startZ; cz < startZ + terrainConfig.chunksZ; cz++)
            {
                for (int cx = startX; cx < startX + terrainConfig.chunksX; cx++)
                {
                    float chunkMinX = cx * chunkWorldLength;
                    float chunkMaxX = chunkMinX + chunkWorldLength;
                    float chunkMinZ = cz * chunkWorldLength;
                    float chunkMaxZ = chunkMinZ + chunkWorldLength;

                    foreach (var propDef in propDefs)
                    {
                        if (propDef.prefab == null) continue;

                        var cluster = propDef.clusterSettings;
                        bool clusteringEnabled = cluster != null ? cluster.enableClustering : propDef.useClustering;

                        if (clusteringEnabled && cluster != null)
                        {
                            PlaceClusteredPropsForChunk(propDef, cluster, chunkMinX, chunkMaxX, chunkMinZ, chunkMaxZ,
                                centerRadiusSqr, maxBoundaryRadius, terrainConfig, rng);
                        }
                        else
                        {
                            PlaceIndividualPropsForChunk(propDef, chunkMinX, chunkMaxX, chunkMinZ, chunkMaxZ,
                                centerRadiusSqr, maxBoundaryRadius, terrainConfig, rng);
                        }
                    }
                }
            }
        }

        private void PlaceClusteredPropsForChunk(
            PropDefinition propDef,
            ResourceClusterSettings cluster,
            float chunkMinX, float chunkMaxX, float chunkMinZ, float chunkMaxZ,
            float centerRadiusSqr, float maxBoundaryRadius,
            LowPolyTerrainConfig terrainConfig, SeededRNG rng)
        {
            int numClusters = cluster.clustersPerChunk;
            float effectiveSolidRadius = propDef.solidRadius > 0.05f ? propDef.solidRadius : (propDef.exclusionRadius * 0.5f);
            float effectiveCanopyRadius = propDef.canopyRadius;

            for (int c = 0; c < numClusters; c++)
            {
                const int maxCenterAttempts = 32;
                bool foundCenter = false;
                Vector3 clusterCenter = Vector3.zero;

                for (int attempt = 0; attempt < maxCenterAttempts; attempt++)
                {
                    float sampleX = rng.Range(chunkMinX + 1.5f, chunkMaxX - 1.5f);
                    float sampleZ = rng.Range(chunkMinZ + 1.5f, chunkMaxZ - 1.5f);
                    float distCenterSq = sampleX * sampleX + sampleZ * sampleZ;
                    float distCenter = Mathf.Sqrt(distCenterSq);

                    // Reject the central sanctuary.
                    if (distCenterSq < centerRadiusSqr)
                        continue;

                    // Radial zoning (if configured on the prop).
                    if (propDef.minRadialDistance > 0f && distCenter < propDef.minRadialDistance)
                        continue;
                    if (propDef.maxRadialDistance > 0f && distCenter > propDef.maxRadialDistance)
                        continue;

                    // Map boundary margin.
                    if (distCenter + cluster.clusterRadius > maxBoundaryRadius)
                        continue;

                    Vector2 centerXZ = new Vector2(sampleX, sampleZ);

                    // Avoid protected clearings (Sanctuary and Combat Arenas).
                    if (_occupancyMap.IsInClearing(centerXZ))
                        continue;

                    // Intercluster spacing from other resources.
                    if (_occupancyMap.IsSolidOccupied(centerXZ, cluster.interClusterSpacing))
                        continue;

                    Vector3 rayOrigin = new Vector3(sampleX, 150f, sampleZ);
                    if (!RaycastGround(rayOrigin, out RaycastHit hit))
                        continue;

                    // Dry ground
                    if (hit.point.y <= terrainConfig.waterLevel + 0.35f)
                        continue;

                    float effectiveMinH = Mathf.Max(propDef.minHeight, terrainConfig.waterLevel + 0.35f);
                    if (hit.point.y < effectiveMinH || hit.point.y > propDef.maxHeight)
                        continue;

                    float slope = Vector3.Angle(hit.normal, Vector3.up);
                    if (slope > propDef.maxSlopeAngle)
                        continue;

                    clusterCenter = hit.point;
                    foundCenter = true;
                    break;
                }

                if (!foundCenter)
                    continue;

                // Spawn cluster nodes (intracluster Poisson disc sampling).
                int nodeCount = rng.Range(cluster.nodesPerCluster.x, cluster.nodesPerCluster.y + 1);
                const int maxNodeAttempts = 24;

                for (int n = 0; n < nodeCount; n++)
                {
                    for (int attempt = 0; attempt < maxNodeAttempts; attempt++)
                    {
                        float angle = rng.Range(0f, Mathf.PI * 2f);
                        float radius = cluster.clusterRadius * Mathf.Sqrt(rng.Range(0.04f, 1.0f));
                        float nodeX = clusterCenter.x + Mathf.Cos(angle) * radius;
                        float nodeZ = clusterCenter.z + Mathf.Sin(angle) * radius;

                        Vector2 nodeXZ = new Vector2(nodeX, nodeZ);

                        if (_occupancyMap.IsInClearing(nodeXZ))
                            continue;

                        // Intracluster spacing from neighboring nodes.
                        if (_occupancyMap.IsSolidOccupied(nodeXZ, cluster.intraClusterSpacing))
                            continue;

                        Vector3 rayOrigin = new Vector3(nodeX, 150f, nodeZ);
                        if (!RaycastGround(rayOrigin, out RaycastHit hit))
                            continue;

                        if (hit.point.y <= terrainConfig.waterLevel + 0.35f)
                            continue;

                        float effectiveMinH = Mathf.Max(propDef.minHeight, terrainConfig.waterLevel + 0.35f);
                        if (hit.point.y < effectiveMinH || hit.point.y > propDef.maxHeight)
                            continue;

                        float slope = Vector3.Angle(hit.normal, Vector3.up);
                        if (slope > propDef.maxSlopeAngle)
                            continue;

                        Quaternion rot = Quaternion.identity;
                        if (propDef.alignToNormal)
                        {
                            rot = Quaternion.FromToRotation(Vector3.up, hit.normal);
                        }
                        if (propDef.randomYRotation)
                        {
                            rot = rot * Quaternion.Euler(0f, rng.Range(0f, 360f), 0f);
                        }

                        float scale = rng.Range(propDef.scaleRange.x, propDef.scaleRange.y);
                        float heightFactor = (propDef.heightScaleMultiplier.x > 0f && propDef.heightScaleMultiplier.y > 0f)
                            ? rng.Range(propDef.heightScaleMultiplier.x, propDef.heightScaleMultiplier.y)
                            : 1f;
                        Vector3 scaleVec = new Vector3(scale, scale * heightFactor, scale);

                        GameObject chosenPrefab = propDef.GetRandomPrefab(rng);
                        InstantiateProp(chosenPrefab, hit.point, rot, scaleVec);
                        _placedPositions.Add(hit.point);
                        _occupancyMap.Register(hit.point, effectiveSolidRadius * scale, effectiveCanopyRadius * scale, OccupancyType.Resource_Solid);
                        break;
                    }
                }
            }
        }

        private void PlaceIndividualPropsForChunk(
            PropDefinition propDef,
            float chunkMinX, float chunkMaxX, float chunkMinZ, float chunkMaxZ,
            float centerRadiusSqr, float maxBoundaryRadius,
            LowPolyTerrainConfig terrainConfig, SeededRNG rng)
        {
            int targetCount = rng.Range(propDef.minPerChunk, propDef.maxPerChunk + 1);
            float propSeedOffset = (float)(propDef.name.GetHashCode() & 0x7FFF) * 0.17f;
            float effectiveSolidRadius = propDef.solidRadius > 0.05f ? propDef.solidRadius : (propDef.exclusionRadius * 0.5f);
            float effectiveCanopyRadius = propDef.canopyRadius;

            for (int i = 0; i < targetCount; i++)
            {
                const int maxAttempts = 24;
                for (int attempt = 0; attempt < maxAttempts; attempt++)
                {
                    float sampleX = rng.Range(chunkMinX, chunkMaxX);
                    float sampleZ = rng.Range(chunkMinZ, chunkMaxZ);
                    float distSq = sampleX * sampleX + sampleZ * sampleZ;
                    float distFromCenter = Mathf.Sqrt(distSq);

                    // Skip the central safety clearing.
                    if (distSq < centerRadiusSqr)
                        continue;

                    // Radial zoning
                    if (propDef.minRadialDistance > 0f && distFromCenter < propDef.minRadialDistance)
                        continue;
                    if (propDef.maxRadialDistance > 0f && distFromCenter > propDef.maxRadialDistance)
                        continue;

                    // Skip areas outside the playable boundary.
                    if (distSq > maxBoundaryRadius * maxBoundaryRadius)
                        continue;

                    Vector2 sampleXZ = new Vector2(sampleX, sampleZ);
                    if (_occupancyMap.IsInClearing(sampleXZ))
                        continue;

                    // Legacy organic clustering.
                    if (propDef.useClustering)
                    {
                        float clusterNoise = Mathf.PerlinNoise(
                            sampleX * propDef.clusterFrequency + propSeedOffset,
                            sampleZ * propDef.clusterFrequency + propSeedOffset
                        );

                        if (clusterNoise < propDef.clusterThreshold)
                            continue;
                    }

                    Vector3 rayOrigin = new Vector3(sampleX, 150f, sampleZ);
                    if (!RaycastGround(rayOrigin, out RaycastHit hit))
                        continue;

                    // Dry ground
                    if (hit.point.y <= terrainConfig.waterLevel + 0.35f)
                        continue;

                    float effectiveMinH = Mathf.Max(propDef.minHeight, terrainConfig.waterLevel + 0.35f);
                    if (hit.point.y < effectiveMinH || hit.point.y > propDef.maxHeight)
                        continue;

                    float slope = Vector3.Angle(hit.normal, Vector3.up);
                    if (slope > propDef.maxSlopeAngle)
                        continue;

                    // Exclusion radius.
                    if (_occupancyMap.IsSolidOccupied(sampleXZ, propDef.exclusionRadius))
                        continue;

                    Quaternion rot = Quaternion.identity;
                    if (propDef.alignToNormal)
                    {
                        rot = Quaternion.FromToRotation(Vector3.up, hit.normal);
                    }
                    if (propDef.randomYRotation)
                    {
                        rot = rot * Quaternion.Euler(0f, rng.Range(0f, 360f), 0f);
                    }

                    float scale = rng.Range(propDef.scaleRange.x, propDef.scaleRange.y);
                    float heightFactor = (propDef.heightScaleMultiplier.x > 0f && propDef.heightScaleMultiplier.y > 0f)
                        ? rng.Range(propDef.heightScaleMultiplier.x, propDef.heightScaleMultiplier.y)
                        : 1f;
                    Vector3 scaleVec = new Vector3(scale, scale * heightFactor, scale);

                    GameObject chosenPrefab = propDef.GetRandomPrefab(rng);
                    InstantiateProp(chosenPrefab, hit.point, rot, scaleVec);
                    _placedPositions.Add(hit.point);
                    _occupancyMap.Register(hit.point, effectiveSolidRadius * scale, effectiveCanopyRadius * scale, OccupancyType.Resource_Solid);
                    break;
                }
            }
        }

        private void PlaceChests(LowPolyTerrainConfig terrainConfig, WorldPropsConfig propsConfig, SeededRNG rng)
        {
            if (propsConfig.chestPrefab == null || propsConfig.totalChests <= 0) return;

            float halfMapX = (terrainConfig.chunksX * terrainConfig.chunkSize * terrainConfig.cellSize) * 0.5f;
            float halfMapZ = (terrainConfig.chunksZ * terrainConfig.chunkSize * terrainConfig.cellSize) * 0.5f;
            float mapRadius = Mathf.Min(halfMapX, halfMapZ);
            float maxBoundaryRadius = terrainConfig.boundaryType != LowPolyTerrainConfig.MapBoundaryType.None
                ? terrainConfig.GetPlayableBoundaryRadius(mapRadius) * 0.92f
                : (mapRadius * 0.95f);

            int chestsPlaced = 0;
            const int maxAttemptsPerChest = 40;

            for (int i = 0; i < propsConfig.totalChests; i++)
            {
                for (int attempt = 0; attempt < maxAttemptsPerChest; attempt++)
                {
                    float sampleX = rng.Range(-halfMapX * 0.95f, halfMapX * 0.95f);
                    float sampleZ = rng.Range(-halfMapZ * 0.95f, halfMapZ * 0.95f);
                    float distFromCenter = Mathf.Sqrt(sampleX * sampleX + sampleZ * sampleZ);

                    // Avoid spawning next to the immediate spawn point (radius < 3m).
                    if (distFromCenter < 3f)
                        continue;

                    // Boundary safety limit.
                    if (distFromCenter > maxBoundaryRadius)
                        continue;

                    Vector3 rayOrigin = new Vector3(sampleX, 150f, sampleZ);
                    if (!RaycastGround(rayOrigin, out RaycastHit hit))
                        continue;

                    // Dry ground (above water).
                    if (hit.point.y <= terrainConfig.waterLevel + 0.5f)
                        continue;

                    // Gentle slope for chest stability on combat plateaus.
                    float slope = Vector3.Angle(hit.normal, Vector3.up);
                    if (slope > 18f)
                        continue;

                    // Exclude other chests and props (3.0m radius).
                    if (_occupancyMap.IsSolidOccupied(new Vector2(hit.point.x, hit.point.z), 2.5f))
                        continue;

                    // Distance Scaling (Risk vs. Reward).
                    float t = Mathf.Clamp01(distFromCenter / maxBoundaryRadius);
                    int cost = Mathf.RoundToInt(Mathf.Lerp(propsConfig.minChestCost, propsConfig.maxChestCost, t));

                    LootTable chosenTable = t < 0.5f
                        ? (propsConfig.basicLootTable != null ? propsConfig.basicLootTable : propsConfig.rareLootTable)
                        : (propsConfig.rareLootTable != null ? propsConfig.rareLootTable : propsConfig.basicLootTable);

                    Quaternion rot = Quaternion.Euler(0f, rng.Range(0f, 360f), 0f);
                    GameObject chestGO = InstantiateProp(propsConfig.chestPrefab, hit.point, rot, Vector3.one);

                    if (chestGO.TryGetComponent<Chest>(out var chest))
                    {
                        chest.Configure(cost, chosenTable);
#if UNITY_EDITOR
                        if (!Application.isPlaying)
                        {
                            UnityEditor.EditorUtility.SetDirty(chest);
                        }
#endif
                    }

                    _placedPositions.Add(hit.point);
                    _occupancyMap.Register(hit.point, solidRadius: 1.2f, canopyRadius: 0f, OccupancyType.Resource_Solid);
                    chestsPlaced++;
                    break;
                }
            }

            DuskLog.Log(LogChannel.Loot, $"[WorldPropsPlacer] {chestsPlaced}/{propsConfig.totalChests} chests placed successfully.");
        }

        private GameObject InstantiateProp(GameObject prefab, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            EnsureContainer();
            GameObject go;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                go = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, propsContainer);
                if (go == null)
                {
                    go = Instantiate(prefab, position, rotation, propsContainer);
                }
                go.transform.SetPositionAndRotation(position, rotation);
                UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Spawn Prop");
            }
            else
#endif
            {
                go = Instantiate(prefab, position, rotation, propsContainer);
                go.transform.SetPositionAndRotation(position, rotation);
            }

            go.transform.localScale = scale;

            // Support FishNet network spawning during active sessions.
            if (Application.isPlaying && InstanceFinder.ServerManager != null && InstanceFinder.ServerManager.Started)
            {
                if (go.TryGetComponent<NetworkObject>(out var nob))
                {
                    InstanceFinder.ServerManager.Spawn(go);
                }
            }

            return go;
        }

        private bool IsPositionOccupied(Vector3 pos, float radius)
        {
            return _occupancyMap.IsSolidOccupied(new Vector2(pos.x, pos.z), radius);
        }

        public void ClearProps()
        {
            _placedPositions.Clear();
            _occupancyMap.Clear();

            // Find and clear all child "WorldPropsContainer" containers of this object.
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child != null && child.name == "WorldPropsContainer")
                {
                    for (int c = child.childCount - 1; c >= 0; c--)
                    {
                        Transform prop = child.GetChild(c);
                        if (prop != null)
                        {
                            if (Application.isPlaying)
                            {
                                if (prop.TryGetComponent<NetworkObject>(out var nob) && nob.IsSpawned &&
                                    InstanceFinder.ServerManager != null && InstanceFinder.ServerManager.Started)
                                {
                                    InstanceFinder.ServerManager.Despawn(nob.gameObject);
                                }
                                else
                                {
                                    Destroy(prop.gameObject);
                                }
                            }
                            else
                            {
                                DestroyImmediate(prop.gameObject);
                            }
                        }
                    }
                }
            }

            ClearSpawnPoints();
            EnsureContainer();
            EnsureSpawnPointsContainer();
        }

        public void ClearSpawnPoints()
        {
            _spawnPoints.Clear();

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child != null && child.name == "SpawnPoints")
                {
                    for (int c = child.childCount - 1; c >= 0; c--)
                    {
                        Transform sp = child.GetChild(c);
                        if (sp != null)
                        {
                            if (Application.isPlaying)
                                Destroy(sp.gameObject);
                            else
                                DestroyImmediate(sp.gameObject);
                        }
                    }
                }
            }
        }

        public void EnsureSpawnPointsContainer()
        {
            if (spawnPointsContainer != null) return;

            Transform existing = transform.Find("SpawnPoints");
            if (existing != null)
            {
                spawnPointsContainer = existing;
            }
            else
            {
                GameObject go = new GameObject("SpawnPoints");
                go.transform.parent = transform;
                go.transform.localPosition = Vector3.zero;
                spawnPointsContainer = go.transform;
            }
        }

        public void SyncWithPlayerSpawners(Transform[] spawnTransforms)
        {
            if (spawnTransforms == null || spawnTransforms.Length == 0) return;

            var fishnetSpawner = FindAnyObjectByType<FishNet.Component.Spawning.PlayerSpawner>();
            if (fishnetSpawner != null)
            {
                fishnetSpawner.Spawns = spawnTransforms;
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    UnityEditor.EditorUtility.SetDirty(fishnetSpawner);
                }
#endif
            }

            var duskbornSpawner = FindAnyObjectByType<Duskborn.Network.PlayerSpawner>();
            if (duskbornSpawner != null)
            {
                duskbornSpawner.SetSpawnPoints(spawnTransforms);
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    UnityEditor.EditorUtility.SetDirty(duskbornSpawner);
                }
#endif
            }
        }

        public void EnsureContainer()
        {
            if (propsContainer != null) return;

            Transform existing = transform.Find("WorldPropsContainer");
            if (existing != null)
            {
                propsContainer = existing;
            }
            else
            {
                GameObject containerGO = new GameObject("WorldPropsContainer");
                containerGO.transform.parent = transform;
                containerGO.transform.localPosition = Vector3.zero;
                propsContainer = containerGO.transform;
            }
        }
    }
}
