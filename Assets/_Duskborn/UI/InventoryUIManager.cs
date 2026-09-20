using System.Collections.Generic;
using InventorySystem.Bootstrap;
using InventorySystem.Core;
using InventorySystem.Data;
using InventorySystem.UI;
using Duskborn.Gameplay.ActionBar;
using Duskborn.Gameplay.Loot;
using Duskborn.Gameplay.Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Duskborn.UI
{
    public class InventoryUIManager : MonoBehaviour
    {
        public static InventoryUIManager Instance { get; private set; }
        public int LastClosedFrame { get; private set; } = -1;

        [SerializeField] private InventoryInstaller installer;
        [SerializeField] private GameObject         inventoryRoot;
        [SerializeField] private ActionBarInstaller actionBarInstaller;
        [SerializeField] private MaterialDefinition[] resourceDefinitions;

        private ResourceInventory _resourceInventory;
        private PlayerInteractor  _playerInteractor;
        private Duskborn.Gameplay.Equipment.PlayerEquipmentContainer _playerEquipment;
        private bool              _dropEventSubscribed;
        private bool              _actionBarDropSubscribed;

        private readonly Dictionary<string, MaterialDefinition> _defById      = new();
        private readonly Dictionary<string, int>                _resourceSlot = new();

        // Tracks which slot the pointer is currently hovering over (-1 = none)
        private int _hoveredInventorySlot  = -1;
        private int _hoveredActionBarSlot  = -1;
        private bool _slotEventsSubscribed = false;

        public bool InstallerReady => installer != null && installer.Inventory != null;

        private void Awake()
        {
            Instance = this;
            if (resourceDefinitions != null)
            {
                foreach (var def in resourceDefinitions)
                {
                    if (def == null || string.IsNullOrWhiteSpace(def.Id)) continue;
                    _defById[def.Id] = def;
                }
            }
        }

        private void Start()
        {
            if (installer != null)
            {
                foreach (var def in _defById.Values)
                {
                    if (def.Icon != null)
                    {
                        installer.RegisterIcon(def.Id, def.Icon);
                        if (actionBarInstaller != null)
                            actionBarInstaller.RegisterIcon(def.Id, def.Icon);
                    }
                }

                // Registra ícones das receitas conhecidas
                var recipes = Resources.LoadAll<Duskborn.Gameplay.Crafting.CraftingRecipe>("Crafting");
                foreach (var r in recipes)
                {
                    if (r != null && r.OutputItem != null && r.OutputItem.Icon != null)
                    {
                        installer.RegisterIcon(r.OutputItem.Id, r.OutputItem.Icon);
                        if (actionBarInstaller != null)
                            actionBarInstaller.RegisterIcon(r.OutputItem.Id, r.OutputItem.Icon);
                    }
                }
            }

            EnsureInventoryRoot();

            if (inventoryRoot != null)
            {
                inventoryRoot.SetActive(false);
                SetupDraggableInventory();
            }

            SubscribeSlotHoverEvents();
            SubscribeActionBarDrop();
            PopulatePlaceholderTestGear();
        }

        private void EnsureInventoryRoot()
        {
            if (inventoryRoot != null) return;

            if (installer != null)
            {
                var frame = installer.transform.Find("InventoryFrame");
                if (frame != null)
                {
                    inventoryRoot = frame.gameObject;
                    return;
                }

                var grid = installer.GetComponentInChildren<InventoryGridLayoutController>();
                if (grid != null && grid.transform.parent != null)
                {
                    inventoryRoot = grid.transform.parent.gameObject;
                    return;
                }
            }

            var found = GameObject.Find("InventoryFrame");
            if (found != null)
                inventoryRoot = found;
        }

        private void SetupDraggableInventory()
        {
            if (inventoryRoot == null) return;

            var rootRect = inventoryRoot.GetComponent<RectTransform>();
            if (rootRect == null) return;

            // Remove DraggablePanel do root se existir, para que o arraste de itens nos slots não arraste a janela inteira
            var rootDrag = inventoryRoot.GetComponent<DraggablePanel>();
            if (rootDrag != null)
            {
                Destroy(rootDrag);
            }

            // Adiciona área dedicada para arraste no topo da moldura do inventário
            var handleTransform = inventoryRoot.transform.Find("HeaderDragHandle");
            if (handleTransform == null)
            {
                var handleGO = new GameObject("HeaderDragHandle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                handleGO.transform.SetParent(inventoryRoot.transform, false);

                var hRect = handleGO.GetComponent<RectTransform>();
                hRect.anchorMin = new Vector2(0, 1);
                hRect.anchorMax = new Vector2(1, 1);
                hRect.pivot = new Vector2(0.5f, 1);
                hRect.anchoredPosition = Vector2.zero;
                hRect.sizeDelta = new Vector2(0, 40);

                var hImg = handleGO.GetComponent<Image>();
                hImg.color = new Color(0, 0, 0, 0.001f);
                hImg.raycastTarget = true;

                var hDrag = handleGO.AddComponent<DraggablePanel>();
                hDrag.TargetPanel = rootRect;
            }
        }

        public InventoryInstaller Installer => installer;
        public ActionBarInstaller ActionBarInstaller => actionBarInstaller;
        public RectTransform InventoryFrameRect
        {
            get
            {
                EnsureInventoryRoot();
                return inventoryRoot != null ? inventoryRoot.GetComponent<RectTransform>() : null;
            }
        }

        private void Update()
        {
            TryCacheLocalPlayer();

            if (!_slotEventsSubscribed)
                SubscribeSlotHoverEvents();

            if (IsInventoryKeyDown())
                Toggle();
            else if (IsOpen && IsEscapeKeyDown())
                Close();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_resourceInventory != null)
                _resourceInventory.ResourceChanged -= OnResourceChanged;
            if (_dropEventSubscribed && installer != null)
                installer.OnItemDroppedOutside -= OnInventoryItemDroppedOutside;
            if (_actionBarDropSubscribed && actionBarInstaller != null)
                actionBarInstaller.OnItemDroppedOutside -= OnActionBarItemDroppedOutside;
        }

        // ── Slot hover tracking ───────────────────────────────────────────────

        private void SubscribeSlotHoverEvents()
        {
            if (_slotEventsSubscribed) return;

            bool inventoryReady   = installer != null && installer.SlotViews != null;
            bool actionBarReady   = actionBarInstaller != null && actionBarInstaller.SlotViews != null;

            if (!inventoryReady || !actionBarReady) return;

            foreach (var slot in installer.SlotViews)
            {
                slot.PointerEntered += OnInventorySlotEntered;
                slot.PointerExited  += OnInventorySlotExited;
                slot.PointerClicked += OnInventorySlotClicked;
            }

            foreach (var slot in actionBarInstaller.SlotViews)
            {
                slot.PointerEntered += OnActionBarSlotEntered;
                slot.PointerExited  += OnActionBarSlotExited;
                slot.PointerClicked += OnActionBarSlotClicked;
            }

            _slotEventsSubscribed = true;
        }

        private void OnInventorySlotEntered(int index, PointerEventData _) => _hoveredInventorySlot = index;
        private void OnInventorySlotExited(int index, PointerEventData _)
        {
            if (_hoveredInventorySlot == index) _hoveredInventorySlot = -1;
        }

        private void OnActionBarSlotEntered(int index, PointerEventData _) => _hoveredActionBarSlot = index;
        private void OnActionBarSlotExited(int index, PointerEventData _)
        {
            if (_hoveredActionBarSlot == index) _hoveredActionBarSlot = -1;
        }

        private void OnInventorySlotClicked(int index, PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                TryEquipFromInventory(index);
            }
        }

        private void OnActionBarSlotClicked(int index, PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                TryEquipFromActionBar(index);
            }
        }

        // ── Cross-inventory drop handling ─────────────────────────────────────

        private Vector2 GetPointerScreenPosition()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
                return Mouse.current.position.ReadValue();
