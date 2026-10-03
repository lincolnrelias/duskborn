using System;
using Duskborn.Gameplay.Equipment;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Duskborn.Editor
{
    public static class AuthoredBowPlaybackTests
    {
        public static void RunAllTests()
        {
            var definition=Resources.Load<WeaponDefinition>("Weapons/weapon_wooden_bow");
            var set=definition.Actions[0].BowAnimations;
            Check(set != null && set.IsValid,"Authored bow set is incomplete.");
            Check(set.hold.name.EndsWith(" - Hold") && set.HasDiagonals,
                "Use vendor Hold and a complete eight-direction aim run set.");
            foreach(var clip in new[]{set.forward,set.backward,set.left,set.right,set.forwardLeft,set.forwardRight,set.backwardLeft,set.backwardRight})
                Check(clip.name.StartsWith("Archer@Run01"),"Aim movement must use the compatible Archer run family.");
            TestRedrawStride(set);
            TestDirectionWrap(set);
            TestContactAlignment(set);
            TestDirectionChanges(set,definition.ActionMask);
            TestBowDoesNotMoveLegs(set,definition.ActionMask);
            using(var baseline=new Rig(set,definition.ActionMask,false))
            using(var corrected=new Rig(set,definition.ActionMask,true))
            {
                // Warm up Load, then RMB-only Hold, with no release request.
                var directions = new[] { Vector2.left, Vector2.right, Vector2.up, Vector2.down,
                    new Vector2(-1,1).normalized, new Vector2(1,1).normalized,
                    new Vector2(-1,-1).normalized, new Vector2(1,-1).normalized, Vector2.zero };
                for(int i=0;i<900;i++)
                {
                    var input=directions[i/100];
                    baseline.Step(input); corrected.Step(input);
                    Check(corrected.playback.Clock.Phase != AuthoredBowClock.Stage.Release &&
                        corrected.playback.Clock.Phase != AuthoredBowClock.Stage.Complete,"RMB-only entered Release.");
                    Compare(baseline,corrected);
                }
                Check(corrected.playback.Clock.Phase==AuthoredBowClock.Stage.Hold,"Long RMB hold failed.");
                baseline.playback.Clock.RequestRelease(); corrected.playback.Clock.RequestRelease();
                for(int i=0;i<60;i++) { baseline.Step(new Vector2(-.7071f,.7071f)); corrected.Step(new Vector2(-.7071f,.7071f)); Compare(baseline,corrected); }
                Check(corrected.playback.Clock.Phase==AuthoredBowClock.Stage.Complete,"Requested release did not finish.");
            }
            using(var baseline=new Rig(set,definition.ActionMask,false,true))
            using(var corrected=new Rig(set,definition.ActionMask,true,true))
            {
                for(int i=0;i<180;i++)
                {
                    baseline.Step(Vector2.up); corrected.Step(Vector2.up);
                    Compare(baseline,corrected,false);
                }
            }
            Debug.Log("[AuthoredBowPlaybackTests] 960 load/hold/release poses, waist limits and lower-body invariance passed.");
        }
        private static void TestRedrawStride(BowAnimationSet set)
        {
            var graph=PlayableGraph.Create("BowRedrawStrideTest");
            AuthoredBowPlayback previous=null;
            try
            {
                previous=new AuthoredBowPlayback(graph,set);
                previous.Advance(set.load.length+.37f,1,Vector2.left);
                previous.Clock.RequestRelease();
                previous.Advance(set.release.length+.01f,1,Vector2.left);
                previous.Advance(.17f,1,Vector2.left); // cooldown movement
                int count=previous.MovementClipCount;
                var times=new double[count];
                var weights=new float[count];
                for(int i=0;i<count;i++) { times[i]=previous.Movement.GetInput(i).GetTime(); weights[i]=previous.Movement.GetInputWeight(i); }
                float phase=previous.MovementPhase;
                var movement=previous.Movement;
                previous.RestartDraw();
                previous.Advance(0,1,Vector2.left);
                Check(previous.Clock.Phase==AuthoredBowClock.Stage.Load && previous.Clock.Time==0,
                    "Redraw did not restart only the bow action clock.");
                Check(previous.Movement.Equals(movement),"Redraw replaced the locomotion playable.");
                for(int i=0;i<count;i++)
                {
                    Check(Math.Abs(times[i]-previous.Movement.GetInput(i).GetTime())<.00001,
                        "Redraw restarted a leg clip's stride time.");
                    Check(Math.Abs(weights[i]-previous.Movement.GetInputWeight(i))<.00001,
                        "Redraw changed strafe blend weights.");
                }
                previous.Advance(.1f,1,Vector2.zero);
                Check(Math.Abs(phase-previous.MovementPhase)<.00001,"Idle advanced the walking cycle.");
                previous.Advance(set.load.length,1,Vector2.left);
                Check(previous.Clock.Phase==AuthoredBowClock.Stage.Hold,"Redraw retained the prior shot request.");
                var inputs=new[]{Vector2.up,Vector2.down,Vector2.left,Vector2.right,
                    new Vector2(-1,1).normalized,new Vector2(1,1).normalized,
                    new Vector2(-1,-1).normalized,new Vector2(1,-1).normalized};
                for(int d=0;d<inputs.Length;d++)
                {
                    previous.Advance(0,1,inputs[d]);
                    for(int i=0;i<previous.MovementClipCount;i++)
                        Check(Mathf.Abs(previous.Movement.GetInputWeight(i)-(i==d+1?1:0))<.00001f,
                            "A cardinal/diagonal direction did not select its authored clip.");
                }
                phase=previous.MovementPhase;
                previous.Advance(.1f,1,Vector2.up,set.movementStrideLength*2f);
                Check(Mathf.Abs(previous.MovementPhase-Mathf.Repeat(phase+.2f,1))<.00001f,
                    "Gait cadence does not track covered distance.");
            }
            finally
            {
                if(graph.IsValid())graph.Destroy();
                previous?.Dispose();
            }
        }
        private static void TestDirectionWrap(BowAnimationSet set)
        {
            var graph=PlayableGraph.Create("BowDirectionWrapTest");
            AuthoredBowPlayback playback=null;
            try
            {
                playback=new AuthoredBowPlayback(graph,set);
                // Smoothed lateral input approaches zero from both sides while
                // moving forward. Tiny negative angles can round up to 360.
                foreach(float lateral in new[]{-1e-8f,-1e-7f,-1e-6f,0f,1e-8f,1e-7f,1e-6f})
                {
                    playback.Advance(0,1,new Vector2(lateral,1));
                    Check(playback.Movement.GetInputWeight(1)>.9999f,
                        "Forward wrap did not preserve the forward movement clip.");
                }
                foreach(float magnitude in new[]{.25f,1f})
                for(int degrees=0;degrees<360;degrees++)
                {
                    float angle=degrees*Mathf.Deg2Rad;
                    playback.Advance(0,1,new Vector2(Mathf.Sin(angle),Mathf.Cos(angle))*magnitude);
                    float total=0;
                    for(int i=0;i<playback.MovementClipCount;i++)
                    {
                        float weight=playback.Movement.GetInputWeight(i);
                        Check(!float.IsNaN(weight) && weight>=0 && weight<=1,
                            "Direction wrap produced an invalid movement weight.");
                        total+=weight;
                    }
                    Check(Mathf.Abs(total-1)<.00001f,"Direction blend weights do not sum to one.");
                    Check(Mathf.Abs(playback.Movement.GetInputWeight(0)-(1-magnitude))<.00001f,
                        "Direction wrap changed the idle blend weight.");
                }
            }
            finally
            {
                if(graph.IsValid())graph.Destroy();
                playback?.Dispose();
            }
        }
        private static void TestContactAlignment(BowAnimationSet set)
        {
            var clips=new[]{set.forward,set.backward,set.left,set.right,set.forwardLeft,set.forwardRight,set.backwardLeft,set.backwardRight};
            var heights=new Vector2[clips.Length][];
            var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(IronrootPlayerBuilder.ModelPath));
            root.hideFlags=HideFlags.HideAndDontSave;
            var animator=root.GetComponent<Animator>();
            animator.applyRootMotion=false; animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            try
            {
                for(int c=0;c<clips.Length;c++)
                {
                    var graph=PlayableGraph.Create("BowContactAlignment");
                    var clone=AuthoredBowPlayback.CloneClip(clips[c]);
                    try
                    {
                        var clip=AnimationClipPlayable.Create(graph,clone);
                        clip.SetSpeed(0); clip.SetApplyFootIK(false);
                        graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                        AnimationPlayableOutput.Create(graph,"Feet",animator).SetSourcePlayable(clip);
                        graph.Play(); heights[c]=new Vector2[128];
                        var min=new Vector2(float.PositiveInfinity,float.PositiveInfinity);
                        var max=new Vector2(float.NegativeInfinity,float.NegativeInfinity);
                        for(int i=0;i<128;i++)
                        {
                            clip.SetTime(i/128f*clone.length); graph.Evaluate(0);
                            var h=new Vector2(root.transform.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.LeftFoot).position).y,
                                root.transform.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.RightFoot).position).y);
                            heights[c][i]=h; min=Vector2.Min(min,h); max=Vector2.Max(max,h);
                        }
                        for(int i=0;i<128;i++) heights[c][i]=new Vector2(
                            (heights[c][i].x-min.x)/Mathf.Max(.001f,max.x-min.x),
                            (heights[c][i].y-min.y)/Mathf.Max(.001f,max.y-min.y));
                    }
                    finally { graph.Destroy(); UnityEngine.Object.DestroyImmediate(clone); }
                }
                for(int c=1;c<clips.Length;c++)
                {
                    float before=0,after=0;
                    float phaseOffset=c<4?set.movementPhaseOffsets[c]:set.diagonalPhaseOffsets[c-4];
                    int offset=Mathf.RoundToInt(phaseOffset*128);
                    for(int i=0;i<128;i++)
                    {
                        before+=(heights[0][i]-heights[c][i]).sqrMagnitude/128;
                        after+=(heights[0][i]-heights[c][(i+offset)%128]).sqrMagnitude/128;
                    }
                    Debug.Log($"[BowGaitContact] {clips[c].name}: contact error {before:F4} -> {after:F4}.");
                    if(c==1 || c>=6) Check(after<before*.8f,"Contact alignment failed on the retargeted rig: "+clips[c].name);
                    else Check(after<.04f,"Forward/side foot contacts are incompatible: "+clips[c].name);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        private static void TestDirectionChanges(BowAnimationSet set,AvatarMask mask)
        {
            var directions=new[]{Vector2.up,Vector2.down,Vector2.left,Vector2.right,
                new Vector2(-1,1).normalized,new Vector2(1,1).normalized,
                new Vector2(-1,-1).normalized,new Vector2(1,-1).normalized,Vector2.zero};
            float largestStep=0;
            using(var rig=new Rig(set,mask,true))
            {
                Vector2 input=Vector2.zero,velocity=Vector2.zero;
                foreach(var from in directions)
                foreach(var to in directions)
                {
                    foreach(var target in new[]{from,to})
                    for(int frame=0;frame<60;frame++)
                    {
                        var left=rig.Bone(HumanBodyBones.LeftFoot).rotation;
                        var right=rig.Bone(HumanBodyBones.RightFoot).rotation;
                        input=Vector2.SmoothDamp(input,target,ref velocity,.15f,float.PositiveInfinity,1f/60);
                        rig.Step(input);
                        if(frame>0) largestStep=Mathf.Max(largestStep,Quaternion.Angle(left,rig.Bone(HumanBodyBones.LeftFoot).rotation),
                            Quaternion.Angle(right,rig.Bone(HumanBodyBones.RightFoot).rotation));
                    }
                }
            }
            Debug.Log($"[BowGaitTransitions] All 81 direction/idle pairs: maximum foot rotation per 60 Hz frame {largestStep:F2} degrees.");
            Check(largestStep<35f,"Direction blends still produce a discontinuous foot rotation.");
        }
        private static void Compare(Rig baseline,Rig corrected,bool requireHeldConvergence=true)
        {
            foreach(var bone in new[]{HumanBodyBones.Spine,HumanBodyBones.Chest,HumanBodyBones.UpperChest})
                Check(Quaternion.Angle(baseline.Bone(bone).localRotation,corrected.Bone(bone).localRotation)<20.2f,
                    "Facing correction exceeded the per-joint budget: "+bone);
            foreach(var bone in new[]{HumanBodyBones.Hips,HumanBodyBones.LeftFoot,HumanBodyBones.RightFoot})
                Check(Vector3.Distance(baseline.Bone(bone).position,corrected.Bone(bone).position)<.0005f &&
                    Quaternion.Angle(baseline.Bone(bone).rotation,corrected.Bone(bone).rotation)<.2f,
                    "Facing correction changed aim-locomotion hips/feet: "+bone);
            float before=Quaternion.Angle(baseline.Bone(HumanBodyBones.UpperChest).rotation,corrected.anchor.FacingReferenceRotation);
            float after=Quaternion.Angle(corrected.Bone(HumanBodyBones.UpperChest).rotation,corrected.anchor.FacingReferenceRotation);
            Check(Mathf.Abs(after-Mathf.Max(0,before-60))<.2f,"Distributed facing did not reach the bounded reference.");
            // The correction deliberately leaves residual error beyond its 60-degree
            // budget rather than forcing a large twist through the waist. Require
            // near convergence only when the measured input pose is reachable.
            if (requireHeldConvergence && before<=65f && corrected.playback.Clock.Phase == AuthoredBowClock.Stage.Hold)
                Check(after < 5f, $"Held bow still turns away from the authored aim direction: {after:F2} degrees.");
        }
        private static void TestBowDoesNotMoveLegs(BowAnimationSet set,AvatarMask mask)
        {
            var directions=new[]{Vector2.zero,Vector2.up,Vector2.down,Vector2.left,Vector2.right,
                new Vector2(-1,1).normalized,new Vector2(1,1).normalized,
                new Vector2(-1,-1).normalized,new Vector2(1,-1).normalized};
            float maxAngle=0,maxDistance=0;
            using(var movementOnly=new Rig(set,mask,false))
            using(var bow=new Rig(set,mask,true))
            {
                movementOnly.SetActionWeight(0);
                foreach(var direction in directions)
                for(int frame=0;frame<180;frame++)
                {
                    // Include partial blends and abrupt rejected draw cancellation.
                    bow.SetActionWeight(frame<60?1:frame<90?.35f:frame<110?0:1);
                    if(frame==110) { movementOnly.playback.RestartDraw(); bow.playback.RestartDraw(); }
                    movementOnly.Step(direction); bow.Step(direction);
                    foreach(var bone in new[]{HumanBodyBones.Hips,HumanBodyBones.LeftUpperLeg,HumanBodyBones.RightUpperLeg,
                        HumanBodyBones.LeftFoot,HumanBodyBones.RightFoot})
                    {
                        float angle=Quaternion.Angle(movementOnly.Bone(bone).rotation,bow.Bone(bone).rotation);
                        float distance=Vector3.Distance(movementOnly.Bone(bone).position,bow.Bone(bone).position);
                        maxAngle=Mathf.Max(maxAngle,angle); maxDistance=Mathf.Max(maxDistance,distance);
                        Check(angle<.25f && distance<.001f,$"Bow blend moved {bone}: direction={direction}, frame={frame}, angle={angle}, distance={distance}.");
                    }
                }
            }
            Debug.Log($"[BowLowerBodyInvariance] 1620 poses: maximum difference {maxAngle:F3} degrees / {maxDistance:F6} metres.");
        }
        private static void Check(bool value,string message) { if(!value) throw new InvalidOperationException(message); }
        private sealed class Rig:IDisposable
        {
            private GameObject root;
            private Animator animator;
            private PlayableGraph graph;
            private AnimationLayerMixerPlayable layers;
            private BowLocomotionBodyAnchor bodyAnchor;
            private bool correct;
            private AnimationClip jumpClip;
            private AnimationClipPlayable jump;
            private float jumpTime;
            public AuthoredBowPlayback playback;
            public BowPoseAnchor anchor;
            public Rig(BowAnimationSet set,AvatarMask mask,bool correct,bool airborne=false)
            {
                try
                {
                    root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(IronrootPlayerBuilder.ModelPath));
                    root.hideFlags=HideFlags.HideAndDontSave;
                    animator=root.GetComponent<Animator>(); animator.applyRootMotion=false;
                    animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                    graph=PlayableGraph.Create("AuthoredBowTest"); graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                    this.correct=correct;
                    playback=new AuthoredBowPlayback(graph,set);
                    layers=AnimationLayerMixerPlayable.Create(graph,2);
                    Playable movement=playback.Movement;
                    if(airborne)
                    {
                        var source=AssetDatabase.LoadAssetAtPath<AnimationClip>(
                            "Assets/ThirdPartyAssets/Kevin Iglesias/Basic Motions/Animations/Movement/BasicMotions@Jump01.fbx");
                        Check(source != null,"Jump clip is missing.");
                        jumpClip=AuthoredBowPlayback.CloneClip(source);
                        jump=AnimationClipPlayable.Create(graph,jumpClip);
                        jump.SetSpeed(0); jump.SetApplyFootIK(false);
                        movement=jump;
                    }
                    bodyAnchor=new BowLocomotionBodyAnchor(graph,movement,layers,animator);
                    layers.ConnectInput(0,bodyAnchor.Locomotion,0,1);
                    bodyAnchor.SetEnabled(true);
                    layers.SetLayerMaskFromAvatarMask(1,mask);
                    anchor=new BowPoseAnchor(graph,animator,bodyAnchor.Output);
                    layers.ConnectInput(1,anchor.ConnectClip(playback.Pose),0,1);
                    anchor.SetFacing(true); anchor.SetWeight(correct?1:0);
                    AnimationPlayableOutput.Create(graph,"Bow",animator).SetSourcePlayable(anchor.Output);
                    graph.Play();
                }
                catch { Dispose(); throw; }
            }
            public Transform Bone(HumanBodyBones bone)=>animator.GetBoneTransform(bone);
            public void SetActionWeight(float weight) { layers.SetInputWeight(1,weight); anchor.SetWeight(correct?weight:0); }
            public void Step(Vector2 input)
            {
                playback.Advance(1f/60,1,input);
                if(jump.IsValid()) { jumpTime+=1f/60; jump.SetTime(jumpTime % jumpClip.length); }
                graph.Evaluate(1f/60); anchor.ApplyPose();
            }
            public void Dispose()
            {
                if(graph.IsValid())graph.Destroy();
                playback?.Dispose();anchor?.Dispose();
                bodyAnchor?.Dispose();
                if(jumpClip!=null)UnityEngine.Object.DestroyImmediate(jumpClip);
                if(root!=null)UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
