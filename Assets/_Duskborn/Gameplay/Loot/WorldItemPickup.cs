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

        private readonly SyncVar<string>     _resourceId   = new();
        private readonly SyncVar<int>        _amount       = new();
        private readonly SyncVar<bool>       _collectible  = new();
        private readonly SyncVar<ItemRarity> _rarity       = new();
        private bool _collected;

        private DroppedItemVisuals _visuals;

        public bool       IsCollectible => _collectible.Value;
        public ItemRarity Rarity        => _rarity.Value;

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

            _visuals = GetComponent<DroppedItemVisuals>();
            if (_visuals == null)
                _visuals = gameObject.AddComponent<DroppedItemVisuals>();

            _rarity.OnChange += OnRarityChanged;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (_visuals != null)
                _visuals.Setup(_rarity.Value);
        }

        private void OnRarityChanged(ItemRarity prev, ItemRarity next, bool asServer)
        {
            if (_visuals != null)
                _visuals.Setup(next);
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            Invoke(nameof(SetCollectible), 1f);
        }

        private void SetCollectible() => _collectible.Value = true;

        public void ServerInitialize(string resourceId, int amount, ItemRarity rarity = ItemRarity.Common)
        {
            _resourceId.Value = resourceId;
            _amount.Value     = amount;

            if (rarity == ItemRarity.Common && !string.IsNullOrEmpty(resourceId) && WorldDropRegistry.Instance != null)
            {
                var def = WorldDropRegistry.Instance.GetDefinition(resourceId);
                if (def != null)
                    rarity = def.Rarity;
            }

            _rarity.Value = rarity;

            if (_visuals != null)
                _visuals.Setup(rarity);
        }

        [ObserversRpc(RunLocally = true)]
        public void RpcPlayDropSound(ItemRarity dropRarity)
        {
            PlayDropSound(dropRarity, transform.position);
        }

        public static void PlayDropSound(ItemRarity dropRarity, Vector3 position)
        {
            AudioClip clip = null;
            float volume = 1.0f;

            if (Duskborn.Audio.AudioDatabase.Instance != null)
            {
                clip   = Duskborn.Audio.AudioDatabase.Instance.Loot.GetDropClip(dropRarity);
                volume = Duskborn.Audio.AudioDatabase.Instance.Loot.GetDropVolume(dropRarity);
            }

            if (clip == null)
            {
                clip = Resources.Load<AudioClip>($"SFX/drop_{dropRarity.ToString().ToLower()}")
                    ?? Resources.Load<AudioClip>($"drop_{dropRarity.ToString().ToLower()}");
            }

            if (clip != null)
            {
                DuskLog.Log(LogChannel.Loot, $"PlayDropSound: Tocando som de drop [{clip.name}] para tier {dropRarity} (vol={volume:F2}) em {position}");
                if (Duskborn.Audio.AudioManager.Instance != null)
                    Duskborn.Audio.AudioManager.Instance.PlayAtPoint(clip, position, volume, 6f, 60f, 0.03f, 0.35f);
                else
                    AudioSource.PlayClipAtPoint(clip, position, volume);
            }
            else
            {
                DuskLog.Warn(LogChannel.Loot, $"RpcPlayDropSound: clipe de áudio para tier {dropRarity} não foi encontrado.");
            }
        }

        public void ServerThrow(Vector3 impulse, float torque = 0f)
        {
            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.AddForce(impulse, ForceMode.VelocityChange);
                if (torque > 0f)
                    rb.AddTorque(Random.insideUnitSphere * torque, ForceMode.VelocityChange);
            }
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
            DeliverResourceRpc(requester, resourceInventory.GetComponent<NetworkObject>(), _resourceId.Value, _amount.Value, _rarity.Value);
            InstanceFinder.ServerManager.Despawn(NetworkObject, DespawnType.Destroy);
        }

        [TargetRpc]
        private void DeliverResourceRpc(NetworkConnection conn, NetworkObject playerNob, string resourceId, int amount, ItemRarity rarity)
        {
            playerNob.GetComponent<ResourceInventory>()?.Add(resourceId, amount);

            AudioClip clip = null;
            float volume = 1.0f;

            if (Duskborn.Audio.AudioDatabase.Instance != null)
            {
                clip = Duskborn.Audio.AudioDatabase.Instance.GetPickupClip(rarity) ?? pickupClip;
                volume = Duskborn.Audio.AudioDatabase.Instance.GetPickupVolume(rarity);
            }
            else
            {
                string clipName = rarity switch
                {
                    ItemRarity.Common    => "pickup_common",
                    ItemRarity.Uncommon  => "pickup_uncommon",
                    ItemRarity.Rare      => "pickup_rare",
                    ItemRarity.Epic      => "pickup_epic",
                    ItemRarity.Legendary => "pickup_legendary",
                    _                    => "pickup_common"
                };

                clip = Resources.Load<AudioClip>($"SFX/{clipName}")
                    ?? pickupClip
                    ?? Resources.Load<AudioClip>("SFX/item_pickup");
            }

            if (clip != null)
            {
                if (Duskborn.Audio.AudioManager.Instance != null)
                    Duskborn.Audio.AudioManager.Instance.PlayAtPoint(clip, playerNob.transform.position, volume);
                else
                    AudioSource.PlayClipAtPoint(clip, playerNob.transform.position, volume);
            }
        }
    }
}
