#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Duskborn.Gameplay.Equipment;
using Duskborn.Inventory;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Duskborn.Editor
{
    /// <summary>
    /// Utilitário de Editor para visualização, ajuste de empunhadura/socket e teste de animações
    /// de armas e equipamentos no avatar do jogador.
    /// Utiliza o prefab real do jogador (Player 1.prefab) e renderiza em ambiente isolado via PreviewRenderUtility.
    /// </summary>
    public sealed class ItemFittingStudioWindow : EditorWindow
    {
        private const string DefaultCharacterPath = "Assets/_Duskborn/Prefabs/Player/Player 1.prefab";
        private const string FallbackCharacterPath = "Assets/_Duskborn/Art/Models/modelTextures/char.fbx";
        private const string ProfilesDirectory = "Assets/_Duskborn/ScriptableObjects/Equipment/Profiles";

        // Referências principais
        [SerializeField] private WeaponDefinition selectedWeapon;
        [SerializeField] private ItemAttachmentProfile attachmentProfile;
        [SerializeField] private GameObject customCharacterPrefab;

        // Estado do Preview
        private PreviewRenderUtility _preview;
        private GameObject _characterInstance;
        private Animator _animator;
        private GameObject _weaponInstance;

        // PlayableGraph para amostragem fiel de animações Humanoid
        private PlayableGraph _playableGraph;
        private AnimationPlayableOutput _playableOutput;
        private AnimationClipPlayable _clipPlayable;

        // Câmera do Preview
        private Vector3 _camTarget = new Vector3(0f, 1.0f, 0f);
        private float _camYaw = 160f; // Visto de frente
        private float _camPitch = 10f;
        private float _camDistance = 2.2f;
        private bool _darkBackground;
        private float _lightIntensity = 1.4f;

        // Transform atual sendo editado
        private HumanBodyBones _targetBone = HumanBodyBones.RightHand;
        private Vector3 _positionOffset = Vector3.zero;
        private Vector3 _rotationOffset = Vector3.zero;
        private Vector3 _scaleOffset = Vector3.one;
        private bool _uniformScale = true;

        // Animação & Timeline
        private struct ClipOption
        {
            public string Label;
            public AnimationClip Clip;
            public WeaponActionEvent[] Events;
        }

        private readonly List<ClipOption> _availableClips = new List<ClipOption>();
        private int _selectedClipIndex = 0;
        private AnimationClip _customClip;
        private bool _isPlaying;
        private bool _loopAnimation = true;
        private float _playbackSpeed = 1.0f;
        private float _currentTime;
        private float _normalizedTime;
        private double _lastUpdateTime;

        // UI Scroll
        private Vector2 _sidebarScroll;

        // Clipboard temporário
        private static Vector3 s_ClipPos;
        private static Vector3 s_ClipRot;
        private static Vector3 s_ClipScale = Vector3.one;
        private static bool s_HasClipboard;

        [MenuItem("Tools/Duskborn/Item Fitting Studio", priority = 102)]
        public static void Open()
        {
            var window = GetWindow<ItemFittingStudioWindow>("Ajuste de Itens");
            window.minSize = new Vector2(750, 480);
            window.Show();
        }

        public static void OpenWithWeapon(WeaponDefinition weapon)
        {
            var window = GetWindow<ItemFittingStudioWindow>("Ajuste de Itens");
            window.minSize = new Vector2(750, 480);
            bool isNewWeapon = window.selectedWeapon != weapon;
            window.selectedWeapon = weapon;
            if (isNewWeapon)
            {
                window.attachmentProfile = weapon != null ? weapon.AttachmentProfile : null;
                window.RefreshWeaponAndClips(resetOffsetsFromProfile: true);
            }
            else
            {
                if (window.attachmentProfile == null && weapon != null)
                {
                    window.attachmentProfile = weapon.AttachmentProfile;
                }
                window.RefreshWeaponAndClips(resetOffsetsFromProfile: false);
            }
            window.SaveWindowState();
            window.Show();
        }

        private void OnEnable()
        {
            _lastUpdateTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += OnEditorUpdate;
            bool loaded = LoadWindowState();
            InitPreview();
            RefreshWeaponAndClips(resetOffsetsFromProfile: !loaded);
        }

        private void OnDisable()
        {
            SaveWindowState();
            EditorApplication.update -= OnEditorUpdate;
            DestroyPlayableGraph();
            CleanupPreview();
        }

        private void OnDestroy()
        {
            SaveWindowState();
        }

        private void OnLostFocus()
        {
            SaveWindowState();
        }

        [Serializable]
        public sealed class WindowSavedState
        {
            public string weaponGuid = string.Empty;
            public string profileGuid = string.Empty;
            public string customCharacterGuid = string.Empty;
            public string customClipGuid = string.Empty;

            public Vector3 camTarget = new Vector3(0f, 1.0f, 0f);
            public float camYaw = 160f;
            public float camPitch = 10f;
            public float camDistance = 2.2f;
            public bool darkBackground;
            public float lightIntensity = 1.4f;

            public int targetBone = (int)HumanBodyBones.RightHand;
            public Vector3 positionOffset = Vector3.zero;
            public Vector3 rotationOffset = Vector3.zero;
            public Vector3 scaleOffset = Vector3.one;
            public bool uniformScale = true;

            public int selectedClipIndex;
            public bool loopAnimation = true;
            public float playbackSpeed = 1.0f;
            public float currentTime;
            public float normalizedTime;

            public Vector2 sidebarScroll;
        }

        public static string PrefKey => $"Duskborn_ItemFittingStudio_{Application.dataPath.GetHashCode():X8}_State";

        public void SaveWindowState()
        {
            var state = new WindowSavedState
            {
                weaponGuid = GetAssetGuid(selectedWeapon),
                profileGuid = GetAssetGuid(attachmentProfile),
                customCharacterGuid = GetAssetGuid(customCharacterPrefab),
                customClipGuid = GetAssetGuid(_customClip),

                camTarget = _camTarget,
                camYaw = _camYaw,
                camPitch = _camPitch,
                camDistance = _camDistance,
                darkBackground = _darkBackground,
                lightIntensity = _lightIntensity,

                targetBone = (int)_targetBone,
                positionOffset = _positionOffset,
                rotationOffset = _rotationOffset,
                scaleOffset = _scaleOffset,
                uniformScale = _uniformScale,

                selectedClipIndex = _selectedClipIndex,
                loopAnimation = _loopAnimation,
                playbackSpeed = _playbackSpeed,
                currentTime = _currentTime,
                normalizedTime = _normalizedTime,

                sidebarScroll = _sidebarScroll
            };

            string json = JsonUtility.ToJson(state);
            EditorPrefs.SetString(PrefKey, json);
        }

        public bool LoadWindowState()
        {
            string json = EditorPrefs.GetString(PrefKey, null);
            if (string.IsNullOrEmpty(json)) return false;

            WindowSavedState state;
            try
            {
                state = JsonUtility.FromJson<WindowSavedState>(json);
            }
            catch
            {
                return false;
            }

            if (state == null) return false;

            if (selectedWeapon == null && !string.IsNullOrEmpty(state.weaponGuid))
            {
                selectedWeapon = LoadAssetByGuid<WeaponDefinition>(state.weaponGuid);
            }

            if (attachmentProfile == null && !string.IsNullOrEmpty(state.profileGuid))
            {
                attachmentProfile = LoadAssetByGuid<ItemAttachmentProfile>(state.profileGuid);
            }

            if (customCharacterPrefab == null && !string.IsNullOrEmpty(state.customCharacterGuid))
            {
                customCharacterPrefab = LoadAssetByGuid<GameObject>(state.customCharacterGuid);
            }

            if (_customClip == null && !string.IsNullOrEmpty(state.customClipGuid))
            {
                _customClip = LoadAssetByGuid<AnimationClip>(state.customClipGuid);
            }

            _camTarget = state.camTarget;
            _camYaw = state.camYaw;
            _camPitch = state.camPitch;
            _camDistance = state.camDistance > 0.05f ? state.camDistance : 2.2f;
            _darkBackground = state.darkBackground;
            _lightIntensity = state.lightIntensity > 0.01f ? state.lightIntensity : 1.4f;

            _targetBone = (HumanBodyBones)state.targetBone;
            _positionOffset = state.positionOffset;
            _rotationOffset = state.rotationOffset;
            _scaleOffset = state.scaleOffset != Vector3.zero ? state.scaleOffset : Vector3.one;
            _uniformScale = state.uniformScale;

            if (attachmentProfile == null && selectedWeapon != null && selectedWeapon.Prefab != null)
            {
                if (_scaleOffset == Vector3.one && selectedWeapon.Prefab.transform.localScale != Vector3.one && selectedWeapon.Prefab.transform.localScale != Vector3.zero)
                {
                    _scaleOffset = selectedWeapon.Prefab.transform.localScale;
                }
            }

            _selectedClipIndex = state.selectedClipIndex;
            _loopAnimation = state.loopAnimation;
            _playbackSpeed = state.playbackSpeed > 0.01f ? state.playbackSpeed : 1.0f;
            _currentTime = state.currentTime;
            _normalizedTime = state.normalizedTime;

            _sidebarScroll = state.sidebarScroll;

            return true;
        }

        private static void SetLayerRecursively(GameObject obj, int layer)
        {
            if (obj == null) return;
            obj.layer = layer;
            foreach (Transform child in obj.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        private static string GetAssetGuid(UnityEngine.Object obj)
        {
            if (obj == null) return string.Empty;
            string path = AssetDatabase.GetAssetPath(obj);
            return string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
        }

        private static T LoadAssetByGuid<T>(string guid) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(guid)) return null;
            string path = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<T>(path);
        }

        private void InitPreview()
        {
            CleanupPreview();

            _preview = new PreviewRenderUtility();
            _preview.camera.fieldOfView = 35f;
            _preview.camera.nearClipPlane = 0.05f;
            _preview.camera.farClipPlane = 50f;
            _preview.camera.clearFlags = CameraClearFlags.SolidColor;
            _preview.camera.cullingMask = ~0;

            // Carrega o modelo oficial do jogador (Player 1.prefab) ou fallback
            GameObject charPrefab = customCharacterPrefab;
            if (charPrefab == null)
            {
                charPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultCharacterPath);
                if (charPrefab == null)
                {
                    charPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FallbackCharacterPath);
                }
            }

            if (charPrefab != null)
            {
                _characterInstance = Instantiate(charPrefab);
                _characterInstance.hideFlags = HideFlags.HideAndDontSave;

                // Desativa scripts de gameplay/rede para não interferirem no preview
                foreach (var mb in _characterInstance.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    mb.enabled = false;
                }
                foreach (var col in _characterInstance.GetComponentsInChildren<Collider>(true))
                {
                    col.enabled = false;
                }
                foreach (var canvas in _characterInstance.GetComponentsInChildren<Canvas>(true))
                {
                    canvas.enabled = false;
                }

                _animator = _characterInstance.GetComponentInChildren<Animator>();
                _preview.AddSingleGO(_characterInstance);
            }

            RebuildPlayableGraph();
            SpawnWeaponModel();
        }

        private void CleanupPreview()
        {
            if (_weaponInstance != null)
            {
                DestroyImmediate(_weaponInstance);
                _weaponInstance = null;
            }

            if (_characterInstance != null)
            {
                DestroyImmediate(_characterInstance);
                _characterInstance = null;
                _animator = null;
            }

            if (_preview != null)
            {
                _preview.Cleanup();
                _preview = null;
            }
        }

        private void RebuildPlayableGraph()
        {
            DestroyPlayableGraph();

            if (_animator == null) return;

            _playableGraph = PlayableGraph.Create("ItemFittingStudio_Graph");
            _playableGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            _playableOutput = AnimationPlayableOutput.Create(_playableGraph, "AnimationOutput", _animator);

            UpdateCurrentClipPlayable();
        }

        private void DestroyPlayableGraph()
        {
            if (_playableGraph.IsValid())
            {
                _playableGraph.Destroy();
            }
        }

        private void UpdateCurrentClipPlayable()
        {
            if (!_playableGraph.IsValid()) return;

            AnimationClip clipToPlay = GetCurrentSelectedClip();

            if (_clipPlayable.IsValid())
            {
                _playableOutput.SetSourcePlayable(Playable.Null);
                _clipPlayable.Destroy();
            }

            if (clipToPlay != null)
            {
                _clipPlayable = AnimationClipPlayable.Create(_playableGraph, clipToPlay);
                _playableOutput.SetSourcePlayable(_clipPlayable);
                _currentTime = Mathf.Clamp(_currentTime, 0f, clipToPlay.length);
                _normalizedTime = clipToPlay.length > 0f ? Mathf.Clamp01(_currentTime / clipToPlay.length) : 0f;
                _clipPlayable.SetTime(_currentTime);
                _playableGraph.Evaluate(0);
            }
        }

        private AnimationClip GetCurrentSelectedClip()
        {
            if (_selectedClipIndex >= 0 && _selectedClipIndex < _availableClips.Count)
            {
                return _availableClips[_selectedClipIndex].Clip;
            }
            return _customClip;
        }

        private void RefreshWeaponAndClips(bool resetOffsetsFromProfile = true)
        {
            _availableClips.Clear();

            if (selectedWeapon != null)
            {
                if (attachmentProfile == null && selectedWeapon.AttachmentProfile != null)
                {
                    attachmentProfile = selectedWeapon.AttachmentProfile;
                }

                if (resetOffsetsFromProfile)
                {
                    if (attachmentProfile != null)
                    {
                        _targetBone = attachmentProfile.Bone;
                        _positionOffset = attachmentProfile.PositionOffset;
                        _rotationOffset = attachmentProfile.RotationOffset;
                        _scaleOffset = attachmentProfile.Scale;
                    }
                    else
                    {
                        _targetBone = HumanBodyBones.RightHand;
                        _positionOffset = Vector3.zero;
                        _rotationOffset = Vector3.zero;

                        Vector3 defaultScale = Vector3.one;
                        if (selectedWeapon.Prefab != null && selectedWeapon.Prefab.transform.localScale != Vector3.zero)
                        {
                            defaultScale = selectedWeapon.Prefab.transform.localScale;
                        }
                        _scaleOffset = defaultScale;
                    }
                }

                // Coleta animações das ações da arma (combos / variações)
                if (selectedWeapon.Actions != null)
                {
                    for (int a = 0; a < selectedWeapon.Actions.Length; a++)
                    {
                        var action = selectedWeapon.Actions[a];
                        if (action == null) continue;
                        action.EnsureMigrated();

                        if (action.Entries != null)
                        {
                            for (int e = 0; e < action.Entries.Length; e++)
                            {
                                var entry = action.Entries[e];
                                if (entry?.Clip != null)
                                {
                                    string actionType = a == 0 ? "Ataque LMB" : (a == 1 ? "Ataque RMB" : $"Ação {a}");
                                    string comboLabel = action.ComboChain ? $"Combo #{e + 1}" : $"Variante #{e + 1}";
                                    _availableClips.Add(new ClipOption
                                    {
                                        Label = $"{actionType} - {comboLabel} ({entry.Clip.name})",
                                        Clip = entry.Clip,
                                        Events = entry.Events
                                    });
                                }
                            }
                        }
                    }
                }

                // Coleta animações das habilidades (Skills Q, E, R)
                if (selectedWeapon.Skills != null)
                {
                    for (int s = 0; s < selectedWeapon.Skills.Length; s++)
                    {
                        var skill = selectedWeapon.Skills[s];
                        if (skill == null || skill.animation == null) continue;
                        skill.animation.EnsureMigrated();

                        if (skill.animation.Entries != null)
                        {
                            for (int e = 0; e < skill.animation.Entries.Length; e++)
                            {
                                var entry = skill.animation.Entries[e];
                                if (entry?.Clip != null)
                                {
                                    string skillName = string.IsNullOrEmpty(skill.name) ? $"Skill {s + 1}" : skill.name;
                                    _availableClips.Add(new ClipOption
                                    {
                                        Label = $"Habilidade [{skillName}] ({entry.Clip.name})",
                                        Clip = entry.Clip,
                                        Events = entry.Events
                                    });
                                }
                            }
                        }
                    }
                }
            }

            if (_selectedClipIndex > _availableClips.Count)
            {
                _selectedClipIndex = 0;
            }

            if (resetOffsetsFromProfile)
            {
                _currentTime = 0f;
                _normalizedTime = 0f;
            }

            SpawnWeaponModel();
            UpdateCurrentClipPlayable();
        }

        private void SpawnWeaponModel()
        {
            if (_weaponInstance != null)
            {
                DestroyImmediate(_weaponInstance);
                _weaponInstance = null;
            }

            if (selectedWeapon?.Prefab == null || _characterInstance == null) return;

            Transform targetParent = null;
            if (_animator != null && _animator.isHuman)
            {
                targetParent = _animator.GetBoneTransform(_targetBone);
            }

            if (targetParent == null)
            {
                targetParent = _characterInstance.transform;
            }

            _weaponInstance = Instantiate(selectedWeapon.Prefab, targetParent);
            _weaponInstance.hideFlags = HideFlags.HideAndDontSave;
            _weaponInstance.SetActive(true);

            // Garante que a camada seja renderizada pela câmera do preview
            SetLayerRecursively(_weaponInstance, _characterInstance != null ? _characterInstance.layer : 0);

            // Desativa scripts que possam interferir no preview (rede, combate, som)
            foreach (var mb in _weaponInstance.GetComponentsInChildren<MonoBehaviour>(true))
            {
                mb.enabled = false;
            }
            foreach (var col in _weaponInstance.GetComponentsInChildren<Collider>(true))
            {
                col.enabled = false;
            }
            foreach (var lod in _weaponInstance.GetComponentsInChildren<LODGroup>(true))
            {
                lod.enabled = false;
            }
            var rb = _weaponInstance.GetComponent<Rigidbody>();
            if (rb != null) DestroyImmediate(rb);

            // Garante que todos os Renderers do modelo estejam ativos e habilitados
            foreach (var r in _weaponInstance.GetComponentsInChildren<Renderer>(true))
            {
                r.gameObject.SetActive(true);
                r.enabled = true;
            }

            ApplyCurrentTransformToWeapon();
        }

        private void ApplyCurrentTransformToWeapon()
        {
            if (_weaponInstance == null) return;
            _weaponInstance.transform.localPosition = _positionOffset;
            _weaponInstance.transform.localRotation = Quaternion.Euler(_rotationOffset);
            _weaponInstance.transform.localScale = _scaleOffset;
        }

        private void OnEditorUpdate()
        {
            double now = EditorApplication.timeSinceStartup;
            float dt = Mathf.Min(0.08f, (float)(now - _lastUpdateTime));
            _lastUpdateTime = now;

            if (_isPlaying)
            {
                AnimationClip currentClip = GetCurrentSelectedClip();
                if (currentClip != null && currentClip.length > 0f)
                {
                    _currentTime += dt * _playbackSpeed;
                    if (_currentTime >= currentClip.length)
                    {
                        if (_loopAnimation)
                        {
                            _currentTime %= currentClip.length;
                        }
                        else
                        {
                            _currentTime = currentClip.length;
                            _isPlaying = false;
                        }
                    }

                    _normalizedTime = Mathf.Clamp01(_currentTime / currentClip.length);
                    EvaluateAnimation();
                    Repaint();
                }
            }
        }

        private void EvaluateAnimation()
        {
            if (!_playableGraph.IsValid() || !_clipPlayable.IsValid()) return;
            _clipPlayable.SetTime(_currentTime);
            _playableGraph.Evaluate(0);
        }

        private void OnGUI()
        {
            EditorGUIUtility.labelWidth = 125f;

            float sidebarWidth = 380f;
            float previewWidth = Mathf.Max(200f, position.width - sidebarWidth);

            Rect previewRect = new Rect(0f, 0f, previewWidth, position.height);
            Rect sidebarRect = new Rect(previewWidth, 0f, sidebarWidth, position.height);

            // 1. Viewport 3D
            DrawPreviewArea(previewRect);

            // 2. Barra lateral de controles com scroll independente
            GUILayout.BeginArea(sidebarRect);
            _sidebarScroll = EditorGUILayout.BeginScrollView(_sidebarScroll);
            DrawSidebar();
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawPreviewArea(Rect rect)
        {
            HandleCameraInput(rect);

            if (_preview == null) return;

            if (Event.current.type == EventType.Repaint)
            {
                Quaternion camRot = Quaternion.Euler(_camPitch, _camYaw, 0f);
                Vector3 camPos = _camTarget + camRot * new Vector3(0f, 0f, -_camDistance);
                _preview.camera.transform.position = camPos;
                _preview.camera.transform.rotation = camRot;

                _preview.camera.backgroundColor = _darkBackground ? new Color(0.12f, 0.13f, 0.15f) : new Color(0.32f, 0.35f, 0.40f);

                // Key directional light angled with camera
                _preview.lights[0].intensity = _lightIntensity * 1.5f;
                _preview.lights[0].color = Color.white;
                _preview.lights[0].transform.rotation = camRot * Quaternion.Euler(25f, -30f, 0f);

                // Fill directional light from the other side
                _preview.lights[1].intensity = _lightIntensity * 1.1f;
                _preview.lights[1].color = new Color(0.9f, 0.95f, 1.0f);
                _preview.lights[1].transform.rotation = camRot * Quaternion.Euler(-15f, 40f, 0f);

                // Ambient light for shadow clarity
                _preview.ambientColor = (_darkBackground ? new Color(0.3f, 0.3f, 0.35f) : new Color(0.6f, 0.62f, 0.66f)) * _lightIntensity;

                _preview.BeginPreview(rect, GUIStyle.none);
                _preview.Render(true);
                Texture result = _preview.EndPreview();
                GUI.DrawTexture(rect, result, ScaleMode.StretchToFill, false);
            }

            // Toolbar superior sobre o preview
            Rect toolbarRect = new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, 24f);
            GUILayout.BeginArea(toolbarRect);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Focar Arma", EditorStyles.miniButton, GUILayout.Width(75)))
            {
                FocusOnWeapon();
            }
            if (GUILayout.Button("Focar Corpo", EditorStyles.miniButton, GUILayout.Width(75)))
            {
                _camTarget = new Vector3(0f, 1.0f, 0f);
                _camDistance = 2.2f;
            }
            if (GUILayout.Button("Reset Visão", EditorStyles.miniButton, GUILayout.Width(75)))
            {
                _camYaw = 160f;
                _camPitch = 10f;
                _camDistance = 2.2f;
                _camTarget = new Vector3(0f, 1.0f, 0f);
            }

            GUILayout.FlexibleSpace();

            GUILayout.Label("Luz:", EditorStyles.miniLabel, GUILayout.Width(28));
            _lightIntensity = GUILayout.HorizontalSlider(_lightIntensity, 0.5f, 3.0f, GUILayout.Width(70));

            _darkBackground = GUILayout.Toggle(_darkBackground, "Fundo Escuro", EditorStyles.miniButton, GUILayout.Width(85));

            EditorGUILayout.EndHorizontal();
            GUILayout.EndArea();

            // Legenda no canto inferior esquerdo
            Rect hintRect = new Rect(rect.x + 8f, rect.y + rect.height - 22f, rect.width - 16f, 18f);
            GUI.Label(hintRect, "MMB: Orbitar | Shift+MMB / Shift+Clique: Mover Visão | Ctrl+MMB / Scroll: Zoom | F / .: Focar", EditorStyles.miniLabel);
        }

        private void FocusOnWeapon()
        {
            if (_weaponInstance != null)
            {
                var renderers = _weaponInstance.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    Bounds bounds = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++)
                    {
                        bounds.Encapsulate(renderers[i].bounds);
                    }
                    _camTarget = bounds.center;
                    _camDistance = Mathf.Clamp(bounds.size.magnitude * 1.5f, 0.5f, 5.0f);
                    return;
                }
                _camTarget = _weaponInstance.transform.position;
                _camDistance = 1.0f;
            }
            else if (_animator != null && _animator.isHuman)
            {
                Transform bone = _animator.GetBoneTransform(_targetBone);
                if (bone != null)
                {
                    _camTarget = bone.position;
                    _camDistance = 1.0f;
                }
            }
        }

        private void HandleCameraInput(Rect rect)
        {
            Event e = Event.current;
            Rect toolbarRect = new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, 26f);

            int controlId = GUIUtility.GetControlID("ItemFittingCamNav".GetHashCode(), FocusType.Passive);

            if (e.type == EventType.MouseDown)
            {
                if (rect.Contains(e.mousePosition) && !toolbarRect.Contains(e.mousePosition))
                {
                    if (e.button == 2 || (e.button == 0 && (e.shift || e.alt)) || (e.button == 1 && (e.alt || e.control)))
                    {
                        GUIUtility.hotControl = controlId;
                        e.Use();
                    }
                }
            }
            else if (e.type == EventType.MouseUp)
            {
                if (GUIUtility.hotControl == controlId)
                {
                    GUIUtility.hotControl = 0;
                    e.Use();
                }
            }
            else if (e.type == EventType.MouseDrag)
            {
                if (GUIUtility.hotControl == controlId || (rect.Contains(e.mousePosition) && !toolbarRect.Contains(e.mousePosition)))
                {
                    bool isPan = (e.button == 2 && e.shift) || (e.button == 0 && e.shift) || (e.button == 1 && e.alt);
                    bool isSmoothZoom = (e.button == 2 && e.control) || (e.button == 1 && (e.control || e.alt));
                    bool isOrbit = (e.button == 2 && !e.shift && !e.control) || (e.button == 0 && e.alt && !e.shift);

                    if (isPan)
                    {
                        Quaternion camRot = Quaternion.Euler(_camPitch, _camYaw, 0f);
                        Vector3 right = camRot * Vector3.right;
                        Vector3 up = camRot * Vector3.up;
                        _camTarget += right * (-e.delta.x * 0.0025f * _camDistance) + up * (e.delta.y * 0.0025f * _camDistance);
                        e.Use();
                        Repaint();
                    }
                    else if (isSmoothZoom)
                    {
                        _camDistance = Mathf.Clamp(_camDistance * (1f + e.delta.y * 0.01f), 0.2f, 15f);
                        e.Use();
                        Repaint();
                    }
                    else if (isOrbit)
                    {
                        _camYaw += e.delta.x * 0.5f;
                        _camPitch = Mathf.Clamp(_camPitch - e.delta.y * 0.5f, -85f, 85f);
                        e.Use();
                        Repaint();
                    }
                }
            }
            else if (e.type == EventType.ScrollWheel && rect.Contains(e.mousePosition))
            {
                _camDistance = Mathf.Clamp(_camDistance * (1f + e.delta.y * 0.05f), 0.2f, 15f);
                e.Use();
                Repaint();
            }
            else if (e.type == EventType.KeyDown && rect.Contains(e.mousePosition))
            {
                if (e.keyCode == KeyCode.KeypadPeriod || e.keyCode == KeyCode.F)
                {
                    FocusOnWeapon();
                    e.Use();
                    Repaint();
                }
                else if (e.keyCode == KeyCode.Keypad1)
                {
                    _camYaw = e.control ? 0f : 180f;
                    _camPitch = 0f;
                    e.Use();
                    Repaint();
                }
                else if (e.keyCode == KeyCode.Keypad3)
                {
                    _camYaw = e.control ? 270f : 90f;
                    _camPitch = 0f;
                    e.Use();
                    Repaint();
                }
                else if (e.keyCode == KeyCode.Keypad7)
                {
                    _camYaw = 180f;
                    _camPitch = e.control ? -89f : 89f;
                    e.Use();
                    Repaint();
                }
                else if (e.keyCode == KeyCode.Keypad9)
                {
                    _camYaw = (_camYaw + 180f) % 360f;
                    e.Use();
                    Repaint();
                }
                else if (e.keyCode == KeyCode.Home)
                {
                    _camYaw = 160f;
                    _camPitch = 10f;
                    _camDistance = 2.2f;
                    _camTarget = new Vector3(0f, 1.0f, 0f);
                    e.Use();
                    Repaint();
                }
            }
        }

        private void DrawSidebar()
        {
            GUILayout.Space(8);
            DrawItemSelectionSection();
            GUILayout.Space(8);
            DrawTransformEditingSection();
            GUILayout.Space(8);
            DrawAnimationSection();
            GUILayout.Space(8);
            DrawAvatarSection();
            GUILayout.Space(12);
        }

        private void DrawItemSelectionSection()
        {
            EditorGUILayout.LabelField("1. Item & Perfil de Empunhadura", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUI.BeginChangeCheck();
            var newWeapon = (WeaponDefinition)EditorGUILayout.ObjectField("Arma / Item", selectedWeapon, typeof(WeaponDefinition), false);
            if (EditorGUI.EndChangeCheck())
            {
                selectedWeapon = newWeapon;
                attachmentProfile = selectedWeapon != null ? selectedWeapon.AttachmentProfile : null;
                RefreshWeaponAndClips(resetOffsetsFromProfile: true);
                SaveWindowState();
            }

            EditorGUI.BeginChangeCheck();
            var newProfile = (ItemAttachmentProfile)EditorGUILayout.ObjectField("Perfil de Ajuste", attachmentProfile, typeof(ItemAttachmentProfile), false);
            if (EditorGUI.EndChangeCheck())
            {
                attachmentProfile = newProfile;
                if (selectedWeapon != null && selectedWeapon.AttachmentProfile != attachmentProfile)
                {
                    Undo.RecordObject(selectedWeapon, "Vincular Perfil de Ajuste");
                    selectedWeapon.SetAttachmentProfile(attachmentProfile);
                    EditorUtility.SetDirty(selectedWeapon);
                }
                if (attachmentProfile != null)
                {
                    _targetBone = attachmentProfile.Bone;
                    _positionOffset = attachmentProfile.PositionOffset;
                    _rotationOffset = attachmentProfile.RotationOffset;
                    _scaleOffset = attachmentProfile.Scale;
                    SpawnWeaponModel();
                }
                SaveWindowState();
            }

            if (selectedWeapon != null)
            {
                if (selectedWeapon.Prefab == null)
                {
                    EditorGUILayout.HelpBox("⚠ O ScriptableObject desta arma NÃO possui um Prefab 3D atribuído no campo 'Prefab'. Por isso ela não pode ser exibida.", MessageType.Error);
                }
                else
                {
                    var renderers = selectedWeapon.Prefab.GetComponentsInChildren<Renderer>(true);
                    if (renderers == null || renderers.Length == 0)
                    {
                        EditorGUILayout.HelpBox("⚠ O Prefab vinculado não contém nenhum componente Renderer visível.", MessageType.Warning);
                    }
                }

                if (attachmentProfile != null)
                {
                    if (selectedWeapon.AttachmentProfile == attachmentProfile)
                    {
                        EditorGUILayout.HelpBox($"✓ Perfil vinculado a '{selectedWeapon.DisplayName}'", MessageType.Info);
                    }
                    else
                    {
                        string currentLinked = selectedWeapon.AttachmentProfile != null ? selectedWeapon.AttachmentProfile.name : "Nenhum";
                        EditorGUILayout.HelpBox($"⚠ Este perfil NÃO está vinculado a '{selectedWeapon.DisplayName}' (atual: {currentLinked}).\nClique para vincular e salvar.", MessageType.Warning);
                        if (GUILayout.Button("🔗 Vincular este Perfil à Arma Selecionada", GUILayout.Height(24)))
                        {
                            LinkCurrentProfileToWeapon();
                        }
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("Esta arma não possui um perfil de acoplamento atribuído.", MessageType.Warning);
                    if (GUILayout.Button("Criar Novo Perfil para esta Arma", GUILayout.Height(24)))
                    {
                        CreateProfileForSelectedWeapon();
                    }
                }

                GUILayout.Space(2);
                if (GUILayout.Button("🎮 Colocar no 1º Slot Inicial (Testar em Jogo)", EditorStyles.miniButton))
                {
                    SetAsStartingWeapon();
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void LinkCurrentProfileToWeapon()
        {
            if (selectedWeapon == null || attachmentProfile == null) return;

            Undo.RecordObject(selectedWeapon, "Vincular Perfil à Arma");
            selectedWeapon.SetAttachmentProfile(attachmentProfile);
            EditorUtility.SetDirty(selectedWeapon);
            AssetDatabase.SaveAssets();
            SaveWindowState();

            ShowNotification(new GUIContent($"✓ Perfil vinculado a '{selectedWeapon.DisplayName}'!"));
        }

        private void SetAsStartingWeapon()
        {
            if (selectedWeapon == null) return;

            var db = AssetDatabase.LoadAssetAtPath<InitialInventoryDatabase>("Assets/_Duskborn/Resources/Inventory/InitialInventoryDatabase.asset");
            if (db != null)
            {
                var so = new SerializedObject(db);
                var abProp = so.FindProperty("actionBarItems");
                if (abProp != null && abProp.arraySize > 0)
                {
                    var slot0 = abProp.GetArrayElementAtIndex(0);
                    var itemProp = slot0.FindPropertyRelative("item");
                    if (itemProp != null)
                    {
                        itemProp.objectReferenceValue = selectedWeapon;
                        so.ApplyModifiedProperties();
                        EditorUtility.SetDirty(db);
                        AssetDatabase.SaveAssets();
                        ShowNotification(new GUIContent($"✓ '{selectedWeapon.DisplayName}' no 1º slot inicial!"));
                        return;
                    }
                }
            }
            ShowNotification(new GUIContent("Não foi possível atualizar o banco inicial."));
        }

        private void CreateProfileForSelectedWeapon()
        {
            if (selectedWeapon == null) return;

            if (!Directory.Exists(ProfilesDirectory))
            {
                Directory.CreateDirectory(ProfilesDirectory);
                AssetDatabase.Refresh();
            }

            string profilePath = $"{ProfilesDirectory}/Profile_{selectedWeapon.name}.asset";
            profilePath = AssetDatabase.GenerateUniqueAssetPath(profilePath);

            var newProfile = CreateInstance<ItemAttachmentProfile>();
            newProfile.SetOffsets(_positionOffset, _rotationOffset, _scaleOffset, _targetBone);

            AssetDatabase.CreateAsset(newProfile, profilePath);
            Undo.RecordObject(selectedWeapon, "Atribuir Perfil Criado");
            selectedWeapon.SetAttachmentProfile(newProfile);
            EditorUtility.SetDirty(selectedWeapon);
            AssetDatabase.SaveAssets();

            attachmentProfile = newProfile;
            SaveWindowState();
            ShowNotification(new GUIContent($"Perfil criado: {Path.GetFileName(profilePath)}"));
        }

        private void DrawTransformEditingSection()
        {
            EditorGUILayout.LabelField("2. Ajuste do Socket e Transform", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Osso Alvo
            EditorGUI.BeginChangeCheck();
            _targetBone = (HumanBodyBones)EditorGUILayout.EnumPopup("Osso Alvo (Socket)", _targetBone);
            if (EditorGUI.EndChangeCheck())
            {
                SpawnWeaponModel();
            }

            GUILayout.Space(6);
            EditorGUI.BeginChangeCheck();

            // Posição
            _positionOffset = EditorGUILayout.Vector3Field("Posição (Local)", _positionOffset);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Passo Pos (+/-)");
            if (GUILayout.Button("X-", EditorStyles.miniButton)) _positionOffset.x -= 0.02f;
            if (GUILayout.Button("X+", EditorStyles.miniButton)) _positionOffset.x += 0.02f;
            if (GUILayout.Button("Y-", EditorStyles.miniButton)) _positionOffset.y -= 0.02f;
            if (GUILayout.Button("Y+", EditorStyles.miniButton)) _positionOffset.y += 0.02f;
            if (GUILayout.Button("Z-", EditorStyles.miniButton)) _positionOffset.z -= 0.02f;
            if (GUILayout.Button("Z+", EditorStyles.miniButton)) _positionOffset.z += 0.02f;
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(6);

            // Rotação
            _rotationOffset = EditorGUILayout.Vector3Field("Rotação Euler", _rotationOffset);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Girar (+45°)");
            if (GUILayout.Button("X+45", EditorStyles.miniButton)) _rotationOffset.x = (_rotationOffset.x + 45f) % 360f;
            if (GUILayout.Button("Y+45", EditorStyles.miniButton)) _rotationOffset.y = (_rotationOffset.y + 45f) % 360f;
            if (GUILayout.Button("Z+45", EditorStyles.miniButton)) _rotationOffset.z = (_rotationOffset.z + 45f) % 360f;
            if (GUILayout.Button("Y+180", EditorStyles.miniButton)) _rotationOffset.y = (_rotationOffset.y + 180f) % 360f;
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(6);

            // Escala
            _scaleOffset = EditorGUILayout.Vector3Field("Escala", _scaleOffset);
            EditorGUILayout.BeginHorizontal();
            _uniformScale = EditorGUILayout.ToggleLeft("Uniforme", _uniformScale, GUILayout.Width(75));
            if (_uniformScale)
            {
                float uScale = EditorGUILayout.FloatField(_scaleOffset.x);
                if (Mathf.Abs(uScale - _scaleOffset.x) > 0.001f)
                {
                    _scaleOffset = Vector3.one * Mathf.Max(0.001f, uScale);
                }
            }
            if (selectedWeapon?.Prefab != null && GUILayout.Button("Escala do Prefab", EditorStyles.miniButton, GUILayout.Width(110)))
            {
                _scaleOffset = selectedWeapon.Prefab.transform.localScale;
                ApplyCurrentTransformToWeapon();
            }
            EditorGUILayout.EndHorizontal();

            if (EditorGUI.EndChangeCheck())
            {
                ApplyCurrentTransformToWeapon();
            }

            GUILayout.Space(8);

            // Botões de ação do Transform
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Copiar", EditorStyles.miniButtonLeft))
            {
                s_ClipPos = _positionOffset;
                s_ClipRot = _rotationOffset;
                s_ClipScale = _scaleOffset;
                s_HasClipboard = true;
                ShowNotification(new GUIContent("Transform copiado!"));
            }
            GUI.enabled = s_HasClipboard;
            if (GUILayout.Button("Colar", EditorStyles.miniButtonMid))
            {
                _positionOffset = s_ClipPos;
                _rotationOffset = s_ClipRot;
                _scaleOffset = s_ClipScale;
                ApplyCurrentTransformToWeapon();
            }
            GUI.enabled = true;
            if (GUILayout.Button("Zerar Pos/Rot", EditorStyles.miniButtonRight))
            {
                _positionOffset = Vector3.zero;
                _rotationOffset = Vector3.zero;
                ApplyCurrentTransformToWeapon();
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(6);

            // Botões Salvar / Reverter
            EditorGUILayout.BeginHorizontal();
            GUI.enabled = attachmentProfile != null;
            GUI.backgroundColor = new Color(0.35f, 0.85f, 0.45f);
            if (GUILayout.Button("💾 Salvar no Perfil", GUILayout.Height(28)))
            {
                SaveOffsetsToProfile();
            }
            GUI.backgroundColor = Color.white;
            if (GUILayout.Button("Reverter", GUILayout.Height(28), GUILayout.Width(80)))
            {
                RevertOffsetsFromProfile();
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void SaveOffsetsToProfile()
        {
            if (attachmentProfile == null) return;

            Undo.RecordObject(attachmentProfile, "Salvar Ajuste do Item");
            attachmentProfile.SetOffsets(_positionOffset, _rotationOffset, _scaleOffset, _targetBone);
            EditorUtility.SetDirty(attachmentProfile);

            if (selectedWeapon != null)
            {
                if (selectedWeapon.AttachmentProfile != attachmentProfile)
                {
                    Undo.RecordObject(selectedWeapon, "Vincular Perfil à Arma");
                    selectedWeapon.SetAttachmentProfile(attachmentProfile);
                }
                EditorUtility.SetDirty(selectedWeapon);
            }

            AssetDatabase.SaveAssets();
            SaveWindowState();

            string weaponMsg = selectedWeapon != null ? $" e vinculado a '{selectedWeapon.DisplayName}'" : "";
            ShowNotification(new GUIContent($"✓ Salvo no Perfil{weaponMsg}!"));
        }

        private void RevertOffsetsFromProfile()
        {
            if (attachmentProfile == null) return;
            _targetBone = attachmentProfile.Bone;
            _positionOffset = attachmentProfile.PositionOffset;
            _rotationOffset = attachmentProfile.RotationOffset;
            _scaleOffset = attachmentProfile.Scale;
            SpawnWeaponModel();
            SaveWindowState();
            ShowNotification(new GUIContent("Valores restaurados do perfil."));
        }

        private void DrawAnimationSection()
        {
            EditorGUILayout.LabelField("3. Teste de Animações & Impacto", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Seletor de clipe
            string[] clipLabels = new string[_availableClips.Count + 1];
            for (int i = 0; i < _availableClips.Count; i++)
            {
                clipLabels[i] = _availableClips[i].Label;
            }
            clipLabels[_availableClips.Count] = "Animação Personalizada (Custom Clip)";

            EditorGUI.BeginChangeCheck();
            _selectedClipIndex = EditorGUILayout.Popup("Animação", _selectedClipIndex, clipLabels);
            if (EditorGUI.EndChangeCheck())
            {
                _currentTime = 0f;
                _normalizedTime = 0f;
                UpdateCurrentClipPlayable();
                SaveWindowState();
            }

            if (_selectedClipIndex == _availableClips.Count)
            {
                EditorGUI.BeginChangeCheck();
                _customClip = (AnimationClip)EditorGUILayout.ObjectField("Clipe Customizado", _customClip, typeof(AnimationClip), false);
                if (EditorGUI.EndChangeCheck())
                {
                    _currentTime = 0f;
                    _normalizedTime = 0f;
                    UpdateCurrentClipPlayable();
                    SaveWindowState();
                }
            }

            AnimationClip activeClip = GetCurrentSelectedClip();
            float clipLength = activeClip != null ? activeClip.length : 0f;

            GUILayout.Space(6);

            // Linha 1: Botões de Playback
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(_isPlaying ? "⏸ Pausar" : "▶ Reproduzir", GUILayout.Height(24)))
            {
                _isPlaying = !_isPlaying;
            }

            if (GUILayout.Button("⏮ Início", EditorStyles.miniButton, GUILayout.Height(24), GUILayout.Width(60)))
            {
                _currentTime = 0f;
                _normalizedTime = 0f;
                EvaluateAnimation();
            }

            _loopAnimation = GUILayout.Toggle(_loopAnimation, "Loop", EditorStyles.miniButton, GUILayout.Height(24), GUILayout.Width(50));
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(4);

            // Linha 2: Controle de Velocidade isolado
            _playbackSpeed = EditorGUILayout.Slider("Velocidade", _playbackSpeed, 0.1f, 2.0f);

            GUILayout.Space(4);

            // Linha 3: Scrubber da Timeline
            EditorGUI.BeginChangeCheck();
            _normalizedTime = EditorGUILayout.Slider("Timeline (0-1)", _normalizedTime, 0f, 1f);
            if (EditorGUI.EndChangeCheck())
            {
                _currentTime = _normalizedTime * clipLength;
                EvaluateAnimation();
                Repaint();
            }

            EditorGUILayout.LabelField($"Tempo: {_currentTime:F2}s / {clipLength:F2}s ({_normalizedTime * 100:F0}%)", EditorStyles.centeredGreyMiniLabel);

            // Linha 4: Momentos de Impacto
            if (_selectedClipIndex < _availableClips.Count)
            {
                var events = _availableClips[_selectedClipIndex].Events;
                if (events != null && events.Length > 0)
                {
                    GUILayout.Space(4);
                    EditorGUILayout.LabelField("Momentos de Impacto (Hit Events):", EditorStyles.miniBoldLabel);

                    EditorGUILayout.BeginHorizontal();
                    for (int i = 0; i < events.Length; i++)
                    {
                        var evt = events[i];
                        string btnLabel = $"Impacto #{i + 1} ({(int)(evt.NormalizedTime * 100)}%)";
                        if (GUILayout.Button(btnLabel, EditorStyles.miniButton, GUILayout.Height(20)))
                        {
                            _isPlaying = false;
                            _normalizedTime = evt.NormalizedTime;
                            _currentTime = evt.NormalizedTime * clipLength;
                            EvaluateAnimation();
                            Repaint();
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawAvatarSection()
        {
            EditorGUILayout.LabelField("4. Modelo do Personagem (Preview)", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUI.BeginChangeCheck();
            customCharacterPrefab = (GameObject)EditorGUILayout.ObjectField("Modelo Base", customCharacterPrefab, typeof(GameObject), false);
            if (EditorGUI.EndChangeCheck())
            {
                InitPreview();
                SaveWindowState();
            }

            if (customCharacterPrefab != null)
            {
                if (GUILayout.Button("Restaurar Jogador Padrão (Player 1)", EditorStyles.miniButton))
                {
                    customCharacterPrefab = null;
                    InitPreview();
                    SaveWindowState();
                }
            }

            EditorGUILayout.EndVertical();
        }
    }
}
#endif
