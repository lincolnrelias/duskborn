using System;
using System.Collections;
using System.Reflection;
using Duskborn.Effects;
using Duskborn.Gameplay.Enchanting;
using Duskborn.Gameplay.Equipment;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace Duskborn.Editor
{
    public static class RuntimePresentationRegressionTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private sealed class Burning : IDebuffSource
        {
            public bool TryGetDebuff(RuneKind kind, out DebuffView view)
            { view = new DebuffView(3, .8f); return kind == RuneKind.Flame; }
        }
        private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        public static void RunAllTests()
        {
            TestPlayerRecentHitVisibility(); TestEquippedWeaponEffects(); TestCachedTerrainNavigation(); TestDebuffVisualsBeforeClock();
            TestFreshTerrainNavigation();
            Debug.Log("[RuntimePresentationRegressionTests] 5/5 tests passed.");
        }
        private static void TestFreshTerrainNavigation()
        {
            var owner = new GameObject("Fresh terrain navigation regression");
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Unity.AI.Navigation.NavMeshSurface surface = null;
            try
            {
                // Production TerrainManager is translated down by 5.5m. A larger
                // offset and rotation also catch accidental world/local-space mixing.
                owner.transform.SetPositionAndRotation(new Vector3(10000, -15, 10000), Quaternion.Euler(0, 30, 0));
                floor.transform.position = new Vector3(10000, 0, 10000);
                floor.transform.localScale = new Vector3(120, 1, 120);
                var manager = owner.AddComponent<ChunkGridManager>();
                surface = manager.EnsureNavMeshSurface();
                Physics.SyncTransforms();
                var build = manager.RebuildNavMeshAsync();
                Check(build.MoveNext(), "Async navigation starts with a frame yield");
                build.MoveNext();
                // This static edit-mode runner cannot advance native async completion
                // frames. Finish a floor build synchronously in the production data's
                // coordinate frame, then exercise the production registration helper.
                NavMeshBuilder.Cancel(surface.navMeshData);
                var sources = new System.Collections.Generic.List<NavMeshBuildSource>
                {
                    new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box,
                        transform = floor.transform.localToWorldMatrix, size = Vector3.one, area = 0 }
                };
                var bounds = new Bounds(owner.transform.InverseTransformPoint(floor.transform.position), new Vector3(200, 40, 200));
                Check(NavMeshBuilder.UpdateNavMeshData(surface.navMeshData, surface.GetBuildSettings(), sources, bounds),
                    "Native navigation fixture builds successfully");
                manager.EnsureRuntimeNavigation();
                Vector3 candidate = floor.transform.position + Vector3.forward * 40 + Vector3.up * .5f;
                Check(NavMesh.SamplePosition(candidate, out var hit, 2, NavMesh.AllAreas),
                    "Fresh async navigation must serve grounded wave perimeter queries");
                Check(Mathf.Abs(hit.position.y - candidate.y) < .5f,
                    "Fresh async navigation must align with terrain, without applying the surface offset twice");
                Debug.Log("[PASS] TestFreshTerrainNavigation");
            }
            finally
            {
                var data = surface != null ? surface.navMeshData : null;
                if (data != null) NavMeshBuilder.Cancel(data);
                if (surface != null) surface.RemoveData();
                UnityEngine.Object.DestroyImmediate(owner); UnityEngine.Object.DestroyImmediate(floor);
                if (data != null) UnityEngine.Object.DestroyImmediate(data);
            }
        }
        private static void TestPlayerRecentHitVisibility()
        {
            var actor = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Duskborn/Prefabs/Player/Player 1.prefab"));
            try
            {
                var bar = WorldHealthBar.EnsureForActor(actor.transform);
                typeof(WorldHealthBar).GetMethod("Start", Private).Invoke(bar, null);
                var group = bar.GetComponent<CanvasGroup>();
                Check(group.alpha == 0, "Player health must start hidden");
                var row = bar.GetComponentInChildren<RuneStatusBar>();
                typeof(RuneStatusBar).GetField("_source", Private).SetValue(row, new Burning());
                typeof(WorldHealthBar).GetMethod("LateUpdate", Private).Invoke(bar, null);
                Check(group.alpha == 0, "A player debuff alone must not override recent-hit visibility");
                typeof(WorldHealthBar).GetField("_targetFill", Private).SetValue(bar, 1f);
                typeof(WorldHealthBar).GetField("_displayFill", Private).SetValue(bar, 1f);
                typeof(WorldHealthBar).GetMethod("HandleHealthChanged", Private).Invoke(bar, new object[] { 70f, 100f });
                Check(group.alpha == 1 && (float)typeof(WorldHealthBar).GetField("_fadeTimer", Private).GetValue(bar) > 3, "Damage shows the player bar for the existing recent-hit delay");
                group.alpha = 0;
                typeof(WorldHealthBar).GetField("_fadeTimer", Private).SetValue(bar, 0f);
                typeof(WorldHealthBar).GetField("_displayFill", Private).SetValue(bar, .7f);
                typeof(WorldHealthBar).GetMethod("LateUpdate", Private).Invoke(bar, null);
                Check(group.alpha == 0 && (float)typeof(WorldHealthBar).GetField("_fadeTimer", Private).GetValue(bar) == 0, "Expired player bar stays hidden even with a debuff");
            }
            finally { UnityEngine.Object.DestroyImmediate(actor); }
        }
        private static void TestCachedTerrainNavigation()
        {
            var owner = new GameObject("Cached terrain navigation regression");
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var chunk = new GameObject("Chunk_0_0").AddComponent<TerrainChunk>();
            Unity.AI.Navigation.NavMeshSurface surface = null;
            try
            {
                Vector3 center = new Vector3(10000, 0, 10000);
                owner.transform.position = center;
                floor.transform.position = center - Vector3.up * .5f; floor.transform.localScale = new Vector3(120, 1, 120);
                chunk.transform.SetParent(owner.transform, false);
                var manager = owner.AddComponent<ChunkGridManager>();
                manager.generateWaterPlane = false; manager.generateAtmosphereFX = false;
                surface = manager.EnsureNavMeshSurface();
                Check(surface.navMeshData == null, "Reproduce commit cleanup with no baked navigation");
                bool readyEvent = false;
                manager.OnWorldGenerationComplete += () =>
                {
                    Check(NavMesh.SamplePosition(center + Vector3.right * 40, out _, 2, NavMesh.AllAreas), "World-ready event must have reachable perimeter navigation");
                    readyEvent = true;
                };
                typeof(ChunkGridManager).GetMethod("UseExistingSceneTerrain", Private).Invoke(manager, new object[] { new[] { chunk } });
                Check(manager.IsWorldReady && readyEvent, "Cached terrain initializes navigation before world readiness");
                foreach (var direction in new[] { Vector3.left, Vector3.right, Vector3.forward, Vector3.back })
                    Check(NavMesh.SamplePosition(center + direction * 40, out _, 2, NavMesh.AllAreas), "Wave perimeter can be sampled");
                surface.RemoveData();
                Check(!NavMesh.SamplePosition(center + Vector3.forward * 40, out _, 2, NavMesh.AllAreas), "Unregistered navigation data cannot serve wave queries");
                // The async completion path calls this same registration helper. Build completion
                // is frame-driven, so the synchronous CLI fixture exercises completed native data.
                manager.EnsureRuntimeNavigation();
                Check(NavMesh.SamplePosition(center + Vector3.forward * 40, out _, 2, NavMesh.AllAreas), "Completed data must be registered for wave queries");
            }
            finally
            {
                var data = surface != null ? surface.navMeshData : null;
                if (surface != null) surface.RemoveData();
                UnityEngine.Object.DestroyImmediate(owner); UnityEngine.Object.DestroyImmediate(floor);
                if (data != null) UnityEngine.Object.DestroyImmediate(data);
            }
        }
        private static void TestDebuffVisualsBeforeClock()
        {
            var actor = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Duskborn/Prefabs/Enemies/Bramblekin.prefab"));
            try
            {
                var enemy = actor.GetComponent<Duskborn.Gameplay.Enemies.EnemyBase>();
                ((FishNet.Object.Synchronizing.SyncVar<float>)typeof(Duskborn.Gameplay.Enemies.EnemyBase).GetField("_currentHP", Private | BindingFlags.Public).GetValue(enemy)).Value = 30;
                ((FishNet.Object.Synchronizing.SyncVar<ulong>)typeof(Duskborn.Gameplay.Enemies.EnemyBase).GetField("_runeStacks", Private | BindingFlags.Public).GetValue(enemy)).Value = 3;
                Check(enemy.TryGetDebuff(RuneKind.Flame, out var view) && view.Stacks == 3, "Stacks must display before separate clock metadata arrives");
                var visuals = actor.GetComponent<EnemyRuneVisuals>() ?? actor.AddComponent<EnemyRuneVisuals>();
                typeof(EnemyRuneVisuals).GetMethod("Start", Private).Invoke(visuals, null);
                typeof(EnemyRuneVisuals).GetMethod("LateUpdate", Private).Invoke(visuals, null);
                var particles = actor.GetComponentInChildren<ParticleSystem>();
                Check(particles != null && particles.isPlaying && actor.GetComponentInChildren<RuneSurfaceMesh>() != null,
                    "Active stack state must start actual enemy particles and shader surface even without a clock packet");
            }
            finally { UnityEngine.Object.DestroyImmediate(actor); }
        }
        private static void TestEquippedWeaponEffects()
        {
            var definition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/_Duskborn/ScriptableObjects/Weapons/weapon_blood_blade.asset");
            Check(definition != null && definition.Prefab != null, "Blood blade definition exists");
            var actor = new GameObject("Weapon handler regression");
            var held = UnityEngine.Object.Instantiate(definition.Prefab, actor.transform);
            try
            {
                definition.AttachmentProfile?.ApplyToTransform(held.transform);
                var handler = actor.AddComponent<PlayerWeaponHandler>();
                var weapon = (WeaponItem)definition.CreateRuntimeItem(); weapon.Etching = new WeaponEtching { kind = RuneKind.Flame, level = 3 };
                typeof(PlayerWeaponHandler).GetField("_activeWeapon", Private).SetValue(handler, weapon);
                typeof(PlayerWeaponHandler).GetField("_heldInstance", Private).SetValue(handler, held);
                typeof(PlayerWeaponHandler).GetMethod("RefreshRuneAura", Private).Invoke(handler, null);
                var aura = held.GetComponent<RuneAura>(); Check(aura != null, "Equipped rune initializes via the real weapon handler");
                var particles = held.GetComponentInChildren<ParticleSystem>();
                particles.Simulate(.3f, true, true);
                var values = new ParticleSystem.Particle[80]; int count = particles.GetParticles(values);
                Check(count > 0 && held.GetComponentInChildren<RuneSurfaceMesh>() != null, "Equipped weapon has live particles and a shader surface");
                Bounds emitted = new Bounds(values[0].position, Vector3.zero);
                for (int i = 0; i < count; i++) emitted.Encapsulate(values[i].position);
                var local = RuneAura.LocalBounds(held.transform);
                Vector3 actualSize = Vector3.Scale(local.size, held.transform.lossyScale);
                Debug.Log($"[RuntimePresentationRegressionTests] Equipped weapon scale={held.transform.lossyScale}, body size={actualSize}, particle size={values[0].GetCurrentSize(particles)}, emission extent={emitted.size}, count={count}.");
                Check(emitted.size.magnitude < actualSize.magnitude + 2f, "Weapon particles must stay near the actual scaled mesh");
                weapon.Etching = default; typeof(PlayerWeaponHandler).GetMethod("RefreshRuneAura", Private).Invoke(handler, null);
                Check(!particles.isPlaying, "Removing an etching stops its particles");
            }
            finally
            {
                var handler = actor.GetComponent<PlayerWeaponHandler>();
                if (handler != null) typeof(PlayerWeaponHandler).GetField("_heldInstance", Private).SetValue(handler, null);
                UnityEngine.Object.DestroyImmediate(actor);
            }
        }
    }
}
