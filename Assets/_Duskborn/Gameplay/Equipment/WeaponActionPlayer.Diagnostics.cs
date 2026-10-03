#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

namespace Duskborn.Gameplay.Equipment
{
    public partial class WeaponActionPlayer
    {
        // Opt-in observer only: never evaluates the graph or modifies a pose.
        public event Action<AnimationDiagnosticFrame> AnimationDiagnosticSample;
        public Animator DiagnosticAnimator => animator;

        [Serializable]
        public sealed class AnimationDiagnosticFrame
        {
            public int frame;
            public float time, deltaTime, actionWeight, actionTime, actionSpeed, releaseTime, aimPitch;
            public string weapon, actionClip, mask, phase;
            public bool aiming, rootMotion, preserveLocomotion, humanoid, actionConnected, shotRequested, bowFacingEnabled;
            public bool controllerGrounded, animationGrounded;
            public float movementPhase;
            public Vector3 localVelocity;
            public float torsoLeftLean, bowAnchorWeight, bowSpineReferenceError, bowSpineTiltError, bowUpperChestReferenceError;
            public List<DiagnosticLayer> layers = new List<DiagnosticLayer>();
            public List<DiagnosticBone> bones = new List<DiagnosticBone>();
        }

        [Serializable]
        public sealed class DiagnosticLayer
        {
            public string name;
            public float weight, velocityX, velocityY, animatorVelocityX, animatorVelocityY;
            public int currentState, nextState;
            public float currentTime, nextTime, transitionTime;
            public bool inTransition;
            public List<DiagnosticClip> currentClips = new List<DiagnosticClip>();
            public List<DiagnosticClip> nextClips = new List<DiagnosticClip>();
        }

        [Serializable]
        public sealed class DiagnosticClip
        {
            public string name;
            public float weight;
        }

        [Serializable]
        public sealed class DiagnosticBone
        {
            public string name;
            public Quaternion localRotation, animatorSpaceRotation;
            public Vector3 animatorSpacePosition;
            public float leftLean;
        }

        private static readonly HumanBodyBones[] DiagnosticBones =
        {
            HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest,
            HumanBodyBones.UpperChest, HumanBodyBones.LeftHand, HumanBodyBones.RightHand,
            HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot
        };

        public AvatarMask DiagnosticMask => ResolveMask(
            (!_isPlaying && _retainedBowMovement == null) || _activeData == null || _activeData.PreserveLocomotion,
            (_isPlaying || _retainedBowMovement != null) && _activeData != null
                ? _activeData.ResolveMask(_activeWeapon != null ? _activeWeapon.ActionMask : _equippedActionMask)
                : _equippedActionMask);

