using Duskborn.Gameplay.ActionBar;
using UnityEngine;

namespace Duskborn.Gameplay.Equipment
{
    /// <summary>
    /// Per-weapon-type behaviour asset. Defines what happens at each animation event.
    /// Subclass and create a ScriptableObject asset for each distinct weapon behaviour.
    /// </summary>
    public abstract class WeaponBehaviour : ScriptableObject
    {
        // actionIndex: 0 = LMB action, 1 = RMB action, etc.
        public abstract void OnActionEvent(WeaponEventType type, int actionIndex, ActionContext ctx);
    }
}
