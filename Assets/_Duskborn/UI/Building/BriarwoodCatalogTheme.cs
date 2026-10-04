using Duskborn.Gameplay.Building;
using UnityEngine;
using UnityEngine.UI;

namespace Duskborn.UI.Building
{
    // Catalog-only skin: station processors and placement retain their existing UI.
    internal static class BriarwoodCatalogTheme
    {
        private const string Folder = "UI/Briarwood/";
        private static Sprite buttonSprite;
        private static readonly System.Collections.Generic.Dictionary<string, Texture2D> Icons = new();
        internal static readonly Color Leather = new(.105f, .083f, .065f, 1f);
        internal static readonly Color Raised = new(.16f, .13f, .10f, 1f);
        internal static readonly Color Ink = new(.96f, .9f, .76f, 1f);
        internal static readonly Color Muted = new(.73f, .68f, .57f, 1f);
        internal static readonly Color Brass = new(.91f, .69f, .34f, 1f);
        internal static readonly Color Positive = new(.65f, .79f, .48f, 1f);
        internal static readonly Color Negative = new(.92f, .53f, .39f, 1f);

        internal static Image Panel(string name, Transform parent, Color color) =>
            BuildingUIElements.SolidPanel(name, parent, color);

        internal static Image Sprite(string name, Transform parent, string resource, bool sliced = false)
        {
            var image = Panel(name, parent, Color.white);
            image.sprite = resource == "button" ? ButtonSprite() : Resources.Load<Sprite>(Folder + resource);
            image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            image.raycastTarget = false;
            if (sliced) image.pixelsPerUnitMultiplier = 2f;
            else image.preserveAspect = true;
            return image;
        }

        internal static Text Label(string name, Transform parent, int size, TextAnchor alignment = TextAnchor.MiddleLeft, bool heading = false)
        {
            var text = BuildingUIElements.Label(name, parent, size, alignment);
            text.color = Ink;
            if (heading)
            {
                var font = Resources.Load<Font>(Folder + "AlegreyaSC-Bold");
                if (font != null) text.font = font;
            }
            return text;
        }

        internal static Button Button(string name, Transform parent, string caption, System.Action action, bool primary = false)
        {
            var image = Sprite(name, parent, "button", true);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = primary ? Color.white : new Color(.75f, .72f, .66f);
            colors.highlightedColor = new Color(1f, .94f, .77f);
            colors.pressedColor = new Color(.68f, .58f, .43f);
            colors.disabledColor = new Color(.46f, .44f, .4f, 1f);
            colors.fadeDuration = .1f;
            button.colors = colors;
            if (action != null) button.onClick.AddListener(() => action());
            var text = Label("Label", image.transform, primary ? 22 : 18, TextAnchor.MiddleCenter, true);
            BuildingUIElements.Stretch(text.rectTransform, 18, 6, -18, -6);
            text.text = caption;
            return button;
        }

        internal static Texture2D Icon(BuildableDefinition definition)
        {
            if (definition == null) return null;
            // The unmodeled stations deliberately retain their placeholder treatment.
            if (definition.id == "workbench" || definition.id == "forge" || definition.id == "arcane_table")
            {
                if (!Icons.TryGetValue(definition.id, out var texture) || texture == null)
                    Icons[definition.id] = texture = Resources.Load<Texture2D>(Folder + "Icon_" + definition.id);
                return texture ?? definition.icon;
            }
            return null;
        }

        private static Sprite ButtonSprite()
        {
            if (buttonSprite != null) return buttonSprite;
            var texture = Resources.Load<Texture2D>(Folder + "button");
            if (texture == null) return null;
            // Trim transparent source padding through the sprite rect, preserving the PNG.
            // Alpha bounds in the 2172 x 724 source: (11, 140), 2151 x 458, bottom origin.
            var rect = new Rect(texture.width * 11f / 2172f, texture.height * 140f / 724f,
                texture.width * 2151f / 2172f, texture.height * 458f / 724f);
            float scale = texture.width / 2172f;
            buttonSprite = UnityEngine.Sprite.Create(texture, rect, new Vector2(.5f, .5f), 100, 0,
                SpriteMeshType.FullRect, new Vector4(140, 48, 140, 48) * scale);
            buttonSprite.name = "Briarwood button (trimmed)";
            return buttonSprite;
        }
    }
}
