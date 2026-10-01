using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Duskborn.Gameplay.Equipment
{
    // A humanoid Body mask includes the centre-of-mass pose used to solve hips.
    // Preserve that pose from locomotion so upper-body clips cannot rotate legs.
    public sealed class BowLocomotionBodyAnchor : IDisposable
    {
        private struct BodyPose
        {
            public Vector3 position;
            public Quaternion rotation;
            public bool enabled;
        }
        private NativeArray<BodyPose> pose;
        public Playable Locomotion { get; }
        public Playable Output { get; }
        public BowLocomotionBodyAnchor(PlayableGraph graph,Playable locomotion,Playable mixed)
        {
            pose=new NativeArray<BodyPose>(1,Allocator.Persistent);
            var read=AnimationScriptPlayable.Create(graph,new ReadBody {pose=pose},1);
            read.ConnectInput(0,locomotion,0,1);
            Locomotion=read;
            var restore=AnimationScriptPlayable.Create(graph,new RestoreBody {pose=pose},1);
            restore.ConnectInput(0,mixed,0,1);
            Output=restore;
        }
        public void SetEnabled(bool enabled)
        {
            var value=pose[0]; value.enabled=enabled; pose[0]=value;
        }
        public void Dispose() { if(pose.IsCreated)pose.Dispose(); }
        private struct ReadBody:IAnimationJob
        {
            public NativeArray<BodyPose> pose;
            public void ProcessRootMotion(AnimationStream stream) { }
            public void ProcessAnimation(AnimationStream stream)
            {
                if(!stream.isHumanStream)return;
                var human=stream.AsHuman(); var value=pose[0];
                value.position=human.bodyLocalPosition; value.rotation=human.bodyLocalRotation;
                pose[0]=value;
            }
        }
        private struct RestoreBody:IAnimationJob
        {
            [ReadOnly] public NativeArray<BodyPose> pose;
            public void ProcessRootMotion(AnimationStream stream) { }
            public void ProcessAnimation(AnimationStream stream)
            {
                var value=pose[0];
                if(!value.enabled || !stream.isHumanStream)return;
                var human=stream.AsHuman();
                human.bodyLocalPosition=value.position; human.bodyLocalRotation=value.rotation;
            }
        }
    }
}
