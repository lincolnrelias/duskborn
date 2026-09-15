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
        [Header("Hierarquia de Props & Spawns")]
        [Tooltip("Transform pai onde todos os props instanciados serão agrupados. Criado automaticamente se nulo.")]
        [SerializeField] private Transform propsContainer;

        [Tooltip("Transform pai onde os pontos de spawn de jogadores são gerados. Criado automaticamente se nulo.")]
        [SerializeField] private Transform spawnPointsContainer;

        public Transform PropsContainer => propsContainer;
        public Transform SpawnPointsContainer => spawnPointsContainer;
        public int PropsCount => propsContainer != null ? propsContainer.childCount : 0;

        private readonly List<Transform> _spawnPoints = new List<Transform>();
        public IReadOnlyList<Transform> SpawnPoints => _spawnPoints;

        private readonly List<Vector3> _placedPositions = new List<Vector3>();
        private readonly SpatialOccupancyMap _occupancyMap = new SpatialOccupancyMap();
        public SpatialOccupancyMap OccupancyMap => _occupancyMap;

        private bool _isSubscribed;

        private void Awake()
        {
            EnsureContainer();
            EnsureSpawnPointsContainer();
        }

        private void OnEnable()
        {
            SubscribeToNetworkEvents();

            if (Application.isPlaying)
            {
                StartCoroutine(FallbackOfflineActivationRoutine());
            }
        }

        private void Start()
        {
            SubscribeToNetworkEvents();

            EnsureSpawnPointsReady();

            // Se o servidor já iniciou antes ou durante o Start, spawna os props agora
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
                // Se for um cliente remoto conectado (não é Host), limpa props da cena local
                // para que receba apenas as instâncias de rede autoritativas sincronizadas pelo Host
                if (InstanceFinder.ServerManager == null || !InstanceFinder.ServerManager.Started)
                {
                    DuskLog.Log(LogChannel.World, "[WorldPropsPlacer] Conectado como CLIENT remoto. Limpando props locais da cena.");
                    ClearProps();
                }
            }
        }

        /// <summary>
        /// Ativa e spawna todos os NetworkObjects contidos no propsContainer na rede FishNet.
        /// Chamado assim que o servidor conclui sua inicialização.
        /// </summary>
        public void SpawnAllPropsOnServer()
        {
            if (!Application.isPlaying) return;
            if (InstanceFinder.ServerManager == null || !InstanceFinder.ServerManager.Started) return;

            EnsureContainer();
            if (propsContainer == null) return;

            NetworkObject[] nobs = propsContainer.GetComponentsInChildren<NetworkObject>(true);
            if (nobs == null || nobs.Length == 0)
            {
                // Se nenhum prop existe na cena, tenta gerar usando as configurações do ChunkGridManager
                if (ChunkGridManager.Instance != null && ChunkGridManager.Instance.config != null && ChunkGridManager.Instance.propsConfig != null)
                {
                    DuskLog.Log(LogChannel.World, "[WorldPropsPlacer] Nenhum prop existente na cena. Gerando props procedurais no servidor...");
                    PlaceWorldProps(ChunkGridManager.Instance.config, ChunkGridManager.Instance.propsConfig, ChunkGridManager.Instance.ActivePropsSeed);
                }
                return;
            }

            int spawnedCount = 0;
            for (int i = 0; i < nobs.Length; i++)
            {
                NetworkObject nob = nobs[i];
                if (nob == null || nob.IsSpawned) continue;

                // Reativa o GameObject que o FishNet desativou antes do servidor iniciar
                nob.gameObject.SetActive(true);
                InstanceFinder.ServerManager.Spawn(nob.gameObject);
                spawnedCount++;
            }

            DuskLog.Log(LogChannel.World, $"[WorldPropsPlacer] Servidor iniciado: {spawnedCount} props ativados e sincronizados na rede FishNet.");
        }

        /// <summary>
        /// Reativa todos os GameObjects no propsContainer localmente (sem rede).
        /// Útil como fallback para testes em Play Mode offline sem inicialização de rede FishNet.
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
                DuskLog.Log(LogChannel.World, "[WorldPropsPlacer] Rede inativa/offline detectada. Reativando props locais para modo de teste.");
                ActivateAllPropsLocally();
            }
        }

        public void PlaceWorldProps(LowPolyTerrainConfig terrainConfig, WorldPropsConfig propsConfig, int seed)
        {
            if (terrainConfig == null || propsConfig == null)
            {
                DuskLog.Warn(LogChannel.World, "[WorldPropsPlacer] Configuração de terreno ou de props nula!");
                return;
            }

            EnsureContainer();
            ClearProps();
            _occupancyMap.Clear();

            // Garante que os colliders recém-gerados dos chunks estejam sincronizados na física
            Physics.SyncTransforms();

            SeededRNG rng = new SeededRNG(seed);
            _placedPositions.Clear();

            // 1. Clareira Central (Centro do Mapa e Santuário)
            PlaceCentralClearing(terrainConfig, propsConfig, rng);

            // 2. Clareiras de Combate Preservadas (30-40% área aberta para kite e batalhas)
            PlaceCombatClearings(terrainConfig, propsConfig, rng);

            // 3. Recursos Naturais (Resource Nodes) por Chunk com Agrupamento (Clustering)
            PlaceResourceNodes(terrainConfig, propsConfig, rng);

            // 4. Distribuição de Baús (Chests) com Escalonamento de Distância
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
                DuskLog.Log(LogChannel.World, $"[WorldPropsPlacer] Serializados {countReserialized} NetworkObjects com SceneIds válidos no Editor.");
            }
