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
using Duskborn.Gameplay.ActionBar;
using Duskborn.Gameplay.Crafting;
using Duskborn.Gameplay.Equipment;
using Duskborn.Gameplay.Loot;
using Duskborn.Gameplay.Player;
using InventorySystem.Bootstrap;
using InventorySystem.Core;
using InventorySystem.Data;

namespace Duskborn.UI
{
    /// <summary>
    /// Gerenciador da interface de fabricação (Crafting UI) na Bancada de Trabalho (Workbench).
    /// Integrado com o sistema de inventário (InventoryUIManager), sistema de recursos (ResourceInventory),
    /// câmera (PlayerCameraController) e barra de ação (ActionBarInstaller).
    /// </summary>
    public class CraftingUIManager : MonoBehaviour
    {
        private const float CraftingPanelScale = 0.67f;
        private const float CraftingPanelPairedX = -160f;
        public static CraftingUIManager Instance { get; private set; }

        [Header("Configuração de Áudio")]
        [SerializeField] private AudioClip openSound;
        [SerializeField] private AudioClip clickSound;
        [SerializeField] private AudioClip craftSound;
        [SerializeField] private AudioClip errorSound;

        [Header("Sprites da Interface")]
        [SerializeField] private Sprite panelFrameSprite;
        [SerializeField] private Sprite slotFrameSprite;

        // Estado do Sistema
        public bool IsOpen { get; private set; }
        public int LastClosedFrame { get; private set; } = -1;
        public Workbench CurrentWorkbench { get; private set; }

        private Canvas _canvas;
        private RectTransform _craftingRoot;
        public RectTransform CraftingRoot => _craftingRoot;
        private RectTransform _recipeListContainer;
        private RectTransform _ingredientsContainer;
        private ScrollRect _recipeScrollRect;
        private ScrollRect _ingredientsScrollRect;
        private RectTransform _recipeListViewport;
        private RectTransform _ingredientsViewport;
        private readonly Dictionary<string, Sprite> _generatedIconSprites = new();

        // Elementos de Detalhes
        private TextMeshProUGUI _headerTitle;
        private Image _detailIcon;
        private TextMeshProUGUI _detailTitle;
        private TextMeshProUGUI _detailCategory;
        private TextMeshProUGUI _detailStats;
        private TextMeshProUGUI _detailDescription;
        private TextMeshProUGUI _statusLabel;
        private Button _craftButton;
        private TextMeshProUGUI _craftButtonLabel;

        // Categoria Tabs
        private RectTransform _tabsContainer;
        private string _activeCategory = "Todos";
        private readonly string[] _categories = { "Todos", "Ferramentas", "Armas", "Armadura", "Acessórios", "Consumíveis", "Materiais" };
        private readonly List<GameObject> _tabViews = new();

        private RecipeDiscoveryTracker _discoveryTracker;

        // Lista de Receitas e Seleção
        private readonly List<CraftingRecipe> _recipes = new();
        private readonly List<CraftingRecipe> _filteredRecipes = new();
        private CraftingRecipe _selectedRecipe;
        private readonly List<GameObject> _recipeEntryViews = new();
        private readonly List<GameObject> _ingredientViews = new();

        // Integrações com Jogador e Inventário
        private ResourceInventory _playerResources;
        private InventoryUIManager _inventoryUIManager;
        private InventoryInstaller _inventoryInstaller;
        private ActionBarInstaller _actionBarInstaller;
        private Vector2 _originalInventoryPos = new Vector2(0, 25);
        private bool _hasOriginalInventoryPos;
        private bool _inventoryOpenedByCrafting;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            EnsureInstance();
        }

        public static CraftingUIManager EnsureInstance()
        {
            if (Instance != null) return Instance;

            var existing = FindAnyObjectByType<CraftingUIManager>();
            if (existing != null)
            {
                Instance = existing;
                return Instance;
            }

            var go = new GameObject("CraftingUIManager");
            Instance = go.AddComponent<CraftingUIManager>();
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

            LoadAudioClips();
        }

        private void Start()
        {
            LoadSprites();
            LoadDefaultRecipes();
            TryFindIntegrations();
        }

        private void Update()
        {
            if (!IsOpen) return;

            // Fecha se pressionar ESC
            if (IsEscapePressed())
            {
                Close();
                return;
            }

            HandleNavigationInput();
            HandleCraftingWheelInput();

            // Fecha automaticamente se o jogador se afastar da bancada
            if (CurrentWorkbench != null && !IsLocalPlayerNearWorkbench(4.0f))
            {
                Close();
                return;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_playerResources != null)
                _playerResources.ResourceChanged -= OnResourceChanged;
        }

        // ── Integrações e Cache ────────────────────────────────────────────────

