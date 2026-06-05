using FishNet;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Duskborn.Gameplay.Loot
{
    public class WorldItemPickup : NetworkBehaviour
    {
        [SerializeField] private string    outlineLayerName = "GreenOutline";
        [SerializeField] private Renderer  outlineRenderer;
        [SerializeField] private AudioClip pickupClip;

        private readonly SyncVar<string> _resourceId   = new();
        private readonly SyncVar<int>    _amount       = new();
        private readonly SyncVar<bool>   _collectible  = new();
        private bool _collected;

        public bool IsCollectible => _collectible.Value;

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

        public override void OnStartServer()
        {
            base.OnStartServer();
            Invoke(nameof(SetCollectible), 1f);
        }

        private void SetCollectible() => _collectible.Value = true;

        public void ServerInitialize(string resourceId, int amount)
        {
            _resourceId.Value = resourceId;
            _amount.Value     = amount;
        }

        public void ServerThrow(Vector3 impulse)
        {
            var rb = GetComponent<Rigidbody>();
            if (rb != null)
                rb.AddForce(impulse, ForceMode.VelocityChange);
            else
                DuskLog.Warn(LogChannel.Loot, $"{name}: ServerThrow called but no Rigidbody found.");
        }

        public void SetOutline(bool show)
        {
            if (outlineRenderer == null) return;
            outlineRenderer.renderingLayerMask = show ? _baseMask | _outlineMask : _baseMask;
        }

        public void ServerCollect(NetworkConnection requester, ResourceInventory resourceInventory)
        {
            if (!IsServerStarted || _collected || !_collectible.Value) return;
            if (string.IsNullOrEmpty(_resourceId.Value))
            {
                DuskLog.Warn(LogChannel.Loot, $"{name}: ServerCollect called with null/empty resourceId — item discarded.");
                InstanceFinder.ServerManager.Despawn(NetworkObject, DespawnType.Destroy);
                return;
            }
            _collected = true;

            // ResourceInventory is client-authoritative — deliver only via TargetRpc, never add server-side.
            DeliverResourceRpc(requester, resourceInventory.GetComponent<NetworkObject>(), _resourceId.Value, _amount.Value);
            InstanceFinder.ServerManager.Despawn(NetworkObject, DespawnType.Destroy);
        }

        [TargetRpc]
        private void DeliverResourceRpc(NetworkConnection conn, NetworkObject playerNob, string resourceId, int amount)
        {
            playerNob.GetComponent<ResourceInventory>()?.Add(resourceId, amount);
            if (pickupClip != null)
                AudioSource.PlayClipAtPoint(pickupClip, playerNob.transform.position);
        }
    }
}
