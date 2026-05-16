using Duskborn.Gameplay;
using UnityEngine;

namespace Duskborn.Gameplay.Equipment
{
    public abstract class WeaponBehaviour : ScriptableObject
    {
        // actionIndex: 0 = LMB action, 1 = RMB action, etc.
        public abstract void OnActionEvent(WeaponEventType type, int actionIndex, CombatContext ctx);
    }
}
