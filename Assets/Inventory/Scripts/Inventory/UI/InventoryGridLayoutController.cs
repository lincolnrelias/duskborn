using UnityEngine;
using UnityEngine.UI;

namespace InventorySystem.UI
{
    [ExecuteAlways]
    public sealed class InventoryGridLayoutController : MonoBehaviour
    {
        private const string GeneratedSlotNamePrefix = "Slot_";
        [SerializeField] private GridLayoutGroup gridLayoutGroup;
        [SerializeField] private InventorySlotView slotTemplate;
        [SerializeField] private int columns = 6;
        [SerializeField] private int rows = 5;
        [SerializeField] private Vector2 spacing = new(6f, 6f);
        [SerializeField] private float holdThresholdSeconds = 0.12f;

        public int Columns => Mathf.Max(1, columns);
        public int Rows => Mathf.Max(1, rows);
        public float HoldThresholdSeconds => Mathf.Max(0f, holdThresholdSeconds);
        public int SlotCount => Columns * Rows;

        private void Reset()
        {
            if (gridLayoutGroup == null)
            {
                gridLayoutGroup = GetComponent<GridLayoutGroup>();
            }
        }

        private void OnValidate()
        {
            ApplyLayout();
        }

        public void ApplyLayout()
        {
            if (gridLayoutGroup == null)
            {
                return;
            }

            gridLayoutGroup.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayoutGroup.constraintCount = Columns;
            gridLayoutGroup.spacing = new Vector2(Mathf.Max(0f, spacing.x), Mathf.Max(0f, spacing.y));

            if (slotTemplate == null)
            {
                return;
            }

            var slotRect = slotTemplate.transform as RectTransform;
            if (slotRect == null)
            {
                return;
            }

            var templateSize = slotRect.sizeDelta;
            if (templateSize.x <= 0f || templateSize.y <= 0f)
            {
                templateSize = slotRect.rect.size;
            }

            if (templateSize.x > 0f && templateSize.y > 0f)
            {
                gridLayoutGroup.cellSize = templateSize;
            }
        }

        [ContextMenu("Populate Slots")]
        public void PopulateSlots()
        {
            if (slotTemplate == null)
            {
                Debug.LogError("InventoryGridLayoutController: slotTemplate is required to populate slots.");
                return;
            }

            ApplyLayout();
            ClearGeneratedSlots();
            var targetCount = SlotCount;
            for (var i = 0; i < targetCount; i++)
            {
                InventorySlotView slotInstance;
                if (Application.isPlaying)
                {
                    slotInstance = Instantiate(slotTemplate, transform);
                }
                else
                {
#if UNITY_EDITOR
                    var created = UnityEditor.PrefabUtility.InstantiatePrefab(slotTemplate.gameObject, transform) as GameObject;
                    if (created == null)
                    {
                        continue;
                    }

                    slotInstance = created.GetComponent<InventorySlotView>();
                    if (slotInstance == null)
                    {
                        DestroyImmediate(created);
                        continue;
                    }
#else
                    slotInstance = Instantiate(slotTemplate, transform);
#endif
                }

                slotInstance.name = $"{GeneratedSlotNamePrefix}{i}";
                slotInstance.gameObject.SetActive(true);
            }
        }

        [ContextMenu("Clear Generated Slots")]
        public void ClearGeneratedSlots()
        {
            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (slotTemplate != null && child == slotTemplate.transform)
                {
                    continue;
                }

                if (!child.TryGetComponent<InventorySlotView>(out _))
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }
    }
}
