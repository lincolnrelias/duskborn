using Duskborn.Gameplay;
using UnityEngine;

namespace Duskborn.Gameplay.Equipment
{
    [CreateAssetMenu(fileName = "CleaveSkill", menuName = "Duskborn/Weapon Skills/Cleave")]
    public class CleaveSkill : WeaponSkill
    {
        [SerializeField] private float arcDegrees      = 180f;
        [SerializeField] private float damageMultiplier = 1f;

        public override void Use(CombatContext ctx)
        {
            ctx.Caster.ExecuteCleave(range, arcDegrees, damageMultiplier);
        }
    }
}
