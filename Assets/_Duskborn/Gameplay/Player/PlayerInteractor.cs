using System;
using System.Collections.Generic;
using FishNet;
using FishNet.Object;
using UnityEngine;
using Duskborn.Core;
using Duskborn.Gameplay.Hotkeys;
using Duskborn.Gameplay.Loot;

namespace Duskborn.Gameplay.Player
{
    [RequireComponent(typeof(PlayerBuffContainer))]
    public class PlayerInteractor : NetworkBehaviour
    {
        [SerializeField] private Transform dropSpawnPoint;
        [SerializeField] private float     pickupRange = 2f;
        [SerializeField] private float     dropScatter = 0.4f;

        private readonly HashSet<Chest> _chestsInRange = new();
        private readonly Collider[]     _overlapBuffer = new Collider[16];

        private Chest           _linked;
        private Chest           _prevLinked;
        private WorldItemPickup _linkedPickup;
        private WorldItemPickup _prevLinkedPickup;
        private WorldItemPickup _pendingCollect;
        private WorldGoldPickup _linkedGoldPickup;
        private WorldGoldPickup _prevLinkedGoldPickup;
        private WorldGoldPickup _pendingGoldCollect;

        private Action _onInteract;

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!IsOwner) return;

            _onInteract = TryOpenLinkedChest;
            var hk = HotkeyManager.Instance;
            if (hk != null)
                hk.Register(HotkeyManager.Interact, _onInteract);
            else
                DuskLog.Warn(LogChannel.PlayerInteractor, "PlayerInteractor: HotkeyManager not found in scene.");
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (!IsOwner) return;
            HotkeyManager.Instance?.Unregister(HotkeyManager.Interact, _onInteract);
        }

        private void Update()
        {
            if (!IsOwner) return;

            RefreshLinkedChest();
            RefreshLinkedPickups();

            if (_linked != _prevLinked)
            {
                _prevLinked?.SetOutline(false);
                _linked?.SetOutline(true);
                _prevLinked = _linked;
            }

            if (_linkedPickup != _prevLinkedPickup)
            {
                _prevLinkedPickup?.SetOutline(false);
                _linkedPickup?.SetOutline(true);
                _prevLinkedPickup = _linkedPickup;
            }

            if (_linkedPickup != null && _linkedPickup.IsCollectible && _linkedPickup != _pendingCollect)
            {
                _pendingCollect = _linkedPickup;
                RequestPickupResourceRpc(_linkedPickup.GetComponent<NetworkObject>());
            }

            if (_linkedPickup == null)
                _pendingCollect = null;

            if (_linkedGoldPickup != _prevLinkedGoldPickup)
            {
                _prevLinkedGoldPickup?.SetOutline(false);
                _linkedGoldPickup?.SetOutline(true);
                _prevLinkedGoldPickup = _linkedGoldPickup;
            }

            if (_linkedGoldPickup != null && _linkedGoldPickup.IsCollectible && _linkedGoldPickup != _pendingGoldCollect)
            {
                _pendingGoldCollect = _linkedGoldPickup;
                RequestPickupGoldRpc(_linkedGoldPickup.GetComponent<NetworkObject>());
            }

            if (_linkedGoldPickup == null)
                _pendingGoldCollect = null;
        }

        private void TryOpenLinkedChest()
        {
            if (_linked != null)
                RequestOpenChestRpc(_linked.GetComponent<NetworkObject>());
        }

        public void DropResource(string resourceId, int amount)
        {
            if (IsOwner) RequestDropResourceRpc(resourceId, amount);
        }

        [ServerRpc]
        private void RequestOpenChestRpc(NetworkObject chestNob)
        {
            var chest = chestNob.GetComponent<Chest>();
            chest?.ServerOpen(Owner, GetComponent<PlayerBuffContainer>());
        }

        [ServerRpc]
        private void RequestPickupResourceRpc(NetworkObject pickupNob)
        {
            pickupNob.GetComponent<WorldItemPickup>()?.ServerCollect(Owner, GetComponent<ResourceInventory>());
        }

        [ServerRpc]
        private void RequestPickupGoldRpc(NetworkObject pickupNob)
        {
            pickupNob.GetComponent<WorldGoldPickup>()?.ServerCollect(Owner);
        }

        [ServerRpc]
        private void RequestDropResourceRpc(string resourceId, int amount)
        {
            var prefab = WorldDropRegistry.Instance?.GetDropPrefab(resourceId);
            if (prefab == null)
            {
                DuskLog.Warn(LogChannel.PlayerInteractor, $"No drop prefab for '{resourceId}' in WorldDropRegistry.");
                return;
            }

            Vector2 disc = UnityEngine.Random.insideUnitCircle * dropScatter;
            Vector3 pos  = ResolveDropPosition() + new Vector3(disc.x, 0f, disc.y);
            var go = Instantiate(prefab, pos, Quaternion.identity);
            InstanceFinder.ServerManager.Spawn(go);
            go.GetComponent<WorldItemPickup>()?.ServerInitialize(resourceId, amount);
        }

        private Vector3 ResolveDropPosition()
        {
            if (dropSpawnPoint == null)
                return transform.position + Vector3.up * 0.3f;

            Vector3 origin  = dropSpawnPoint.position;
            float   yOffset = dropSpawnPoint.localPosition.y;

            if (Physics.Raycast(origin, Vector3.up, out RaycastHit hit))
                return hit.point + Vector3.up * yOffset;

            return origin;
        }

        private void RefreshLinkedChest()
        {
            _linked = null;
            float best = float.MaxValue;
            foreach (var chest in _chestsInRange)
            {
                if (chest == null || !chest.gameObject.activeSelf) continue;
                float sq = SqDist(chest);
                if (sq < best) { best = sq; _linked = chest; }
            }
        }

        private void RefreshLinkedPickups()
        {
            _linkedPickup     = null;
            _linkedGoldPickup = null;
            float bestItem = float.MaxValue;
            float bestGold = float.MaxValue;
            int   count    = Physics.OverlapSphereNonAlloc(transform.position, pickupRange, _overlapBuffer);
            for (int i = 0; i < count; i++)
            {
                var item = _overlapBuffer[i].GetComponentInParent<WorldItemPickup>();
                if (item != null && item.gameObject.activeSelf)
                {
                    float sq = SqDist(item);
                    if (sq < bestItem) { bestItem = sq; _linkedPickup = item; }
                }

                var gold = _overlapBuffer[i].GetComponentInParent<WorldGoldPickup>();
                if (gold != null && gold.gameObject.activeSelf)
                {
                    float sq = SqDist(gold);
                    if (sq < bestGold) { bestGold = sq; _linkedGoldPickup = gold; }
                }
            }
        }

        private float SqDist(Component c) => (c.transform.position - transform.position).sqrMagnitude;

        private void OnTriggerEnter(Collider other)
        {
            if (!IsOwner) return;
            var chest = other.GetComponentInParent<Chest>();
            if (chest != null) _chestsInRange.Add(chest);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsOwner) return;
            var chest = other.GetComponentInParent<Chest>();
            if (chest != null) _chestsInRange.Remove(chest);
        }

        private void OnDrawGizmosSelected()
        {
            var col = GetComponent<SphereCollider>();
            if (col != null)
            {
                Gizmos.color = new Color(0f, 1f, 1f, 0.25f);
                Gizmos.DrawWireSphere(transform.position + col.center, col.radius);
            }

            Gizmos.color = new Color(1f, 0.85f, 0f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, pickupRange);
        }
    }
}
