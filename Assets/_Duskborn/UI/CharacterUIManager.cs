using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using Duskborn.Audio;
using Duskborn.Core;
using Duskborn.Gameplay;
using Duskborn.Gameplay.ActionBar;
using Duskborn.Gameplay.Equipment;
using Duskborn.Gameplay.Loot;
using Duskborn.Gameplay.Player;
using InventorySystem.Bootstrap;
using InventorySystem.Core;

namespace Duskborn.UI
{
    /// <summary>
    /// Gerenciador visual do Painel de Personagem (Character Section / Paperdoll) estilo WoW.
    /// Exibe visualmente o modelo 3D do personagem com suporte a rotação por clique-e-arraste,
    /// os 12 slots de equipamentos e arma ativa, atributos atualizados em tempo real,
    /// e suporte completo a desequipar itens via clique direito e equipar diretamente do inventário.
    /// </summary>
    public class CharacterUIManager : MonoBehaviour
    {
        public static CharacterUIManager Instance { get; private set; }

        [Header("Áudio")]
        [SerializeField] private AudioClip openSound;
        [SerializeField] private AudioClip closeSound;
        [SerializeField] private AudioClip clickSound;
        [SerializeField] private AudioClip equipSound;
        [SerializeField] private AudioClip unequipSound;
        [SerializeField] private AudioClip errorSound;

        [Header("Sprites")]
        [SerializeField] private Sprite panelFrameSprite;
        [SerializeField] private Sprite slotFrameSprite;
        [SerializeField] private Sprite charSilhouetteSprite;

        // Estado do Painel
        public bool IsOpen { get; private set; }
        public int LastClosedFrame { get; private set; } = -1;

        // Referências Visuais Principais
        private Canvas _canvas;
        private RectTransform _characterRoot;
        public RectTransform CharacterRoot => _characterRoot;

        private TextMeshProUGUI _titleText;
        private TextMeshProUGUI _classSubtitleText;
        private GameObject _statusBannerGO;
        private TextMeshProUGUI _statusMessageText;
        private Coroutine _statusHideCoroutine;

        // Visualização 3D do Personagem
        private RawImage _previewRawImage;
        private Image _previewFallbackImage;
        private RenderTexture _previewRenderTexture;
        private GameObject _previewRig;
        private Camera _previewCamera;
        private GameObject _previewModel;
        private Animator _previewAnimator;

        private static readonly int HashVelocityX = Animator.StringToHash("VelocityX");
        private static readonly int HashVelocityY = Animator.StringToHash("VelocityY");
        private static readonly int HashIsGrounded = Animator.StringToHash("IsGrounded");
        private static readonly int HashDead = Animator.StringToHash("Dead");

        // Slots de Equipamentos
        private readonly Dictionary<EquipmentSlot, CharacterSlotView> _slotViews = new();
        private CharacterSlotView _weaponSlotView;

        // Painel de Atributos (Stats)
        private TextMeshProUGUI _hpStatText;
        private TextMeshProUGUI _damageStatText;
        private TextMeshProUGUI _attackSpeedStatText;
        private TextMeshProUGUI _moveSpeedStatText;
        private TextMeshProUGUI _critStatText;
        private TextMeshProUGUI _defenseStatText;
        private TextMeshProUGUI _miningStatText;
        private TextMeshProUGUI _woodcuttingStatText;

        // Tooltip Flutuante de Equipamento
        private RectTransform _tooltipPanel;
        private TextMeshProUGUI _tooltipTitle;
        private TextMeshProUGUI _tooltipSlot;
        private TextMeshProUGUI _tooltipStats;
        private TextMeshProUGUI _tooltipDescription;
        private TextMeshProUGUI _tooltipHint;

        // Integração com Jogador e Inventário
        private PlayerCombat _playerCombat;
        private PlayerEquipmentContainer _equipment;
        private PlayerStats _playerStats;
        private PlayerBuffContainer _buffs;
        private PlayerWeaponHandler _weaponHandler;
        private InventoryUIManager _inventoryUI;
        private Vector2 _originalInventoryPos = new Vector2(0f, 25f);
        private bool _hasOriginalInventoryPos;

        // Dicionário de ícones de silhuetas para slots vazios
        private readonly Dictionary<EquipmentSlot, Sprite> _slotSilhouetteMap = new();
        private Sprite _weaponSilhouetteSprite;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            EnsureInstance();
        }

        public static CharacterUIManager EnsureInstance()
        {
            if (Instance != null) return Instance;

            var existing = FindAnyObjectByType<CharacterUIManager>();
            if (existing != null)
            {
                Instance = existing;
                return Instance;
            }

            var go = new GameObject("CharacterUIManager");
            Instance = go.AddComponent<CharacterUIManager>();
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            LoadAudio();
            LoadSprites();
        }

        private void Start()
        {
            TryFindIntegrations();
            EnsureUIHierarchy();
        }

        private void Update()
        {
            TryFindIntegrations();

            if (IsCharacterPanelKeyPressed())
            {
                Toggle();
            }
            else if (IsOpen && IsEscapeKeyPressed())
            {
                Close();
            }

            if (IsOpen)
            {
                if (_previewAnimator != null)
                {
                    _previewAnimator.SetFloat(HashVelocityX, 0f);
                    _previewAnimator.SetFloat(HashVelocityY, 0f);
                    _previewAnimator.SetBool(HashIsGrounded, true);
                    _previewAnimator.SetBool(HashDead, false);
                }

                RefreshStats();
                UpdateTooltipPosition();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;

            if (IsOpen)
            {
                bool otherMenuOpen = (_inventoryUI != null && _inventoryUI.IsOpen) ||
                                     (CraftingUIManager.Instance != null && CraftingUIManager.Instance.IsOpen) ||
                                     (InGameMenuController.Instance != null && InGameMenuController.Instance.IsOpen);
                if (!otherMenuOpen && PlayerCameraController.LocalInstance != null)
                {
                    PlayerCameraController.LocalInstance.SetRotationLocked(false);
                    PlayerCameraController.LocalInstance.SetCursorLocked(true);
                }
            }

            if (_equipment != null)
                _equipment.OnEquipmentChanged -= HandleEquipmentChanged;
            if (_buffs != null)
                _buffs.OnStatsApplied -= HandleStatsApplied;
            if (_playerStats != null)
                _playerStats.OnHealthChanged -= HandleHealthChanged;

            CleanupPreviewRig();
        }

        // ── Integrações e Cache do Jogador ─────────────────────────────────────

        private void TryFindIntegrations()
        {
            if (_inventoryUI == null)
                _inventoryUI = FindAnyObjectByType<InventoryUIManager>();

            if (_canvas == null)
            {
                if (_inventoryUI != null && _inventoryUI.Installer != null)
                    _canvas = _inventoryUI.Installer.GetComponentInParent<Canvas>();

                if (_canvas == null)
                {
                    foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                    {
                        if (c.renderMode != RenderMode.WorldSpace)
                        {
                            _canvas = c;
                            break;
                        }
                    }
                    if (_canvas == null) _canvas = FindAnyObjectByType<Canvas>();
                }
            }

            if (_equipment == null || _playerStats == null)
            {
                PlayerCombat fallbackCombat = null;
                foreach (var combat in FindObjectsByType<PlayerCombat>(FindObjectsSortMode.None))
                {
                    if (combat.IsOwner)
                    {
                        _playerCombat = combat;
                        break;
                    }
                    if (fallbackCombat == null) fallbackCombat = combat;
                }

                if (_playerCombat == null && fallbackCombat != null)
                    _playerCombat = fallbackCombat;

                if (_playerCombat != null)
                {
                    _equipment = _playerCombat.GetComponent<PlayerEquipmentContainer>();
                    _playerStats = _playerCombat.GetComponent<PlayerStats>();
                    _buffs = _playerCombat.GetComponent<PlayerBuffContainer>();
                    _weaponHandler = _playerCombat.GetComponent<PlayerWeaponHandler>();
                }
                else
                {
                    if (_equipment == null)
                        _equipment = FindAnyObjectByType<PlayerEquipmentContainer>();
                    if (_playerStats == null)
                        _playerStats = FindAnyObjectByType<PlayerStats>();
                    if (_buffs == null)
                        _buffs = FindAnyObjectByType<PlayerBuffContainer>();
                    if (_weaponHandler == null)
                        _weaponHandler = FindAnyObjectByType<PlayerWeaponHandler>();
                }

                if (_equipment != null)
                {
                    _equipment.OnEquipmentChanged -= HandleEquipmentChanged;
                    _equipment.OnEquipmentChanged += HandleEquipmentChanged;
                }

                if (_buffs != null)
                {
                    _buffs.OnStatsApplied -= HandleStatsApplied;
                    _buffs.OnStatsApplied += HandleStatsApplied;
                }

                if (_playerStats != null)
                {
                    _playerStats.OnHealthChanged -= HandleHealthChanged;
                    _playerStats.OnHealthChanged += HandleHealthChanged;
                }

                if (IsOpen && _previewModel == null)
                    SetupPreviewRig();
            }
        }

        private void HandleEquipmentChanged(EquipmentSlot slot, GearItem prev, GearItem next)
        {
            RefreshSlots();
            RefreshStats();
        }

        private void HandleStatsApplied(float prevMaxHP)
        {
            RefreshStats();
        }

        private void HandleHealthChanged(float cur, float max)
        {
            RefreshStats();
        }

        // ── Abertura e Fechamento ─────────────────────────────────────────────

        public void Toggle()
        {
            if (IsOpen)
                Close();
            else
                Open();
        }

        public void Open()
        {
            TryFindIntegrations();
            EnsureUIHierarchy();

            if (_characterRoot != null)
                _characterRoot.gameObject.SetActive(true);

            IsOpen = true;
            PlaySound(openSound);

            AdjustDualPanelPositions(true);

            SetupPreviewRig();
            Refresh();

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            PlayerCameraController.LocalInstance?.SetRotationLocked(true);
        }

        public void Close()
        {
            if (!IsOpen) return;

            IsOpen = false;
            LastClosedFrame = Time.frameCount;

            if (_characterRoot != null)
                _characterRoot.gameObject.SetActive(false);

            HideTooltip();
            HideStatusBannerInstant();

            AdjustDualPanelPositions(false);

            bool otherMenuOpen = (_inventoryUI != null && _inventoryUI.IsOpen) ||
                                 (CraftingUIManager.Instance != null && CraftingUIManager.Instance.IsOpen) ||
                                 (InGameMenuController.Instance != null && InGameMenuController.Instance.IsOpen);

            if (!otherMenuOpen)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                if (PlayerCameraController.LocalInstance != null)
                {
                    PlayerCameraController.LocalInstance.SetRotationLocked(false);
                    PlayerCameraController.LocalInstance.SetCursorLocked(true);
                }
            }

            PlaySound(closeSound);
        }

