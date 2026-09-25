using System;
using UnityEngine;
using UnityEngine.UI;

namespace Duskborn.UI.Building
{
    internal static class BuildingUIElements
    {
        internal static readonly Color Backdrop = new(.025f, .035f, .05f, .97f);
        internal static readonly Color Surface = new(.065f, .085f, .115f, .98f);
        internal static readonly Color SurfaceRaised = new(.105f, .135f, .17f, 1f);
        internal static readonly Color Accent = new(.85f, .58f, .2f, 1f);
        internal static readonly Color Positive = new(.28f, .76f, .48f, 1f);
        internal static readonly Color Negative = new(.88f, .3f, .27f, 1f);
        internal static readonly Color Muted = new(.52f, .58f, .65f, 1f);
        internal static readonly Color Text = new(.92f, .94f, .97f, 1f);

        internal static GameObject Object(string name, Transform parent, params Type[] components)
        {
            var gameObject = new GameObject(name, components);
            gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        internal static Image Panel(string name, Transform parent, Color color)
        {
            var image = Object(name, parent, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            image.color = color;
            var frame = Resources.Load<Sprite>("Textures/frame_classic");
            if (frame != null) { image.sprite = frame; image.type = Image.Type.Sliced; }
            return image;
        }

        // Plain uGUI panels are used by the forge so its presentation does not
        // depend on any of the legacy frame or slot sprites.
        internal static Image SolidPanel(string name, Transform parent, Color color, Color? outline = null)
        {
            var image = Object(name, parent, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            image.color = color;
            if (outline.HasValue)
            {
                var border = image.gameObject.AddComponent<Outline>();
                border.effectColor = outline.Value;
                border.effectDistance = new Vector2(2f, -2f);
                border.useGraphicAlpha = true;
            }
            return image;
        }

        internal static Text Label(string name, Transform parent, int size, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var label = Object(name, parent, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)).GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = size;
            label.color = Text;
            label.alignment = alignment;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }

        internal static Button Button(string name, Transform parent, string caption, Action action, bool danger = false)
        {
            var image = Panel(name, parent, danger ? new Color(.42f, .12f, .12f, 1f) : new Color(.16f, .21f, .27f, 1f));
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = danger ? new Color(1f, .78f, .78f) : new Color(1f, .88f, .68f);
            colors.pressedColor = new Color(.72f, .72f, .72f);
            colors.disabledColor = new Color(.38f, .4f, .43f, .72f);
            colors.colorMultiplier = 1f;
            button.colors = colors;
            if (action != null) button.onClick.AddListener(() => action());
            var text = Label("Label", image.transform, 18, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform, 10, 6, -10, -6);
            text.text = caption;
            return button;
        }

        internal static Button SolidButton(string name, Transform parent, string caption, Action action, bool danger = false)
        {
            var image = SolidPanel(name, parent,
                danger ? new Color(.42f, .12f, .12f, 1f) : new Color(.16f, .21f, .27f, 1f),
                danger ? new Color(.72f, .25f, .2f, .9f) : new Color(.31f, .36f, .42f, .9f));
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = danger ? new Color(1f, .78f, .78f) : new Color(1f, .88f, .68f);
            colors.pressedColor = new Color(.72f, .72f, .72f);
            colors.disabledColor = new Color(.38f, .4f, .43f, .72f);
            colors.colorMultiplier = 1f;
            button.colors = colors;
            if (action != null) button.onClick.AddListener(() => action());
            var text = Label("Label", image.transform, 18, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform, 10, 6, -10, -6);
            text.text = caption;
            return button;
        }

        internal static ScrollRect Scroll(string name, Transform parent, out RectTransform content)
        {
            var root = Object(name, parent, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            root.GetComponent<Image>().color = Color.clear;
            var contentObject = Object("Content", root.transform, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content = (RectTransform)contentObject.transform;
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1);
            content.sizeDelta = Vector2.zero;
            var layout = contentObject.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 10;
            layout.padding = new RectOffset(4, 8, 4, 8);
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            contentObject.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = root.GetComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = (RectTransform)root.transform;
            scroll.horizontal = false;
            scroll.scrollSensitivity = 32;
            return scroll;
        }

        internal static void Stretch(RectTransform rect, float left = 0, float bottom = 0, float right = 0, float top = 0)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(right, top);
        }

        internal static void Anchors(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        internal static void SetButtonCaption(Button button, string caption)
        {
            var label = button != null ? button.GetComponentInChildren<Text>() : null;
            if (label != null) label.text = caption;
        }
    }
}