#endif
            return Input.mousePosition;
        }

        private int GetTargetActionBarSlot()
        {
            if (_hoveredActionBarSlot >= 0) return _hoveredActionBarSlot;
            if (actionBarInstaller == null || actionBarInstaller.SlotViews == null) return -1;

            Vector2 pointerPos = GetPointerScreenPosition();
            var abCanvas = actionBarInstaller.GetComponentInParent<Canvas>();
            var cam = (abCanvas != null && abCanvas.renderMode != RenderMode.ScreenSpaceOverlay) ? abCanvas.worldCamera : null;

            for (int i = 0; i < actionBarInstaller.SlotViews.Count; i++)
            {
                var slot = actionBarInstaller.SlotViews[i];
                if (slot != null && slot.RectTransform != null &&
                    RectTransformUtility.RectangleContainsScreenPoint(slot.RectTransform, pointerPos, cam))
                {
                    return i;
                }
            }
            return -1;
        }

        private int GetTargetInventorySlot()
        {
            if (_hoveredInventorySlot >= 0) return _hoveredInventorySlot;
            if (installer == null || installer.SlotViews == null) return -1;

            Vector2 pointerPos = GetPointerScreenPosition();
            var invCanvas = installer.GetComponentInParent<Canvas>();
            var cam = (invCanvas != null && invCanvas.renderMode != RenderMode.ScreenSpaceOverlay) ? invCanvas.worldCamera : null;

            for (int i = 0; i < installer.SlotViews.Count; i++)
            {
                var slot = installer.SlotViews[i];
                if (slot != null && slot.RectTransform != null &&
                    RectTransformUtility.RectangleContainsScreenPoint(slot.RectTransform, pointerPos, cam))
                {
                    return i;
                }
            }
            return -1;
        }

        private void SubscribeActionBarDrop()
        {
            if (_actionBarDropSubscribed || actionBarInstaller == null) return;
            actionBarInstaller.OnItemDroppedOutside += OnActionBarItemDroppedOutside;
            _actionBarDropSubscribed = true;
        }

        // Called when an item is dragged out of the main inventory and released with no valid target.
        // The drag controller has already removed it from the inventory service.
        private void OnInventoryItemDroppedOutside(IInventoryItem item, int sourceSlot)
        {
            int targetActionBarSlot = GetTargetActionBarSlot();
            if (targetActionBarSlot >= 0 && actionBarInstaller != null)
            {
                var abService = actionBarInstaller.Service.Service;
                var displaced = abService.GetItem(targetActionBarSlot);
                if (displaced != null)
                {
                    abService.RemoveItem(targetActionBarSlot);
                    abService.TryPlaceItemAt(targetActionBarSlot, item);
                    installer.Service.TryPlaceItemAt(sourceSlot, displaced);
                    DuskLog.Log(LogChannel.ActionBar, $"Swapped '{item.DisplayName}' (inventory→action bar slot {targetActionBarSlot}) with '{displaced.DisplayName}'.");
                }
                else
                {
                    abService.TryPlaceItemAt(targetActionBarSlot, item);
                    DuskLog.Log(LogChannel.ActionBar, $"Moved '{item.DisplayName}' from inventory to action bar slot {targetActionBarSlot}.");
                }
                return;
            }

            // Se soltou fora da grade mas ainda dentro da moldura do inventário, restaura o item no slot original
            if (inventoryRoot != null)
            {
                var invRect = inventoryRoot.GetComponent<RectTransform>();
                var invCanvas = installer != null ? installer.GetComponentInParent<Canvas>() : null;
                var cam = (invCanvas != null && invCanvas.renderMode != RenderMode.ScreenSpaceOverlay) ? invCanvas.worldCamera : null;
                if (invRect != null && RectTransformUtility.RectangleContainsScreenPoint(invRect, GetPointerScreenPosition(), cam))
                {
                    installer.Service.TryPlaceItemAt(sourceSlot, item);
                    return;
                }
            }

            HandleResourceDrop(item);
        }

        // Called when an item is dragged out of the action bar and released with no valid target.
        // The drag controller has already removed it from the action bar service.
        private void OnActionBarItemDroppedOutside(IInventoryItem item, int sourceSlot)
        {
            int targetInventorySlot = GetTargetInventorySlot();
            if (targetInventorySlot >= 0 && InstallerReady)
            {
                var displaced = installer.Service.GetItem(targetInventorySlot);
                if (displaced != null)
                {
                    installer.Service.RemoveItem(targetInventorySlot);
                    installer.Service.TryPlaceItemAt(targetInventorySlot, item);
                    actionBarInstaller.Service.Service.TryPlaceItemAt(sourceSlot, displaced);
                    DuskLog.Log(LogChannel.ActionBar, $"Swapped '{item.DisplayName}' (action bar→inventory slot {targetInventorySlot}) with '{displaced.DisplayName}'.");
                }
                else
                {
                    installer.Service.TryPlaceItemAt(targetInventorySlot, item);
                    DuskLog.Log(LogChannel.ActionBar, $"Moved '{item.DisplayName}' from action bar to inventory slot {targetInventorySlot}.");
                }
                return;
            }

            // Se soltou fora do inventário mas ainda na área da ActionBar, restaura o item no slot original
            if (actionBarInstaller != null)
            {
                var abRect = actionBarInstaller.transform as RectTransform;
                var abCanvas = actionBarInstaller.GetComponentInParent<Canvas>();
                var cam = (abCanvas != null && abCanvas.renderMode != RenderMode.ScreenSpaceOverlay) ? abCanvas.worldCamera : null;
                if (abRect != null && RectTransformUtility.RectangleContainsScreenPoint(abRect, GetPointerScreenPosition(), cam))
                {
                    actionBarInstaller.Service.Service.TryPlaceItemAt(sourceSlot, item);
                    return;
                }
            }
        }

        // ── Player caching ────────────────────────────────────────────────────

        private void TryCacheLocalPlayer()
        {
            if (_resourceInventory != null && _playerInteractor != null && _playerEquipment != null) return;

            foreach (var combat in FindObjectsByType<PlayerCombat>(FindObjectsSortMode.None))
            {
                if (!combat.IsOwner) continue;

                if (_playerEquipment == null)
                {
                    _playerEquipment = combat.GetComponent<Duskborn.Gameplay.Equipment.PlayerEquipmentContainer>();
                }

                if (_resourceInventory == null)
                {
                    var res = combat.GetComponent<ResourceInventory>();
                    if (res != null)
                    {
                        _resourceInventory = res;
                        _resourceInventory.ResourceChanged += OnResourceChanged;
                    }
                    else
                    {
                        DuskLog.Warn(LogChannel.Inventory, $"No ResourceInventory on {combat.name}");
                    }
                }

                if (_playerInteractor == null)
                {
                    _playerInteractor = combat.GetComponent<PlayerInteractor>();
                    if (_playerInteractor != null && !_dropEventSubscribed && installer != null)
                    {
                        installer.OnItemDroppedOutside += OnInventoryItemDroppedOutside;
                        _dropEventSubscribed = true;
                    }
                }

                break;
            }
        }

        public bool TryEquipFromInventory(int slotIndex)
        {
            if (!InstallerReady) return false;
            var item = installer.Service.GetItem(slotIndex);
            if (item == null) return false;

            TryCacheLocalPlayer();
            if (_playerEquipment == null)
            {
                foreach (var combat in FindObjectsByType<PlayerCombat>(FindObjectsSortMode.None))
                {
                    if (combat.IsOwner)
                    {
                        _playerEquipment = combat.GetComponent<Duskborn.Gameplay.Equipment.PlayerEquipmentContainer>();
                        break;
                    }
                }
                if (_playerEquipment == null)
                    _playerEquipment = FindFirstObjectByType<Duskborn.Gameplay.Equipment.PlayerEquipmentContainer>();
            }

            if (item is Duskborn.Gameplay.Equipment.GearItem gearItem)
            {
                if (_playerEquipment == null) return false;

                Duskborn.Gameplay.Equipment.EquipmentSlot targetSlot = gearItem.Slot;
                if (gearItem.Slot == Duskborn.Gameplay.Equipment.EquipmentSlot.Ring1 || gearItem.Slot == Duskborn.Gameplay.Equipment.EquipmentSlot.Ring2)
                {
                    if (_playerEquipment.GetEquipped(Duskborn.Gameplay.Equipment.EquipmentSlot.Ring1) == null)
                        targetSlot = Duskborn.Gameplay.Equipment.EquipmentSlot.Ring1;
                    else if (_playerEquipment.GetEquipped(Duskborn.Gameplay.Equipment.EquipmentSlot.Ring2) == null)
                        targetSlot = Duskborn.Gameplay.Equipment.EquipmentSlot.Ring2;
                    else
                        targetSlot = Duskborn.Gameplay.Equipment.EquipmentSlot.Ring1;
                }

                installer.Service.RemoveItem(slotIndex);
                _playerEquipment.TryEquipToSlot(targetSlot, gearItem, out var displaced);

                if (displaced != null)
                {
                    installer.Service.TryPlaceItemAt(slotIndex, displaced);
                }

                PlayEquipAudio();
                CharacterUIManager.Instance?.Refresh();
                return true;
            }

            if (item is Duskborn.Gameplay.Equipment.WeaponItem weaponItem && actionBarInstaller != null)
            {
                var abService = actionBarInstaller.Service.Service;
                int targetSlot = actionBarInstaller.Service.SelectedIndex;
                if (targetSlot < 0 || targetSlot >= abService.Grid.SlotCount) targetSlot = 0;

                var displaced = abService.GetItem(targetSlot);
                installer.Service.RemoveItem(slotIndex);
                if (displaced != null)
                {
                    abService.RemoveItem(targetSlot);
                    abService.TryPlaceItemAt(targetSlot, weaponItem);
                    installer.Service.TryPlaceItemAt(slotIndex, displaced);
                }
                else
                {
                    abService.TryPlaceItemAt(targetSlot, weaponItem);
                }

                PlayEquipAudio();
                CharacterUIManager.Instance?.Refresh();
                return true;
            }

            return false;
        }

        public bool TryEquipFromActionBar(int slotIndex)
        {
            if (actionBarInstaller?.Service?.Service == null) return false;
            var abService = actionBarInstaller.Service.Service;
            var item = abService.GetItem(slotIndex);
            if (item is not Duskborn.Gameplay.Equipment.GearItem gearItem) return false;

            TryCacheLocalPlayer();
            if (_playerEquipment == null) return false;

            Duskborn.Gameplay.Equipment.EquipmentSlot targetSlot = gearItem.Slot;
            if (gearItem.Slot == Duskborn.Gameplay.Equipment.EquipmentSlot.Ring1 || gearItem.Slot == Duskborn.Gameplay.Equipment.EquipmentSlot.Ring2)
            {
                if (_playerEquipment.GetEquipped(Duskborn.Gameplay.Equipment.EquipmentSlot.Ring1) == null)
                    targetSlot = Duskborn.Gameplay.Equipment.EquipmentSlot.Ring1;
                else if (_playerEquipment.GetEquipped(Duskborn.Gameplay.Equipment.EquipmentSlot.Ring2) == null)
                    targetSlot = Duskborn.Gameplay.Equipment.EquipmentSlot.Ring2;
                else
                    targetSlot = Duskborn.Gameplay.Equipment.EquipmentSlot.Ring1;
            }

            abService.RemoveItem(slotIndex);
            _playerEquipment.TryEquipToSlot(targetSlot, gearItem, out var displaced);

            if (displaced != null)
            {
                abService.TryPlaceItemAt(slotIndex, displaced);
            }

            PlayEquipAudio();
            CharacterUIManager.Instance?.Refresh();
            return true;
        }

        private void PlayEquipAudio()
        {
            var clip = Resources.Load<AudioClip>("SFX/pickup_common");
            if (clip == null) clip = Resources.Load<AudioClip>("SFX/ui_button_click");
            if (clip != null)
            {
                if (Duskborn.Audio.AudioManager.Instance != null)
                    Duskborn.Audio.AudioManager.Instance.PlayAtPoint(clip, Camera.main != null ? Camera.main.transform.position : transform.position, 1.0f);
                else
                    AudioSource.PlayClipAtPoint(clip, Camera.main != null ? Camera.main.transform.position : transform.position);
            }
        }

        private void HandleResourceDrop(IInventoryItem item)
        {
            if (item is not MaterialItem mat || _playerInteractor == null || _resourceInventory == null) return;
            int count = _resourceInventory.GetCount(mat.Id);
            if (count <= 0) return;
            _resourceInventory.TrySpend(mat.Id, count);
            _playerInteractor.DropResource(mat.Id, count);
        }

        private int FindResourceSlot(string resourceId)
        {
            if (!InstallerReady) return -1;
            var slots = installer.Service.GetSlots();
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].Item != null && slots[i].Item.Id == resourceId)
                    return i;
            }
            return -1;
        }

        private void OnResourceChanged(string resourceId, int newTotal)
        {
            if (!InstallerReady) return;

            int existingSlot = FindResourceSlot(resourceId);

            if (newTotal <= 0)
            {
                if (existingSlot >= 0)
                {
                    installer.Inventory.RemoveItem(existingSlot);
                }
                _resourceSlot.Remove(resourceId);
                return;
            }

            _defById.TryGetValue(resourceId, out var def);
            string displayName = def != null ? def.DisplayName : resourceId;
            string iconId      = def?.Icon != null ? def.Icon.name : string.Empty;

            var newItem = new MaterialItem(resourceId, displayName, string.Empty, iconId, "resource", newTotal);

            if (existingSlot >= 0)
            {
                // Substitui no mesmo slot atual do inventário, respeitando movimentações do jogador
                installer.Inventory.RemoveItem(existingSlot);
                installer.Service.TryPlaceItemAt(existingSlot, newItem);
                _resourceSlot[resourceId] = existingSlot;
            }
            else
            {
                if (installer.Inventory.TryAddItem(newItem, out int newSlot))
                    _resourceSlot[resourceId] = newSlot;
            }
        }

        private void Toggle()
        {
            if (inventoryRoot == null) return;
            bool willShow = !inventoryRoot.activeSelf;
            inventoryRoot.SetActive(willShow);

            if (!willShow)
            {
                LastClosedFrame = Time.frameCount;
            }

            if (!willShow && CraftingUIManager.Instance != null && CraftingUIManager.Instance.IsOpen)
            {
                CraftingUIManager.Instance.Close();
            }

            // Se o painel de personagem estiver aberto, ajusta posicionamento lado a lado
            if (CharacterUIManager.Instance != null && CharacterUIManager.Instance.IsOpen)
            {
                var charRoot = CharacterUIManager.Instance.CharacterRoot;
                var invRect = InventoryFrameRect;
                if (charRoot != null && invRect != null)
                {
                    if (willShow)
                    {
                        invRect.anchoredPosition = new Vector2(180f, invRect.anchoredPosition.y);
                        charRoot.anchoredPosition = new Vector2(-180f, invRect.anchoredPosition.y);
                    }
                    else
                    {
                        charRoot.anchoredPosition = new Vector2(0f, charRoot.anchoredPosition.y);
                    }
                }
            }

            // Libera o cursor e pausa a rotação da câmera quando o inventário estiver aberto
            bool otherMenuOpen = (CraftingUIManager.Instance != null && CraftingUIManager.Instance.IsOpen) ||
                                 (CharacterUIManager.Instance != null && CharacterUIManager.Instance.IsOpen);
            bool shouldUnlock = willShow || otherMenuOpen;

            if (shouldUnlock)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                PlayerCameraController.LocalInstance?.SetRotationLocked(true);
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                if (PlayerCameraController.LocalInstance != null)
                {
                    PlayerCameraController.LocalInstance.SetRotationLocked(false);
                    PlayerCameraController.LocalInstance.SetCursorLocked(true);
                }
            }

            if (willShow)
                SyncResources();
        }

        private void SyncResources()
        {
            if (!InstallerReady || _resourceInventory == null) return;

            foreach (var kv in _resourceInventory.Counts)
            {
                if (kv.Value > 0)
                {
                    OnResourceChanged(kv.Key, kv.Value);
                }
                else
                {
                    int slot = FindResourceSlot(kv.Key);
                    if (slot >= 0)
                    {
                        installer.Inventory.RemoveItem(slot);
                    }
                    _resourceSlot.Remove(kv.Key);
                }
            }
        }

        public bool IsOpen => inventoryRoot != null && inventoryRoot.activeSelf;

        public void Open()
        {
            if (!IsOpen)
                Toggle();
        }

        public void Close()
        {
            if (IsOpen)
            {
                LastClosedFrame = Time.frameCount;
                Toggle();
            }
            else if ((CraftingUIManager.Instance == null || !CraftingUIManager.Instance.IsOpen) &&
                     (CharacterUIManager.Instance == null || !CharacterUIManager.Instance.IsOpen))
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                if (PlayerCameraController.LocalInstance != null)
                {
                    PlayerCameraController.LocalInstance.SetRotationLocked(false);
                    PlayerCameraController.LocalInstance.SetCursorLocked(true);
                }
            }
        }

        private static bool IsInventoryKeyDown()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && (Keyboard.current.iKey.wasPressedThisFrame || Keyboard.current.tabKey.wasPressedThisFrame);