        private void AdjustDualPanelPositions(bool opening)
        {
            if (_inventoryUI == null) return;

            var invRect = _inventoryUI.InventoryFrameRect;
            if (invRect != null)
            {
                if (!_hasOriginalInventoryPos)
                {
                    _originalInventoryPos = invRect.anchoredPosition;
                    _hasOriginalInventoryPos = true;
                }

                if (opening)
                {
                    if (_inventoryUI.IsOpen)
                    {
                        // Posiciona ambos lado a lado estilo WoW (-174 / +174)
                        invRect.anchoredPosition = new Vector2(174f, _originalInventoryPos.y);
                        if (_characterRoot != null)
                            _characterRoot.anchoredPosition = new Vector2(-174f, _originalInventoryPos.y);
                    }
                    else
                    {
                        // Centralizado quando aberto sozinho
                        if (_characterRoot != null)
                            _characterRoot.anchoredPosition = new Vector2(0f, _originalInventoryPos.y);
                    }
                }
                else
                {
                    // Retorna o inventário ao centro se permanecer aberto
                    if (_inventoryUI.IsOpen)
                    {
                        invRect.anchoredPosition = _originalInventoryPos;
                    }
                }
            }
            else if (_characterRoot != null)
            {
                _characterRoot.anchoredPosition = new Vector2(0f, 0f);
            }
        }

        // ── Atualização de Dados e Slots ───────────────────────────────────────

        public void Refresh()
        {
            RefreshSlots();
            RefreshStats();
            RefreshCharacterInfo();
        }

        public void RefreshSlots()
        {
            if (_equipment == null) return;

            var slots = (EquipmentSlot[])Enum.GetValues(typeof(EquipmentSlot));
            foreach (var slot in slots)
            {
                if (!_slotViews.TryGetValue(slot, out var view)) continue;

                var gear = _equipment.GetEquipped(slot);
                if (gear != null)
                {
                    Sprite spr = ResolveItemSprite(gear.Id, gear.IconId);
                    view.SetEquipped(gear, spr);
                }
                else
                {
                    _slotSilhouetteMap.TryGetValue(slot, out var sil);
                    view.SetEmpty(sil);
                }
            }

            // Atualiza slot de arma ativa
            if (_weaponSlotView != null)
            {
                var weapon = _weaponHandler != null ? _weaponHandler.ActiveWeapon : null;
                if (weapon != null)
                {
                    Sprite spr = ResolveItemSprite(weapon.Id, weapon.IconId);
                    _weaponSlotView.SetEquippedWeapon(weapon, spr);
                }
                else
                {
                    _weaponSlotView.SetEmpty(_weaponSilhouetteSprite);
                }
            }
        }

