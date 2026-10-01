using UnityEngine;
namespace Duskborn.Gameplay.Equipment
{
    [CreateAssetMenu(menuName="Duskborn/Bow Animation Set")]
    public sealed class BowAnimationSet : ScriptableObject
    {
        public AnimationClip load, hold, release;
        public AnimationClip idle, forward, backward, left, right;
        public bool IsValid => load != null && hold != null && release != null &&
            load.length > 0 && hold.length > 0 && release.length > 0 && idle != null && forward != null && backward != null && left != null && right != null;
    }
}
