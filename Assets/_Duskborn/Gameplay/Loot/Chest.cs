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
        [SerializeField] private AudioClip   openClip;

        [Header("Outline")]
        [SerializeField] private string   outlineLayerName = "GreenOutline";
        [SerializeField] private Renderer outlineRenderer;

        private readonly SyncVar<bool> _isOpenSync = new();
        private readonly SyncVar<int>  _goldCostSync = new();

        public bool IsOpen   => _isOpenSync.Value;
        public int  GoldCost => _goldCostSync.Value > 0 ? _goldCostSync.Value : goldCost;

        public void Configure(int cost, LootTable table)
        {
            goldCost = cost;
            if (IsServerStarted)
                _goldCostSync.Value = cost;
            lootTable = table;
            if (priceLabel != null)
                priceLabel.text = $"{cost}g";
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            if (_goldCostSync.Value <= 0)
                _goldCostSync.Value = goldCost;
        }

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
            if (next)
            {
                float volume = 1.0f;
                if (Duskborn.Audio.AudioDatabase.Instance != null)
                {
                    if (openClip == null) openClip = Duskborn.Audio.AudioDatabase.Instance.Loot.chestOpenClip;
                    volume = Duskborn.Audio.AudioDatabase.Instance.Loot.chestVolume;
                }
                else if (openClip == null)
                {
                    openClip = Resources.Load<AudioClip>("SFX/chest_open");
                }

                if (openClip != null)
                {
                    if (Duskborn.Audio.AudioManager.Instance != null)
                        Duskborn.Audio.AudioManager.Instance.PlayAtPoint(openClip, transform.position, volume);
                    else
                        AudioSource.PlayClipAtPoint(openClip, transform.position);
                }
                gameObject.SetActive(false);
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

            int activeCost = GoldCost;
            int cost = GoldManager.Instance != null
                ? GoldManager.Instance.GetChestCost(activeCost)
                : activeCost;

            priceLabel.text = $"{cost}g";

            if (Camera.main != null)
                priceLabel.transform.rotation = Camera.main.transform.rotation;
        }

        // Called server-side by PlayerInteractor's ServerRpc.
        public void ServerOpen(NetworkConnection requester, PlayerBuffContainer inventory)
        {
            if (!IsServerStarted) return;
            if (_isOpenSync.Value) return;

            if (lootTable == null || lootTable.Items == null || lootTable.Items.Length == 0)
            {
                DuskLog.Warn(LogChannel.Loot, $"{name} has no loot table assigned.");
                return;
            }

            int activeCost = GoldCost;
            int cost = GoldManager.Instance != null
                ? GoldManager.Instance.GetChestCost(activeCost)
                : activeCost;

            if (GoldManager.Instance == null || !GoldManager.Instance.TrySpend(cost))
            {
                DuskLog.Log(LogChannel.Loot, $"Not enough gold (need {cost}).");
                return;
            }

            int index = GameSession.Instance != null
                ? GameSession.Instance.RNG.Range(0, lootTable.Items.Length)
                : Random.Range(0, lootTable.Items.Length);

            ItemDefinition awardedItem = lootTable.Items[index];

            // Apply item on the server so server-side stat multipliers are updated.
            inventory.AddBuff(awardedItem);

            GoldManager.Instance.OnChestOpened(cost);
            _isOpenSync.Value = true; // disables chest on all clients via SyncVar hook

            // Tell the owning client to also add the item so their local inventory/display is correct.
            DeliverItemRpc(requester, inventory.GetComponent<NetworkObject>(), index, awardedItem != null ? awardedItem.name : string.Empty);
        }

        [TargetRpc]
        private void DeliverItemRpc(NetworkConnection conn, NetworkObject playerNob, int itemIndex, string itemName)
        {
            if (IsServerStarted) return; // host already applied in ServerOpen
            
            ItemDefinition item = null;
            if (!string.IsNullOrEmpty(itemName) && ItemDefinitionRegistry.Instance != null)
                item = ItemDefinitionRegistry.Instance.GetById(itemName);

            if (item == null && lootTable != null && itemIndex >= 0 && itemIndex < lootTable.Items.Length)
                item = lootTable.Items[itemIndex];

            if (item != null)
                playerNob.GetComponent<PlayerBuffContainer>()?.AddBuff(item);
        }
    }
}
