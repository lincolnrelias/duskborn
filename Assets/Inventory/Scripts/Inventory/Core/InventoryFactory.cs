using System.Collections.Generic;

namespace InventorySystem.Core
{
    /// <summary>
    /// Small helper to create inventory instances from host projects.
    /// </summary>
    public static class InventoryFactory
    {
        public static IInventory Create(int columns, int rows, IReadOnlyList<IItemValidationRule> rules = null)
        {
            var grid = new InventoryGrid(columns, rows);
            return new InventoryService(grid, rules);
        }
    }
}
