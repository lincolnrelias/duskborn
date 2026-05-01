using System;

namespace InventorySystem.Core
{
    public interface IInventoryItem
    {
        string Id { get; }
        string DisplayName { get; }
        string Description { get; }
        string IconId { get; }
        InventoryItemKind Kind { get; }
    }

    public enum InventoryItemKind
    {
        Equipment = 0,
        Material = 1
    }

    [Serializable]
    public abstract class InventoryItemBase : IInventoryItem
    {
        public string Id { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public string IconId { get; }
        public abstract InventoryItemKind Kind { get; }

        protected InventoryItemBase(string id, string displayName, string description, string iconId)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
            Description = description ?? string.Empty;
            IconId = iconId ?? string.Empty;
        }
    }
}
