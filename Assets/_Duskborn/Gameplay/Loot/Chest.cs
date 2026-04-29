using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using TMPro;
using UnityEngine;
using Duskborn.Core;
using Duskborn.Gameplay.Player;

namespace Duskborn.Gameplay.Loot
{
    public class Chest : NetworkBehaviour
    {
        [SerializeField] private int         goldCost  = 50;
        [SerializeField] private LootTable   lootTable;
        [SerializeField] private TextMeshPro priceLabel;

        [Header("Outline")]
        [SerializeField] private string   outlineLayerName = "GreenOutline";
        [SerializeField] private Renderer outlineRenderer;

        private readonly SyncVar<bool> _isOpenSync = new();

        public bool IsOpen   => _isOpenSync.Value;
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

            _isOpenSync.OnChange += OnIsOpenChanged;
        }

        private void OnIsOpenChanged(bool prev, bool next, bool asServer)
        {
            if (next) gameObject.SetActive(false);
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

        // Called server-side by PlayerInteractor's ServerRpc.
        public void ServerOpen(NetworkConnection requester, PlayerInventory inventory)
        {
            if (!IsServerStarted) return;
            if (_isOpenSync.Value) return;

            if (lootTable == null || lootTable.Items == null || lootTable.Items.Length == 0)
            {
                Debug.LogWarning($"[Chest] {name} has no loot table assigned.");
                return;
            }

            int cost = GoldManager.Instance != null
                ? GoldManager.Instance.GetChestCost(goldCost)
                : goldCost;

            if (GoldManager.Instance == null || !GoldManager.Instance.TrySpend(cost))
            {
                Debug.Log($"[Chest] Not enough gold (need {cost}).");
                return;
            }

            int index = GameSession.Instance != null
                ? GameSession.Instance.RNG.Range(0, lootTable.Items.Length)
                : Random.Range(0, lootTable.Items.Length);

            // Apply item on the server so server-side stat multipliers are updated.
            inventory.AddItem(lootTable.Items[index]);

            GoldManager.Instance.OnChestOpened(cost);
            _isOpenSync.Value = true; // disables chest on all clients via SyncVar hook

            // Tell the owning client to also add the item so their local inventory/display is correct.
            DeliverItemRpc(requester, inventory.GetComponent<NetworkObject>(), index);
        }

        [TargetRpc]
        private void DeliverItemRpc(NetworkConnection conn, NetworkObject playerNob, int itemIndex)
        {
            if (lootTable == null || itemIndex >= lootTable.Items.Length) return;
            playerNob.GetComponent<PlayerInventory>()?.AddItem(lootTable.Items[itemIndex]);
        }
    }
}
