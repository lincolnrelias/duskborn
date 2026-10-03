using System;
using System.Reflection;
using Duskborn.Gameplay.Equipment;
using Duskborn.Gameplay.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Duskborn.Editor
{
    public static class FreeMovingCombatTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        [MenuItem("Duskborn/Tests/Run Free Moving Combat Tests")]
        public static void RunAllTests()
        {
            TestMovementSpace();
            var definition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(
                "Assets/_Duskborn/ScriptableObjects/Weapons/weapon_blood_blade.asset");
            Check(definition != null && definition.Actions[0].Entries.Length > 0, "Melee test clips missing.");
            var clip = AuthoredBowPlayback.CloneClip(definition.Actions[0].Entries[0].Clip);
            var skill = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<WeaponSkill>(
                "Assets/_Duskborn/ScriptableObjects/Weapons/Stone Axe/cleave_skill.asset"));
            try
            {
                using (var baseline = new Rig())
                using (var combat = new Rig())
                {
                    var data = new WeaponActionData
                    {
                        PreserveLocomotion = true,
                        Entries = new[] { new WeaponActionClip { Clip = clip } }
                    };
                    // No configured mask: the runtime fallback must also preserve legs.
                    var weapon = new WeaponItem("test", "Test", "", "", null, null, null, null, new[] { data });
                    combat.Player.PlayAction(0, weapon, null);
                    Check(!combat.Animator.applyRootMotion, "Moving attack enabled root motion.");
                    var mask = combat.Player.DiagnosticMask;
                    foreach (var part in new[] { AvatarMaskBodyPart.Root, AvatarMaskBodyPart.LeftLeg,
                        AvatarMaskBodyPart.RightLeg, AvatarMaskBodyPart.LeftFootIK, AvatarMaskBodyPart.RightFootIK })
                        Check(!mask.GetHumanoidBodyPartActive(part), $"Fallback combat mask includes {part}.");
                    Check(mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.Body), "Fallback lost torso animation.");

                    bool upperBodyMoved = false;
                    int comparisons = 0;
                    for (int mode = 0; mode < 3; mode++)
                    {
                        if (mode > 0)
                        {
                            combat.Player.CancelAction();
                            data.MaskOverride = definition.ActionMask;
                            if (mode == 1) combat.Player.PlayAction(0, weapon, null);
                            else
                            {
                                skill.animation = data;
                                combat.Player.PlaySkillAction(skill, null);
                            }
                        }
                        foreach (float yaw in new[] { 0f, 90f, 179f })
                        foreach (var direction in new[] { Vector2.zero, Vector2.up, Vector2.down, Vector2.left,
                            Vector2.right, new Vector2(1, -1).normalized })
                        foreach (float weight in new[] { 0f, .35f, 1f })
                        {
                            for (int frame = 0; frame < 5; frame++)
                            {
                                baseline.Step(yaw, direction, 0f, clip.length * .4f);
                                combat.Step(yaw, direction, weight, clip.length * .4f);
                                CompareLegs(baseline, combat);
                                upperBodyMoved |= Vector3.Distance(baseline.Bone(HumanBodyBones.RightHand).position,
                                    combat.Bone(HumanBodyBones.RightHand).position) > .01f;
                                comparisons++;
                            }
                        }
                    }
                    Check(upperBodyMoved, "Moving combat protection suppressed the weapon pose.");
                    combat.Player.CancelAction();
                    baseline.Step(0, Vector2.up, 0, 0);
                    combat.Step(0, Vector2.up, 0, 0);
                    CompareLegs(baseline, combat);
                    Check(!combat.Player.IsPlaying && !combat.Animator.applyRootMotion, "Cancel failed to restore locomotion.");

                    // Full-body actions must remain free to drive the complete pose.
                    data.PreserveLocomotion = false;
                    combat.Player.PlayAction(0, weapon, null);
                    Check(combat.Animator.applyRootMotion &&
                        combat.Player.DiagnosticMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.Root),
                        "Full-body action was incorrectly restricted.");
                    combat.Player.CancelAction();
                    Check(!combat.Animator.applyRootMotion, "Full-body cancellation left root motion enabled.");
                    Debug.Log($"[FreeMovingCombatTests] Movement-space checks, {comparisons} melee/skill lower-body comparisons, fallback mask and cancellation passed.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(clip);
                UnityEngine.Object.DestroyImmediate(skill);
            }
        }

        private static void TestMovementSpace()
        {
            foreach (float yaw in new[] { -179f, -90f, 0f, 45f, 179f })
            foreach (var input in new[] { Vector2.up, Vector2.down, Vector2.left, Vector2.right,
                new Vector2(1, -1).normalized * 1.5f })
            {
                Quaternion facing = Quaternion.Euler(0, yaw, 0);
                Vector3 world = facing * new Vector3(input.x * 4f, 0, input.y * 4f);
                world.y = -20f;
                Check(Vector2.Distance(PlayerController.GetLocomotionVelocity(world, facing, 4f), input) < .0001f,
                    "Locomotion direction changed with world heading or gravity.");
            }
            Check(Vector2.Distance(PlayerController.GetLocomotionVelocity(Vector3.forward * 4,
                Quaternion.Euler(0, 90, 0), 4), Vector2.left) < .0001f,
                "Camera-forward travel must become strafe while the body has a different heading.");
            Check(PlayerController.GetLocomotionVelocity(Vector3.up * 10, Quaternion.identity, 4) == Vector2.zero,
                "Stationary/blocked movement must not animate walking.");
            Check(PlayerController.GetLocomotionVelocity(Vector3.forward, Quaternion.identity, 0) == Vector2.zero,
                "Zero movement speed must not produce invalid animation parameters.");
        }

        private static void CompareLegs(Rig baseline, Rig combat)
        {
            foreach (var bone in new[] { HumanBodyBones.Hips, HumanBodyBones.LeftUpperLeg,
                HumanBodyBones.RightUpperLeg, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot })
            {
                Check(Vector3.Distance(baseline.Bone(bone).position, combat.Bone(bone).position) < .0005f &&
                    Quaternion.Angle(baseline.Bone(bone).rotation, combat.Bone(bone).rotation) < .15f,
                    $"Moving melee changed locomotion bone {bone}: position error={Vector3.Distance(baseline.Bone(bone).position, combat.Bone(bone).position)}, rotation error={Quaternion.Angle(baseline.Bone(bone).rotation, combat.Bone(bone).rotation)}, case={combat.Case}.");
            }
        }

        private sealed class Rig : IDisposable
        {
            private GameObject root;
            private PlayableGraph graph;
            private AnimatorControllerPlayable controller;
            private AnimationLayerMixerPlayable layers;
            public WeaponActionPlayer Player { get; private set; }
            public Animator Animator { get; private set; }
            public string Case { get; private set; }

            public Rig()
            {
                try
                {
                    root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(IronrootPlayerBuilder.ModelPath));
                    root.hideFlags = HideFlags.HideAndDontSave;
                    Animator = root.GetComponent<Animator>();
                    Animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                        "Assets/_Duskborn/Art/Models/char_animator_controller.controller");
                    Animator.applyRootMotion = false;
                    Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    Player = root.AddComponent<WeaponActionPlayer>();
                    typeof(WeaponActionPlayer).GetField("animator", PrivateInstance).SetValue(Player, Animator);
                    typeof(WeaponActionPlayer).GetMethod("BuildGraph", PrivateInstance).Invoke(Player, null);
                    graph = (PlayableGraph)typeof(WeaponActionPlayer).GetField("_graph", PrivateInstance).GetValue(Player);
                    controller = (AnimatorControllerPlayable)typeof(WeaponActionPlayer).GetField("_controllerPlayable", PrivateInstance).GetValue(Player);
                    layers = (AnimationLayerMixerPlayable)typeof(WeaponActionPlayer).GetField("_layerMixer", PrivateInstance).GetValue(Player);
                    graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                    controller.SetBool("IsGrounded", true);
                }
                catch { Dispose(); throw; }
            }

            public Transform Bone(HumanBodyBones bone) => Animator.GetBoneTransform(bone);
            public void Step(float yaw, Vector2 direction, float weight, float time)
            {
                Case = $"yaw={yaw}, direction={direction}, weight={weight}, time={time}";
                root.transform.rotation = Quaternion.Euler(0, yaw, 0);
                controller.SetFloat("VelocityX", direction.x);
                controller.SetFloat("VelocityY", direction.y);
                layers.SetInputWeight(1, weight);
                var action = (AnimationClipPlayable)typeof(WeaponActionPlayer).GetField("_clipPlayable", PrivateInstance).GetValue(Player);
                if (action.IsValid()) { action.SetSpeed(0); action.SetTime(time); }
                graph.Evaluate(1f / 60f);
            }

            public void Dispose()
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
