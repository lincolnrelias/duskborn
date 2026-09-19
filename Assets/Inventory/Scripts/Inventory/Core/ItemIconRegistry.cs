using System.Collections.Generic;
using UnityEngine;

namespace InventorySystem.Core
{
    /// <summary>
    /// Registro compartilhado e estático de ícones de itens.
    /// Garante que qualquer ícone conhecido pelo InventoryInstaller, ActionBarInstaller,
    /// receitas de crafting ou definições de recursos esteja acessível globalmente ao mover
    /// ou trocar itens entre diferentes painéis e a barra de ação.
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
