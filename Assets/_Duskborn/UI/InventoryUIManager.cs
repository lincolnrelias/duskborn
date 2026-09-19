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
        private bool              _dropEventSubscribed;
        private bool              _actionBarDropSubscribed;

        private readonly Dictionary<string, MaterialDefinition> _defById      = new();
        private readonly Dictionary<string, int>                _resourceSlot = new();

        // Tracks which slot the pointer is currently hovering over (-1 = none)
        private int _hoveredInventorySlot  = -1;
        private int _hoveredActionBarSlot  = -1;
        private bool _slotEventsSubscribed = false;

        private bool InstallerReady => installer != null && installer.Inventory != null;

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
            }

            foreach (var slot in actionBarInstaller.SlotViews)
            {
                slot.PointerEntered += OnActionBarSlotEntered;
                slot.PointerExited  += OnActionBarSlotExited;
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
            if (_resourceInventory != null && _playerInteractor != null) return;

            foreach (var combat in FindObjectsByType<PlayerCombat>(FindObjectsSortMode.None))
            {
                if (!combat.IsOwner) continue;

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

            // Libera o cursor e pausa a rotação da câmera quando o inventário estiver aberto
            bool otherMenuOpen = CraftingUIManager.Instance != null && CraftingUIManager.Instance.IsOpen;
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
            else if (CraftingUIManager.Instance == null || !CraftingUIManager.Instance.IsOpen)
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
    }
}
