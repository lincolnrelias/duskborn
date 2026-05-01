using InventorySystem.Core;
using UnityEngine;

namespace InventorySystem.Data
{
    public abstract class ItemDefinitionBase : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] [TextArea] private string description;
        [SerializeField] private Texture2D icon;

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public Texture2D Icon => icon;

        public abstract IInventoryItem CreateRuntimeItem();
    }
}
