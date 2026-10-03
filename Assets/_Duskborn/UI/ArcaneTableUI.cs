using System;
using System.Collections.Generic;
using System.Text;
using Duskborn.Gameplay.ActionBar;
using Duskborn.Gameplay.Crafting;
using Duskborn.Gameplay.Enchanting;
using Duskborn.Gameplay.Equipment;
using Duskborn.Gameplay.Player;
using InventorySystem.Bootstrap;
using InventorySystem.Core;
using InventorySystem.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Duskborn.UI
{
    /// <summary>Dedicated arcane station view; rebuilt independently of the workbench UI.</summary>
    public sealed class ArcaneTableUI : MonoBehaviour
    {
        private enum View { Forge, Etch, Crafts }
        private View _view;
        private RectTransform _root, _catalog, _targets;
        private TextMeshProUGUI _detail, _costs, _message, _actionLabel, _targetTitle, _catalogTitle, _previewTitle;
        private RawImage _preview;
        private Button _action;
        private CraftingRecipe _recipe;
        private WeaponItem _weapon;
        private WeaponEtching _rune = new WeaponEtching { kind = RuneKind.Flame, level = 1 };
        private readonly List<CraftingRecipe> _recipes = new();
        private readonly List<WeaponItem> _weapons = new();
        private readonly List<GameObject> _catalogRows = new(), _targetRows = new();
        private readonly List<(Button button, Func<bool> available)> _availability = new();
        private readonly List<(Button button, View view)> _tabs = new();
        private readonly List<(Button button, Func<bool> selected)> _catalogChoices = new(), _targetChoices = new();
        private readonly List<(TextMeshProUGUI label, WeaponEtching rune)> _ownedLabels = new();
        private InventoryInstaller _backpack;
        private ActionBarInstaller _bar;
        private bool _confirmReplace;
        private bool _acting;
        private float _refreshAt;
        private string _inventorySignature;
        private readonly Color _panel = new Color(.045f, .035f, .075f, .98f);
        private readonly Color _card = new Color(.105f, .075f, .16f);
        private readonly Color _gold = new Color(.92f, .76f, .43f);

        public void Show()
        {
            if (_root == null) Build();
            _backpack = FindAnyObjectByType<InventoryInstaller>();
            _bar = FindAnyObjectByType<ActionBarInstaller>();
            _recipes.Clear();
            foreach (var r in Resources.LoadAll<CraftingRecipe>("Crafting"))
                if (r != null && r.OutputItem != null && r.RequiredStation == CraftingStationType.ArcaneTable)
                    _recipes.Add(r);
            _recipes.Sort((a, b) => string.CompareOrdinal(a.RecipeId, b.RecipeId));
            _root.gameObject.SetActive(true);
            SetView(View.Forge);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            PlayerCameraController.LocalInstance?.SetRotationLocked(true);
        }
        public void Hide()
        {
            if (_root != null) _root.gameObject.SetActive(false);
            _confirmReplace = false;
        }
        private void OnDestroy()
        {
            if (_root == null) return;
            if (Application.isPlaying) Destroy(_root.parent.gameObject); else DestroyImmediate(_root.parent.gameObject);
        }
        private void Update()
        {
            if (_root == null || !_root.gameObject.activeSelf || Time.unscaledTime < _refreshAt) return;
            _refreshAt = Time.unscaledTime + .2f;
            if (!LocalPlayerContext.HasLocalPlayer) { CraftingUIManager.Instance?.Close(); return; }
            if (_view == View.Etch)
            {
                string signature = WeaponSignature();
                if (_inventorySignature != signature) { _inventorySignature = signature; RebuildTargets(); }
            }
            foreach (var row in _availability) row.button.interactable = row.available();
            foreach (var row in _catalogChoices) PaintChoice(row.button, row.selected());
            foreach (var row in _targetChoices) PaintChoice(row.button, row.selected());
            foreach (var row in _ownedLabels) row.label.text = row.rune + "\n<color=#B99CCB>Owned: " +
                (LocalPlayerContext.Resources?.GetCount(RuneCatalog.Id(row.rune.kind, row.rune.level)) ?? 0) + "</color>";
            RefreshDetails();
        }
        private bool InRange => LocalPlayerContext.Stats != null && LocalPlayerContext.Stats.IsAlive &&
            CraftingUIManager.Instance?.CurrentWorkbench != null &&
            CraftingUIManager.Instance.CurrentWorkbench.StationType == CraftingStationType.ArcaneTable &&
            CraftingUIManager.Instance.CurrentWorkbench.IsInRange(LocalPlayerContext.Stats.transform.position, 4f);
        private void SetView(View view)
        {
            _view = view; _confirmReplace = false; _message.text = "";
            foreach (var tab in _tabs) tab.button.GetComponent<Image>().color = tab.view == view ? new Color(.3f, .2f, .42f) : _card;
            ClearRows(_catalogRows); ClearRows(_targetRows); _availability.Clear();
            _catalogChoices.Clear(); _targetChoices.Clear(); _ownedLabels.Clear();
            _targets.gameObject.SetActive(view == View.Etch);
            _targetTitle.text = view == View.Etch ? "2  /  CHOOSE A WEAPON" : "REQUIRED MATERIALS";
            _catalogTitle.text = view == View.Etch ? "1  /  OWNED RUNESTONES" : view == View.Forge ? "RUNESTONE RECIPES" : "ARCANE RECIPES";
            _targets.parent.GetComponent<RectTransform>().sizeDelta = new Vector2(296, view == View.Etch ? 230 : 382);
            Place(_costs.rectTransform, 20, view == View.Etch ? 300 : 52, 280, view == View.Etch ? 128 : 370);
            _costs.fontSize = view == View.Etch ? 14 : 17;
            if (view == View.Etch)
            {
                for (int kind = 1; kind <= 8; kind++) for (int level = 1; level <= 3; level++)
                {
                    var rune = new WeaponEtching { kind = (RuneKind)kind, level = level };
                    var def = Resources.Load<MaterialDefinition>("Runestones/" + RuneCatalog.Id(rune.kind, rune.level));
                    var button = Row(_catalog, _catalogRows, rune.ToString(), def?.Icon, () =>
                    { _rune = rune; _confirmReplace = false; _message.text = ""; RefreshDetails(); });
                    _availability.Add((button, () => LocalPlayerContext.Resources?.GetCount(RuneCatalog.Id(rune.kind, rune.level)) > 0));
                    _catalogChoices.Add((button, () => _rune.Packed == rune.Packed));
                    _ownedLabels.Add((button.GetComponentInChildren<TextMeshProUGUI>(), rune));
                }
                RebuildTargets();
            }
            else
            {
                _recipe = null;
                foreach (var recipe in _recipes)
                {
                    bool runeRecipe = recipe.OutputItem.Id.StartsWith("runestone_", StringComparison.Ordinal);
                    if ((view == View.Forge) != runeRecipe) continue;
                    var selected = recipe;
                    var button = Row(_catalog, _catalogRows, recipe.RecipeName, recipe.Icon, () =>
                    { _recipe = selected; _message.text = ""; RefreshDetails(); });
                    _catalogChoices.Add((button, () => _recipe == selected));
                    if (_recipe == null) _recipe = recipe;
                }
            }
            RefreshDetails();
        }
        private string WeaponSignature()
        {
            var b = new StringBuilder();
            foreach (var inventory in Inventories())
                if (inventory != null) foreach (var slot in inventory.GetSlots())
                    if (slot.Item is WeaponItem w) b.Append(w.GetHashCode()).Append(':').Append(w.Etching.Packed).Append(';');
            return b.ToString();
        }
        private IInventoryReader[] Inventories() => new IInventoryReader[] { _backpack?.Service, _bar?.Service?.Service };
        private void RebuildTargets()
        {
            ClearRows(_targetRows); _targetChoices.Clear(); _weapons.Clear();
            foreach (var inventory in Inventories())
                if (inventory != null) foreach (var slot in inventory.GetSlots())
                    if (slot.Item is WeaponItem weapon && !_weapons.Contains(weapon)) _weapons.Add(weapon);
            if (!_weapons.Contains(_weapon)) { _weapon = _weapons.Count > 0 ? _weapons[0] : null; _confirmReplace = false; }
            foreach (var weapon in _weapons)
            {
                var selected = weapon;
                var icon = _backpack?.GetIcon(weapon.Id) ?? PlayerCombat.FindRuneWeapon(weapon.Id)?.Icon;
                var button = Row(_targets, _targetRows, weapon.DisplayName + "\n<color=#B99CCB>" + weapon.Etching + "</color>", icon,
                    () => { _weapon = selected; _confirmReplace = false; _message.text = ""; RefreshDetails(); });
                _targetChoices.Add((button, () => ReferenceEquals(_weapon, selected)));
            }
            if (_weapons.Count == 0)
            {
                var empty = Label(_targets, "No weapons in backpack or action bar.", 16);
                empty.gameObject.AddComponent<LayoutElement>().preferredHeight = 64;
                _targetRows.Add(empty.gameObject);
            }
        }
        private void RefreshDetails()
        {
            if (_view == View.Etch)
            {
                var def = Resources.Load<MaterialDefinition>("Runestones/" + RuneCatalog.Id(_rune.kind, _rune.level));
                _preview.texture = def?.Icon;
                _previewTitle.text = _rune.ToString();
                _detail.text = $"{RuneCatalog.Effect(_rune.kind)}\n\n" +
                    $"<b>+{_rune.StacksPerHit} stacks per hit</b>  ·  12 stack limit\n" +
                    (_rune.kind == RuneKind.Venom ? "9" : "6") + " second duration; hits refresh duration.\n\n" +
                    (_weapon != null ? $"<b>Target: {_weapon.DisplayName}</b>\nCurrent: {_weapon.Etching}\nAfter: {_rune}" : "Select a weapon to etch.");
                int owned = LocalPlayerContext.Resources?.GetCount(RuneCatalog.Id(_rune.kind, _rune.level)) ?? 0;
                _costs.text = $"Consumes 1 {_rune} runestone  ·  Owned: {owned}\n\nOne etching per weapon. Replacing destroys the old etching.\nArmor and accessories: coming later.";
                _action.interactable = InRange && _weapon != null && owned > 0 &&
                    !(_weapon.Etching.kind == _rune.kind && _weapon.Etching.level >= _rune.level);
                _actionLabel.text = _confirmReplace ? "CONFIRM REPLACEMENT" : "ETCH WEAPON";
            }
            else
            {
                _preview.texture = _recipe?.Icon;
                _previewTitle.text = _recipe?.RecipeName ?? "Arcane Crafts";
                _detail.text = _recipe == null ? "No recipes available." :
                    $"{_recipe.Description}\n\nCrafted at the Arcane Table.";
                var b = new StringBuilder();
                if (_recipe != null)
                {
                    foreach (var ingredient in _recipe.Ingredients)
                    {
                        int count = LocalPlayerContext.Resources?.GetCount(ingredient.material) ?? 0;
                        b.Append(count >= ingredient.amount ? "<color=#BFE3BB>" : "<color=#EE9B91>")
                            .Append(ingredient.material != null ? ingredient.material.DisplayName : "Missing material")
                            .Append("  ").Append(count).Append(" / ").Append(ingredient.amount).Append("</color>\n");
                    }
                    foreach (var fuel in _recipe.FuelIngredients) b.Append("Fuel: ").Append(fuel.material?.DisplayName).Append(" ×").Append(fuel.amount).Append('\n');
                }
                _costs.text = b.ToString();
                _action.interactable = InRange && _recipe != null && _recipe.CanCraft(LocalPlayerContext.Resources);
                _actionLabel.text = _recipe != null && _recipe.ProcessingSeconds > 0 ? "OPEN PROCESSING" : "CRAFT";
            }
        }
        private void Act()
        {
            if (_acting) return;
            _acting = true;
            try { PerformAction(); } finally { _acting = false; }
        }
        private void PerformAction()
        {
            if (!InRange) { _message.text = "Stay near the Arcane Table."; return; }
            if (_view == View.Etch)
            {
                if (_weapon == null) return;
                if (_weapon.Etching.IsValid && !_confirmReplace)
                { _confirmReplace = true; _message.text = $"Replace {_weapon.Etching} with {_rune}? The old etching is lost."; RefreshDetails(); return; }
                bool success = RuneEtchingService.TryEtch(LocalPlayerContext.Resources, _backpack?.Service,
                    _bar?.Service?.Service, _weapon, _rune);
                _confirmReplace = false;
                _message.text = success ? $"Etched {_rune} onto {_weapon.DisplayName}." : "Etching failed. Check your weapon and runestone.";
                if (success) LocalPlayerContext.Combat?.PublishRuneEquipment(LocalPlayerContext.WeaponHandler?.ActiveWeapon);
            }
            else if (_recipe != null)
            {
                var recipe = _recipe;
                if (recipe.ProcessingSeconds > 0)
                {
                    // Existing station jobs remain available through the processing controller.
                    CraftingUIManager.Instance.OpenArcaneLegacyRecipe(recipe); return;
                }
                bool success;
                if (recipe.OutputItem is MaterialDefinition material)
                {
                    success = recipe.TrySpendIngredients(LocalPlayerContext.Resources);
                    if (success) LocalPlayerContext.Resources.Add(material, recipe.OutputAmount);
                }
                else success = recipe.TryCraftItem(LocalPlayerContext.Resources, _backpack?.Service);
                _message.text = success ? "Crafted " + recipe.RecipeName + "." : "Missing materials or no free inventory slot.";
            }
            RefreshDetails();
        }
        private void Build()
        {
            var canvasObject = new GameObject("ArcaneTableCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 130;
            var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 800); scaler.matchWidthOrHeight = .5f;
            _root = Panel(canvasObject.transform, "Arcane screen", new Color(0, 0, 0, .68f)); Stretch(_root);
            var panel = Panel(_root, "Arcane Table", _panel); panel.anchorMin = panel.anchorMax = new Vector2(.5f, .5f);
            panel.sizeDelta = new Vector2(1180, 700);
            var title = Label(panel, "ARCANE TABLE", 30); Place(title.rectTransform, 28, 16, 1080, 44); title.color = _gold;
            var subtitle = Label(panel, "Bind gathered stone and creature remains into living inscriptions.", 16);
            Place(subtitle.rectTransform, 30, 66, 1040, 28);
            var close = Button(panel, "×", () => CraftingUIManager.Instance.Close()); Place(close.GetComponent<RectTransform>(), 1124, 18, 30, 34);
            var tabX = 28f;
            foreach (View view in Enum.GetValues(typeof(View)))
            {
                var captured = view;
                var tab = Button(panel, view == View.Forge ? "FORGE RUNESTONES" : view == View.Etch ? "ETCH WEAPON" : "ARCANE CRAFTS", () => SetView(captured));
                Place(tab.GetComponent<RectTransform>(), tabX, 108, 245, 42); tabX += 257; _tabs.Add((tab, view));
            }
            var left = Panel(panel, "Rune catalog", _card); Place(left, 28, 172, 300, 444);
            _catalogTitle = Label(left, "1  /  CHOOSE A RUNESTONE", 15); Place(_catalogTitle.rectTransform, 14, 10, 275, 30);
            _catalog = Scroll(left, 12, 48, 276, 382);
            var middle = Panel(panel, "Targets", _card); Place(middle, 342, 172, 320, 444);
            _targetTitle = Label(middle, "2  /  CHOOSE A WEAPON", 15); Place(_targetTitle.rectTransform, 14, 10, 294, 30);
            _targets = Scroll(middle, 12, 48, 296, 382);
            // The middle column carries recipe cost details outside etching mode.
            _costs = Label(middle, "", 17); Place(_costs.rectTransform, 20, 52, 280, 370);
            var detailPanel = Panel(panel, "Etching preview", new Color(.07f, .05f, .115f)); Place(detailPanel, 676, 172, 476, 444);
            var iconGo = new GameObject("Runestone preview", typeof(RectTransform), typeof(RawImage)); iconGo.transform.SetParent(detailPanel, false);
            _preview = iconGo.GetComponent<RawImage>(); _preview.raycastTarget = false; Place(_preview.rectTransform, 356, 14, 96, 96);
            _previewTitle = Label(detailPanel, "", 28); Place(_previewTitle.rectTransform, 22, 22, 310, 86); _previewTitle.color = _gold;
            _detail = Label(detailPanel, "", 16); Place(_detail.rectTransform, 22, 128, 430, 230);
            _action = Button(detailPanel, "CRAFT", Act); Place(_action.GetComponent<RectTransform>(), 22, 374, 430, 48);
            _actionLabel = _action.GetComponentInChildren<TextMeshProUGUI>();
            _message = Label(panel, "", 16); Place(_message.rectTransform, 28, 630, 1124, 42); _message.color = _gold;
            _root.gameObject.SetActive(false);
        }
        private void LateUpdate()
        {
            if (_costs != null) _costs.gameObject.SetActive(true);
            // Etching cost text fits below the main effect description.
            if (_view == View.Etch && _root != null && _root.gameObject.activeSelf)
                _message.text = string.IsNullOrEmpty(_message.text) ? "Select an owned rune and a weapon. One stone is consumed per etching." : _message.text;
        }
        private RectTransform Panel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color; return go.GetComponent<RectTransform>();
        }
        private TextMeshProUGUI Label(Transform parent, string text, int size)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
            var label = go.GetComponent<TextMeshProUGUI>(); label.text = text; label.fontSize = size;
            label.color = new Color(.9f, .87f, .94f); label.raycastTarget = false; label.textWrappingMode = TextWrappingModes.Normal;
            return label;
        }
        private Button Button(Transform parent, string text, Action click)
        {
            var rect = Panel(parent, text, _card); var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>(); button.onClick.AddListener(() => click());
            var label = Label(rect, text, 16); Stretch(label.rectTransform, 10); label.alignment = TextAlignmentOptions.Center;
            return button;
        }
        private Button Row(Transform parent, List<GameObject> rows, string text, Texture2D texture, Action click)
        {
            var button = Button(parent, text, click); rows.Add(button.gameObject);
            var layout = button.gameObject.AddComponent<LayoutElement>(); layout.preferredHeight = 66;
            var label = button.GetComponentInChildren<TextMeshProUGUI>(); label.alignment = TextAlignmentOptions.MidlineLeft;
            label.rectTransform.offsetMin = new Vector2(64, 5); label.rectTransform.offsetMax = new Vector2(-8, -5);
            var go = new GameObject("Icon", typeof(RectTransform), typeof(RawImage)); go.transform.SetParent(button.transform, false);
            var raw = go.GetComponent<RawImage>(); raw.texture = texture; raw.raycastTarget = false;
            Place(raw.rectTransform, 7, 7, 50, 50); return button;
        }
        private RectTransform Scroll(Transform parent, float x, float y, float w, float h)
        {
            var viewport = Panel(parent, "Scroll", new Color(.04f, .025f, .065f)); Place(viewport, x, y, w, h);
            viewport.gameObject.AddComponent<RectMask2D>(); var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)).GetComponent<RectTransform>();
            content.SetParent(viewport, false); content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(.5f, 1); content.sizeDelta = Vector2.zero;
            var layout = content.GetComponent<VerticalLayoutGroup>(); layout.spacing = 6; layout.childControlHeight = true;
            layout.childForceExpandHeight = false; layout.childControlWidth = true; layout.childForceExpandWidth = true;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content; scroll.viewport = viewport; scroll.horizontal = false; scroll.scrollSensitivity = 25;
            return content;
        }
        private void ClearRows(List<GameObject> rows)
        { foreach (var row in rows) { row.SetActive(false); if (Application.isPlaying) Destroy(row); else DestroyImmediate(row); } rows.Clear(); }
        private void PaintChoice(Button button, bool selected)
        {
            if (button == null) return;
            button.GetComponent<Image>().color = selected ? new Color(.29f, .2f, .39f) : _card;
            var outline = button.GetComponent<Outline>();
            if (outline == null) { outline = button.gameObject.AddComponent<Outline>(); outline.effectColor = _gold; outline.effectDistance = new Vector2(1, -1); }
            outline.enabled = selected;
        }
        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
        }
        private static void Stretch(RectTransform rect, float inset = 0)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.one * inset; rect.offsetMax = Vector2.one * -inset; }
    }
}