        private Sprite ResolveItemSprite(string id, string iconId)
        {
            Texture2D tex = null;
            if (_inventoryUI != null && _inventoryUI.Installer != null)
                tex = _inventoryUI.Installer.GetIcon(id);

            if (tex == null && !string.IsNullOrWhiteSpace(iconId))
                tex = Resources.Load<Texture2D>(iconId);

            if (tex == null && !string.IsNullOrWhiteSpace(iconId))
                tex = Resources.Load<Texture2D>($"Textures/Gear/{iconId}");

            if (tex != null)
                return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));

            if (!string.IsNullOrWhiteSpace(iconId))
            {
                var spr = Resources.Load<Sprite>($"Textures/Gear/{iconId}");
                if (spr != null) return spr;
            }

            return null;
        }

        public void RefreshStats()
        {
            if (_playerStats == null) return;

            if (_hpStatText != null)
                _hpStatText.text = $"<color=#4ade80>{_playerStats.CurrentHP:F0}</color> / {_playerStats.MaxHP:F0}";

            if (_damageStatText != null)
                _damageStatText.text = $"<color=#f87171>{_playerStats.Damage:F1}</color>";

            if (_attackSpeedStatText != null)
                _attackSpeedStatText.text = $"<color=#fde047>{_playerStats.AttackSpeed:F2}x</color>";

            if (_moveSpeedStatText != null)
                _moveSpeedStatText.text = $"<color=#60a5fa>{_playerStats.MoveSpeed:F1}</color>";

            if (_critStatText != null)
                _critStatText.text = $"<color=#e879f9>{_playerStats.CritChance * 100f:F0}%</color>";

            if (_defenseStatText != null)
            {
                float reduction = (1f - _playerStats.EffectiveIncomingDamage) * 100f;
                _defenseStatText.text = $"<color=#a78bfa>{reduction:F0}%</color>";
            }

            if (_miningStatText != null)
                _miningStatText.text = $"<color=#fb923c>+{_playerStats.EffectiveMiningResourceBonus * 100f:F0}%</color>";

            if (_woodcuttingStatText != null)
                _woodcuttingStatText.text = $"<color=#34d399>+{_playerStats.EffectiveWoodcuttingResourceBonus * 100f:F0}%</color>";
        }

        private void RefreshCharacterInfo()
        {
            string className = _playerStats != null ? _playerStats.ClassName : "Guerreiro";
            if (_titleText != null)
                _titleText.text = "PERSONAGEM";

            if (_classSubtitleText != null)
                _classSubtitleText.text = $"{className} • Nv. 1";
        }

        // ── Desequipar Slot (Clique Direito) ───────────────────────────────────

        public void UnequipSlot(EquipmentSlot slot)
        {
            if (_equipment == null) return;

            var equipped = _equipment.GetEquipped(slot);
            if (equipped == null) return;

            if (_inventoryUI != null && _inventoryUI.Installer != null && _inventoryUI.Installer.Inventory != null)
            {
                var inv = _inventoryUI.Installer.Inventory;
                if (!inv.TryAddItem(equipped, out int placedSlot))
                {
                    ShowStatusFeedback("Inventário cheio! Não é possível desequipar.", true);
                    PlaySound(errorSound);
                    return;
                }
            }

            _equipment.Unequip(slot);
            PlaySound(unequipSound);
            HideTooltip();
            Refresh();
            ShowStatusFeedback($"<b>{equipped.DisplayName}</b> desequipado.", false);
        }

        public void ShowStatusFeedback(string msg, bool isError)
        {
            if (_statusBannerGO == null || _statusMessageText == null) return;

            string col = isError ? "#f87171" : "#4ade80";
            _statusMessageText.text = $"<color={col}>{msg}</color>";
            _statusBannerGO.SetActive(true);

            if (_statusHideCoroutine != null)
                StopCoroutine(_statusHideCoroutine);

            _statusHideCoroutine = StartCoroutine(HideStatusBannerRoutine(3.2f));
        }

        private IEnumerator HideStatusBannerRoutine(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (_statusBannerGO != null)
                _statusBannerGO.SetActive(false);
            _statusHideCoroutine = null;
        }

        private void HideStatusBannerInstant()
        {
            if (_statusHideCoroutine != null)
            {
                StopCoroutine(_statusHideCoroutine);
                _statusHideCoroutine = null;
            }
            if (_statusBannerGO != null)
                _statusBannerGO.SetActive(false);
        }

        // ── Tooltip do Equipamento ─────────────────────────────────────────────

        public void ShowTooltip(IInventoryItem item, EquipmentSlot? slot, Vector2 screenPos)
        {
            if (item == null || _tooltipPanel == null) return;

            if (_tooltipTitle != null)
                _tooltipTitle.text = item.DisplayName;

            if (_tooltipSlot != null)
                _tooltipSlot.text = slot.HasValue ? FormatSlotName(slot.Value) : "Arma";

            if (_tooltipStats != null)
            {
                if (item is GearItem gear && gear.Bonuses != null && gear.Bonuses.Count > 0)
                {
                    var lines = new List<string>();
                    foreach (var b in gear.Bonuses)
                    {
                        string sign = b.Value >= 0 ? "+" : "";
                        lines.Add($"<color=#86efac>{sign}{b.Value * 100:F0}% {FormatStatType(b.Type)}</color>");
                    }
                    _tooltipStats.text = string.Join("\n", lines);
                }
                else if (item is WeaponItem weapon && weapon.Bonuses != null)
                {
                    var lines = new List<string>();
                    foreach (var b in weapon.Bonuses)
                    {
                        string sign = b.Value >= 0 ? "+" : "";
                        lines.Add($"<color=#86efac>{sign}{b.Value * 100:F0}% {FormatStatType(b.Type)}</color>");
                    }
                    _tooltipStats.text = string.Join("\n", lines);
                }
                else
                {
                    _tooltipStats.text = string.Empty;
                }
            }

            if (_tooltipDescription != null)
                _tooltipDescription.text = item.Description;

            if (_tooltipHint != null)
                _tooltipHint.text = "<color=#94a3b8><i>Clique direito para desequipar</i></color>";

            _tooltipPanel.gameObject.SetActive(true);
            _tooltipPanel.SetAsLastSibling();
            PositionTooltip(screenPos);
        }

        public void HideTooltip()
        {
            if (_tooltipPanel != null)
                _tooltipPanel.gameObject.SetActive(false);
        }

        private void UpdateTooltipPosition()
        {
            if (_tooltipPanel == null || !_tooltipPanel.gameObject.activeSelf) return;
            PositionTooltip(GetPointerPosition());
        }

        private void PositionTooltip(Vector2 screenPos)
        {
            if (_tooltipPanel == null || _canvas == null) return;

            var cam = (_canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? _canvas.worldCamera : null;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas.transform as RectTransform, screenPos, cam, out Vector2 localPoint))
            {
                _tooltipPanel.localPosition = localPoint + new Vector2(16f, -16f);
            }
        }

        // ── Setup do Rig de Preview 3D ─────────────────────────────────────────

        private void SetupPreviewRig()
        {
            if (_previewRig != null) return;

            _previewRig = new GameObject("[CharacterPreviewRig]");
            _previewRig.transform.position = new Vector3(0f, -1000f, 0f);
            DontDestroyOnLoad(_previewRig);

            var camGO = new GameObject("PreviewCamera", typeof(Camera));
            camGO.transform.SetParent(_previewRig.transform, false);
            camGO.transform.localPosition = new Vector3(0f, 1.2f, -3.2f);
            camGO.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);

            _previewCamera = camGO.GetComponent<Camera>();
            _previewCamera.clearFlags = CameraClearFlags.SolidColor;
            _previewCamera.backgroundColor = new Color(0.05f, 0.06f, 0.08f, 1f);
            _previewCamera.fieldOfView = 30f;
            _previewCamera.nearClipPlane = 0.05f;
            _previewCamera.farClipPlane = 100f;

            _previewRenderTexture = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32)
            {
                name = "CharPreviewTexture",
                filterMode = FilterMode.Bilinear
            };
            _previewCamera.targetTexture = _previewRenderTexture;

            // Luz Principal (Key Light)
            var lightGO = new GameObject("PreviewKeyLight", typeof(Light));
            lightGO.transform.SetParent(_previewRig.transform, false);
            lightGO.transform.localPosition = new Vector3(1.5f, 2.5f, -1.5f);
            lightGO.transform.localRotation = Quaternion.Euler(35f, -30f, 0f);
            var light = lightGO.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.4f;
            light.color = new Color(1f, 0.97f, 0.92f, 1f);

            // Luz de Borda (Rim Light)
            var rimLightGO = new GameObject("PreviewRimLight", typeof(Light));
            rimLightGO.transform.SetParent(_previewRig.transform, false);
            rimLightGO.transform.localPosition = new Vector3(-1.8f, 1.5f, 1.5f);
            rimLightGO.transform.localRotation = Quaternion.Euler(20f, 150f, 0f);
            var rimLight = rimLightGO.GetComponent<Light>();
            rimLight.type = LightType.Directional;
            rimLight.intensity = 0.9f;
            rimLight.color = new Color(0.6f, 0.75f, 1f, 1f);

            // Luz Pontual de Preenchimento (Point Fill Light)
            var pointLightGO = new GameObject("PreviewFillPointLight", typeof(Light));
            pointLightGO.transform.SetParent(_previewRig.transform, false);
            pointLightGO.transform.localPosition = new Vector3(0f, 1.2f, -1.2f);
            var pointLight = pointLightGO.GetComponent<Light>();
            pointLight.type = LightType.Point;
            pointLight.range = 8f;
            pointLight.intensity = 1.2f;
            pointLight.color = new Color(1f, 0.98f, 0.95f, 1f);

            InstantiatePreviewModel();

            if (_previewRawImage != null)
            {
                _previewRawImage.texture = _previewRenderTexture;
                _previewRawImage.color = Color.white;
                _previewRawImage.gameObject.SetActive(true);
            }
        }

        private void InstantiatePreviewModel()
        {
            if (_previewModel != null) return;
            if (_previewRig == null) return;

            GameObject sourceVisual = FindPlayerVisualSource();
            if (sourceVisual != null)
            {
                _previewModel = Instantiate(sourceVisual, _previewRig.transform);
                _previewModel.name = "PreviewCharacterModel";

                // Remove scripts de gameplay, colisores e física
                StripGameplayComponents(_previewModel);

                // Configura Animator
                _previewAnimator = _previewModel.GetComponentInChildren<Animator>();
                if (_previewAnimator == null)
                    _previewAnimator = _previewModel.AddComponent<Animator>();

                if (_previewAnimator.runtimeAnimatorController == null)
                {
                    var ctrl = Resources.Load<RuntimeAnimatorController>("Models/char_animator_controller");
#if UNITY_EDITOR
                    if (ctrl == null)
                        ctrl = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/_Duskborn/Art/Models/char_animator_controller.controller");
#endif
                    if (ctrl != null)
                        _previewAnimator.runtimeAnimatorController = ctrl;
                }

                _previewAnimator.applyRootMotion = false;
                _previewAnimator.updateMode = AnimatorUpdateMode.Normal;
                _previewAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                _previewAnimator.SetFloat(HashVelocityX, 0f);
                _previewAnimator.SetFloat(HashVelocityY, 0f);
                _previewAnimator.SetBool(HashIsGrounded, true);
                _previewAnimator.SetBool(HashDead, false);
                _previewAnimator.ResetTrigger("Roll");
                _previewAnimator.ResetTrigger("Jump");
                _previewAnimator.Play(0, 0, 0f);
                _previewAnimator.Update(0f);

                // Garante que os Renderers e Materiais estão ativos e visíveis
                var renderers = _previewModel.GetComponentsInChildren<Renderer>(true);
                foreach (var r in renderers)
                {
                    r.enabled = true;
                    r.gameObject.SetActive(true);

                    if (r.sharedMaterial == null || r.sharedMaterial.shader == null || r.sharedMaterial.shader.name == "Hidden/InternalErrorShader")
                    {
                        var mat = Resources.Load<Material>("Models/Material");
#if UNITY_EDITOR
                        if (mat == null)
                            mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/_Duskborn/Art/Models/Material.mat");
#endif
                        if (mat != null)
                            r.sharedMaterial = mat;
                    }
                }

                // Enquadramento automático com base no cálculo dos Bounds
                FramePreviewCamera();

                if (_previewFallbackImage != null)
                    _previewFallbackImage.gameObject.SetActive(false);

                if (_previewRawImage != null)
                {
                    _previewRawImage.texture = _previewRenderTexture;
                    _previewRawImage.color = Color.white;
                    _previewRawImage.gameObject.SetActive(true);
                }
            }
            else
            {
                if (_previewFallbackImage != null)
                    _previewFallbackImage.gameObject.SetActive(true);
            }
        }

        private GameObject FindPlayerVisualSource()
        {
            // 1. Tenta pegar do PlayerCombat local ativo
            if (_playerCombat != null)
            {
                var vis = GetVisualHierarchy(_playerCombat.transform);
                if (vis != null) return vis;
            }

            // 2. Tenta qualquer PlayerCombat na cena (online ou offline)
            foreach (var combat in FindObjectsByType<PlayerCombat>(FindObjectsSortMode.None))
            {
                var vis = GetVisualHierarchy(combat.transform);
                if (vis != null) return vis;
            }

            // 3. Tenta GameObject com tag Player
            var playerGO = GameObject.FindWithTag("Player");
            if (playerGO != null)
            {
                var vis = GetVisualHierarchy(playerGO.transform);
                if (vis != null) return vis;
            }

            // 4. Tenta qualquer objeto com PlayerStats ou PlayerEquipmentContainer
            var stats = FindAnyObjectByType<PlayerStats>();
            if (stats != null)
            {
                var vis = GetVisualHierarchy(stats.transform);
                if (vis != null) return vis;
            }

            // 5. Tenta qualquer SkinnedMeshRenderer na cena
            foreach (var smr in FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None))
            {
                if (smr.transform.root.name.Contains("Preview")) continue;
                if (smr.name.Contains("char") || smr.transform.root.name.Contains("Player"))
                {
                    var vis = GetVisualHierarchy(smr.transform.parent != null ? smr.transform.parent : smr.transform);
                    if (vis != null) return vis;
                }
            }

            // 6. Carrega de Resources/Models/char
            var resModel = Resources.Load<GameObject>("Models/char");
            if (resModel != null) return resModel;

            var previewPrefab = Resources.Load<GameObject>("Preview/CharacterPreview");
            if (previewPrefab != null) return previewPrefab;