        private void TryFindIntegrations()
        {
            if (_inventoryUIManager == null)
                _inventoryUIManager = FindAnyObjectByType<InventoryUIManager>();

            if (_inventoryInstaller == null)
                _inventoryInstaller = FindAnyObjectByType<InventoryInstaller>();

            if (_actionBarInstaller == null)
                _actionBarInstaller = FindAnyObjectByType<ActionBarInstaller>();

            if (_canvas == null)
            {
                if (_inventoryInstaller != null && _inventoryInstaller.GetComponentInParent<Canvas>() != null)
                {
                    _canvas = _inventoryInstaller.GetComponentInParent<Canvas>();
                }
                else
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

            if (_canvas != null && _canvas.renderMode != RenderMode.WorldSpace)
            {
                var scaler = _canvas.GetComponent<CanvasScaler>();
                if (scaler == null)
                    scaler = _canvas.gameObject.AddComponent<CanvasScaler>();

                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(800f, 600f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0f;
            }

            // Cache do jogador local
            if (_playerResources == null)
            {
                foreach (var combat in FindObjectsByType<PlayerCombat>())
                {
                    if (!combat.IsOwner) continue;
                    _playerResources = combat.GetComponent<ResourceInventory>();
                    if (_playerResources != null)
                    {
                        _playerResources.ResourceChanged -= OnResourceChanged;
                        _playerResources.ResourceChanged += OnResourceChanged;
                    }

                    if (_playerResources != null && _discoveryTracker == null)
                    {
                        _discoveryTracker = _playerResources.GetComponent<RecipeDiscoveryTracker>();
                        if (_discoveryTracker == null)
                            _discoveryTracker = _playerResources.gameObject.AddComponent<RecipeDiscoveryTracker>();
                    }

                    break;
                }
            }
        }

        private void OnResourceChanged(string resourceId, int newTotal)
        {
            if (IsOpen)
            {
                RefreshRecipeListStates();
                RefreshDetailsView();
            }
        }

        private bool IsLocalPlayerNearWorkbench(float maxDistance)
        {
            if (CurrentWorkbench == null) return false;
            foreach (var combat in FindObjectsByType<PlayerCombat>())
            {
                if (combat.IsOwner)
                    return CurrentWorkbench.IsInRange(combat.transform.position, maxDistance);
            }
            return false;
        }

        // ── Abertura e Fechamento ─────────────────────────────────────────────

        public void Toggle(Workbench workbench)
        {
            if (IsOpen && CurrentWorkbench == workbench)
                Close();
            else
                Open(workbench);
        }

        public void Open(Workbench workbench)
        {
            if (workbench == null) return;
            TryFindIntegrations();

            CurrentWorkbench = workbench;
            CurrentWorkbench.OpenForLocalPlayer();

            // Carrega receitas padrão do banco de dados (Resources/Crafting)
            _recipes.Clear();
            LoadDefaultRecipes();

            // Adiciona receitas específicas da bancada (se configuradas)
            if (workbench.Recipes != null)
            {
                foreach (var r in workbench.Recipes)
                {
                    if (r != null && !_recipes.Contains(r))
                        _recipes.Add(r);
                }
            }

            EnsureUIHierarchy();

            if (_headerTitle != null && CurrentWorkbench != null)
                _headerTitle.text = CurrentWorkbench.StationDisplayName.ToUpper();

            if (_craftingRoot != null)
                _craftingRoot.gameObject.SetActive(true);

            // Ajusta posição lado a lado com o inventário
            AdjustDualPanelPositions(true);

            // Seleciona a primeira receita por padrão
            if (_recipes.Count > 0)
                SelectRecipe(_recipes[0]);

            RefreshRecipeList();

            IsOpen = true;
            PlaySound(openSound);

            // Bloqueia rotação da câmera e libera cursor
            PlayerCameraController.LocalInstance?.SetRotationLocked(true);
        }

        public void Close()
        {
            if (!IsOpen) return;

            IsOpen = false;
            LastClosedFrame = Time.frameCount;

            if (CurrentWorkbench != null)
            {
                CurrentWorkbench.CloseForLocalPlayer();
                CurrentWorkbench = null;
            }

            if (_craftingRoot != null)
                _craftingRoot.gameObject.SetActive(false);

            // Restaura posição do inventário
            AdjustDualPanelPositions(false);

            // Se o inventário foi aberto apenas pela bancada, fecha-o
            if (_inventoryOpenedByCrafting && _inventoryUIManager != null && _inventoryUIManager.IsOpen)
            {
                _inventoryUIManager.Close();
            }
            _inventoryOpenedByCrafting = false;

            // Restaura rotação da câmera e trava cursor (caso inventário também esteja fechado)
            bool inventoryStillOpen = _inventoryUIManager != null && _inventoryUIManager.IsOpen;
            if (!inventoryStillOpen)
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

        private void AdjustDualPanelPositions(bool opening)
        {
            if (_inventoryUIManager == null) return;

            var invRect = _inventoryUIManager.InventoryFrameRect;
            if (invRect != null)
            {
                if (!_hasOriginalInventoryPos)
                {
                    _originalInventoryPos = invRect.anchoredPosition;
                    _hasOriginalInventoryPos = true;
                }

                if (opening)
                {
                    // Se o inventário estava fechado, abre-o
                    if (!_inventoryUIManager.IsOpen)
                    {
                        _inventoryOpenedByCrafting = true;
                        _inventoryUIManager.Open();
                    }

                    // Posiciona painéis lado a lado
                    invRect.anchoredPosition = new Vector2(235f, _originalInventoryPos.y);
                    if (_craftingRoot != null)
                        _craftingRoot.anchoredPosition = new Vector2(CraftingPanelPairedX, _originalInventoryPos.y);
                }
                else
                {
                    // Retorna o inventário para a posição centralizada
                    invRect.anchoredPosition = _originalInventoryPos;
                }
            }
        }

        // ── Seleção e Execução de Crafting ────────────────────────────────────

        public void SelectRecipe(CraftingRecipe recipe)
        {
            if (recipe == null) return;
            _selectedRecipe = recipe;
            PlaySound(clickSound);
            RefreshRecipeListStates();
            RefreshDetailsView();
        }

        public void CraftSelectedRecipe()
        {
            if (_selectedRecipe == null) return;
            TryFindIntegrations();
            if (CurrentWorkbench == null || !IsLocalPlayerNearWorkbench(4f) || _selectedRecipe.RequiredStation != CurrentWorkbench.StationType)
            { ShowStatusFeedback("Esta receita requer a estação correta e próxima.", true); return; }
            var discovery = _playerResources != null ? _playerResources.GetComponent<RecipeDiscoveryTracker>() : null;
            if (discovery == null || !discovery.IsDiscovered(_selectedRecipe))
            { ShowStatusFeedback("Descubra um ingrediente para desbloquear esta receita.", true); return; }
            if (_selectedRecipe.ProcessingSeconds > 0)
            {
                var building = CurrentWorkbench.GetComponent<Duskborn.Gameplay.Building.PlacedBuilding>();
                if (building == null) { ShowStatusFeedback("Construa uma estação [B] para processar este material.", true); return; }
                if (building.Definition.station == CraftingStationType.Forja)
                {
                    var controller = Duskborn.Gameplay.Building.BuildingController.Local;
                    if (controller == null) { ShowStatusFeedback("Forja indisponível.", true); return; }
                    var recipe = _selectedRecipe;
                    Close();
                    controller.OpenStation(building, recipe);
                    return;
                }
                Duskborn.Gameplay.Building.BuildingController.Local?.Queue(_selectedRecipe, building);
                ShowStatusFeedback("Processamento solicitado. Retire a produção na estação.", false);
                return;
            }

            if (_playerResources == null)
            {
                ShowStatusFeedback("Inventário de recursos não encontrado!", true);
                PlaySound(errorSound);
                return;
            }

            if (!_selectedRecipe.CanCraft(_playerResources))
            {
                ShowStatusFeedback("Recursos insuficientes!", true);
                PlaySound(errorSound);
                return;
            }

            if (!(_selectedRecipe.OutputItem is InventorySystem.Data.MaterialDefinition) && !HasFreeInventorySlot())
            {
                ShowStatusFeedback("Inventário cheio! Libere um espaço.", true);
                PlaySound(errorSound);
                return;
            }

            // Gasta os recursos
            if (!_selectedRecipe.TrySpendIngredients(_playerResources))
            {
                ShowStatusFeedback("Erro ao consumir recursos.", true);
                PlaySound(errorSound);
                return;
            }

            if (_selectedRecipe.OutputItem is InventorySystem.Data.MaterialDefinition material)
            {
                _playerResources.Add(material.Id, _selectedRecipe.OutputAmount);
                RefreshRecipeListStates(); RefreshDetailsView();
                ShowStatusFeedback("Material fabricado!", false);
                return;
            }
            // Cria e registra o item
            var outputItem = _selectedRecipe.CreateOutputItem();
            if (outputItem != null)
            {
                // Registra os ícones para que apareçam perfeitamente no inventário e na action bar
                if (_selectedRecipe.OutputItem != null && _selectedRecipe.OutputItem.Icon != null)
                {
                    _inventoryInstaller?.RegisterIcon(_selectedRecipe.OutputItem.Id, _selectedRecipe.OutputItem.Icon);
                    _actionBarInstaller?.RegisterIcon(_selectedRecipe.OutputItem.Id, _selectedRecipe.OutputItem.Icon);
                }

                // Insere no inventário do jogador
                if (_inventoryInstaller != null && _inventoryInstaller.Inventory != null)
                {
                    _inventoryInstaller.Inventory.TryAddItem(outputItem, out _);
                }
            }

            PlaySound(craftSound);
            ShowStatusFeedback($"<b>{_selectedRecipe.RecipeName}</b> fabricado com sucesso!", false);
            DuskLog.Log(LogChannel.Inventory, $"Crafted '{_selectedRecipe.RecipeName}' at Workbench.");

            RefreshRecipeListStates();
            RefreshDetailsView();
        }

        private bool HasFreeInventorySlot()
        {
            if (_inventoryInstaller?.Inventory == null) return true;
            foreach (var slot in _inventoryInstaller.Inventory.GetSlots())
            {
                if (slot.IsEmpty) return true;
            }
            return false;
        }

        private void ShowStatusFeedback(string message, bool isError)
        {
            if (_statusLabel == null) return;
            string colorHex = isError ? "#f87171" : "#4ade80";
            _statusLabel.text = $"<color={colorHex}>{message}</color>";
        }

        // ── Atualização Visual dos Painéis ─────────────────────────────────────

        private void SetActiveCategory(string category)
        {
            _activeCategory = category;
            PlaySound(clickSound);
            RefreshTabStates();
            RefreshRecipeList();
            RefreshDetailsView();
        }

        private void RefreshTabStates()
        {
            for (int i = 0; i < _tabViews.Count && i < _categories.Length; i++)
            {
                var img = _tabViews[i].GetComponent<Image>();
                if (img != null)
                    img.color = _categories[i] == _activeCategory
                        ? new Color(0.48f, 0.30f, 0.12f, 1f)
                        : new Color(0.10f, 0.13f, 0.18f, 0.96f);

                var outline = _tabViews[i].GetComponent<Outline>();
                if (outline != null)
                    outline.enabled = _categories[i] == _activeCategory;
            }
        }

        private void RefreshRecipeList()
        {
            if (_recipeListContainer == null) return;

            foreach (var go in _recipeEntryViews)
                Destroy(go);
            _recipeEntryViews.Clear();
            _filteredRecipes.Clear();

            foreach (var recipe in _recipes)
            {
                if (recipe == null) continue;

                // Filter every station by its actual capability.
                if (CurrentWorkbench != null && recipe.RequiredStation != CurrentWorkbench.StationType)
                    continue;

                // Filtragem por categoria selecionada
                if (_activeCategory != "Todos" && recipe.Category != _activeCategory)
                    continue;

                _filteredRecipes.Add(recipe);
                var itemGO = CreateRecipeEntryView(recipe, _recipeListContainer, false);
                _recipeEntryViews.Add(itemGO);
            }

            if (_filteredRecipes.Count == 0) _selectedRecipe = null;
            if (_filteredRecipes.Count > 0 && !_filteredRecipes.Contains(_selectedRecipe))
                _selectedRecipe = _filteredRecipes[0];

            RefreshRecipeListStates();
            ResetScroll(_recipeScrollRect);
        }

        private void HandleNavigationInput()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.leftArrowKey.wasPressedThisFrame)
                SelectAdjacentCategory(-1);
            else if (keyboard.rightArrowKey.wasPressedThisFrame)
                SelectAdjacentCategory(1);
            else if (keyboard.upArrowKey.wasPressedThisFrame)
                SelectAdjacentRecipe(-1);
            else if (keyboard.downArrowKey.wasPressedThisFrame)
                SelectAdjacentRecipe(1);
            else if (keyboard.enterKey.wasPressedThisFrame && _craftButton != null && _craftButton.interactable)
                CraftSelectedRecipe();
#endif
        }

        private void HandleCraftingWheelInput()
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse == null) return;

