using System.Collections.Generic;
using UnityEngine;

namespace InventorySystem.Core
{
    /// <summary>
    /// Shared static registry of item icons.
    /// Ensures that any icon known to InventoryInstaller, ActionBarInstaller,
    /// crafting recipes, or resource definitions is globally available when moving
    /// or swapping items between panels and the action bar.
    /// </summary>
    public static class ItemIconRegistry
    {
        private static readonly Dictionary<string, Texture2D> _icons = new();

        public static void Register(string id, Texture2D icon)
        {
            if (string.IsNullOrWhiteSpace(id) || icon == null) return;
            _icons[id] = icon;
            if (!string.IsNullOrWhiteSpace(icon.name))
            {
                _icons[icon.name] = icon;
            }
        }

        public static bool TryGetIcon(string id, out Texture2D icon)
        {
            if (!string.IsNullOrWhiteSpace(id) && _icons.TryGetValue(id, out icon))
            {
                return true;
            }

            icon = null;
            return false;
        }

        public static Texture2D Resolve(IInventoryItem item)
        {
            if (item == null) return null;

            if (!string.IsNullOrWhiteSpace(item.Id) && _icons.TryGetValue(item.Id, out var tex))
                return tex;

            if (!string.IsNullOrWhiteSpace(item.IconId) && _icons.TryGetValue(item.IconId, out tex))
                return tex;

            return null;
        }

        public static void Clear() => _icons.Clear();
    }
}
