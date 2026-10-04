using System;
using System.Collections.Generic;
using System.Linq;
using Duskborn.Gameplay.Building;
using UnityEngine;
using UnityEngine.UI;

namespace Duskborn.UI.Building
{
    internal sealed class BuildingCatalogView
    {
        private sealed class Card
        {
            internal BuildableDefinition Definition;
            internal Button Button;
            internal RawImage Icon;
            internal Text Placeholder;
            internal Text Name;
            internal Text Category;
            internal Text State;
            internal Image Selection;
            internal readonly List<CostChip> Costs = new();
        }

        private sealed class CostChip
        {
            internal RawImage Icon;
            internal Text Placeholder;
            internal Text Value;
        }

        private readonly GameObject root;
        private readonly RectTransform cardContent;
        private readonly RawImage detailIcon;
        private readonly Text detailPlaceholder;
        private readonly Text detailName;
        private readonly Text detailCategory;
        private readonly Text detailDescription;
        private readonly Text detailCapability;
        private readonly Text detailCosts;
        private readonly Text detailReason;
        private readonly Button placeButton;
        private readonly List<Card> cards = new();
        private Func<BuildableDefinition, BuildablePresentation> presenter;
        private Action<BuildableDefinition> place;
        private string selectedId;

        internal bool IsOpen => root.activeSelf;

        internal BuildingCatalogView(Transform canvas)
        {
            root = BriarwoodCatalogTheme.Panel("BuildingCatalog", canvas, BriarwoodCatalogTheme.Leather).gameObject;
            var rootRect = (RectTransform)root.transform;
            BuildingUIElements.Anchors(rootRect, new Vector2(.035f, .11f), new Vector2(.8f, .93f), Vector2.zero, Vector2.zero);

            var title = BriarwoodCatalogTheme.Label("Title", root.transform, 32, TextAnchor.MiddleLeft, true);
            title.text = "BUILD INFRASTRUCTURE";
            title.fontStyle = FontStyle.Bold;
            BuildingUIElements.Anchors(title.rectTransform, new Vector2(.05f, .885f), new Vector2(.77f, .947f), Vector2.zero, Vector2.zero);

            var subtitle = BriarwoodCatalogTheme.Label("Subtitle", root.transform, 15);
            subtitle.text = "Choose a station and check its cost before placing it.";
            subtitle.color = BriarwoodCatalogTheme.Muted;
            BuildingUIElements.Anchors(subtitle.rectTransform, new Vector2(.035f, .845f), new Vector2(.82f, .9f), Vector2.zero, Vector2.zero);

            var listPanel = BriarwoodCatalogTheme.Panel("CatalogList", root.transform, BriarwoodCatalogTheme.Leather);
            BuildingUIElements.Anchors(listPanel.rectTransform, new Vector2(.025f, .13f), new Vector2(.44f, .83f), Vector2.zero, Vector2.zero);
            var scroll = BuildingUIElements.Scroll("Cards", listPanel.transform, out cardContent);
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = false;
            BuildingUIElements.Stretch((RectTransform)scroll.transform, 10, 10, -6, -10);

            var details = BriarwoodCatalogTheme.Sprite("Details", root.transform, "socket", true);
            details.pixelsPerUnitMultiplier = 3f;
            BuildingUIElements.Anchors(details.rectTransform, new Vector2(.46f, .13f), new Vector2(.975f, .83f), Vector2.zero, Vector2.zero);

            var iconFrame = BriarwoodCatalogTheme.Sprite("IconFrame", details.transform, "socket", true);
            iconFrame.pixelsPerUnitMultiplier = 5f;
            BuildingUIElements.Anchors(iconFrame.rectTransform, new Vector2(.065f, .63f), new Vector2(.39f, .93f), Vector2.zero, Vector2.zero);
            detailIcon = BuildingUIElements.Object("Icon", iconFrame.transform, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage)).GetComponent<RawImage>();
            detailIcon.raycastTarget = false;
            detailIcon.gameObject.AddComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            detailIcon.GetComponent<AspectRatioFitter>().aspectRatio = 1f;
            BuildingUIElements.Stretch(detailIcon.rectTransform, 10, 10, -10, -10);
            detailPlaceholder = BriarwoodCatalogTheme.Label("Placeholder", iconFrame.transform, 38, TextAnchor.MiddleCenter, true);
            detailPlaceholder.color = BriarwoodCatalogTheme.Muted;
            BuildingUIElements.Stretch(detailPlaceholder.rectTransform);