#if UNITY_EDITOR
            // 7. Fallback robusto no Unity Editor
            var editorFbx = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Duskborn/Art/Models/modelTextures/char.fbx");
            if (editorFbx != null) return editorFbx;

            var editorPlayer = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Duskborn/Prefabs/Player/Player.prefab");
            if (editorPlayer != null)
            {
                var charChild = editorPlayer.transform.Find("char");
                if (charChild != null) return charChild.gameObject;
                return editorPlayer;
            }
#endif

            return null;
        }

        private GameObject GetVisualHierarchy(Transform root)
        {
            if (root == null) return null;

            var charChild = root.Find("char");
            if (charChild != null) return charChild.gameObject;

            var smr = root.GetComponentInChildren<SkinnedMeshRenderer>();
            if (smr != null)
            {
                Transform t = smr.transform;
                while (t.parent != null && t.parent != root)
                {
                    t = t.parent;
                }
                return t.gameObject;
            }

            var anim = root.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                Transform t = anim.transform;
                while (t.parent != null && t.parent != root)
                {
                    t = t.parent;
                }
                return t.gameObject;
            }

            return null;
        }

        private void StripGameplayComponents(GameObject go)
        {
            if (go == null) return;

            var monoBehaviours = go.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var mb in monoBehaviours)
            {
                Destroy(mb);
            }

            var colliders = go.GetComponentsInChildren<Collider>(true);
            foreach (var col in colliders)
            {
                Destroy(col);
            }

            var rigidbodies = go.GetComponentsInChildren<Rigidbody>(true);
            foreach (var rb in rigidbodies)
            {
                Destroy(rb);
            }

            var audioListeners = go.GetComponentsInChildren<AudioListener>(true);
            foreach (var al in audioListeners)
            {
                Destroy(al);
            }

            var audioSources = go.GetComponentsInChildren<AudioSource>(true);
            foreach (var asrc in audioSources)
            {
                Destroy(asrc);
            }
        }

        private void FramePreviewCamera()
        {
            if (_previewModel == null || _previewCamera == null || _previewRig == null) return;

            _previewModel.transform.localPosition = Vector3.zero;
            _previewModel.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            var anim = _previewModel.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                anim.Update(0f);
            }

            var renderers = _previewModel.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            if (bounds.size.magnitude < 0.001f)
            {
                bounds = new Bounds(_previewRig.transform.position + Vector3.up, new Vector3(1f, 2f, 1f));
            }

            // Alinha o modelo no centro horizontal e assenta os pés no chão da rig
            Vector3 curPos = _previewModel.transform.position;
            float rigX = _previewRig.transform.position.x;
            float rigY = _previewRig.transform.position.y;
            float rigZ = _previewRig.transform.position.z;

            float shiftX = rigX - bounds.center.x;
            float shiftY = rigY - bounds.min.y;
            float shiftZ = rigZ - bounds.center.z;

            _previewModel.transform.position = curPos + new Vector3(shiftX, shiftY, shiftZ);

            // Recalcula bounds após reposicionamento
            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            float height = bounds.size.y;
            float width = bounds.size.x;
            float depth = bounds.size.z;
            float maxDim = Mathf.Max(height, width * 1.3f, depth);

            float fovRad = _previewCamera.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float dist = (maxDim * 0.5f) / Mathf.Tan(fovRad) * 1.25f;

            Vector3 targetPoint = bounds.center + Vector3.up * (height * 0.04f);
            _previewCamera.transform.position = targetPoint + new Vector3(0f, height * 0.06f, -dist);
            _previewCamera.transform.LookAt(targetPoint);
            _previewCamera.nearClipPlane = Mathf.Min(0.05f, dist * 0.05f);
            _previewCamera.farClipPlane = Mathf.Max(100f, dist * 6f);
        }

        private void CleanupPreviewRig()
        {
            if (_previewRenderTexture != null)
            {
                _previewRenderTexture.Release();
                Destroy(_previewRenderTexture);
                _previewRenderTexture = null;
            }

            if (_previewRig != null)
            {
                Destroy(_previewRig);
                _previewRig = null;
            }

            _previewModel = null;
            _previewAnimator = null;
        }

        public void RotatePreviewModel(float deltaX)
        {
            if (_previewModel != null)
            {
                _previewModel.transform.Rotate(Vector3.up, -deltaX * 0.7f, Space.Self);
            }
        }

        // ── Construção Dinâmica da UI (WoW-Style Proportions & Solid Backings) ──

        public void EnsureUIHierarchy()
        {
            if (_characterRoot != null) return;

            if (_canvas == null)
            {
                TryFindIntegrations();
                if (_canvas == null) return;
            }

            // 1. Moldura Principal da Janela (~2/3 da altura da tela: 336x364)
            var frameGO = new GameObject("CharacterFrame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            frameGO.transform.SetParent(_canvas.transform, false);

            _characterRoot = frameGO.GetComponent<RectTransform>();
            _characterRoot.anchorMin = new Vector2(0.5f, 0.5f);
            _characterRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _characterRoot.pivot = new Vector2(0.5f, 0.5f);
            _characterRoot.sizeDelta = new Vector2(336f, 364f);
            _characterRoot.anchoredPosition = new Vector2(0f, 0f);

            var frameImg = frameGO.GetComponent<Image>();
            frameImg.raycastTarget = true;
            if (panelFrameSprite != null)
            {
                frameImg.sprite = panelFrameSprite;
                frameImg.type = Image.Type.Sliced;
                frameImg.color = Color.white;
            }
            else
            {
                frameImg.color = new Color(0.10f, 0.12f, 0.16f, 0.98f);
            }

            var frameOutline = frameGO.AddComponent<Outline>();
            frameOutline.effectColor = new Color(0.24f, 0.28f, 0.36f, 0.95f);
            frameOutline.effectDistance = new Vector2(1.5f, -1.5f);

            var frameDrag = frameGO.AddComponent<DraggablePanel>();
            frameDrag.TargetPanel = _characterRoot;

            // 2. Barra de Cabeçalho com background escuro dedicado
            var headerGO = new GameObject("HeaderBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            headerGO.transform.SetParent(frameGO.transform, false);

            var headerRect = headerGO.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0, 1);
            headerRect.anchorMax = new Vector2(1, 1);
            headerRect.pivot = new Vector2(0.5f, 1);
            headerRect.anchoredPosition = Vector2.zero;
            headerRect.sizeDelta = new Vector2(0, 28);

            var headerImg = headerGO.GetComponent<Image>();
            headerImg.color = new Color(0.08f, 0.09f, 0.13f, 0.95f);
            headerImg.raycastTarget = true;

            var headerDrag = headerGO.AddComponent<DraggablePanel>();
            headerDrag.TargetPanel = _characterRoot;

            // Título no Cabeçalho
            var titleGO = CreateText("Title", headerGO.transform, "PERSONAGEM", 11f, FontStyles.Bold, new Color(0.98f, 0.82f, 0.38f, 1f), TextAlignmentOptions.Left);
            var titleRect = titleGO.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 0);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.pivot = new Vector2(0, 0.5f);
            titleRect.anchoredPosition = new Vector2(10, 0);
            titleRect.sizeDelta = new Vector2(-155, 0);
            _titleText = titleGO.GetComponent<TextMeshProUGUI>();

            // Container Badge da Classe / Nível
            var classBadge = CreatePanel("ClassBadge", headerGO.transform, new Color(0.13f, 0.16f, 0.22f, 0.95f));
            classBadge.anchorMin = new Vector2(1, 0.5f);
            classBadge.anchorMax = new Vector2(1, 0.5f);
            classBadge.pivot = new Vector2(1, 0.5f);
            classBadge.sizeDelta = new Vector2(114, 18);
            classBadge.anchoredPosition = new Vector2(-30, 0);

            var badgeOutline = classBadge.gameObject.AddComponent<Outline>();
            badgeOutline.effectColor = new Color(0.25f, 0.30f, 0.40f, 0.8f);
            badgeOutline.effectDistance = new Vector2(1f, -1f);

            var subGO = CreateText("ClassSubtitle", classBadge.transform, "Guerreiro • Nv. 1", 8.5f, FontStyles.Bold, new Color(0.58f, 0.78f, 1f, 1f), TextAlignmentOptions.Center);
            var subRect = subGO.GetComponent<RectTransform>();
            subRect.anchorMin = Vector2.zero;
            subRect.anchorMax = Vector2.one;
            subRect.sizeDelta = Vector2.zero;
            _classSubtitleText = subGO.GetComponent<TextMeshProUGUI>();

            // Botão Fechar (X)
            var closeBtnGO = CreateButton("CloseButton", headerGO.transform, "X", new Vector2(18, 18), new Color(0.7f, 0.2f, 0.2f, 1f));
            var closeRect = closeBtnGO.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1, 0.5f);
            closeRect.anchorMax = new Vector2(1, 0.5f);
            closeRect.pivot = new Vector2(1, 0.5f);
            closeRect.anchoredPosition = new Vector2(-6, 0);
            closeBtnGO.GetComponent<Button>().onClick.AddListener(Close);

            // Divisória do Cabeçalho
            var headerDiv = CreatePanel("HeaderDivider", frameGO.transform, new Color(0.24f, 0.28f, 0.36f, 0.8f));
            headerDiv.anchorMin = new Vector2(0, 1);
            headerDiv.anchorMax = new Vector2(1, 1);
            headerDiv.pivot = new Vector2(0.5f, 1);
            headerDiv.sizeDelta = new Vector2(-16, 1.5f);
            headerDiv.anchoredPosition = new Vector2(0, -28);

            // 3. Coluna Esquerda: 6 Slots de Equipamento com container escuro dedicado
            var leftCol = CreatePanel("LeftSlotsColumn", frameGO.transform, new Color(0.06f, 0.07f, 0.10f, 0.88f));
            leftCol.anchorMin = new Vector2(0, 1);
            leftCol.anchorMax = new Vector2(0, 1);
            leftCol.pivot = new Vector2(0, 1);
            leftCol.sizeDelta = new Vector2(38, 208);
            leftCol.anchoredPosition = new Vector2(8, -33);

            var leftOutline = leftCol.gameObject.AddComponent<Outline>();
            leftOutline.effectColor = new Color(0.20f, 0.24f, 0.32f, 0.7f);
            leftOutline.effectDistance = new Vector2(1f, -1f);

            EquipmentSlot[] leftSlots = {
                EquipmentSlot.Head,
                EquipmentSlot.Neck,
                EquipmentSlot.Shoulder,
                EquipmentSlot.Back,
                EquipmentSlot.Chest,
                EquipmentSlot.Wrist
            };

            for (int i = 0; i < leftSlots.Length; i++)
            {
                var slot = leftSlots[i];
                var slotView = CreateSlotView(slot, leftCol.transform, new Vector2(3, -2 - i * 34.5f), 32f);
                _slotViews[slot] = slotView;
            }

            // 4. Coluna Direita: 6 Slots de Equipamento com container escuro dedicado
            var rightCol = CreatePanel("RightSlotsColumn", frameGO.transform, new Color(0.06f, 0.07f, 0.10f, 0.88f));
            rightCol.anchorMin = new Vector2(1, 1);
            rightCol.anchorMax = new Vector2(1, 1);
            rightCol.pivot = new Vector2(1, 1);
            rightCol.sizeDelta = new Vector2(38, 208);
            rightCol.anchoredPosition = new Vector2(-8, -33);

            var rightOutline = rightCol.gameObject.AddComponent<Outline>();
            rightOutline.effectColor = new Color(0.20f, 0.24f, 0.32f, 0.7f);
            rightOutline.effectDistance = new Vector2(1f, -1f);

            EquipmentSlot[] rightSlots = {
                EquipmentSlot.Hands,
                EquipmentSlot.Waist,
                EquipmentSlot.Legs,
                EquipmentSlot.Feet,
                EquipmentSlot.Ring1,
                EquipmentSlot.Ring2
            };

            for (int i = 0; i < rightSlots.Length; i++)
            {
                var slot = rightSlots[i];
                var slotView = CreateSlotView(slot, rightCol.transform, new Vector2(3, -2 - i * 34.5f), 32f);
                _slotViews[slot] = slotView;
            }

            // 5. Centro: Painel Paperdoll do Personagem (Preview 3D + Pedestal Integrado)
            var centerBox = CreatePanel("CenterPreviewBox", frameGO.transform, new Color(0.04f, 0.05f, 0.07f, 0.98f));
            centerBox.anchorMin = new Vector2(0.5f, 1);
            centerBox.anchorMax = new Vector2(0.5f, 1);
            centerBox.pivot = new Vector2(0.5f, 1);
            centerBox.sizeDelta = new Vector2(236, 208);
            centerBox.anchoredPosition = new Vector2(0, -33);

            var viewportOutline = centerBox.gameObject.AddComponent<Outline>();
            viewportOutline.effectColor = new Color(0.22f, 0.26f, 0.34f, 0.8f);
            viewportOutline.effectDistance = new Vector2(1.5f, -1.5f);

            // RawImage para renderizar o RenderTexture da Câmera 3D
            var rawImageGO = new GameObject("Character3DRawImage", typeof(RectTransform), typeof(RawImage));
            rawImageGO.transform.SetParent(centerBox.transform, false);
            var rawRect = rawImageGO.GetComponent<RectTransform>();
            rawRect.anchorMin = Vector2.zero;
            rawRect.anchorMax = Vector2.one;
            rawRect.offsetMin = new Vector2(2, 42);
            rawRect.offsetMax = new Vector2(-2, -2);

            _previewRawImage = rawImageGO.GetComponent<RawImage>();
            _previewRawImage.color = Color.white;

            var dragRotator = rawImageGO.AddComponent<CharacterPreviewRotator>();
            dragRotator.Manager = this;

            // Fallback 2D: Imagem de silhueta (caso a câmera 3D ainda não esteja ativa)
            var fallbackGO = new GameObject("FallbackSilhouette", typeof(RectTransform), typeof(Image));
            fallbackGO.transform.SetParent(rawImageGO.transform, false);
            var fbRect = fallbackGO.GetComponent<RectTransform>();
            fbRect.anchorMin = new Vector2(0.5f, 0.5f);
            fbRect.anchorMax = new Vector2(0.5f, 0.5f);
            fbRect.sizeDelta = new Vector2(70, 105);
            _previewFallbackImage = fallbackGO.GetComponent<Image>();
            _previewFallbackImage.sprite = charSilhouetteSprite;
            _previewFallbackImage.color = new Color(0.6f, 0.7f, 0.85f, 0.35f);
            _previewFallbackImage.preserveAspect = true;
            _previewFallbackImage.gameObject.SetActive(false);

            // Pedestal de Arma Integrado (Rodapé do Painel Paperdoll)
            var weaponShelf = CreatePanel("WeaponShelf", centerBox.transform, new Color(0.07f, 0.08f, 0.11f, 0.96f));
            weaponShelf.anchorMin = new Vector2(0, 0);
            weaponShelf.anchorMax = new Vector2(1, 0);
            weaponShelf.pivot = new Vector2(0.5f, 0);
            weaponShelf.sizeDelta = new Vector2(0, 42);
            weaponShelf.anchoredPosition = Vector2.zero;

            var shelfDiv = CreatePanel("ShelfDivider", weaponShelf.transform, new Color(0.20f, 0.24f, 0.30f, 0.8f));
            shelfDiv.anchorMin = new Vector2(0, 1);
            shelfDiv.anchorMax = new Vector2(1, 1);
            shelfDiv.pivot = new Vector2(0.5f, 1);
            shelfDiv.sizeDelta = new Vector2(0, 1.5f);
            shelfDiv.anchoredPosition = Vector2.zero;

            // Slot de Arma Ativa dentro do pedestal
            var weaponSlotGO = CreateSlotView(null, weaponShelf.transform, new Vector2(0, 20), 30f);
            var weaponSlotRect = weaponSlotGO.GetComponent<RectTransform>();
            weaponSlotRect.anchorMin = new Vector2(0.5f, 0);
            weaponSlotRect.anchorMax = new Vector2(0.5f, 0);
            weaponSlotRect.pivot = new Vector2(0.5f, 0.5f);
            _weaponSlotView = weaponSlotGO;

            // Rótulo da Arma dentro do pedestal
            var weaponLabelGO = CreateText("WeaponLabel", weaponShelf.transform, "ARMA ATIVA", 7.5f, FontStyles.Bold, new Color(0.58f, 0.64f, 0.72f, 1f), TextAlignmentOptions.Center);
            var wLabelRect = weaponLabelGO.GetComponent<RectTransform>();
            wLabelRect.anchorMin = new Vector2(0.5f, 0);
            wLabelRect.anchorMax = new Vector2(0.5f, 0);
            wLabelRect.pivot = new Vector2(0.5f, 0);
            wLabelRect.anchoredPosition = new Vector2(0, 3);
            wLabelRect.sizeDelta = new Vector2(80, 11);

            // 6. Painel Inferior de Estatísticas (Stats Sheet)
            var statsBox = CreatePanel("StatsPanel", frameGO.transform, new Color(0.06f, 0.07f, 0.10f, 0.95f));
            statsBox.anchorMin = new Vector2(0.5f, 1);
            statsBox.anchorMax = new Vector2(0.5f, 1);
            statsBox.pivot = new Vector2(0.5f, 1);
            statsBox.sizeDelta = new Vector2(320, 108);
            statsBox.anchoredPosition = new Vector2(0, -246);

            var statsOutline = statsBox.gameObject.AddComponent<Outline>();
            statsOutline.effectColor = new Color(0.22f, 0.26f, 0.34f, 0.8f);
            statsOutline.effectDistance = new Vector2(1f, -1f);

            // Cabeçalho da Seção de Atributos
            var attrHeader = CreatePanel("AttrHeader", statsBox.transform, new Color(0.10f, 0.12f, 0.16f, 0.9f));
            attrHeader.anchorMin = new Vector2(0, 1);
            attrHeader.anchorMax = new Vector2(1, 1);
            attrHeader.pivot = new Vector2(0.5f, 1);
            attrHeader.sizeDelta = new Vector2(0, 16);
            attrHeader.anchoredPosition = Vector2.zero;

            var attrTitle = CreateText("AttrTitle", attrHeader.transform, "ATRIBUTOS DO PERSONAGEM", 8f, FontStyles.Bold, new Color(0.98f, 0.82f, 0.38f, 1f), TextAlignmentOptions.Left);
            var aTitleRect = attrTitle.GetComponent<RectTransform>();
            aTitleRect.anchorMin = Vector2.zero;
            aTitleRect.anchorMax = Vector2.one;
            aTitleRect.offsetMin = new Vector2(8, 0);
            aTitleRect.offsetMax = new Vector2(-8, 0);

            // 8 Cards de Atributos: 2 Colunas x 4 Linhas com cartões escuros individuais
            float cardW = 150f;
            float cardH = 19f;

            // Coluna 1 (Esquerda: x = 6)
            _hpStatText = CreateStatCard("Vida", statsBox.transform, new Vector2(6, -19), cardW, cardH);
            _damageStatText = CreateStatCard("Dano", statsBox.transform, new Vector2(6, -41), cardW, cardH);
            _attackSpeedStatText = CreateStatCard("Vel. Ataque", statsBox.transform, new Vector2(6, -63), cardW, cardH);
            _miningStatText = CreateStatCard("Mineração", statsBox.transform, new Vector2(6, -85), cardW, cardH);

            // Coluna 2 (Direita: x = 164)
            _moveSpeedStatText = CreateStatCard("Velocidade", statsBox.transform, new Vector2(164, -19), cardW, cardH);
            _critStatText = CreateStatCard("Crítico", statsBox.transform, new Vector2(164, -41), cardW, cardH);
            _defenseStatText = CreateStatCard("Redução Dano", statsBox.transform, new Vector2(164, -63), cardW, cardH);
            _woodcuttingStatText = CreateStatCard("Madeira", statsBox.transform, new Vector2(164, -85), cardW, cardH);

            // Toast Banner de Status
            var bannerGO = new GameObject("StatusBanner", typeof(RectTransform), typeof(Image), typeof(Outline));
            bannerGO.transform.SetParent(frameGO.transform, false);
            var bRect = bannerGO.GetComponent<RectTransform>();
            bRect.anchorMin = new Vector2(0.5f, 1);
            bRect.anchorMax = new Vector2(0.5f, 1);
            bRect.pivot = new Vector2(0.5f, 0.5f);
            bRect.anchoredPosition = new Vector2(0, -244);
            bRect.sizeDelta = new Vector2(280, 16);

            var bImg = bannerGO.GetComponent<Image>();
            bImg.color = new Color(0.05f, 0.06f, 0.08f, 0.95f);

            var bOutline = bannerGO.GetComponent<Outline>();
            bOutline.effectColor = new Color(0.24f, 0.28f, 0.36f, 0.8f);
            bOutline.effectDistance = new Vector2(1f, -1f);

            var statusGO = CreateText("StatusText", bannerGO.transform, string.Empty, 8f, FontStyles.Normal, Color.white, TextAlignmentOptions.Center);
            var statusRect = statusGO.GetComponent<RectTransform>();
            statusRect.anchorMin = Vector2.zero;
            statusRect.anchorMax = Vector2.one;
            statusRect.sizeDelta = Vector2.zero;

            _statusBannerGO = bannerGO;
            _statusMessageText = statusGO.GetComponent<TextMeshProUGUI>();
            _statusBannerGO.SetActive(false);

            // 7. Tooltip Flutuante de Equipamento
            CreateEquipmentTooltipUI(frameGO.transform);

            frameGO.SetActive(false);
        }

        private CharacterSlotView CreateSlotView(EquipmentSlot? slot, Transform parent, Vector2 anchoredPos, float slotSize = 32f)
        {
            var go = new GameObject(slot.HasValue ? $"Slot_{slot.Value}" : "Slot_Weapon", typeof(RectTransform), typeof(Image), typeof(Outline));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.sizeDelta = new Vector2(slotSize, slotSize);
            rect.anchoredPosition = anchoredPos;

            var bgImg = go.GetComponent<Image>();
            bgImg.sprite = slotFrameSprite;
            bgImg.type = Image.Type.Sliced;
            bgImg.color = new Color(0.95f, 0.95f, 0.95f, 1f);

            var outline = go.GetComponent<Outline>();
            outline.effectColor = new Color(0.18f, 0.22f, 0.28f, 0.9f);
            outline.effectDistance = new Vector2(1f, -1f);

            // Ícone da Silhueta (Slot Vazio)
            var silGO = new GameObject("Silhouette", typeof(RectTransform), typeof(Image));
            silGO.transform.SetParent(go.transform, false);
            var silRect = silGO.GetComponent<RectTransform>();
            silRect.anchorMin = Vector2.zero;
            silRect.anchorMax = Vector2.one;
            silRect.sizeDelta = new Vector2(-10, -10);
            var silImg = silGO.GetComponent<Image>();
            silImg.preserveAspect = true;
            silImg.color = new Color(1f, 1f, 1f, 0.35f);
            silImg.raycastTarget = false;

            // Ícone do Item (Equipado)
            var itemGO = new GameObject("ItemIcon", typeof(RectTransform), typeof(Image));
            itemGO.transform.SetParent(go.transform, false);
            var itemRect = itemGO.GetComponent<RectTransform>();
            itemRect.anchorMin = Vector2.zero;
            itemRect.anchorMax = Vector2.one;
            itemRect.sizeDelta = new Vector2(-4, -4);
            var itemImg = itemGO.GetComponent<Image>();
            itemImg.preserveAspect = true;
            itemImg.color = Color.white;
            itemImg.raycastTarget = false;
            itemGO.SetActive(false);

            var slotView = go.AddComponent<CharacterSlotView>();
            slotView.Initialize(slot, this, silImg, itemImg, outline);

            return slotView;
        }

        private TextMeshProUGUI CreateStatCard(string label, Transform parent, Vector2 pos, float width, float height)
        {
            var go = new GameObject($"StatCard_{label}", typeof(RectTransform), typeof(Image), typeof(Outline));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(width, height);

            var bgImg = go.GetComponent<Image>();
            bgImg.color = new Color(0.09f, 0.11f, 0.15f, 0.90f);

            var outline = go.GetComponent<Outline>();
            outline.effectColor = new Color(0.18f, 0.22f, 0.30f, 0.6f);
            outline.effectDistance = new Vector2(1f, -1f);

            var labelGO = CreateText("Label", go.transform, label, 8f, FontStyles.Normal, new Color(0.58f, 0.64f, 0.72f, 1f), TextAlignmentOptions.Left);
            var lRect = labelGO.GetComponent<RectTransform>();
            lRect.anchorMin = new Vector2(0, 0);
            lRect.anchorMax = new Vector2(0.52f, 1);
            lRect.pivot = new Vector2(0, 0.5f);
            lRect.anchoredPosition = new Vector2(5, 0);
            lRect.sizeDelta = Vector2.zero;

            var valGO = CreateText("Value", go.transform, "—", 9f, FontStyles.Bold, Color.white, TextAlignmentOptions.Right);
            var vRect = valGO.GetComponent<RectTransform>();
            vRect.anchorMin = new Vector2(0.52f, 0);
            vRect.anchorMax = new Vector2(1, 1);
            vRect.pivot = new Vector2(1, 0.5f);
            vRect.anchoredPosition = new Vector2(-5, 0);
            vRect.sizeDelta = Vector2.zero;

            return valGO.GetComponent<TextMeshProUGUI>();
        }

        private void CreateEquipmentTooltipUI(Transform parent)
        {
            var tooltipGO = new GameObject("EquipmentTooltip", typeof(RectTransform), typeof(Image), typeof(Outline));
            tooltipGO.transform.SetParent(parent, false);

            _tooltipPanel = tooltipGO.GetComponent<RectTransform>();
            _tooltipPanel.anchorMin = new Vector2(0, 1);
            _tooltipPanel.anchorMax = new Vector2(0, 1);
            _tooltipPanel.pivot = new Vector2(0, 1);
            _tooltipPanel.sizeDelta = new Vector2(180, 130);

            var img = tooltipGO.GetComponent<Image>();
            img.color = new Color(0.07f, 0.08f, 0.11f, 0.98f);

            var outline = tooltipGO.GetComponent<Outline>();
            outline.effectColor = new Color(0.24f, 0.28f, 0.36f, 0.9f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            var titleGO = CreateText("TooltipTitle", tooltipGO.transform, "Item", 11, FontStyles.Bold, new Color(0.98f, 0.82f, 0.38f, 1f), TextAlignmentOptions.Left);
            var tRect = titleGO.GetComponent<RectTransform>();
            tRect.anchorMin = new Vector2(0, 1);
            tRect.anchorMax = new Vector2(1, 1);
            tRect.pivot = new Vector2(0, 1);
            tRect.anchoredPosition = new Vector2(10, -8);
            tRect.sizeDelta = new Vector2(-20, 16);
            _tooltipTitle = titleGO.GetComponent<TextMeshProUGUI>();

            var slotGO = CreateText("TooltipSlot", tooltipGO.transform, "Slot", 8.5f, FontStyles.Normal, new Color(0.58f, 0.64f, 0.72f, 1f), TextAlignmentOptions.Left);
            var sRect = slotGO.GetComponent<RectTransform>();
            sRect.anchorMin = new Vector2(0, 1);
            sRect.anchorMax = new Vector2(1, 1);
            sRect.pivot = new Vector2(0, 1);
            sRect.anchoredPosition = new Vector2(10, -25);
            sRect.sizeDelta = new Vector2(-20, 14);
            _tooltipSlot = slotGO.GetComponent<TextMeshProUGUI>();

            var statsGO = CreateText("TooltipStats", tooltipGO.transform, "", 9f, FontStyles.Normal, new Color(0.52f, 0.93f, 0.67f, 1f), TextAlignmentOptions.Left);
            var stRect = statsGO.GetComponent<RectTransform>();
            stRect.anchorMin = new Vector2(0, 1);
            stRect.anchorMax = new Vector2(1, 1);
            stRect.pivot = new Vector2(0, 1);
            stRect.anchoredPosition = new Vector2(10, -42);
            stRect.sizeDelta = new Vector2(-20, 44);
            _tooltipStats = statsGO.GetComponent<TextMeshProUGUI>();

            var descGO = CreateText("TooltipDescription", tooltipGO.transform, "", 8.5f, FontStyles.Italic, new Color(0.78f, 0.82f, 0.88f, 1f), TextAlignmentOptions.Left);
            var dRect = descGO.GetComponent<RectTransform>();
            dRect.anchorMin = new Vector2(0, 0);
            dRect.anchorMax = new Vector2(1, 0);
            dRect.pivot = new Vector2(0, 0);
            dRect.anchoredPosition = new Vector2(10, 24);
            dRect.sizeDelta = new Vector2(-20, 22);
            _tooltipDescription = descGO.GetComponent<TextMeshProUGUI>();

            var hintGO = CreateText("TooltipHint", tooltipGO.transform, "Clique direito para desequipar", 8f, FontStyles.Normal, new Color(0.48f, 0.54f, 0.62f, 1f), TextAlignmentOptions.Left);
            var hRect = hintGO.GetComponent<RectTransform>();
            hRect.anchorMin = new Vector2(0, 0);
            hRect.anchorMax = new Vector2(1, 0);
            hRect.pivot = new Vector2(0, 0);
            hRect.anchoredPosition = new Vector2(10, 6);
            hRect.sizeDelta = new Vector2(-20, 14);
            _tooltipHint = hintGO.GetComponent<TextMeshProUGUI>();

            tooltipGO.SetActive(false);
        }

        // ── Auxiliares de Criação de UI ────────────────────────────────────────

        private static RectTransform CreatePanel(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);

            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;

            return go.GetComponent<RectTransform>();
        }

        private static GameObject CreateText(string name, Transform parent, string text, float fontSize, FontStyles style, Color color, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);

            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.Normal;

            return go;
        }

        private static GameObject CreateButton(string name, Transform parent, string label, Vector2 sizeDelta, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = sizeDelta;

            var img = go.GetComponent<Image>();
            img.color = color;

            var labelGO = CreateText("Label", go.transform, label, 12, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
            var labelRect = labelGO.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.sizeDelta = Vector2.zero;

            return go;
        }

        // ── Carregamento de Recursos e Áudio ──────────────────────────────────

        private void LoadSprites()
        {
            panelFrameSprite = Resources.Load<Sprite>("Textures/frame_classic");
            slotFrameSprite = Resources.Load<Sprite>("Textures/inventory_slot");
            charSilhouetteSprite = Resources.Load<Sprite>("Textures/Slots/char_silhouette");

            _slotSilhouetteMap[EquipmentSlot.Head] = Resources.Load<Sprite>("Textures/Slots/slot_head");
            _slotSilhouetteMap[EquipmentSlot.Neck] = Resources.Load<Sprite>("Textures/Slots/slot_neck");
            _slotSilhouetteMap[EquipmentSlot.Shoulder] = Resources.Load<Sprite>("Textures/Slots/slot_shoulder");
            _slotSilhouetteMap[EquipmentSlot.Back] = Resources.Load<Sprite>("Textures/Slots/slot_back");
            _slotSilhouetteMap[EquipmentSlot.Chest] = Resources.Load<Sprite>("Textures/Slots/slot_chest");
            _slotSilhouetteMap[EquipmentSlot.Wrist] = Resources.Load<Sprite>("Textures/Slots/slot_wrist");
            _slotSilhouetteMap[EquipmentSlot.Hands] = Resources.Load<Sprite>("Textures/Slots/slot_hands");
            _slotSilhouetteMap[EquipmentSlot.Waist] = Resources.Load<Sprite>("Textures/Slots/slot_waist");
            _slotSilhouetteMap[EquipmentSlot.Legs] = Resources.Load<Sprite>("Textures/Slots/slot_legs");
            _slotSilhouetteMap[EquipmentSlot.Feet] = Resources.Load<Sprite>("Textures/Slots/slot_feet");
            _slotSilhouetteMap[EquipmentSlot.Ring1] = Resources.Load<Sprite>("Textures/Slots/slot_ring");
            _slotSilhouetteMap[EquipmentSlot.Ring2] = Resources.Load<Sprite>("Textures/Slots/slot_ring");
            _weaponSilhouetteSprite = Resources.Load<Sprite>("Textures/Slots/slot_weapon");
        }

        private void LoadAudio()
        {
            var db = AudioDatabase.Instance?.UI;
            if (db != null)
            {
                openSound = db.modalOpenClip;
                clickSound = db.buttonClickClip;
                equipSound = Resources.Load<AudioClip>("SFX/pickup_common");
                unequipSound = Resources.Load<AudioClip>("SFX/drop_common");
                errorSound = db.errorClip;
            }
            else
            {
                openSound = Resources.Load<AudioClip>("SFX/ui_modal_open");
                clickSound = Resources.Load<AudioClip>("SFX/ui_button_click");
                equipSound = Resources.Load<AudioClip>("SFX/pickup_common");
                unequipSound = Resources.Load<AudioClip>("SFX/drop_common");
                errorSound = Resources.Load<AudioClip>("SFX/ui_error");
            }
        }

        private void PlaySound(AudioClip clip)
        {
            if (clip == null) return;
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayAtPoint(clip, Camera.main != null ? Camera.main.transform.position : Vector3.zero, 1.0f);
            else
                AudioSource.PlayClipAtPoint(clip, Camera.main != null ? Camera.main.transform.position : Vector3.zero);
        }

        private static Vector2 GetPointerPosition()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
                return Mouse.current.position.ReadValue();
#endif
            return Input.mousePosition;
        }

        private static bool IsCharacterPanelKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.C);
