using Duskborn.Gameplay.ActionBar;
using Duskborn.Gameplay.Equipment;
using Duskborn.Gameplay.Player;
using UnityEngine;

namespace Duskborn.Gameplay
{
    public class CombatContext
    {
        public readonly ICombatEntity      Caster;
        public readonly Transform          Target;
        // Player-specific — null when the caster is an enemy.
        public readonly PlayerCombat       Combat;
        public readonly PlayerStats        Stats;
        public readonly ActionBarService   ActionBar;
        public readonly int                SlotIndex;
        public readonly WeaponActionPlayer WeaponAnimator;

        // Player constructor.
        public CombatContext(PlayerCombat combat, PlayerStats stats,
                             ActionBarService actionBar, int slotIndex,
                             WeaponActionPlayer weaponAnimator = null)
        {
            Caster         = combat;
            Target         = null;
            Combat         = combat;
            Stats          = stats;
            ActionBar      = actionBar;
            SlotIndex      = slotIndex;
            WeaponAnimator = weaponAnimator;
        }

        // Enemy constructor.
        public CombatContext(ICombatEntity caster, Transform target)
        {
            Caster         = caster;
            Target         = target;
            Combat         = null;
            Stats          = null;
            ActionBar      = null;
            SlotIndex      = -1;
            WeaponAnimator = null;
        }
    }
}
