using Duskborn.Gameplay.Equipment;
using Duskborn.Gameplay.Player;

namespace Duskborn.Gameplay.ActionBar
{
    public interface ILeftClickAction
    {
        void OnLeftClick(ActionContext ctx);
    }

    public interface IRightClickAction
    {
        void OnRightClick(ActionContext ctx);
    }

    public readonly struct ActionContext
    {
        public readonly PlayerCombat       Combat;
        public readonly PlayerStats        Stats;
        public readonly ActionBarService   ActionBar;
        public readonly int                SlotIndex;
        public readonly WeaponActionPlayer WeaponAnimator;

        public ActionContext(PlayerCombat combat, PlayerStats stats, ActionBarService actionBar,
                             int slotIndex, WeaponActionPlayer weaponAnimator = null)
        {
            Combat         = combat;
            Stats          = stats;
            ActionBar      = actionBar;
            SlotIndex      = slotIndex;
            WeaponAnimator = weaponAnimator;
        }
    }
}
