using UnityEngine;

namespace Duskborn.Gameplay
{
    [System.Flags]
    public enum TargetType
    {
        None       = 0,

        // Enemy categories
        Humanoid   = 1 << 0,
        Beast      = 1 << 1,

        // Resource node categories — separate bit range so enemy/node masks never overlap.
        Tree       = 1 << 8,
        MiningNode = 1 << 9,
        Stone      = 1 << 9,
        Ore        = 1 << 10,
        Bush       = 1 << 11,
    }

    public static class TargetTypeMasks
    {
        public const TargetType EnemyTypes = TargetType.Humanoid | TargetType.Beast;
        public const TargetType NodeTypes  = TargetType.Tree | TargetType.MiningNode | TargetType.Ore | TargetType.Bush;
    }

    // Restricts which TargetType flags a field exposes in the inspector — see TargetTypeFilterDrawer.
    public sealed class TargetTypeFilterAttribute : PropertyAttribute
    {
        public readonly TargetType AllowedMask;
        public TargetTypeFilterAttribute(TargetType allowedMask) => AllowedMask = allowedMask;
    }
}
