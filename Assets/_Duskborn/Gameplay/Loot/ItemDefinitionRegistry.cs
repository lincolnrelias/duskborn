using System.Collections.Generic;
using UnityEngine;

namespace Duskborn.Gameplay.Loot
{
    public class ItemDefinitionRegistry : MonoBehaviour
    {
        public static ItemDefinitionRegistry Instance { get; private set; }

        [SerializeField] private ItemDefinition[] _definitions;

        private readonly Dictionary<string, ItemDefinition> _byId = new();

        private void Awake()
        {
            Instance = this;
            if (_definitions == null) return;
            foreach (var def in _definitions)
                if (def != null) _byId[def.name] = def;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public ItemDefinition GetById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            _byId.TryGetValue(id, out var def);
            return def;
        }
    }
}
