using System.Collections.Generic;
using InventorySystem.Data;
using UnityEngine;

namespace Duskborn.Gameplay.Loot
{
    public class WorldDropRegistry : MonoBehaviour
    {
        public static WorldDropRegistry Instance { get; private set; }

        [SerializeField] private ItemDefinitionBase[] definitions;

        private readonly Dictionary<string, ItemDefinitionBase> _byId = new();

        private void Awake()
        {
            Instance = this;
            if (definitions == null) return;
            foreach (var def in definitions)
                if (def != null && !string.IsNullOrEmpty(def.Id))
                    _byId[def.Id] = def;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public GameObject GetDropPrefab(string id)
        {
            return _byId.TryGetValue(id, out var def) ? def.dropPrefab : null;
        }
    }
}
