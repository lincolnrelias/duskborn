using System;

namespace InventorySystem.Core
{
    public sealed class InventoryGrid
    {
        public int Columns { get; }
        public int Rows { get; }
        public int SlotCount => Columns * Rows;

        public InventoryGrid(int columns, int rows)
        {
            if (columns <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(columns));
            }

            if (rows <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(rows));
            }

            Columns = columns;
            Rows = rows;
        }

        public bool IsValidIndex(int index)
        {
            return index >= 0 && index < SlotCount;
        }

        public int ToIndex(int x, int y)
        {
            if (x < 0 || x >= Columns)
            {
                throw new ArgumentOutOfRangeException(nameof(x));
            }

            if (y < 0 || y >= Rows)
            {
                throw new ArgumentOutOfRangeException(nameof(y));
            }

            return (y * Columns) + x;
        }
    }
}
