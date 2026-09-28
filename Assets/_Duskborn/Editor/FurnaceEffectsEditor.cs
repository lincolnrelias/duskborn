using Duskborn.Effects;
using Duskborn.Gameplay.Building;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Duskborn.Editor
{
    [CustomEditor(typeof(FurnaceEffects))]
    public sealed class FurnaceEffectsEditor : UnityEditor.Editor
    {
        private FurnaceEffects effect;
        private static bool isPlaying = true;
        private static bool isOperating = true;
        private static float playbackSpeed = 1.0f;
        private static bool showFurnaceReference = true;
        private static bool darkLighting = false;

        private double lastEditorTime;
        private GameObject sceneGhostFurnace;

        // Inspector 3D preview utility
        private PreviewRenderUtility previewUtility;
        private GameObject previewModel;
        private FurnaceEffects previewEffect;
        private Vector2 previewOrbit = new Vector2(25f, -30f);
        private Vector3 previewPivot = new Vector3(0f, 1.35f, 0f);
        private float previewDistance = 4.2f;
        private double lastPreviewTime;

        private void OnEnable()
        {
            effect = (FurnaceEffects)target;
            lastEditorTime = EditorApplication.timeSinceStartup;
            lastPreviewTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            DestroyGhostFurnace();
            CleanupPreviewUtility();
        }

        private void OnEditorUpdate()
        {
            if (effect == null) return;

            if (PrefabUtility.IsPartOfPrefabAsset(effect.gameObject))
            {
                if (isPlaying) Repaint();
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            float dt = Mathf.Min(0.05f, (float)(now - lastEditorTime));
            lastEditorTime = now;

            if (isPlaying && !Application.isPlaying)
            {
                effect.EnsureBuilt();
                effect.SetOperating(isOperating);
                Camera sceneCam = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.camera : null;
                effect.SimulateEditor(dt * playbackSpeed, sceneCam);
                SceneView.RepaintAll();
            }

            // Sync ghost reference model in Scene / Prefab mode
            UpdateGhostFurnace();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Preview Playback Bar (similar to Particle System inspector)
            DrawPlaybackControls();

            EditorGUILayout.Space(8);

            // Draw categorized properties
            DrawCustomProperties();

            if (serializedObject.ApplyModifiedProperties())
            {
                if (effect != null)
                {
                    effect.ApplyConfiguration();
                }
                SyncPreviewProperties();
            }
        }

        private void DrawPlaybackControls()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Controles de Preview (Estilo Particle System)", EditorStyles.boldLabel);

            if (PrefabUtility.IsPartOfPrefabAsset(effect.gameObject))
            {
                EditorGUILayout.HelpBox("Visualização 3D interativa ativa abaixo no Preview do Inspector. Para simular diretamente no Scene View, abra o Prefab no Prefab Stage ou posicione-o na cena.", MessageType.Info);
            }

            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = isPlaying ? new Color(0.9f, 0.4f, 0.4f) : new Color(0.4f, 0.9f, 0.4f);
            if (GUILayout.Button(isPlaying ? "⏸ Pausar" : "▶ Reproduzir", GUILayout.Height(26)))
            {
                isPlaying = !isPlaying;
                lastEditorTime = EditorApplication.timeSinceStartup;
            }
            GUI.backgroundColor = Color.white;

            if (GUILayout.Button("⏹ Parar", GUILayout.Height(26)))
            {
                isPlaying = false;
                effect.ResetSimulation();
                SceneView.RepaintAll();
            }

            if (GUILayout.Button("⏮ Reiniciar", GUILayout.Height(26)))
            {
                effect.ResetSimulation();
                isPlaying = true;
                lastEditorTime = EditorApplication.timeSinceStartup;
                SceneView.RepaintAll();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            bool newOperating = GUILayout.Toggle(isOperating, " Simular Fornalha Ativa (Queima)", "Button");
            if (newOperating != isOperating)
            {
                isOperating = newOperating;
                effect.SetOperating(isOperating);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Velocidade:", GUILayout.Width(70));
            if (GUILayout.Toggle(Mathf.Approximately(playbackSpeed, 0.5f), "0.5x", "Button")) playbackSpeed = 0.5f;
            if (GUILayout.Toggle(Mathf.Approximately(playbackSpeed, 1.0f), "1.0x", "Button")) playbackSpeed = 1.0f;
            if (GUILayout.Toggle(Mathf.Approximately(playbackSpeed, 2.0f), "2.0x", "Button")) playbackSpeed = 2.0f;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2);

            // Live metrics
            float heatPercent = effect.CurrentHeat * 100f;
            int particleCount = effect.LiveParticleCount;
            int maxParticles = effect.maxSmokeParticles;
            EditorGUILayout.LabelField($"Calor: {heatPercent:F0}%  |  Partículas de Fumaça: {particleCount} / {maxParticles}");

            EditorGUILayout.EndVertical();
        }

        private void DrawCustomProperties()
        {
            SerializedProperty prop = serializedObject.GetIterator();
            bool enterChildren = true;
            while (prop.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (prop.name == "m_Script") continue;
                EditorGUILayout.PropertyField(prop, true);
            }
        }

        private void OnSceneGUI()
        {
            if (effect == null || PrefabUtility.IsPartOfPrefabAsset(effect.gameObject)) return;

            Handles.BeginGUI();
            var sceneView = SceneView.currentDrawingSceneView ?? SceneView.lastActiveSceneView;
            if (sceneView != null)
            {
                float width = 230;
                float height = 145;
                var rect = new Rect(sceneView.position.width - width - 12, sceneView.position.height - height - 24, width, height);

                GUILayout.BeginArea(rect, "Fornalha • Efeitos VFX", GUI.skin.window);

                EditorGUILayout.BeginHorizontal();
                GUI.backgroundColor = isPlaying ? new Color(0.9f, 0.4f, 0.4f) : new Color(0.4f, 0.9f, 0.4f);
                if (GUILayout.Button(isPlaying ? "⏸ Pausar" : "▶ Play"))
                {
                    isPlaying = !isPlaying;
                    lastEditorTime = EditorApplication.timeSinceStartup;
                }
                GUI.backgroundColor = Color.white;

                if (GUILayout.Button("⏹ Parar"))
                {
                    isPlaying = false;
                    effect.ResetSimulation();
                }

                if (GUILayout.Button("⏮ Reiniciar"))
                {
                    effect.ResetSimulation();
                    isPlaying = true;
                    lastEditorTime = EditorApplication.timeSinceStartup;
                }
                EditorGUILayout.EndHorizontal();

                isOperating = GUILayout.Toggle(isOperating, isOperating ? "🔥 Fogo & Fumaça: Ativo" : "❄ Resfriamento / Idle");

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Vel:", GUILayout.Width(30));
                playbackSpeed = EditorGUILayout.Slider(playbackSpeed, 0.25f, 3.0f);
                EditorGUILayout.EndHorizontal();

                showFurnaceReference = GUILayout.Toggle(showFurnaceReference, "Exibir Modelo da Fornalha");

                EditorGUILayout.LabelField($"Calor: {effect.CurrentHeat * 100f:F0}% | Partículas: {effect.LiveParticleCount}");

                GUILayout.EndArea();
            }
            Handles.EndGUI();
        }

        private void UpdateGhostFurnace()
        {
            if (effect == null || PrefabUtility.IsPartOfPrefabAsset(effect.gameObject))
            {
                DestroyGhostFurnace();
                return;
            }

            bool isPrefabStage = PrefabStageUtility.GetCurrentPrefabStage() != null;
            bool needsGhost = showFurnaceReference && isPrefabStage;

            if (needsGhost)
            {
                if (sceneGhostFurnace == null)
                {
                    var definition = AssetDatabase.LoadAssetAtPath<BuildableDefinition>("Assets/_Duskborn/Resources/Building/Build_forge.asset");
                    if (definition != null)
                    {
                        sceneGhostFurnace = BuildingWorld.CreateVisual(definition);
                        sceneGhostFurnace.name = "Preview_Ghost_Furnace";
                        sceneGhostFurnace.hideFlags = HideFlags.HideAndDontSave;
                        sceneGhostFurnace.transform.SetParent(effect.transform, false);
                        sceneGhostFurnace.transform.localPosition = Vector3.zero;
                        sceneGhostFurnace.transform.localRotation = Quaternion.identity;
                        effect.PrepareModel(sceneGhostFurnace);
                    }
                }
            }
            else
            {
                DestroyGhostFurnace();
            }
        }

        private void DestroyGhostFurnace()
        {
            if (sceneGhostFurnace != null)
            {
                DestroyImmediate(sceneGhostFurnace);
                sceneGhostFurnace = null;
            }
        }

        // ==========================================
        // Interactive 3D Inspector Preview
        // ==========================================

        public override bool HasPreviewGUI() => true;

        public override GUIContent GetPreviewTitle() => new GUIContent("Fornalha VFX Preview");

        public override void OnPreviewSettings()
        {
            GUI.backgroundColor = isPlaying ? new Color(0.9f, 0.4f, 0.4f) : new Color(0.4f, 0.9f, 0.4f);
            if (GUILayout.Button(isPlaying ? "Pause" : "Play", EditorStyles.toolbarButton))
            {
                isPlaying = !isPlaying;
                lastPreviewTime = EditorApplication.timeSinceStartup;
            }
            GUI.backgroundColor = Color.white;

            if (GUILayout.Button("Reset", EditorStyles.toolbarButton))
            {
                if (previewEffect != null) previewEffect.ResetSimulation();
                previewOrbit = new Vector2(25f, -30f);
                previewDistance = 4.2f;
            }

            darkLighting = GUILayout.Toggle(darkLighting, "Noite/Escuro", EditorStyles.toolbarButton);
        }

        public override void OnPreviewGUI(Rect r, GUIStyle background)
        {
            if (Event.current.type == EventType.Repaint && previewUtility == null)
            {
                InitPreviewUtility();
            }

            if (previewUtility == null)
            {
                EditorGUI.LabelField(r, "Carregando preview da fornalha...", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            HandlePreviewInput(r);

            double now = EditorApplication.timeSinceStartup;
            float dt = Mathf.Min(0.05f, (float)(now - lastPreviewTime));
            lastPreviewTime = now;

            if (isPlaying && previewEffect != null)
            {
                previewEffect.SetOperating(isOperating);
                previewEffect.SimulateEditor(dt * playbackSpeed, previewUtility.camera);
            }

            if (Event.current.type != EventType.Repaint) return;

            // Camera setup
            Quaternion rot = Quaternion.Euler(previewOrbit.x, previewOrbit.y, 0f);
            Vector3 camPos = previewPivot + rot * new Vector3(0, 0, -previewDistance);
            previewUtility.camera.transform.position = camPos;
            previewUtility.camera.transform.LookAt(previewPivot);
            previewUtility.camera.backgroundColor = darkLighting ? new Color(0.015f, 0.02f, 0.03f) : new Color(0.22f, 0.26f, 0.30f);

            // Lighting setup
            previewUtility.lights[0].intensity = darkLighting ? 0.15f : 1.15f;
            previewUtility.lights[0].transform.rotation = Quaternion.Euler(45, -35, 0);
            previewUtility.lights[1].intensity = darkLighting ? 0.04f : 0.35f;
            previewUtility.ambientColor = darkLighting ? new Color(0.03f, 0.04f, 0.05f) : new Color(0.32f, 0.34f, 0.36f);

            previewUtility.BeginPreview(r, background);
            previewUtility.Render(true);
            Texture rendered = previewUtility.EndPreview();
            GUI.DrawTexture(r, rendered, ScaleMode.StretchToFill, false);

            Repaint();
        }

        private void HandlePreviewInput(Rect r)
        {
            int controlID = GUIUtility.GetControlID("FurnacePreviewControl".GetHashCode(), FocusType.Passive);
            Event current = Event.current;
            switch (current.GetTypeForControl(controlID))
            {
                case EventType.MouseDown:
                    if (r.Contains(current.mousePosition))
                    {
                        GUIUtility.hotControl = controlID;
                        current.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == controlID)
                    {
                        if (current.button == 0)
                        {
                            previewOrbit.y += current.delta.x * 0.7f;
                            previewOrbit.x = Mathf.Clamp(previewOrbit.x + current.delta.y * 0.7f, -80f, 80f);
                            current.Use();
                            GUI.changed = true;
                        }
                        else if (current.button == 1 || current.button == 2)
                        {
                            previewPivot.y -= current.delta.y * 0.005f;
                            current.Use();
                            GUI.changed = true;
                        }
                    }
                    break;
                case EventType.ScrollWheel:
                    if (r.Contains(current.mousePosition))
                    {
                        previewDistance = Mathf.Clamp(previewDistance + current.delta.y * 0.25f, 1.8f, 9f);
                        current.Use();
                        GUI.changed = true;
                    }
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == controlID)
                    {
                        GUIUtility.hotControl = 0;
                        current.Use();
                    }
                    break;
            }
        }

        private void InitPreviewUtility()
        {
            CleanupPreviewUtility();
            var definition = AssetDatabase.LoadAssetAtPath<BuildableDefinition>("Assets/_Duskborn/Resources/Building/Build_forge.asset");
            if (definition == null || definition.operatingEffect == null) return;

            previewUtility = new PreviewRenderUtility();
            previewUtility.camera.fieldOfView = 38f;
            previewUtility.camera.clearFlags = CameraClearFlags.SolidColor;

            previewModel = BuildingWorld.CreateVisual(definition);
            previewModel.hideFlags = HideFlags.HideAndDontSave;
            var placed = previewModel.AddComponent<PlacedBuilding>();
            placed.Initialize(definition, new BuildingState());

            previewEffect = Instantiate(definition.operatingEffect, previewModel.transform, false);
            previewEffect.hideFlags = HideFlags.HideAndDontSave;
            previewEffect.Bind(placed);

            SyncPreviewProperties();

            previewUtility.AddSingleGO(previewModel);
        }

        private void SyncPreviewProperties()
        {
            if (previewEffect == null || effect == null) return;
            previewEffect.smokeDensity = effect.smokeDensity;
            previewEffect.maxSmokeParticles = effect.maxSmokeParticles;
            previewEffect.smokeSize = effect.smokeSize;
            previewEffect.smokeAlpha = effect.smokeAlpha;
            previewEffect.smokeLifetime = effect.smokeLifetime;
            previewEffect.smokeSpeed = effect.smokeSpeed;
            previewEffect.smokeTint = effect.smokeTint;
            previewEffect.flameCount = effect.flameCount;
            previewEffect.flameScale = effect.flameScale;
            previewEffect.flameHeight = effect.flameHeight;
            previewEffect.flameSpread = effect.flameSpread;
            previewEffect.flameTint = effect.flameTint;
            previewEffect.coalIntensity = effect.coalIntensity;
            previewEffect.intensity = effect.intensity;
            previewEffect.lightIntensity = effect.lightIntensity;
            previewEffect.ApplyConfiguration();
        }

        private void CleanupPreviewUtility()
        {
            if (previewModel != null)
            {
                DestroyImmediate(previewModel);
                previewModel = null;
            }
            if (previewUtility != null)
            {
                previewUtility.Cleanup();
                previewUtility = null;
            }
        }
    }
}
