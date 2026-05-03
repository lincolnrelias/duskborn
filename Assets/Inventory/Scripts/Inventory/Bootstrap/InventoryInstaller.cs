using System;
using System.Collections.Generic;
using InventorySystem.Core;
using InventorySystem.Data;
using InventorySystem.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace InventorySystem.Bootstrap
{
    public sealed class InventoryInstaller : MonoBehaviour
    {
        [SerializeField] private ItemDefinitionBase[] startupItems;
        [Header("Editor UI References")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private RectTransform gridRoot;
        [SerializeField] private InventoryGridLayoutController gridController;
        [SerializeField] private Image dragIcon;
        [SerializeField] private RectTransform tooltipPanel;
        [SerializeField] private TextMeshProUGUI tooltipTitle;
        [SerializeField] private TextMeshProUGUI tooltipDescription;
        [Header("Tooltip")]
        [SerializeField] private InventoryTooltipController.TooltipBehaviorMode tooltipBehaviorMode = InventoryTooltipController.TooltipBehaviorMode.HoverFollowPointer;
        [SerializeField] private InventoryTooltipController.TooltipAnchor tooltipAnchor = InventoryTooltipController.TooltipAnchor.TopLeft;
        [SerializeField, Min(0f)] private float tooltipDisplayDelay = 1f;
        [Header("Debug Tools")]
        [SerializeField] private bool showDebugTools = true;

        private InventoryService _service;
        private InventoryPresenter _presenter;
        private InventoryDragController _dragController;
        private InventoryTooltipController _tooltipController;
        private InventoryInteractionPanelController _interactionPanelController;
        private GameObject _debugToolsRoot;
        private RectTransform _debugToolsRect;
        private int _runtimeItemCounter;
        private readonly Dictionary<string, Texture2D> _iconByItemId = new();

        public InventoryService Service => _service;
        public IInventory Inventory => _service;
        public IReadOnlyList<InventorySlotView> SlotViews => _presenter?.SlotElements;

        public event Action<IInventoryItem, int> OnItemDroppedOutside;

#if UNITY_EDITOR
        [ContextMenu("Populate Slots")]
        private void PopulateSlotsFromInstaller()
        {
            if (gridController == null)
            {
                Debug.LogError("InventoryInstaller: gridController is not assigned.");
                return;
            }

            gridController.PopulateSlots();
        }

        [ContextMenu("Clear Generated Slots")]
        private void ClearGeneratedSlotsFromInstaller()
        {
            if (gridController == null)
            {
                Debug.LogError("InventoryInstaller: gridController is not assigned.");
                return;
            }

            gridController.ClearGeneratedSlots();
        }
#endif

        private void Awake()
        {
            EnsureEventSystem();
            if (!ValidateReferences())
            {
                enabled = false;
                return;
            }

            gridController.ApplyLayout();

            _service = new InventoryService(new InventoryGrid(gridController.Columns, gridController.Rows));
            CacheStartupItemIcons();

            _presenter = new InventoryPresenter(
                _service,
                gridRoot,
                BuildViewModel,
                ResolveIconTexture);

            _dragController = new InventoryDragController(
                _service,
                _presenter,
                canvas,
                dragIcon,
                gridController.HoldThresholdSeconds,
                gridRoot,
                (item, srcSlot) => OnItemDroppedOutside?.Invoke(item, srcSlot));

            _tooltipController = new InventoryTooltipController(
                _service,
                _presenter,
                canvas,
                tooltipPanel,
                tooltipTitle,
                tooltipDescription,
                BuildTooltip,
                Vector2.zero,
                tooltipAnchor,
                tooltipDisplayDelay,
                tooltipBehaviorMode);

            if (showDebugTools)
            {
                CreateDebugToolsUi(
                    out _debugToolsRoot,
                    out var addMaterialButton,
                    out var addEquipmentButton,
                    out var removeLastButton,
                    out var fillRandomButton,
                    out var clearAllButton);

                _interactionPanelController = new InventoryInteractionPanelController(
                    _service,
                    addMaterialButton,
                    addEquipmentButton,
                    removeLastButton,
                    fillRandomButton,
                    clearAllButton,
                    CreateDebugMaterialItem,
                    CreateDebugEquipmentItem);
            }

            AddStartupItems();
        }

        private void Update()
        {
            _dragController?.Tick();
            _tooltipController?.Tick();
            UpdateDebugToolsLayout();

            if (IsHotkeyPressedAddMaterial())
            {
                TryAddMaterial();
            }

            if (IsHotkeyPressedAddEquipment())
            {
                TryAddEquipment();
            }

            if (IsHotkeyPressedRemoveLast())
            {
                RemoveLastItem();
            }
        }

        private void OnDestroy()
        {
            _interactionPanelController?.Dispose();
            _tooltipController?.Dispose();
            _dragController?.Dispose();
            _presenter?.Dispose();
            if (_debugToolsRoot != null)
            {
                Destroy(_debugToolsRoot);
                _debugToolsRoot = null;
            }
        }

        private static ItemViewModel BuildViewModel(IInventoryItem item)
        {
            int stackSize = item is IStackable s ? s.StackSize : 0;
            return new ItemViewModel(item.DisplayName, item.Description, item.IconId, stackSize);
        }

        private string BuildTooltip(IInventoryItem item)
        {
            return item.Kind switch
            {
                InventoryItemKind.Equipment => $"Type: Equipment\n{item.Description}",
                InventoryItemKind.Material => $"Type: Material\n{item.Description}",
                _ => item.Description
            };
        }

        private void AddStartupItems()
        {
            if (startupItems == null)
            {
                return;
            }

            for (var i = 0; i < startupItems.Length; i++)
            {
                var definition = startupItems[i];
                if (definition == null)
                {
                    continue;
                }

                var item = definition.CreateRuntimeItem();
                _service.TryAddItem(item, out _);
            }
        }

        private void CacheStartupItemIcons()
        {
            _iconByItemId.Clear();
            if (startupItems == null)
            {
                return;
            }

            for (var i = 0; i < startupItems.Length; i++)
            {
                var definition = startupItems[i];
                if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
                {
                    continue;
                }

                if (definition.Icon != null)
                {
                    _iconByItemId[definition.Id] = definition.Icon;
                }
            }
        }

        private Texture2D ResolveIconTexture(IInventoryItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Id))
            {
                return null;
            }

            return _iconByItemId.TryGetValue(item.Id, out var texture) ? texture : null;
        }

        public void RegisterIcon(string id, Texture2D icon)
        {
            if (!string.IsNullOrWhiteSpace(id) && icon != null)
                _iconByItemId[id] = icon;
        }

        public bool TryAddMaterial()
        {
            return _service.TryAddItem(CreateDebugMaterialItem(), out _);
        }

        public bool TryAddEquipment()
        {
            return _service.TryAddItem(CreateDebugEquipmentItem(), out _);
        }

        public bool RemoveAt(int slotIndex)
        {
            return _service.RemoveItem(slotIndex);
        }

        public bool RemoveLastItem()
        {
            for (var i = _service.Grid.SlotCount - 1; i >= 0; i--)
            {
                if (_service.GetItem(i) != null)
                {
                    return _service.RemoveItem(i);
                }
            }

            return false;
        }

        public void ClearAllItems()
        {
            for (var i = _service.Grid.SlotCount - 1; i >= 0; i--)
            {
                _service.RemoveItem(i);
            }
        }

        private IInventoryItem CreateDebugMaterialItem()
        {
            _runtimeItemCounter++;
            return new MaterialItem(
                $"mat_runtime_{_runtimeItemCounter}",
                $"Material {_runtimeItemCounter}",
                "Generated material item",
                string.Empty,
                "runtime");
        }

        private IInventoryItem CreateDebugEquipmentItem()
        {
            _runtimeItemCounter++;
            return new EquipmentItem(
                $"eq_runtime_{_runtimeItemCounter}",
                $"Equipment {_runtimeItemCounter}",
                "Generated equipment item",
                string.Empty,
                UnityEngine.Random.Range(5, 31));
        }

        private static bool IsHotkeyPressedAddMaterial()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.F1);
