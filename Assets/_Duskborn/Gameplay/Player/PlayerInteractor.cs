using System.Collections.Generic;
using FishNet;
using FishNet.Object;
using UnityEngine;
using Duskborn.Gameplay.Loot;

namespace Duskborn.Gameplay.Player
{
    [RequireComponent(typeof(PlayerBuffContainer))]
    public class PlayerInteractor : NetworkBehaviour
    {
        [SerializeField] private Transform dropSpawnPoint;
        [SerializeField] private float     pickupRange   = 2f;
        [SerializeField] private float     dropScatter   = 0.4f;

        private readonly HashSet<Chest> _chestsInRange = new();
        private readonly Collider[]     _overlapBuffer = new Collider[16];

        private Chest           _linked;
        private Chest           _prevLinked;
        private WorldItemPickup _linkedPickup;
        private WorldItemPickup _prevLinkedPickup;
        private WorldItemPickup _pendingCollect;

        private void Update()
        {
            if (!IsOwner) return;

            RefreshLinkedChest();
            RefreshLinkedPickup();

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

            if (Input.GetKeyDown(KeyCode.E) && _linked != null)
                RequestOpenChestRpc(_linked.GetComponent<NetworkObject>());

            if (_linkedPickup != null && _linkedPickup.IsCollectible && _linkedPickup != _pendingCollect)
            {
                _pendingCollect = _linkedPickup;
                RequestPickupResourceRpc(_linkedPickup.GetComponent<NetworkObject>());
            }

            if (_linkedPickup == null)
                _pendingCollect = null;
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
        private void RequestDropResourceRpc(string resourceId, int amount)
        {
            // ResourceInventory state is client-authoritative (populated only via TargetRpc).
            // Client already validated and spent before sending this RPC; just spawn the pickup.
            var prefab = WorldDropRegistry.Instance?.GetDropPrefab(resourceId);
            if (prefab == null)
            {
                Debug.LogWarning($"[PlayerInteractor] No drop prefab for '{resourceId}' in WorldDropRegistry.");
                return;
            }

            Vector2 disc = Random.insideUnitCircle * dropScatter;
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

        private void RefreshLinkedPickup()
        {
            _linkedPickup = null;
            float best  = float.MaxValue;
            int   count = Physics.OverlapSphereNonAlloc(transform.position, pickupRange, _overlapBuffer);
            for (int i = 0; i < count; i++)
            {
                var pickup = _overlapBuffer[i].GetComponentInParent<WorldItemPickup>();
                if (pickup == null || !pickup.gameObject.activeSelf) continue;
                float sq = SqDist(pickup);
                if (sq < best) { best = sq; _linkedPickup = pickup; }
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