            float wheelDelta = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(wheelDelta) < 0.01f) return;

            Vector2 pointerPosition = mouse.position.ReadValue();
            Camera eventCamera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
            const float wheelStep = 0.085f;

            if (IsPointerInside(_recipeListViewport, pointerPosition, eventCamera))
                ScrollTo(_recipeScrollRect, wheelDelta * wheelStep);
            else if (IsPointerInside(_ingredientsViewport, pointerPosition, eventCamera))
                ScrollTo(_ingredientsScrollRect, wheelDelta * wheelStep);
#endif
        }

        private static bool IsPointerInside(RectTransform rect, Vector2 pointerPosition, Camera eventCamera)
        {
            return rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, pointerPosition, eventCamera);
        }

        private static void ScrollTo(ScrollRect scrollRect, float delta)
        {
            if (scrollRect == null || !scrollRect.gameObject.activeInHierarchy) return;
            Canvas.ForceUpdateCanvases();
            scrollRect.StopMovement();
            scrollRect.verticalNormalizedPosition = Mathf.Clamp01(scrollRect.verticalNormalizedPosition + delta);
        }

        private void SelectAdjacentCategory(int direction)
        {
            int currentIndex = System.Array.IndexOf(_categories, _activeCategory);
            int nextIndex = (currentIndex + direction + _categories.Length) % _categories.Length;
            SetActiveCategory(_categories[nextIndex]);
        }

        private void SelectAdjacentRecipe(int direction)
        {
            if (_filteredRecipes.Count == 0) return;
            int currentIndex = _filteredRecipes.IndexOf(_selectedRecipe);
            int nextIndex = Mathf.Clamp(currentIndex + direction, 0, _filteredRecipes.Count - 1);
            if (nextIndex != currentIndex)
                SelectRecipe(_filteredRecipes[nextIndex]);
        }

        private void RefreshRecipeListStates()
        {
            for (int i = 0; i < _recipeEntryViews.Count; i++)
            {
                if (i >= _filteredRecipes.Count) break;
                var view = _recipeEntryViews[i];
                var recipe = _filteredRecipes[i];
                if (view == null || recipe == null) continue;

                bool isSelected = recipe == _selectedRecipe;
                bool canCraft = _playerResources != null && recipe.CanCraft(_playerResources);

                var bg = view.GetComponent<Image>();
                if (bg != null)
                {
                    if (isSelected)
                        bg.color = new Color(0.22f, 0.30f, 0.42f, 1f); // Destaque azul/cinza
                    else
                        bg.color = new Color(0.12f, 0.14f, 0.18f, 0.95f);
                }

                // Borda de seleção
                var outline = view.GetComponent<Outline>();
                if (outline != null)
                {
                    outline.enabled = isSelected;
                    outline.effectColor = new Color(0.96f, 0.72f, 0.22f, 1f); // Ouro brilhante
                }

                // Indicador de craftabilidade (filho "Indicator")
                var indicator = view.transform.Find("Indicator")?.GetComponent<TextMeshProUGUI>();
                if (indicator != null)
                {
                    indicator.text = canCraft ? "<color=#4ade80>● Pronto</color>" : "<color=#64748b>Falta</color>";
                }
            }
        }

        private void RefreshDetailsView()
        {
            if (_selectedRecipe == null)
            {
                if (_detailTitle != null) _detailTitle.text = "Selecione uma receita";
                if (_detailStats != null) _detailStats.text = string.Empty;
                if (_detailDescription != null) _detailDescription.text = string.Empty;
                if (_craftButton != null) _craftButton.interactable = false;
                return;
            }

            // Título e Categoria
            if (_detailTitle != null)
                _detailTitle.text = _selectedRecipe.RecipeName;

            if (_detailCategory != null)
            {
                string tierStr = _selectedRecipe.Tier switch
                {
                    CraftingTier.Primitivo => "Tier 1",
                    CraftingTier.Ferro => "Tier 2",
                    CraftingTier.Reforcado => "Tier 3",
                    CraftingTier.Espinheiro => "Tier 4",
                    _ => ""
                };
                string stationStr = _selectedRecipe.RequiredStation switch
                {
                    CraftingStationType.Forja => "Forja",
                    CraftingStationType.Caldeirao => "Caldeirão",
                    CraftingStationType.MesaArcana => "Mesa Arcana",
                    _ => "Bancada"
                };
                _detailCategory.text = $"{_selectedRecipe.Category} • {tierStr} • {stationStr}";
            }

            // Ícone grande
            if (_detailIcon != null)
            {
                _detailIcon.sprite = GetRecipeSprite(_selectedRecipe);
                _detailIcon.color = Color.white;
            }

            // Estatísticas e Descrição
            if (_detailStats != null)
            {
                _detailStats.text = BuildStatsString(_selectedRecipe);
            }

            if (_detailDescription != null)
            {
                _detailDescription.text = _selectedRecipe.Description;
            }

            // Lista de Ingredientes Requeridos
            RefreshIngredientsList(_selectedRecipe);

            // Botão Fabricar e Status
            bool canCraft = _playerResources != null && _selectedRecipe.CanCraft(_playerResources);
            bool discovered = _discoveryTracker != null && _discoveryTracker.IsDiscovered(_selectedRecipe);
            bool hasProcessor = _selectedRecipe.ProcessingSeconds <= 0 || (CurrentWorkbench != null && CurrentWorkbench.GetComponent<Duskborn.Gameplay.Building.PlacedBuilding>() != null);
            canCraft = canCraft && discovered && hasProcessor;
            bool hasSpace = _selectedRecipe.OutputItem is InventorySystem.Data.MaterialDefinition || HasFreeInventorySlot();

            if (_craftButton != null)
                _craftButton.interactable = canCraft && hasSpace;

            if (!discovered) ShowStatusFeedback("Descubra os ingredientes desta receita.", true);
            else if (!hasProcessor) ShowStatusFeedback("Construa esta estação [B] para processar materiais.", true);
            else if (!canCraft) ShowStatusFeedback("Recursos insuficientes", true);
            else if (!hasSpace)
                ShowStatusFeedback("Inventário cheio", true);
            else
                ShowStatusFeedback("Pronto para fabricar!", false);
        }

        private string BuildStatsString(CraftingRecipe recipe)
        {
            if (recipe.OutputItem is ConsumableDefinition consumable)
            {
                return $"<color=#67e8f9><b>Efeito:</b> {consumable.EffectDescription}</color>\n<color=#94a3b8>Pilha máx: {consumable.MaxStack}</color>";
            }

            if (recipe.OutputItem is GearDefinition gear)
            {
                var lines = new List<string>();
                foreach (var b in gear.Bonuses)
                {
                    string sign = b.Value >= 0 ? "+" : "";
                    string colorHex = b.Value >= 0 ? "#86efac" : "#f87171";
                    lines.Add($"<color={colorHex}>{sign}{b.Value * 100:F0}% {b.Type}</color>");
                }
                return string.Join("\n", lines);
            }

            if (recipe.OutputItem is WeaponDefinition weapon)
            {
                var lines = new List<string>();

                foreach (var b in weapon.Bonuses)
                {
                    string sign = b.Value >= 0 ? "+" : "";
                    string colorHex = b.Value >= 0 ? "#86efac" : "#f87171";
                    lines.Add($"<color={colorHex}>{sign}{b.Value * 100:F0}% {b.Type}</color>");
                }

                foreach (var mod in weapon.TypeModifiers)
                {
                    string sign = mod.Bonus >= 0 ? "+" : "";
                    string targetName = mod.Type.ToString();
                    if (mod.Type == Duskborn.Gameplay.TargetType.Tree) targetName = "Árvores (Madeira)";
                    else if (mod.Type == Duskborn.Gameplay.TargetType.MiningNode || (int)mod.Type == 512) targetName = "Rochas / Minérios";
                    else if (mod.Type == Duskborn.Gameplay.TargetType.Humanoid) targetName = "Humanoides";

                    lines.Add($"<color=#fde047>{sign}{mod.Bonus * 100:F0}% Dano vs {targetName}</color>");
                }

                return string.Join("\n", lines);
            }

            return "<color=#94a3b8>Item padrão</color>";
        }

        private void RefreshIngredientsList(CraftingRecipe recipe)
        {
            if (_ingredientsContainer == null) return;

            foreach (var go in _ingredientViews)
                Destroy(go);
            _ingredientViews.Clear();

            foreach (var ing in recipe.Ingredients)
            {
                if (ing.material == null) continue;
                int current = _playerResources != null ? _playerResources.GetCount(ing.material.Id) : 0;
                var view = CreateIngredientRowView(ing.material, current, ing.amount, _ingredientsContainer);
                _ingredientViews.Add(view);
            }
            foreach (var fuel in recipe.FuelIngredients)
            {
                if (fuel.material == null) continue;
                int current = _playerResources != null ? _playerResources.GetCount(fuel.material.Id) : 0;
                var view = CreateIngredientRowView(fuel.material, current, fuel.amount, _ingredientsContainer);
                _ingredientViews.Add(view);
            }

            ResetScroll(_ingredientsScrollRect);
        }

        private static void ResetScroll(ScrollRect scrollRect)
        {
            if (scrollRect == null) return;
            Canvas.ForceUpdateCanvases();
            scrollRect.StopMovement();
            scrollRect.verticalNormalizedPosition = 1f;
        }

        // ── Construção Dinâmica da Hierarquia UI ───────────────────────────────

        public void EnsureUIHierarchy()
        {
            if (_craftingRoot != null) return;

            if (_canvas == null)
            {
                TryFindIntegrations();
                if (_canvas == null)
                {
                    DuskLog.Error(LogChannel.Inventory, "CraftingUIManager: Nenhum Canvas encontrado para renderizar a UI.");
                    return;
                }
            }

            // 1. Painel Principal (Moldura de Pedra/Ferro Medieval)
            var frameGO = new GameObject("CraftingFrame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            frameGO.transform.SetParent(_canvas.transform, false);

            _craftingRoot = frameGO.GetComponent<RectTransform>();
            _craftingRoot.anchorMin = new Vector2(0.5f, 0.5f);
            _craftingRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _craftingRoot.pivot = new Vector2(0.5f, 0.5f);
            _craftingRoot.sizeDelta = new Vector2(460f, 420f);
            _craftingRoot.anchoredPosition = new Vector2(CraftingPanelPairedX, 0f);
            _craftingRoot.localScale = Vector3.one * CraftingPanelScale;

            // A full opaque backing prevents transparent frame sprites from exposing the world
            // through the middle of the crafting screen.
            var backdrop = CreatePanel("Backdrop", frameGO.transform, new Color(0.025f, 0.035f, 0.055f, 0.985f));
            backdrop.anchorMin = Vector2.zero;
            backdrop.anchorMax = Vector2.one;
            backdrop.sizeDelta = Vector2.zero;
            backdrop.SetSiblingIndex(0);
            backdrop.GetComponent<Image>().raycastTarget = false;
            AddFrameOutline(backdrop.gameObject, new Color(0.72f, 0.47f, 0.18f, 0.9f), new Vector2(2f, -2f));

            var frameImg = frameGO.GetComponent<Image>();
            frameImg.raycastTarget = true;
            if (panelFrameSprite != null)
            {
                frameImg.sprite = panelFrameSprite;
                frameImg.type = Image.Type.Sliced;
                frameImg.color = new Color(0.95f, 0.95f, 0.95f, 1f);
            }
            else
            {
                frameImg.color = new Color(0.055f, 0.07f, 0.10f, 0.985f);
            }

            // Arraste pelo corpo da moldura
            var frameDrag = frameGO.AddComponent<DraggablePanel>();
            frameDrag.TargetPanel = _craftingRoot;

            // 2. Barra de Cabeçalho / Título (Área dedicada para arrastar a janela)
            var headerGO = new GameObject("HeaderBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            headerGO.transform.SetParent(frameGO.transform, false);

            var headerRect = headerGO.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0, 1);
            headerRect.anchorMax = new Vector2(1, 1);
            headerRect.pivot = new Vector2(0.5f, 1);
            headerRect.anchoredPosition = Vector2.zero;
            headerRect.sizeDelta = new Vector2(0, 42);

            var headerImg = headerGO.GetComponent<Image>();
            headerImg.color = new Color(0.12f, 0.075f, 0.035f, 0.98f);
            headerImg.raycastTarget = true;
            AddFrameOutline(headerGO, new Color(0.84f, 0.55f, 0.20f, 0.55f), new Vector2(1f, -1f));

            var headerDrag = headerGO.AddComponent<DraggablePanel>();
            headerDrag.TargetPanel = _craftingRoot;

            // Título dentro do Cabeçalho
            var titleGO = CreateText("Title", headerGO.transform, "BANCADA DE TRABALHO", 13, FontStyles.Bold, new Color(0.98f, 0.82f, 0.38f, 1f), TextAlignmentOptions.Left);
            _headerTitle = titleGO.GetComponent<TextMeshProUGUI>();
            var titleRect = titleGO.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 0.40f);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.pivot = new Vector2(0, 0.5f);
            titleRect.anchoredPosition = new Vector2(18, -1);
            titleRect.sizeDelta = new Vector2(-60, 0);

            var subtitleGO = CreateText("Subtitle", headerGO.transform, "CATÁLOGO DE FABRICAÇÃO", 7.5f, FontStyles.Bold,
                new Color(0.74f, 0.66f, 0.52f, 1f), TextAlignmentOptions.Left);
            var subtitleRect = subtitleGO.GetComponent<RectTransform>();
            subtitleRect.anchorMin = new Vector2(0, 0);
            subtitleRect.anchorMax = new Vector2(1, 0.45f);
            subtitleRect.pivot = new Vector2(0, 0);
            subtitleRect.anchoredPosition = new Vector2(18, 4);
            subtitleRect.sizeDelta = new Vector2(-66, 12);

            // Botão Fechar (X) dentro do Cabeçalho
            var closeBtnGO = CreateButton("CloseButton", headerGO.transform, "X", new Vector2(24, 24), new Color(0.7f, 0.2f, 0.2f, 1f));
            var closeRect = closeBtnGO.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1, 0.5f);
            closeRect.anchorMax = new Vector2(1, 0.5f);
            closeRect.pivot = new Vector2(1, 0.5f);
            closeRect.anchoredPosition = new Vector2(-12, 0);
            closeBtnGO.GetComponent<Button>().onClick.AddListener(() => { PlaySound(clickSound); Close(); });

            // Divisória do Cabeçalho
            var headerDiv = CreatePanel("HeaderDivider", frameGO.transform, new Color(0.24f, 0.28f, 0.36f, 0.8f));
            headerDiv.anchorMin = new Vector2(0, 1);
            headerDiv.anchorMax = new Vector2(1, 1);
            headerDiv.pivot = new Vector2(0.5f, 1);
            headerDiv.sizeDelta = new Vector2(-28, 2);
            headerDiv.anchoredPosition = new Vector2(0, -42);

            // ── Barra de Categorias ──────────────────────────────────────────
            var tabBarGO = new GameObject("TabBar", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
            tabBarGO.transform.SetParent(frameGO.transform, false);
            _tabsContainer = tabBarGO.GetComponent<RectTransform>();
            _tabsContainer.anchorMin = new Vector2(0, 1);
            _tabsContainer.anchorMax = new Vector2(1, 1);
            _tabsContainer.pivot = new Vector2(0.5f, 1);
            _tabsContainer.anchoredPosition = new Vector2(0, -45);
            _tabsContainer.sizeDelta = new Vector2(-28, 25);
            tabBarGO.GetComponent<Image>().color = new Color(0.035f, 0.05f, 0.075f, 0.88f);

            var tabHlg = tabBarGO.GetComponent<HorizontalLayoutGroup>();
            tabHlg.spacing = 3f;
            tabHlg.childAlignment = TextAnchor.MiddleCenter;
            tabHlg.childControlWidth = true;
            tabHlg.childControlHeight = true;
            tabHlg.childForceExpandWidth = true;
            tabHlg.childForceExpandHeight = true;

            foreach (var cat in _categories)
            {
                var tabGO = CreateButton($"Tab_{cat}", tabBarGO.transform, cat, new Vector2(60, 23),
                    new Color(0.15f, 0.17f, 0.22f, 0.9f));
                var tabLabel = tabGO.GetComponentInChildren<TextMeshProUGUI>();
                if (tabLabel != null)
                {
                    tabLabel.fontSize = 8.2f;
                    tabLabel.textWrappingMode = TextWrappingModes.NoWrap;
                    tabLabel.overflowMode = TextOverflowModes.Ellipsis;
                }
                var tabOutline = tabGO.AddComponent<Outline>();
                tabOutline.effectColor = new Color(0.94f, 0.66f, 0.26f, 0.9f);
                tabOutline.effectDistance = new Vector2(1f, -1f);
                tabOutline.enabled = cat == _activeCategory;
                string captured = cat;
                tabGO.GetComponent<Button>().onClick.AddListener(() => SetActiveCategory(captured));
                _tabViews.Add(tabGO);
            }

            // 3. Coluna Esquerda: Lista de Receitas
            var leftCol = CreatePanel("LeftColumn", frameGO.transform, new Color(0.08f, 0.09f, 0.12f, 0.7f));
            leftCol.anchorMin = new Vector2(0, 0);
            leftCol.anchorMax = new Vector2(0, 1);
            leftCol.pivot = new Vector2(0, 0.5f);
            leftCol.sizeDelta = new Vector2(180f, -86f);
            leftCol.anchoredPosition = new Vector2(14f, -34f);
            AddFrameOutline(leftCol.gameObject, new Color(0.24f, 0.30f, 0.40f, 0.85f), new Vector2(1f, -1f));

            var recipesTitle = CreateText("RecipesHeader", leftCol.transform, "RECEITAS", 10, FontStyles.Bold, new Color(0.58f, 0.64f, 0.72f, 1f), TextAlignmentOptions.Left);
            var rTitleRect = recipesTitle.GetComponent<RectTransform>();
            rTitleRect.anchorMin = new Vector2(0, 1);
            rTitleRect.anchorMax = new Vector2(1, 1);
            rTitleRect.pivot = new Vector2(0, 1);
            rTitleRect.anchoredPosition = new Vector2(8, -6);
            rTitleRect.sizeDelta = new Vector2(-16, 18);

            var listScrollGO = new GameObject("RecipeListScroll", typeof(RectTransform), typeof(ScrollRect));
            listScrollGO.transform.SetParent(leftCol.transform, false);
            var scrollRectTransform = listScrollGO.GetComponent<RectTransform>();
            scrollRectTransform.anchorMin = Vector2.zero;
            scrollRectTransform.anchorMax = Vector2.one;
            scrollRectTransform.offsetMin = new Vector2(5, 8);
            scrollRectTransform.offsetMax = new Vector2(-10, -34);

            var viewportGO = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewportGO.transform.SetParent(listScrollGO.transform, false);
            var viewportRect = viewportGO.GetComponent<RectTransform>();
            viewportRect.anchorMin = new Vector2(0, 0);
            viewportRect.anchorMax = new Vector2(1, 1);
            viewportRect.sizeDelta = new Vector2(-10, 0); // Leave space for scrollbar on the right
            viewportRect.anchoredPosition = new Vector2(-5, 0);
            _recipeListViewport = viewportRect;

            var viewportImg = viewportGO.GetComponent<Image>();
            viewportImg.color = new Color(1, 1, 1, 0.01f);
            var mask = viewportGO.GetComponent<Mask>();
            mask.showMaskGraphic = false;

            var listContainerGO = new GameObject("RecipeList", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            listContainerGO.transform.SetParent(viewportGO.transform, false);
            _recipeListContainer = listContainerGO.GetComponent<RectTransform>();
            _recipeListContainer.anchorMin = new Vector2(0, 1);
            _recipeListContainer.anchorMax = new Vector2(1, 1);
            _recipeListContainer.pivot = new Vector2(0.5f, 1);
            _recipeListContainer.anchoredPosition = new Vector2(0, 0);
            _recipeListContainer.sizeDelta = new Vector2(0, 0);

            var vlg = listContainerGO.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 4f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var csf = listContainerGO.GetComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scrollRect = listScrollGO.GetComponent<ScrollRect>();
            scrollRect.content = _recipeListContainer;
            scrollRect.viewport = viewportRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            ConfigureScroll(scrollRect);

            var scrollbarGO = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            scrollbarGO.transform.SetParent(listScrollGO.transform, false);
            var scrollbarRect = scrollbarGO.GetComponent<RectTransform>();
            scrollbarRect.anchorMin = new Vector2(1, 0);
            scrollbarRect.anchorMax = new Vector2(1, 1);
            scrollbarRect.pivot = new Vector2(1, 0.5f);
            scrollbarRect.sizeDelta = new Vector2(8, 0);
            scrollbarRect.anchoredPosition = new Vector2(0, 0);

            var sbImg = scrollbarGO.GetComponent<Image>();
            sbImg.color = new Color(0.05f, 0.06f, 0.08f, 0.8f);

            var slidingAreaGO = new GameObject("SlidingArea", typeof(RectTransform));
            slidingAreaGO.transform.SetParent(scrollbarGO.transform, false);
            var slidingAreaRect = slidingAreaGO.GetComponent<RectTransform>();
            slidingAreaRect.anchorMin = Vector2.zero;
            slidingAreaRect.anchorMax = Vector2.one;
            slidingAreaRect.sizeDelta = Vector2.zero;

            var handleGO = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handleGO.transform.SetParent(slidingAreaGO.transform, false);
            var handleRect = handleGO.GetComponent<RectTransform>();
            handleRect.sizeDelta = Vector2.zero;
            var handleImg = handleGO.GetComponent<Image>();
            handleImg.color = new Color(0.3f, 0.35f, 0.45f, 1f);

            var scrollbar = scrollbarGO.GetComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.targetGraphic = handleImg;
            scrollbar.handleRect = handleRect;

            scrollRect.verticalScrollbar = scrollbar;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            _recipeScrollRect = scrollRect;

            // 4. Coluna Direita: Detalhes da Receita Selecionada
            var rightCol = CreatePanel("RightColumn", frameGO.transform, new Color(0.08f, 0.09f, 0.12f, 0.7f));
            rightCol.anchorMin = new Vector2(1, 0);
            rightCol.anchorMax = new Vector2(1, 1);
            rightCol.pivot = new Vector2(1, 0.5f);
            rightCol.sizeDelta = new Vector2(250f, -86f);
            rightCol.anchoredPosition = new Vector2(-14f, -34f);
            AddFrameOutline(rightCol.gameObject, new Color(0.24f, 0.30f, 0.40f, 0.85f), new Vector2(1f, -1f));

            var detailsTitle = CreateText("DetailsHeader", rightCol.transform, "DETALHES", 10, FontStyles.Bold, new Color(0.58f, 0.64f, 0.72f, 1f), TextAlignmentOptions.Left);
            var dTitleRect = detailsTitle.GetComponent<RectTransform>();
            dTitleRect.anchorMin = new Vector2(0, 1);
            dTitleRect.anchorMax = new Vector2(1, 1);
            dTitleRect.pivot = new Vector2(0, 1);
            dTitleRect.anchoredPosition = new Vector2(8, -6);
            dTitleRect.sizeDelta = new Vector2(-16, 18);

            // Caixa de Preview (Ícone + Título + Categoria)
            var previewBox = CreatePanel("PreviewBox", rightCol.transform, new Color(0.12f, 0.14f, 0.18f, 0.85f));
            previewBox.anchorMin = new Vector2(0, 1);
            previewBox.anchorMax = new Vector2(1, 1);
            previewBox.pivot = new Vector2(0.5f, 1);
            previewBox.anchoredPosition = new Vector2(0, -24);
            previewBox.sizeDelta = new Vector2(-14, 48);
            AddFrameOutline(previewBox.gameObject, new Color(0.34f, 0.42f, 0.55f, 0.65f), new Vector2(1f, -1f));

            // Slot do Ícone Grande
            var iconSlot = CreatePanel("IconSlot", previewBox.transform, new Color(0.06f, 0.07f, 0.09f, 1f));
            iconSlot.anchorMin = new Vector2(0, 0.5f);
            iconSlot.anchorMax = new Vector2(0, 0.5f);
            iconSlot.pivot = new Vector2(0, 0.5f);
            iconSlot.anchoredPosition = new Vector2(6, 0);
            iconSlot.sizeDelta = new Vector2(38, 38);
            if (slotFrameSprite != null)
            {
                var sImg = iconSlot.GetComponent<Image>();
                sImg.sprite = slotFrameSprite;
                sImg.type = Image.Type.Sliced;
            }

            var iconInnerGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconInnerGO.transform.SetParent(iconSlot.transform, false);
            var iconInnerRect = iconInnerGO.GetComponent<RectTransform>();
            iconInnerRect.anchorMin = Vector2.zero;
            iconInnerRect.anchorMax = Vector2.one;
            iconInnerRect.sizeDelta = new Vector2(-4, -4);
            _detailIcon = iconInnerGO.GetComponent<Image>();
            _detailIcon.preserveAspect = true;

            // Título do Item
            var itemTitleGO = CreateText("ItemTitle", previewBox.transform, "Machado de Pedra", 12, FontStyles.Bold, Color.white, TextAlignmentOptions.Left);
            var itemTitleRect = itemTitleGO.GetComponent<RectTransform>();
            itemTitleRect.anchorMin = new Vector2(0, 0.5f);
            itemTitleRect.anchorMax = new Vector2(1, 0.5f);
            itemTitleRect.pivot = new Vector2(0, 0);
            itemTitleRect.anchoredPosition = new Vector2(50, 2);
            itemTitleRect.sizeDelta = new Vector2(-54, 18);
            _detailTitle = itemTitleGO.GetComponent<TextMeshProUGUI>();
            _detailTitle.textWrappingMode = TextWrappingModes.NoWrap;
            _detailTitle.overflowMode = TextOverflowModes.Ellipsis;

            // Categoria do Item
            var itemCatGO = CreateText("ItemCategory", previewBox.transform, "Ferramenta • Nível 1", 9, FontStyles.Normal, new Color(0.58f, 0.64f, 0.72f, 1f), TextAlignmentOptions.Left);
            var itemCatRect = itemCatGO.GetComponent<RectTransform>();
            itemCatRect.anchorMin = new Vector2(0, 0.5f);
            itemCatRect.anchorMax = new Vector2(1, 0.5f);
            itemCatRect.pivot = new Vector2(0, 1);
            itemCatRect.anchoredPosition = new Vector2(50, -2);
            itemCatRect.sizeDelta = new Vector2(-54, 14);
            _detailCategory = itemCatGO.GetComponent<TextMeshProUGUI>();
            _detailCategory.textWrappingMode = TextWrappingModes.NoWrap;
            _detailCategory.overflowMode = TextOverflowModes.Ellipsis;

            // Caixa de Atributos & Bônus
            var statsBox = CreatePanel("StatsBox", rightCol.transform, new Color(0.05f, 0.06f, 0.08f, 0.9f));
            statsBox.anchorMin = new Vector2(0, 1);
            statsBox.anchorMax = new Vector2(1, 1);
            statsBox.pivot = new Vector2(0.5f, 1);
            statsBox.anchoredPosition = new Vector2(0, -76);
            statsBox.sizeDelta = new Vector2(-14, 60);
            AddFrameOutline(statsBox.gameObject, new Color(0.19f, 0.27f, 0.37f, 0.8f), new Vector2(1f, -1f));

            var statsTextGO = CreateText("StatsText", statsBox.transform, "+15% Dano\n+300% Dano vs Árvores", 9.5f, FontStyles.Normal, new Color(0.85f, 0.9f, 0.95f, 1f), TextAlignmentOptions.TopLeft);
            var statsTextRect = statsTextGO.GetComponent<RectTransform>();
            statsTextRect.anchorMin = Vector2.zero;
            statsTextRect.anchorMax = Vector2.one;
            statsTextRect.sizeDelta = new Vector2(-10, -10);
            statsTextRect.anchoredPosition = Vector2.zero;
            _detailStats = statsTextGO.GetComponent<TextMeshProUGUI>();
            _detailStats.overflowMode = TextOverflowModes.Ellipsis;

            // Descrição Flavour
            var descGO = CreateText("Description", rightCol.transform, "Descrição da ferramenta...", 9, FontStyles.Italic, new Color(0.55f, 0.60f, 0.68f, 1f), TextAlignmentOptions.TopLeft);
            var descRect = descGO.GetComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0, 1);
            descRect.anchorMax = new Vector2(1, 1);
            descRect.pivot = new Vector2(0.5f, 1);
            descRect.anchoredPosition = new Vector2(0, -140);
            descRect.sizeDelta = new Vector2(-14, 28);
            _detailDescription = descGO.GetComponent<TextMeshProUGUI>();
            _detailDescription.textWrappingMode = TextWrappingModes.Normal;
            _detailDescription.maxVisibleLines = 2;
            _detailDescription.overflowMode = TextOverflowModes.Ellipsis;

            // Seção de Ingredientes
            var reqHeader = CreateText("ReqHeader", rightCol.transform, "MATERIAIS NECESSÁRIOS", 9.5f, FontStyles.Bold, new Color(0.58f, 0.64f, 0.72f, 1f), TextAlignmentOptions.Left);
            var reqHeaderRect = reqHeader.GetComponent<RectTransform>();
            reqHeaderRect.anchorMin = new Vector2(0, 1);
            reqHeaderRect.anchorMax = new Vector2(1, 1);
            reqHeaderRect.pivot = new Vector2(0, 1);
            reqHeaderRect.anchoredPosition = new Vector2(8, -170);
            reqHeaderRect.sizeDelta = new Vector2(-16, 16);

            var ingScrollGO = new GameObject("IngredientsScroll", typeof(RectTransform), typeof(ScrollRect));
            ingScrollGO.transform.SetParent(rightCol.transform, false);
            var ingScrollRectTransform = ingScrollGO.GetComponent<RectTransform>();
            ingScrollRectTransform.anchorMin = new Vector2(0, 1);
            ingScrollRectTransform.anchorMax = new Vector2(1, 1);
            ingScrollRectTransform.pivot = new Vector2(0.5f, 1);
            ingScrollRectTransform.anchoredPosition = new Vector2(0, -188);
            ingScrollRectTransform.sizeDelta = new Vector2(-14, 52);

            var ingViewportGO = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            ingViewportGO.transform.SetParent(ingScrollGO.transform, false);
            var ingViewportRect = ingViewportGO.GetComponent<RectTransform>();
            ingViewportRect.anchorMin = new Vector2(0, 0);
            ingViewportRect.anchorMax = new Vector2(1, 1);
            ingViewportRect.sizeDelta = new Vector2(-10, 0); // Leave space for scrollbar on the right
            ingViewportRect.anchoredPosition = new Vector2(-5, 0);
            _ingredientsViewport = ingViewportRect;

            var ingViewportImg = ingViewportGO.GetComponent<Image>();
            ingViewportImg.color = new Color(1, 1, 1, 0.01f);
            var ingMask = ingViewportGO.GetComponent<Mask>();
            ingMask.showMaskGraphic = false;

            var ingContainerGO = new GameObject("IngredientsContainer", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            ingContainerGO.transform.SetParent(ingViewportGO.transform, false);
            _ingredientsContainer = ingContainerGO.GetComponent<RectTransform>();
            _ingredientsContainer.anchorMin = new Vector2(0, 1);
            _ingredientsContainer.anchorMax = new Vector2(1, 1);
            _ingredientsContainer.pivot = new Vector2(0.5f, 1);
            _ingredientsContainer.anchoredPosition = new Vector2(0, 0);
            _ingredientsContainer.sizeDelta = new Vector2(0, 0);

            var ingVlg = ingContainerGO.GetComponent<VerticalLayoutGroup>();
            ingVlg.spacing = 3f;
            ingVlg.childAlignment = TextAnchor.UpperCenter;
            ingVlg.childControlWidth = true;
            ingVlg.childControlHeight = false;
            ingVlg.childForceExpandWidth = true;
            ingVlg.childForceExpandHeight = false;

            var ingCsf = ingContainerGO.GetComponent<ContentSizeFitter>();
            ingCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var ingScrollRect = ingScrollGO.GetComponent<ScrollRect>();
            ingScrollRect.content = _ingredientsContainer;
            ingScrollRect.viewport = ingViewportRect;
            ingScrollRect.horizontal = false;
            ingScrollRect.vertical = true;
            ConfigureScroll(ingScrollRect);

            var ingScrollbarGO = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            ingScrollbarGO.transform.SetParent(ingScrollGO.transform, false);
            var ingScrollbarRect = ingScrollbarGO.GetComponent<RectTransform>();
            ingScrollbarRect.anchorMin = new Vector2(1, 0);
            ingScrollbarRect.anchorMax = new Vector2(1, 1);
            ingScrollbarRect.pivot = new Vector2(1, 0.5f);
            ingScrollbarRect.sizeDelta = new Vector2(8, 0);
            ingScrollbarRect.anchoredPosition = new Vector2(0, 0);

            var ingSbImg = ingScrollbarGO.GetComponent<Image>();
            ingSbImg.color = new Color(0.05f, 0.06f, 0.08f, 0.8f);

            var ingSlidingAreaGO = new GameObject("SlidingArea", typeof(RectTransform));
            ingSlidingAreaGO.transform.SetParent(ingScrollbarGO.transform, false);
            var ingSlidingAreaRect = ingSlidingAreaGO.GetComponent<RectTransform>();
            ingSlidingAreaRect.anchorMin = Vector2.zero;
            ingSlidingAreaRect.anchorMax = Vector2.one;
            ingSlidingAreaRect.sizeDelta = Vector2.zero;

            var ingHandleGO = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            ingHandleGO.transform.SetParent(ingSlidingAreaGO.transform, false);
            var ingHandleRect = ingHandleGO.GetComponent<RectTransform>();
            ingHandleRect.sizeDelta = Vector2.zero;
            var ingHandleImg = ingHandleGO.GetComponent<Image>();
            ingHandleImg.color = new Color(0.3f, 0.35f, 0.45f, 1f);

            var ingScrollbar = ingScrollbarGO.GetComponent<Scrollbar>();
            ingScrollbar.direction = Scrollbar.Direction.BottomToTop;
            ingScrollbar.targetGraphic = ingHandleImg;
            ingScrollbar.handleRect = ingHandleRect;

            ingScrollRect.verticalScrollbar = ingScrollbar;
            ingScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            _ingredientsScrollRect = ingScrollRect;

            // Status de Fabricação
            var statusGO = CreateText("StatusLabel", rightCol.transform, string.Empty, 9.5f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
            var statusRect = statusGO.GetComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0, 0);
            statusRect.anchorMax = new Vector2(1, 0);
            statusRect.pivot = new Vector2(0.5f, 0);
            statusRect.anchoredPosition = new Vector2(0, 42);
            statusRect.sizeDelta = new Vector2(-14, 18);
            _statusLabel = statusGO.GetComponent<TextMeshProUGUI>();

            // Botão Fabricar
            var craftBtnGO = CreateButton("CraftButton", rightCol.transform, "FABRICAR", new Vector2(-20, 30), new Color(0.85f, 0.55f, 0.12f, 1f));
            var craftBtnRect = craftBtnGO.GetComponent<RectTransform>();
            craftBtnRect.anchorMin = new Vector2(0, 0);
            craftBtnRect.anchorMax = new Vector2(1, 0);
            craftBtnRect.pivot = new Vector2(0.5f, 0);
            craftBtnRect.anchoredPosition = new Vector2(0, 8);
            _craftButton = craftBtnGO.GetComponent<Button>();
            _craftButton.onClick.AddListener(CraftSelectedRecipe);
            _craftButtonLabel = craftBtnGO.GetComponentInChildren<TextMeshProUGUI>();
            if (_craftButtonLabel != null) _craftButtonLabel.fontSize = 11.5f;

            // Cores do Botão Fabricar
            var btnColors = _craftButton.colors;
            btnColors.normalColor = new Color(0.85f, 0.55f, 0.12f, 1f);
            btnColors.highlightedColor = new Color(0.98f, 0.72f, 0.22f, 1f);
            btnColors.pressedColor = new Color(0.70f, 0.42f, 0.08f, 1f);
            btnColors.disabledColor = new Color(0.25f, 0.28f, 0.35f, 0.7f);
            _craftButton.colors = btnColors;

            // Inicia oculto até ser ativado explicitamente via Open()
            frameGO.SetActive(false);
        }

        private GameObject CreateRecipeEntryView(CraftingRecipe recipe, Transform parent, bool isHidden = false)
        {
            var go = new GameObject($"Recipe_{recipe.RecipeId}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline), typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(168, 46);
            go.GetComponent<LayoutElement>().preferredHeight = 46f;

            var img = go.GetComponent<Image>();
            img.color = isHidden ? new Color(0.08f, 0.09f, 0.11f, 0.95f) : new Color(0.12f, 0.14f, 0.18f, 0.95f);

            var outline = go.GetComponent<Outline>();
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            outline.enabled = false;

            var cardShadow = go.AddComponent<Shadow>();
            cardShadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
            cardShadow.effectDistance = new Vector2(1.5f, -2f);

            // Ícone do Item
            var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGO.transform.SetParent(go.transform, false);
            var iconRect = iconGO.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0, 0.5f);
            iconRect.anchorMax = new Vector2(0, 0.5f);
            iconRect.pivot = new Vector2(0, 0.5f);
            iconRect.anchoredPosition = new Vector2(7, 0);
            iconRect.sizeDelta = new Vector2(34, 34);

            var iconImg = iconGO.GetComponent<Image>();
            iconImg.sprite = GetRecipeSprite(recipe);
            iconImg.preserveAspect = true;
            if (isHidden) iconImg.color = new Color(1, 1, 1, 0.2f);

            // Nome da Receita
            string displayName = isHidden ? "??? Receita Desconhecida" : recipe.RecipeName;
            var labelGO = CreateText("Name", go.transform, displayName, 11, FontStyles.Bold, Color.white, TextAlignmentOptions.Left);
            var labelRect = labelGO.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0, 0.5f);
            labelRect.anchorMax = new Vector2(1, 0.5f);
            labelRect.pivot = new Vector2(0, 0.5f);
            labelRect.anchoredPosition = new Vector2(48, 6);
            labelRect.sizeDelta = new Vector2(-54, 18);
            var labelTmp = labelGO.GetComponent<TextMeshProUGUI>();
            labelTmp.textWrappingMode = TextWrappingModes.NoWrap;
            labelTmp.overflowMode = TextOverflowModes.Ellipsis;

            // Indicador de Status (Pronto / Falta)
            var indGO = CreateText("Indicator", go.transform, "Pronto", 8.5f, FontStyles.Normal, new Color(0.58f, 0.64f, 0.72f, 1f), TextAlignmentOptions.Left);
            var indRect = indGO.GetComponent<RectTransform>();
            indRect.anchorMin = new Vector2(0, 0.5f);
            indRect.anchorMax = new Vector2(1, 0.5f);
            indRect.pivot = new Vector2(0, 0.5f);
            indRect.anchoredPosition = new Vector2(48, -10);
            indRect.sizeDelta = new Vector2(-54, 13);

            var btn = go.GetComponent<Button>();
            if (!isHidden)
            {
                btn.onClick.AddListener(() => SelectRecipe(recipe));
                AddRecipeHoverHighlight(go);
            }

            // Badge de Tier
            string tierLabel = recipe.Tier switch
            {
                CraftingTier.Primitivo => "T1",
                CraftingTier.Ferro => "T2",
                CraftingTier.Reforcado => "T3",
                CraftingTier.Espinheiro => "T4",
                _ => ""
            };
            Color tierColor = recipe.Tier switch
            {
                CraftingTier.Primitivo => new Color(0.6f, 0.6f, 0.6f, 1f),
                CraftingTier.Ferro => new Color(0.7f, 0.8f, 0.9f, 1f),
                CraftingTier.Reforcado => new Color(0.4f, 0.6f, 1f, 1f),
                CraftingTier.Espinheiro => new Color(0.9f, 0.5f, 0.2f, 1f),
                _ => Color.gray
            };
            var tierGO = CreateText("TierBadge", go.transform, tierLabel, 7.5f, FontStyles.Bold, tierColor, TextAlignmentOptions.Right);
            var tierRect = tierGO.GetComponent<RectTransform>();
            tierRect.anchorMin = new Vector2(1, 1);
            tierRect.anchorMax = new Vector2(1, 1);
            tierRect.pivot = new Vector2(1, 1);
            tierRect.anchoredPosition = new Vector2(-6, -3);
            tierRect.sizeDelta = new Vector2(24, 13);

            return go;
        }

        private GameObject CreateIngredientRowView(MaterialDefinition mat, int current, int required, Transform parent)
        {
            var rowGO = new GameObject($"Ingredient_{mat.Id}", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            rowGO.transform.SetParent(parent, false);

            var rect = rowGO.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(180, 22);
            rowGO.GetComponent<LayoutElement>().preferredHeight = 22f;

            var img = rowGO.GetComponent<Image>();
            img.color = new Color(0.12f, 0.14f, 0.18f, 0.6f);

            // Ícone do Material
            var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGO.transform.SetParent(rowGO.transform, false);
            var iconRect = iconGO.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0, 0.5f);
            iconRect.anchorMax = new Vector2(0, 0.5f);
            iconRect.pivot = new Vector2(0, 0.5f);
            iconRect.anchoredPosition = new Vector2(3, 0);
            iconRect.sizeDelta = new Vector2(16, 16);

            var iconImg = iconGO.GetComponent<Image>();
            iconImg.sprite = GetMaterialSprite(mat);
            iconImg.preserveAspect = true;

            // Nome do Material
            var nameGO = CreateText("Name", rowGO.transform, mat.DisplayName, 9.5f, FontStyles.Normal, Color.white, TextAlignmentOptions.Left);
            var nameRect = nameGO.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0, 0.5f);
            nameRect.anchorMax = new Vector2(0.6f, 0.5f);
            nameRect.pivot = new Vector2(0, 0.5f);
            nameRect.anchoredPosition = new Vector2(24, 0);
            nameRect.sizeDelta = new Vector2(0, 18);

            // Quantidade (Verde se tem o suficiente, Vermelho se não)
            bool enough = current >= required;
            string countColor = enough ? "#4ade80" : "#f87171";
            string countText = $"<color={countColor}>{current}</color> / {required}";

            var countGO = CreateText("Count", rowGO.transform, countText, 9.5f, FontStyles.Bold, Color.white, TextAlignmentOptions.Right);
            var countRect = countGO.GetComponent<RectTransform>();
            countRect.anchorMin = new Vector2(0.6f, 0.5f);
            countRect.anchorMax = new Vector2(1, 0.5f);
            countRect.pivot = new Vector2(1, 0.5f);
            countRect.anchoredPosition = new Vector2(-4, 0);
            countRect.sizeDelta = new Vector2(0, 18);

            return rowGO;
        }

        // ── Utilitários UI Básicos ─────────────────────────────────────────────

        private void ConfigureScroll(ScrollRect scrollRect)
        {
            // Elastic movement was allowing the list to visibly spring past its first and last card.
            // Clamped bounds retain the responsive drag feel without exposing empty space.
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.inertia = true;
            scrollRect.decelerationRate = 0.12f;
            // Wheel input is routed explicitly by HandleCraftingWheelInput so it cannot be
            // swallowed by the draggable frame or a nested viewport.
            scrollRect.scrollSensitivity = 0f;
        }

        private Sprite GetRecipeSprite(CraftingRecipe recipe)
        {
            if (recipe == null) return GetGeneratedIcon("unknown", new Color(0.35f, 0.40f, 0.48f));
            return GetTextureSprite(recipe.Icon, $"recipe:{recipe.RecipeId}", GetCategoryColor(recipe.Category));
        }

        private Sprite GetMaterialSprite(MaterialDefinition material)
        {
            if (material == null) return GetGeneratedIcon("material", new Color(0.45f, 0.55f, 0.48f));
            return GetTextureSprite(material.Icon, $"material:{material.Id}", new Color(0.45f, 0.60f, 0.52f));
        }

        private Sprite GetTextureSprite(Texture2D texture, string fallbackKey, Color fallbackColor)
        {
            if (texture == null) return GetGeneratedIcon(fallbackKey, fallbackColor);
            string key = $"texture:{texture.GetInstanceID()}";
            if (_generatedIconSprites.TryGetValue(key, out var cached)) return cached;

            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            _generatedIconSprites[key] = sprite;
            return sprite;
        }

        private Sprite GetGeneratedIcon(string key, Color accent)
        {
            if (_generatedIconSprites.TryGetValue(key, out var cached)) return cached;

            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = $"CraftingIcon_{key}" };
            var pixels = new Color[size * size];
            Color baseColor = new Color(0.06f, 0.08f, 0.12f, 1f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                bool border = x < 2 || x >= size - 2 || y < 2 || y >= size - 2;
                bool diamond = Mathf.Abs(x - 15) + Mathf.Abs(y - 15) < 11;
                bool glyph = (x > 13 && x < 19 && y > 7 && y < 25) || (y > 13 && y < 19 && x > 7 && x < 25);
                pixels[y * size + x] = border ? accent * 0.72f : (glyph ? accent : (diamond ? accent * 0.34f : baseColor));
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            _generatedIconSprites[key] = sprite;
            return sprite;
        }

        private static Color GetCategoryColor(string category)
        {
            return category switch
            {
                "Armas" => new Color(0.92f, 0.48f, 0.28f),
                "Ferramentas" => new Color(0.52f, 0.72f, 0.88f),
                "Armadura" => new Color(0.62f, 0.72f, 0.82f),
                "Acessórios" => new Color(0.94f, 0.72f, 0.28f),
                "Consumíveis" => new Color(0.48f, 0.84f, 0.62f),
                "Materiais" => new Color(0.62f, 0.78f, 0.50f),
                _ => new Color(0.60f, 0.66f, 0.78f)
            };
        }

        private static RectTransform CreatePanel(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            var img = go.GetComponent<Image>();
            img.color = color;
            return rect;
        }

        private static void AddFrameOutline(GameObject gameObject, Color color, Vector2 distance)
        {
            var outline = gameObject.GetComponent<Outline>() ?? gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = distance;
            outline.useGraphicAlpha = false;
        }

        private void AddRecipeHoverHighlight(GameObject gameObject)
        {
            var trigger = gameObject.GetComponent<EventTrigger>() ?? gameObject.AddComponent<EventTrigger>();
            trigger.triggers ??= new List<EventTrigger.Entry>();

            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ =>
            {
                var image = gameObject.GetComponent<Image>();
                if (image != null)
                    image.color = new Color(0.20f, 0.27f, 0.36f, 1f);
            });

            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => RefreshRecipeListStates());

            trigger.triggers.Add(enter);
            trigger.triggers.Add(exit);
        }

        private static GameObject CreateText(string name, Transform parent, string content, float size, FontStyles style, Color color, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            if (TMP_Settings.defaultFontAsset != null)
                tmp.font = TMP_Settings.defaultFontAsset;
            tmp.text = content;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.raycastTarget = false;
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

            var labelGO = CreateText("Label", go.transform, label, 13, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
            var labelRect = labelGO.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.sizeDelta = Vector2.zero;

            return go;
        }

        // ── Recursos Padrão e Áudio ───────────────────────────────────────────

        private void LoadDefaultRecipes()
        {
            var loaded = Resources.LoadAll<CraftingRecipe>("Crafting");
            foreach (var r in loaded)
            {
                if (r != null && !_recipes.Contains(r))
                    _recipes.Add(r);
            }
        }

        private void LoadSprites()
        {
            if (panelFrameSprite == null && _inventoryUIManager != null)
            {
                var invImg = _inventoryUIManager.InventoryFrameRect?.GetComponent<Image>();
                if (invImg != null && invImg.sprite != null)
                    panelFrameSprite = invImg.sprite;
            }

            if (panelFrameSprite == null)
            {
                panelFrameSprite = Resources.Load<Sprite>("Textures/frame_classic");
            }

            if (slotFrameSprite == null && _inventoryInstaller != null && _inventoryInstaller.SlotViews != null && _inventoryInstaller.SlotViews.Count > 0)
            {
                var slotView = _inventoryInstaller.SlotViews[0];
                var slotImg = slotView.GetComponent<Image>();
                if (slotImg != null && slotImg.sprite != null)
                    slotFrameSprite = slotImg.sprite;
            }

            if (slotFrameSprite == null)
            {
                slotFrameSprite = Resources.Load<Sprite>("Textures/inventory_slot");
            }
        }

        private void LoadAudioClips()
        {
            var db = Duskborn.Audio.AudioDatabase.Instance?.UI;
            if (db != null)
            {
                if (openSound == null) openSound = db.modalOpenClip;
                if (clickSound == null) clickSound = db.buttonClickClip;
                if (craftSound == null) craftSound = db.craftSuccessClip;
                if (errorSound == null) errorSound = db.errorClip;
            }
            else
            {
                if (openSound == null) openSound = Resources.Load<AudioClip>("SFX/ui_modal_open");
                if (clickSound == null) clickSound = Resources.Load<AudioClip>("SFX/ui_button_click");
                if (craftSound == null) craftSound = Resources.Load<AudioClip>("SFX/hit_stone_01");
                if (errorSound == null) errorSound = Resources.Load<AudioClip>("SFX/ui_error");
            }
        }

        private void PlaySound(AudioClip clip, float volume = 1.0f)
        {
            if (clip == null) return;
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayAtPoint(clip, Camera.main != null ? Camera.main.transform.position : transform.position, volume);
            else
                AudioSource.PlayClipAtPoint(clip, Camera.main != null ? Camera.main.transform.position : transform.position, volume);
        }

        private static bool IsEscapePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }
    }
}
