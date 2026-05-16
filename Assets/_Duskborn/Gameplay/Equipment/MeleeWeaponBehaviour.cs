using Duskborn.Gameplay;
using UnityEngine;

namespace Duskborn.Gameplay.Equipment
{
    [CreateAssetMenu(fileName = "MeleeWeaponBehaviour", menuName = "Duskborn/Weapon Behaviours/Melee")]
    public class MeleeWeaponBehaviour : WeaponBehaviour
    {
        public override void OnActionEvent(WeaponEventType type, int actionIndex, CombatContext ctx)
        {
            if (type != WeaponEventType.HitboxOpen) return;

            if (actionIndex == 0) ctx.Caster.ExecuteBasicMelee();
            if (actionIndex == 1) ctx.Caster.ExecuteHeavyMelee();
        }
    }
}
