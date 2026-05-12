using Duskborn.Gameplay.ActionBar;
using UnityEngine;

namespace Duskborn.Gameplay.Equipment
{
    public abstract class WeaponSkill : ScriptableObject
    {
        [SerializeField] public float            cooldown  = 5f;
        [SerializeField] public WeaponActionData animation;

        public abstract void Use(ActionContext ctx);
    }
}
