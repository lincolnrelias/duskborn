using Duskborn.Gameplay.ActionBar;
using UnityEngine;

namespace Duskborn.Gameplay.Equipment
{
    [CreateAssetMenu(fileName = "CleaveSkill", menuName = "Duskborn/Weapon Skills/Cleave")]
    public class CleaveSkill : WeaponSkill
    {
        [SerializeField] private float range          = 3f;
        [SerializeField] private float arcDegrees     = 180f;
        [SerializeField] private float damageMultiplier = 1f;

        public override void Use(ActionContext ctx)
        {
            ctx.Combat.TriggerCleave(range, arcDegrees, damageMultiplier);
        }
    }
}
