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
    /// Editor utility for previewing, adjusting grips / sockets, and testing animations
    /// for weapons and equipment on the player avatar.
    /// Uses the actual player prefab (Player 1.prefab) and renders in an isolated environment through PreviewRenderUtility.
    /// </summary>
    public sealed partial class ItemFittingStudioWindow : EditorWindow
    {
        private const string DefaultCharacterPath = "Assets/_Duskborn/Prefabs/Player/Player 1.prefab";
        private const string FallbackCharacterPath = "Assets/_Duskborn/Art/Models/modelTextures/char.fbx";
        private const string ProfilesDirectory = "Assets/_Duskborn/ScriptableObjects/Equipment/Profiles";

        // Main references.
        [SerializeField] private WeaponDefinition selectedWeapon;
        [SerializeField] private ItemAttachmentProfile attachmentProfile;
        [SerializeField] private GameObject customCharacterPrefab;

        // Preview state.
        private PreviewRenderUtility _preview;
        private GameObject _characterInstance;
        private Animator _animator;
        private GameObject _weaponInstance;

        // PlayableGraph for accurate Humanoid animation sampling.
        private PlayableGraph _playableGraph;
        private AnimationPlayableOutput _playableOutput;
        private AnimationClipPlayable _clipPlayable;
        private AnimatorControllerPlayable _previewBasePlayable;
        private AnimationLayerMixerPlayable _previewLayerMixer;
        private WeaponActionData _customPreviewAction;
        private UnityEngine.Object _customPreviewOwner;
        private string _customPreviewPath;
        private bool _maskPreviewRefreshRequested;
        private AvatarMask _previewAppliedMask;

        // Preview camera.
        private Vector3 _camTarget = new Vector3(0f, 1.0f, 0f);
        private float _camYaw = 160f; // Front view.
        private float _camPitch = 10f;
        private float _camDistance = 2.2f;
        private bool _darkBackground;
        private float _lightIntensity = 1.4f;

        // Current transform being edited
        private HumanBodyBones _targetBone = HumanBodyBones.RightHand;
        private Vector3 _positionOffset = Vector3.zero;
        private Vector3 _rotationOffset = Vector3.zero;
        private Vector3 _scaleOffset = Vector3.one;
        private bool _uniformScale = true;

        // Animation & Timeline.
        private struct ClipOption
        {
            public string Label;
            public AnimationClip Clip;
            public WeaponActionEvent[] Events;
            public WeaponActionData Action;
            public UnityEngine.Object Owner;
            public string ActionPath;
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

        // Temporary clipboard.
        private static Vector3 s_ClipPos;
        private static Vector3 s_ClipRot;
        private static Vector3 s_ClipScale = Vector3.one;
        private static bool s_HasClipboard;

        [MenuItem("Tools/Duskborn/Item Fitting Studio", priority = 102)]
        public static void Open()
        {
            var window = GetWindow<ItemFittingStudioWindow>("Item Fitting");
            window.minSize = new Vector2(750, 480);
            window.Show();
        }

        public static void OpenWithWeapon(WeaponDefinition weapon)
        {
            var window = GetWindow<ItemFittingStudioWindow>("Item Fitting");
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
            Undo.undoRedoPerformed += OnCombatUndoRedo;
            bool loaded = LoadWindowState();
            InitPreview();
            RefreshWeaponAndClips(resetOffsetsFromProfile: !loaded);
        }

        private void OnDisable()
        {
            SaveWindowState();
            EditorApplication.update -= OnEditorUpdate;
            Undo.undoRedoPerformed -= OnCombatUndoRedo;
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

            // Load the official player model (Player 1.prefab) or a fallback.
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

                // Disable gameplay / networking scripts to avoid interference with the preview.
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
                Duskborn.Gameplay.Player.IronrootAppearance.EnsureAnimationEventReceiver(_animator);
                // Scrubbing must never dispatch gameplay callbacks from imported clips.
                if (_animator != null) _animator.fireEvents = false;
                _preview.AddSingleGO(_characterInstance);
            }

            RebuildPlayableGraph();
            SpawnWeaponModel();
        }

        private void CleanupPreview()
        {
            CleanupProjectilePreview();
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
            _playableOutput.SetSourcePlayable(Playable.Null);
            if (_previewLayerMixer.IsValid()) _previewLayerMixer.Destroy();
            if (_previewBasePlayable.IsValid()) _previewBasePlayable.Destroy();
            if (_clipPlayable.IsValid()) _clipPlayable.Destroy();

            if (clipToPlay == null) return;
            _clipPlayable = AnimationClipPlayable.Create(_playableGraph, clipToPlay);
            _clipPlayable.SetApplyFootIK(false);
            WeaponActionData action = GetLivePreviewAction();
            bool preserveLocomotion = action == null || action.PreserveLocomotion;
            AvatarMask weaponMask = selectedWeapon != null ? selectedWeapon.ActionMask : null;
            AvatarMask mask = preserveLocomotion ? action != null ? action.ResolveMask(weaponMask) : weaponMask : null;
            if (preserveLocomotion && mask == null)
                mask = LoadCombatMaskPreset(false);

            _previewAppliedMask = null;
            if (mask != null && _animator.runtimeAnimatorController != null)
            {
                _previewBasePlayable = AnimatorControllerPlayable.Create(_playableGraph, _animator.runtimeAnimatorController);
                _previewLayerMixer = AnimationLayerMixerPlayable.Create(_playableGraph, 2);
                _previewLayerMixer.ConnectInput(0, _previewBasePlayable, 0, 1f);
                _previewLayerMixer.ConnectInput(1, _clipPlayable, 0, 1f);
                _previewLayerMixer.SetLayerAdditive(1, false);
                _previewLayerMixer.SetLayerMaskFromAvatarMask(1, mask);
                _previewAppliedMask = mask;
                _playableOutput.SetSourcePlayable(_previewLayerMixer);
            }
            else _playableOutput.SetSourcePlayable(_clipPlayable);

            _currentTime = Mathf.Clamp(_currentTime, 0f, clipToPlay.length);
            _normalizedTime = clipToPlay.length > 0f ? Mathf.Clamp01(_currentTime / clipToPlay.length) : 0f;
            _clipPlayable.SetTime(_currentTime);
            _playableGraph.Evaluate(0);
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
            bool wasCustomClip = _selectedClipIndex == _availableClips.Count;
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

                // Collect weapon action animations (combos / variations).
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
                                    string actionType = a == 0 ? "LMB Attack" : (a == 1 ? "RMB Attack" : $"Action {a}");
                                    string comboLabel = action.ComboChain ? $"Combo #{e + 1}" : $"Variante #{e + 1}";
                                    _availableClips.Add(new ClipOption
                                    {
                                        Label = $"{actionType} - {comboLabel} ({entry.Clip.name})",
                                        Clip = entry.Clip,
                                        Events = entry.Events,
                                        Action = action,
                                        Owner = selectedWeapon,
                                        ActionPath = $"actions.Array.data[{a}]"
                                    });
                                }
                            }
                        }
                    }
                }

                // Collect skill animations (Skills Q, E, R).
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
                                        Label = $"Ability [{skillName}] ({entry.Clip.name})",
                                        Clip = entry.Clip,
                                        Events = entry.Events,
                                        Action = skill.animation,
                                        Owner = skill,
                                        ActionPath = "animation"
                                    });
                                }
                            }
                        }
                    }
                }
            }

            if (!resetOffsetsFromProfile && wasCustomClip)
                _selectedClipIndex = _availableClips.Count;

            if (_selectedClipIndex < 0 || _selectedClipIndex > _availableClips.Count)
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
            RebuildProjectilePreview();
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
                targetParent = Duskborn.Gameplay.Player.IronrootAppearance.EquipmentBone(_animator, _targetBone);
            }

            if (targetParent == null)
            {
                targetParent = _characterInstance.transform;
            }

            _weaponInstance = Instantiate(selectedWeapon.Prefab, targetParent);
            _weaponInstance.hideFlags = HideFlags.HideAndDontSave;
            _weaponInstance.SetActive(true);

            // Ensure the preview camera renders the layer.
            SetLayerRecursively(_weaponInstance, _characterInstance != null ? _characterInstance.layer : 0);

            // Disable scripts that might interfere with the preview (networking, combat, sound).
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

            // Ensure all model Renderers are active and enabled.
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
            UpdateProjectilePlayback(dt);

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

            // 2. Control sidebar with independent scrolling.
            GUILayout.BeginArea(sidebarRect);
            _sidebarScroll = EditorGUILayout.BeginScrollView(_sidebarScroll);
            DrawSidebar();
            EditorGUILayout.EndScrollView();
            DrawSaveFooter();
            GUILayout.EndArea();
        }

        private void DrawPreviewArea(Rect rect)
        {
            HandleCameraInput(rect);

            if (_preview == null) return;

            if (Event.current.type == EventType.Repaint)
            {
                EnsureProjectilePreview();
                ApplyProjectilePreviewPose();
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

            // Upper toolbar above the preview
            Rect toolbarRect = new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, 24f);
            GUILayout.BeginArea(toolbarRect);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Focus Weapon", EditorStyles.miniButton, GUILayout.Width(75)))
            {
                FocusOnWeapon();
            }
            if (GUILayout.Button("Focus Body", EditorStyles.miniButton, GUILayout.Width(75)))
            {
                _camTarget = new Vector3(0f, 1.0f, 0f);
                _camDistance = 2.2f;
            }
            if (GUILayout.Button("Reset View", EditorStyles.miniButton, GUILayout.Width(75)))
            {
                _camYaw = 160f;
                _camPitch = 10f;
                _camDistance = 2.2f;
                _camTarget = new Vector3(0f, 1.0f, 0f);
            }

            GUILayout.FlexibleSpace();

            GUILayout.Label("Luz:", EditorStyles.miniLabel, GUILayout.Width(28));
            _lightIntensity = GUILayout.HorizontalSlider(_lightIntensity, 0.5f, 3.0f, GUILayout.Width(70));

            _darkBackground = GUILayout.Toggle(_darkBackground, "Dark Background", EditorStyles.miniButton, GUILayout.Width(85));

            EditorGUILayout.EndHorizontal();
            GUILayout.EndArea();

            // Lower-left corner legend.
            Rect hintRect = new Rect(rect.x + 8f, rect.y + rect.height - 22f, rect.width - 16f, 18f);
            GUI.Label(hintRect, "MMB: Orbit | Shift+MMB / Shift+Click: Pan | Ctrl+MMB / Scroll: Zoom | F / .: Focus", EditorStyles.miniLabel);
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
                Transform bone = Duskborn.Gameplay.Player.IronrootAppearance.EquipmentBone(_animator, _targetBone);
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
            DrawWeaponCombatSection();
            GUILayout.Space(8);
            DrawProjectileSection();
            GUILayout.Space(8);
            DrawAvatarSection();
            GUILayout.Space(12);
        }

        [SerializeField] private bool _showWeaponCombatEditor;
        [SerializeField] private bool _showWeaponStats;

        private void DrawWeaponCombatSection()
        {
            _showWeaponCombatEditor = EditorGUILayout.Foldout(_showWeaponCombatEditor,
                "Weapon Animations, Impact Frames & Stats", true, EditorStyles.foldoutHeader);
            if (!_showWeaponCombatEditor) return;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (selectedWeapon == null)
                {
                    EditorGUILayout.HelpBox("Select a weapon to edit its combat settings.", MessageType.Info);
                    return;
                }

                bool changed;
                using (var weapon = new SerializedObject(selectedWeapon))
                {
                    weapon.Update();
                    var actions = weapon.FindProperty("actions");
                    actions.isExpanded = EditorGUILayout.Foldout(actions.isExpanded, "Attack Animations (LMB / RMB)", true);
                    if (actions.isExpanded)
                    {
                        DrawCombatArraySize(actions, "Actions");
                        for (int i = 0; i < actions.arraySize; i++)
                        {
                            var action = actions.GetArrayElementAtIndex(i);
                            string label = i == 0 ? "LMB Attack" : i == 1 ? "RMB Attack" : $"Action {i + 1}";
                            DrawCombatAction(action, label);
                        }
                    }

                    DrawWeaponMaskSettings(weapon);
                    var bonuses = weapon.FindProperty("bonuses");
                    _showWeaponStats = EditorGUILayout.Foldout(_showWeaponStats, "Weapon Stats", true);
                    if (_showWeaponStats)
                    {
                        EditorGUILayout.PropertyField(bonuses, new GUIContent("Stat Bonuses"), true);
                        EditorGUILayout.PropertyField(weapon.FindProperty("typeModifiers"), true);
                        EditorGUILayout.PropertyField(weapon.FindProperty("behaviour"));

                    }

                    var skills = weapon.FindProperty("skills");
                    skills.isExpanded = EditorGUILayout.Foldout(skills.isExpanded, "Skills (Q / E / R)", true);
                    if (skills.isExpanded)
                    {
                        DrawCombatArraySize(skills, "Skills", 3);
                        for (int i = 0; i < skills.arraySize; i++)
                        {
                            var skill = skills.GetArrayElementAtIndex(i);
                            EditorGUILayout.PropertyField(skill, new GUIContent(i == 0 ? "Q" : i == 1 ? "E" : "R"));
                            if (skill.objectReferenceValue != null)
                                DrawSharedCombatAsset(skill.objectReferenceValue, "Skill Animation & Stats");
                        }
                    }

                    changed = weapon.ApplyModifiedProperties();
                }

                if (changed || _maskPreviewRefreshRequested)
                {
                    _maskPreviewRefreshRequested = false;
                    if (changed) EditorUtility.SetDirty(selectedWeapon);
                    RefreshWeaponAndClips(resetOffsetsFromProfile: false);
                    Repaint();
                }
            }
        }

        private static void DrawCombatArraySize(SerializedProperty array, string label, int max = 64)
        {
            int previousSize = array.arraySize;
            int size = EditorGUILayout.DelayedIntField(label, previousSize);
            if (size == previousSize) return;
            array.arraySize = Mathf.Clamp(size, 0, max);
            for (int i = previousSize; i < array.arraySize; i++)
            {
                var element = array.GetArrayElementAtIndex(i);
                if (element.propertyType == SerializedPropertyType.ObjectReference)
                {
                    element.objectReferenceValue = null;
                }
                else if (element.FindPropertyRelative("BaseSpeed") != null)
                {
                    element.FindPropertyRelative("BowAnimations").objectReferenceValue = null;
                    element.FindPropertyRelative("BaseSpeed").floatValue = 1f;
                    element.FindPropertyRelative("PreserveLocomotion").boolValue = true;
                    element.FindPropertyRelative("MaskOverride").objectReferenceValue = null;
                    element.FindPropertyRelative("ComboChain").boolValue = false;
                    element.FindPropertyRelative("ComboResetTime").floatValue = 0.8f;
                    foreach (string field in new[] { "Entries", "Clips", "Events", "ComboDamageMultipliers" })
                        element.FindPropertyRelative(field).arraySize = 0;
                }
                else if (element.FindPropertyRelative("Clip") != null)
                {
                    element.FindPropertyRelative("Clip").objectReferenceValue = null;
                    element.FindPropertyRelative("DamageMultiplier").floatValue = 1f;
                    element.FindPropertyRelative("Events").arraySize = 0;
                }
                else
                {
                    element.FindPropertyRelative("Type").enumValueIndex = 0;
                    element.FindPropertyRelative("NormalizedTime").floatValue = 0f;
                }
            }
        }

        private const string OneHandedMaskPath = "Assets/_Duskborn/Prefabs/Player/WeaponMask.mask";
        private const string TwoHandedMaskPath = "Assets/_Duskborn/Prefabs/Player/TwoHandedWeaponMask.mask";
        [SerializeField] private bool _showAnimationMasks;

        private void DrawWeaponMaskSettings(SerializedObject weapon)
        {
            _showAnimationMasks = EditorGUILayout.Foldout(_showAnimationMasks, "Animation Masks", true);
            if (!_showAnimationMasks) return;
            var mask = weapon.FindProperty("actionMask");
            EditorGUILayout.PropertyField(mask, new GUIContent("Weapon Mask"));
            EditorGUILayout.HelpBox("Weapon presets also switch the currently previewed action to use the weapon mask. 2H includes both arms and torso while preserving walking. Other actions keep their overrides; use Full Body on an action for root motion.", MessageType.None);
            DrawMaskPresetButtons(mask, null);
        }

        private void DrawMaskPresetButtons(SerializedProperty mask, SerializedProperty preserveLocomotion)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("1H", EditorStyles.miniButton))
                    ApplyMaskButton(mask, preserveLocomotion, LoadCombatMaskPreset(false), false);
                if (GUILayout.Button("2H", EditorStyles.miniButton))
                    ApplyMaskButton(mask, preserveLocomotion, LoadCombatMaskPreset(true), false);
                if (preserveLocomotion != null && GUILayout.Button("Full Body", EditorStyles.miniButton))
                    ApplyMaskButton(mask, preserveLocomotion, null, true);
                if (GUILayout.Button(preserveLocomotion != null ? "Use Weapon Mask" : "Default", EditorStyles.miniButton))
                    ApplyMaskButton(mask, preserveLocomotion, null, false);
            }
        }

        internal static AvatarMask LoadCombatMaskPreset(bool twoHanded)
        {
            string path = twoHanded ? TwoHandedMaskPath : OneHandedMaskPath;
            var preset = AssetDatabase.LoadAssetAtPath<AvatarMask>(path);
            if (preset != null) return preset;
            // New assets may not have been discovered by Unity's incremental refresh yet.
            if (File.Exists(path))
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                preset = AssetDatabase.LoadAssetAtPath<AvatarMask>(path);
                if (preset == null) throw new InvalidOperationException($"Unable to import animation mask at {path}.");
                return preset;
            }
            preset = new AvatarMask { name = twoHanded ? "TwoHandedWeaponMask" : "WeaponMask" };
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            {
                var part = (AvatarMaskBodyPart)i;
                bool active = part == AvatarMaskBodyPart.RightArm || part == AvatarMaskBodyPart.RightFingers ||
                    part == AvatarMaskBodyPart.RightHandIK || twoHanded &&
                    (part == AvatarMaskBodyPart.Body || part == AvatarMaskBodyPart.Head ||
                     part == AvatarMaskBodyPart.LeftArm || part == AvatarMaskBodyPart.LeftFingers || part == AvatarMaskBodyPart.LeftHandIK);
                preset.SetHumanoidBodyPartActive(part, active);
            }
            AssetDatabase.CreateAsset(preset, path);
            return preset;
        }

        internal static void AssignCombatMask(SerializedProperty mask, SerializedProperty preserveLocomotion,
            AvatarMask preset, bool fullBody)
        {
            mask.objectReferenceValue = preset;
            if (preserveLocomotion != null) preserveLocomotion.boolValue = !fullBody;
        }

        private void ApplyMaskButton(SerializedProperty mask, SerializedProperty preserveLocomotion,
            AvatarMask preset, bool fullBody)
        {
            AssignCombatMask(mask, preserveLocomotion, preset, fullBody);
            if (preserveLocomotion == null)
            {
                // A weapon preset should immediately affect the action being previewed,
                // even if that action previously used full-body playback or an override.
                GetPreviewActionSource(out var owner, out var path);
                if (owner == mask.serializedObject.targetObject && !string.IsNullOrEmpty(path))
                    AssignActionToWeaponMask(mask.serializedObject.FindProperty(path));
                else if (owner != null && !string.IsNullOrEmpty(path))
                {
                    using (var data = new SerializedObject(owner))
                    {
                        data.Update();
                        AssignActionToWeaponMask(data.FindProperty(path));
                        if (data.ApplyModifiedProperties()) EditorUtility.SetDirty(owner);
                    }
                }
            }
            else
            {
                string path = preserveLocomotion.propertyPath;
                path = path.Substring(0, path.LastIndexOf('.'));
                var owner = mask.serializedObject.targetObject;
                bool alreadyPreviewing = GetPreviewActionSource(out var previewOwner, out var previewPath) &&
                    previewOwner == owner && previewPath == path;
                if (!alreadyPreviewing)
                {
                    int index = _availableClips.FindIndex(option => option.Owner == owner && option.ActionPath == path);
                    if (index >= 0) _selectedClipIndex = index;
                }
            }
            _maskPreviewRefreshRequested = true;
            GUI.changed = true;
        }

        internal static void AssignActionToWeaponMask(SerializedProperty action)
        {
            if (action == null) return;
            action.FindPropertyRelative("MaskOverride").objectReferenceValue = null;
            action.FindPropertyRelative("PreserveLocomotion").boolValue = true;
        }

        private bool GetPreviewActionSource(out UnityEngine.Object owner, out string path)
        {
            if (_selectedClipIndex >= 0 && _selectedClipIndex < _availableClips.Count)
            {
                owner = _availableClips[_selectedClipIndex].Owner;
                path = _availableClips[_selectedClipIndex].ActionPath;
            }
            else
            {
                owner = _customPreviewOwner;
                path = _customPreviewPath;
            }
            return owner != null && !string.IsNullOrEmpty(path);
        }

        private WeaponActionData GetLivePreviewAction()
        {
            if (GetPreviewActionSource(out var owner, out var path))
            {
                using (var data = new SerializedObject(owner))
                {
                    data.Update();
                    var action = data.FindProperty(path);
                    if (action != null)
                        return new WeaponActionData
                        {
                            PreserveLocomotion = action.FindPropertyRelative("PreserveLocomotion").boolValue,
                            MaskOverride = action.FindPropertyRelative("MaskOverride").objectReferenceValue as AvatarMask
                        };
                }
            }
            return _selectedClipIndex >= 0 && _selectedClipIndex < _availableClips.Count
                ? _availableClips[_selectedClipIndex].Action : _customPreviewAction;
        }


        private void DrawCombatAction(SerializedProperty action, string label)
        {
            action.isExpanded = EditorGUILayout.Foldout(action.isExpanded, label, true);
            if (!action.isExpanded) return;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var bow = action.FindPropertyRelative("BowAnimations");
                EditorGUILayout.PropertyField(bow);
                if (bow.objectReferenceValue != null)
                    DrawSharedCombatAsset(bow.objectReferenceValue, "Bow Clips & Settings");
                EditorGUILayout.PropertyField(action.FindPropertyRelative("BaseSpeed"));
                var preserveLocomotion = action.FindPropertyRelative("PreserveLocomotion");
                EditorGUILayout.PropertyField(preserveLocomotion);
                var actionMask = action.FindPropertyRelative("MaskOverride");
                using (new EditorGUI.DisabledScope(!preserveLocomotion.boolValue))
                    EditorGUILayout.PropertyField(actionMask, new GUIContent("Action Mask"));
                DrawMaskPresetButtons(actionMask, preserveLocomotion);
                var combo = action.FindPropertyRelative("ComboChain");
                EditorGUILayout.PropertyField(combo);
                if (combo.boolValue)
                    EditorGUILayout.PropertyField(action.FindPropertyRelative("ComboResetTime"));

                // Clear the legacy fallback so removing every clip does not resurrect old entries.
                var entries = action.FindPropertyRelative("Entries");
                var legacyClips = action.FindPropertyRelative("Clips");
                var legacyEvents = action.FindPropertyRelative("Events");
                var legacyMultipliers = action.FindPropertyRelative("ComboDamageMultipliers");
                if (entries.arraySize == 0 && legacyClips.arraySize > 0)
                {
                    entries.arraySize = legacyClips.arraySize;
                    for (int i = 0; i < entries.arraySize; i++)
                    {
                        var entry = entries.GetArrayElementAtIndex(i);
                        entry.FindPropertyRelative("Clip").objectReferenceValue = legacyClips.GetArrayElementAtIndex(i).objectReferenceValue;
                        float multiplier = i < legacyMultipliers.arraySize ? legacyMultipliers.GetArrayElementAtIndex(i).floatValue : 1f;
                        entry.FindPropertyRelative("DamageMultiplier").floatValue = multiplier > 0f ? multiplier : 1f;
                        var events = entry.FindPropertyRelative("Events");
                        events.arraySize = legacyEvents.arraySize;
                        for (int e = 0; e < events.arraySize; e++)
                        {
                            var from = legacyEvents.GetArrayElementAtIndex(e);
                            var to = events.GetArrayElementAtIndex(e);
                            to.FindPropertyRelative("Type").enumValueIndex = from.FindPropertyRelative("Type").enumValueIndex;
                            to.FindPropertyRelative("NormalizedTime").floatValue = from.FindPropertyRelative("NormalizedTime").floatValue;
                        }
                    }
                }
                legacyClips.arraySize = 0;
                legacyEvents.arraySize = 0;
                legacyMultipliers.arraySize = 0;
                DrawCombatArraySize(entries, combo.boolValue ? "Combo Steps" : "Clip Variants");
                for (int i = 0; i < entries.arraySize; i++)
                {
                    var entry = entries.GetArrayElementAtIndex(i);
                    entry.isExpanded = EditorGUILayout.Foldout(entry.isExpanded,
                        combo.boolValue ? $"Step {i + 1}" : $"Variant {i + 1}", true);
                    if (!entry.isExpanded) continue;
                    EditorGUILayout.PropertyField(entry.FindPropertyRelative("Clip"));
                    EditorGUILayout.PropertyField(entry.FindPropertyRelative("DamageMultiplier"));
                    var clip = entry.FindPropertyRelative("Clip").objectReferenceValue as AnimationClip;
                    var events = entry.FindPropertyRelative("Events");
                    EditorGUILayout.HelpBox("Hitbox Open / Close define the impact window. Spawn Projectile sets the firing moment. Frames start at 0; timing is stored as normalized clip time.", MessageType.None);
                    DrawCombatArraySize(events, "Impact Events");
                    for (int e = 0; e < events.arraySize; e++)
                    {
                        var impact = events.GetArrayElementAtIndex(e);
                        EditorGUILayout.PropertyField(impact.FindPropertyRelative("Type"), new GUIContent($"Event {e + 1}"));
                        var time = impact.FindPropertyRelative("NormalizedTime");
                        EditorGUILayout.Slider(time, 0f, 1f, new GUIContent("Normalized Time"));
                        using (new EditorGUI.DisabledScope(clip == null || clip.length <= 0f || clip.frameRate <= 0f))
                        {
                            float frameCount = clip != null ? clip.length * clip.frameRate : 0f;
                            EditorGUI.BeginChangeCheck();
                            int frame = EditorGUILayout.DelayedIntField("Impact Frame", Mathf.RoundToInt(time.floatValue * frameCount));
                            if (EditorGUI.EndChangeCheck() && frameCount > 0f)
                                time.floatValue = Mathf.Clamp01(frame / frameCount);
                            if (GUILayout.Button("Preview Impact Pose", EditorStyles.miniButton))
                            {
                                _isPlaying = false;
                                _customPreviewOwner = action.serializedObject.targetObject;
                                _customPreviewPath = action.propertyPath;
                                _customPreviewAction = new WeaponActionData
                                {
                                    PreserveLocomotion = preserveLocomotion.boolValue,
                                    MaskOverride = actionMask.objectReferenceValue as AvatarMask
                                };
                                _customClip = clip;
                                _selectedClipIndex = _availableClips.Count;
                                _normalizedTime = time.floatValue;
                                _currentTime = _normalizedTime * clip.length;
                                UpdateCurrentClipPlayable();
                                EvaluateAnimation();
                                Repaint();
                            }
                        }
                    }
                }
            }
        }

        private void DrawSharedCombatAsset(UnityEngine.Object asset, string label)
        {
            using (var data = new SerializedObject(asset))
            {
                data.Update();
                var script = data.FindProperty("m_Script");
                script.isExpanded = EditorGUILayout.Foldout(script.isExpanded, label, true);
                if (!script.isExpanded) return;
                EditorGUILayout.HelpBox($"Editing {asset.name}. Changes also affect weapons using this shared asset. Ctrl+Z undoes edits.", MessageType.Info);
                var property = data.GetIterator();
                bool enterChildren = true;
                while (property.NextVisible(enterChildren))
                {
                    enterChildren = false;
                    if (property.name == "m_Script") continue;
                    if (property.name == "animation" && asset is WeaponSkill)
                        DrawCombatAction(property, "Skill Animation & Impact Frames");
                    else
                        EditorGUILayout.PropertyField(property, true);
                }
                if (data.ApplyModifiedProperties())
                {
                    EditorUtility.SetDirty(asset);
                    RefreshWeaponAndClips(resetOffsetsFromProfile: false);
                    Repaint();
                }
            }
        }

        private void DrawItemSelectionSection()
        {
            EditorGUILayout.LabelField("1. Item & Grip Profile", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUI.BeginChangeCheck();
            var newWeapon = (WeaponDefinition)EditorGUILayout.ObjectField("Weapon / Item", selectedWeapon, typeof(WeaponDefinition), false);
            if (EditorGUI.EndChangeCheck())
            {
                selectedWeapon = newWeapon;
                attachmentProfile = selectedWeapon != null ? selectedWeapon.AttachmentProfile : null;
                RefreshWeaponAndClips(resetOffsetsFromProfile: true);
                SaveWindowState();
            }

            EditorGUI.BeginChangeCheck();
            var newProfile = (ItemAttachmentProfile)EditorGUILayout.ObjectField("Fitting Profile", attachmentProfile, typeof(ItemAttachmentProfile), false);
            if (EditorGUI.EndChangeCheck())
            {
                attachmentProfile = newProfile;
                if (selectedWeapon != null && selectedWeapon.AttachmentProfile != attachmentProfile)
                {
                    Undo.RecordObject(selectedWeapon, "Link Fitting Profile");
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
                    EditorGUILayout.HelpBox("⚠ This weapon's ScriptableObject has NO 3D prefab assigned in the 'Prefab' field, so it cannot be displayed.", MessageType.Error);
                }
                else
                {
                    var renderers = selectedWeapon.Prefab.GetComponentsInChildren<Renderer>(true);
                    if (renderers == null || renderers.Length == 0)
                    {
                        EditorGUILayout.HelpBox("⚠ The linked prefab contains no visible Renderer component.", MessageType.Warning);
                    }
                }

                if (attachmentProfile != null)
                {
                    if (selectedWeapon.AttachmentProfile == attachmentProfile)
                    {
                        EditorGUILayout.HelpBox($"✓ Profile linked to '{selectedWeapon.DisplayName}'", MessageType.Info);
                    }
                    else
                    {
                        string currentLinked = selectedWeapon.AttachmentProfile != null ? selectedWeapon.AttachmentProfile.name : "None";
                        EditorGUILayout.HelpBox($"⚠ This profile is NOT linked to '{selectedWeapon.DisplayName}' (current: {currentLinked}).\nClick to link and save.", MessageType.Warning);
                        if (GUILayout.Button("🔗 Link This Profile to the Selected Weapon", GUILayout.Height(24)))
                        {
                            LinkCurrentProfileToWeapon();
                        }
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("This weapon has no attachment profile assigned.", MessageType.Warning);
                    if (GUILayout.Button("Create New Profile for This Weapon", GUILayout.Height(24)))
                    {
                        CreateProfileForSelectedWeapon();
                    }
                }

                GUILayout.Space(2);
                if (GUILayout.Button("🎮 Place in First Starting Slot (Test In Game)", EditorStyles.miniButton))
                {
                    SetAsStartingWeapon();
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void LinkCurrentProfileToWeapon()
        {
            if (selectedWeapon == null || attachmentProfile == null) return;

            Undo.RecordObject(selectedWeapon, "Link Profile to Weapon");
            selectedWeapon.SetAttachmentProfile(attachmentProfile);
            EditorUtility.SetDirty(selectedWeapon);
            AssetDatabase.SaveAssets();
            SaveWindowState();

            ShowNotification(new GUIContent($"✓ Profile linked to '{selectedWeapon.DisplayName}'!"));
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
                        ShowNotification(new GUIContent($"✓ '{selectedWeapon.DisplayName}' in the first starting slot!"));
                        return;
                    }
                }
            }
            ShowNotification(new GUIContent("Could not update the initial database."));
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
            Undo.RecordObject(selectedWeapon, "Assign Created Profile");
            selectedWeapon.SetAttachmentProfile(newProfile);
            EditorUtility.SetDirty(selectedWeapon);
            AssetDatabase.SaveAssets();

            attachmentProfile = newProfile;
            SaveWindowState();
            ShowNotification(new GUIContent($"Profile created: {Path.GetFileName(profilePath)}"));
        }

        private void DrawTransformEditingSection()
        {
            EditorGUILayout.LabelField("2. Socket and Transform Adjustment", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Target bone.
            EditorGUI.BeginChangeCheck();
            _targetBone = (HumanBodyBones)EditorGUILayout.EnumPopup("Target Bone (Socket)", _targetBone);
            if (EditorGUI.EndChangeCheck())
            {
                SpawnWeaponModel();
            }

            GUILayout.Space(6);
            EditorGUI.BeginChangeCheck();

            // Position.
            _positionOffset = EditorGUILayout.Vector3Field("Position (Local)", _positionOffset);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Position Step (+/-)");
            if (GUILayout.Button("X-", EditorStyles.miniButton)) _positionOffset.x -= 0.02f;
            if (GUILayout.Button("X+", EditorStyles.miniButton)) _positionOffset.x += 0.02f;
            if (GUILayout.Button("Y-", EditorStyles.miniButton)) _positionOffset.y -= 0.02f;
            if (GUILayout.Button("Y+", EditorStyles.miniButton)) _positionOffset.y += 0.02f;
            if (GUILayout.Button("Z-", EditorStyles.miniButton)) _positionOffset.z -= 0.02f;
            if (GUILayout.Button("Z+", EditorStyles.miniButton)) _positionOffset.z += 0.02f;
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(6);

            // Rotation.
            _rotationOffset = EditorGUILayout.Vector3Field("Euler Rotation", _rotationOffset);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Rotate (+45°)");
            if (GUILayout.Button("X+45", EditorStyles.miniButton)) _rotationOffset.x = (_rotationOffset.x + 45f) % 360f;
            if (GUILayout.Button("Y+45", EditorStyles.miniButton)) _rotationOffset.y = (_rotationOffset.y + 45f) % 360f;
            if (GUILayout.Button("Z+45", EditorStyles.miniButton)) _rotationOffset.z = (_rotationOffset.z + 45f) % 360f;
            if (GUILayout.Button("Y+180", EditorStyles.miniButton)) _rotationOffset.y = (_rotationOffset.y + 180f) % 360f;
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(6);

            // Scale
            _scaleOffset = EditorGUILayout.Vector3Field("Scale", _scaleOffset);
            EditorGUILayout.BeginHorizontal();
            _uniformScale = EditorGUILayout.ToggleLeft("Uniform", _uniformScale, GUILayout.Width(75));
            if (_uniformScale)
            {
                float uScale = EditorGUILayout.FloatField(_scaleOffset.x);
                if (Mathf.Abs(uScale - _scaleOffset.x) > 0.001f)
                {
                    _scaleOffset = Vector3.one * Mathf.Max(0.001f, uScale);
                }
            }
            if (selectedWeapon?.Prefab != null && GUILayout.Button("Prefab Scale", EditorStyles.miniButton, GUILayout.Width(110)))
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

            // Transform action buttons.
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Copy", EditorStyles.miniButtonLeft))
            {
                s_ClipPos = _positionOffset;
                s_ClipRot = _rotationOffset;
                s_ClipScale = _scaleOffset;
                s_HasClipboard = true;
                ShowNotification(new GUIContent("Transform copiado!"));
            }
            GUI.enabled = s_HasClipboard;
            if (GUILayout.Button("Paste", EditorStyles.miniButtonMid))
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

            using (new EditorGUI.DisabledScope(attachmentProfile == null))
            {
                if (GUILayout.Button("Revert Grip", GUILayout.Height(24)))
                    RevertOffsetsFromProfile();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawSaveFooter()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            using (new EditorGUI.DisabledScope(selectedWeapon == null && attachmentProfile == null))
            {
                Color previousColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.35f, 0.85f, 0.45f);
                bool save = GUILayout.Button("Save Changes", GUILayout.Height(32));
                GUI.backgroundColor = previousColor;
                if (save) SaveStudioChanges();
            }
        }

        private void SaveStudioChanges()
        {
            SaveOffsetsToProfile();
            SaveProjectileConfiguration();
            // Serialized combat edits mark the weapon, skill and bow assets dirty.
            // Flush them together with the grip and projectile settings on Save.
            AssetDatabase.SaveAssets();
            SaveWindowState();
            ShowNotification(new GUIContent("Saved item fitting, animations, impact frames and stats."));
        }

        private void SaveOffsetsToProfile()
        {
            if (attachmentProfile == null) return;

            Undo.RecordObject(attachmentProfile, "Save Item Fitting");
            attachmentProfile.SetOffsets(_positionOffset, _rotationOffset, _scaleOffset, _targetBone);
            EditorUtility.SetDirty(attachmentProfile);

            if (selectedWeapon != null)
            {
                if (selectedWeapon.AttachmentProfile != attachmentProfile)
                {
                    Undo.RecordObject(selectedWeapon, "Link Profile to Weapon");
                    selectedWeapon.SetAttachmentProfile(attachmentProfile);
                }
                EditorUtility.SetDirty(selectedWeapon);
            }


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
            ShowNotification(new GUIContent("Values restored from the profile."));
        }

        private void DrawAnimationSection()
        {
            EditorGUILayout.LabelField("3. Animation & Impact Testing", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Clip selector.
            string[] clipLabels = new string[_availableClips.Count + 1];
            for (int i = 0; i < _availableClips.Count; i++)
            {
                clipLabels[i] = _availableClips[i].Label;
            }
            clipLabels[_availableClips.Count] = "Custom Animation Clip";

            EditorGUI.BeginChangeCheck();
            _selectedClipIndex = EditorGUILayout.Popup("Animation", _selectedClipIndex, clipLabels);
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
                _customClip = (AnimationClip)EditorGUILayout.ObjectField("Custom Clip", _customClip, typeof(AnimationClip), false);
                if (EditorGUI.EndChangeCheck())
                {
                    _customPreviewAction = null;
                    _customPreviewOwner = null;
                    _customPreviewPath = null;
                    _currentTime = 0f;
                    _normalizedTime = 0f;
                    UpdateCurrentClipPlayable();
                    SaveWindowState();
                }
            }

            EditorGUILayout.LabelField("Preview Mask", _previewAppliedMask != null ? _previewAppliedMask.name : "Full Body");
            AnimationClip activeClip = GetCurrentSelectedClip();
            float clipLength = activeClip != null ? activeClip.length : 0f;

            GUILayout.Space(6);

            // Row 1: playback buttons.
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(_isPlaying ? "⏸ Pause" : "▶ Reproduzir", GUILayout.Height(24)))
            {
                _isPlaying = !_isPlaying;
                if (_isPlaying) UpdateCurrentClipPlayable();
            }

            if (GUILayout.Button("⏮ Start", EditorStyles.miniButton, GUILayout.Height(24), GUILayout.Width(60)))
            {
                _currentTime = 0f;
                _normalizedTime = 0f;
                EvaluateAnimation();
            }

            _loopAnimation = GUILayout.Toggle(_loopAnimation, "Loop", EditorStyles.miniButton, GUILayout.Height(24), GUILayout.Width(50));
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(4);

            // Row 2: independent speed control.
            _playbackSpeed = EditorGUILayout.Slider("Speed", _playbackSpeed, 0.1f, 2.0f);

            GUILayout.Space(4);

            // Row 3: timeline scrubber.
            EditorGUI.BeginChangeCheck();
            _normalizedTime = EditorGUILayout.Slider("Timeline (0-1)", _normalizedTime, 0f, 1f);
            if (EditorGUI.EndChangeCheck())
            {
                _currentTime = _normalizedTime * clipLength;
                EvaluateAnimation();
                Repaint();
            }

            EditorGUILayout.LabelField($"Time: {_currentTime:F2}s / {clipLength:F2}s ({_normalizedTime * 100:F0}%)", EditorStyles.centeredGreyMiniLabel);

            // Row 4: impact moments.
            if (_selectedClipIndex < _availableClips.Count)
            {
                var events = _availableClips[_selectedClipIndex].Events;
                if (events != null && events.Length > 0)
                {
                    GUILayout.Space(4);
                    EditorGUILayout.LabelField("Impact Moments (Hit Events):", EditorStyles.miniBoldLabel);

                    EditorGUILayout.BeginHorizontal();
                    for (int i = 0; i < events.Length; i++)
                    {
                        var evt = events[i];
                        string btnLabel = $"Impact #{i + 1} ({(int)(evt.NormalizedTime * 100)}%)";
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
            EditorGUILayout.LabelField("4. Character Model (Preview)", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUI.BeginChangeCheck();
            customCharacterPrefab = (GameObject)EditorGUILayout.ObjectField("Base Model", customCharacterPrefab, typeof(GameObject), false);
            if (EditorGUI.EndChangeCheck())
            {
                InitPreview();
                SaveWindowState();
            }

            if (customCharacterPrefab != null)
            {
                if (GUILayout.Button("Restore Default Player (Player 1)", EditorStyles.miniButton))
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
