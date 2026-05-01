using System;
using System.Collections.Generic;
using InventorySystem.Data;
using UnityEngine;

namespace Duskborn.Gameplay.Loot
{
    public class ResourceInventory : MonoBehaviour
    {
        public event Action<string, int> ResourceChanged;

        private readonly Dictionary<string, int> _counts = new();

        public IReadOnlyDictionary<string, int> Counts => _counts;

        public int GetCount(string resourceId) =>
            _counts.TryGetValue(resourceId, out int v) ? v : 0;

        public int GetCount(MaterialDefinition def) => GetCount(def.Id);

        public void Add(string resourceId, int amount)
        {
            _counts[resourceId] = GetCount(resourceId) + amount;
            Debug.Log($"[Resources] +{amount} {resourceId}  (total: {_counts[resourceId]})");
            ResourceChanged?.Invoke(resourceId, _counts[resourceId]);
        }

        public void Add(MaterialDefinition def, int amount) => Add(def.Id, amount);

        public bool TrySpend(string resourceId, int amount)
        {
            if (GetCount(resourceId) < amount) return false;
            _counts[resourceId] -= amount;
            ResourceChanged?.Invoke(resourceId, _counts[resourceId]);
            return true;
        }

        public bool TrySpend(MaterialDefinition def, int amount) => TrySpend(def.Id, amount);
    }
}
