using System;
using Duskborn.Gameplay.Equipment;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Duskborn.Editor
{
    public static class BowPoseAnchorTests
    {
        [MenuItem("Duskborn/Tests/Run Bow Pose Anchor Tests")]
        public static void RunAllTests()
        {
            TestTiltLimits();
            var definition = Resources.Load<WeaponDefinition>("Weapons/weapon_wooden_bow");
            var clip = AuthoredBowPlayback.CloneClip(definition.Actions[0].Entries[0].Clip);

            try
            {
                using (var baseline = new Rig(clip, definition.ActionMask, false, false))
                using (var corrected = new Rig(clip, definition.ActionMask, true, false))
                using (var reference = new Rig(clip, definition.ActionMask, false, true))
                {
                    var directions = new[] { Vector2.zero, Vector2.up, Vector2.down, Vector2.left, Vector2.right,
                        new Vector2(-1, 1).normalized, new Vector2(1, 1).normalized,
                        new Vector2(-1, -1).normalized, new Vector2(1, -1).normalized };
                    int checks = 0;
                    foreach (float yaw in new[] { 0f, 137f })
                    foreach (float clipTime in new[] { .2f, 1.3807916f, 1.55f, 1.95f })
                    foreach (float weight in new[] { 0f, .35f, 1f })
                    foreach (var direction in directions)
                    {
                        baseline.Configure(yaw, clipTime, weight, direction);
                        corrected.Configure(yaw, clipTime, weight, direction);
                        reference.Configure(yaw, clipTime, weight, direction);
                        for (int i = 0; i < 5; i++)
                        {
                            baseline.Evaluate();
                            corrected.Evaluate();
                            reference.Evaluate();
                            var before = baseline.Bone(HumanBodyBones.Spine);
                            var after = corrected.Bone(HumanBodyBones.Spine);
                            var source = reference.Bone(HumanBodyBones.Spine);
                            float beforeTilt = Vector3.Angle(weight > 0f ? corrected.InputSpine * Vector3.up : before.up, source.up);
                            float afterTilt = Vector3.Angle(after.up, source.up);
                            float expectedTilt = beforeTilt - Mathf.Min(beforeTilt, BowPoseAnchor.MaxTiltCorrection) * weight;
                            Check(Mathf.Abs(afterTilt - expectedTilt) < .15f,
                                $"Spine tilt reduction failed: yaw={yaw}, time={clipTime}, weight={weight}, direction={direction}, before={beforeTilt}, after={afterTilt}, expected={expectedTilt}, tapError={Quaternion.Angle(corrected.Reference, source.rotation)}, inputDifference={Quaternion.Angle(corrected.InputSpine,before.rotation)}, outputDifference={Quaternion.Angle(corrected.OutputSpine,after.rotation)}, inputTilt={Vector3.Angle(corrected.InputSpine*Vector3.up,source.up)}.");
                            Check(Quaternion.Angle(weight > 0f ? corrected.InputSpine : before.rotation, after.rotation) <= BowPoseAnchor.MaxTiltCorrection * weight + .15f,
                                "Correction overtwisted the spine relative to its parent.");
                            foreach (var bone in new[] { HumanBodyBones.Hips, HumanBodyBones.LeftUpperLeg,
                                HumanBodyBones.RightUpperLeg, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot })
                            {
                                Check(Vector3.Distance(baseline.Bone(bone).position, corrected.Bone(bone).position) < .0005f &&
                                    Quaternion.Angle(baseline.Bone(bone).rotation, corrected.Bone(bone).rotation) < .15f,
                                    $"Anchor changed locomotion bone {bone}.");
                            }
                            checks++;
                        }
                    }
                    baseline.DisconnectAction();
                    corrected.DisconnectAction();
                    baseline.Evaluate();
                    corrected.Evaluate();
                    Check(Quaternion.Angle(baseline.Bone(HumanBodyBones.Spine).rotation,
                        corrected.Bone(HumanBodyBones.Spine).rotation) < .15f, "Cancelled bow left an anchored pose.");
                    Debug.Log($"[BowPoseAnchorTests] {checks} pose comparisons and cancellation passed.");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(clip); }
        }

        private static void TestTiltLimits()
        {
            // The reported collapse occurs when source and locomotion headings
            // oppose each other. Heading alone must produce NO correction.
            foreach (float yaw in new[] { -179f, -110f, -90f, 0f, 90f, 179f })
            {
                var current = Quaternion.Euler(0, yaw, 0);
                var source = Quaternion.Euler(0, 65, 0);
                Check(Quaternion.Angle(current, BowPoseAnchor.CorrectTilt(current, source, 1)) < .05f,
                    "Yaw-only disagreement must not twist the waist.");
                foreach (float lean in new[] { -60f, -8f, 8f, 60f })
                foreach (float weight in new[] { 0f, .35f, 1f })
                {
                    current = Quaternion.Euler(0, yaw, 0) * Quaternion.Euler(0, 0, lean);
                    var result = BowPoseAnchor.CorrectTilt(current, source, weight);
                    Check(Quaternion.Angle(current, result) <= BowPoseAnchor.MaxTiltCorrection * weight + .05f,
                        "Tilt correction exceeded its angular budget.");
                    float expected = Mathf.Abs(lean) - Mathf.Min(Mathf.Abs(lean), BowPoseAnchor.MaxTiltCorrection) * weight;
                    Check(Mathf.Abs(Vector3.Angle(result * Vector3.up, Vector3.up) - expected) < .05f,
                        "Tilt correction failed to remove the expected lean.");
                }
            }
        }

        private static void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }

        // Only the imported visual rig is cloned, never a network/gameplay prefab.
        private sealed class Rig : IDisposable
        {
            private GameObject root;
            private Animator animator;
            private AnimationLayerMixerPlayable mixer;
            private AnimatorControllerPlayable controller;
            private AnimationClipPlayable action;
            private BowPoseAnchor anchor;
            private bool referenceOnly;
            private bool stabilize;
            public PlayableGraph graph;

            public Rig(AnimationClip clip, AvatarMask mask, bool stabilize, bool referenceOnly)
            {
                try
                {
                    this.referenceOnly = referenceOnly;
                    this.stabilize = stabilize;
                    root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(IronrootPlayerBuilder.ModelPath));
                    root.hideFlags = HideFlags.HideAndDontSave;
                    animator = root.GetComponent<Animator>();
                    Check(BowPoseAnchor.Supports(animator), "Ironroot must provide a valid humanoid spine.");
                    animator.applyRootMotion = false;
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    graph = PlayableGraph.Create("BowPoseAnchorTest");
                    graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                    action = AnimationClipPlayable.Create(graph, clip);
                    action.SetApplyFootIK(false);
                    action.SetSpeed(0);
                    Playable result = action;
                    if (!referenceOnly)
                    {
                        var asset = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                            "Assets/_Duskborn/Art/Models/char_animator_controller.controller");
                        controller = AnimatorControllerPlayable.Create(graph, asset);
                        controller.SetBool("IsGrounded", true);
                        mixer = AnimationLayerMixerPlayable.Create(graph, 2);
                        mixer.ConnectInput(0, controller, 0, 1f);
                        mixer.SetLayerAdditive(1, false);
                        mixer.SetLayerMaskFromAvatarMask(1, mask);
                        anchor = new BowPoseAnchor(graph, animator, mixer);
                        mixer.ConnectInput(1, anchor != null ? anchor.ConnectClip(action) : (Playable)action, 0, 1f);
                        result = anchor != null ? anchor.Output : (Playable)mixer;
                    }
                    AnimationPlayableOutput.Create(graph, "Pose", animator).SetSourcePlayable(result);
                    graph.Play();
                }
                catch { Dispose(); throw; }
            }

            public Quaternion InputSpine => anchor.InputSpineRotation;
            public Quaternion OutputSpine => anchor.OutputSpineRotation;
            public Quaternion Reference => anchor.ReferenceRotation;
            public void Evaluate() { graph.Evaluate(1f / 60f); anchor?.ApplyPose(); }
            public Transform Bone(HumanBodyBones bone) => animator.GetBoneTransform(bone);
            public void Configure(float yaw, float time, float weight, Vector2 direction)
            {
                root.transform.rotation = Quaternion.Euler(0, yaw, 0);
                action.SetTime(time);
                if (referenceOnly) return;
                controller.SetFloat("VelocityX", direction.x);
                controller.SetFloat("VelocityY", direction.y);
                mixer.SetInputWeight(1, weight);
                anchor?.SetWeight(stabilize ? weight : 0f);
            }
            public void DisconnectAction()
            {
                mixer.SetInputWeight(1, 0);
                mixer.DisconnectInput(1);
                anchor?.DisconnectClip();
            }
            public void Dispose()
            {
                if (graph.IsValid()) graph.Destroy();
                anchor?.Dispose();
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
