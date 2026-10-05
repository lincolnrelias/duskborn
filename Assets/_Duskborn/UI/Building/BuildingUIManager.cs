using System;
using System.Collections.Generic;
using Duskborn.Gameplay.Building;
using UnityEngine;
using UnityEngine.UI;

namespace Duskborn.UI.Building
{
    public sealed class ForgeInventoryOption
    {
        public string Id;
        public string Name;
        public Texture2D Icon;
        public int Owned;
        public string Blocker;
    }

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
        private readonly Image forgeFrame;
        private readonly RawImage forgeStationIcon;
        private GameObject forgePicker;
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
            forgeFrame = BriarwoodCatalogTheme.Sprite("BriarwoodFrame", station.transform, "frame", true);
            forgeFrame.sprite = BriarwoodCatalogTheme.ForgeSprite("frame");
            forgeFrame.pixelsPerUnitMultiplier = 3f;
            BuildingUIElements.Stretch(forgeFrame.rectTransform, -10, -10, 10, 10);
            forgeFrame.gameObject.SetActive(false);
            var forgeIconHolder = BriarwoodCatalogTheme.Sprite("ForgeCrest", station.transform, "socket", true);
            forgeIconHolder.sprite = BriarwoodCatalogTheme.ForgeSprite("socket");
            forgeIconHolder.pixelsPerUnitMultiplier = 6f;
            forgeIconHolder.rectTransform.anchorMin = forgeIconHolder.rectTransform.anchorMax = new Vector2(.5f, 1f);
            forgeIconHolder.rectTransform.sizeDelta = new Vector2(92, 92);
            forgeIconHolder.rectTransform.anchoredPosition = new Vector2(0, 8);
            forgeStationIcon = BuildingUIElements.Object("ForgeIcon", forgeIconHolder.transform,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage)).GetComponent<RawImage>();
            forgeStationIcon.texture = Resources.Load<Texture2D>("UI/Briarwood/Icon_forge");
            forgeStationIcon.raycastTarget = false;
            // Trim the model capture's alpha padding without distorting its silhouette.
            forgeStationIcon.uvRect = new Rect(136f / 512f, 52f / 512f, 240f / 512f, 394f / 512f);
            forgeStationIcon.rectTransform.anchorMin = forgeStationIcon.rectTransform.anchorMax = new Vector2(.5f, .5f);
            forgeStationIcon.rectTransform.sizeDelta = new Vector2(44, 72);
            forgeIconHolder.gameObject.SetActive(false);
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
            var cancel = BuildingUIElements.Button("Cancel", dialog.transform, "CANCEL", HideConfirmation);
            BuildingUIElements.Anchors((RectTransform)cancel.transform, new Vector2(.08f, .09f), new Vector2(.47f, .29f), Vector2.zero, Vector2.zero);
            confirmationAccept = BuildingUIElements.Button("Accept", dialog.transform, "CONFIRM", Confirm, true);
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
            Action close)
        {
            station.SetActive(false);
            confirmation.SetActive(false);
            catalog.Show(definitions, presentation, place, close);
        }

        public void RefreshCatalog() => catalog.Refresh();

        public void ShowPlacement(BuildablePresentation model, string invalidReason, float yaw, bool pending) =>
            placement.Render(model, invalidReason, yaw, pending);

        public void HidePlacement() => placement.Hide();

        public void BeginStation(string title, Action close)
        {
            catalog.Hide();
            confirmation.SetActive(false);
            CloseForgePicker();
            stationRefreshers.Clear();
            ClearChildren(stationContent);
            ClearChildren(forgeContent);
            stationCloseAction = close;
            stationTitle.text = title;
            stationSubtitle.text = "STATION";
            SetForgeSkin(false);
            ((RectTransform)station.transform).pivot = new Vector2(.5f, .5f);
            BuildingUIElements.Anchors((RectTransform)station.transform, new Vector2(.08f, .12f), new Vector2(.48f, .9f), Vector2.zero, Vector2.zero);
            stationScroll.SetActive(true);
            forgeContent.gameObject.SetActive(false);
            station.SetActive(true);
        }

        public void BeginForgeStation(string title, Action close)
        {
            catalog.Hide();
            confirmation.SetActive(false);
            CloseForgePicker();
            stationRefreshers.Clear();
            ClearChildren(stationContent);
            ClearChildren(forgeContent);
            stationCloseAction = close;
            stationTitle.text = title;
            stationSubtitle.text = "SMELTING";
            SetForgeSkin(true);
            BuildingUIElements.Anchors((RectTransform)station.transform, new Vector2(.065f, .5f), new Vector2(.065f, .5f), Vector2.zero, Vector2.zero);
            var forgeRect = (RectTransform)station.transform;
            forgeRect.pivot = new Vector2(0, .5f);
            forgeRect.sizeDelta = new Vector2(700, 580);
            stationScroll.SetActive(false);
            forgeContent.gameObject.SetActive(true);
            station.SetActive(true);
        }

        private void SetForgeSkin(bool forge)
        {
            forgeFrame.gameObject.SetActive(forge);
            forgeStationIcon.transform.parent.gameObject.SetActive(forge);
            var stationSurface = station.GetComponent<Image>();
            stationSurface.color = forge ? Color.white : BuildingUIElements.Backdrop;
            stationSurface.sprite = forge ? BriarwoodCatalogTheme.ForgeSprite("surface") : null;
            stationSurface.type = forge ? Image.Type.Sliced : Image.Type.Simple;
            stationSurface.pixelsPerUnitMultiplier = 6f;
            var header = stationTitle.transform.parent.GetComponent<Image>();
            BuildingUIElements.Anchors(header.rectTransform, new Vector2(forge ? .035f : .02f, forge ? .82f : .88f),
                new Vector2(forge ? .965f : .98f, forge ? .94f : .98f), Vector2.zero, Vector2.zero);
            BuildingUIElements.Anchors(forgeContent, new Vector2(.07f, .12f), new Vector2(.93f, .80f), Vector2.zero, Vector2.zero);
            header.color = forge ? new Color(.8f, .72f, .60f) : new Color(.09f, .065f, .035f, .99f);
            header.sprite = forge ? BriarwoodCatalogTheme.ForgeSprite("surface") : null;
            header.type = forge ? Image.Type.Sliced : Image.Type.Simple;
            header.pixelsPerUnitMultiplier = 6f;
            stationTitle.font = forge ? Resources.Load<Font>("UI/Briarwood/AlegreyaSC-Bold") : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            stationTitle.color = forge ? BriarwoodCatalogTheme.Ink : new Color(.98f, .82f, .38f, 1f);
            stationTitle.fontSize = forge ? 28 : 23;
            stationSubtitle.fontSize = forge ? 14 : 11;
            stationSubtitle.color = forge ? BriarwoodCatalogTheme.Muted : new Color(.70f, .65f, .55f, 1f);
            BuildingUIElements.Anchors(stationTitle.rectTransform, new Vector2(.035f, .38f), new Vector2(.82f, .97f), Vector2.zero, Vector2.zero);
            BuildingUIElements.Anchors(stationSubtitle.rectTransform, new Vector2(.035f, .04f), new Vector2(.82f, .42f), Vector2.zero, Vector2.zero);
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
            Action collectOutput,
            Func<bool, IReadOnlyList<ForgeInventoryOption>> inventoryOptions = null,
            Action<string, bool> loadItem = null,
            Func<bool> pending = null)
        {
            var root = BriarwoodCatalogTheme.Panel("ForgeProcessor", forgeContent, Color.clear);
            BuildingUIElements.Stretch(root.rectTransform);

            var hint = BuildingUIElements.Label("Hint", root.transform, 18, TextAnchor.MiddleCenter);
            hint.text = "Click MATERIAL or FUEL to choose from your inventory.";
            hint.fontStyle = FontStyle.Bold;
            hint.color = BriarwoodCatalogTheme.Muted;
            BuildingUIElements.Anchors(hint.rectTransform, new Vector2(.03f, .89f), new Vector2(.97f, .98f), Vector2.zero, Vector2.zero);

            var input = ForgeSlot("InputSlot", root.transform, "MATERIAL", inputIcon?.Invoke(), () => ShowForgePicker(false, inventoryOptions, loadItem), returnInput, out var inputValue);
            BuildingUIElements.Anchors((RectTransform)input.transform, new Vector2(.035f, .47f), new Vector2(.325f, .88f), Vector2.zero, Vector2.zero);
            var inputImage = input.GetComponentInChildren<RawImage>();
            var fuel = ForgeSlot("FuelSlot", root.transform, "FUEL", fuelIcon?.Invoke(), () => ShowForgePicker(true, inventoryOptions, loadItem), returnFuel, out var fuelValue);
            BuildingUIElements.Anchors((RectTransform)fuel.transform, new Vector2(.035f, .04f), new Vector2(.325f, .45f), Vector2.zero, Vector2.zero);
            var fuelImage = fuel.GetComponentInChildren<RawImage>();

            var flameObject = BuildingUIElements.Object("FlameFill", root.transform, typeof(RectTransform), typeof(CanvasRenderer), typeof(ForgeProgressGraphic));
            var flame = flameObject.GetComponent<ForgeProgressGraphic>();
            flame.raycastTarget = false;
            BuildingUIElements.Anchors(flame.rectTransform, new Vector2(.405f, .29f), new Vector2(.605f, .63f), Vector2.zero, Vector2.zero);
            var arrowTrack = BriarwoodCatalogTheme.Sprite("ProgressTrack", root.transform, "socket", true);
            arrowTrack.sprite = BriarwoodCatalogTheme.ForgeSprite("socket");
            arrowTrack.pixelsPerUnitMultiplier = 12f;
            arrowTrack.raycastTarget = false;
            BuildingUIElements.Anchors(arrowTrack.rectTransform, new Vector2(.37f, .68f), new Vector2(.60f, .80f), Vector2.zero, Vector2.zero);
            var well = BuildingUIElements.SolidPanel("Recess", arrowTrack.transform, Color.clear);
            well.raycastTarget = false;
            BuildingUIElements.Stretch(well.rectTransform, 8, 8, -8, -8);
            var arrowFill = BuildingUIElements.SolidPanel("ProgressFill", well.transform, BriarwoodCatalogTheme.Positive);
            arrowFill.raycastTarget = false;
            arrowFill.rectTransform.anchorMin = Vector2.zero;
            arrowFill.rectTransform.anchorMax = new Vector2(0f, 1f);
            arrowFill.rectTransform.pivot = Vector2.zero;
            arrowFill.rectTransform.offsetMin = Vector2.zero;
            arrowFill.rectTransform.offsetMax = Vector2.zero;
            var grain = BriarwoodCatalogTheme.ForgeSurface("PaintGrain", arrowFill.transform, new Color(1, 1, 1, .25f));
            grain.raycastTarget = false;
            BuildingUIElements.Stretch(grain.rectTransform);
            var shine = BuildingUIElements.SolidPanel("BevelLight", arrowFill.transform, new Color(1f, .94f, .72f, .35f));
            shine.raycastTarget = false;
            BuildingUIElements.Anchors(shine.rectTransform, new Vector2(0, .78f), Vector2.one, Vector2.zero, Vector2.zero);
            var shade = BuildingUIElements.SolidPanel("BevelShadow", arrowFill.transform, new Color(0, 0, 0, .25f));
            shade.raycastTarget = false;
            BuildingUIElements.Anchors(shade.rectTransform, Vector2.zero, new Vector2(1, .18f), Vector2.zero, Vector2.zero);
            for (int i = 1; i < 5; i++)
            {
                float x = i / 5f;
                var tick = BuildingUIElements.SolidPanel("Tick", well.transform, new Color(.91f, .69f, .34f, .28f));
                tick.raycastTarget = false;
                BuildingUIElements.Anchors(tick.rectTransform, new Vector2(x, .08f), new Vector2(x, .92f), new Vector2(-1f, 0f), new Vector2(1f, 0f));
            }
            var progressValue = BuildingUIElements.Label("ProgressValue", well.transform, 16, TextAnchor.MiddleCenter);
            progressValue.fontStyle = FontStyle.Bold;
            BuildingUIElements.Stretch(progressValue.rectTransform);
            var arrowHead = BuildingUIElements.Label("ArrowHead", root.transform, 24, TextAnchor.MiddleCenter);
            arrowHead.text = "▶";
            arrowHead.color = BriarwoodCatalogTheme.Brass;
            BuildingUIElements.Anchors(arrowHead.rectTransform, new Vector2(.61f, .665f), new Vector2(.66f, .79f), Vector2.zero, Vector2.zero);

            var output = ForgeSlot("OutputSlot", root.transform, "OUTPUT", outputIcon?.Invoke(), collectOutput, collectOutput, out var outputValue, true);
            // Keep the output cell square-ish at the panel's aspect ratio instead
            // of stretching it into a tall card. Its centre aligns to the arrow.
            BuildingUIElements.Anchors((RectTransform)output.transform, new Vector2(.665f, .47f), new Vector2(.955f, .88f), Vector2.zero, Vector2.zero);
            var outputImage = output.GetComponentInChildren<RawImage>();

            var statusPanel = BriarwoodCatalogTheme.ForgeSurface("StatusPanel", root.transform, new Color(.8f, .72f, .60f));
            BuildingUIElements.Anchors(statusPanel.rectTransform, new Vector2(.36f, .05f), new Vector2(.965f, .27f), Vector2.zero, Vector2.zero);
            var status = BriarwoodCatalogTheme.Label("Status", statusPanel.transform, 17, TextAnchor.MiddleCenter);
            BuildingUIElements.Stretch(status.rectTransform, 14, 8, -14, -8);
            var slotHint = BriarwoodCatalogTheme.Label("SlotHint", root.transform, 15, TextAnchor.MiddleCenter);
            slotHint.text = "Loaded / Need • Right-click to return • Click output to collect";
            slotHint.color = BriarwoodCatalogTheme.Muted;
            BuildingUIElements.Anchors(slotHint.rectTransform, new Vector2(0, -.045f), new Vector2(1, .01f), Vector2.zero, Vector2.zero);

            stationRefreshers.Add(() =>
            {
                if (root == null) return;
                float value = Mathf.Clamp01(progress());
                bool isWarning = warning != null && warning();
                SetForgeSlotIcon(inputImage, inputIcon?.Invoke());
                SetForgeSlotIcon(fuelImage, fuelIcon?.Invoke());
                SetForgeSlotIcon(outputImage, outputIcon?.Invoke());
                var materialSummary = inputText();
                var fuelSummary = fuelText();
                inputValue.text = string.IsNullOrEmpty(materialSummary) ? "Empty" : materialSummary;
                fuelValue.text = string.IsNullOrEmpty(fuelSummary) ? "Empty" : fuelSummary;
                outputValue.text = outputText();
                status.text = statusText();
                status.color = isWarning ? BriarwoodCatalogTheme.Negative : BriarwoodCatalogTheme.Ink;
                arrowFill.color = isWarning ? BriarwoodCatalogTheme.Negative : BriarwoodCatalogTheme.Positive;
                arrowHead.color = arrowFill.color;
                arrowFill.rectTransform.anchorMax = new Vector2(value, 1f);
                progressValue.text = value > 0f ? Mathf.RoundToInt(value * 100f) + "%" : "—";
                float fire = Mathf.Clamp01(flameProgress());
                flame.Progress = fire;
                flame.Phase = Time.unscaledTime;
                bool canAct = pending == null || !pending();
                input.interactable = fuel.interactable = output.interactable = canAct;
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
            var depositAll = MiniButton(row.transform, "+ STACK", depositStack, .58f, .73f);
            var withdraw1Button = MiniButton(row.transform, "−1", withdrawOne, .75f, .87f);
            var withdrawAllButton = MiniButton(row.transform, "− STACK", withdrawStack, .88f, .99f);
            stationRefreshers.Add(() =>
            {
                if (counts == null) return;
                counts.text = "PLAYER  " + playerAmount() + "\nCHEST  " + storedAmount();
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
            CloseForgePicker();
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
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                child.gameObject.SetActive(false);
                if (Application.isPlaying) UnityEngine.Object.Destroy(child.gameObject);
                else UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }

        private static void SetForgeSlotIcon(RawImage image, Texture2D texture)
        {
            if (image == null) return;
            image.texture = texture;
            image.enabled = texture != null;
        }

        private void CloseForgePicker()
        {
            if (forgePicker == null) return;
            forgePicker.SetActive(false);
            if (Application.isPlaying) UnityEngine.Object.Destroy(forgePicker);
            else UnityEngine.Object.DestroyImmediate(forgePicker);
            forgePicker = null;
        }

        private void ShowForgePicker(bool fuel, Func<bool, IReadOnlyList<ForgeInventoryOption>> options, Action<string, bool> load)
        {
            CloseForgePicker();
            if (options == null || load == null) return;
            // A transparent dismissal layer intercepts clicks outside the dropdown.
            var dismiss = BuildingUIElements.SolidPanel("ForgePicker", station.transform, Color.clear);
            forgePicker = dismiss.gameObject;
            BuildingUIElements.Stretch(dismiss.rectTransform);
            dismiss.gameObject.AddComponent<Button>().onClick.AddListener(CloseForgePicker);
            var panel = BriarwoodCatalogTheme.ForgeSurface("Dropdown", dismiss.transform, new Color(.85f, .78f, .65f));
            panel.rectTransform.anchorMin = panel.rectTransform.anchorMax = new Vector2(0, 1);
            panel.rectTransform.pivot = new Vector2(0, 1);
            var border = panel.gameObject.AddComponent<Outline>();
            border.effectColor = BriarwoodCatalogTheme.Brass;
            var title = BriarwoodCatalogTheme.Label("Title", panel.transform, 20, TextAnchor.MiddleLeft, true);
            title.text = fuel ? "CHOOSE FUEL" : "CHOOSE MATERIAL";
            BuildingUIElements.Anchors(title.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(16, -46), new Vector2(-64, -8));
            var close = BriarwoodCatalogTheme.Button("Close", panel.transform, "X", CloseForgePicker);
            BuildingUIElements.Anchors((RectTransform)close.transform, Vector2.one, Vector2.one, new Vector2(-50, -42), new Vector2(-12, -10));
            var closeText = close.GetComponentInChildren<Text>();
            closeText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            closeText.fontSize = 17;
            BuildingUIElements.Stretch(closeText.rectTransform, 2, 2, -2, -2);
            var scroll = BuildingUIElements.Scroll("Items", panel.transform, out var content, true);
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = false;
            BuildingUIElements.Stretch((RectTransform)scroll.transform, 12, 12, -12, -54);
            // Replace the station list layout with fixed square inventory tiles.
            var grid = content.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(128, 128);
            grid.spacing = new Vector2(8, 8);
            grid.padding = new RectOffset(6, 6, 6, 6);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperLeft;
            var empty = BriarwoodCatalogTheme.Label("Empty", panel.transform, 18, TextAnchor.MiddleCenter);
            empty.text = "No matching items in your inventory.";
            BuildingUIElements.Stretch(empty.rectTransform, 16, 16, -16, -54);
            empty.gameObject.SetActive(false);
            string previous = null;
            float nextRefresh = 0;
            void Refresh()
            {
                if (dismiss == null || !dismiss.gameObject.activeSelf || Time.unscaledTime < nextRefresh) return;
                nextRefresh = Time.unscaledTime + .2f;
                var current = options(fuel);
                string key = string.Empty;
                if (current != null)
                    foreach (var item in current) key += item.Id + ":" + item.Owned + ":" + item.Blocker + ";";
                if (key == previous) return;
                previous = key;
                ClearChildren(content);
                int count = current?.Count ?? 0;
                float height = Mathf.Min(356, 78 + Mathf.Max(1, Mathf.CeilToInt(count / 4f)) * 136);
                panel.rectTransform.sizeDelta = new Vector2(588, height);
                panel.rectTransform.anchoredPosition = new Vector2(56, -Mathf.Min(fuel ? 320 : 190, 540 - height));
                empty.gameObject.SetActive(count == 0);
                scroll.gameObject.SetActive(count > 0);
                if (current == null || current.Count == 0)
                {
                    return;
                }
                foreach (var item in current)
                {
                    var captured = item;
                    var row = BriarwoodCatalogTheme.Button("Item_" + item.Id, content, string.Empty, () =>
                    {
                        // Recheck the current inventory and pending state before submitting.
                        var latest = options(fuel);
                        if (latest == null) return;
                        foreach (var candidate in latest)
                        {
                            if (candidate.Id != captured.Id || candidate.Owned <= 0 || !string.IsNullOrEmpty(candidate.Blocker)) continue;
                            CloseForgePicker();
                            load(captured.Id, fuel);
                            return;
                        }
                    });
                    row.GetComponent<Image>().sprite = BriarwoodCatalogTheme.ForgeSprite("socket");
                    row.GetComponent<Image>().pixelsPerUnitMultiplier = 8f;
                    row.interactable = item.Owned > 0 && string.IsNullOrEmpty(item.Blocker);
                    var text = row.GetComponentInChildren<Text>();
                    text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    text.fontSize = 14;
                    text.alignment = TextAnchor.MiddleCenter;
                    text.text = item.Name;
                    BuildingUIElements.Anchors(text.rectTransform, new Vector2(.08f, .07f), new Vector2(.92f, .34f), Vector2.zero, Vector2.zero);
                    var amount = BriarwoodCatalogTheme.Label("Owned", row.transform, 13, TextAnchor.MiddleCenter);
                    amount.text = "Have " + item.Owned;
                    amount.color = BriarwoodCatalogTheme.Brass;
                    BuildingUIElements.Anchors(amount.rectTransform, new Vector2(.08f, .81f), new Vector2(.92f, .97f), Vector2.zero, Vector2.zero);
                    var icon = BuildingUIElements.Object("Icon", row.transform, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage)).GetComponent<RawImage>();
                    icon.texture = item.Icon;
                    icon.enabled = item.Icon != null;
                    icon.raycastTarget = false;
                    icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(.5f, .57f);
                    icon.rectTransform.sizeDelta = new Vector2(50, 50);
                    if (item.Icon == null && string.IsNullOrEmpty(item.Blocker))
                    {
                        var fallback = BriarwoodCatalogTheme.Label("Fallback", row.transform, 28, TextAnchor.MiddleCenter, true);
                        fallback.text = string.IsNullOrEmpty(item.Name) ? "?" : item.Name.Substring(0, 1).ToUpperInvariant();
                        fallback.color = BriarwoodCatalogTheme.Muted;
                        BuildingUIElements.Anchors(fallback.rectTransform, new Vector2(.2f, .36f), new Vector2(.8f, .78f), Vector2.zero, Vector2.zero);
                    }
                    if (!string.IsNullOrEmpty(item.Blocker))
                    {
                        icon.enabled = false;
                        var reason = BriarwoodCatalogTheme.Label("Blocker", row.transform, 12, TextAnchor.MiddleCenter);
                        reason.color = BriarwoodCatalogTheme.Negative;
                        reason.text = item.Blocker;
                        BuildingUIElements.Anchors(reason.rectTransform, new Vector2(.08f, .34f), new Vector2(.92f, .80f), Vector2.zero, Vector2.zero);
                    }
                }
            }
            // Remove old dropdown refreshers when reopening, avoiding accumulated closures.
            stationRefreshers.RemoveAll(refresh => refresh.Target is ForgePickerRefresh);
            var pickerRefresh = new ForgePickerRefresh { Refresh = Refresh };
            stationRefreshers.Add(pickerRefresh.Tick);
            Refresh();
        }

        private sealed class ForgePickerRefresh
        {
            internal Action Refresh;
            internal void Tick() => Refresh();
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
            var button = BriarwoodCatalogTheme.Button(name, parent, string.Empty, leftClick, highlighted);
            var slotImage = button.GetComponent<Image>();
            slotImage.sprite = BriarwoodCatalogTheme.ForgeSprite("socket");
            slotImage.pixelsPerUnitMultiplier = 4f;
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
                title.fontSize = 20;
                title.fontStyle = FontStyle.Bold;
                title.alignment = TextAnchor.UpperCenter;
                title.color = highlighted ? BriarwoodCatalogTheme.Brass : BriarwoodCatalogTheme.Ink;
                BuildingUIElements.Anchors(title.rectTransform, new Vector2(.08f, .70f), new Vector2(.92f, .9f), Vector2.zero, Vector2.zero);
            }
            var iconFrame = BuildingUIElements.SolidPanel("IconFrame", button.transform, Color.clear);
            iconFrame.raycastTarget = false;
            BuildingUIElements.Anchors(iconFrame.rectTransform, new Vector2(.29f, .38f), new Vector2(.71f, .76f), Vector2.zero, Vector2.zero);
            var raw = BuildingUIElements.Object("Icon", iconFrame.transform, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage)).GetComponent<RawImage>();
            raw.texture = icon;
            raw.enabled = icon != null;
            raw.raycastTarget = false;
            BuildingUIElements.Stretch(raw.rectTransform, 5, 5, -5, -5);
            value = BriarwoodCatalogTheme.Label("Value", button.transform, 18, TextAnchor.MiddleCenter);
            BuildingUIElements.Anchors(value.rectTransform, new Vector2(.08f, .09f), new Vector2(.92f, .36f), Vector2.zero, Vector2.zero);
            return button;
        }
    }
}
