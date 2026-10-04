using Duskborn.Gameplay.Enchanting;
using InventorySystem.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Duskborn.Effects
{
    /// <summary>Shared health-bar child, with a dim base and clockwise remaining-time image.</summary>
    public sealed class RuneStatusBar : MonoBehaviour
    {
        private readonly RectTransform[] _chips = new RectTransform[9];
        private readonly Image[] _clocks = new Image[9];
        private readonly TextMeshProUGUI[] _counts = new TextMeshProUGUI[9];
        private readonly Sprite[] _ownedSprites = new Sprite[9];
        private IDebuffSource _source;
        public bool HasDebuffs { get; private set; }

        public void Initialize(IDebuffSource source)
        {
            _source = source;
            var rect = (RectTransform)transform;
            rect.sizeDelta = new Vector2(2.24f, .33f);
            rect.anchoredPosition = new Vector2(0, .40f);
            for (int i = 1; i <= 8; i++)
            {
                var kind = (RuneKind)i;
                var chip = new GameObject(kind + " status", typeof(RectTransform)).GetComponent<RectTransform>();
                chip.SetParent(transform, false); chip.sizeDelta = new Vector2(.26f, .29f); _chips[i] = chip;
                Image panel = Image("Obsidian socket", chip, new Vector2(.26f, .29f), new Color(.025f, .032f, .055f, .96f));
                var border = Image("Element accent", chip, new Vector2(.26f, .018f), RuneCatalog.Color(kind));
                border.rectTransform.anchoredPosition = new Vector2(0, .14f);
                Texture2D texture = Resources.Load<MaterialDefinition>("Runestones/" + RuneCatalog.Id(kind, 1))?.Icon as Texture2D;
                Sprite sprite = texture != null ? Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * .5f) : null;
                _ownedSprites[i] = sprite;
                var baseIcon = Image("Expired portion", chip, new Vector2(.23f, .23f), new Color(.25f, .27f, .31f, .8f));
                baseIcon.sprite = sprite; baseIcon.preserveAspect = true;
                var clock = Image("Clockwise time remaining", chip, new Vector2(.23f, .23f), Color.white);
                clock.sprite = sprite; clock.preserveAspect = true; clock.type = UnityEngine.UI.Image.Type.Filled;
                clock.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360;
                clock.fillOrigin = (int)UnityEngine.UI.Image.Origin360.Top; clock.fillClockwise = true; _clocks[i] = clock;
                var countSocket = Image("Stack badge", chip, new Vector2(.13f, .115f), new Color(.015f, .02f, .03f, 1));
                countSocket.rectTransform.anchoredPosition = new Vector2(.065f, -.095f);
                var count = new GameObject("Stacks", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
                count.transform.SetParent(countSocket.transform, false); count.rectTransform.sizeDelta = countSocket.rectTransform.sizeDelta;
                count.raycastTarget = false; count.fontSize = .093f; count.fontStyle = FontStyles.Bold;
                count.alignment = TextAlignmentOptions.Center; count.color = Color.white;
                _counts[i] = count; chip.gameObject.SetActive(false);
            }
        }

        private static Image Image(string name, Transform parent, Vector2 size, Color color)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false); image.rectTransform.sizeDelta = size;
            image.color = color; image.raycastTarget = false; return image;
        }

        public void Refresh()
        {
            if (_source == null) return;
            int active = 0;
            for (int i = 1; i <= 8; i++) if (_source.TryGetDebuff((RuneKind)i, out _)) active++;
            HasDebuffs = active > 0;
            int ordinal = 0;
            for (int i = 1; i <= 8; i++)
            {
                bool show = _source.TryGetDebuff((RuneKind)i, out var view);
                _chips[i].gameObject.SetActive(show);
                if (!show) continue;
                _chips[i].anchoredPosition = new Vector2((ordinal++ - (active - 1) * .5f) * .28f, 0);
                _clocks[i].fillAmount = Mathf.Clamp01(view.Remaining);
                _counts[i].SetText("{0}", view.Stacks);
                _counts[i].color = view.Locked ? RuneCatalog.Color((RuneKind)i) : Color.white;
            }
        }
        private void OnDestroy()
        {
            foreach (var sprite in _ownedSprites)
                if (sprite != null) { if (Application.isPlaying) Destroy(sprite); else DestroyImmediate(sprite); }
        }
    }
}
