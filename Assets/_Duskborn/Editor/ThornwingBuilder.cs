using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Duskborn.Gameplay.Enemies;
using Duskborn.Gameplay.Loot;
using Duskborn.Gameplay.Projectiles;
using FishNet.Component.Transforming;
using FishNet.Managing.Object;
using FishNet.Object;
using GameKit.Dependencies.Utilities;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace Duskborn.Editor
{
    public static class ThornwingBuilder
    {
        public const string Folder = "Assets/_Duskborn/Art/Models/Thornwing";
        public const string PrefabPath = "Assets/_Duskborn/Prefabs/Enemies/Thornwing.prefab";
        public const string Data = "Assets/_Duskborn/ScriptableObjects/Enemies";
        public const int BudgetCost = 8;
        public static readonly string[] Cues = { "Windup", "Spit", "Hurt", "Death", "Flutter", "Buzz" };
        public static int Takes(string cue) => cue == "Flutter" ? 4 : cue == "Buzz" ? 1 : 2;

        public static void Build()
        {
            var generator = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("FishNet.Editing.PrefabCollectionGenerator.Generator")).First(t => t != null);
            var suppression = generator.GetField("IgnorePostProcess", BindingFlags.Public | BindingFlags.Static);
            bool previous = (bool)suppression.GetValue(null);
            try { suppression.SetValue(null, true); BuildAssets(generator); }
            finally { suppression.SetValue(null, previous); }
        }
        private static void BuildAssets(Type generator)
        {
            Directory.CreateDirectory(Folder);
            foreach (string file in new[] { "Thornwing.fbx", "TW_Palette.png" })
                File.Copy("Artifacts/Thornwing/v002/" + file, Folder + "/" + file, true);
            foreach (string cue in Cues) for (int i = 1; i <= Takes(cue); i++)
                File.Copy("Artifacts/NaturalSfx/Thornwing/" + (cue == "Buzz" || cue == "Death" ? "v002" : "v001") +
                    "/unity/" + AudioName(cue, i), Folder + "/" + AudioName(cue, i), true);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var model = (ModelImporter)AssetImporter.GetAtPath(Folder + "/Thornwing.fbx");
            model.globalScale = 1; model.useFileScale = true; model.importAnimation = false;
            model.importCameras = model.importLights = false; model.materialImportMode = ModelImporterMaterialImportMode.None;
            model.importNormals = ModelImporterNormals.Import; model.SaveAndReimport();
            var texture = (TextureImporter)AssetImporter.GetAtPath(Folder + "/TW_Palette.png");
            texture.filterMode = FilterMode.Point; texture.mipmapEnabled = false;
            texture.textureCompression = TextureImporterCompression.Uncompressed; texture.SaveAndReimport();
            foreach (string cue in Cues) for (int i = 1; i <= Takes(cue); i++)
            {
                var importer = (AudioImporter)AssetImporter.GetAtPath(Folder + "/" + AudioName(cue, i));
                importer.forceToMono = true;
                var settings = new SerializedObject(importer); settings.FindProperty("m_Normalize").boolValue = false;
                settings.ApplyModifiedPropertiesWithoutUndo();
                var samples = importer.defaultSampleSettings; samples.loadType = AudioClipLoadType.DecompressOnLoad;
                samples.compressionFormat = AudioCompressionFormat.PCM; samples.preloadAudioData = true;
                importer.defaultSampleSettings = samples; importer.SaveAndReimport();
            }
            var palette = Material("TW_Palette", "Universal Render Pipeline/Lit");
            palette.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/TW_Palette.png"));
            palette.SetColor("_BaseColor", Color.white); palette.SetFloat("_Smoothness", .05f);
            var amber = Material("TW_Amber", "Universal Render Pipeline/Lit");
            amber.SetColor("_BaseColor", new Color(.95f, .48f, .06f)); amber.EnableKeyword("_EMISSION");
            amber.SetColor("_EmissionColor", new Color(1, .3f, .015f) * 2f);
            amber.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            amber.EnableKeyword("_EMISSION");
            var flightTrail = Material("TW_FlightTrail", "Universal Render Pipeline/Particles/Unlit");
            flightTrail.SetColor("_BaseColor", Color.white);
            flightTrail.SetFloat("_Surface", 1); flightTrail.SetFloat("_Blend", 2);
            flightTrail.SetFloat("_Cull", 0); flightTrail.SetFloat("_ZWrite", 0);
            flightTrail.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            flightTrail.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            flightTrail.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            flightTrail.SetOverrideTag("RenderType", "Transparent"); flightTrail.renderQueue = 3000;
            var trail = Material("TW_Trail", "Universal Render Pipeline/Particles/Unlit");
            trail.SetColor("_BaseColor", new Color(.68f, .85f, .24f));
            var spitRoot = new GameObject("ThornwingSpit");
            try
            {
                var body = GameObject.CreatePrimitive(PrimitiveType.Sphere); body.name = "SapGlob";
                Object.DestroyImmediate(body.GetComponent<Collider>());
                body.transform.SetParent(spitRoot.transform, false); body.transform.localScale = new Vector3(.16f,.16f,.26f);
                body.GetComponent<Renderer>().sharedMaterial = amber;
                PrefabUtility.SaveAsPrefabAsset(spitRoot, Folder + "/ThornwingSpit.prefab");
            }
            finally { Object.DestroyImmediate(spitRoot); }
            var spit = Asset<ProjectileDefinition>(Folder + "/ThornwingSpit.asset");
            spit.visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/ThornwingSpit.prefab");
            spit.visualScale = Vector3.one; spit.nockLocalPosition = Vector3.zero;
            spit.speed = 18; spit.gravityScale = 0; spit.radius = .08f; spit.lifetime = 1.2f;
            spit.embeddedLifetime = .1f; spit.damageMultiplier = 1; spit.impactImpulse = .1f;
            spit.trailMaterial = trail; spit.impactParticleMaterial = trail; spit.impact = null; spit.collisionMask = ~0;
            var loot = Asset<DropLootTable>(Data + "/thornwing_loot_table.asset");
            var sap = AssetDatabase.LoadAssetAtPath<InventorySystem.Data.ItemDefinitionBase>("Assets/_Duskborn/ScriptableObjects/Resources/material_sap.asset");
            Require(sap != null && sap.dropPrefab != null && sap.dropPrefab.GetComponent<NetworkObject>() != null, "Canonical sap pickup missing.");
            loot.goldMin = 1; loot.goldMax = 2;
            loot.entries = new[] { new DropEntry { itemDefinition = sap, baseChance = .2f, minAmount = 1, maxAmount = 1, scalingBonus = 0 } };
            var root = new GameObject("Thornwing");
            try
            {
                root.layer = LayerMask.NameToLayer("Enemy"); root.tag = "Enemy";
                string hash = new string((PrefabPath + root.name).ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
                root.AddComponent<NetworkObject>().SetAssetPathHash(hash.GetStableHashU64());
                var network = new SerializedObject(root.AddComponent<NetworkTransform>());
                network.FindProperty("_clientAuthoritative").boolValue = false; network.ApplyModifiedPropertiesWithoutUndo();
                var agent = root.AddComponent<NavMeshAgent>(); agent.enabled = false; agent.baseOffset = 0;
                agent.radius = .35f; agent.height = 1.7f; agent.speed = 4.8f; agent.acceleration = 22;
                agent.stoppingDistance = .15f; agent.updateRotation = false; agent.angularSpeed = 540;
                var collider = root.AddComponent<CapsuleCollider>(); collider.radius = .42f;
                collider.height = 1.35f; collider.center = Vector3.up * Thornwing.HoverHeight;
                var enemy = root.AddComponent<Thornwing>();
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Thornwing.fbx"));
                visual.name = "Visual"; visual.transform.SetParent(root.transform, false);
                PrefabUtility.UnpackPrefabInstance(visual, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                visual.transform.localPosition = Vector3.up * Thornwing.HoverHeight;
                var mouth = new GameObject("Mouth").transform;
                mouth.SetParent(visual.transform, false); mouth.localPosition = new Vector3(0,.07f,.48f);
                foreach (var renderer in visual.GetComponentsInChildren<MeshRenderer>())
                    renderer.sharedMaterials = Enumerable.Repeat(renderer.name == "Eyes" ? amber : palette,
                        renderer.GetComponent<MeshFilter>().sharedMesh.subMeshCount).ToArray();
                foreach (var t in visual.GetComponentsInChildren<Transform>()) t.gameObject.layer = root.layer;
                BuildRagdoll(root, visual);
                var stats = new SerializedObject(enemy);
                stats.FindProperty("_entity.maxHP").floatValue = 24; stats.FindProperty("_entity.damage").floatValue = 3;
                stats.FindProperty("_entity.moveSpeed").floatValue = 4.8f; stats.FindProperty("_entity.critChance").floatValue = 0;
                stats.FindProperty("creatureTypes").intValue = (int)Duskborn.Gameplay.TargetType.Beast;
                stats.FindProperty("attackRange").floatValue = ThornwingClock.Range;
                stats.FindProperty("playerLayer").intValue = 1 << LayerMask.NameToLayer("Player");
                var swarmer = AssetDatabase.LoadAssetAtPath<GameObject>(BramblekinBuilder.PrefabPath);
                Require(swarmer != null, "Swarmer corpse lifetime reference missing.");
                stats.FindProperty("deathDelay").floatValue = new SerializedObject(swarmer.GetComponent<EnemyBase>())
                    .FindProperty("deathDelay").floatValue;
                stats.FindProperty("projectile").objectReferenceValue = spit; stats.FindProperty("lootTable").objectReferenceValue = loot;
                stats.FindProperty("flightAnchor").objectReferenceValue = visual.transform;
                stats.FindProperty("mouth").objectReferenceValue = mouth;
                stats.FindProperty("_damageNumberConfig").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Object>("Assets/_Duskborn/Prefabs/Enemies/enemy_floating_dmg_config.asset");
                stats.ApplyModifiedPropertiesWithoutUndo();
                var transforms = visual.GetComponentsInChildren<Transform>();
                var presentation = new SerializedObject(root.AddComponent<ThornwingPresentation>());
                presentation.FindProperty("enemy").objectReferenceValue = enemy; presentation.FindProperty("visual").objectReferenceValue = visual.transform;
                presentation.FindProperty("leftWing").objectReferenceValue = transforms.Single(t => t.name == "Wing.L");
                presentation.FindProperty("rightWing").objectReferenceValue = transforms.Single(t => t.name == "Wing.R");
                presentation.FindProperty("eyes").objectReferenceValue = transforms.Single(t => t.name == "Eyes").GetComponent<Renderer>();
                presentation.FindProperty("flightTrailMaterial").objectReferenceValue = flightTrail;
                foreach (string cue in Cues)
                {
                    var clips = presentation.FindProperty((cue == "Spit" ? "spit" : char.ToLowerInvariant(cue[0]) + cue.Substring(1)) + "Clips");
                    clips.arraySize = Takes(cue);
                    for (int i = 1; i <= Takes(cue); i++) clips.GetArrayElementAtIndex(i - 1).objectReferenceValue =
                        AssetDatabase.LoadAssetAtPath<AudioClip>(Folder + "/" + AudioName(cue, i));
                }
                presentation.ApplyModifiedPropertiesWithoutUndo();
                var health = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Duskborn/Prefabs/UI/WorldHealthBar.prefab"));
                health.transform.SetParent(root.transform, false);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
            var registry = AssetDatabase.LoadAssetAtPath<EnemyPrefabRegistry>(Data + "/EnemyPrefabRegistry.asset");
            registry.Entries = registry.Entries.Where(e => e.Type != EnemyType.Thornwing).Concat(new[] {
                new EnemyPrefabRegistry.Entry { Type = EnemyType.Thornwing, Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent<Thornwing>(), InitialPoolSize = 5 }
            }).ToArray(); EditorUtility.SetDirty(registry);
            var pool = Asset<EnemySpawnPool>(Data + "/Pool_Thornwing.asset"); pool.PoolName = "Thornwing skirmishers (day 1+)";
            pool.Entries = new[] { new EnemySpawnPoolEntry { Type = EnemyType.Thornwing, Cost = BudgetCost, Weight = .25f } };
            for (int night = 1; night <= 6; night++)
            {
                var def = AssetDatabase.LoadAssetAtPath<NightDefinition>(Data + "/Night_" + night + "_Definition.asset");
                Require(def != null, "Missing night " + night);
                if (!def.Pools.Contains(pool)) def.Pools = def.Pools.Concat(new[] { pool }).ToArray();
                if (night == 1) def.NightNumber = 1;
                EditorUtility.SetDirty(def);
            }
            foreach (var asset in new Object[] { palette, amber, trail, flightTrail, spit, loot, pool }) EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            InstallSceneNightReferences();
            generator.GetMethod("GenerateFull", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { null, true, true });
            foreach (string path in new[] { "Assets/DefaultPrefabObjects.asset", "Assets/_Duskborn/Network/DefaultPrefabObjects.asset" })
            {
                var collection = AssetDatabase.LoadAssetAtPath<SinglePrefabObjects>(path);
                Require(collection != null, "Network collection missing: " + path);
                collection.AddObject(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent<NetworkObject>(), true, false);
                collection.AddObject(sap.dropPrefab.GetComponent<NetworkObject>(), true, false);
                EditorUtility.SetDirty(collection);
            }
            AssetDatabase.SaveAssets(); ThornwingTests.RunAllTests();
        }
        private static void InstallSceneNightReferences()
        {
            // An interactive save can retain an older single-night list. Patch this one
            // integration field while preserving terrain and every unrelated user edit.
            const string path = "Assets/_Duskborn/Scenes/SampleScene.unity";
            string scene = File.ReadAllText(path);
            var pattern = new Regex(@"(?m)^  nightDefinitions:\r?\n(?:  - \{fileID: 11400000, guid: [a-f0-9]+, type: 2\}\r?\n)+");
            Require(pattern.Matches(scene).Count == 1, "Cannot uniquely find WaveManager night definitions.");
            string newline = scene.Contains("\r\n") ? "\r\n" : "\n";
            string list = "  nightDefinitions:" + newline;
            for (int night = 1; night <= 6; night++)
                list += "  - {fileID: 11400000, guid: " + AssetDatabase.AssetPathToGUID(Data + "/Night_" + night + "_Definition.asset") + ", type: 2}" + newline;
            string updated = pattern.Replace(scene, list, 1);
            if (updated == scene) return;
            AssetDatabase.ReleaseCachedFileHandles();
            string temporary = path + ".thornwing.tmp";
            File.WriteAllText(temporary, updated); File.Replace(temporary, path, null);
            AssetDatabase.ImportAsset(path);
        }
        private static void BuildRagdoll(GameObject root, GameObject visual)
        {
            var nodes = visual.GetComponentsInChildren<Transform>().ToDictionary(t => t.name);
            // Eyes follow the physical head; no independent floating eye rigidbodies.
            nodes["Eyes"].SetParent(nodes["Head"], true);
            Rigidbody Body(string name, float mass)
            {
                var body = nodes[name].gameObject.AddComponent<Rigidbody>();
                body.mass = mass; body.isKinematic = true; body.useGravity = true;
                body.interpolation = RigidbodyInterpolation.None;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                body.linearDamping = .25f; body.angularDamping = .7f;
                var bounds = nodes[name].GetComponent<MeshFilter>().sharedMesh.bounds;
                var collider = nodes[name].gameObject.AddComponent<BoxCollider>();
                collider.center = bounds.center;
                collider.size = bounds.size;
                if (name.StartsWith("Wing", StringComparison.Ordinal))
                {
                    var size = collider.size;
                    // FBX mesh units can be centimeters with a compensating node scale.
                    size.z = Mathf.Max(size.z, .06f / Mathf.Max(.001f, Mathf.Abs(nodes[name].lossyScale.z)));
                    collider.size = size;
                }
                collider.enabled = false; return body;
            }
            var torso = Body("Body", .5f);
            foreach (string name in new[] { "Head", "Abdomen" })
            {
                var body = Body(name, .15f);
                var joint = body.gameObject.AddComponent<CharacterJoint>();
                joint.connectedBody = torso; joint.anchor = Vector3.zero;
                joint.autoConfigureConnectedAnchor = true; joint.enableCollision = false;
                joint.enableProjection = true; joint.projectionDistance = .025f; joint.projectionAngle = 10;
                joint.lowTwistLimit = new SoftJointLimit { limit = -15 };
                joint.highTwistLimit = new SoftJointLimit { limit = 15 };
                joint.swing1Limit = new SoftJointLimit { limit = 25 };
                joint.swing2Limit = new SoftJointLimit { limit = 20 };
            }
            Body("Wing.L", .06f); Body("Wing.R", .06f);
            var ragdoll = new SerializedObject(root.AddComponent<EnemyRagdoll>());
            ragdoll.FindProperty("_impulseScale").floatValue = 2f;
            ragdoll.FindProperty("_wholeBodyImpulseFraction").floatValue = .8f;
            ragdoll.FindProperty("_applyImpulseAtHitPoint").boolValue = true;
            ragdoll.FindProperty("_settleDuration").floatValue = 2f;
            ragdoll.FindProperty("_disableBoneCollidersWhileAlive").boolValue = true;
            ragdoll.FindProperty("_restorePoseOnReset").boolValue = true;
            ragdoll.FindProperty("_ignoreSelfCollisions").boolValue = true;
            ragdoll.FindProperty("_disableInterpolationWhileAlive").boolValue = true;
            var detached = ragdoll.FindProperty("_detachOnDeath"); detached.arraySize = 2;
            detached.GetArrayElementAtIndex(0).objectReferenceValue = nodes["Wing.L"];
            detached.GetArrayElementAtIndex(1).objectReferenceValue = nodes["Wing.R"];
            ragdoll.ApplyModifiedPropertiesWithoutUndo();
        }
        public static string AudioName(string cue, int take) => "TW_" + cue + "_" + take.ToString("00") + ".wav";
        private static T Asset<T>(string path) where T : ScriptableObject
        {
            var value = AssetDatabase.LoadAssetAtPath<T>(path);
            if (value == null) { value = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(value, path); }
            return value;
        }
        private static Material Material(string name, string shader)
        {
            string path = Folder + "/" + name + ".mat";
            var value = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (value == null) { value = new Material(Shader.Find(shader)); AssetDatabase.CreateAsset(value, path); }
            return value;
        }
        public static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("[Thornwing] " + message); }
    }
}
