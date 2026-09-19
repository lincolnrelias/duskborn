using System;
using System.Collections.Generic;
using InventorySystem.Core;
using InventorySystem.Data;
using InventorySystem.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Duskborn.Gameplay.ActionBar
{
    [ExecuteAlways]
    public sealed class ActionBarInstaller : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Canvas                        canvas;
        [SerializeField] private RectTransform                 actionBarRoot;
        [SerializeField] private InventoryGridLayoutController gridController;
        [SerializeField] private Image                         dragIcon;

        [Header("Layout Settings (Consistência com Inventário)")]
        [SerializeField] private Vector2                       slotCellSize = new(55f, 55f);
        [SerializeField] private Vector2                       slotSpacing = new(4f, 4f);
        [SerializeField] private float                         bottomOffset = 15f;
        [SerializeField] private bool                          syncScalerWithInventory = true;

        [Header("Highlight")]
        [SerializeField] private Color selectedSlotColor = Color.white;
        [SerializeField] private Color normalSlotColor   = new(0.4f, 0.4f, 0.4f, 1f);

        [Header("Starting Items")]
        [SerializeField] private ItemDefinitionBase[] startingItems;

        private ActionBarService         _service;
        private InventoryPresenter       _presenter;
        private InventoryDragController  _dragController;
        private readonly Dictionary<string, Texture2D> _iconByItemId = new();

        public ActionBarService                   Service    => _service;
        public IReadOnlyList<InventorySlotView>   SlotViews  => _presenter?.SlotElements;

        public event Action<IInventoryItem, int> OnItemDroppedOutside;

        private void OnEnable()
        {
            ConfigureCanvasAndLayout();
        }

        private void OnValidate()
        {
            ConfigureCanvasAndLayout();
        }

        private void Start()
        {
            ConfigureCanvasAndLayout();
        }

        private void Awake()
        {
            ConfigureCanvasAndLayout();

            if (!Application.isPlaying)
            {
                return;
            }

            if (!ValidateReferences())
            {
                enabled = false;
                return;
            }

            gridController.ApplyLayout();

            // Re-assegura dimensões exatas de célula após ApplyLayout do gridController
            var glg = gridController.GetComponent<GridLayoutGroup>();
            if (glg != null)
            {
                float cellW = slotCellSize.x > 0f ? slotCellSize.x : 55f;
                float cellH = slotCellSize.y > 0f ? slotCellSize.y : 55f;
                glg.cellSize = new Vector2(cellW, cellH);
            }

            _service = new ActionBarService(gridController.SlotCount);

            CacheStartupItemIcons();

            _presenter = new InventoryPresenter(
                _service.Service,
                actionBarRoot,
                BuildViewModel,
                ResolveIconTexture);

            _dragController = new InventoryDragController(
                _service.Service,
                _presenter,
                canvas,
                dragIcon,
                gridController.HoldThresholdSeconds,
                actionBarRoot,
                (item, srcSlot) => OnItemDroppedOutside?.Invoke(item, srcSlot));

            _service.SelectedSlotChanged += RefreshSelectionHighlight;
            RefreshSelectionHighlight(_service.SelectedIndex);

            AddStartingItems();
        }

        private void Update()
        {
            _dragController?.Tick();
        }

        private void OnDestroy()
        {
            if (_service != null)
                _service.SelectedSlotChanged -= RefreshSelectionHighlight;
            _dragController?.Dispose();
            _presenter?.Dispose();
        }

        public void RegisterIcon(string id, Texture2D icon)
        {
            if (!string.IsNullOrWhiteSpace(id) && icon != null)
            {
                _iconByItemId[id] = icon;
                ItemIconRegistry.Register(id, icon);
            }
        }

        public Texture2D GetIcon(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            if (_iconByItemId.TryGetValue(id, out var icon)) return icon;
            return ItemIconRegistry.TryGetIcon(id, out icon) ? icon : null;
        }

        private void RefreshSelectionHighlight(int selectedIndex)
        {
            if (_presenter == null) return;
            var slots = _presenter.SlotElements;
            for (int i = 0; i < slots.Count; i++)
                slots[i].SetBorderColor(i == selectedIndex ? selectedSlotColor : normalSlotColor);
        }

        private void AddStartingItems()
        {
            if (startingItems == null) return;
            foreach (var def in startingItems)
            {
                if (def == null) continue;
                var item = def.CreateRuntimeItem();
                if (!_service.Service.TryAddItem(item, out _))
                    DuskLog.Warn(LogChannel.ActionBar, $"Could not add starting item '{def.name}' to action bar (no free slot).");
            }
        }

        private void CacheStartupItemIcons()
        {
            if (startingItems == null) return;
            foreach (var def in startingItems)
            {
                if (def == null || string.IsNullOrWhiteSpace(def.Id)) continue;
                if (def.Icon != null)
                {
                    _iconByItemId[def.Id] = def.Icon;
                    ItemIconRegistry.Register(def.Id, def.Icon);
                }
            }
        }

        private Texture2D ResolveIconTexture(IInventoryItem item)
        {
            if (item == null) return null;
            if (!string.IsNullOrWhiteSpace(item.Id) && _iconByItemId.TryGetValue(item.Id, out var tex))
                return tex;
            return ItemIconRegistry.Resolve(item);
        }

        private static ItemViewModel BuildViewModel(IInventoryItem item)
        {
            int stack = item is IStackable s ? s.StackSize : 0;
            return new ItemViewModel(item.DisplayName, item.Description, item.IconId, stack);
        }

        public void ConfigureCanvasAndLayout()
        {
            if (canvas == null)
                canvas = GetComponentInParent<Canvas>();

            if (canvas != null)
            {
                var scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler == null)
                    scaler = canvas.gameObject.AddComponent<CanvasScaler>();

                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

                CanvasScaler invScaler = syncScalerWithInventory ? FindInventoryCanvasScaler() : null;
                if (invScaler != null)
                {
                    scaler.referenceResolution = invScaler.referenceResolution;
                    scaler.screenMatchMode = invScaler.screenMatchMode;
                    scaler.matchWidthOrHeight = invScaler.matchWidthOrHeight;
                }
                else
                {
                    scaler.referenceResolution = new Vector2(800f, 600f);
                    scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                    scaler.matchWidthOrHeight = 0f;
                }
            }

            if (actionBarRoot != null)
            {
                // Âncora inferior central (Bottom-Center)
                actionBarRoot.anchorMin = new Vector2(0.5f, 0f);
                actionBarRoot.anchorMax = new Vector2(0.5f, 0f);
                actionBarRoot.pivot = new Vector2(0.5f, 0f);

                int cols = gridController != null ? gridController.Columns : 8;
                float cellW = slotCellSize.x > 0f ? slotCellSize.x : 55f;
                float cellH = slotCellSize.y > 0f ? slotCellSize.y : 55f;
                float spacingX = slotSpacing.x >= 0f ? slotSpacing.x : 4f;
                float spacingY = slotSpacing.y >= 0f ? slotSpacing.y : 4f;

                if (gridController != null)
                {
                    var glg = gridController.GetComponent<GridLayoutGroup>();
                    if (glg != null)
                    {
                        glg.cellSize = new Vector2(cellW, cellH);
                        glg.spacing = new Vector2(spacingX, spacingY);
                        glg.childAlignment = TextAnchor.MiddleCenter;
                        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                        glg.constraintCount = cols;
                    }
                }

                float totalW = cols * cellW + Mathf.Max(0, cols - 1) * spacingX;
                float totalH = cellH;
                actionBarRoot.sizeDelta = new Vector2(totalW, totalH);
                actionBarRoot.anchoredPosition = new Vector2(0f, bottomOffset);

                // Normaliza a escala e rotação de todos os slots filhos para evitar distorções
                for (int i = 0; i < actionBarRoot.childCount; i++)
                {
                    var child = actionBarRoot.GetChild(i) as RectTransform;
                    if (child != null)
                    {
                        child.localScale = Vector3.one;
                        child.localRotation = Quaternion.identity;
                    }
                }
            }
        }

        private CanvasScaler FindInventoryCanvasScaler()
        {
            var invInstaller = FindAnyObjectByType<InventorySystem.Bootstrap.InventoryInstaller>();
            if (invInstaller != null)
            {
                var canvasField = typeof(InventorySystem.Bootstrap.InventoryInstaller).GetField("canvas", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var invCanvas = (canvasField?.GetValue(invInstaller) as Canvas) ?? invInstaller.GetComponentInParent<Canvas>();
                if (invCanvas != null)
                {
                    var scaler = invCanvas.GetComponent<CanvasScaler>();
                    if (scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
                        return scaler;
                }
            }

            foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (c == canvas) continue;
                if (c.renderMode == RenderMode.WorldSpace) continue;
                if (c.gameObject.name.IndexOf("Inventory", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var scaler = c.GetComponent<CanvasScaler>();
                    if (scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
                    {
                        return scaler;
                    }
                }
            }

            return null;
        }

        private void OnRectTransformDimensionsChange()
        {
            if (isActiveAndEnabled)
            {
                ConfigureCanvasAndLayout();
            }
        }

        private bool ValidateReferences()
        {
            if (canvas == null || actionBarRoot == null || dragIcon == null || gridController == null)
            {
                Debug.LogError("ActionBarInstaller: assign all required UI references in the Inspector.");
                return false;
            }
            return true;
        }
    }
}