        private void CaptureAnimationDiagnostics()
        {
            if (AnimationDiagnosticSample == null || animator == null ||
                !_graph.IsValid() || !_controllerPlayable.IsValid()) return;
            var bowPlayback = _authoredBow ?? _retainedBowMovement;
            var sample = new AnimationDiagnosticFrame
            {
                frame = Time.frameCount, time = Time.unscaledTime, deltaTime = Time.unscaledDeltaTime,
                weapon = _activeWeapon != null ? _activeWeapon.DisplayName : "",
                actionClip = bowPlayback != null ? bowPlayback.ClipName : _isPlaying && _activeClip != null ? _activeClip.name : "",
                mask = DiagnosticMask != null ? DiagnosticMask.name : "",
                // A disconnected mixer slot can retain a weight; it contributes no pose.
                actionConnected = (_isPlaying || _retainedBowMovement != null) && _layerMixer.GetInput(1).IsValid(),
                actionWeight = (_isPlaying || _retainedBowMovement != null) && _layerMixer.GetInput(1).IsValid() ? _layerMixer.GetInputWeight(1) : 0f,
                actionTime = bowPlayback != null ? (float)bowPlayback.Clock.ActionTime : _isPlaying ? (float)ActionTime : 0f,
                shotRequested = _shotRequested,
                actionSpeed = _authoredBow != null ? (_authoredBow.Clock.Phase == AuthoredBowClock.Stage.Hold ? 0f : _activeData.BaseSpeed * _runtimeSpeedMultiplier) : _isPlaying && _clipPlayable.IsValid() ? (float)_clipPlayable.GetSpeed() : 0f,
                releaseTime = _bowReleaseTime, aimPitch = _aimPitch,
                aiming = IsRangedAimAction, rootMotion = animator.applyRootMotion,
                preserveLocomotion = !_isPlaying || _activeData == null || _activeData.PreserveLocomotion,
                humanoid = animator.isHuman,
                phase = _retainedBowMovement != null ? "BetweenShots" : _authoredBow != null ? _authoredBow.Clock.Phase.ToString() : !_isPlaying ? "Locomotion" : !_rangedAimAction ? "Action" :
                    !_shotRequested ? (_clipPlayable.GetSpeed() == 0 ? "Hold" : "Draw") : "ReleaseRequested"
            };
            var cc = GetComponent<CharacterController>();
            if (cc != null)
            {
                sample.localVelocity = transform.InverseTransformDirection(cc.velocity);
                sample.controllerGrounded = cc.isGrounded;
            }
            sample.animationGrounded = _controllerPlayable.GetBool("IsGrounded");
            for (int i = 0; i < _controllerPlayable.GetLayerCount(); i++)
            {
                var current = _controllerPlayable.GetCurrentAnimatorStateInfo(i);
                var next = _controllerPlayable.GetNextAnimatorStateInfo(i);
                var layer = new DiagnosticLayer
                {
                    name = _controllerPlayable.GetLayerName(i), weight = i == 0 ? 1f : _controllerPlayable.GetLayerWeight(i),
                    currentState = current.fullPathHash, currentTime = current.normalizedTime,
                    nextState = next.fullPathHash, nextTime = next.normalizedTime,
                    inTransition = _controllerPlayable.IsInTransition(i),
                    transitionTime = _controllerPlayable.GetAnimatorTransitionInfo(i).normalizedTime,
                    velocityX = _controllerPlayable.GetFloat("VelocityX"), velocityY = _controllerPlayable.GetFloat("VelocityY"),
                    animatorVelocityX = animator.GetFloat("VelocityX"), animatorVelocityY = animator.GetFloat("VelocityY")
                };
                foreach (var clip in _controllerPlayable.GetCurrentAnimatorClipInfo(i))
                    layer.currentClips.Add(new DiagnosticClip { name = clip.clip.name, weight = clip.weight });
                foreach (var clip in _controllerPlayable.GetNextAnimatorClipInfo(i))
                    layer.nextClips.Add(new DiagnosticClip { name = clip.clip.name, weight = clip.weight });
                sample.layers.Add(layer);
            }
            var movementSource = _authoredBow ?? _retainedBowMovement;
            if (movementSource != null)
            {
                sample.movementPhase=movementSource.MovementPhase;
                var movement = new DiagnosticLayer { name = "Authored Aim Locomotion", weight = _aimLocomotionMixer.GetInputWeight(1) };
                for (int i = 0; i < movementSource.MovementClipCount; i++)
                    movement.currentClips.Add(new DiagnosticClip { name = movementSource.MovementClipName(i), weight = movementSource.Movement.GetInputWeight(i) });
                sample.layers.Add(movement);
            }
            if (animator.isHuman && animator.avatar != null && animator.avatar.isValid)
            {
                foreach (var id in DiagnosticBones)
                {
                    var bone = animator.GetBoneTransform(id);
                    if (bone == null) continue;
                    Vector3 up = animator.transform.InverseTransformDirection(bone.up);
                    sample.bones.Add(new DiagnosticBone
                    {
                        name = id.ToString(), localRotation = bone.localRotation,
                        animatorSpaceRotation = Quaternion.Inverse(animator.transform.rotation) * bone.rotation,
                        animatorSpacePosition = animator.transform.InverseTransformPoint(bone.position),
                        leftLean = Mathf.Atan2(-up.x, up.y) * Mathf.Rad2Deg
                    });
                }
                var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                var chest = animator.GetBoneTransform(HumanBodyBones.UpperChest) ?? animator.GetBoneTransform(HumanBodyBones.Chest);
                if (hips != null && chest != null)
                {
                    var axis = animator.transform.InverseTransformDirection(chest.position - hips.position);
                    sample.torsoLeftLean = Mathf.Atan2(-axis.x, axis.y) * Mathf.Rad2Deg;
                }
            }
            sample.bowFacingEnabled = _bowPoseAnchor?.FacingEnabled ?? false;
            sample.bowAnchorWeight = _bowPoseAnchor?.Weight ?? 0f;
            if (sample.bowAnchorWeight > 0f && animator.isHuman)
            {
                Quaternion spineRotation = animator.GetBoneTransform(HumanBodyBones.Spine).rotation;
                sample.bowSpineReferenceError = Quaternion.Angle(spineRotation, _bowPoseAnchor.ReferenceRotation);
                sample.bowSpineTiltError = Vector3.Angle(spineRotation * Vector3.up,
                    _bowPoseAnchor.ReferenceRotation * Vector3.up);
            }
            if (sample.bowFacingEnabled && sample.bowAnchorWeight > 0f)
                sample.bowUpperChestReferenceError = Quaternion.Angle(
                    animator.GetBoneTransform(HumanBodyBones.UpperChest).rotation, _bowPoseAnchor.FacingReferenceRotation);
            AnimationDiagnosticSample.Invoke(sample);
        }
    }
}
#endif
