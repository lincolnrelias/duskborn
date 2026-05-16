using UnityEngine;

namespace Duskborn.Gameplay
{
    public interface ICombatEntity
    {
        Transform Transform { get; }
        void ExecuteBasicMelee();
        void ExecuteHeavyMelee();
        void ExecuteCleave(float range, float arcDegrees, float damageMultiplier);
    }
}
