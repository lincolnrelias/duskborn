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
            Check(set.hold.name.EndsWith(" - Hold") && set.left.name.Contains("Strafe") && set.right.name.Contains("Strafe"),
                "Use vendor Hold and Strafe takes, not combined shot/directional runs.");
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
            Debug.Log("[AuthoredBowPlaybackTests] 960 load/hold/release poses, waist limits and lower-body invariance passed.");
        }
        private static void Compare(Rig baseline,Rig corrected)
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
            if (corrected.playback.Clock.Phase == AuthoredBowClock.Stage.Hold)
                Check(after < 5f, $"Held bow still turns away from the authored aim direction: {after:F2} degrees.");
        }
        private static void Check(bool value,string message) { if(!value) throw new InvalidOperationException(message); }
        private sealed class Rig:IDisposable
        {
            private GameObject root;
            private Animator animator;
            private PlayableGraph graph;
            public AuthoredBowPlayback playback;
            public BowPoseAnchor anchor;
            public Rig(BowAnimationSet set,AvatarMask mask,bool correct)
            {
                try
                {
                    root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(IronrootPlayerBuilder.ModelPath));
                    root.hideFlags=HideFlags.HideAndDontSave;
                    animator=root.GetComponent<Animator>(); animator.applyRootMotion=false;
                    animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                    graph=PlayableGraph.Create("AuthoredBowTest"); graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                    playback=new AuthoredBowPlayback(graph,set);
                    var layers=AnimationLayerMixerPlayable.Create(graph,2);
                    layers.ConnectInput(0,playback.Movement,0,1);
                    layers.SetLayerMaskFromAvatarMask(1,mask);
                    anchor=new BowPoseAnchor(graph,animator,layers);
                    layers.ConnectInput(1,anchor.ConnectClip(playback.Pose),0,1);
                    anchor.SetFacing(true); anchor.SetWeight(correct?1:0);
                    AnimationPlayableOutput.Create(graph,"Bow",animator).SetSourcePlayable(anchor.Output);
                    graph.Play();
                }
                catch { Dispose(); throw; }
            }
            public Transform Bone(HumanBodyBones bone)=>animator.GetBoneTransform(bone);
            public void Step(Vector2 input) { playback.Advance(1f/60,1,input); graph.Evaluate(1f/60); anchor.ApplyPose(); }
            public void Dispose()
            {
                if(graph.IsValid())graph.Destroy();
                playback?.Dispose();anchor?.Dispose();
                if(root!=null)UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