            detailName = BriarwoodCatalogTheme.Label("Name", details.transform, 28, TextAnchor.MiddleLeft, true);
            detailName.fontStyle = FontStyle.Bold;
            BuildingUIElements.Anchors(detailName.rectTransform, new Vector2(.43f, .8f), new Vector2(.93f, .94f), Vector2.zero, Vector2.zero);
            detailCategory = BriarwoodCatalogTheme.Label("Category", details.transform, 15);
            detailCategory.color = BriarwoodCatalogTheme.Brass;
            BuildingUIElements.Anchors(detailCategory.rectTransform, new Vector2(.43f, .71f), new Vector2(.93f, .81f), Vector2.zero, Vector2.zero);
            detailDescription = BriarwoodCatalogTheme.Label("Description", details.transform, 17);
            BuildingUIElements.Anchors(detailDescription.rectTransform, new Vector2(.07f, .53f), new Vector2(.93f, .63f), Vector2.zero, Vector2.zero);
            detailCapability = BriarwoodCatalogTheme.Label("Capability", details.transform, 16);
            detailCapability.color = BriarwoodCatalogTheme.Muted;
            BuildingUIElements.Anchors(detailCapability.rectTransform, new Vector2(.07f, .42f), new Vector2(.93f, .53f), Vector2.zero, Vector2.zero);
            detailCosts = BriarwoodCatalogTheme.Label("Costs", details.transform, 17);
            BuildingUIElements.Anchors(detailCosts.rectTransform, new Vector2(.07f, .235f), new Vector2(.93f, .42f), Vector2.zero, Vector2.zero);
            detailReason = BriarwoodCatalogTheme.Label("Reason", details.transform, 15);
            BuildingUIElements.Anchors(detailReason.rectTransform, new Vector2(.07f, .14f), new Vector2(.93f, .235f), Vector2.zero, Vector2.zero);
            placeButton = BriarwoodCatalogTheme.Button("Place", details.transform, "PLACE", PlaceSelected, true);
            BuildingUIElements.Anchors((RectTransform)placeButton.transform, new Vector2(.05f, .035f), new Vector2(.95f, .14f), Vector2.zero, Vector2.zero);

            var frame = BriarwoodCatalogTheme.Sprite("BriarwoodFrame", root.transform, "frame", true);
            frame.pixelsPerUnitMultiplier = 3f;
            BuildingUIElements.Stretch(frame.rectTransform, -10, -10, 10, 10);
            var crest = BriarwoodCatalogTheme.Sprite("HammerCrest", root.transform, "crest");
            crest.rectTransform.anchorMin = crest.rectTransform.anchorMax = new Vector2(.5f, 1f);
            crest.rectTransform.sizeDelta = new Vector2(104, 104);
            crest.rectTransform.anchoredPosition = new Vector2(0, -12);
            root.SetActive(false);
        }

        internal void Show(
            IReadOnlyList<BuildableDefinition> definitions,
            Func<BuildableDefinition, BuildablePresentation> presentation,
            Action<BuildableDefinition> onPlace,
            Action close)
        {
            presenter = presentation;
            place = onPlace;
            RebuildCards(definitions);
            if (string.IsNullOrEmpty(selectedId) || definitions.All(definition => definition.id != selectedId))
                selectedId = definitions.FirstOrDefault()?.id;

            var closeButton = root.transform.Find("Close")?.GetComponent<Button>();
            if (closeButton == null)
            {
                closeButton = BriarwoodCatalogTheme.Button("Close", root.transform, "CLOSE  [B / Esc]", close);
                BuildingUIElements.Anchors((RectTransform)closeButton.transform, new Vector2(.82f, .905f), new Vector2(.97f, .97f), Vector2.zero, Vector2.zero);

            }

            root.SetActive(true);
            Refresh();
        }

        internal void Hide() => root.SetActive(false);

        internal void Refresh()
        {
            if (!root.activeSelf || presenter == null) return;
            foreach (var card in cards) RenderCard(card, presenter(card.Definition));
            RenderDetails(cards.FirstOrDefault(card => card.Definition.id == selectedId));
        }

