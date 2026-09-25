using System;
using System.Collections.Generic;
using Duskborn.Gameplay.Building;
using UnityEngine;
using UnityEngine.UI;

namespace Duskborn.UI.Building
{
    public sealed class BuildingUIManager
    {
        private readonly GameObject canvas;
        private readonly BuildingCatalogView catalog;
        private readonly PlacementHUDView placement;
        private readonly GameObject station;
        private readonly GameObject stationScroll;
        private readonly RectTransform stationContent;
        private readonly RectTransform forgeContent;
        private readonly Text stationTitle;
        private readonly Text stationSubtitle;
        private readonly GameObject confirmation;
        private readonly Text confirmationTitle;
        private readonly Text confirmationBody;
        private readonly Button confirmationAccept;
        private readonly Text toast;
        private readonly List<Action> stationRefreshers = new();
        private float toastUntil;
        private Action confirmationAction;
        private Action stationCloseAction;

        public bool IsPanelOpen => catalog.IsOpen || station.activeSelf || confirmation.activeSelf;

        private BuildingUIManager(Transform owner)
        {
            canvas = new GameObject("BuildingUI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.transform.SetParent(owner, false);
            var canvasComponent = canvas.GetComponent<Canvas>();
            canvasComponent.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasComponent.sortingOrder = 90;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .55f;

            catalog = new BuildingCatalogView(canvas.transform);
            placement = new PlacementHUDView(canvas.transform);

            station = BuildingUIElements.SolidPanel("StationPanel", canvas.transform, BuildingUIElements.Backdrop,
                new Color(.45f, .31f, .15f, .9f)).gameObject;
            BuildingUIElements.Anchors((RectTransform)station.transform, new Vector2(.08f, .12f), new Vector2(.48f, .9f), Vector2.zero, Vector2.zero);

            var stationHeader = BuildingUIElements.SolidPanel("Header", station.transform, new Color(.09f, .065f, .035f, .99f),
                new Color(.72f, .47f, .18f, .65f));
            BuildingUIElements.Anchors(stationHeader.rectTransform, new Vector2(.02f, .88f), new Vector2(.98f, .98f), Vector2.zero, Vector2.zero);
            stationTitle = BuildingUIElements.Label("Title", stationHeader.transform, 23);
            stationTitle.fontStyle = FontStyle.Bold;
            stationTitle.color = new Color(.98f, .82f, .38f, 1f);
            BuildingUIElements.Anchors(stationTitle.rectTransform, new Vector2(.035f, .38f), new Vector2(.82f, .97f), Vector2.zero, Vector2.zero);
            stationSubtitle = BuildingUIElements.Label("Subtitle", stationHeader.transform, 11);
            stationSubtitle.fontStyle = FontStyle.Bold;
            stationSubtitle.color = new Color(.70f, .65f, .55f, 1f);
            BuildingUIElements.Anchors(stationSubtitle.rectTransform, new Vector2(.035f, .04f), new Vector2(.82f, .42f), Vector2.zero, Vector2.zero);
            var closeButton = BuildingUIElements.SolidButton("CloseButton", stationHeader.transform, "X", () => stationCloseAction?.Invoke(), true);
            BuildingUIElements.Anchors((RectTransform)closeButton.transform, new Vector2(.90f, .20f), new Vector2(.975f, .80f), Vector2.zero, Vector2.zero);
            var closeLabel = closeButton.GetComponentInChildren<Text>();
            if (closeLabel != null) { closeLabel.fontSize = 17; closeLabel.fontStyle = FontStyle.Bold; }

            stationScroll = BuildingUIElements.Scroll("StationScroll", station.transform, out stationContent).gameObject;
            BuildingUIElements.Anchors((RectTransform)stationScroll.transform, new Vector2(.035f, .035f), new Vector2(.965f, .86f), Vector2.zero, Vector2.zero);
            forgeContent = (RectTransform)BuildingUIElements.Object("ForgeContent", station.transform, typeof(RectTransform)).transform;
            BuildingUIElements.Anchors(forgeContent, new Vector2(.035f, .045f), new Vector2(.965f, .86f), Vector2.zero, Vector2.zero);
            forgeContent.gameObject.SetActive(false);
            station.SetActive(false);

            confirmation = BuildingUIElements.Object("Confirmation", canvas.transform,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var blocker = confirmation.GetComponent<Image>();
            blocker.color = new Color(0f, 0f, 0f, .58f);
            blocker.raycastTarget = true;
            BuildingUIElements.Stretch((RectTransform)confirmation.transform);
            var dialog = BuildingUIElements.Panel("Dialog", confirmation.transform, new Color(.025f, .03f, .04f, .99f));
            BuildingUIElements.Anchors(dialog.rectTransform, new Vector2(.34f, .35f), new Vector2(.66f, .65f), Vector2.zero, Vector2.zero);
            confirmationTitle = BuildingUIElements.Label("Title", dialog.transform, 25, TextAnchor.MiddleCenter);
            confirmationTitle.fontStyle = FontStyle.Bold;
            BuildingUIElements.Anchors(confirmationTitle.rectTransform, new Vector2(.07f, .71f), new Vector2(.93f, .94f), Vector2.zero, Vector2.zero);
            confirmationBody = BuildingUIElements.Label("Body", dialog.transform, 17, TextAnchor.MiddleCenter);
            BuildingUIElements.Anchors(confirmationBody.rectTransform, new Vector2(.08f, .34f), new Vector2(.92f, .7f), Vector2.zero, Vector2.zero);
            var cancel = BuildingUIElements.Button("Cancel", dialog.transform, "CANCELAR", HideConfirmation);
            BuildingUIElements.Anchors((RectTransform)cancel.transform, new Vector2(.08f, .09f), new Vector2(.47f, .29f), Vector2.zero, Vector2.zero);
            confirmationAccept = BuildingUIElements.Button("Accept", dialog.transform, "CONFIRMAR", Confirm, true);
            BuildingUIElements.Anchors((RectTransform)confirmationAccept.transform, new Vector2(.53f, .09f), new Vector2(.92f, .29f), Vector2.zero, Vector2.zero);
            confirmation.SetActive(false);

            var toastPanel = BuildingUIElements.Panel("Toast", canvas.transform, new Color(.03f, .04f, .055f, .96f));
            BuildingUIElements.Anchors(toastPanel.rectTransform, new Vector2(.33f, .88f), new Vector2(.67f, .95f), Vector2.zero, Vector2.zero);
            toast = BuildingUIElements.Label("Message", toastPanel.transform, 17, TextAnchor.MiddleCenter);
            BuildingUIElements.Stretch(toast.rectTransform, 12, 6, -12, -6);
            toastPanel.gameObject.SetActive(false);
        }

        public static BuildingUIManager Create(Transform owner) => new(owner);

        public void Tick()
        {
            if (toast.transform.parent.gameObject.activeSelf && Time.unscaledTime >= toastUntil)
                toast.transform.parent.gameObject.SetActive(false);
            if (station.activeSelf)
                foreach (var refresh in stationRefreshers) refresh();
        }

        public void ShowCatalog(
            IReadOnlyList<BuildableDefinition> definitions,
            Func<BuildableDefinition, BuildablePresentation> presentation,
            Action<BuildableDefinition> place,
            Action close,
            Action save,
            Action load)
        {
            station.SetActive(false);
            confirmation.SetActive(false);
            catalog.Show(definitions, presentation, place, close, save, load);
        }

        public void RefreshCatalog() => catalog.Refresh();

        public void ShowPlacement(BuildablePresentation model, string invalidReason, float yaw, bool pending) =>
            placement.Render(model, invalidReason, yaw, pending);

        public void HidePlacement() => placement.Hide();

        public void BeginStation(string title, Action close)
        {
            catalog.Hide();
            confirmation.SetActive(false);
            stationRefreshers.Clear();
            ClearChildren(stationContent);
            ClearChildren(forgeContent);
            stationCloseAction = close;
            stationTitle.text = title;
            stationSubtitle.text = "ESTAÇÃO";
            BuildingUIElements.Anchors((RectTransform)station.transform, new Vector2(.08f, .12f), new Vector2(.48f, .9f), Vector2.zero, Vector2.zero);
            stationScroll.SetActive(true);
            forgeContent.gameObject.SetActive(false);
            station.SetActive(true);
        }

        public void BeginForgeStation(string title, Action close)
        {
            catalog.Hide();
            confirmation.SetActive(false);
            stationRefreshers.Clear();
            ClearChildren(stationContent);
            ClearChildren(forgeContent);
            stationCloseAction = close;
            stationTitle.text = title;
            stationSubtitle.text = "FUNDIÇÃO";
            BuildingUIElements.Anchors((RectTransform)station.transform, new Vector2(.09f, .18f), new Vector2(.47f, .82f), Vector2.zero, Vector2.zero);
            stationScroll.SetActive(false);
            forgeContent.gameObject.SetActive(true);
            station.SetActive(true);
        }

        public void AddStationLabel(Func<string> value, int height = 86)
        {
            var label = BuildingUIElements.Label("Info", stationContent, 17);
            label.gameObject.AddComponent<LayoutElement>().minHeight = height;
            label.text = value();
            stationRefreshers.Add(() => { if (label != null) label.text = value(); });
        }

        public void AddStationSection(string caption)
        {
            var label = BuildingUIElements.Label("Section", stationContent, 15);
            label.fontStyle = FontStyle.Bold;
            label.color = BuildingUIElements.Accent;
            label.gameObject.AddComponent<LayoutElement>().minHeight = 30;
            label.text = caption.ToUpperInvariant();
        }

        public void AddStationProgress(Func<string> caption, Func<float> progress, Func<bool> warning = null)
        {
            var row = BuildingUIElements.Panel("Progress", stationContent, BuildingUIElements.SurfaceRaised);
            row.gameObject.AddComponent<LayoutElement>().minHeight = 72;
            var label = BuildingUIElements.Label("Label", row.transform, 15);
            BuildingUIElements.Anchors(label.rectTransform, new Vector2(.03f, .48f), new Vector2(.97f, .94f), Vector2.zero, Vector2.zero);
            var track = BuildingUIElements.Panel("Track", row.transform, new Color(.025f, .035f, .05f, 1f));
            BuildingUIElements.Anchors(track.rectTransform, new Vector2(.03f, .16f), new Vector2(.97f, .4f), Vector2.zero, Vector2.zero);
            var fill = BuildingUIElements.Panel("Fill", track.transform, BuildingUIElements.Positive);
            fill.raycastTarget = false;
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.pivot = Vector2.zero;
            fill.rectTransform.offsetMin = Vector2.zero;
            fill.rectTransform.offsetMax = Vector2.zero;
            stationRefreshers.Add(() =>
            {
                if (label == null || fill == null) return;
                label.text = caption();
                bool isWarning = warning != null && warning();
                label.color = isWarning ? BuildingUIElements.Negative : BuildingUIElements.Text;
                fill.color = isWarning ? BuildingUIElements.Negative : BuildingUIElements.Positive;
                fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(progress()), 1f);
            });
        }

        public void AddForgeProcessor(
            Func<Texture2D> inputIcon,
            Func<Texture2D> fuelIcon,
            Func<Texture2D> outputIcon,
            Func<string> inputText,
            Func<string> fuelText,
            Func<string> outputText,
            Func<string> statusText,
            Func<float> progress,
            Func<float> flameProgress,
            Func<bool> warning,
            Action returnInput,
            Action returnFuel,
            Action collectOutput)
        {
            var root = BuildingUIElements.SolidPanel("ForgeProcessor", forgeContent, new Color(.045f, .055f, .07f, .98f),
                new Color(.24f, .28f, .34f, .95f));
            BuildingUIElements.Stretch(root.rectTransform);

            var hint = BuildingUIElements.Label("Hint", root.transform, 13, TextAnchor.MiddleCenter);
            hint.text = "BOTÃO DIREITO NO INVENTÁRIO PARA CARREGAR";
            hint.fontStyle = FontStyle.Bold;
            hint.color = new Color(.73f, .68f, .58f, 1f);
            BuildingUIElements.Anchors(hint.rectTransform, new Vector2(.03f, .89f), new Vector2(.97f, .98f), Vector2.zero, Vector2.zero);

            var input = ForgeSlot("InputSlot", root.transform, "MATERIAL", inputIcon?.Invoke(), null, returnInput, out var inputValue);
            BuildingUIElements.Anchors((RectTransform)input.transform, new Vector2(.055f, .53f), new Vector2(.30f, .86f), Vector2.zero, Vector2.zero);
            var inputImage = input.GetComponentInChildren<RawImage>();
            var fuel = ForgeSlot("FuelSlot", root.transform, "COMBUSTÍVEL", fuelIcon?.Invoke(), null, returnFuel, out var fuelValue);
            BuildingUIElements.Anchors((RectTransform)fuel.transform, new Vector2(.055f, .13f), new Vector2(.30f, .46f), Vector2.zero, Vector2.zero);
            var fuelImage = fuel.GetComponentInChildren<RawImage>();

            var flameBackObject = BuildingUIElements.Object("FlameBack", root.transform, typeof(RectTransform), typeof(CanvasRenderer), typeof(ForgeProgressGraphic));
            var flameBack = flameBackObject.GetComponent<ForgeProgressGraphic>();
            flameBack.color = new Color(.18f, .20f, .23f, 1f);
            flameBack.raycastTarget = false;
            BuildingUIElements.Anchors((RectTransform)flameBackObject.transform, new Vector2(.37f, .22f), new Vector2(.49f, .41f), Vector2.zero, Vector2.zero);
            var flameObject = BuildingUIElements.Object("FlameFill", root.transform, typeof(RectTransform), typeof(CanvasRenderer), typeof(ForgeProgressGraphic));
            var flame = flameObject.GetComponent<ForgeProgressGraphic>();
            flame.color = new Color(1f, .34f, .045f, 1f);
            flame.raycastTarget = false;
            BuildingUIElements.Anchors((RectTransform)flameObject.transform, new Vector2(.37f, .22f), new Vector2(.49f, .41f), Vector2.zero, Vector2.zero);
            var emberObject = BuildingUIElements.Object("FlameCore", root.transform, typeof(RectTransform), typeof(CanvasRenderer), typeof(ForgeProgressGraphic));
            var ember = emberObject.GetComponent<ForgeProgressGraphic>();
            ember.color = new Color(1f, .78f, .16f, 1f);
            ember.raycastTarget = false;
            BuildingUIElements.Anchors((RectTransform)emberObject.transform, new Vector2(.402f, .23f), new Vector2(.466f, .36f), Vector2.zero, Vector2.zero);

            var arrowTrack = BuildingUIElements.SolidPanel("ProgressTrack", root.transform, new Color(.025f, .03f, .04f, 1f),
                new Color(.28f, .31f, .35f, .9f));
            BuildingUIElements.Anchors(arrowTrack.rectTransform, new Vector2(.37f, .665f), new Vector2(.65f, .72f), Vector2.zero, Vector2.zero);
            var arrowFill = BuildingUIElements.SolidPanel("ProgressFill", arrowTrack.transform, BuildingUIElements.Accent);
            arrowFill.raycastTarget = false;
            arrowFill.rectTransform.anchorMin = Vector2.zero;
            arrowFill.rectTransform.anchorMax = new Vector2(0f, 1f);
            arrowFill.rectTransform.pivot = Vector2.zero;
            arrowFill.rectTransform.offsetMin = Vector2.zero;
            arrowFill.rectTransform.offsetMax = Vector2.zero;
            for (int i = 1; i < 5; i++)
            {
                float x = i / 5f;
                var tick = BuildingUIElements.SolidPanel("Tick", arrowTrack.transform, new Color(.02f, .025f, .032f, .7f));
                tick.raycastTarget = false;
                BuildingUIElements.Anchors(tick.rectTransform, new Vector2(x, .08f), new Vector2(x, .92f), new Vector2(-1f, 0f), new Vector2(1f, 0f));
            }
            var progressValue = BuildingUIElements.Label("ProgressValue", arrowTrack.transform, 13, TextAnchor.MiddleCenter);
            progressValue.fontStyle = FontStyle.Bold;
            BuildingUIElements.Stretch(progressValue.rectTransform);
            var arrowHead = BuildingUIElements.Label("ArrowHead", root.transform, 32, TextAnchor.MiddleCenter);
            arrowHead.text = "▶";
            arrowHead.color = BuildingUIElements.Accent;
            BuildingUIElements.Anchors(arrowHead.rectTransform, new Vector2(.69f, .63f), new Vector2(.735f, .76f), Vector2.zero, Vector2.zero);

            var output = ForgeSlot("OutputSlot", root.transform, "RESULTADO · RMB", outputIcon?.Invoke(), collectOutput, collectOutput, out var outputValue, true);
            // Keep the output cell square-ish at the panel's aspect ratio instead
            // of stretching it into a tall card. Its centre aligns to the arrow.
            BuildingUIElements.Anchors((RectTransform)output.transform, new Vector2(.745f, .56f), new Vector2(.96f, .82f), Vector2.zero, Vector2.zero);
            var outputImage = output.GetComponentInChildren<RawImage>();

            var statusPanel = BuildingUIElements.SolidPanel("StatusPanel", root.transform, new Color(.025f, .03f, .04f, .96f));
            BuildingUIElements.Anchors(statusPanel.rectTransform, new Vector2(.34f, .06f), new Vector2(.97f, .19f), Vector2.zero, Vector2.zero);
            var status = BuildingUIElements.Label("Status", statusPanel.transform, 14, TextAnchor.MiddleCenter);
            BuildingUIElements.Stretch(status.rectTransform, 8, 2, -8, -2);

            stationRefreshers.Add(() =>
            {
                if (root == null) return;
                float value = Mathf.Clamp01(progress());
                bool isWarning = warning != null && warning();
                SetForgeSlotIcon(inputImage, inputIcon?.Invoke());
                SetForgeSlotIcon(fuelImage, fuelIcon?.Invoke());
                SetForgeSlotIcon(outputImage, outputIcon?.Invoke());
                inputValue.text = inputText();
                fuelValue.text = fuelText();
                outputValue.text = outputText();
                status.text = statusText();
                status.color = isWarning ? BuildingUIElements.Negative : BuildingUIElements.Text;
                arrowFill.color = isWarning ? BuildingUIElements.Negative : BuildingUIElements.Accent;
                arrowHead.color = arrowFill.color;
                arrowFill.rectTransform.anchorMax = new Vector2(value, 1f);
                progressValue.text = value > 0f ? Mathf.RoundToInt(value * 100f) + "%" : "—";
                flame.color = isWarning ? BuildingUIElements.Negative : new Color(1f, .46f, .08f, 1f);
                float fire = Mathf.Clamp01(flameProgress());
                flame.Progress = fire;
                ember.Progress = Mathf.Clamp01(fire * 1.18f);
                flame.Phase = Time.unscaledTime;
                ember.Phase = Time.unscaledTime + .65f;
            });
        }

        public void AddStationButton(string caption, Action action, Func<bool> enabled = null, bool danger = false) =>
            AddStationButton(() => caption, action, enabled, danger);

        public void AddStationButton(Func<string> caption, Action action, Func<bool> enabled = null, bool danger = false)
        {
            var button = BuildingUIElements.Button("Action", stationContent, caption(), action, danger);
            button.gameObject.AddComponent<LayoutElement>().minHeight = 72;
            var label = button.GetComponentInChildren<Text>();
            stationRefreshers.Add(() =>
            {
                bool interactable = enabled == null || enabled();
                button.interactable = interactable;
                if (label != null) label.text = caption();
            });
        }

        public void AddStorageRow(
            string materialName,
            Texture2D icon,
            Func<int> playerAmount,
            Func<int> storedAmount,
            Action depositOne,
            Action depositStack,
            Action withdrawOne,
            Action withdrawStack,
            Func<bool> canDeposit,
            Func<bool> canWithdraw)
        {
            var row = BuildingUIElements.Panel("Material_" + materialName, stationContent, BuildingUIElements.SurfaceRaised);
            row.gameObject.AddComponent<LayoutElement>().minHeight = 112;
            var iconFrame = BuildingUIElements.Panel("IconFrame", row.transform, new Color(.04f, .055f, .075f, 1f));
            BuildingUIElements.Anchors(iconFrame.rectTransform, new Vector2(.02f, .18f), new Vector2(.13f, .82f), Vector2.zero, Vector2.zero);
            var rawIcon = BuildingUIElements.Object("Icon", iconFrame.transform, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage)).GetComponent<RawImage>();
            rawIcon.texture = icon;
            rawIcon.enabled = icon != null;
            rawIcon.raycastTarget = false;
            BuildingUIElements.Stretch(rawIcon.rectTransform, 5, 5, -5, -5);
            var fallback = BuildingUIElements.Label("Fallback", iconFrame.transform, 15, TextAnchor.MiddleCenter);
            fallback.text = string.IsNullOrEmpty(materialName) ? "?" : materialName.Substring(0, 1).ToUpperInvariant();
            fallback.gameObject.SetActive(icon == null);
            BuildingUIElements.Stretch(fallback.rectTransform);
            var name = BuildingUIElements.Label("Name", row.transform, 16);
            name.fontStyle = FontStyle.Bold;
            name.text = materialName;
            BuildingUIElements.Anchors(name.rectTransform, new Vector2(.15f, .57f), new Vector2(.43f, .92f), Vector2.zero, Vector2.zero);
            var counts = BuildingUIElements.Label("Counts", row.transform, 14);
            BuildingUIElements.Anchors(counts.rectTransform, new Vector2(.15f, .15f), new Vector2(.43f, .57f), Vector2.zero, Vector2.zero);
            var deposit1 = MiniButton(row.transform, "+1", depositOne, .45f, .57f);
            var depositAll = MiniButton(row.transform, "+ PILHA", depositStack, .58f, .73f);
            var withdraw1Button = MiniButton(row.transform, "−1", withdrawOne, .75f, .87f);
            var withdrawAllButton = MiniButton(row.transform, "− PILHA", withdrawStack, .88f, .99f);
            stationRefreshers.Add(() =>
            {
                if (counts == null) return;
                counts.text = "JOGADOR  " + playerAmount() + "\nBAÚ  " + storedAmount();
                deposit1.interactable = depositAll.interactable = canDeposit();
                withdraw1Button.interactable = withdrawAllButton.interactable = canWithdraw();
            });
        }