#endif
        }

        private static bool IsEscapeKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        public static string FormatSlotName(EquipmentSlot slot) => slot switch
        {
            EquipmentSlot.Head => "Cabeça",
            EquipmentSlot.Neck => "Colar",
            EquipmentSlot.Shoulder => "Ombros",
            EquipmentSlot.Back => "Costas",
            EquipmentSlot.Chest => "Torso",
            EquipmentSlot.Wrist => "Braçadeiras",
            EquipmentSlot.Hands => "Luvas",
            EquipmentSlot.Waist => "Cinto",
            EquipmentSlot.Legs => "Pernas",
            EquipmentSlot.Feet => "Botas",
            EquipmentSlot.Ring1 => "Anel 1",
            EquipmentSlot.Ring2 => "Anel 2",
            _ => slot.ToString()
        };

        public static string FormatStatType(StatType stat) => stat switch
        {
            StatType.HP => "Vida Máxima",
            StatType.Damage => "Dano",
            StatType.MoveSpeed => "Vel. Movimento",
            StatType.AttackSpeed => "Vel. Ataque",
            StatType.CritChance => "Chance de Crítico",
            StatType.DamageReduction => "Redução de Dano",
            StatType.MiningResourceBonus => "Bônus Mineração",
            StatType.WoodcuttingResourceBonus => "Bônus Madeira",
            _ => stat.ToString()
        };
    }

    /// <summary>
    /// Componente de interação para cada slot de equipamento no painel de personagem.
    /// Gerencia hover (tooltip), clique direito (desequipar) e estados vazios/equipados.
    /// </summary>
    public class CharacterSlotView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        private EquipmentSlot? _slot;
        private CharacterUIManager _manager;
        private Image _silhouetteImage;
        private Image _itemImage;
        private Outline _outline;

        private IInventoryItem _currentItem;

        public void Initialize(EquipmentSlot? slot, CharacterUIManager manager, Image silhouette, Image item, Outline outline)
        {
            _slot = slot;
            _manager = manager;
            _silhouetteImage = silhouette;
            _itemImage = item;
            _outline = outline;
        }

        public void SetEmpty(Sprite silhouetteSprite)
        {
            _currentItem = null;
            if (_silhouetteImage != null)
            {
                _silhouetteImage.sprite = silhouetteSprite;
                _silhouetteImage.gameObject.SetActive(silhouetteSprite != null);
            }

            if (_itemImage != null)
            {
                _itemImage.sprite = null;
                _itemImage.gameObject.SetActive(false);
            }

            if (_outline != null)
            {
                _outline.effectColor = new Color(0.18f, 0.22f, 0.28f, 0.8f);
            }
        }

        public void SetEquipped(GearItem gear, Sprite itemSprite)
        {
            _currentItem = gear;
            if (_silhouetteImage != null)
                _silhouetteImage.gameObject.SetActive(false);

            if (_itemImage != null)
            {
                _itemImage.sprite = itemSprite;
                _itemImage.gameObject.SetActive(itemSprite != null);
            }

            if (_outline != null)
            {
                _outline.effectColor = new Color(0.96f, 0.72f, 0.22f, 0.9f); // Dourado
            }
        }

        public void SetEquippedWeapon(WeaponItem weapon, Sprite itemSprite)
        {
            _currentItem = weapon;
            if (_silhouetteImage != null)
                _silhouetteImage.gameObject.SetActive(false);

            if (_itemImage != null)
            {
                _itemImage.sprite = itemSprite;
                _itemImage.gameObject.SetActive(itemSprite != null);
            }

            if (_outline != null)
            {
                _outline.effectColor = new Color(0.98f, 0.42f, 0.22f, 0.9f); // Laranja/Fogo
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_outline != null)
            {
                _outline.effectDistance = new Vector2(2f, -2f);
            }

            if (_currentItem != null && _manager != null)
            {
                _manager.ShowTooltip(_currentItem, _slot, eventData.position);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_outline != null)
            {
                _outline.effectDistance = new Vector2(1f, -1f);
            }

            if (_manager != null)
            {
                _manager.HideTooltip();
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                if (_slot.HasValue && _manager != null)
                {
                    _manager.UnequipSlot(_slot.Value);
                }
            }
        }
    }

    /// <summary>
    /// Componente de rotação 3D por arraste do mouse sobre a área de pré-visualização.
    /// </summary>
    public class CharacterPreviewRotator : MonoBehaviour, IDragHandler, IBeginDragHandler
    {
        public CharacterUIManager Manager { get; set; }

        public void OnBeginDrag(PointerEventData eventData)
        {
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (Manager != null)
            {
                Manager.RotatePreviewModel(eventData.delta.x);
            }
        }
    }
}
