using Duskborn.Gameplay.ActionBar;
using UnityEngine;

namespace Duskborn.Gameplay.Equipment
{
    public abstract class WeaponSkill : ScriptableObject
    {
        [SerializeField] public float cooldown = 5f;

        public abstract void Use(ActionContext ctx);
    }
}
