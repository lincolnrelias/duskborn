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
            root = BuildingUIElements.Panel("BuildingCatalog", canvas, BuildingUIElements.Backdrop).gameObject;
            var rootRect = (RectTransform)root.transform;
            BuildingUIElements.Anchors(rootRect, new Vector2(.035f, .11f), new Vector2(.8f, .93f), Vector2.zero, Vector2.zero);

            var title = BuildingUIElements.Label("Title", root.transform, 28);
            title.text = "CONSTRUIR INFRAESTRUTURA";
            title.fontStyle = FontStyle.Bold;
            BuildingUIElements.Anchors(title.rectTransform, new Vector2(.035f, .89f), new Vector2(.78f, .98f), Vector2.zero, Vector2.zero);

            var subtitle = BuildingUIElements.Label("Subtitle", root.transform, 15);
            subtitle.text = "Escolha uma estação e confira o custo antes de posicionar.";
            subtitle.color = BuildingUIElements.Muted;
            BuildingUIElements.Anchors(subtitle.rectTransform, new Vector2(.035f, .845f), new Vector2(.82f, .9f), Vector2.zero, Vector2.zero);

            var listPanel = BuildingUIElements.Panel("CatalogList", root.transform, BuildingUIElements.Surface);
            BuildingUIElements.Anchors(listPanel.rectTransform, new Vector2(.025f, .13f), new Vector2(.44f, .83f), Vector2.zero, Vector2.zero);
            var scroll = BuildingUIElements.Scroll("Cards", listPanel.transform, out cardContent);
            BuildingUIElements.Stretch((RectTransform)scroll.transform, 10, 10, -6, -10);

            var details = BuildingUIElements.Panel("Details", root.transform, BuildingUIElements.Surface);
            BuildingUIElements.Anchors(details.rectTransform, new Vector2(.46f, .13f), new Vector2(.975f, .83f), Vector2.zero, Vector2.zero);

            var iconFrame = BuildingUIElements.Panel("IconFrame", details.transform, BuildingUIElements.SurfaceRaised);
            BuildingUIElements.Anchors(iconFrame.rectTransform, new Vector2(.045f, .75f), new Vector2(.285f, .96f), Vector2.zero, Vector2.zero);
            detailIcon = BuildingUIElements.Object("Icon", iconFrame.transform, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage)).GetComponent<RawImage>();
            detailIcon.raycastTarget = false;
            BuildingUIElements.Stretch(detailIcon.rectTransform, 10, 10, -10, -10);
            detailPlaceholder = BuildingUIElements.Label("Placeholder", iconFrame.transform, 38, TextAnchor.MiddleCenter);
            detailPlaceholder.color = BuildingUIElements.Accent;
            BuildingUIElements.Stretch(detailPlaceholder.rectTransform);

            detailName = BuildingUIElements.Label("Name", details.transform, 26);
            detailName.fontStyle = FontStyle.Bold;
            BuildingUIElements.Anchors(detailName.rectTransform, new Vector2(.32f, .84f), new Vector2(.95f, .96f), Vector2.zero, Vector2.zero);
            detailCategory = BuildingUIElements.Label("Category", details.transform, 15);
            detailCategory.color = BuildingUIElements.Accent;
            BuildingUIElements.Anchors(detailCategory.rectTransform, new Vector2(.32f, .76f), new Vector2(.95f, .85f), Vector2.zero, Vector2.zero);
            detailDescription = BuildingUIElements.Label("Description", details.transform, 17);
            BuildingUIElements.Anchors(detailDescription.rectTransform, new Vector2(.05f, .57f), new Vector2(.95f, .74f), Vector2.zero, Vector2.zero);
            detailCapability = BuildingUIElements.Label("Capability", details.transform, 16);
            detailCapability.color = new Color(.72f, .8f, .88f);
            BuildingUIElements.Anchors(detailCapability.rectTransform, new Vector2(.05f, .45f), new Vector2(.95f, .57f), Vector2.zero, Vector2.zero);
            detailCosts = BuildingUIElements.Label("Costs", details.transform, 17);
            BuildingUIElements.Anchors(detailCosts.rectTransform, new Vector2(.05f, .25f), new Vector2(.95f, .44f), Vector2.zero, Vector2.zero);
            detailReason = BuildingUIElements.Label("Reason", details.transform, 15);
            BuildingUIElements.Anchors(detailReason.rectTransform, new Vector2(.05f, .15f), new Vector2(.95f, .25f), Vector2.zero, Vector2.zero);
            placeButton = BuildingUIElements.Button("Place", details.transform, "POSICIONAR", PlaceSelected);
            BuildingUIElements.Anchors((RectTransform)placeButton.transform, new Vector2(.05f, .035f), new Vector2(.95f, .14f), Vector2.zero, Vector2.zero);

            root.SetActive(false);
        }

        internal void Show(
            IReadOnlyList<BuildableDefinition> definitions,
            Func<BuildableDefinition, BuildablePresentation> presentation,
            Action<BuildableDefinition> onPlace,
            Action close,
            Action save,
            Action load)
        {
            presenter = presentation;
            place = onPlace;
            RebuildCards(definitions);
            if (string.IsNullOrEmpty(selectedId) || definitions.All(definition => definition.id != selectedId))
                selectedId = definitions.FirstOrDefault()?.id;

            var closeButton = root.transform.Find("Close")?.GetComponent<Button>();
            if (closeButton == null)
            {
                closeButton = BuildingUIElements.Button("Close", root.transform, "FECHAR  [B / Esc]", close);
                BuildingUIElements.Anchors((RectTransform)closeButton.transform, new Vector2(.82f, .905f), new Vector2(.97f, .97f), Vector2.zero, Vector2.zero);
                var saveButton = BuildingUIElements.Button("Save", root.transform, "SALVAR", save);
                BuildingUIElements.Anchors((RectTransform)saveButton.transform, new Vector2(.025f, .035f), new Vector2(.18f, .105f), Vector2.zero, Vector2.zero);
                var loadButton = BuildingUIElements.Button("Load", root.transform, "CARREGAR", load);
                BuildingUIElements.Anchors((RectTransform)loadButton.transform, new Vector2(.19f, .035f), new Vector2(.345f, .105f), Vector2.zero, Vector2.zero);
                var note = BuildingUIElements.Label("SaveNote", root.transform, 13);
                note.text = "Checkpoint de infraestrutura • host solo";
                note.color = BuildingUIElements.Muted;
                BuildingUIElements.Anchors(note.rectTransform, new Vector2(.36f, .035f), new Vector2(.76f, .105f), Vector2.zero, Vector2.zero);
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
                var cardObject = BuildingUIElements.Panel("Buildable_" + definition.id, cardContent, BuildingUIElements.SurfaceRaised).gameObject;
                cardObject.AddComponent<LayoutElement>().minHeight = 124;
                var button = cardObject.AddComponent<Button>();
                button.targetGraphic = cardObject.GetComponent<Image>();
                var iconFrame = BuildingUIElements.Panel("IconFrame", cardObject.transform, new Color(.055f, .07f, .09f, 1f));
                BuildingUIElements.Anchors(iconFrame.rectTransform, new Vector2(.025f, .16f), new Vector2(.23f, .84f), Vector2.zero, Vector2.zero);
                var icon = BuildingUIElements.Object("Icon", iconFrame.transform, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage)).GetComponent<RawImage>();
                icon.raycastTarget = false;
                BuildingUIElements.Stretch(icon.rectTransform, 7, 7, -7, -7);
                var placeholder = BuildingUIElements.Label("Placeholder", iconFrame.transform, 26, TextAnchor.MiddleCenter);
                placeholder.color = BuildingUIElements.Accent;
                BuildingUIElements.Stretch(placeholder.rectTransform);
                var name = BuildingUIElements.Label("Name", cardObject.transform, 19);
                name.fontStyle = FontStyle.Bold;
                name.horizontalOverflow = HorizontalWrapMode.Overflow;
                BuildingUIElements.Anchors(name.rectTransform, new Vector2(.27f, .58f), new Vector2(.7f, .92f), Vector2.zero, Vector2.zero);
                var state = BuildingUIElements.Label("State", cardObject.transform, 12, TextAnchor.MiddleRight);
                state.horizontalOverflow = HorizontalWrapMode.Overflow;
                BuildingUIElements.Anchors(state.rectTransform, new Vector2(.72f, .59f), new Vector2(.97f, .92f), Vector2.zero, Vector2.zero);
                var category = BuildingUIElements.Label("Category", cardObject.transform, 13);
                category.color = BuildingUIElements.Muted;
                BuildingUIElements.Anchors(category.rectTransform, new Vector2(.27f, .43f), new Vector2(.97f, .65f), Vector2.zero, Vector2.zero);
                var costRow = BuildingUIElements.Object("Costs", cardObject.transform, typeof(RectTransform), typeof(HorizontalLayoutGroup));
                BuildingUIElements.Anchors((RectTransform)costRow.transform, new Vector2(.27f, .07f), new Vector2(.97f, .41f), Vector2.zero, Vector2.zero);
                var costLayout = costRow.GetComponent<HorizontalLayoutGroup>();
                costLayout.spacing = 6;
                costLayout.childControlWidth = true;
                costLayout.childForceExpandWidth = true;
                var card = new Card { Definition = definition, Button = button, Icon = icon, Placeholder = placeholder, Name = name, State = state, Category = category };
                var initialModel = presenter(definition);
                foreach (var cost in initialModel.Costs)
                {
                    var chip = BuildingUIElements.Panel("Cost_" + cost.MaterialId, costRow.transform, new Color(.05f, .065f, .085f, 1f));
                    chip.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
                    var costIcon = BuildingUIElements.Object("Icon", chip.transform, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage)).GetComponent<RawImage>();
                    costIcon.raycastTarget = false;
                    BuildingUIElements.Anchors(costIcon.rectTransform, new Vector2(.05f, .18f), new Vector2(.29f, .82f), Vector2.zero, Vector2.zero);
                    var costPlaceholder = BuildingUIElements.Label("Placeholder", chip.transform, 12, TextAnchor.MiddleCenter);
                    costPlaceholder.color = BuildingUIElements.Accent;
                    BuildingUIElements.Anchors(costPlaceholder.rectTransform, new Vector2(.05f, .18f), new Vector2(.29f, .82f), Vector2.zero, Vector2.zero);
                    var costValue = BuildingUIElements.Label("Value", chip.transform, 11, TextAnchor.MiddleCenter);
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
            card.Icon.texture = model.Definition.icon;
            card.Icon.enabled = model.Definition.icon != null;
            card.Placeholder.gameObject.SetActive(model.Definition.icon == null);
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
                chip.Value.color = cost.Missing == 0 ? BuildingUIElements.Positive : BuildingUIElements.Negative;
            }
            card.State.text = !model.IsUnlocked ? "BLOQUEADO" : !model.IsAvailable ? "INDISPONÍVEL" : model.IsAffordable ? "PRONTO" : "FALTA";
            card.State.color = model.CanPlace ? BuildingUIElements.Positive : !model.IsUnlocked ? BuildingUIElements.Muted : BuildingUIElements.Negative;
            card.Button.targetGraphic.color = card.Definition.id == selectedId
                ? new Color(.2f, .25f, .31f, 1f)
                : BuildingUIElements.SurfaceRaised;
        }

        private void RenderDetails(Card selected)
        {
            if (selected == null || presenter == null) return;
            var model = presenter(selected.Definition);
            detailIcon.texture = model.Definition.icon;
            detailIcon.enabled = model.Definition.icon != null;
            detailPlaceholder.gameObject.SetActive(model.Definition.icon == null);
            detailPlaceholder.text = Initials(model.Definition.displayName);
            detailName.text = model.Definition.displayName;
            detailCategory.text = model.Definition.category + " • " + model.Definition.station;
            detailDescription.text = model.Definition.description;
            detailCapability.text = "FUNÇÃO\n" + model.Capability;
            detailCosts.text = "CUSTO\n" + string.Join("\n", model.Costs.Select(cost =>
                (cost.Missing == 0 ? "✓  " : "!  ") + cost.DisplayName + "   " + cost.Owned + " / " + cost.Required +
                (cost.Missing > 0 ? "   (faltam " + cost.Missing + ")" : string.Empty)));
            detailReason.text = model.CanPlace ? "Tudo pronto para posicionar." : model.DisabledReason;
            detailReason.color = model.CanPlace ? BuildingUIElements.Positive : BuildingUIElements.Negative;
            placeButton.interactable = model.CanPlace;
            BuildingUIElements.SetButtonCaption(placeButton, model.CanPlace ? "POSICIONAR" : "INDISPONÍVEL");
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
