using InventorySystem.Core;
using Duskborn.Gameplay.ActionBar;
using Duskborn.Gameplay.Player;

namespace Duskborn.Gameplay.Equipment
{
    /// <summary>
    /// Consumable item in the player's inventory and action bar.
    /// Can be activated with the primary button (LMB) to apply healing or temporary buffs.
    /// </summary>
    public class ConsumableItem : InventoryItemBase, ILeftClickAction, IStackable
    {
        public override InventoryItemKind Kind => InventoryItemKind.Equipment;
        public int StackSize { get; private set; }
        public ConsumableEffectType EffectType { get; }
        public float EffectValue { get; }
        public float Duration { get; }

        public ConsumableItem(string id, string displayName, string description, string iconId,
            ConsumableEffectType effectType, float effectValue, float duration, int stackSize = 1)
            : base(id, displayName, description, iconId)
        {
            EffectType = effectType;
            EffectValue = effectValue;
            Duration = duration;
            StackSize = stackSize;
        }

        public void OnLeftClick(CombatContext context)
        {
            if (context.Stats == null || !context.Stats.IsAlive) return;

            var player = context.Stats.GetComponent<PlayerCombat>();
            if (player != null)
            {
                player.ApplyConsumable(this, context.SlotIndex);
            }
        }
    }
}
