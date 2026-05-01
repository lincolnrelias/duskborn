namespace InventorySystem.UI
{
    public readonly struct ItemViewModel
    {
        public string Title { get; }
        public string Description { get; }
        public string IconPath { get; }

        public ItemViewModel(string title, string description, string iconPath)
        {
            Title = title;
            Description = description;
            IconPath = iconPath;
        }
    }
}
