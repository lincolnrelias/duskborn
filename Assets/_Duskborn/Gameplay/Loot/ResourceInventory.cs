using System;
using System.Collections.Generic;
using InventorySystem.Data;
using UnityEngine;

namespace Duskborn.Gameplay.Loot
{
    public class ResourceInventory : MonoBehaviour
    {
        public event Action<string, int> ResourceChanged;
        public int Revision { get; private set; }
        private readonly Dictionary<string, int> _counts = new();
        public IReadOnlyDictionary<string, int> Counts => _counts;
        public int GetCount(string resourceId) => resourceId != null && _counts.TryGetValue(resourceId, out int v) ? v : 0;
        public int GetCount(MaterialDefinition def) => def != null ? GetCount(def.Id) : 0;
        // Establishes a new-session baseline without marking the wallet as gameplay activity.
        // This keeps the infrastructure checkpoint load gate usable before the player acts.
        public void EnsureStartingAmount(string resourceId, int amount)
        {
            if (string.IsNullOrEmpty(resourceId) || amount <= 0 || GetCount(resourceId) >= amount) return;
            _counts[resourceId] = amount;
            Notify(resourceId);
        }
        public void Add(string resourceId, int amount)
        {
            if (string.IsNullOrEmpty(resourceId) || amount <= 0) return;
            Revision++;
            _counts[resourceId] = checked(GetCount(resourceId) + amount);
            Notify(resourceId);
        }
        public void Add(MaterialDefinition def, int amount) { if (def != null) Add(def.Id, amount); }
        public bool TrySpend(string resourceId, int amount) => TrySpendBatch(new Dictionary<string, int> { [resourceId ?? ""] = amount });
        public bool TrySpend(MaterialDefinition def, int amount) => def != null && TrySpend(def.Id, amount);
        public bool CanSpendBatch(IReadOnlyDictionary<string, int> costs)
        {
            if (costs == null || costs.Count == 0) return false;
            foreach (var pair in costs)
                if (string.IsNullOrEmpty(pair.Key) || pair.Value <= 0 || GetCount(pair.Key) < pair.Value) return false;
            return true;
        }
        public bool TrySpendBatch(IReadOnlyDictionary<string, int> costs)
        {
            if (!CanSpendBatch(costs)) return false;
            Revision++;
            // Commit every count before dispatching callbacks (no partial or duplicate-line spending).
            foreach (var pair in costs) _counts[pair.Key] -= pair.Value;
            foreach (var pair in costs) Notify(pair.Key);
            return true;
        }
        public void Restore(IReadOnlyDictionary<string, int> counts)
        {
            Revision++;
            var changed = new HashSet<string>(_counts.Keys);
            _counts.Clear();
            foreach (var pair in counts) if (!string.IsNullOrEmpty(pair.Key) && pair.Value > 0) { _counts[pair.Key] = pair.Value; changed.Add(pair.Key); }
            foreach (var id in changed) Notify(id);
        }
        private void Notify(string id)
        {
            if (ResourceChanged == null) return;
            foreach (Action<string, int> listener in ResourceChanged.GetInvocationList())
                try { listener(id, GetCount(id)); } catch (Exception e) { Debug.LogException(e); }
        }
    }
}