#else
            return Input.GetKeyDown(KeyCode.I) || Input.GetKeyDown(KeyCode.Tab);
#endif
        }

        private static bool IsEscapeKeyDown()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        /// <summary>
        /// Popula o inventário com itens de equipamento de teste placeholder cobrindo todos os slots
        /// e com atributos variados (Vida, Dano, Vel. Ataque, Velocidade, Crítico, Redução de Dano, Mineração, Madeira),
        /// permitindo testes imediatos com clique direito.
        /// </summary>
        public void PopulatePlaceholderTestGear()
        {
            if (!InstallerReady) return;
            var inv = installer.Inventory;
            if (inv == null) return;

            var addedIds = new HashSet<string>();

            // 1. Tenta carregar as definições de Resources/Gear
            var gearDefs = Resources.LoadAll<Duskborn.Gameplay.Equipment.GearDefinition>("Gear");
            if (gearDefs != null && gearDefs.Length > 0)
            {
                foreach (var def in gearDefs)
                {
                    if (def == null) continue;

                    if (def.Icon != null)
                    {
                        installer.RegisterIcon(def.Id, def.Icon);
                        ItemIconRegistry.Register(def.Id, def.Icon);
                        if (actionBarInstaller != null)
                            actionBarInstaller.RegisterIcon(def.Id, def.Icon);
                    }

                    bool exists = false;
                    for (int i = 0; i < inv.Grid.SlotCount; i++)
                    {
                        var cur = inv.GetItem(i);
                        if (cur != null && (cur.Id == def.Id || cur.DisplayName == def.DisplayName))
                        {
                            exists = true;
                            break;
                        }
                    }

                    if (!exists)
                    {
                        var runtimeItem = def.CreateRuntimeItem();
                        if (inv.TryAddItem(runtimeItem, out _))
                        {
                            addedIds.Add(def.Id);
                        }
                    }
                }
            }

            // 2. Garante loadout completo de 12 slots caso algum não esteja em Resources
            EnsureProgrammaticTestLoadout(inv, addedIds);
        }

        private void EnsureProgrammaticTestLoadout(IInventory inv, HashSet<string> addedIds)
        {
            var testItems = new (string id, string name, string desc, string icon, Duskborn.Gameplay.Equipment.EquipmentSlot slot, Duskborn.Gameplay.Equipment.StatBonus[] bonuses)[]
            {
                ("gear_iron_helm", "Elmo de Ferro", "HP: +5%", "Iron Helmet", Duskborn.Gameplay.Equipment.EquipmentSlot.Head,
                    new[] { new Duskborn.Gameplay.Equipment.StatBonus { Type = Duskborn.Gameplay.Equipment.StatType.HP, Value = 0.05f } }),

                ("gear_bone_necklace", "Colar de Ossos", "Dano: +7%  Crítico: +3%", "Bone Necklace", Duskborn.Gameplay.Equipment.EquipmentSlot.Neck,
                    new[] {
                        new Duskborn.Gameplay.Equipment.StatBonus { Type = Duskborn.Gameplay.Equipment.StatType.Damage, Value = 0.07f },
                        new Duskborn.Gameplay.Equipment.StatBonus { Type = Duskborn.Gameplay.Equipment.StatType.CritChance, Value = 0.03f }
                    }),

                ("gear_iron_shoulders", "Ombreiras de Ferro", "Dano: +8%  Redução Dano: +4%", "Iron Shoulders", Duskborn.Gameplay.Equipment.EquipmentSlot.Shoulder,
                    new[] {
                        new Duskborn.Gameplay.Equipment.StatBonus { Type = Duskborn.Gameplay.Equipment.StatType.Damage, Value = 0.08f },
                        new Duskborn.Gameplay.Equipment.StatBonus { Type = Duskborn.Gameplay.Equipment.StatType.DamageReduction, Value = 0.04f }
                    }),

                ("gear_shadow_cloak", "Manto das Sombras", "Velocidade: +10%  Crítico: +5%", "Shadow Cloak", Duskborn.Gameplay.Equipment.EquipmentSlot.Back,
                    new[] {
                        new Duskborn.Gameplay.Equipment.StatBonus { Type = Duskborn.Gameplay.Equipment.StatType.MoveSpeed, Value = 0.10f },
                        new Duskborn.Gameplay.Equipment.StatBonus { Type = Duskborn.Gameplay.Equipment.StatType.CritChance, Value = 0.05f }
                    }),

                ("gear_leather_chest", "Peitoral de Couro", "HP: +12%  Redução Dano: +6%", "Iron Armor", Duskborn.Gameplay.Equipment.EquipmentSlot.Chest,
                    new[] {
                        new Duskborn.Gameplay.Equipment.StatBonus { Type = Duskborn.Gameplay.Equipment.StatType.HP, Value = 0.12f },
                        new Duskborn.Gameplay.Equipment.StatBonus { Type = Duskborn.Gameplay.Equipment.StatType.DamageReduction, Value = 0.06f }
                    }),

                ("gear_iron_bracers", "Braçadeiras de Ferro", "Vel. Ataque: +6%  Redução Dano: +3%", "Iron Bracers", Duskborn.Gameplay.Equipment.EquipmentSlot.Wrist,
                    new[] {
                        new Duskborn.Gameplay.Equipment.StatBonus { Type = Duskborn.Gameplay.Equipment.StatType.AttackSpeed, Value = 0.06f },
                        new Duskborn.Gameplay.Equipment.StatBonus { Type = Duskborn.Gameplay.Equipment.StatType.DamageReduction, Value = 0.03f }
                    }),

                ("gear_leather_gloves", "Luvas de Couro", "Vel. Ataque: +8%  Mineração: +15%", "Leather Gloves", Duskborn.Gameplay.Equipment.EquipmentSlot.Hands,
                    new[] {
                        new Duskborn.Gameplay.Equipment.StatBonus { Type = Duskborn.Gameplay.Equipment.StatType.AttackSpeed, Value = 0.08f },
                        new Duskborn.Gameplay.Equipment.StatBonus { Type = Duskborn.Gameplay.Equipment.StatType.MiningResourceBonus, Value = 0.15f }
                    }),

                ("gear_leather_belt", "Cinto de Couro", "HP: +8%  Madeira: +15%", "Belt", Duskborn.Gameplay.Equipment.EquipmentSlot.Waist,
                    new[] {
                        new Duskborn.Gameplay.Equipment.StatBonus { Type = Duskborn.Gameplay.Equipment.StatType.HP, Value = 0.08f },
                        new Duskborn.Gameplay.Equipment.StatBonus { Type = Duskborn.Gameplay.Equipment.StatType.WoodcuttingResourceBonus, Value = 0.15f }
                    }),

                ("gear_iron_greaves", "Grevas de Ferro", "Redução Dano: +8%  HP: +5%", "Iron Greaves", Duskborn.Gameplay.Equipment.EquipmentSlot.Legs,
                    new[] {
                        new Duskborn.Gameplay.Equipment.StatBonus { Type = Duskborn.Gameplay.Equipment.StatType.DamageReduction, Value = 0.08f },
                        new Duskborn.Gameplay.Equipment.StatBonus { Type = Duskborn.Gameplay.Equipment.StatType.HP, Value = 0.05f }
                    }),

                ("gear_worn_boots", "Botas Desgastadas", "Velocidade: +12%", "Iron Boot", Duskborn.Gameplay.Equipment.EquipmentSlot.Feet,
                    new[] { new Duskborn.Gameplay.Equipment.StatBonus { Type = Duskborn.Gameplay.Equipment.StatType.MoveSpeed, Value = 0.12f } }),

                ("gear_copper_ring", "Anel de Cobre", "Crítico: +5%  Vel. Ataque: +4%", "Copper Ring", Duskborn.Gameplay.Equipment.EquipmentSlot.Ring1,
                    new[] {
                        new Duskborn.Gameplay.Equipment.StatBonus { Type = Duskborn.Gameplay.Equipment.StatType.CritChance, Value = 0.05f },
                        new Duskborn.Gameplay.Equipment.StatBonus { Type = Duskborn.Gameplay.Equipment.StatType.AttackSpeed, Value = 0.04f }
                    }),

                ("gear_ruby_ring", "Anel de Rubi", "Dano: +10%  Crítico: +6%", "Ruby Ring", Duskborn.Gameplay.Equipment.EquipmentSlot.Ring2,
                    new[] {
                        new Duskborn.Gameplay.Equipment.StatBonus { Type = Duskborn.Gameplay.Equipment.StatType.Damage, Value = 0.10f },
                        new Duskborn.Gameplay.Equipment.StatBonus { Type = Duskborn.Gameplay.Equipment.StatType.CritChance, Value = 0.06f }
                    })
            };

            foreach (var item in testItems)
            {
                if (addedIds.Contains(item.id)) continue;

                var tex = Resources.Load<Texture2D>($"Textures/Gear/{item.icon}");
                if (tex == null) tex = Resources.Load<Texture2D>(item.icon);
                if (tex != null)
                {
                    installer.RegisterIcon(item.id, tex);
                    ItemIconRegistry.Register(item.id, tex);
                    if (actionBarInstaller != null)
                        actionBarInstaller.RegisterIcon(item.id, tex);
                }

                bool exists = false;
                for (int i = 0; i < inv.Grid.SlotCount; i++)
                {
                    var cur = inv.GetItem(i);
                    if (cur != null && (cur.Id == item.id || cur.DisplayName == item.name))
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                {
                    var runtimeGear = new Duskborn.Gameplay.Equipment.GearItem(item.id, item.name, item.desc, item.icon, item.slot, item.bonuses);
                    inv.TryAddItem(runtimeGear, out _);
                }
            }
        }
    }
}
