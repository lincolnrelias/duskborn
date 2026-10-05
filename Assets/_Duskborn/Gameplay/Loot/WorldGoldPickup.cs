using FishNet;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Duskborn.Gameplay.Loot
{
    public class WorldGoldPickup : NetworkBehaviour
    {
        [SerializeField] private string    outlineLayerName = "GreenOutline";
        [SerializeField] private Renderer  outlineRenderer;
        [SerializeField] private AudioClip pickupClip;

        private readonly SyncVar<int>  _amount      = new();
        private readonly SyncVar<bool> _collectible = new();
        private readonly SyncVar<NetworkObject> _targetPlayer = new();
        [SerializeField] private float lingerDuration = WorldItemPickup.DefaultLingerDuration;
        private bool _collected;
        private bool _isFlying;
        private float _spawnTime;

        public bool IsCollectible => _collectible.Value;
        public NetworkObject TargetPlayer => _targetPlayer.Value;

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
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            var visuals = GetComponent<DroppedItemVisuals>();
            if (visuals == null)
                visuals = gameObject.AddComponent<DroppedItemVisuals>();
            visuals.SetupGold();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            Invoke(nameof(SetCollectible), _targetPlayer.Value != null ? GetLingerDuration() : 1f);
        }

        private void SetCollectible() => _collectible.Value = true;

        private float GetLingerDuration() => WorldItemPickup.GetLingerDuration(NetworkObject, lingerDuration);

        public void ServerInitialize(int amount, NetworkObject targetPlayer = null)
        {
            _amount.Value = amount;
            _targetPlayer.Value = targetPlayer;
            if (targetPlayer != null)
            {
                CancelInvoke(nameof(SetCollectible));
                Invoke(nameof(SetCollectible), GetLingerDuration());
            }
        }

        private void LateUpdate()
        {
            if (_collected) return;
            var target = _targetPlayer.Value;
            if (target == null || !target.gameObject.activeInHierarchy)
            {
                if (_isFlying)
                {
                    GetComponent<DroppedItemVisuals>()?.BeginDropMotion(Vector3.zero, Vector3.zero);
                    _isFlying = false;
                }
                return;
            }

            float flightDuration = Time.time - _spawnTime - GetLingerDuration();
            if (flightDuration < 0f) return;

            if (!_isFlying)
            {
                _isFlying = true;
                var visuals = GetComponent<DroppedItemVisuals>();
                if (visuals == null) visuals = gameObject.AddComponent<DroppedItemVisuals>();
                visuals.BeginCollectionMotion();
            }

            Vector3 targetPosition = target.transform.position + Vector3.up * 0.85f;
            transform.position = Vector3.MoveTowards(transform.position, targetPosition,
                WorldItemPickup.GetFlightSpeed(flightDuration) * Time.deltaTime);

            if (IsServerStarted && (transform.position - targetPosition).sqrMagnitude <= 1.2f * 1.2f)
                ServerCollect(target.Owner);
        }

        public void ServerThrow(Vector3 impulse, float torque = 0f)
        {
            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                var visuals = GetComponent<DroppedItemVisuals>();
                if (visuals == null) visuals = gameObject.AddComponent<DroppedItemVisuals>();
                visuals.BeginDropMotion(impulse, Random.insideUnitSphere * Mathf.Max(0f, torque));
            }
            else
                DuskLog.Warn(LogChannel.Loot, $"{name}: ServerThrow called but no Rigidbody found.");
        }

        public void ServerCollect(NetworkConnection requester)
        {
            if (!IsServerStarted || _collected || !_collectible.Value) return;
            _collected = true;

            GoldManager.Instance?.AddGold(_amount.Value);
            DuskLog.Log(LogChannel.Loot, $"WorldGoldPickup: +{_amount.Value}g collected.");

            PlayPickupSoundRpc(requester, transform.position);
            InstanceFinder.ServerManager.Despawn(NetworkObject, DespawnType.Destroy);
        }

        [TargetRpc]
        private void PlayPickupSoundRpc(NetworkConnection conn, Vector3 pos)
        {
            float volume = 1.0f;
            if (Duskborn.Audio.AudioDatabase.Instance != null)
            {
                if (pickupClip == null) pickupClip = Duskborn.Audio.AudioDatabase.Instance.Loot.goldPickupClip;
                volume = Duskborn.Audio.AudioDatabase.Instance.Loot.goldVolume;
            }
            else if (pickupClip == null)
            {
                pickupClip = Resources.Load<AudioClip>("SFX/gold_pickup");
            }

            if (pickupClip != null)
            {
                if (Duskborn.Audio.AudioManager.Instance != null)
                    Duskborn.Audio.AudioManager.Instance.PlayAtPoint(pickupClip, pos, volume);
                else
                    AudioSource.PlayClipAtPoint(pickupClip, pos);
            }
        }

        public void SetOutline(bool show)
        {
            if (outlineRenderer == null) return;
            outlineRenderer.renderingLayerMask = show ? _baseMask | _outlineMask : _baseMask;
        }
    }
}
