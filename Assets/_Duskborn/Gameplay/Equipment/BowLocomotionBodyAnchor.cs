using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Duskborn.Gameplay.Equipment
{
    // A humanoid Body mask includes the centre-of-mass pose used to solve hips.
    // Preserve that pose and the solved lower-body transforms from locomotion.
    // Melee clips can carry hip muscle motion beyond the centre-of-mass channels.
    public sealed class BowLocomotionBodyAnchor : IDisposable
    {
        private struct BodyPose
        {
            public Vector3 position;
            public Quaternion rotation;
            public bool enabled;
        }
        private NativeArray<BodyPose> pose;
        private struct BonePose
        {
            public Vector3 position;
            public Quaternion rotation;
        }
        private NativeArray<TransformStreamHandle> bones;
        private NativeArray<BonePose> bonePoses;
        public Playable Locomotion { get; }
        public Playable Output { get; }
        public BowLocomotionBodyAnchor(PlayableGraph graph,Playable locomotion,Playable mixed, Animator animator = null)
        {
            pose=new NativeArray<BodyPose>(1,Allocator.Persistent);
            var lowerBody = new[] { HumanBodyBones.Hips, HumanBodyBones.LeftUpperLeg,
                HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes,
                HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot,
                HumanBodyBones.RightToes };
            bones = new NativeArray<TransformStreamHandle>(animator != null ? lowerBody.Length : 0, Allocator.Persistent);
            bonePoses = new NativeArray<BonePose>(bones.Length, Allocator.Persistent);
            for (int i = 0; i < bones.Length; i++)
            {
                Transform bone = animator.GetBoneTransform(lowerBody[i]);
                if (bone != null) bones[i] = animator.BindStreamTransform(bone);
            }
            var read=AnimationScriptPlayable.Create(graph,new ReadBody {pose=pose, bones=bones, bonePoses=bonePoses},1);
            read.ConnectInput(0,locomotion,0,1);
            Locomotion=read;
            var restore=AnimationScriptPlayable.Create(graph,new RestoreBody {pose=pose, bones=bones, bonePoses=bonePoses},1);
            restore.ConnectInput(0,mixed,0,1);
            Output=restore;
        }
        public void SetEnabled(bool enabled)
        {
            var value=pose[0]; value.enabled=enabled; pose[0]=value;
        }
        public void Dispose()
        {
            if(pose.IsCreated)pose.Dispose();
            if(bones.IsCreated)bones.Dispose();
            if(bonePoses.IsCreated)bonePoses.Dispose();
        }
        private struct ReadBody:IAnimationJob
        {
            public NativeArray<BodyPose> pose;
            [ReadOnly] public NativeArray<TransformStreamHandle> bones;
            public NativeArray<BonePose> bonePoses;
            public void ProcessRootMotion(AnimationStream stream) { }
            public void ProcessAnimation(AnimationStream stream)
            {
                if(!stream.isHumanStream)return;
                var human=stream.AsHuman(); var value=pose[0];
                value.position=human.bodyLocalPosition; value.rotation=human.bodyLocalRotation;
                pose[0]=value;
                for (int i = 0; i < bones.Length; i++)
                {
                    if (!bones[i].IsValid(stream)) continue;
                    bonePoses[i] = new BonePose { position = bones[i].GetLocalPosition(stream), rotation = bones[i].GetLocalRotation(stream) };
                }
            }
        }
        private struct RestoreBody:IAnimationJob
        {
            [ReadOnly] public NativeArray<BodyPose> pose;
            [ReadOnly] public NativeArray<TransformStreamHandle> bones;
            [ReadOnly] public NativeArray<BonePose> bonePoses;
            public void ProcessRootMotion(AnimationStream stream) { }
            public void ProcessAnimation(AnimationStream stream)
            {
                var value=pose[0];
                if(!value.enabled || !stream.isHumanStream)return;
                var human=stream.AsHuman();
                human.bodyLocalPosition=value.position; human.bodyLocalRotation=value.rotation;
                for (int i = 0; i < bones.Length; i++)
                {
                    if (!bones[i].IsValid(stream)) continue;
                    var bone = bones[i];
                    bone.SetLocalPosition(stream, bonePoses[i].position);
                    bone.SetLocalRotation(stream, bonePoses[i].rotation);
                }
            }
        }
    }
}
