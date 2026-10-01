using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
namespace Duskborn.Gameplay.Equipment
{
    public sealed class AuthoredBowPlayback : IDisposable
    {
        public static AnimationClip CloneClip(AnimationClip source)
        {
            var clone = UnityEngine.Object.Instantiate(source);
#if UNITY_EDITOR
            if (!Application.isPlaying) UnityEditor.AnimationUtility.SetAnimationEvents(clone, Array.Empty<AnimationEvent>());
            else
#endif
                clone.events = Array.Empty<AnimationEvent>();
            return clone;
        }
        public readonly AuthoredBowClock Clock;
        public AnimationMixerPlayable Pose { get; }
        public AnimationMixerPlayable Movement { get; }
        private readonly AnimationClipPlayable[] poses = new AnimationClipPlayable[3];
        private readonly AnimationClipPlayable[] moves = new AnimationClipPlayable[5];
        private readonly AnimationClip[] clones = new AnimationClip[8];
        private float phase;
        public string MovementClipName(int index) => clones[index + 3].name;
        public string ClipName => clones[Math.Min((int)Clock.Phase, 2)].name;
        public AuthoredBowPlayback(PlayableGraph graph, BowAnimationSet set)
        {
            Clock=new AuthoredBowClock(set.load.length, set.release.length);
            Pose=AnimationMixerPlayable.Create(graph,3);
            Movement=AnimationMixerPlayable.Create(graph,5);
            var assets=new[] {set.load,set.hold,set.release,set.idle,set.forward,set.backward,set.left,set.right};
            for(int i=0;i<8;i++)
            {
                clones[i]=CloneClip(assets[i]);
                var clip=AnimationClipPlayable.Create(graph,clones[i]);
                clip.SetApplyFootIK(false); clip.SetSpeed(0);
                if(i<3) { poses[i]=clip; Pose.ConnectInput(i,clip,0,i==0?1:0); }
                else { moves[i-3]=clip; Movement.ConnectInput(i-3,clip,0,i==3?1:0); }
            }
        }
        public void Advance(float dt, float speed, Vector2 input)
        {
            Clock.Advance(dt*Mathf.Max(0,speed));
            int stage=Math.Min((int)Clock.Phase,2);
            for(int i=0;i<3;i++) Pose.SetInputWeight(i,i==stage?1:0);
            double time=Clock.Time;
            if(stage==1) time%=clones[1].length;
            poses[stage].SetTime(time);
            // A shared cycle keeps blended cardinal steps in phase.
            float magnitude=Mathf.Clamp01(input.magnitude);
            float sum=Mathf.Abs(input.x)+Mathf.Abs(input.y);
            Movement.SetInputWeight(0,1-magnitude);
            Movement.SetInputWeight(1,sum>0?Mathf.Max(0,input.y)/sum*magnitude:0);
            Movement.SetInputWeight(2,sum>0?Mathf.Max(0,-input.y)/sum*magnitude:0);
            Movement.SetInputWeight(3,sum>0?Mathf.Max(0,-input.x)/sum*magnitude:0);
            Movement.SetInputWeight(4,sum>0?Mathf.Max(0,input.x)/sum*magnitude:0);
            phase=Mathf.Repeat(phase+dt*Mathf.Max(.5f,input.magnitude)*1.5f,1);
            for(int i=0;i<5;i++) moves[i].SetTime(phase*clones[i+3].length);
        }
        public void Dispose()
        {
            // Call only after disconnecting both graph branches.
            foreach(var p in poses) if(p.IsValid()) p.Destroy();
            foreach(var p in moves) if(p.IsValid()) p.Destroy();
            if(Pose.IsValid()) Pose.Destroy();
            if(Movement.IsValid()) Movement.Destroy();
            foreach(var clip in clones) if(clip!=null)
            { if(Application.isPlaying) UnityEngine.Object.Destroy(clip); else UnityEngine.Object.DestroyImmediate(clip); }
        }
    }
}
