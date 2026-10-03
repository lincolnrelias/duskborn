using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Duskborn.Core;
using Duskborn.Gameplay.Enemies;
using Duskborn.Gameplay.Loot;
using Duskborn.Gameplay.Projectiles;
using FishNet.Component.Transforming;
using FishNet.Managing.Object;
using FishNet.Object;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace Duskborn.Editor
{
    public static class ThornwingTests
    {
        private static void Check(bool ok, string message) => ThornwingBuilder.Require(ok, message);
        public static void RunAllTests()
        {
            Clock();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ThornwingBuilder.PrefabPath);
            Check(prefab != null, "Prefab missing.");
            var enemy = prefab.GetComponent<Thornwing>();
            Check(enemy != null && enemy.MaxHP == 24 && enemy.Damage == 3 && Mathf.Approximately(enemy.MoveSpeed, 4.8f), "Stats missing.");
            Check(enemy.Types == Duskborn.Gameplay.TargetType.Beast, "Targeting type missing.");
            Check(prefab.GetComponentInChildren<Duskborn.Effects.WorldHealthBar>() != null, "Health UI missing.");
            Check(!new SerializedObject(prefab.GetComponent<NetworkTransform>()).FindProperty("_clientAuthoritative").boolValue, "Client controls movement.");
            var settings = new SerializedObject(enemy);
            var swarmer = AssetDatabase.LoadAssetAtPath<GameObject>(BramblekinBuilder.PrefabPath);
            Check(settings.FindProperty("deathDelay").floatValue ==
                new SerializedObject(swarmer.GetComponent<EnemyBase>()).FindProperty("deathDelay").floatValue,
                "Corpse lifetime differs from Swarmer standard.");
            var projectile = settings.FindProperty("projectile").objectReferenceValue as ProjectileDefinition;
            Check(settings.FindProperty("flightAnchor").objectReferenceValue != null &&
                settings.FindProperty("mouth").objectReferenceValue != null, "Shared flight/mouth anchors missing.");
            Check(projectile != null && projectile.visualPrefab != null && projectile.gravityScale == 0 && projectile.speed == 18 && projectile.lifetime <= 1.3f, "Spit trajectory mismatch.");
            Check(projectile.visualPrefab.GetComponentsInChildren<Collider>().Length == 0, "Cosmetic projectile collider.");
            var loot = settings.FindProperty("lootTable").objectReferenceValue as DropLootTable;
            Check(loot != null && loot.entries.Length == 1 && loot.entries[0].itemDefinition == AssetDatabase.LoadAssetAtPath<InventorySystem.Data.ItemDefinitionBase>("Assets/_Duskborn/ScriptableObjects/Resources/material_sap.asset") &&
                loot.entries[0].itemDefinition.dropPrefab != null, "Canonical sap reward missing.");
            var presentation = new SerializedObject(prefab.GetComponent<ThornwingPresentation>());
            foreach (string field in new[] { "enemy", "visual", "leftWing", "rightWing", "eyes" })
                Check(presentation.FindProperty(field).objectReferenceValue != null, "Missing presentation " + field);
            var eyeRenderer = (Renderer)presentation.FindProperty("eyes").objectReferenceValue;
            Check(eyeRenderer.sharedMaterial.IsKeywordEnabled("_EMISSION") &&
                eyeRenderer.sharedMaterial.GetColor("_EmissionColor").maxColorComponent >= 2,
                "Eyes must emit light independently of scene lighting.");
            var flightMaterial = (Material)presentation.FindProperty("flightTrailMaterial").objectReferenceValue;
            Check(flightMaterial != null && flightMaterial.renderQueue == 3000 &&
                flightMaterial.GetFloat("_ZWrite") == 0 && flightMaterial.GetFloat("_DstBlend") == 1,
                "Flight trails require a shared transparent additive material.");
            foreach (string cue in ThornwingBuilder.Cues)
            {
                var clips = presentation.FindProperty(char.ToLowerInvariant(cue[0]) + cue.Substring(1) + "Clips");
                Check(clips.arraySize == ThornwingBuilder.Takes(cue), "Audio variations missing.");
                for (int i = 1; i <= clips.arraySize; i++)
                {
                    var clip = clips.GetArrayElementAtIndex(i - 1).objectReferenceValue as AudioClip;
                    Check(clip != null && clip.channels == 1 && clip.frequency == 48000, "Audio fidelity mismatch.");
                    var importer = (AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip));
                    Check(!new SerializedObject(importer).FindProperty("m_Normalize").boolValue, "Foley gain changed.");
                }
            }
            var registry = AssetDatabase.LoadAssetAtPath<EnemyPrefabRegistry>(ThornwingBuilder.Data + "/EnemyPrefabRegistry.asset");
            Check(registry.Entries.Count(e => e.Type == EnemyType.Thornwing && e.Prefab == enemy) == 1, "Registry mismatch.");
            foreach (string path in new[] { "Assets/DefaultPrefabObjects.asset", "Assets/_Duskborn/Network/DefaultPrefabObjects.asset" })
            {
                var collection = AssetDatabase.LoadAssetAtPath<SinglePrefabObjects>(path);
                Check(Enumerable.Range(0, collection.GetObjectCount()).Any(i => collection.GetObject(true, i) == prefab.GetComponent<NetworkObject>()), "FishNet registration missing.");
                Check(Enumerable.Range(0, collection.GetObjectCount()).Any(i => collection.GetObject(true, i) == loot.entries[0].itemDefinition.dropPrefab.GetComponent<NetworkObject>()), "Sap pickup registration missing.");
            }
            SpatialAndReset(prefab); DartNavigation(prefab); Ragdoll(prefab); Waves();
            Debug.Log("[ThornwingTests] Clock, ground reach, assets, reset, projectiles, network, rewards and seeded spawns passed.");
        }
        private static void Clock()
        {
            var clock = new ThornwingClock(); int shots = 0; clock.Release += () => shots++;
            clock.Reset(); Check(!clock.TryBegin(5, 0, true), "Spawn grace missing.");
            clock.Tick(2);
            Check(!clock.TryBegin(10, 0, true) && !clock.TryBegin(5, 2, true) && !clock.TryBegin(5, 0, false), "Attack limits missing.");
            Check(clock.TryBegin(5, 0, true), "Attack unavailable.");
            Check(ThornwingClock.TrackingSeconds == .4f && ThornwingClock.WindupSeconds == .5f, "Attack tracking/commit window changed.");
            clock.Tick(.49f); Check(shots == 0 && clock.Phase == ThornwingPhase.Windup, "Early shot.");
            clock.Tick(100); Check(shots == 1 && clock.Phase == ThornwingPhase.Recover && clock.Age == 0, "Hitch skipped recovery.");
            clock.Tick(100); Check(shots == 1 && !clock.TryBegin(5, 0, true), "Cooldown skipped.");
            clock.Tick(2); Check(clock.TryBegin(1, 0, true), "Close-range telegraph unavailable.");
            clock.Interrupt(); clock.Tick(100); Check(shots == 1, "Interrupted shot escaped.");
            clock.Kill(); clock.Tick(100); Check(clock.Phase == ThornwingPhase.Dead && !clock.TryBegin(1, 0, true), "Dead attack.");
            clock.Reset(); Check(clock.Phase == ThornwingPhase.Hunt && clock.Age == 0 && clock.Cooldown > 0, "Clock reset.");
        }
        private static void SpatialAndReset(GameObject prefab)
        {
            var instance = Object.Instantiate(prefab);
            try
            {
                var enemy = instance.GetComponent<Thornwing>();
                // Static edit-mode tests do not run FishNet's object/behaviour association lifecycle.
                // Associate the isolated fixture without starting a server or simulating a connection.
                typeof(NetworkBehaviour).GetMethod("SerializeComponents", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                    .Invoke(enemy, new object[] { instance.GetComponent<NetworkObject>(), (byte)1 });
                typeof(Thornwing).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(enemy, null);
                for (Type t = typeof(Thornwing); t != null; t = t.BaseType)
                    foreach (var field in t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                        if (field.GetValue(enemy) is FishNet.Object.Synchronizing.Internal.SyncBase sync) sync.NetworkBehaviour = enemy;
                typeof(ThornwingPresentation).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(instance.GetComponent<ThornwingPresentation>(), null);
                var audio = instance.GetComponents<AudioSource>();
                Check(audio.Length == 4 && audio.Take(3).All(s => s.rolloffMode == AudioRolloffMode.Linear &&
                    s.minDistance == 6 && s.maxDistance == 28 && s.spatialBlend == 1), "Combat cue audibility settings missing.");
                Check(audio.Count(s => s.priority == 64) == 2, "Attack/voice priority must beat quiet flutter.");
                var buzz = audio.Single(s => s.loop);
                Check(buzz.priority == 128 && buzz.minDistance == 3 && buzz.maxDistance == 24,
                    "Flight buzz should remain local and below attack/voice priority.");
                var flightPresentation = instance.GetComponent<ThornwingPresentation>();
                var trails = instance.GetComponentsInChildren<TrailRenderer>();
                Check(trails.Length == 2 && trails.All(t => t.sharedMaterial != null && t.time <= .3f &&
                    !t.autodestruct && !t.emitting), "Bounded pooled flight trails missing.");
                foreach (var trail in trails) { trail.emitting = true; trail.AddPosition(Vector3.zero); trail.AddPosition(Vector3.right); }
                flightPresentation.ResetPresentation();
                Check(trails.All(t => !t.emitting && t.positionCount == 0), "Pooled trails retained stale ribbons.");
                typeof(ThornwingPresentation).GetMethod("UpdateBuzz", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(flightPresentation,null);
                var selectedBuzz = buzz.clip; float selectedPitch = buzz.pitch;
                Check(selectedBuzz != null && selectedBuzz.length == 4 && Mathf.Approximately(buzz.volume,.35f), "Buzz loop not configured.");
                typeof(ThornwingPresentation).GetMethod("UpdateBuzz", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(flightPresentation,null);
                Check(buzz.clip == selectedBuzz && buzz.pitch == selectedPitch, "Buzz reselected on every frame.");
                flightPresentation.ResetPresentation(); Check(buzz.clip == null, "Buzz retained stale pooled playback.");
                var windup = new SerializedObject(instance.GetComponent<ThornwingPresentation>()).FindProperty("windupClips")
                    .GetArrayElementAtIndex(0).objectReferenceValue as AudioClip;
                typeof(ThornwingPresentation).GetMethod("Play", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null,new object[] { audio[0],windup,.85f,0f });
                Check(audio[0].clip == windup && Mathf.Approximately(audio[0].volume,.85f), "Cue playback did not configure the source.");
                audio[0].Stop();
                var visual = instance.transform.Find("Visual");
                var filters = visual.GetComponentsInChildren<MeshFilter>();
                Check(filters.Length == 6 && filters.Sum(f => f.sharedMesh.triangles.Length / 3) == 792, "Imported mesh budget mismatch.");
                Check(filters.All(f => f.sharedMesh.uv.Length == f.sharedMesh.vertexCount), "Imported UVs missing.");
                var bounds = new Bounds(visual.position, Vector3.zero);
                // Ribbons intentionally extend beyond the anatomy and retain cached bounds after Clear.
                foreach (var renderer in visual.GetComponentsInChildren<MeshRenderer>()) bounds.Encapsulate(renderer.bounds);
                Check(bounds.size.x > 2 && bounds.size.x < 2.5f && bounds.min.y > .55f && bounds.max.y < 1.9f, "Hover/import dimensions: " + bounds);
                var eyeRenderer = visual.GetComponentsInChildren<Renderer>().Single(r => r.name == "Eyes");
                Check(eyeRenderer.transform.parent.name == "Head", "Eyes must follow the physical head.");
                Check(eyeRenderer.bounds.center.z > visual.position.z + .3f, "Imported creature faces away from its firing direction.");
                // Actual player combat overlaps at ground height. Validate intersection on import.
                var capsule = instance.GetComponent<CapsuleCollider>();
                var attackOrigin = new Vector3(0, 0, 1);
                Check(Vector3.Distance(capsule.ClosestPoint(attackOrigin), attackOrigin) < 2, "Ground melee cannot reach collider.");
                Check(capsule.bounds.max.y >= Thornwing.HoverHeight && capsule.bounds.min.y < .5f, "Hitbox excludes hovering body or ground reach.");
                Check(Vector3.Distance(capsule.bounds.center, visual.position) < .001f, "Hitbox center differs from flight anchor.");
                var socket = visual.Find("Mouth");
                Check(socket != null && Vector3.Distance(socket.position, eyeRenderer.bounds.center) < .25f,
                    "Shot socket is detached from the visible face.");
                var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                try
                {
                    var shotLine = typeof(Thornwing).GetMethod("ClearShot", BindingFlags.Instance | BindingFlags.NonPublic);
                    Physics.SyncTransforms();
                    blocker.SetActive(false); Physics.SyncTransforms();
                    Check((bool)shotLine.Invoke(enemy, new object[] { new Vector3(0, 1, 5) }), "Clear shot rejected.");
                    blocker.transform.position = new Vector3(0, Thornwing.HoverHeight, 2.5f);
                    blocker.transform.localScale = new Vector3(1,2,.1f); blocker.SetActive(true); Physics.SyncTransforms();
                    Check(!(bool)shotLine.Invoke(enemy, new object[] { new Vector3(0, 1, 5) }), "Shot penetrates solid obstacle.");
                    blocker.transform.position = new Vector3(0, Thornwing.HoverHeight, .2f); Physics.SyncTransforms();
                    Check(!(bool)shotLine.Invoke(enemy, new object[] { new Vector3(0, 1, 5) }), "Mouth offset bypasses thin wall.");
                }
                finally { Object.DestroyImmediate(blocker); }
                var left = visual.GetComponentsInChildren<Transform>().Single(t => t.name == "Wing.L");
                Quaternion rest = left.localRotation;
                Vector3 tipBefore = left.TransformPoint(filters.Single(f => f.name == "Wing.L").sharedMesh.vertices.OrderByDescending(v => v.x).First());
                left.localRotation = Quaternion.Euler(0,0,35) * rest;
                Vector3 tipAfter = left.TransformPoint(filters.Single(f => f.name == "Wing.L").sharedMesh.vertices.OrderByDescending(v => v.x).First());
                Check(Mathf.Abs(tipAfter.y - tipBefore.y) > .15f, "Wing axis does not flap vertically.");
                visual.localPosition = Vector3.zero; visual.localRotation = Quaternion.Euler(0,0,100); capsule.enabled = false;
                instance.GetComponent<Thornwing>().ResetEnemy(Vector3.zero);
                Check(capsule.enabled && visual.localRotation == Quaternion.identity &&
                    Mathf.Approximately(visual.localPosition.y, Thornwing.HoverHeight) && left.localRotation == rest, "Pooled pose/collider reset failed.");
                var ragdoll = instance.GetComponent<EnemyRagdoll>();
                ragdoll.EnableRagdoll();
                var presenter = instance.GetComponent<ThornwingPresentation>();
                typeof(ThornwingPresentation).GetField("previousHP", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(presenter,0f);
                typeof(ThornwingPresentation).GetMethod("HealthChanged", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(presenter,new object[] { 24f,24f });
                Check(!ragdoll.IsRagdoll && left.parent == visual && capsule.enabled,
                    "Replicated revival after stale corpse startup did not reattach living wings.");
                var source = new GameObject("Thornwing test projectile owner");
                try
                {
                    var projectile = new SerializedObject(instance.GetComponent<Thornwing>()).FindProperty("projectile").objectReferenceValue as ProjectileDefinition;
                    var flight = ProjectileFlight.Launch(ProjectileFlight.NextId(), projectile, source.transform, true, Vector3.zero, Vector3.forward, true);
                    Check(flight.CanHit(capsule), "Player arrow rejects living Thornwing.");
                    Object.DestroyImmediate(flight.gameObject);
                }
                finally { Object.DestroyImmediate(source); }
            }
            finally { Object.DestroyImmediate(instance); }
        }
        private static void DartNavigation(GameObject prefab)
        {
            var floor = new GameObject("Thornwing navigation fixture");
            var blocker = new GameObject("Thornwing navigation blocker");
            var instance = Object.Instantiate(prefab);
            NavMeshData data = null;
            NavMeshDataInstance installed = default;
            try
            {
                // Keep test surfaces far from existing scene navigation and physics.
                Vector3 origin = new Vector3(10000, 100, 10000);
                floor.transform.position = origin + Vector3.down * .1f;
                floor.AddComponent<BoxCollider>().size = new Vector3(8,.2f,8);
                blocker.AddComponent<BoxCollider>().size = new Vector3(.15f,2,2);
                blocker.SetActive(false);
                var agent = instance.GetComponent<NavMeshAgent>();
                var query = typeof(Thornwing).GetMethod("ValidateDartPath", BindingFlags.Static | BindingFlags.NonPublic);
                var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
                var path = new NavMeshPath(); Vector3 start = origin;
                int solids = ~((1 << instance.layer) | (1 << LayerMask.NameToLayer("Player")) | (1 << 2));
                bool Valid(Vector3 offset) => (bool)query.Invoke(null, new object[] { start, origin + offset, filter, solids, path });
                void Bake(float degrees)
                {
                    agent.enabled = false;
                    if (installed.valid) installed.Remove();
                    if (data != null) Object.DestroyImmediate(data);
                    floor.transform.rotation = Quaternion.Euler(0,0,degrees);
                    var source = new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box,
                        size = new Vector3(8,.2f,8), transform = floor.transform.localToWorldMatrix, area = 0 };
                    data = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(agent.agentTypeID),
                        new System.Collections.Generic.List<NavMeshBuildSource> { source },
                        new Bounds(origin,Vector3.one * 12),Vector3.zero,Quaternion.identity);
                    Check(data != null,"Navigation fixture bake failed.");
                    installed = NavMesh.AddNavMeshData(data);
                    Check(NavMesh.SamplePosition(origin,out var hit,.5f,filter),"Navigation fixture origin missing.");
                    start = hit.position; instance.transform.position = start;
                    Physics.SyncTransforms();
                }
                Bake(0);
                Check(Valid(Vector3.right),"Flat supported dart rejected.");
                // The bake already sits above the collider; add a small terrain mismatch.
                floor.transform.position += Vector3.down * .12f; Physics.SyncTransforms();
                Check(Valid(Vector3.right), "Dart rejected normal navigation bake height tolerance.");
                floor.transform.position += Vector3.up * .12f; Physics.SyncTransforms();
                Check(!Valid(Vector3.right * 5),"Off-mesh/long dart accepted.");
                blocker.transform.position = origin + new Vector3(.5f,1,0); blocker.SetActive(true); Physics.SyncTransforms();
                Check(!Valid(Vector3.right),"Dart crossed a physical wall absent from the bake.");
                blocker.SetActive(false);
                floor.transform.position += Vector3.up * .4f; Physics.SyncTransforms();
                Check(!Valid(Vector3.right),"Dart accepted navigation buried under changed terrain.");
                floor.transform.position -= Vector3.up * .8f; Physics.SyncTransforms();
                Check(!Valid(Vector3.right),"Dart accepted unsupported navigation over a drop.");
                floor.transform.position = origin + Vector3.down * .1f;
                Bake(15);
                Check(Valid(new Vector3(1,.27f,0)),"Supported gradual uphill dart rejected.");
                Check(Valid(new Vector3(-1,-.27f,0)),"Supported gradual downhill dart rejected.");
                Bake(30);
                Check(Valid(new Vector3(.8f,0,0)),"Terrain-projected uphill dodge at the old elevation rejected.");
                Check(Valid(new Vector3(-.8f,0,0)),"Terrain-projected downhill dodge at the old elevation rejected.");
                Bake(40);
                Check(!Valid(new Vector3(1,.84f,0)),"Steep/elevation-changing dart accepted.");
                Debug.Log("[ThornwingTests] Baked navigation: flat/sloped support, walls, drops, buried terrain and elevation limits passed.");
            }
            finally
            {
                Object.DestroyImmediate(instance); Object.DestroyImmediate(blocker); Object.DestroyImmediate(floor);
                if (installed.valid) installed.Remove();
                if (data != null) Object.DestroyImmediate(data);
            }
        }
        private static void Ragdoll(GameObject prefab)
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            var instance = Object.Instantiate(prefab);
            var floor = new GameObject("Thornwing ragdoll test floor");
            try
            {
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(instance, scene);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(floor, scene);
                instance.transform.position = Vector3.up * 1.5f;
                floor.transform.position = Vector3.down * .1f;
                floor.AddComponent<BoxCollider>().size = new Vector3(20,.2f,20);
                var ragdoll = instance.GetComponent<EnemyRagdoll>();
                Check(ragdoll != null, "Ragdoll component missing.");
                var bodies = instance.GetComponentsInChildren<Rigidbody>();
                var joints = instance.GetComponentsInChildren<CharacterJoint>();
                Check(bodies.Length == 5 && joints.Length == 2 && joints.All(j => j.connectedBody != null), "Jointed corpse graph incomplete.");
                Check(bodies.Where(b => b.name.StartsWith("Wing")).All(b => b.GetComponent<Joint>() == null), "Wings remain jointed to body.");
                Check(bodies.All(b => b.isKinematic && b.interpolation == RigidbodyInterpolation.None &&
                    !b.GetComponent<Collider>().enabled), "Living bones must not interpolate world poses.");
                ragdoll.EnsureInitialized();
                var parents = bodies.Select(b => b.transform.parent).ToArray();
                var positions = bodies.Select(b => b.transform.localPosition).ToArray();
                var rotations = bodies.Select(b => b.transform.localRotation).ToArray();
                var torso = bodies.Single(b => b.name == "Body");
                var left = bodies.Single(b => b.name == "Wing.L");
                var right = bodies.Single(b => b.name == "Wing.R");
                var physics = scene.GetPhysicsScene();
                Check(physics != Physics.defaultPhysicsScene, "Ragdoll simulation must be isolated.");
                // Exercise moving/turning living roots, the condition missed by static pose checks.
                for (int frame = 0; frame < 20; frame++)
                {
                    instance.transform.position = new Vector3(frame * .12f,1.5f,frame * .04f);
                    instance.transform.rotation = Quaternion.Euler(0,frame * 9,0);
                    Physics.SyncTransforms(); physics.Simulate(.02f);
                    for (int i = 0; i < bodies.Length; i++)
                        Check(bodies[i].transform.parent == parents[i] &&
                            Vector3.Distance(bodies[i].position, parents[i].TransformPoint(positions[i])) < .005f,
                            "Living body/wing drifted from its moving root.");
                }
                instance.transform.position = Vector3.up * 1.5f; instance.transform.rotation = Quaternion.identity;
                Physics.SyncTransforms();
                float height = torso.position.y;
                ragdoll.EnableRagdoll();
                Check(left.transform.parent == instance.transform && right.transform.parent == instance.transform, "Wings did not detach from visual.");
                Check(!instance.GetComponent<Collider>().enabled && bodies.All(b => !b.isKinematic && b.GetComponent<Collider>().enabled), "Collision handover failed.");
                Check(bodies.All(b => b.interpolation == RigidbodyInterpolation.Interpolate), "Corpse interpolation not enabled.");
                ragdoll.EnableRagdoll(); // HP and impulse can arrive in either order; handover is idempotent.
                Physics.SyncTransforms(); physics.Simulate(.02f);
                var beforeImpulse = bodies.Select(b => b.linearVelocity).ToArray();
                ragdoll.ApplyImpulse(torso.worldCenterOfMass, Vector3.forward);
                Physics.SyncTransforms(); physics.Simulate(.02f);
                for (int i = 0; i < bodies.Length; i++)
                    Check(bodies[i].linearVelocity.z > beforeImpulse[i].z + 1f,
                        "Fatal-hit momentum did not reach " + bodies[i].name);
                var firstImpulse = bodies.Select(b => b.linearVelocity).ToArray();
                ragdoll.ApplyImpulse(torso.worldCenterOfMass, Vector3.forward);
                physics.Simulate(.02f);
                for (int i = 0; i < bodies.Length; i++)
                    Check(bodies[i].linearVelocity.z < firstImpulse[i].z + .3f,
                        "Fatal impulse replayed on " + bodies[i].name);
                Physics.SyncTransforms();
                for (int i = 0; i < 100; i++) physics.Simulate(.02f);
                Check(torso.position.y < height - .3f && torso.position.y > -.2f, "Body did not fall onto the floor.");
                Check(left.position.y < height - .3f && right.position.y < height - .3f &&
                    left.GetComponent<Collider>().bounds.min.y > -.04f && right.GetComponent<Collider>().bounds.min.y > -.04f,
                    $"Detached wing floor contact failed: left={left.position}, minY={left.GetComponent<Collider>().bounds.min.y}; right={right.position}, minY={right.GetComponent<Collider>().bounds.min.y}.");
                Check(Vector3.Distance(left.position,right.position) > .6f, "Wings did not separate physically.");
                Check(bodies.All(b => ProjectileFlight.IsFinite(b.position) && b.position.sqrMagnitude < 100), "Corpse physics unstable.");
                ragdoll.DisableRagdoll();
                Check(!ragdoll.IsRagdoll && bodies.All(b => b.isKinematic &&
                    b.interpolation == RigidbodyInterpolation.None && !b.GetComponent<Collider>().enabled), "Living collision/interpolation state not restored.");
                for (int i = 0; i < bodies.Length; i++)
                    Check(bodies[i].transform.parent == parents[i] && Vector3.Distance(bodies[i].transform.localPosition,positions[i]) < .001f &&
                        Quaternion.Angle(bodies[i].transform.localRotation,rotations[i]) < .1f, "Pool reset did not reattach/restore parts.");
                ragdoll.EnableRagdoll(); ragdoll.DisableRagdoll();
                Check(left.transform.parent == parents[Array.IndexOf(bodies,left)], "Second pooled life lost wing parent.");
                Debug.Log("[ThornwingTests] Jointed corpse, detached wings, isolated gravity/impulse/floor collision and repeated reuse passed.");
            }
            finally
            {
                Object.DestroyImmediate(instance); Object.DestroyImmediate(floor);
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            }
        }
        private static void Waves()
        {
            string scene = File.ReadAllText("Assets/_Duskborn/Scenes/SampleScene.unity");
            for (int night = 1; night <= 6; night++)
            {
                string path = ThornwingBuilder.Data + "/Night_" + night + "_Definition.asset";
                var def = AssetDatabase.LoadAssetAtPath<NightDefinition>(path);
                Check(scene.Contains(AssetDatabase.AssetPathToGUID(path)), "WaveManager missing night " + night);
                var entries = def.Pools.SelectMany(p => p.Entries).ToArray();
                Check(entries.Count(e => e.Type == EnemyType.Thornwing && e.Cost == 8) == 1, "Day-one eligibility or pool duplication.");
                int total = 0, maximum = 0, empty = 0;
                for (int players = 1; players <= 4; players++) for (int seed = 0; seed < 128; seed++)
                {
                    var t = TimelineGenerator.Generate(def, players, 120, new SeededRNG(seed));
                    var same = TimelineGenerator.Generate(def, players, 120, new SeededRNG(seed));
                    float spent = t.Events.Sum(e => entries.First(x => x.Type == e.EnemyType).Cost);
                    Check(Mathf.Approximately(spent,t.TotalBudgetSpent) && spent <= def.BaseBudget * (1 + (players-1)*.4f), "Spawn budget exceeded.");
                    Check(t.Events.Select(e => e.EnemyType).SequenceEqual(same.Events.Select(e => e.EnemyType)) &&
                        t.Events.Select(e => e.Timestamp).SequenceEqual(same.Events.Select(e => e.Timestamp)), "Spawns nondeterministic.");
                    if (players == 1)
                    { int count = t.Events.Count(e => e.EnemyType == EnemyType.Thornwing); total += count; maximum = Math.Max(maximum,count); if(count==0)empty++; }
                }
                Debug.Log($"[ThornwingTests] Night {night}: solo mean={total/128f:F2}, max={maximum}, zero-spawn seeds={empty}/128; 1-4 player budgets passed.");
            }
        }
    }
}