        public void ShowConfirmation(string title, string body, string acceptCaption, Action accept)
        {
            confirmationTitle.text = title;
            confirmationBody.text = body;
            BuildingUIElements.SetButtonCaption(confirmationAccept, acceptCaption);
            confirmationAction = accept;
            confirmation.SetActive(true);
        }

        public void HideConfirmation()
        {
            confirmation.SetActive(false);
            confirmationAction = null;
        }

        public void HidePanels()
        {
            catalog.Hide();
            station.SetActive(false);
            HideConfirmation();
        }

        public void ShowToast(string message, bool error = false, float seconds = 4f)
        {
            toast.text = message;
            toast.color = error ? new Color(1f, .72f, .7f) : new Color(.75f, 1f, .83f);
            toast.transform.parent.gameObject.SetActive(true);
            toastUntil = Time.unscaledTime + seconds;
        }

        private void Confirm()
        {
            var action = confirmationAction;
            HideConfirmation();
            action?.Invoke();
        }

        private static void ClearChildren(Transform parent)
        {
            foreach (Transform child in parent)
            {
                child.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(child.gameObject);
            }
        }

        private static void SetForgeSlotIcon(RawImage image, Texture2D texture)
        {
            if (image == null) return;
            image.texture = texture;
            image.enabled = texture != null;
        }

