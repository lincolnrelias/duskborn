using System;
using System.Reflection;
using Duskborn.Gameplay.Equipment;
using Duskborn.Gameplay.Projectiles;
using UnityEditor;
using UnityEngine;

namespace Duskborn.Editor
{
    public static class RangedCombatTests
    {
        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }

        [MenuItem("Duskborn/Tests/Run Ranged Combat Tests")]
        public static void RunAllTests()
        {
            var bow = Resources.Load<WeaponDefinition>("Weapons/weapon_wooden_bow");
            Check(bow != null && bow.Prefab != null, "Wooden bow missing.");
            Check(bow.AttachmentProfile != null && bow.AttachmentProfile.Bone == HumanBodyBones.LeftHand, "Bow must use left hand.");
            Check(bow.Behaviour is RangedWeaponBehaviour, "Bow is not ranged.");
            Check(bow.ActionMask != null, "Bow needs its two-arm action mask.");
            foreach (var part in new[] { AvatarMaskBodyPart.Body, AvatarMaskBodyPart.LeftArm,
                         AvatarMaskBodyPart.RightArm, AvatarMaskBodyPart.LeftFingers, AvatarMaskBodyPart.RightFingers })
                Check(bow.ActionMask.GetHumanoidBodyPartActive(part), $"Bow mask excludes {part}.");
            foreach (var part in new[] { AvatarMaskBodyPart.Root, AvatarMaskBodyPart.LeftLeg, AvatarMaskBodyPart.RightLeg })
                Check(!bow.ActionMask.GetHumanoidBodyPartActive(part), $"Bow mask overrides locomotion: {part}.");
            Check(((WeaponItem)bow.CreateRuntimeItem()).ActionMask == bow.ActionMask, "Runtime bow lost its action mask.");
            Check(RangedWeaponBehaviour.TryGetTiming(bow.Actions, out float release, out float duration), "Release timeline missing.");
            Check(release > 0f && release < duration, "Windup must precede release and recovery.");
            Check(bow.Actions[0].Entries[0].Clip.name.Contains("BowShot"), "Expected existing Archer animation.");
            var data = ((RangedWeaponBehaviour)bow.Behaviour).projectile;
            Check(data != null && data.visualPrefab != null && data.impact is ArrowImpactBehaviour, "Arrow setup incomplete.");
            Check(!ProjectileFlight.IsFinite(new Vector3(float.NaN, 0, 0)) &&
                !ProjectileFlight.IsFinite(new Vector3(0, float.PositiveInfinity, 0)), "Non-finite aim accepted.");
            TestSweptImpact(data, true);
            TestSweptImpact(data, false);
            Debug.Log("[RangedCombatTests] Assets, timing, invalid aim, thin-wall obstruction, one-hit impact and attachment passed.");
        }

        private static void TestSweptImpact(ProjectileDefinition source, bool wallFirst)
        {
            // Isolated far-away fixtures. Manual physics simulation does not enter Play Mode.
            var mode = Physics.simulationMode;
            var data = UnityEngine.Object.Instantiate(source);
            var owner = new GameObject("RangedTestOwner");
            var target = new GameObject("RangedTestTarget");
            var wall = new GameObject("RangedTestThinWall");
            ProjectileFlight shot = null;
            try
            {
                Physics.simulationMode = SimulationMode.Script;
                Vector3 origin = new Vector3(10000, 10000, 10000);
                owner.transform.position = origin;
                var self = owner.AddComponent<BoxCollider>();
                self.size = Vector3.one;
                target.transform.position = origin + Vector3.forward * 3f;
                target.AddComponent<BoxCollider>();
                target.AddComponent<SphereCollider>(); // Multiple colliders must still yield one damage callback.
                wall.transform.position = origin + Vector3.forward * (wallFirst ? 1.5f : 5f);
                wall.AddComponent<BoxCollider>().size = new Vector3(2f, 2f, 0.01f);
                data.speed = 250f; // Crosses both obstacles in one tick: catches tunnelling/regression.
                data.gravityScale = 0f;
                Physics.SyncTransforms();
                int impacts = 0;
                Collider struck = null;
                shot = ProjectileFlight.Launch(ProjectileFlight.NextId(), data, owner.transform, true,
                    origin, Vector3.forward, true, (col, point, direction) => { impacts++; struck = col; });
                Check(!shot.CanHit(self), "Shooter collider was not ignored.");
                var tick = typeof(ProjectileFlight).GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
                tick.Invoke(shot, null);
                Check(impacts == 1, "Expected exactly one swept impact.");
                Check(struck != null && struck.gameObject == (wallFirst ? wall : target), "Projectile did not choose nearest obstruction.");
                Check(shot.transform.parent == struck.transform, "Arrow failed to embed in hit surface.");
                Vector3 local = shot.transform.localPosition;
                struck.transform.position += Vector3.right;
                Check(shot.transform.localPosition == local, "Embedded arrow did not retain local attachment.");
                tick.Invoke(shot, null);
                Check(impacts == 1, "Embedded arrow applied damage again.");
            }
            finally
            {
                if (shot != null) UnityEngine.Object.DestroyImmediate(shot.gameObject);
                UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(wall);
                UnityEngine.Object.DestroyImmediate(data);
                Physics.simulationMode = mode;
            }
        }
    }
}
