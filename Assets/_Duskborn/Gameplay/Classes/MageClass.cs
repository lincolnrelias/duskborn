using System.Collections.Generic;
using UnityEngine;
using Duskborn.Gameplay.Enemies;

namespace Duskborn.Gameplay.Classes
{
    /// <summary>
    /// Mage — AoE caster. SKELETON — abilities implemented after items/crafting.
    ///
    /// Passive (stub): kill with spell → restore mana; crit → random status effect.
    /// Active  (stub): Arcane Burst — short-range AoE explosion.
    /// Mana resource deferred to Phase 2.4.
    /// </summary>
    public class MageClass : ClassAbility
    {
        public override void TryUseAbility()
        {
            Debug.Log("[Mage] Arcane Burst — not yet implemented.");
        }
    }
}
