using Duskborn.Gameplay;
using Duskborn.Gameplay.ActionBar;
using InventorySystem.Core;

namespace Duskborn.Gameplay.Items
{
    public sealed class AppleItem : InventoryItemBase, IRightClickAction
    {
        public float HealAmount { get; }

        public override InventoryItemKind Kind => InventoryItemKind.Material;

        public AppleItem(string id, string displayName, string description, float healAmount)
            : base(id, displayName, description, string.Empty)
        {
            HealAmount = healAmount;
        }

        public void OnRightClick(CombatContext ctx)
            => ctx.Combat.RequestConsumeItem(ctx.SlotIndex, HealAmount);
    }
}
