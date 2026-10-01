using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Duskborn.Gameplay.Equipment
{
    /// <summary>
    /// Samples the vendor pose in the graph, then corrects the evaluated spine
    /// hierarchy in LateUpdate. Applying after humanoid retargeting avoids changing
    /// its body/hip solve when rotating upper-body stream transforms.
    /// </summary>
    public sealed class BowPoseAnchor : IDisposable
    {
        public const float MaxTiltCorrection = 25f;
        public static Quaternion CorrectTilt(Quaternion current, Quaternion reference, float weight)
        {
            Quaternion swing = Quaternion.FromToRotation(current * Vector3.up, reference * Vector3.up);
            Quaternion bounded = Quaternion.RotateTowards(Quaternion.identity, swing, MaxTiltCorrection);
            return Quaternion.Slerp(Quaternion.identity, bounded, Mathf.Clamp01(weight)) * current;
        }

        private NativeArray<Quaternion> referenceRotation;
        private AnimationScriptPlayable referenceTap;
        private readonly Transform spine, chest, upper;
        public Playable Output { get; }
        public float Weight { get; private set; }
        public bool FacingEnabled { get; private set; }
        public Quaternion ReferenceRotation => referenceRotation[0];
        public Quaternion FacingReferenceRotation => referenceRotation[1];
        public Quaternion InputSpineRotation { get; private set; }
        public Quaternion OutputSpineRotation { get; private set; }

        public static bool Supports(Animator animator) => animator != null && animator.isHuman &&
            animator.avatar != null && animator.avatar.isValid &&
            animator.GetBoneTransform(HumanBodyBones.Spine) != null;

        public BowPoseAnchor(PlayableGraph graph, Animator animator, Playable mixedPose)
        {
            spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            upper = animator.GetBoneTransform(HumanBodyBones.UpperChest);
            referenceRotation = new NativeArray<Quaternion>(2, Allocator.Persistent);
            referenceRotation[0] = referenceRotation[1] = Quaternion.identity;
            referenceTap = AnimationScriptPlayable.Create(graph, new ReadReference
            {
                spine = animator.BindStreamTransform(spine),
                upper = animator.BindStreamTransform(upper != null ? upper : spine),
                rotation = referenceRotation
            }, 1);
            Output = mixedPose;
        }

        public Playable ConnectClip(Playable clip)
        {
            referenceTap.ConnectInput(0, clip, 0, 1f);
            return referenceTap;
        }
        public void DisconnectClip()
        {
            SetWeight(0);
            if (referenceTap.IsValid() && referenceTap.GetInput(0).IsValid()) referenceTap.DisconnectInput(0);
        }
        public void SetFacing(bool enabled) => FacingEnabled = enabled && chest != null && upper != null;
        public void SetWeight(float weight) => Weight = Mathf.Clamp01(weight);

        // Call once after Animator evaluation, before capturing the final pose.
        public void ApplyPose()
        {
            if (Weight <= 0f || spine == null) return;
            InputSpineRotation = spine.rotation;
            if (FacingEnabled)
            {
                Quaternion delta = FacingReferenceRotation * Quaternion.Inverse(upper.rotation);
                Quaternion bounded = Quaternion.RotateTowards(Quaternion.identity, delta, 60f);
                Quaternion step = Quaternion.Slerp(Quaternion.identity, bounded, Weight / 3f);
                // Each child already inherits the preceding parent's step.
                spine.rotation = step * spine.rotation;
                chest.rotation = step * chest.rotation;
                upper.rotation = step * upper.rotation;
            }
            else spine.rotation = CorrectTilt(spine.rotation, ReferenceRotation, Weight);
            OutputSpineRotation = spine.rotation;
        }

        // Destroy the graph before releasing storage referenced by the sampling job.
        public void Dispose() { if (referenceRotation.IsCreated) referenceRotation.Dispose(); }
        private struct ReadReference : IAnimationJob
        {
            public TransformStreamHandle spine, upper;
            public NativeArray<Quaternion> rotation;
            public void ProcessRootMotion(AnimationStream stream) { }
            public void ProcessAnimation(AnimationStream stream)
            {
                if (spine.IsValid(stream)) rotation[0] = spine.GetRotation(stream);
                if (upper.IsValid(stream)) rotation[1] = upper.GetRotation(stream);
            }
        }
    }
}
