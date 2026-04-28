using System.Collections.Generic;
using UnityEngine;
using Duskborn.Gameplay.Loot;

namespace Duskborn.Gameplay.Player
{
    // Attach to the player alongside a SphereCollider (IsTrigger = true).
    // Detects nearby chests, stays linked to the closest one, opens it on E.
    [RequireComponent(typeof(PlayerInventory))]
    public class PlayerInteractor : MonoBehaviour
    {
        private readonly HashSet<Chest> _chestsInRange = new();
        private Chest           _linked;
        private Chest           _prevLinked;
        private PlayerInventory _inventory;

        private void Awake() => _inventory = GetComponent<PlayerInventory>();

        private void Update()
        {
            RefreshLinked();

            if (_linked != _prevLinked)
            {
                _prevLinked?.SetOutline(false);
                _linked?.SetOutline(true);
                _prevLinked = _linked;
            }

            if (_linked != null && Input.GetKeyDown(KeyCode.E))
                _linked.TryOpen(_inventory);
        }

        private void RefreshLinked()
        {
            _linked = null;
            float best = float.MaxValue;

            foreach (var chest in _chestsInRange)
            {
                if (chest == null || !chest.gameObject.activeSelf) continue;

                float sqDist = (chest.transform.position - transform.position).sqrMagnitude;
                if (sqDist < best) { best = sqDist; _linked = chest; }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            var chest = other.GetComponentInParent<Chest>();
            if (chest != null) _chestsInRange.Add(chest);
        }

        private void OnTriggerExit(Collider other)
        {
            var chest = other.GetComponentInParent<Chest>();
            if (chest != null) _chestsInRange.Remove(chest);
        }

        private void OnDrawGizmosSelected()
        {
            var col = GetComponent<SphereCollider>();
            if (col == null) return;
            Gizmos.color = new Color(0f, 1f, 1f, 0.25f);
            Gizmos.DrawWireSphere(transform.position + col.center, col.radius);
        }
    }
}
