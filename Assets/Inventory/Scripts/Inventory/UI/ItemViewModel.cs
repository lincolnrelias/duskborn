namespace InventorySystem.UI
{
    public readonly struct ItemViewModel
    {
        public string Title       { get; }
        public string Description { get; }
        public string IconPath    { get; }
        public int    StackSize   { get; }

        public ItemViewModel(string title, string description, string iconPath, int stackSize = 0)
        {
            Title       = title;
            Description = description;
            IconPath    = iconPath;
            StackSize   = stackSize;
        }
    }
}