#endif

            DuskLog.Log(LogChannel.World, $"[WorldPropsPlacer] Geração de props concluída! Total de posições: {_placedPositions.Count}, Ocupação indexada: {_occupancyMap.Count}.");
        }

        private bool RaycastGround(Vector3 rayOrigin, out RaycastHit groundHit)
        {
            RaycastHit[] hits = Physics.RaycastAll(rayOrigin, Vector3.down, 300f);
            float closestDist = float.MaxValue;
            groundHit = default;
            bool found = false;

            for (int i = 0; i < hits.Length; i++)
            {
                // Prioriza acertos contra malhas de terreno (TerrainChunk ou MeshCollider do relevo)
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

            // Fallback para qualquer collider se não encontrar TerrainChunk explícito
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
            // Registra o Santuário central no mapa de ocupação
            _occupancyMap.RegisterClearing(Vector2.zero, propsConfig.centerClearingRadius, OccupancyType.Player_Sanctuary);

            Vector3 centerRayOrigin = new Vector3(0f, 150f, 0f);
            float centerGroundY = 0f;

            if (RaycastGround(centerRayOrigin, out RaycastHit centerHit))
            {
                centerGroundY = centerHit.point.y;
            }

            // A) Posicionamento / Atualização de Pontos de Spawn de Jogadores
            SetupPlayerSpawnPoints(propsConfig, centerGroundY);

            // B) Posicionamento da Bancada (Workbench)
            if (propsConfig.workbenchPrefab != null)
            {
                Vector3 wbOffset = new Vector3(2.5f, 0f, 1.5f);
                Vector3 wbRayOrigin = new Vector3(wbOffset.x, 150f, wbOffset.z);
                Vector3 wbPos = wbOffset + Vector3.up * centerGroundY;

                if (RaycastGround(wbRayOrigin, out RaycastHit wbHit))
                {
                    wbPos = wbHit.point;
                }

                GameObject wbGO = InstantiateProp(propsConfig.workbenchPrefab, wbPos, Quaternion.Euler(0f, -45f, 0f), Vector3.one);
                _placedPositions.Add(wbPos);
                _occupancyMap.Register(wbPos, solidRadius: 1.5f, canopyRadius: 0f, OccupancyType.Resource_Solid);

                if (wbGO.TryGetComponent<Workbench>(out var wb))
                {
                    // Pronto para interação
                }
            }
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
                    // O chunk central já contém o Santuário inicial
                    if (cx == 0 && cz == 0) continue;

                    float chunkMinX = cx * chunkWorldLength;
                    float chunkMaxX = chunkMinX + chunkWorldLength;
                    float chunkMinZ = cz * chunkWorldLength;
                    float chunkMaxZ = chunkMinZ + chunkWorldLength;

                    // Posiciona uma clareira de combate aberta por chunk para preservar arena de combate (30-40% do mapa)
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
            DuskLog.Log(LogChannel.World, $"[WorldPropsPlacer] {_spawnPoints.Count} pontos de spawn de jogador gerados com sucesso com o relevo.");
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

                    // Rejeita santuário central
                    if (distCenterSq < centerRadiusSqr)
                        continue;

                    // Zoneamento radial (se configurado no prop)
                    if (propDef.minRadialDistance > 0f && distCenter < propDef.minRadialDistance)
                        continue;
                    if (propDef.maxRadialDistance > 0f && distCenter > propDef.maxRadialDistance)
                        continue;

                    // Margem de borda do mapa
                    if (distCenter + cluster.clusterRadius > maxBoundaryRadius)
                        continue;

                    Vector2 centerXZ = new Vector2(sampleX, sampleZ);

                    // Evita clareiras protegidas (Santuário e Arenas de Combate)
                    if (_occupancyMap.IsInClearing(centerXZ))
                        continue;

                    // Espaçamento inter-cluster com outros recursos
                    if (_occupancyMap.IsSolidOccupied(centerXZ, cluster.interClusterSpacing))
                        continue;

                    Vector3 rayOrigin = new Vector3(sampleX, 150f, sampleZ);
                    if (!RaycastGround(rayOrigin, out RaycastHit hit))
                        continue;

                    // Solo seco
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

                // Spawna os nós do aglomerado (Intra-cluster Poisson Disc Sampling)
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

                        // Espaçamento intra-cluster contra nós vizinhos
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

                    // Ignora clareira central de segurança
                    if (distSq < centerRadiusSqr)
                        continue;

                    // Zoneamento radial
                    if (propDef.minRadialDistance > 0f && distFromCenter < propDef.minRadialDistance)
                        continue;
                    if (propDef.maxRadialDistance > 0f && distFromCenter > propDef.maxRadialDistance)
                        continue;

                    // Ignora áreas fora da borda jogável
                    if (distSq > maxBoundaryRadius * maxBoundaryRadius)
                        continue;

                    Vector2 sampleXZ = new Vector2(sampleX, sampleZ);
                    if (_occupancyMap.IsInClearing(sampleXZ))
                        continue;

                    // Agrupamento orgânico legado
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

                    // Solo seco
                    if (hit.point.y <= terrainConfig.waterLevel + 0.35f)
                        continue;

                    float effectiveMinH = Mathf.Max(propDef.minHeight, terrainConfig.waterLevel + 0.35f);
                    if (hit.point.y < effectiveMinH || hit.point.y > propDef.maxHeight)
                        continue;

                    float slope = Vector3.Angle(hit.normal, Vector3.up);
                    if (slope > propDef.maxSlopeAngle)
                        continue;

                    // Raio de exclusão
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

                    // Evita spawnar colado ao ponto de spawn imediato (raio < 3m)
                    if (distFromCenter < 3f)
                        continue;

                    // Limite de segurança de borda
                    if (distFromCenter > maxBoundaryRadius)
                        continue;

                    Vector3 rayOrigin = new Vector3(sampleX, 150f, sampleZ);
                    if (!RaycastGround(rayOrigin, out RaycastHit hit))
                        continue;

                    // Solo seco (acima da água)
                    if (hit.point.y <= terrainConfig.waterLevel + 0.5f)
                        continue;

                    // Inclinação suave para estabilidade do baú nos platôs de combate
                    float slope = Vector3.Angle(hit.normal, Vector3.up);
                    if (slope > 18f)
                        continue;

                    // Exclusão de outros baús e props (raio de 3.0m)
                    if (_occupancyMap.IsSolidOccupied(new Vector2(hit.point.x, hit.point.z), 2.5f))
                        continue;

                    // Escalonamento por Distância (Risk vs. Reward)
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

            DuskLog.Log(LogChannel.Loot, $"[WorldPropsPlacer] {chestsPlaced}/{propsConfig.totalChests} baús posicionados com sucesso.");
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

            // Suporte a spawn de rede FishNet durante partidas ativas
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

            // Encontra e limpa todos os contêineres "WorldPropsContainer" filhos deste objeto
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
