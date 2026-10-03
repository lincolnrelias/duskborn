using Duskborn.Gameplay.Enchanting;
using InventorySystem.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Duskborn.Effects
{
    public sealed class RuneStatusBar : MonoBehaviour
    {
        private Canvas _canvas;
        private Camera _camera;
        private readonly RectTransform[] _chips = new RectTransform[9];
        private readonly TextMeshProUGUI[] _counts = new TextMeshProUGUI[9];
        public void Configure(float height)
        {
            _canvas = gameObject.AddComponent<Canvas>(); _canvas.renderMode = RenderMode.WorldSpace;
            var rect = GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(288, 24);
            transform.localPosition = Vector3.up * height; transform.localScale = Vector3.one * .0075f;
            for (int i = 1; i <= 8; i++)
            {
                var rune = (RuneKind)i;
                var chip = new GameObject(rune + " stacks", typeof(RectTransform)).GetComponent<RectTransform>();
                chip.SetParent(transform, false); chip.sizeDelta = new Vector2(36, 24); _chips[i] = chip;
                var image = new GameObject("Rune", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
                image.transform.SetParent(chip, false); image.raycastTarget = false;
                image.rectTransform.sizeDelta = new Vector2(20, 20); image.rectTransform.anchoredPosition = new Vector2(-8, 0);
                image.texture = Resources.Load<MaterialDefinition>("Runestones/" + RuneCatalog.Id(rune, 1))?.Icon;
                var label = new GameObject("Count", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
                label.transform.SetParent(chip, false); label.raycastTarget = false; label.fontSize = 13; label.fontStyle = FontStyles.Bold;
                label.color = Color.white; label.outlineWidth = .2f;
                label.alignment = TextAlignmentOptions.MidlineLeft;
                label.rectTransform.sizeDelta = new Vector2(18, 24); label.rectTransform.anchoredPosition = new Vector2(12, 0);
                _counts[i] = label; chip.gameObject.SetActive(false);
            }
        }
        public void SetStacks(int[] values)
        {
            int active = 0;
            for (int i = 1; i <= 8; i++) if (values[i] != 0) active++;
            _canvas.enabled = active > 0;
            int ordinal = 0;
            for (int i = 1; i <= 8; i++)
            {
                bool show = values[i] != 0; _chips[i].gameObject.SetActive(show);
                if (!show) continue;
                _chips[i].anchoredPosition = new Vector2((ordinal++ - (active - 1) * .5f) * 36, 0);
                _counts[i].text = values[i] < 0 ? "!" : values[i].ToString();
            }
        }
        private void LateUpdate()
        {
            if (_camera == null) _camera = Camera.main;
            if (_camera == null || !_canvas.enabled) return;
            transform.rotation = _camera.transform.rotation;
        }
    }
}
