using Duskborn.Gameplay;
using UnityEngine;

namespace Duskborn.Gameplay.Equipment
{
    public abstract class WeaponSkill : ScriptableObject
    {
        [SerializeField] public float            cooldown  = 5f;
        [SerializeField] public float            range     = 2f;
        [SerializeField] public WeaponActionData animation;

        public virtual bool CanUse(CombatContext ctx)
        {
            if (ctx.Target == null) return true;
            return Vector3.Distance(ctx.Caster.Transform.position, ctx.Target.position) <= range;
        }

        public abstract void Use(CombatContext ctx);
    }
}
