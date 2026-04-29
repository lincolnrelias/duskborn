using System.Collections.Generic;
using UnityEngine;

namespace Duskborn.Gameplay.Loot
{
    public class ResourceInventory : MonoBehaviour
    {
        private readonly Dictionary<ResourceType, int> _counts = new();

        public int GetCount(ResourceType type) =>
            _counts.TryGetValue(type, out int v) ? v : 0;

        public void Add(ResourceType type, int amount)
        {
            _counts[type] = GetCount(type) + amount;
            Debug.Log($"[Resources] +{amount} {type}  (total: {_counts[type]})");
        }

        public bool TrySpend(ResourceType type, int amount)
        {
            if (GetCount(type) < amount) return false;
            _counts[type] -= amount;
            return true;
        }
    }
}