#endif
        }

        private static bool IsHotkeyPressedAddEquipment()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.f2Key.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.F2);
#endif
        }

        private static bool IsHotkeyPressedRemoveLast()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.F3);
#endif
        }

        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null)
            {
                return;
            }

            var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(eventSystemObject);
        }

        private bool ValidateReferences()
        {
            if (canvas == null || gridRoot == null || dragIcon == null ||
                tooltipPanel == null || tooltipTitle == null || tooltipDescription == null || gridController == null)
            {
                Debug.LogError("InventoryInstaller: assign all required editor UI references.");
                return false;
            }

            return true;
        }

        private void CreateDebugToolsUi(
            out GameObject rootObject,
            out Button addMaterialButton,
            out Button addEquipmentButton,
            out Button removeLastButton,
            out Button fillRandomButton,
            out Button clearAllButton)
        {
            var debugPanelParent = canvas.transform as RectTransform;
            var rootRect = CreatePanel("DebugToolsPanel", debugPanelParent, new Color(0.12f, 0.12f, 0.16f, 0.95f));
            rootRect.anchorMin = new Vector2(1f, 1f);
            rootRect.anchorMax = new Vector2(1f, 1f);
            rootRect.pivot = new Vector2(1f, 1f);
            rootRect.sizeDelta = new Vector2(190f, 255f);
            _debugToolsRect = rootRect;
            UpdateDebugToolsLayout();

            addMaterialButton = CreateDebugButton("AddMaterial", rootRect, "Add Material", new Vector2(0f, -35f));
            addEquipmentButton = CreateDebugButton("AddEquipment", rootRect, "Add Equipment", new Vector2(0f, -77f));
            removeLastButton = CreateDebugButton("RemoveLast", rootRect, "Remove Last", new Vector2(0f, -119f));
            fillRandomButton = CreateDebugButton("FillRandom", rootRect, "Fill Random", new Vector2(0f, -161f));
            clearAllButton = CreateDebugButton("ClearAll", rootRect, "Clear All", new Vector2(0f, -203f));
            rootObject = rootRect.gameObject;
        }

        private void UpdateDebugToolsLayout()
        {
            if (_debugToolsRect == null || canvas == null || canvas.transform is not RectTransform canvasRect)
            {
                return;
            }

            var canvasSize = canvasRect.rect.size;
            if (canvasSize.x <= 0f || canvasSize.y <= 0f)
            {
                return;
            }

            // Keep panel close to the corner while scaling with resolution.
            var edgePadding = Mathf.Clamp(Mathf.Min(canvasSize.x, canvasSize.y) * 0.005f, 2f, 10f);
            _debugToolsRect.anchoredPosition = new Vector2(-edgePadding, -edgePadding);

            var widthScale = canvasSize.x / 1920f;
            var heightScale = canvasSize.y / 1080f;
            var panelScale = Mathf.Clamp(Mathf.Min(widthScale, heightScale), 0.75f, 1.4f);
            _debugToolsRect.localScale = new Vector3(panelScale, panelScale, 1f);
        }

        private static RectTransform CreatePanel(string name, RectTransform parent, Color color)
        {
            var panelObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            panelObject.transform.SetParent(parent, false);
            var rect = panelObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            var image = panelObject.GetComponent<Image>();
            image.color = color;
            return rect;
        }

        private static Button CreateDebugButton(string name, RectTransform parent, string label, Vector2 anchoredPosition)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(-20f, 34f);

            var image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.2f, 0.2f, 0.26f, 1f);
            var button = buttonObject.GetComponent<Button>();

            var labelObject = new GameObject(name + "Label", typeof(RectTransform));
            labelObject.transform.SetParent(rect, false);
            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            var tmp = labelObject.AddComponent<TextMeshProUGUI>();
            if (TMP_Settings.defaultFontAsset != null)
            {
                tmp.font = TMP_Settings.defaultFontAsset;
            }

            tmp.text = label;
            tmp.fontSize = 14f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            return button;
        }

    }
}
