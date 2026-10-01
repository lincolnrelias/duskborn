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
        private readonly AnimationClipPlayable[] moves;
        private readonly AnimationClip[] clones;
        private static readonly int[] OctantInputs = {1,6,4,8,2,7,3,5};
        private float phase;
        private double idleTime;
        public BowAnimationSet Set { get; }
        public float MovementPhase => phase;
        public int MovementClipCount => moves.Length;
        public string MovementClipName(int index) => clones[index + 3].name;
        public string ClipName => clones[Math.Min((int)Clock.Phase, 2)].name;
        public void RestartDraw()
        {
            Clock.RestartDraw();
            for(int i=0;i<3;i++) Pose.SetInputWeight(i,i==0?1:0);
            poses[0].SetTime(0);
        }
        public AuthoredBowPlayback(PlayableGraph graph, BowAnimationSet set, float movementPhase = 0f)
        {
            Set=set;
            phase=Mathf.Repeat(movementPhase,1f);
            Clock=new AuthoredBowClock(set.load.length, set.release.length);
            Pose=AnimationMixerPlayable.Create(graph,3);
            moves=new AnimationClipPlayable[set.HasDiagonals?9:5];
            clones=new AnimationClip[moves.Length+3];
            Movement=AnimationMixerPlayable.Create(graph,moves.Length);
            var assets=set.HasDiagonals
                ? new[] {set.load,set.hold,set.release,set.idle,set.forward,set.backward,set.left,set.right,set.forwardLeft,set.forwardRight,set.backwardLeft,set.backwardRight}
                : new[] {set.load,set.hold,set.release,set.idle,set.forward,set.backward,set.left,set.right};
            for(int i=0;i<assets.Length;i++)
            {
                clones[i]=CloneClip(assets[i]);
                var clip=AnimationClipPlayable.Create(graph,clones[i]);
                clip.SetApplyFootIK(false); clip.SetSpeed(0);
                if(i<3) { poses[i]=clip; Pose.ConnectInput(i,clip,0,i==0?1:0); }
                else { moves[i-3]=clip; Movement.ConnectInput(i-3,clip,0,i==3?1:0); }
            }
        }
        public void Advance(float dt, float speed, Vector2 input, float movementMetersPerSecond = -1f)
        {
            Clock.Advance(dt*Mathf.Max(0,speed));
            int stage=Math.Min((int)Clock.Phase,2);
            for(int i=0;i<3;i++) Pose.SetInputWeight(i,i==stage?1:0);
            double time=Clock.Time;
            if(stage==1) time%=clones[1].length;
            poses[stage].SetTime(time);
            // A shared cycle plus authored contact offsets keeps the same foot
            // planted across direction blends. Equal clip time alone does not.
            float magnitude=Mathf.Clamp01(input.magnitude);
            float sum=Mathf.Abs(input.x)+Mathf.Abs(input.y);
            Movement.SetInputWeight(0,1-magnitude);
            for(int i=1;i<moves.Length;i++) Movement.SetInputWeight(i,0);
            if(Set.HasDiagonals && magnitude>0)
            {
                float octant=Mathf.Repeat(Mathf.Atan2(input.x,input.y)*Mathf.Rad2Deg,360f)/45f;
                int first=Mathf.FloorToInt(octant);
                float blend=octant-first;
                // Repeat can round a tiny negative angle up to 360 degrees.
                // Keep its blend fraction, but wrap the endpoint back to forward.
                first%=OctantInputs.Length;
                Movement.SetInputWeight(OctantInputs[first],magnitude*(1-blend));
                Movement.SetInputWeight(OctantInputs[(first+1)%OctantInputs.Length],magnitude*blend);
            }
            else if(sum>0)
            {
                Movement.SetInputWeight(1,Mathf.Max(0,input.y)/sum*magnitude);
                Movement.SetInputWeight(2,Mathf.Max(0,-input.y)/sum*magnitude);
                Movement.SetInputWeight(3,Mathf.Max(0,-input.x)/sum*magnitude);
                Movement.SetInputWeight(4,Mathf.Max(0,input.x)/sum*magnitude);
            }
            float cycleRate=movementMetersPerSecond>=0 && Set.movementStrideLength>0
                ? movementMetersPerSecond/Set.movementStrideLength : magnitude*1.5f;
            phase=Mathf.Repeat(phase+dt*cycleRate,1);
            idleTime+=dt;
            moves[0].SetTime(idleTime % clones[3].length);
            for(int i=1;i<moves.Length;i++)
            {
                float offset=i<5?Set.movementPhaseOffsets[i-1]:Set.diagonalPhaseOffsets[i-5];
                moves[i].SetTime(Mathf.Repeat(phase+offset,1)*clones[i+3].length);
            }
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