        private void RebuildCards(IReadOnlyList<BuildableDefinition> definitions)
        {
            if (cards.Count == definitions.Count && cards.Select(card => card.Definition).SequenceEqual(definitions)) return;
            foreach (Transform child in cardContent) UnityEngine.Object.Destroy(child.gameObject);
            cards.Clear();
            foreach (var definition in definitions)
            {
                var cardObject = BriarwoodCatalogTheme.Panel("Buildable_" + definition.id, cardContent, BriarwoodCatalogTheme.Raised).gameObject;
                cardObject.AddComponent<LayoutElement>().minHeight = 124;
                var button = cardObject.AddComponent<Button>();
                button.targetGraphic = cardObject.GetComponent<Image>();
                var iconFrame = BriarwoodCatalogTheme.Sprite("IconFrame", cardObject.transform, "socket", true);
                iconFrame.pixelsPerUnitMultiplier = 6f;
                BuildingUIElements.Anchors(iconFrame.rectTransform, new Vector2(.025f, .16f), new Vector2(.23f, .84f), Vector2.zero, Vector2.zero);
                var icon = BuildingUIElements.Object("Icon", iconFrame.transform, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage)).GetComponent<RawImage>();
                icon.raycastTarget = false;
                icon.gameObject.AddComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                icon.GetComponent<AspectRatioFitter>().aspectRatio = 1f;
                BuildingUIElements.Stretch(icon.rectTransform, 7, 7, -7, -7);
                var placeholder = BriarwoodCatalogTheme.Label("Placeholder", iconFrame.transform, 26, TextAnchor.MiddleCenter, true);
                placeholder.color = BriarwoodCatalogTheme.Muted;
                BuildingUIElements.Stretch(placeholder.rectTransform);
                var name = BriarwoodCatalogTheme.Label("Name", cardObject.transform, 22, TextAnchor.MiddleLeft, true);
                name.fontStyle = FontStyle.Bold;
                name.horizontalOverflow = HorizontalWrapMode.Overflow;
                BuildingUIElements.Anchors(name.rectTransform, new Vector2(.27f, .58f), new Vector2(.7f, .92f), Vector2.zero, Vector2.zero);
                var state = BriarwoodCatalogTheme.Label("State", cardObject.transform, 12, TextAnchor.MiddleRight);
                state.horizontalOverflow = HorizontalWrapMode.Overflow;
                BuildingUIElements.Anchors(state.rectTransform, new Vector2(.72f, .59f), new Vector2(.97f, .92f), Vector2.zero, Vector2.zero);
                var category = BriarwoodCatalogTheme.Label("Category", cardObject.transform, 13);
                category.color = BriarwoodCatalogTheme.Muted;
                BuildingUIElements.Anchors(category.rectTransform, new Vector2(.27f, .43f), new Vector2(.97f, .65f), Vector2.zero, Vector2.zero);
                var costRow = BuildingUIElements.Object("Costs", cardObject.transform, typeof(RectTransform), typeof(HorizontalLayoutGroup));
                BuildingUIElements.Anchors((RectTransform)costRow.transform, new Vector2(.27f, .07f), new Vector2(.97f, .41f), Vector2.zero, Vector2.zero);
                var costLayout = costRow.GetComponent<HorizontalLayoutGroup>();
                costLayout.spacing = 6;
                costLayout.childControlWidth = true;
                costLayout.childForceExpandWidth = true;
                costLayout.childControlHeight = true;
                costLayout.childForceExpandHeight = true;
                var selection = BriarwoodCatalogTheme.Sprite("SelectedFrame", cardObject.transform, "socket", true);
                selection.fillCenter = false;
                selection.pixelsPerUnitMultiplier = 5f;
                BuildingUIElements.Stretch(selection.rectTransform, -12, -12, 12, 12);
                var card = new Card { Definition = definition, Button = button, Icon = icon, Placeholder = placeholder, Name = name, State = state, Category = category, Selection = selection };
                var initialModel = presenter(definition);
                foreach (var cost in initialModel.Costs)
                {
                    var chip = BriarwoodCatalogTheme.Panel("Cost_" + cost.MaterialId, costRow.transform, BriarwoodCatalogTheme.Leather);
                    chip.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
                    var costIcon = BuildingUIElements.Object("Icon", chip.transform, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage)).GetComponent<RawImage>();
                    costIcon.raycastTarget = false;
                    BuildingUIElements.Anchors(costIcon.rectTransform, new Vector2(.05f, .18f), new Vector2(.29f, .82f), Vector2.zero, Vector2.zero);
                    var costPlaceholder = BriarwoodCatalogTheme.Label("Placeholder", chip.transform, 12, TextAnchor.MiddleCenter);
                    costPlaceholder.color = BriarwoodCatalogTheme.Brass;
                    BuildingUIElements.Anchors(costPlaceholder.rectTransform, new Vector2(.05f, .18f), new Vector2(.29f, .82f), Vector2.zero, Vector2.zero);
                    var costValue = BriarwoodCatalogTheme.Label("Value", chip.transform, 11, TextAnchor.MiddleCenter);
                    BuildingUIElements.Anchors(costValue.rectTransform, new Vector2(.25f, .04f), new Vector2(.97f, .96f), Vector2.zero, Vector2.zero);
                    card.Costs.Add(new CostChip { Icon = costIcon, Placeholder = costPlaceholder, Value = costValue });
                }
                button.onClick.AddListener(() => { selectedId = definition.id; Refresh(); });
                cards.Add(card);
            }
        }

        private void RenderCard(Card card, BuildablePresentation model)
        {
            card.Name.text = model.Definition.displayName;
            card.Icon.texture = BriarwoodCatalogTheme.Icon(model.Definition);
            card.Icon.enabled = card.Icon.texture != null;
            card.Placeholder.gameObject.SetActive(card.Icon.texture == null);
            card.Placeholder.text = Initials(model.Definition.displayName);
            card.Category.text = model.Definition.category;
            for (int index = 0; index < card.Costs.Count && index < model.Costs.Count; index++)
            {
                var chip = card.Costs[index];
                var cost = model.Costs[index];
                chip.Icon.texture = cost.Icon;
                chip.Icon.enabled = cost.Icon != null;
                chip.Placeholder.gameObject.SetActive(cost.Icon == null);
                chip.Placeholder.text = Initials(cost.DisplayName);
                chip.Value.text = cost.DisplayName + "\n" + cost.Owned + "/" + cost.Required;
                chip.Value.color = cost.Missing == 0 ? BriarwoodCatalogTheme.Positive : BriarwoodCatalogTheme.Negative;
            }
            card.State.text = !model.IsUnlocked ? "BLOQUEADO" : !model.IsAvailable ? "UNAVAILABLE" : model.IsAffordable ? "READY" : "MISSING";
            card.State.color = model.CanPlace ? BriarwoodCatalogTheme.Positive : !model.IsUnlocked ? BriarwoodCatalogTheme.Muted : BriarwoodCatalogTheme.Negative;
            card.Selection.gameObject.SetActive(card.Definition.id == selectedId);
            card.Button.targetGraphic.color = card.Definition.id == selectedId
                ? new Color(.23f, .18f, .105f, 1f)
                : BriarwoodCatalogTheme.Raised;
        }

        private void RenderDetails(Card selected)
        {
            if (selected == null || presenter == null) return;
            var model = presenter(selected.Definition);
            detailIcon.texture = BriarwoodCatalogTheme.Icon(model.Definition);
            detailIcon.enabled = detailIcon.texture != null;
            detailPlaceholder.gameObject.SetActive(detailIcon.texture == null);
            detailPlaceholder.text = Initials(model.Definition.displayName);
            detailName.text = model.Definition.displayName;
            detailCategory.text = model.Definition.category + " • " + model.Definition.station;
            detailDescription.text = model.Definition.description;
            detailCapability.text = "FUNCTION\n" + model.Capability;
            detailCosts.text = "COST\n" + string.Join("\n", model.Costs.Select(cost =>
                (cost.Missing == 0 ? "✓  " : "!  ") + cost.DisplayName + "   " + cost.Owned + " / " + cost.Required +
                (cost.Missing > 0 ? "   (missing " + cost.Missing + ")" : string.Empty)));
            detailReason.text = model.CanPlace ? "Ready to place." : model.DisabledReason;
            detailReason.color = model.CanPlace ? BriarwoodCatalogTheme.Positive : BriarwoodCatalogTheme.Negative;
            placeButton.interactable = model.CanPlace;
            BuildingUIElements.SetButtonCaption(placeButton, model.CanPlace ? "PLACE" : "UNAVAILABLE");
        }

        private void PlaceSelected()
        {
            var card = cards.FirstOrDefault(value => value.Definition.id == selectedId);
            if (card != null && presenter(card.Definition).CanPlace) place?.Invoke(card.Definition);
        }

        private static string Initials(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "?";
            return string.Concat(value.Split(' ').Where(part => part.Length > 0).Take(2).Select(part => char.ToUpperInvariant(part[0])));
        }
    }
}
