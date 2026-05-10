using Duskborn.Gameplay.ActionBar;
using UnityEngine;

namespace Duskborn.Gameplay.Equipment
{
    [CreateAssetMenu(fileName = "MeleeWeaponBehaviour", menuName = "Duskborn/Weapon Behaviours/Melee")]
    public class MeleeWeaponBehaviour : WeaponBehaviour
    {
        public override void OnActionEvent(WeaponEventType type, int actionIndex, ActionContext ctx)
        {
            if (type != WeaponEventType.HitboxOpen) return;

            if (actionIndex == 0) ctx.Combat.TriggerAttack();
            if (actionIndex == 1) ctx.Combat.TriggerHeavyAttack();
        }
    }
}
