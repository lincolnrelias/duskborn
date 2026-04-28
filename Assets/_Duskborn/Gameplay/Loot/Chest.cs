using TMPro;
using UnityEngine;
using Duskborn.Core;

namespace Duskborn.Gameplay.Loot
{
    public class Chest : MonoBehaviour
    {
        [SerializeField] private int         goldCost  = 50;
        [SerializeField] private LootTable   lootTable;
        [SerializeField] private TextMeshPro priceLabel;

        [Header("Outline")]
        [SerializeField] private string   outlineLayerName = "GreenOutline";
        [SerializeField] private Renderer outlineRenderer;

        public bool IsOpen   { get; private set; }
        public int  GoldCost => goldCost;

        private uint _outlineMask;
        private uint _baseMask;

        private void Awake()
        {
            if (outlineRenderer == null)
                outlineRenderer = GetComponentInChildren<Renderer>();

            if (outlineRenderer != null)
            {
                int layerIndex = RenderingLayerMask.NameToRenderingLayer(outlineLayerName);
                _outlineMask = layerIndex >= 0 ? (uint)(1 << layerIndex) : 0u;
                _baseMask    = outlineRenderer.renderingLayerMask & ~_outlineMask;
                outlineRenderer.renderingLayerMask = _baseMask;
            }
        }

        public void SetOutline(bool show)
        {
            if (outlineRenderer == null) return;
            outlineRenderer.renderingLayerMask = show ? _baseMask | _outlineMask : _baseMask;
        }

        private void LateUpdate()
        {
            if (priceLabel == null) return;

            int cost = GoldManager.Instance != null
                ? GoldManager.Instance.GetChestCost(goldCost)
                : goldCost;

            priceLabel.text = $"{cost}g";

            if (Camera.main != null)
                priceLabel.transform.rotation = Camera.main.transform.rotation;
        }

        public bool TryOpen(PlayerInventory inventory)
        {
            if (IsOpen) return false;

            if (lootTable == null || lootTable.Items == null || lootTable.Items.Length == 0)
            {
                Debug.LogWarning($"[Chest] {name} has no loot table assigned.");
                return false;
            }

            int cost = GoldManager.Instance != null
                ? GoldManager.Instance.GetChestCost(goldCost)
                : goldCost;

            if (!GoldManager.Instance.TrySpend(cost))
            {
                Debug.Log($"[Chest] Not enough gold (need {cost}, have {GoldManager.Instance.Gold}).");
                return false;
            }

            int index = GameSession.Instance != null
                ? GameSession.Instance.RNG.Range(0, lootTable.Items.Length)
                : Random.Range(0, lootTable.Items.Length);

            inventory.AddItem(lootTable.Items[index]);
            GoldManager.Instance.OnChestOpened(cost);

            IsOpen = true;
            gameObject.SetActive(false);
            return true;
        }
    }
}
