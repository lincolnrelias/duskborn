using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FishNet.Component.Transforming;
using FishNet.Managing.Object;
using FishNet.Object;
using GameKit.Dependencies.Utilities;
using Duskborn.Gameplay.Enemies;
using Duskborn.Gameplay.Loot;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace Duskborn.Editor
{
    public static class BriarbackBuilder
    {
        public const string ModelFolder = "Assets/_Duskborn/Art/Models/Briarback";
        public const string PrefabPath = "Assets/_Duskborn/Prefabs/Enemies/Briarback.prefab";
        public const string DataFolder = "Assets/_Duskborn/ScriptableObjects/Enemies";
        public const string ModelPath = ModelFolder + "/Briarback.fbx";
        public const string NaturalAudioSource = "Artifacts/NaturalSfx/Briarback/v004/unity";
        public static readonly string[] AudioCues = { "Windup", "Charge", "HeadbuttWindup", "Headbutt", "Hurt", "Death", "Hoof" };
        public static string AudioFile(string cue, int take) => "BB_" + cue +
            (take == 1 && (cue == "Windup" || cue == "Charge" || cue == "Hurt" || cue == "Death") ? "" : "_" + take.ToString("00")) + ".wav";
        public const string NetworkPrefabs = "Assets/_Duskborn/Network/DefaultPrefabObjects.asset";
        public const int BudgetCost = 18;
        public static readonly string[] ClipNames = { "Idle", "Walk", "Windup", "Charge", "Recover", "Hurt", "Death" };
        public static readonly string[] HeadbuttClipNames = { "HeadbuttWindup", "Headbutt", "HeadbuttRecover" };

        [MenuItem("Duskborn/Enemies/Build Briarback")]
        public static void Build()
        {
            var generator = AppDomain.CurrentDomain.GetAssemblies().Select(a =>
                a.GetType("FishNet.Editing.PrefabCollectionGenerator.Generator")).First(t => t != null);
            var suppression = generator.GetField("IgnorePostProcess", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            bool previous = (bool)suppression.GetValue(null);
            try { suppression.SetValue(null, true); BuildAssets(generator); }
            finally { suppression.SetValue(null, previous); }
        }
        private static void BuildAssets(Type generator)
        {
            Directory.CreateDirectory(ModelFolder);
            foreach (string file in new[] { "Briarback.fbx", "BB_Palette.png" })
                File.Copy("Artifacts/Briarback/v003/" + file, ModelFolder + "/" + file, true);
            foreach (string cue in AudioCues)
                for (int take = 1; take <= 2; take++)
                    File.Copy(NaturalAudioSource + "/" + AudioFile(cue, take), ModelFolder + "/" + AudioFile(cue, take), true);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.globalScale = 1; importer.useFileScale = true;
            importer.importAnimation = true; importer.importCameras = false; importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importNormals = ModelImporterNormals.Import;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            var definitions = importer.defaultClipAnimations;
            foreach (var clip in definitions)
            {
                clip.name = clip.name.Substring(clip.name.LastIndexOf('|') + 1);
                clip.loopTime = clip.name == "BB_Idle" || clip.name == "BB_Walk" || clip.name == "BB_Charge";
                clip.loopPose = false;
                clip.keepOriginalOrientation = clip.keepOriginalPositionY = clip.keepOriginalPositionXZ = true;
                clip.lockRootRotation = clip.lockRootHeightY = clip.lockRootPositionXZ = true;
            }
            importer.clipAnimations = definitions; importer.SaveAndReimport();
            var texture = (TextureImporter)AssetImporter.GetAtPath(ModelFolder + "/BB_Palette.png");
            texture.filterMode = FilterMode.Point; texture.mipmapEnabled = false;
            texture.textureCompression = TextureImporterCompression.Uncompressed; texture.SaveAndReimport();
            foreach (string cue in AudioCues)
                for (int take = 1; take <= 2; take++)
            {
                var audio = (AudioImporter)AssetImporter.GetAtPath(ModelFolder + "/" + AudioFile(cue, take));
                audio.forceToMono = true;
                var audioSettings = new SerializedObject(audio);
                audioSettings.FindProperty("m_Normalize").boolValue = false;
                audioSettings.ApplyModifiedPropertiesWithoutUndo();
                var samples = audio.defaultSampleSettings;
                samples.loadType = AudioClipLoadType.DecompressOnLoad;
                samples.compressionFormat = AudioCompressionFormat.PCM;
                samples.preloadAudioData = true;
                audio.defaultSampleSettings = samples; audio.SaveAndReimport();
            }
            var palette = MaterialAsset("BB_Palette", "Universal Render Pipeline/Lit");
            palette.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ModelFolder + "/BB_Palette.png"));
            palette.SetColor("_BaseColor", Color.white); palette.SetFloat("_Smoothness", .05f);
            var amber = MaterialAsset("BB_Amber", "Universal Render Pipeline/Lit");
            amber.SetColor("_BaseColor", new Color(1, .47f, .035f)); amber.EnableKeyword("_EMISSION");
            amber.SetColor("_EmissionColor", new Color(1, .22f, .005f) * 1.4f);
            var warning = MaterialAsset("BB_Warning", "Universal Render Pipeline/Unlit");
            warning.SetFloat("_Surface", 1); warning.SetFloat("_Blend", 0);
            warning.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            warning.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            warning.SetFloat("_ZWrite", 0); warning.SetFloat("_Cull", 0);
            warning.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); warning.renderQueue = 3000;
            var impactDust = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Duskborn/Art/Models/HollowWarden/HW_Warning.mat");
            var impactStone = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Duskborn/Art/Models/HollowWarden/HW_Root.mat");
            Require(impactDust != null && impactStone != null, "Warden ground-impact materials missing.");
            var clips = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__")).ToDictionary(c => c.name);
            foreach (var name in ClipNames) Require(clips.ContainsKey("BB_" + name), "Missing clip BB_" + name);
            BuildHeadbuttClips(clips);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ModelFolder + "/Briarback.controller") ??
                AnimatorController.CreateAnimatorControllerAtPath(ModelFolder + "/Briarback.controller");
            var machine = controller.layers[0].stateMachine;
            foreach (var state in machine.states) machine.RemoveState(state.state);
            controller.parameters = new[] { new AnimatorControllerParameter { name = "Dead", type = AnimatorControllerParameterType.Bool } };
            foreach (var name in ClipNames.Concat(HeadbuttClipNames))
            {
                var state = machine.AddState("BB_" + name); state.motion = clips["BB_" + name];
                state.writeDefaultValues = true;
                if (name == "Idle") machine.defaultState = state;
            }

            var loot = Asset<DropLootTable>(DataFolder + "/briarback_loot_table.asset");
            loot.goldMin = 4; loot.goldMax = 8;
            loot.entries = new[] {
                new DropEntry { itemDefinition = AssetDatabase.LoadAssetAtPath<InventorySystem.Data.ItemDefinitionBase>(
                    "Assets/_Duskborn/ScriptableObjects/Resources/material_leather.asset"), baseChance = .8f, minAmount = 1, maxAmount = 2, scalingBonus = .1f },
                new DropEntry { itemDefinition = AssetDatabase.LoadAssetAtPath<InventorySystem.Data.ItemDefinitionBase>(
                    "Assets/_Duskborn/ScriptableObjects/Resources/material_sap.asset"), baseChance = .35f, minAmount = 1, maxAmount = 1, scalingBonus = .1f } };
            EditorUtility.SetDirty(loot);
            foreach (var entry in loot.entries) BuildPickup(entry.itemDefinition);
            var root = new GameObject("Briarback");
            try
            {
                root.layer = LayerMask.NameToLayer("Enemy"); root.tag = "Enemy";
                string hashText = new string((PrefabPath + root.name).Trim().ToLowerInvariant().Where(c =>
                    (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')).ToArray());
                root.AddComponent<NetworkObject>().SetAssetPathHash(hashText.GetStableHashU64());
                var network = root.AddComponent<NetworkTransform>();
                var networkSettings = new SerializedObject(network);
                networkSettings.FindProperty("_clientAuthoritative").boolValue = false;
                networkSettings.ApplyModifiedPropertiesWithoutUndo();
                var agent = root.AddComponent<NavMeshAgent>();
                agent.enabled = false; agent.radius = .6f; agent.height = 1.8f;
                agent.speed = 3.2f; agent.acceleration = 10; agent.stoppingDistance = .4f;
                agent.angularSpeed = 240; agent.updateRotation = false;
                var collider = root.AddComponent<CapsuleCollider>();
                collider.height = 2.6f; collider.radius = .6f; collider.direction = 2;
                collider.center = new Vector3(0, .85f, 0);
                var enemy = root.AddComponent<Briarback>();
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
                visual.name = "Visual"; visual.transform.SetParent(root.transform, false);
                var animator = visual.GetComponent<Animator>() ?? visual.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller; animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                foreach (var renderer in visual.GetComponentsInChildren<SkinnedMeshRenderer>())
                    renderer.sharedMaterials = new[] { palette, amber };
                BuildRagdoll(root, visual, animator);
                var stats = new SerializedObject(enemy);
                stats.FindProperty("_entity.maxHP").floatValue = 120;
                stats.FindProperty("_entity.damage").floatValue = 18;
                stats.FindProperty("_entity.moveSpeed").floatValue = 3.2f;
                stats.FindProperty("_entity.critChance").floatValue = 0;
                stats.FindProperty("creatureTypes").intValue = (int)Duskborn.Gameplay.TargetType.Beast;
                stats.FindProperty("playerLayer").intValue = 1 << LayerMask.NameToLayer("Player");
                stats.FindProperty("deathDelay").floatValue = 4.5f;
                stats.FindProperty("_animator").objectReferenceValue = animator;
                stats.FindProperty("_damageNumberConfig").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Object>(
                    "Assets/_Duskborn/Prefabs/Enemies/enemy_floating_dmg_config.asset");
                stats.FindProperty("lootTable").objectReferenceValue = loot;
                stats.ApplyModifiedPropertiesWithoutUndo();
                var presentation = new SerializedObject(root.AddComponent<BriarbackPresentation>());
                presentation.FindProperty("enemy").objectReferenceValue = enemy;
                presentation.FindProperty("animator").objectReferenceValue = animator;
                presentation.FindProperty("warningMaterial").objectReferenceValue = warning;
                presentation.FindProperty("impactDustMaterial").objectReferenceValue = impactDust;
                presentation.FindProperty("impactStoneMaterial").objectReferenceValue = impactStone;
                foreach (var cue in new[] { "windup", "charge", "hurt", "death" })
                    presentation.FindProperty(cue + "Clip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(
                        ModelFolder + "/BB_" + char.ToUpperInvariant(cue[0]) + cue.Substring(1) + ".wav");
                foreach (string cue in AudioCues)
                {
                    var variants = presentation.FindProperty(char.ToLowerInvariant(cue[0]) + cue.Substring(1) + "Clips");
                    variants.arraySize = 2;
                    for (int take = 1; take <= 2; take++)
                        variants.GetArrayElementAtIndex(take - 1).objectReferenceValue =
                            AssetDatabase.LoadAssetAtPath<AudioClip>(ModelFolder + "/" + AudioFile(cue, take));
                }
                presentation.ApplyModifiedPropertiesWithoutUndo();
                var healthPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Duskborn/Prefabs/UI/WorldHealthBar.prefab");
                Require(healthPrefab != null, "Shared health bar missing.");
                var health = (GameObject)PrefabUtility.InstantiatePrefab(healthPrefab);
                health.transform.SetParent(root.transform, false);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { Object.DestroyImmediate(root); }

            var registry = AssetDatabase.LoadAssetAtPath<EnemyPrefabRegistry>(DataFolder + "/EnemyPrefabRegistry.asset");
            Require(registry != null, "Enemy registry missing.");
            var entries = registry.Entries.Where(e => e.Type != EnemyType.Briarback).ToList();
            entries.Add(new EnemyPrefabRegistry.Entry { Type = EnemyType.Briarback,
                Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent<Briarback>(), InitialPoolSize = 5 });
            registry.Entries = entries.ToArray(); EditorUtility.SetDirty(registry);
            BuildNightDefinitions();
            foreach (var asset in new Object[] { palette, amber, warning, controller }) EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            generator.GetMethod("GenerateFull", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .Invoke(null, new object[] { null, true, true });
            var collection = AssetDatabase.LoadAssetAtPath<SinglePrefabObjects>(NetworkPrefabs);
            Require(collection != null, "Scene network prefab collection missing.");
            collection.AddObject(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent<NetworkObject>(),
                checkForDuplicates: true, initializeAdded: false);
            foreach (var entry in loot.entries)
                collection.AddObject(entry.itemDefinition.dropPrefab.GetComponent<NetworkObject>(),
                    checkForDuplicates: true, initializeAdded: false);
            EditorUtility.SetDirty(collection); AssetDatabase.SaveAssets();
            BriarbackTests.RunAllTests();
            Debug.Log("[Briarback] Build succeeded.");
        }
        private static void BuildRagdoll(GameObject root, GameObject visual, Animator animator)
        {
            var bones = visual.GetComponentsInChildren<Transform>().ToDictionary(t => t.name);
            foreach (var bone in bones.Values) bone.gameObject.layer = root.layer;
            Rigidbody Body(string name, float mass)
            {
                var body = bones[name].gameObject.AddComponent<Rigidbody>();
                body.mass = mass; body.isKinematic = true;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                return body;
            }
            void Capsule(Transform bone, Vector3 rootCenter, float length, float radius, Vector3 worldAxis)
            {
                var col = bone.gameObject.AddComponent<CapsuleCollider>();
                col.center = bone.InverseTransformPoint(root.transform.TransformPoint(rootCenter));
                Vector3 local = bone.InverseTransformDirection(worldAxis);
                col.direction = Mathf.Abs(local.x) > Mathf.Abs(local.y) && Mathf.Abs(local.x) > Mathf.Abs(local.z)
                    ? 0 : Mathf.Abs(local.y) > Mathf.Abs(local.z) ? 1 : 2;
                col.height = length; col.radius = radius; col.enabled = false;
            }
            void Joint(Rigidbody body, Rigidbody parent)
            {
                var joint = body.gameObject.AddComponent<CharacterJoint>();
                joint.connectedBody = parent; joint.anchor = Vector3.zero;
                joint.autoConfigureConnectedAnchor = true; joint.enableCollision = false;
                joint.enableProjection = true; joint.projectionDistance = .04f; joint.projectionAngle = 10;
                joint.lowTwistLimit = new SoftJointLimit { limit = -25 };
                joint.highTwistLimit = new SoftJointLimit { limit = 25 };
                joint.swing1Limit = new SoftJointLimit { limit = 40 };
                joint.swing2Limit = new SoftJointLimit { limit = 25 };
            }
            var torso = Body("Body", 8);
            Capsule(torso.transform, new Vector3(0, 1.04f, -.15f), 1.9f, .46f, root.transform.forward);
            var head = Body("Head", 3); Joint(head, torso);
            var headCollider = head.gameObject.AddComponent<SphereCollider>();
            headCollider.radius = .43f; headCollider.enabled = false;
            headCollider.center = head.transform.InverseTransformPoint(root.transform.TransformPoint(new Vector3(0, 1.02f, .75f)));
            Capsule(bones["Snout"], new Vector3(0, .81f, 1.20f), .62f, .23f, root.transform.forward);
            foreach (string label in new[] { "Fore.R", "Fore.L", "Hind.R", "Hind.L" })
            {
                var upper = Body(label, 1); Joint(upper, torso);
                var shin = Body("Shin" + label, .7f); Joint(shin, upper);
                Vector3 upperMid = root.transform.InverseTransformPoint((upper.position + shin.position) * .5f);
                Vector3 lowerMid = root.transform.InverseTransformPoint((shin.position + bones["Hoof" + label].position) * .5f);
                Capsule(upper.transform, upperMid, Vector3.Distance(upper.position, shin.position), .13f, shin.position - upper.position);
                Capsule(shin.transform, lowerMid, Vector3.Distance(shin.position, bones["Hoof" + label].position), .09f,
                    bones["Hoof" + label].position - shin.position);
                // Hooves share the shin body; an extra free body would let their toes detach.
                var hoof = bones["Hoof" + label];
                Vector3 rest = root.transform.InverseTransformPoint(hoof.position);
                Capsule(hoof, new Vector3(rest.x, .11f, rest.z + .03f), .32f, .11f, root.transform.forward);
            }
            var settings = new SerializedObject(root.AddComponent<EnemyRagdoll>());
            settings.FindProperty("_animator").objectReferenceValue = animator;
            settings.FindProperty("_settleDuration").floatValue = 2.5f;
            settings.FindProperty("_impulseScale").floatValue = 5;
            settings.FindProperty("_disableBoneCollidersWhileAlive").boolValue = true;
            settings.FindProperty("_restorePoseOnReset").boolValue = true;
            settings.FindProperty("_ignoreSelfCollisions").boolValue = true;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildHeadbuttClips(System.Collections.Generic.Dictionary<string, AnimationClip> clips)
        {
            var model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
            try
            {
                clips["BB_Idle"].SampleAnimation(model, 0);
                var transforms = model.GetComponentsInChildren<Transform>();
                var head = transforms.Single(t => t.name == "Head");
                Vector3 pitchAxis = head.InverseTransformDirection(model.transform.right);
                for (int attack = 0; attack < HeadbuttClipNames.Length; attack++)
                {
                    string name = "BB_" + HeadbuttClipNames[attack];
                    float[] times = attack == 0 ? new[] { 0f, .18f, BriarbackCharge.HeadbuttWindupSeconds } :
                        attack == 1 ? new[] { 0f, .06f, .12f, BriarbackCharge.HeadbuttSeconds } :
                        new[] { 0f, .25f, BriarbackCharge.HeadbuttRecoverSeconds };
                    float[] pitches = attack == 0 ? new[] { 0f, 10f, 18f } :
                        attack == 1 ? new[] { 18f, -28f, -20f, -12f } : new[] { -12f, 5f, 0f };
                    string path = ModelFolder + "/" + name + ".anim";
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                    if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, path); }
                    clip.ClearCurves(); clip.name = name; clip.frameRate = 60;
                    foreach (var bone in transforms.Where(t => t != model.transform))
                    {
                        string bonePath = AnimationUtility.CalculateTransformPath(bone, model.transform);
                        for (int component = 0; component < 4; component++)
                        {
                            var keys = times.Select((time, index) =>
                            {
                                var rotation = bone == head ? bone.localRotation * Quaternion.AngleAxis(pitches[index], pitchAxis) : bone.localRotation;
                                return new Keyframe(time, rotation[component]);
                            }).ToArray();
                            clip.SetCurve(bonePath, typeof(Transform), "m_LocalRotation." + "xyzw"[component], new AnimationCurve(keys));
                        }
                        for (int component = 0; component < 3; component++)
                            clip.SetCurve(bonePath, typeof(Transform), "m_LocalPosition." + "xyz"[component],
                                AnimationCurve.Constant(0, times.Last(), bone.localPosition[component]));
                    }
                    clip.EnsureQuaternionContinuity(); EditorUtility.SetDirty(clip); clips[name] = clip;
                }
            }
            finally { Object.DestroyImmediate(model); }
        }

        private static void BuildPickup(InventorySystem.Data.ItemDefinitionBase definition)
        {
            Require(definition != null, "Canonical drop definition missing.");
            if (definition.dropPrefab != null) return;
            bool leather = definition.Id == "material_leather";
            string path = "Assets/_Duskborn/Prefabs/DroppableItems/droppable_" + (leather ? "leather" : "sap") + ".prefab";
            var material = MaterialAsset(leather ? "BB_LeatherDrop" : "BB_SapDrop", "Universal Render Pipeline/Lit");
            material.SetColor("_BaseColor", leather ? new Color(.39f, .22f, .10f) : new Color(.72f, .37f, .055f));
            material.SetFloat("_Smoothness", leather ? .05f : .35f); EditorUtility.SetDirty(material);
            var root = new GameObject(leather ? "Leather" : "Sticky Sap");
            try
            {
                root.layer = LayerMask.NameToLayer("Resource"); root.tag = "Resource";
                string hash = new string((path + root.name).ToLowerInvariant().Where(c =>
                    (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')).ToArray());
                root.AddComponent<NetworkObject>().SetAssetPathHash(hash.GetStableHashU64());
                var network = new SerializedObject(root.AddComponent<NetworkTransform>());
                network.FindProperty("_clientAuthoritative").boolValue = false; network.ApplyModifiedPropertiesWithoutUndo();
                root.AddComponent<WorldItemPickup>();
                var lookup = new SerializedObject(root.AddComponent<WorldItemPickupDefinition>());
                lookup.FindProperty("definition").objectReferenceValue = definition; lookup.ApplyModifiedPropertiesWithoutUndo();
                root.AddComponent<Rigidbody>().mass = .2f;
                var collider = root.AddComponent<BoxCollider>(); collider.size = new Vector3(.32f, .16f, .25f);
                var visual = GameObject.CreatePrimitive(leather ? PrimitiveType.Cube : PrimitiveType.Sphere);
                Object.DestroyImmediate(visual.GetComponent<Collider>());
                visual.name = leather ? "Folded hide" : "Amber sap";
                visual.layer = root.layer; visual.transform.SetParent(root.transform, false);
                visual.transform.localScale = leather ? new Vector3(.32f, .09f, .25f) : new Vector3(.24f, .16f, .20f);
                visual.GetComponent<Renderer>().sharedMaterial = material;
                definition.dropPrefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                EditorUtility.SetDirty(definition);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void BuildNightDefinitions()
        {
            var basic = AssetDatabase.LoadAssetAtPath<EnemySpawnPool>(DataFolder + "/Pool_Name.asset");
            Require(basic != null, "Existing Swarmer pool missing.");
            int[] budgets = { 0, 100, 120, 100, 160, 190, 220 };
            float[] weights = { 0, 0, .22f, 0, .32f, .38f, .44f };
            for (int night = 2; night <= 6; night++)
            {
                var definition = Asset<NightDefinition>(DataFolder + "/Night_" + night + "_Definition.asset");
                definition.NightNumber = night; definition.BaseBudget = budgets[night];
                if (night == 3) definition.Pools = new[] { basic }; // Preserve Warden's supporting wave pressure.
                else
                {
                    var pool = Asset<EnemySpawnPool>(DataFolder + "/Pool_Briarback_Night" + night + ".asset");
                    pool.PoolName = "Briarback night " + night;
                    pool.Entries = new[] { new EnemySpawnPoolEntry { Type = EnemyType.Briarback, Cost = BudgetCost, Weight = weights[night] } };
                    definition.Pools = new[] { basic, pool }; EditorUtility.SetDirty(pool);
                }
                EditorUtility.SetDirty(definition);
            }
            AssetDatabase.SaveAssets();
            // Patch only WaveManager's definition list; preserve unrelated, uncommitted scene edits.
            const string scenePath = "Assets/_Duskborn/Scenes/SampleScene.unity";
            string scene = File.ReadAllText(scenePath);
            var pattern = new Regex(@"(?m)^  nightDefinitions:\r?\n(?:  - \{fileID: 11400000, guid: [a-f0-9]+, type: 2\}\r?\n)+");
            Require(pattern.Matches(scene).Count == 1, "Cannot uniquely locate scene night definitions.");
            string newline = scene.Contains("\r\n") ? "\r\n" : "\n";
            string list = "  nightDefinitions:" + newline;
            for (int night = 1; night <= 6; night++)
                list += "  - {fileID: 11400000, guid: " + AssetDatabase.AssetPathToGUID(DataFolder + "/Night_" + night + "_Definition.asset") + ", type: 2}" + newline;
            string updated = pattern.Replace(scene, list, 1);
            if (updated != scene)
            {
                // Unity's importer memory-maps YAML on Windows; release it before writing.
                AssetDatabase.ReleaseCachedFileHandles();
                // Replacing the file also works when a worker retains a read-only mapping.
                string temporary = scenePath + ".briarback.tmp";
                File.WriteAllText(temporary, updated);
                File.Replace(temporary, scenePath, null);
                AssetDatabase.ImportAsset(scenePath);
            }
        }
        private static T Asset<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) { asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); }
            return asset;
        }
        private static Material MaterialAsset(string name, string shaderName)
        {
            string path = ModelFolder + "/" + name + ".mat";
            var asset = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (asset == null)
            {
                var shader = Shader.Find(shaderName); Require(shader != null, "Missing shader " + shaderName);
                asset = new Material(shader) { name = name }; AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
        }
        public static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("Briarback: " + message); }
    }
}
