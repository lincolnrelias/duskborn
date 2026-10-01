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

        private readonly SyncVar<string>        _resourceId   = new();
        private readonly SyncVar<int>           _amount       = new();
        private readonly SyncVar<bool>          _collectible  = new();
        private readonly SyncVar<ItemRarity>    _rarity       = new();
        private readonly SyncVar<NetworkObject> _targetPlayer = new();
        private bool _collected;
        private float _spawnTime;
        private bool  _isFlying;

        private DroppedItemVisuals _visuals;

        public bool          IsCollectible => _collectible.Value;
        public ItemRarity    Rarity        => _rarity.Value;
        public NetworkObject TargetPlayer  => _targetPlayer.Value;

        private uint _outlineMask;
        private uint _baseMask;

        private void Awake()
        {
            _spawnTime = Time.time;
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

        private void OnDestroy()
        {
            _rarity.OnChange -= OnRarityChanged;
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
            if (_targetPlayer.Value == null || !CanFlyToPlayer(_rarity.Value))
                Invoke(nameof(SetCollectible), 1f);
        }

        [SerializeField] private float lingerDuration = 2.6f;

        private void SetCollectible() => _collectible.Value = true;

        public static bool CanFlyToPlayer(ItemRarity rarity) => rarity <= ItemRarity.Rare;

        private float GetLingerDuration()
        {
            int seed = NetworkObject != null ? (int)NetworkObject.ObjectId : gameObject.GetInstanceID();
            float jitter = (seed & 7) * 0.05f; // 0.00 a 0.35s de variação para cascata orgânica
            return lingerDuration + jitter;
        }

        public void ServerInitialize(string resourceId, int amount, ItemRarity rarity = ItemRarity.Common, NetworkObject targetPlayer = null)
        {
            _resourceId.Value   = resourceId;
            _amount.Value       = amount;
            _targetPlayer.Value = targetPlayer;

            if (rarity == ItemRarity.Common && !string.IsNullOrEmpty(resourceId) && WorldDropRegistry.Instance != null)
            {
                var def = WorldDropRegistry.Instance.GetDefinition(resourceId);
                if (def != null)
                    rarity = def.Rarity;
            }

            _rarity.Value = rarity;

            if (targetPlayer != null && CanFlyToPlayer(rarity))
            {
                // Permite que os itens scatterem e pousem com física livre pelo dobro do tempo antes de voarem
                CancelInvoke(nameof(SetCollectible));
                Invoke(nameof(SetCollectible), GetLingerDuration());
            }

            if (_visuals != null)
                _visuals.Setup(rarity);
        }

        private void Update()
        {
            if (_targetPlayer.Value != null && CanFlyToPlayer(Rarity))
            {
                UpdateFlyTowardsTarget();
            }
        }

        private void UpdateFlyTowardsTarget()
        {
            if (_collected) return;
            var targetNob = _targetPlayer.Value;
            if (targetNob == null || !targetNob.gameObject.activeInHierarchy)
            {
                var rb = GetComponent<Rigidbody>();
                if (rb != null && rb.isKinematic)
                {
                    rb.isKinematic = false;
                    rb.useGravity = true;
                }
                return;
            }

            float linger = GetLingerDuration();
            float elapsed = Time.time - _spawnTime;

            // Permanece em física livre saltando e assentando no chão pelo período de linger
            if (elapsed < linger) return;

            if (!_isFlying)
            {
                _isFlying = true;
                var rb = GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.isKinematic = true;
                    rb.useGravity = false;
                }
            }

            Vector3 targetPos = targetNob.transform.position + Vector3.up * 0.85f;
            float flightDuration = elapsed - linger;
            float currentSpeed = Mathf.Lerp(8.0f, 26.0f, flightDuration * 2.2f);

            transform.position = Vector3.MoveTowards(transform.position, targetPos, currentSpeed * Time.deltaTime);

            if (IsServerStarted && !_collected)
            {
                float sqrDist = (transform.position - targetPos).sqrMagnitude;
                if (sqrDist <= 1.2f * 1.2f)
                {
                    var resInv = targetNob.GetComponent<ResourceInventory>();
                    if (resInv != null)
                    {
                        ServerCollect(targetNob.Owner, resInv);
                    }
                }
            }
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