        private static Button MiniButton(Transform parent, string caption, Action action, float minX, float maxX)
        {
            var button = BuildingUIElements.Button("Transfer", parent, caption, action);
            BuildingUIElements.Anchors((RectTransform)button.transform, new Vector2(minX, .2f), new Vector2(maxX, .8f), Vector2.zero, Vector2.zero);
            var label = button.GetComponentInChildren<Text>();
            if (label != null) label.fontSize = 13;
            return button;
        }

        private static Button ForgeSlot(
            string name,
            Transform parent,
            string caption,
            Texture2D icon,
            Action leftClick,
            Action rightClick,
            out Text value,
            bool highlighted = false)
        {
            var button = BuildingUIElements.SolidButton(name, parent, string.Empty, leftClick);
            var slotImage = button.GetComponent<Image>();
            if (highlighted && slotImage != null)
            {
                var outline = button.gameObject.GetComponent<Outline>();
                if (outline != null) outline.effectColor = new Color(.85f, .58f, .2f, 1f);
            }
            if (rightClick != null)
                button.gameObject.AddComponent<ForgeSlotClickHandler>().RightClick = rightClick;
            var title = button.GetComponentInChildren<Text>();
            if (title != null)
            {
                title.text = caption;
                title.fontSize = 12;
                title.fontStyle = FontStyle.Bold;
                title.alignment = TextAnchor.UpperCenter;
                title.color = highlighted ? BuildingUIElements.Accent : BuildingUIElements.Text;
                BuildingUIElements.Anchors(title.rectTransform, new Vector2(.04f, .77f), new Vector2(.96f, .97f), Vector2.zero, Vector2.zero);
            }
            var iconFrame = BuildingUIElements.SolidPanel("IconFrame", button.transform, new Color(.025f, .035f, .05f, 1f),
                new Color(.25f, .29f, .34f, 1f));
            iconFrame.raycastTarget = false;
            BuildingUIElements.Anchors(iconFrame.rectTransform, new Vector2(.27f, .32f), new Vector2(.73f, .74f), Vector2.zero, Vector2.zero);
            var raw = BuildingUIElements.Object("Icon", iconFrame.transform, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage)).GetComponent<RawImage>();
            raw.texture = icon;
            raw.enabled = icon != null;
            raw.raycastTarget = false;
            BuildingUIElements.Stretch(raw.rectTransform, 5, 5, -5, -5);
            value = BuildingUIElements.Label("Value", button.transform, 11, TextAnchor.MiddleCenter);
            BuildingUIElements.Anchors(value.rectTransform, new Vector2(.04f, .025f), new Vector2(.96f, .31f), Vector2.zero, Vector2.zero);
            return button;
        }
    }
}
