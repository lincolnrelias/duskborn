using UnityEngine;
namespace Duskborn.Gameplay.Equipment
{
    [CreateAssetMenu(menuName="Duskborn/Bow Animation Set")]
    public sealed class BowAnimationSet : ScriptableObject
    {
        public AnimationClip load, hold, release;
        public AnimationClip idle, forward, backward, left, right;
        public AnimationClip forwardLeft, forwardRight, backwardLeft, backwardRight;
        [Tooltip("Cycle offsets relative to forward, aligned by matching left/right foot contact.")]
        public Vector4 movementPhaseOffsets;
        public Vector4 diagonalPhaseOffsets;
        [Tooltip("Distance covered by one vendor root-motion cycle, in metres. Used only for gait cadence.")]
        public float movementStrideLength;
        public bool HasDiagonals => forwardLeft != null && forwardRight != null && backwardLeft != null && backwardRight != null;
        public bool IsValid => load != null && hold != null && release != null &&
            load.length > 0 && hold.length > 0 && release.length > 0 && idle != null && forward != null && backward != null && left != null && right != null;
    }
}
