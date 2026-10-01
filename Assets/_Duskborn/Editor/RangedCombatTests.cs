using System;
using System.Reflection;
using Duskborn.Gameplay.Enemies;
using Duskborn.Gameplay.Equipment;
using Duskborn.Gameplay.Player;
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
            var bowSet = bow.Actions[0].BowAnimations;
            Check(bowSet != null && bowSet.IsValid, "Load/Hold/Release and aim locomotion clips are required.");
            Check(bowSet.load.length < release && Mathf.Abs(bowSet.load.length + bowSet.release.length - duration) < .02f,
                "Split bow takes must match the authoritative shot timeline.");
            Check(AssetDatabase.GetAssetPath(bow.Actions[0].Entries[0].Clip) == "Assets/ThirdPartyAssets/Kevin Iglesias/Archer Animations/Animations/Combat/Archer@BowShot01.fbx", "Expected Kevin Iglesias bow clip; custom animation must not return.");
            var data = ((RangedWeaponBehaviour)bow.Behaviour).projectile;
            Check(data != null && data.visualPrefab != null && data.impact is ArrowImpactBehaviour, "Arrow setup incomplete.");
            Check(data.trailMaterial != null && data.impactParticleMaterial != null, "Arrow feedback materials missing.");
            Check(data.trailMaterial.shader.name == "Duskborn/Arrow Feedback" &&
                data.impactParticleMaterial.shader == data.trailMaterial.shader, "Feedback shader references missing.");
            BowPoseAnchorTests.RunAllTests();
            AuthoredBowPlaybackTests.RunAllTests();
            TestDuplicateReplicaImpact(data);
            Check(!ProjectileFlight.IsFinite(new Vector3(float.NaN, 0, 0)) &&
                !ProjectileFlight.IsFinite(new Vector3(0, float.PositiveInfinity, 0)), "Non-finite aim accepted.");
            TestSweptImpact(data, true);
            TestSweptImpact(data, false);
            TestSweptImpact(data, true, true);
            TestScaledNockAlignment(data);
            TestArrowLimbAttachmentAndRagdoll(data);
            TestRangedSpawnOffsetControls();
            Debug.Log("[RangedCombatTests] Assets, timing, invalid aim, thin-wall obstruction, one-hit impact, limb attachment, ragdoll following and spawn controls passed.");
        }

        private static void TestDuplicateReplicaImpact(ProjectileDefinition data)
        {
            var shot = ProjectileFlight.Launch(ProjectileFlight.NextId(), data, null, true, Vector3.zero, Vector3.forward, false);
            try
            {
                int id = (int)typeof(ProjectileFlight).GetField("id", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(shot);
                ProjectileFlight.ReceiveImpact(id, Vector3.forward, Vector3.forward, null, "", true, "Flesh");
                Vector3 embedded = shot.transform.position;
                ProjectileFlight.ReceiveImpact(id, Vector3.right * 9, Vector3.right, null, "", false, "Stone");
                Check(shot.transform.position == embedded, "Duplicate impact moved an embedded arrow; effects would repeat too.");
            }
            finally { UnityEngine.Object.DestroyImmediate(shot.gameObject); }
        }

        private static void TestScaledNockAlignment(ProjectileDefinition source)
        {
            var data = UnityEngine.Object.Instantiate(source);
            try
            {
                data.visualScale = new Vector3(0.8f, 1.2f, 1.75f);
                Vector3 hand = new Vector3(3f, 1.4f, -2f);
                Quaternion rotation = Quaternion.Euler(-35f, 70f, 5f);
                Vector3 tip = data.TipPositionFromNock(hand, rotation);
                Vector3 scale = Vector3.Scale(data.visualPrefab.transform.localScale, data.visualScale);
                Vector3 nock = tip + rotation * Vector3.Scale(data.nockLocalPosition, scale);
                Check(Vector3.Distance(nock, hand) < 0.0001f, "Scaled, pitched arrow detached from its drawing hand.");
                Check(Vector3.Dot(tip - hand, rotation * Vector3.forward) > 1f, "Arrow tip must lead its nock.");
            }
            finally { UnityEngine.Object.DestroyImmediate(data); }
        }

        private static void TestSweptImpact(ProjectileDefinition source, bool wallFirst, bool handLaunch = false)
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
                wall.transform.position = origin + Vector3.forward * (handLaunch ? 0.6f : wallFirst ? 1.5f : 5f);
                wall.AddComponent<BoxCollider>().size = new Vector3(2f, 2f, 0.01f);
                data.speed = 250f; // Crosses both obstacles in one tick: catches tunnelling/regression.
                data.gravityScale = 0f;
                Physics.SyncTransforms();
                int impacts = 0;
                Collider struck = null;
                Vector3 tip = handLaunch ? data.TipPositionFromNock(origin, Quaternion.identity) : origin;
                shot = ProjectileFlight.Launch(ProjectileFlight.NextId(), data, owner.transform, true,
                    tip, Vector3.forward, true, (col, point, direction) => { impacts++; struck = col; },
                    clearanceOrigin: handLaunch ? origin : (Vector3?)null);
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

        private static void TestArrowLimbAttachmentAndRagdoll(ProjectileDefinition source)
        {
            var mode = Physics.simulationMode;
            var data = UnityEngine.Object.Instantiate(source);
            data.speed = 250f;
            data.gravityScale = 0f;

            var owner = new GameObject("RangedLimbTestOwner");
            var enemyGo = new GameObject("RagdollTestEnemy");
            var limbGo = new GameObject("ArmBone");
            ProjectileFlight shot = null;

            try
            {
                Physics.simulationMode = SimulationMode.Script;
                Vector3 origin = new Vector3(20000, 20000, 20000);
                owner.transform.position = origin;

                // Enemy outer collider (capsule)
                enemyGo.transform.position = origin + Vector3.forward * 3f;
                var rootCol = enemyGo.AddComponent<CapsuleCollider>();
                rootCol.radius = 0.5f;
                rootCol.height = 2f;
                rootCol.center = new Vector3(0, 1, 0);

                var ragdoll = enemyGo.AddComponent<EnemyRagdoll>();

                // Limb child bone with its own collider and kinematic rigidbody
                limbGo.transform.SetParent(enemyGo.transform, false);
                limbGo.transform.localPosition = new Vector3(0f, 1f, 0f);
                var limbRb = limbGo.AddComponent<Rigidbody>();
                limbRb.isKinematic = true;
                var limbCol = limbGo.AddComponent<CapsuleCollider>();
                limbCol.radius = 0.15f;
                limbCol.height = 0.6f;

                ragdoll.EnsureInitialized();
                Physics.SyncTransforms();

                shot = ProjectileFlight.Launch(ProjectileFlight.NextId(), data, owner.transform, true,
                    origin + Vector3.up * 1f, Vector3.forward, true);

                var tick = typeof(ProjectileFlight).GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
                tick.Invoke(shot, null);

                Check(shot.transform.parent == limbGo.transform,
                    $"Expected arrow parent to be limb {limbGo.name}, was {shot.transform.parent?.name}.");

                // Moving the limb simulates ragdoll / animation movement:
                Vector3 initialLocal = shot.transform.localPosition;
                limbGo.transform.position += new Vector3(0.5f, -0.3f, 0.2f);
                Check(shot.transform.localPosition == initialLocal, "Arrow did not follow limb movement.");
            }
            finally
            {
                if (shot != null) UnityEngine.Object.DestroyImmediate(shot.gameObject);
                if (owner != null) UnityEngine.Object.DestroyImmediate(owner);
                if (enemyGo != null) UnityEngine.Object.DestroyImmediate(enemyGo);
                if (limbGo != null) UnityEngine.Object.DestroyImmediate(limbGo);
                if (data != null) UnityEngine.Object.DestroyImmediate(data);
                Physics.simulationMode = mode;
            }
        }

        private static void TestRangedSpawnOffsetControls()
        {
            var playerGo = new GameObject("TestPlayer");
            try
            {
                playerGo.transform.position = new Vector3(10f, 20f, 30f);
                playerGo.transform.rotation = Quaternion.Euler(0f, 90f, 0f); // Facing East (+X)

                playerGo.AddComponent<PlayerStats>();
                var combat = playerGo.AddComponent<PlayerCombat>();

                // 1. Default spawn position (relative to player, default is (0, 1.3, 0))
                combat.GetRangedSpawn((WeaponDefinition)null, out Vector3 defaultPos, out Quaternion defaultRot);
                Vector3 expectedDefault = playerGo.transform.TransformPoint(new Vector3(0f, 1.3f, 0f));
                Check((defaultPos - expectedDefault).sqrMagnitude < 0.0001f,
                    $"Default spawn position mismatch. Expected {expectedDefault}, got {defaultPos}.");

                // 2. Custom local offset relative to player
                Vector3 customOffset = new Vector3(0.5f, 1.5f, 0.8f);
                combat.RangedSpawnOffset = customOffset;
                combat.GetRangedSpawn((WeaponDefinition)null, out Vector3 customPos, out _);
                Vector3 expectedCustom = playerGo.transform.TransformPoint(customOffset);
                Check((customPos - expectedCustom).sqrMagnitude < 0.0001f,
                    $"Custom local spawn position mismatch. Expected {expectedCustom}, got {customPos}.");

                // 3. Custom rotation offset relative to aim
                Vector3 rotOffset = new Vector3(10f, 20f, 30f);
                combat.RangedSpawnRotationOffset = rotOffset;
                combat.GetRangedSpawn((WeaponDefinition)null, out _, out Quaternion finalRot, Vector3.forward);
                Quaternion expectedRot = Quaternion.LookRotation(Vector3.forward) * Quaternion.Euler(rotOffset);
                Check(Quaternion.Angle(finalRot, expectedRot) < 0.01f,
                    $"Custom rotation offset mismatch. Expected {expectedRot.eulerAngles}, got {finalRot.eulerAngles}.");

                // 4. Transform spawn point override
                var spawnPointGo = new GameObject("SpawnPoint");
                spawnPointGo.transform.SetParent(playerGo.transform, false);
                spawnPointGo.transform.localPosition = new Vector3(-0.3f, 1.4f, 0.2f);
                combat.RangedSpawnPoint = spawnPointGo.transform;
                combat.GetRangedSpawn((WeaponDefinition)null, out Vector3 pointPos, out _);
                Check((pointPos - spawnPointGo.transform.position).sqrMagnitude < 0.0001f,
                    $"RangedSpawnPoint transform position mismatch. Expected {spawnPointGo.transform.position}, got {pointPos}.");

                // 5. Weapon override
                // A per-weapon override must also override an assigned player socket.
                var rangedBehaviour = ScriptableObject.CreateInstance<RangedWeaponBehaviour>();
                rangedBehaviour.useCustomSpawnOffset = true;
                rangedBehaviour.spawnPositionOffset = new Vector3(0.1f, 1.2f, 0.3f);
                rangedBehaviour.spawnRotationOffset = new Vector3(5f, 5f, 0f);
                combat.GetRangedSpawn(rangedBehaviour, out Vector3 weaponPos, out Quaternion weaponRot, Vector3.forward);
                Vector3 expectedWeaponPos = playerGo.transform.TransformPoint(new Vector3(0.1f, 1.2f, 0.3f));
                Check((weaponPos - expectedWeaponPos).sqrMagnitude < 0.0001f,
                    $"Weapon spawn override position mismatch. Expected {expectedWeaponPos}, got {weaponPos}.");
                UnityEngine.Object.DestroyImmediate(rangedBehaviour);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playerGo);
            }
        }
    }
}
