#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.AI.Navigation;
using Unity.AI.Navigation.Editor;

namespace Duskborn.Editor
{
    [CustomEditor(typeof(ChunkGridManager))]
    public class ChunkGridManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            ChunkGridManager manager = (ChunkGridManager)target;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Mundos Pré-Configurados (Presets Prontos)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Clique em qualquer preset abaixo para carregar a configuração e gerar o mundo completo automaticamente:", MessageType.None);

            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.35f, 0.85f, 0.95f);
            if (GUILayout.Button("🏝️ Ilha Náufragos", GUILayout.Height(28)))
            {
                ApplyPreset(manager, "Assets/_Duskborn/ScriptableObjects/World/TerrainConfig_ArchipelagoIslands.asset");
            }
            GUI.backgroundColor = new Color(0.95f, 0.75f, 0.35f);
            if (GUILayout.Button("⚔️ Platôs Combate", GUILayout.Height(28)))
            {
                ApplyPreset(manager, "Assets/_Duskborn/ScriptableObjects/World/TerrainConfig_TerracedPlateaus.asset");
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.75f, 0.65f, 0.95f);
            if (GUILayout.Button("⛰️ Vale dos Titãs", GUILayout.Height(28)))
            {
                ApplyPreset(manager, "Assets/_Duskborn/ScriptableObjects/World/TerrainConfig_SteepMountains.asset");
            }
            GUI.backgroundColor = new Color(0.55f, 0.88f, 0.45f);
            if (GUILayout.Button("🌾 Planícies Ermas", GUILayout.Height(28)))
            {
                ApplyPreset(manager, "Assets/_Duskborn/ScriptableObjects/World/TerrainConfig_RollingPlains.asset");
            }
            GUI.backgroundColor = new Color(0.95f, 0.85f, 0.55f);
            if (GUILayout.Button("🛡️ Co-op 5x5", GUILayout.Height(28)))
            {
                ApplyPreset(manager, "Assets/_Duskborn/ScriptableObjects/World/TerrainConfig_CoopWorld_5x5.asset");
            }
            EditorGUILayout.EndHorizontal();
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Controles de Terreno & Semente", EditorStyles.boldLabel);

            if (manager.config == null)
            {
                EditorGUILayout.HelpBox("Atribua um asset de LowPolyTerrainConfig acima para gerar o terreno.", MessageType.Warning);
            }
            if (manager.vertexColorMaterial == null)
            {
                EditorGUILayout.HelpBox("Atribua um Material de cores de vértice (M_TerrainLowPoly) acima.", MessageType.Info);
            }
            if (manager.propsConfig == null)
            {
                EditorGUILayout.HelpBox("Atribua um asset de WorldPropsConfig acima para posicionar recursos, baús e a bancada.", MessageType.Info);
            }

            if (manager.useRandomSeed)
            {
                EditorGUILayout.HelpBox("🎲 Modo Aleatório ATIVO: Uma nova semente será sorteada automaticamente a cada clique em 'Gerar Terreno'.", MessageType.None);
            }

            GUI.enabled = manager.config != null;

            // Generate with current settings
            GUI.backgroundColor = new Color(0.35f, 0.85f, 0.35f);
            string generateLabel = manager.useRandomSeed 
                ? "🎲 Gerar Terreno & Props (com Nova Semente Aleatória)" 
                : $"Gerar Terreno & Props (Seed: {manager.customSeed})";

            if (GUILayout.Button(generateLabel, GUILayout.Height(34)))
            {
                Undo.RecordObject(manager, "Gerar Terreno Low-Poly");
                manager.GenerateGrid();
                manager.EnsureNavMeshSurface();
                if (manager.navMeshSurface != null && !Application.isPlaying)
                {
                    NavMeshAssetManager.instance.StartBakingSurfaces(new UnityEngine.Object[] { manager.navMeshSurface });
                    EditorUtility.SetDirty(manager.navMeshSurface);
                }
                EditorUtility.SetDirty(manager);
                if (!Application.isPlaying)
                {
                    EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
                }
            }

            // Quick button to force a new random seed regardless of toggle
            GUI.backgroundColor = new Color(0.35f, 0.75f, 0.95f);
            if (GUILayout.Button("🎲 Sortear Nova Semente e Regerar Tudo", GUILayout.Height(28)))
            {
                Undo.RecordObject(manager, "Sortear Semente e Gerar Terreno");
                manager.customSeed = Random.Range(1, 9999999);
                manager.GenerateGrid();
                manager.EnsureNavMeshSurface();
                if (manager.navMeshSurface != null && !Application.isPlaying)
                {
                    NavMeshAssetManager.instance.StartBakingSurfaces(new UnityEngine.Object[] { manager.navMeshSurface });
                    EditorUtility.SetDirty(manager.navMeshSurface);
                }
                EditorUtility.SetDirty(manager);
                if (!Application.isPlaying)
                {
                    EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
                }
            }

            // Button to regenerate only props
            if (manager.propsConfig != null)
            {
                int currentPropsSeed = manager.propsSeed != 0 ? manager.propsSeed : manager.customSeed;
                string regenPropsLabel = manager.useRandomSeed 
                    ? "🎲 Regerar Apenas Props (Nova Semente Aleatória)" 
                    : $"🌲 Regerar Apenas Props (Seed: {currentPropsSeed})";

                GUI.backgroundColor = new Color(0.85f, 0.65f, 0.25f);
                if (GUILayout.Button(regenPropsLabel, GUILayout.Height(28)))
                {
                    Undo.RecordObject(manager, "Regerar Apenas Props");
                    manager.RegeneratePropsOnly(manager.useRandomSeed);
                    EditorUtility.SetDirty(manager);
                    if (!Application.isPlaying)
                    {
                        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
                    }
                }

                GUI.backgroundColor = new Color(0.95f, 0.75f, 0.35f);
                if (GUILayout.Button("🎲 Sortear Nova Semente e Regerar Props", GUILayout.Height(26)))
                {
                    Undo.RecordObject(manager, "Sortear Semente e Regerar Props");
                    manager.propsSeed = Random.Range(1, 9999999);
                    manager.RegeneratePropsOnly(false);
                    EditorUtility.SetDirty(manager);
                    if (!Application.isPlaying)
                    {
                        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
                    }
                }
            }

            if (manager.generateFoliage)
            {
                GUI.backgroundColor = new Color(0.45f, 0.85f, 0.35f);
                if (GUILayout.Button("🌾 Regerar Apenas Folhagem (Grama & Arbustos)", GUILayout.Height(28)))
                {
                    Undo.RecordObject(manager, "Regerar Folhagem");
                    manager.RegenerateFoliageOnly();
                    EditorUtility.SetDirty(manager);
                    if (!Application.isPlaying)
                    {
                        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
                    }
                }
            }

            if (manager.propsPlacer != null)
            {
                var occ = manager.propsPlacer.OccupancyMap;
                int occCount = occ != null ? occ.Count : 0;
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox($"📍 Mapa de Ocupação Espacial: {occCount} registros ativos (Santuário, Clareiras, Recursos Sólidos e Copas). Folhagem configurada para zero sobreposição.", MessageType.Info);

                GUI.backgroundColor = new Color(0.35f, 0.75f, 0.95f);
                if (GUILayout.Button("🧪 Executar Testes de Validação (Ocupação & Folhagem)", GUILayout.Height(24)))
                {
                    SpatialOccupancyMapTests.RunAllTests();
                }

                EditorGUILayout.Space(4);
                GUI.backgroundColor = new Color(0.85f, 0.45f, 0.95f);
                if (GUILayout.Button("🔨 Reimportar & Validar Modelos/Prefabs de Recursos", GUILayout.Height(28)))
                {
                    ReimportAndValidateResourceNodes(manager);
                }
            }

            if (manager.generateWaterPlane)
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("Controle de Altura da Água (Tempo Real)", EditorStyles.boldLabel);
                float currentEffective = manager.EffectiveWaterLevel;
                string baseLevelStr = manager.config != null ? $"{manager.config.waterLevel:F1}m" : "N/A";
                EditorGUILayout.HelpBox($"Nível Base (Config): {baseLevelStr}  |  Nível Efetivo Atual: {currentEffective:F1}m", MessageType.None);

                EditorGUI.BeginChangeCheck();
                float newOffset = EditorGUILayout.Slider("Ajuste de Altura (Offset)", manager.waterHeightOffset, -6f, 10f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(manager, "Ajustar Altura da Água");
                    manager.waterHeightOffset = newOffset;
                    manager.UpdateWaterPlanePosition();
                    EditorUtility.SetDirty(manager);
                    if (!Application.isPlaying)
                    {
                        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
                    }
                }

                GUI.backgroundColor = new Color(0.35f, 0.75f, 0.95f);
                if (GUILayout.Button("🌊 Regerar Malha do Plano de Água", GUILayout.Height(26)))
                {
                    Undo.RecordObject(manager, "Regerar Plano de Água");
                    manager.GenerateWaterPlane();
                    EditorUtility.SetDirty(manager);
                    if (!Application.isPlaying)
                    {
                        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
                    }
                }
            }

            GUI.backgroundColor = Color.white;
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Navegação & AI (NavMesh)", EditorStyles.boldLabel);

            var surface = manager.navMeshSurface != null ? manager.navMeshSurface : manager.GetComponent<NavMeshSurface>();
            bool hasSurface = surface != null;
            bool hasBakedData = hasSurface && surface.navMeshData != null;

            if (!hasSurface)
            {
                EditorGUILayout.HelpBox("⚠️ Nenhum NavMeshSurface detectado neste GameObject.", MessageType.Warning);
                GUI.backgroundColor = new Color(1f, 0.8f, 0.3f);
                if (GUILayout.Button("+ Adicionar & Configurar NavMeshSurface", GUILayout.Height(26)))
                {
                    Undo.RecordObject(manager.gameObject, "Adicionar NavMeshSurface");
                    manager.EnsureNavMeshSurface();
                    EditorUtility.SetDirty(manager.gameObject);
                    if (!Application.isPlaying)
                    {
                        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
                    }
                }
            }
            else
            {
                if (hasBakedData)
                {
                    EditorGUILayout.HelpBox($"✅ NavMesh Ativo e Válido! ({surface.navMeshData.name})", MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox("⚠️ NavMeshSurface presente, mas ainda sem malha assada. Clique no botão abaixo para gerar.", MessageType.Warning);
                }
            }

            // NavMesh Bake Button
            GUI.backgroundColor = new Color(0.25f, 0.7f, 1f);
            if (GUILayout.Button("Assar / Recalcular NavMesh (Rebake)", GUILayout.Height(30)))
            {
                Undo.RecordObject(manager, "Assar NavMesh");
                manager.EnsureNavMeshSurface();
                manager.RebuildNavMesh();

                if (!Application.isPlaying && manager.navMeshSurface != null)
                {
                    NavMeshAssetManager.instance.StartBakingSurfaces(new UnityEngine.Object[] { manager.navMeshSurface });
                    EditorUtility.SetDirty(manager.navMeshSurface);
                    EditorUtility.SetDirty(manager.gameObject);
                    EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
                }
            }

            if (hasBakedData)
            {
                GUI.backgroundColor = new Color(0.85f, 0.4f, 0.4f);
                if (GUILayout.Button("Limpar Dados do NavMesh", GUILayout.Height(22)))
                {
                    if (surface != null)
                    {
                        Undo.RecordObject(surface, "Limpar NavMesh");
                        NavMeshAssetManager.instance.ClearSurfaces(new UnityEngine.Object[] { surface });
                        EditorUtility.SetDirty(surface);
                        if (!Application.isPlaying)
                        {
                            EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
                        }
                    }
                }
            }

            EditorGUILayout.Space(8);

            // Clear button
            GUI.backgroundColor = new Color(0.95f, 0.4f, 0.4f);
            if (GUILayout.Button("Limpar Terreno", GUILayout.Height(26)))
            {
                Undo.RecordObject(manager, "Limpar Terreno Low-Poly");
                manager.ClearGrid();
                EditorUtility.SetDirty(manager);
                if (!Application.isPlaying)
                {
                    EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
                }
            }

            GUI.backgroundColor = Color.white;
            GUI.enabled = true;
        }

        private void ApplyPreset(ChunkGridManager manager, string assetPath)
        {
            var preset = AssetDatabase.LoadAssetAtPath<LowPolyTerrainConfig>(assetPath);
            if (preset != null)
            {
                Undo.RecordObject(manager, "Aplicar Preset de Terreno");
                manager.config = preset;
                manager.GenerateGrid();
                manager.EnsureNavMeshSurface();
                if (manager.navMeshSurface != null && !Application.isPlaying)
                {
                    NavMeshAssetManager.instance.StartBakingSurfaces(new UnityEngine.Object[] { manager.navMeshSurface });
                    EditorUtility.SetDirty(manager.navMeshSurface);
                }
                EditorUtility.SetDirty(manager);
                if (!Application.isPlaying)
                {
                    EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
                }
                Debug.Log($"[ChunkGridManager] Preset '{preset.name}' aplicado e mundo regerado com sucesso.");
            }
            else
            {
                Debug.LogError($"[ChunkGridManagerEditor] Não foi possível carregar o preset em: {assetPath}");
            }
        }

        private static void ReimportAndValidateResourceNodes(ChunkGridManager manager)
        {
            string[] fbxPaths = new string[]
            {
                "Assets/_Duskborn/Art/Models/Tree_Pine.fbx",
                "Assets/_Duskborn/Art/Models/Tree_Birch.fbx",
                "Assets/_Duskborn/Art/Models/StoneRock.fbx",
                "Assets/_Duskborn/Art/Models/StoneMonolith.fbx",
                "Assets/_Duskborn/Art/Models/StoneOutcrop.fbx",
                "Assets/_Duskborn/Art/Models/IronRidge.fbx",
                "Assets/_Duskborn/Art/Models/IronVein.fbx",
                "Assets/_Duskborn/Art/Models/FiberHerbs.fbx",
                "Assets/_Duskborn/Art/Models/FiberFern.fbx",
                "Assets/_Duskborn/Art/Models/FiberReeds.fbx"
            };

            for (int i = 0; i < fbxPaths.Length; i++)
            {
                AssetDatabase.ImportAsset(fbxPaths[i], ImportAssetOptions.ForceUpdate);
            }

            string[] prefabPaths = new string[]
            {
                "Assets/_Duskborn/Prefabs/World/ResourceNode.prefab",
                "Assets/_Duskborn/Prefabs/World/ResourceNode_Pine.prefab",
                "Assets/_Duskborn/Prefabs/World/ResourceNode_Birch.prefab",
                "Assets/_Duskborn/Prefabs/World/Node_Stone.prefab",
                "Assets/_Duskborn/Prefabs/World/Node_Stone_Monolith.prefab",
                "Assets/_Duskborn/Prefabs/World/Node_Stone_Outcrop.prefab",
                "Assets/_Duskborn/Prefabs/World/Node_Iron.prefab",
                "Assets/_Duskborn/Prefabs/World/Node_Iron_Ridge.prefab",
                "Assets/_Duskborn/Prefabs/World/Node_Iron_Vein.prefab",
                "Assets/_Duskborn/Prefabs/World/Node_Fiber.prefab",
                "Assets/_Duskborn/Prefabs/World/Node_Fiber_Herbs.prefab",
                "Assets/_Duskborn/Prefabs/World/Node_Fiber_Fern.prefab"
            };

            for (int i = 0; i < prefabPaths.Length; i++)
            {
                AssetDatabase.ImportAsset(prefabPaths[i], ImportAssetOptions.ForceUpdate);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (manager != null && manager.propsPlacer != null && manager.propsPlacer.PropsContainer != null)
            {
                var sceneNobs = manager.propsPlacer.PropsContainer.GetComponentsInChildren<FishNet.Object.NetworkObject>(true);
                var reserializeMethod = typeof(FishNet.Object.NetworkObject).GetMethod("ReserializeEditorSetValues",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                for (int i = 0; i < sceneNobs.Length; i++)
                {
                    if (sceneNobs[i] != null)
                    {
                        reserializeMethod?.Invoke(sceneNobs[i], new object[] { true, true });
                        EditorUtility.SetDirty(sceneNobs[i]);
                    }
                }
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== DUSKBORN MODEL & PREFAB VALIDATION ===");

            int totalOk = 0;
            for (int i = 0; i < prefabPaths.Length; i++)
            {
                string path = prefabPaths[i];
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null)
                {
                    sb.AppendLine($"❌ {path}: Falha ao carregar Prefab!");
                    continue;
                }

                var mf = go.GetComponent<MeshFilter>();
                var mr = go.GetComponent<MeshRenderer>();
                var col = go.GetComponent<Collider>();
                var nob = go.GetComponent<FishNet.Object.NetworkObject>();

                bool hasMesh = mf != null && mf.sharedMesh != null && mf.sharedMesh.vertexCount > 0;
                bool hasMat = mr != null && mr.sharedMaterial != null;
                bool hasCol = col != null;
                bool hasNob = nob != null && nob.PrefabId != FishNet.Object.NetworkObject.UNSET_PREFABID_VALUE;

                string status = (hasMesh && hasMat && hasCol && hasNob) ? "✅ OK" : "⚠️ AVISO";
                if (hasMesh && hasMat && hasCol && hasNob) totalOk++;

                string meshInfo = hasMesh ? $"Mesh: '{mf.sharedMesh.name}' ({mf.sharedMesh.vertexCount} verts)" : "SEM MALHA!";
                string matInfo = hasMat ? $"Mat: '{mr.sharedMaterial.name}'" : "SEM MATERIAL!";
                string nobInfo = hasNob ? $"PrefabId: {nob.PrefabId}" : "NetworkObject PENDENTE";

                sb.AppendLine($"{status} | {go.name,-24} | {meshInfo,-32} | {matInfo,-24} | {nobInfo}");
            }

            sb.AppendLine($"Resultado: {totalOk}/{prefabPaths.Length} prefabs 100% validados e sincronizados.");
            Debug.Log(sb.ToString());
            EditorUtility.DisplayDialog("Validação de Modelos", $"Validação concluída: {totalOk}/{prefabPaths.Length} prefabs validados com sucesso!\nConsulte a Console para o relatório completo.", "OK");
        }
    }
}
#endif
