using System.Collections.Generic;
using InventorySystem.Data;
using UnityEngine;

namespace Duskborn.Gameplay.Loot
{
    public class WorldDropRegistry : MonoBehaviour
    {
        private static WorldDropRegistry _instance;
        public static WorldDropRegistry Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<WorldDropRegistry>();
                    if (_instance == null)
                    {
                        var go = new GameObject("[WorldDropRegistry]");
                        _instance = go.AddComponent<WorldDropRegistry>();
                    }
                }
                return _instance;
            }
            private set => _instance = value;
        }

        [SerializeField] private ItemDefinitionBase[] definitions;

        private readonly Dictionary<string, ItemDefinitionBase> _byId = new(System.StringComparer.OrdinalIgnoreCase);

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;

            if (definitions != null)
            {
                foreach (var def in definitions)
                    if (def != null && !string.IsNullOrEmpty(def.Id))
                        _byId[def.Id] = def;
            }
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        public GameObject GetDropPrefab(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (_byId.TryGetValue(id, out var def) && def != null) return def.dropPrefab;
            def = GetDefinition(id);
            return def != null ? def.dropPrefab : null;
        }

        public ItemDefinitionBase GetDefinition(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (_byId.TryGetValue(id, out var def) && def != null) return def;

#if UNITY_EDITOR
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:ItemDefinitionBase");
            foreach (var g in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(g);
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemDefinitionBase>(path);
                if (asset != null && string.Equals(asset.Id, id, System.StringComparison.OrdinalIgnoreCase))
                {
                    _byId[id] = asset;
                    return asset;
                }
            }
#endif
            return null;
        }

        public void Register(ItemDefinitionBase def)
        {
            if (def != null && !string.IsNullOrEmpty(def.Id))
                _byId[def.Id] = def;
        }
    }
}
