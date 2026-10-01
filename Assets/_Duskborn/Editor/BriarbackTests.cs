using System;
using System.IO;
using System.Linq;
using System.Reflection;
using FishNet.Component.Transforming;
using FishNet.Managing.Object;
using FishNet.Object;
using Duskborn.Core;
using Duskborn.Gameplay.Enemies;
using Duskborn.Gameplay.Loot;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Duskborn.Editor
{
    public static class BriarbackTests
    {
        [MenuItem("Duskborn/Tests/Briarback")]
        public static void RunAllTests()
        {
            ChargeClock();
            Headbutt();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BriarbackBuilder.PrefabPath);
            Check(prefab != null && prefab.GetComponent<Briarback>() != null &&
                prefab.GetComponent<BriarbackPresentation>() != null, "Gameplay prefab incomplete.");
            var enemy = prefab.GetComponent<Briarback>();
            Check(enemy.MaxHP == 120 && enemy.Damage == 18 && Mathf.Approximately(enemy.MoveSpeed, 3.2f), "Balance stats changed.");
            Check(enemy.Types == Duskborn.Gameplay.TargetType.Beast, "Beast targeting missing.");
            var networkSettings = new SerializedObject(prefab.GetComponent<NetworkTransform>());
            Check(!networkSettings.FindProperty("_clientAuthoritative").boolValue, "Movement must remain server authoritative.");
            var presentation = new SerializedObject(prefab.GetComponent<BriarbackPresentation>());
            foreach (string name in new[] { "enemy", "animator", "warningMaterial", "impactDustMaterial", "impactStoneMaterial", "windupClip", "chargeClip", "hurtClip", "deathClip" })
                Check(presentation.FindProperty(name).objectReferenceValue != null, "Missing presentation reference " + name);
            foreach (string cue in BriarbackBuilder.AudioCues)
            {
                var variants = presentation.FindProperty(char.ToLowerInvariant(cue[0]) + cue.Substring(1) + "Clips");
                Check(variants != null && variants.arraySize == 2, "Missing natural audio variants: " + cue);
                for (int take = 1; take <= 2; take++)
                {
                    string path = BriarbackBuilder.ModelFolder + "/" + BriarbackBuilder.AudioFile(cue, take);
                    var audio = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                    Check(audio != null && audio.channels == 1 && audio.frequency == 48000, "Invalid natural cue: " + path);
                    Check(variants.GetArrayElementAtIndex(take - 1).objectReferenceValue == audio, "Wrong cue reference: " + path);
                    var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                    Check(!new SerializedObject(importer).FindProperty("m_Normalize").boolValue && importer.defaultSampleSettings.preloadAudioData &&
                        importer.defaultSampleSettings.compressionFormat == AudioCompressionFormat.PCM, "Audio gain/fidelity changed: " + path);
                    if (cue == "HeadbuttWindup") Check(audio.length <= BriarbackCharge.HeadbuttWindupSeconds, "Headbutt warning exceeds phase.");
                }
            }
            var loot = new SerializedObject(enemy).FindProperty("lootTable").objectReferenceValue as DropLootTable;
            Check(loot != null && loot.entries.All(e => e.itemDefinition != null && e.itemDefinition.dropPrefab != null), "Loot pickup references missing.");
            var registry = AssetDatabase.LoadAssetAtPath<EnemyPrefabRegistry>(BriarbackBuilder.DataFolder + "/EnemyPrefabRegistry.asset");
            Check(registry.Entries.Count(e => e.Type == EnemyType.Briarback && e.Prefab == enemy) == 1, "Enemy registry entry missing/duplicate.");
            var collection = AssetDatabase.LoadAssetAtPath<SinglePrefabObjects>(BriarbackBuilder.NetworkPrefabs);
            Check(Enumerable.Range(0, collection.GetObjectCount()).Any(i =>
                collection.GetObject(true, i) == prefab.GetComponent<NetworkObject>()), "FishNet spawn registration missing.");
            Check(prefab.GetComponentInChildren<Duskborn.Effects.WorldHealthBar>() != null, "Shared health UI missing.");
            ValidateModel();
            ValidateHeadbuttAnimation();
            ValidateRagdoll(prefab);
            ValidateTrample(prefab);
            ValidateWaves();
            Debug.Log("[BriarbackTests] Clock, assets, import, network registration, loot and seeded waves passed.");
        }
        private static void Check(bool value, string message) => BriarbackBuilder.Require(value, message);
        private static void ChargeClock()
        {
            var clock = new BriarbackCharge(); clock.Reset();
            Check(!clock.TryBegin(), "Spawn must offer a grace period.");
            clock.Tick(1); Check(clock.TryBegin(), "Charge should start after grace period.");
            Check(!clock.TryBegin(), "Cannot restart an active windup.");
            clock.Tick(.89f); Check(clock.Phase == BriarbackPhase.Windup, "Early damage window.");
            clock.Tick(.02f); Check(clock.Phase == BriarbackPhase.Charge && clock.Age == 0, "Windup boundary failed.");
            clock.Tick(100); Check(clock.Phase == BriarbackPhase.Recover, "Hitch must retain recovery.");
            clock.Kill(); clock.Tick(100); Check(clock.Phase == BriarbackPhase.Dead, "Dead enemy restarted.");
            clock.Reset(); Check(clock.Phase == BriarbackPhase.Hunt && clock.Age == 0, "Pool reset failed.");
        }
        private static void ValidateModel()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(BriarbackBuilder.ModelPath);
            Check(source != null, "Imported FBX missing.");
            var instance = UnityEngine.Object.Instantiate(source);
            var baked = new Mesh();
            try
            {
                var clips = AssetDatabase.LoadAllAssetsAtPath(BriarbackBuilder.ModelPath).OfType<AnimationClip>()
                    .Where(c => !c.name.StartsWith("__preview__")).ToArray();
                Check(clips.Length == 7, "FBX must contain exactly seven own clips.");
                foreach (var name in BriarbackBuilder.ClipNames) Check(clips.Any(c => c.name == "BB_" + name), "Missing animation " + name);
                var renderer = instance.GetComponentInChildren<SkinnedMeshRenderer>();
                Check(renderer != null && renderer.sharedMesh.subMeshCount == 2 && renderer.bones.Length == 17, "Skin/mesh budget changed.");
                Check(renderer.sharedMesh.triangles.Length / 3 == 1842 && renderer.sharedMesh.uv.Length == renderer.sharedMesh.vertexCount, "Mesh/UV import mismatch.");
                var idle = clips.Single(c => c.name == "BB_Idle"); idle.SampleAnimation(instance, 0);
                renderer.BakeMesh(baked);
                float low = baked.vertices.Min(v => renderer.transform.TransformPoint(v).y);
                float high = baked.vertices.Max(v => renderer.transform.TransformPoint(v).y);
                Check(Mathf.Abs(low) < .02f && high > 1.8f && high < 2.1f, "FBX ground pivot or scale incorrect.");
                foreach (var clip in clips)
                {
                    int samples = Mathf.CeilToInt(clip.length * 60);
                    float clipLow = float.PositiveInfinity;
                    for (int sample = 0; sample <= samples; sample++)
                    {
                        clip.SampleAnimation(instance, clip.length * sample / samples); renderer.BakeMesh(baked);
                        Check(baked.vertices.All(v => !float.IsNaN(v.x) && !float.IsInfinity(v.x)), "Invalid animated skin.");
                        Check(baked.bounds.size.magnitude < 6, "Animation exploded outside intended bounds.");
                        clipLow = Mathf.Min(clipLow, baked.vertices.Min(v => renderer.transform.TransformPoint(v).y));
                    }
                    Check(clipLow > -.02f, "Imported animation sinks into ground: " + clip.name + " Y=" + clipLow);
                    Debug.Log($"[BriarbackTests] {clip.name} grounded at 60Hz: lowest Y={clipLow:F4}m.");
                }
                Debug.Log($"[BriarbackTests] Imported model grounded at Y={low:F4}, height={high-low:F3}m.");
            }
            finally { UnityEngine.Object.DestroyImmediate(baked); UnityEngine.Object.DestroyImmediate(instance); }
        }
        private static void Headbutt()
        {
            var clock = new BriarbackCharge(); clock.Reset(); clock.Tick(1);
            Check(clock.TryBegin(2) && clock.Phase == BriarbackPhase.HeadbuttWindup, "Close headbutt priority missing.");
            clock.Tick(100); Check(clock.Phase == BriarbackPhase.Headbutt && clock.Age == 0, "Headbutt warning skipped on hitch.");
            clock.Tick(100); Check(clock.Phase == BriarbackPhase.HeadbuttRecover && clock.Age == 0, "Headbutt recovery skipped.");
            clock.Tick(.7f); Check(!clock.TryBegin(2) && clock.Phase == BriarbackPhase.Hunt, "Close cooldown fell back to charge.");
            Check(clock.TryBegin(6) && clock.Phase == BriarbackPhase.Windup, "Far charge priority missing.");
            Check(Briarback.InHeadbuttArc(Vector3.forward * 2.2f, Vector3.forward), "Front reach missing.");
            Check(!Briarback.InHeadbuttArc(Vector3.forward * 2.3f, Vector3.forward), "Headbutt exceeded reach.");
            Check(!Briarback.InHeadbuttArc(Vector3.back, Vector3.forward), "Headbutt hit behind creature.");
            Check(!Briarback.InHeadbuttArc(Vector3.right, Vector3.forward), "Headbutt hit outside telegraph.");
            Check(!Briarback.InHeadbuttArc(Vector3.forward + Vector3.up * 2, Vector3.forward), "Headbutt hit another elevation.");
            Check(Briarback.InHeadbuttArc(new Vector3(.7f, 0, 1), Vector3.forward), "Headbutt cone too narrow.");
        }
        private static void ValidateHeadbuttAnimation()
        {
            var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(BriarbackBuilder.ModelPath));
            var baked = new Mesh();
            try
            {
                var renderer = model.GetComponentInChildren<SkinnedMeshRenderer>();
                var head = model.GetComponentsInChildren<Transform>().Single(t => t.name == "Head");
                var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(BriarbackBuilder.ModelFolder + "/Briarback.controller");
                foreach (var name in BriarbackBuilder.HeadbuttClipNames)
                {
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(BriarbackBuilder.ModelFolder + "/BB_" + name + ".anim");
                    Check(clip != null && !clip.isLooping && controller.layers[0].stateMachine.states.Any(s => s.state.motion == clip), "Headbutt clip/controller missing.");
                    float duration = name == "HeadbuttWindup" ? .4f : name == "Headbutt" ? .18f : .7f;
                    Check(Mathf.Abs(clip.length - duration) < .001f, "Headbutt animation timing mismatch.");
                    clip.SampleAnimation(model, 0); var first = head.localRotation;
                    clip.SampleAnimation(model, clip.length); Check(Quaternion.Angle(first, head.localRotation) > 10, "Headbutt head movement missing.");
                    for (int sample = 0; sample <= 60; sample++)
                    {
                        clip.SampleAnimation(model, clip.length * sample / 60); renderer.BakeMesh(baked);
                        Check(baked.bounds.size.magnitude < 6 && baked.vertices.All(v => !float.IsNaN(v.x)), "Headbutt skin invalid.");
                        Check(baked.vertices.Min(v => renderer.transform.TransformPoint(v).y) > -.02f, "Headbutt animation sinks below ground.");
                    }
                }
                Debug.Log("[BriarbackTests] Close/far selection, headbutt cone and three grounded headbutt animation states passed.");
            }
            finally { UnityEngine.Object.DestroyImmediate(baked); UnityEngine.Object.DestroyImmediate(model); }
        }
        private static void ValidateRagdoll(GameObject prefab)
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            var instance = UnityEngine.Object.Instantiate(prefab);
            var floor = new GameObject("Ragdoll test ground");
            try
            {
                SceneManager.MoveGameObjectToScene(instance, scene); SceneManager.MoveGameObjectToScene(floor, scene);
                instance.transform.position = Vector3.up * 3;
                var ground = floor.AddComponent<BoxCollider>(); ground.size = new Vector3(20, .2f, 20);
                floor.transform.position = Vector3.down * .1f;
                var ragdoll = instance.GetComponent<EnemyRagdoll>();
                Check(ragdoll != null, "Death ragdoll missing.");
                var bodies = instance.GetComponentsInChildren<Rigidbody>();
                var joints = instance.GetComponentsInChildren<CharacterJoint>();
                var animator = instance.GetComponentInChildren<Animator>();
                Check(bodies.Length == 10 && joints.Length == 9 && joints.All(j => j.connectedBody != null), "Ragdoll graph incomplete.");
                Check(bodies.All(b => b.isKinematic), "Living bones must be kinematic.");
                var colliders = instance.GetComponentsInChildren<Collider>();
                Check(colliders.Where(c => c.gameObject != instance).All(c => !c.enabled), "Living bone colliders must stay disabled.");
                ragdoll.EnsureInitialized();
                var rest = bodies.Select(b => b.transform.localPosition).ToArray();
                var rotations = bodies.Select(b => b.transform.localRotation).ToArray();
                var torso = bodies.Single(b => b.name == "Body");
                Physics.SyncTransforms();
                float originalHeight = torso.position.y;
                ragdoll.EnableRagdoll();
                Check(ragdoll.IsRagdoll && !animator.enabled && bodies.All(b => !b.isKinematic), "Death did not release skeleton physics.");
                Check(!instance.GetComponent<Collider>().enabled && colliders.Where(c => c.gameObject != instance).All(c => c.enabled), "Death collision handover failed.");
                ragdoll.ApplyImpulse(torso.position, new Vector3(.8f, .1f, .3f));
                var physics = scene.GetPhysicsScene();
                Check(physics != Physics.defaultPhysicsScene, "Physics test must remain isolated from project scenes.");
                for (int i = 0; i < 100; i++) physics.Simulate(.02f);
                Check(torso.position.y < originalHeight - .3f && torso.position.y > -.2f,
                    "Ragdoll must fall and stay above the test ground.");
                Check(bodies.All(b => !float.IsNaN(b.position.x) && b.position.sqrMagnitude < 400), "Ragdoll exploded.");
                ragdoll.DisableRagdoll();
                Check(!ragdoll.IsRagdoll && animator.enabled && bodies.All(b => b.isKinematic), "Pool reset did not restore animation.");
                for (int i = 0; i < bodies.Length; i++)
                    Check(Vector3.Distance(rest[i], bodies[i].transform.localPosition) < .001f &&
                        Quaternion.Angle(rotations[i], bodies[i].transform.localRotation) < .1f, "Pool reset retained corpse pose.");
                Check(instance.GetComponent<Collider>().enabled && colliders.Where(c => c.gameObject != instance).All(c => !c.enabled), "Pool reset collision handover failed.");
                Debug.Log("[BriarbackTests] Ten-body ragdoll simulated for two seconds, received impulse, collided with ground and restored on reuse.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance); UnityEngine.Object.DestroyImmediate(floor);
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static void ValidateTrample(GameObject prefab)
        {
            var settings = new SerializedObject(prefab.GetComponent<BriarbackPresentation>());
            var dust = settings.FindProperty("impactDustMaterial").objectReferenceValue as Material;
            var stone = settings.FindProperty("impactStoneMaterial").objectReferenceValue as Material;
            Check(AssetDatabase.GetAssetPath(dust).EndsWith("HollowWarden/HW_Warning.mat") &&
                AssetDatabase.GetAssetPath(stone).EndsWith("HollowWarden/HW_Root.mat"), "Trample must reuse Warden impact materials.");
            var type = typeof(Briarback).Assembly.GetType("Duskborn.Gameplay.Enemies.HollowWardenGroundImpact", true);
            var effect = Activator.CreateInstance(type, dust, stone, "Briarback trail test");
            var update = type.GetMethod("UpdateTrail");
            var root = (GameObject)type.GetField("_root", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(effect);
            try
            {
                for (int i = 0; i < 100; i++)
                    update.Invoke(effect, new object[] { Vector3.forward * i * .7f, Vector3.forward, .07f, true });
                var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
                Check(renderers.Length == 60 && renderers.Any(r => r.enabled), "Trail must retain a bounded reusable effect pool.");
                Check(renderers.Where(r => r.enabled).Select(r => r.transform.position.z).Distinct().Count() > 1,
                    "Moving trample must leave distinct world-space impact sites.");
                update.Invoke(effect, new object[] { Vector3.forward * 70, Vector3.forward, 2f, false });
                Check(!root.activeSelf && renderers.All(r => !r.enabled), "Trail did not fade after stopping.");
                type.GetMethod("ResetTrail").Invoke(effect, null);
                Check(!root.activeSelf, "Pool reset retained old trample effects.");
                var burst = type.GetMethod("EmitHeadbutt");
                Check(!(bool)burst.Invoke(effect, new object[] { Vector3.zero, Vector3.forward, 5, .02f }), "Headbutt dust preceded strike pose.");
                Check((bool)burst.Invoke(effect, new object[] { Vector3.zero, Vector3.forward, 5, .07f }), "Headbutt impact missing.");
                Check(!(bool)burst.Invoke(effect, new object[] { Vector3.zero, Vector3.forward, 5, .08f }), "Headbutt impact repeated within one attack.");
                update.Invoke(effect, new object[] { Vector3.zero, Vector3.forward, .02f, false });
                Check(renderers.Count(r => r.enabled) == 15 && renderers.Where(r => r.enabled).All(r => r.transform.position.z > .7f),
                    "Headbutt should emit three small ground sites beneath the forward strike.");
                update.Invoke(effect, new object[] { Vector3.zero, Vector3.forward, 2f, false });
                Check(!root.activeSelf, "Headbutt impact did not fade.");
                Check(!(bool)burst.Invoke(effect, new object[] { Vector3.zero, Vector3.forward, 6, .5f }), "Stale headbutt impact replayed.");
                type.GetMethod("ResetTrail").Invoke(effect, null);
                Check((bool)burst.Invoke(effect, new object[] { Vector3.zero, Vector3.forward, 5, .07f }), "Pool reset retained headbutt sequence.");
                Debug.Log("[BriarbackTests] Shared Warden trample trail stayed at 60 pieces, left spaced impacts, faded and reset.");
                Debug.Log("[BriarbackTests] Headbutt ground burst emitted 15 shared pieces once, faded, rejected stale playback and reset.");
            }
            finally { ((IDisposable)effect).Dispose(); }
        }

        private static void ValidateWaves()
        {
            string scene = File.ReadAllText("Assets/_Duskborn/Scenes/SampleScene.unity");
            for (int night = 1; night <= 6; night++)
            {
                string path = BriarbackBuilder.DataFolder + "/Night_" + night + "_Definition.asset";
                var def = AssetDatabase.LoadAssetAtPath<NightDefinition>(path);
                Check(def != null && scene.Contains(AssetDatabase.AssetPathToGUID(path)), "Night definition not assigned " + night);
                var entries = def.Pools.SelectMany(p => p.Entries).ToArray();
                bool allowed = night == 2 || night >= 4;
                Check(entries.Any(e => e.Type == EnemyType.Briarback) == allowed, "Briarback night gating incorrect " + night);
                float count = 0;
                for (int players = 1; players <= 4; players++)
                    for (int seed = 0; seed < 128; seed++)
                    {
                        var timeline = TimelineGenerator.Generate(def, players, 120, new SeededRNG(seed));
                        var same = TimelineGenerator.Generate(def, players, 120, new SeededRNG(seed));
                        float expected = timeline.Events.Sum(e => entries.First(x => x.Type == e.EnemyType).Cost);
                        Check(Mathf.Approximately(timeline.TotalBudgetSpent, expected) && expected <= def.BaseBudget * (1 + (players - 1) * .4f) + .01f, "Budget overspent.");
                        Check(timeline.Events.All(e => e.Timestamp >= 0 && e.Timestamp < 120), "Spawn outside night.");
                        Check(timeline.Events.Select(e => e.Timestamp).SequenceEqual(same.Events.Select(e => e.Timestamp)) &&
                            timeline.Events.Select(e => e.EnemyType).SequenceEqual(same.Events.Select(e => e.EnemyType)), "Nondeterministic spawn.");
                        if (players == 1) count += timeline.Events.Count(e => e.EnemyType == EnemyType.Briarback);
                    }
                Debug.Log($"[BriarbackTests] Night {night}: mean solo Briarbacks={count/128:F2}, base budget={def.BaseBudget:F0} (128 seeds, 1-4 players).");
            }
        }
    }
}
