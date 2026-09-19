using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace InventorySystem.UI
{
    public sealed class InventorySlotView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private Image              iconImage;
        [SerializeField] private TextMeshProUGUI   fallbackLabel;
        [SerializeField] private Image              borderImage;
        [SerializeField] private TextMeshProUGUI   stackCountLabel;

        private Sprite _dragGhostSprite;

        public int SlotIndex { get; private set; }
        public RectTransform RectTransform { get; private set; }
        public Image IconImage => iconImage;
        public Sprite DragGhostSprite => _dragGhostSprite;
        public event Action<int, PointerEventData> PointerEntered;
        public event Action<int, PointerEventData> PointerExited;
        public event Action<int, PointerEventData> PointerClicked;

        public void Initialize(int slotIndex)
        {
            SlotIndex = slotIndex;
            RectTransform = transform as RectTransform;
        }

        public void BindReferences(Image icon, TextMeshProUGUI fallback, Image border, TextMeshProUGUI stackCount = null)
        {
            iconImage       = icon;
            fallbackLabel   = fallback;
            borderImage     = border;
            stackCountLabel = stackCount;
        }

        public void SetStackCount(int count)
        {
            if (stackCountLabel == null) return;
            stackCountLabel.gameObject.SetActive(count > 0);
            stackCountLabel.enabled = count > 0;
            if (count > 0)
                stackCountLabel.text = count.ToString();
        }

        public void SetBorderColor(Color color)
        {
            if (borderImage != null)
            {
                borderImage.color = color;
            }
        }

        public void SetIcon(Texture2D texture, string fallbackToken)
        {
            if (texture != null)
            {
                if (_dragGhostSprite != null)
                {
                    Destroy(_dragGhostSprite);
                }

                _dragGhostSprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f));

                if (iconImage != null)
                {
                    iconImage.enabled = true;
                    iconImage.sprite = _dragGhostSprite;
                    iconImage.color = Color.white;
                }

                if (fallbackLabel != null)
                {
                    fallbackLabel.text = string.Empty;
                }

                return;
            }

            if (iconImage != null)
            {
                iconImage.enabled = false;
                iconImage.sprite = null;
            }

            if (fallbackLabel != null)
            {
                fallbackLabel.text = fallbackToken;
            }
        }

        public void ClearSlotVisual()
        {
            if (iconImage != null)
            {
                iconImage.enabled = false;
                iconImage.sprite = null;
            }

            if (fallbackLabel != null)
                fallbackLabel.text = string.Empty;

            if (stackCountLabel != null)
            {
                stackCountLabel.gameObject.SetActive(false);
                stackCountLabel.enabled = false;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            PointerEntered?.Invoke(SlotIndex, eventData);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            PointerExited?.Invoke(SlotIndex, eventData);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            PointerClicked?.Invoke(SlotIndex, eventData);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            // Consome o evento de início de arraste na célula para impedir que bolheie para painéis móveis (DraggablePanel)
        }

        public void OnDrag(PointerEventData eventData)
        {
            // Consome o evento de arraste contínuo
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            // Consome o término de arraste
        }

        private void OnDestroy()
        {
            if (_dragGhostSprite != null)
            {
                Destroy(_dragGhostSprite);
            }
        }
    }
}
