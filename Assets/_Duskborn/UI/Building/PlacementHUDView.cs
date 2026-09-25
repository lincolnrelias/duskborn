using System.Linq;
using Duskborn.Gameplay.Building;
using UnityEngine;
using UnityEngine.UI;

namespace Duskborn.UI.Building
{
    internal sealed class PlacementHUDView
    {
        private readonly GameObject root;
        private readonly RawImage icon;
        private readonly Text placeholder;
        private readonly Text title;
        private readonly Text state;
        private readonly Text reason;
        private readonly Text costs;
        private readonly Text rotation;

        internal PlacementHUDView(Transform canvas)
        {
            root = BuildingUIElements.Panel("PlacementHUD", canvas, BuildingUIElements.Backdrop).gameObject;
            var image = root.GetComponent<Image>();
            image.raycastTarget = false;
            BuildingUIElements.Anchors((RectTransform)root.transform, new Vector2(.24f, .025f), new Vector2(.76f, .18f), Vector2.zero, Vector2.zero);

            var iconFrame = BuildingUIElements.Panel("IconFrame", root.transform, BuildingUIElements.SurfaceRaised);
            iconFrame.raycastTarget = false;
            BuildingUIElements.Anchors(iconFrame.rectTransform, new Vector2(.025f, .16f), new Vector2(.15f, .84f), Vector2.zero, Vector2.zero);
            icon = BuildingUIElements.Object("Icon", iconFrame.transform, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage)).GetComponent<RawImage>();
            icon.raycastTarget = false;
            BuildingUIElements.Stretch(icon.rectTransform, 8, 8, -8, -8);
            placeholder = BuildingUIElements.Label("Placeholder", iconFrame.transform, 27, TextAnchor.MiddleCenter);
            placeholder.color = BuildingUIElements.Accent;
            BuildingUIElements.Stretch(placeholder.rectTransform);

            title = BuildingUIElements.Label("Name", root.transform, 21);
            title.fontStyle = FontStyle.Bold;
            BuildingUIElements.Anchors(title.rectTransform, new Vector2(.175f, .63f), new Vector2(.49f, .91f), Vector2.zero, Vector2.zero);
            state = BuildingUIElements.Label("State", root.transform, 17, TextAnchor.MiddleRight);
            state.fontStyle = FontStyle.Bold;
            BuildingUIElements.Anchors(state.rectTransform, new Vector2(.51f, .63f), new Vector2(.96f, .91f), Vector2.zero, Vector2.zero);
            reason = BuildingUIElements.Label("Reason", root.transform, 16);
            BuildingUIElements.Anchors(reason.rectTransform, new Vector2(.175f, .4f), new Vector2(.96f, .65f), Vector2.zero, Vector2.zero);
            costs = BuildingUIElements.Label("Costs", root.transform, 14);
            costs.color = new Color(.76f, .81f, .86f);
            BuildingUIElements.Anchors(costs.rectTransform, new Vector2(.175f, .18f), new Vector2(.7f, .42f), Vector2.zero, Vector2.zero);
            rotation = BuildingUIElements.Label("Rotation", root.transform, 14, TextAnchor.MiddleRight);
            rotation.color = new Color(.76f, .81f, .86f);
            BuildingUIElements.Anchors(rotation.rectTransform, new Vector2(.7f, .18f), new Vector2(.96f, .42f), Vector2.zero, Vector2.zero);
            var controls = BuildingUIElements.Label("Controls", root.transform, 13, TextAnchor.MiddleCenter);
            controls.text = "CLIQUE  confirmar     •     SCROLL / R  girar     •     ESC / B  cancelar";
            controls.color = BuildingUIElements.Muted;
            BuildingUIElements.Anchors(controls.rectTransform, new Vector2(.05f, .01f), new Vector2(.95f, .2f), Vector2.zero, Vector2.zero);
            root.SetActive(false);
        }

        internal void Render(BuildablePresentation model, string invalidReason, float yaw, bool pending)
        {
            root.SetActive(true);
            icon.texture = model.Definition.icon;
            icon.enabled = model.Definition.icon != null;
            placeholder.gameObject.SetActive(model.Definition.icon == null);
            placeholder.text = Initials(model.Definition.displayName);
            title.text = model.Definition.displayName;
            bool valid = string.IsNullOrEmpty(invalidReason);
            state.text = pending ? "◌  CONFIRMANDO..." : valid ? "✓  LOCAL VÁLIDO" : "!  LOCAL INVÁLIDO";
            state.color = pending ? BuildingUIElements.Accent : valid ? BuildingUIElements.Positive : BuildingUIElements.Negative;
            reason.text = pending ? "Validando a construção no servidor." : valid ? "Clique para construir aqui." : invalidReason;
            reason.color = valid && !pending ? BuildingUIElements.Positive : pending ? BuildingUIElements.Accent : BuildingUIElements.Negative;
            costs.text = string.Join("  •  ", model.Costs.Select(cost => cost.DisplayName + " " + cost.Owned + "/" + cost.Required));
            rotation.text = "ROTAÇÃO  " + Mathf.RoundToInt(yaw) + "°";
        }

        internal void Hide() => root.SetActive(false);

        private static string Initials(string value) => string.IsNullOrWhiteSpace(value)
            ? "?"
            : string.Concat(value.Split(' ').Where(part => part.Length > 0).Take(2).Select(part => char.ToUpperInvariant(part[0])));
    }
}
