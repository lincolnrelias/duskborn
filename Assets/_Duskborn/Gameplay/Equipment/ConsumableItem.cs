using InventorySystem.Core;
using Duskborn.Gameplay.ActionBar;
using Duskborn.Gameplay.Player;

namespace Duskborn.Gameplay.Equipment
{
    /// <summary>
    /// Item consumível no inventário e barra de ação do jogador.
    /// Pode ser acionado com botão primário (LMB) para aplicar cura ou buffs temporários.
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
