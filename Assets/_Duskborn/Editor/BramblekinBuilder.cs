using System;
using System.IO;
using System.Linq;
using Duskborn.Gameplay.Enemies;
using Duskborn.Gameplay.Loot;
using FishNet.Component.Transforming;
using FishNet.Managing.Object;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace Duskborn.Editor
{
    public static class BramblekinBuilder
    {
        public const string Folder = "Assets/_Duskborn/Art/Models/Bramblekin";
        // Preserve prefab GUID, NetworkObject ID, registry and all scene references.
        public const string PrefabPath = "Assets/_Duskborn/Prefabs/Enemies/Bramblekin.prefab";
        public static readonly string[] Cues = { "Windup", "Swing", "Hurt", "Death", "Step", "Hit" };
        public static int Takes(string cue) => 4;
        public static string AudioName(string cue, int take) => "BK_" + cue + "_" + take.ToString("00") + ".wav";

        public static void Build()
        {
            Require(File.Exists("Artifacts/Bramblekin/v007/Bramblekin.fbx"), "Build the Blender model first.");
            foreach (string cue in Cues) for (int i = 1; i <= Takes(cue); i++)
                Require(File.Exists("Artifacts/NaturalSfx/Bramblekin/v002/unity/" + AudioName(cue, i)), "Missing foley " + cue);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            {
                string error = AssetDatabase.MoveAsset("Assets/_Duskborn/Prefabs/Enemies/Swarmer.prefab", PrefabPath);
                Require(string.IsNullOrEmpty(error), "Cannot migrate basic prefab: " + error);
            }
            Directory.CreateDirectory(Folder);
            foreach (string file in new[] { "Bramblekin.fbx", "BK_Palette.png" })
                File.Copy("Artifacts/Bramblekin/v007/" + file, Folder + "/" + file, true);
            foreach (string cue in Cues) for (int i = 1; i <= Takes(cue); i++)
                File.Copy("Artifacts/NaturalSfx/Bramblekin/v002/unity/" + AudioName(cue, i), Folder + "/" + AudioName(cue, i), true);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var importer = (ModelImporter)AssetImporter.GetAtPath(Folder + "/Bramblekin.fbx");
            importer.globalScale = 1; importer.useFileScale = true; importer.importAnimation = false;
            importer.importLights = importer.importCameras = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importNormals = ModelImporterNormals.Import; importer.SaveAndReimport();
            var texture = (TextureImporter)AssetImporter.GetAtPath(Folder + "/BK_Palette.png");
            texture.filterMode = FilterMode.Point; texture.mipmapEnabled = false;
            texture.textureCompression = TextureImporterCompression.Uncompressed; texture.SaveAndReimport();
            foreach (string cue in Cues) for (int i = 1; i <= Takes(cue); i++)
            {
                var audio = (AudioImporter)AssetImporter.GetAtPath(Folder + "/" + AudioName(cue, i));
                audio.forceToMono = true;
                var settings = new SerializedObject(audio); settings.FindProperty("m_Normalize").boolValue = false;
                settings.ApplyModifiedPropertiesWithoutUndo();
                var samples = audio.defaultSampleSettings; samples.loadType = AudioClipLoadType.DecompressOnLoad;
                samples.compressionFormat = AudioCompressionFormat.PCM; samples.preloadAudioData = true;
                audio.defaultSampleSettings = samples; audio.SaveAndReimport();
            }
            string materialPath = Folder + "/BK_Palette.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, materialPath); }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/BK_Palette.png"));
            material.SetColor("_BaseColor", Color.white); material.SetFloat("_Smoothness", .05f); EditorUtility.SetDirty(material);

            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                root.name = "Bramblekin";
                // Retain only stable networking, enemy, targeting and loot contracts.
                foreach (var component in root.GetComponents<Component>().Reverse())
                    if (!(component is Transform) && !(component is Bramblekin) && !(component is FishNet.Object.NetworkObject) &&
                        !(component is NetworkTransform) && !(component is NavMeshAgent) && !(component is CapsuleCollider) &&
                        !(component is Rigidbody) && !(component is LootDropper) && !(component is EnemyRagdoll))
                        Object.DestroyImmediate(component);
                foreach (Transform child in root.transform.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
                if (root.GetComponent<Bramblekin>() == null) root.AddComponent<Bramblekin>();
                var network = new SerializedObject(root.GetComponent<NetworkTransform>());
                network.FindProperty("_clientAuthoritative").boolValue = false; network.ApplyModifiedPropertiesWithoutUndo();
                var agent = root.GetComponent<NavMeshAgent>(); agent.radius = .32f; agent.height = 1.6f;
                agent.speed = 6.2f; agent.acceleration = 24; agent.angularSpeed = 360; agent.stoppingDistance = .8f;
                agent.baseOffset = 0; agent.updateRotation = false;
                var collider = root.GetComponent<CapsuleCollider>(); collider.radius = .32f;
                collider.height = 1.4f; collider.center = Vector3.up * .7f;
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Bramblekin.fbx"));
                visual.transform.SetParent(root.transform, false); visual.name = "Visual";
                PrefabUtility.UnpackPrefabInstance(visual, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                foreach (var renderer in visual.GetComponentsInChildren<MeshRenderer>()) renderer.sharedMaterial = material;
                foreach (var part in visual.GetComponentsInChildren<Transform>()) part.gameObject.layer = root.layer;
                var nodes = visual.GetComponentsInChildren<Transform>().ToDictionary(t => t.name);
                foreach (string side in new[] { "L", "R" })
                {
                    nodes["Shin." + side].SetParent(nodes["Leg." + side], true);
                    nodes["Foot." + side].SetParent(nodes["Shin." + side], true);
                }
                var enemy = new SerializedObject(root.GetComponent<Bramblekin>());
                enemy.FindProperty("_entity.maxHP").floatValue = 32; enemy.FindProperty("_entity.damage").floatValue = 5;
                enemy.FindProperty("_entity.moveSpeed").floatValue = 6.2f; enemy.FindProperty("_entity.attackSpeed").floatValue = 2;
                enemy.FindProperty("attackRange").floatValue = BramblekinClock.Range;
                enemy.FindProperty("creatureTypes").intValue = (int)Duskborn.Gameplay.TargetType.Humanoid;
                enemy.FindProperty("playerLayer").intValue = 1 << LayerMask.NameToLayer("Player");
                enemy.FindProperty("outlineLayerName").stringValue = "EnemyOutline";
                enemy.FindProperty("_damageNumberConfig").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Object>("Assets/_Duskborn/Prefabs/Enemies/enemy_floating_dmg_config.asset");
                enemy.FindProperty("weapon").objectReferenceValue = null; enemy.FindProperty("holdPoint").objectReferenceValue = null;
                enemy.FindProperty("_animator").objectReferenceValue = null;
                enemy.FindProperty("outlineRenderers").arraySize = 0; enemy.ApplyModifiedPropertiesWithoutUndo();
                var drop = new SerializedObject(root.GetComponent<LootDropper>());
                drop.FindProperty("dropOrigin").objectReferenceValue = root.transform; drop.ApplyModifiedPropertiesWithoutUndo();
                BuildRagdoll(root, nodes);
                var presentation = new SerializedObject(root.AddComponent<BramblekinPresentation>());
                presentation.FindProperty("enemy").objectReferenceValue = root.GetComponent<Bramblekin>();
                foreach (var binding in new[] { ("body", "Body"), ("head", "Head"), ("leftArm", "Arm.L"),
                    ("rightArm", "Arm.R"), ("leftLeg", "Leg.L"), ("rightLeg", "Leg.R"),
                    ("leftShin", "Shin.L"), ("rightShin", "Shin.R"), ("leftFoot", "Foot.L"), ("rightFoot", "Foot.R") })
                    presentation.FindProperty(binding.Item1).objectReferenceValue = nodes[binding.Item2];
                foreach (string cue in Cues)
                {
                    var clips = presentation.FindProperty(char.ToLowerInvariant(cue[0]) + cue.Substring(1) + "Clips");
                    clips.arraySize = Takes(cue);
                    for (int i = 1; i <= Takes(cue); i++) clips.GetArrayElementAtIndex(i - 1).objectReferenceValue =
                        AssetDatabase.LoadAssetAtPath<AudioClip>(Folder + "/" + AudioName(cue, i));
                }
                presentation.ApplyModifiedPropertiesWithoutUndo();
                var health = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Duskborn/Prefabs/UI/WorldHealthBar.prefab"));
                health.transform.SetParent(root.transform, false);
                // Replacing a NetworkBehaviour requires rebuilding its serialized association.
                // Scope this to the basic enemy; do not regenerate every project prefab.
                var networkObject = root.GetComponent<FishNet.Object.NetworkObject>();
                networkObject.NetworkBehaviours.Clear();
                var behaviours = root.GetComponents<FishNet.Object.NetworkBehaviour>();
                for (int i = 0; i < behaviours.Length; i++)
                {
                    networkObject.NetworkBehaviours.Add(behaviours[i]);
                    var association = new SerializedObject(behaviours[i]);
                    association.FindProperty("_componentIndexCache").intValue = i;
                    association.FindProperty("_networkObjectCache").objectReferenceValue = networkObject;
                    association.FindProperty("_addedNetworkObject").objectReferenceValue = networkObject;
                    association.ApplyModifiedPropertiesWithoutUndo();
                }
                EditorUtility.SetDirty(networkObject);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var registry = AssetDatabase.LoadAssetAtPath<EnemyPrefabRegistry>("Assets/_Duskborn/ScriptableObjects/Enemies/EnemyPrefabRegistry.asset");
            Require(registry != null, "Missing enemy registry.");
            for (int i = 0; i < registry.Entries.Length; i++)
                if (registry.Entries[i].Type == EnemyType.Swarmer)
                    registry.Entries[i].Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent<Bramblekin>();
            EditorUtility.SetDirty(registry);
            foreach (string path in new[] { "Assets/DefaultPrefabObjects.asset", "Assets/_Duskborn/Network/DefaultPrefabObjects.asset" })
            {
                var collection = AssetDatabase.LoadAssetAtPath<SinglePrefabObjects>(path);
                Require(collection != null, "Missing network collection.");
                collection.AddObject(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent<FishNet.Object.NetworkObject>(), true, false);
                EditorUtility.SetDirty(collection);
            }
            AssetDatabase.SaveAssets(); BramblekinTests.RunAllTests();
        }
        private static void BuildRagdoll(GameObject root, System.Collections.Generic.Dictionary<string, Transform> nodes)
        {
            Rigidbody Body(string name, float mass)
            {
                var part = nodes[name]; var body = part.gameObject.AddComponent<Rigidbody>();
                body.mass = mass; body.isKinematic = true; body.useGravity = true;
                body.interpolation = RigidbodyInterpolation.None; body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                body.linearDamping = .3f; body.angularDamping = .8f;
                var bounds = part.GetComponent<MeshFilter>().sharedMesh.bounds;
                var collider = part.gameObject.AddComponent<BoxCollider>(); collider.center = bounds.center; collider.size = bounds.size;
                collider.enabled = false; return body;
            }
            var torso = Body("Body", 2);
            foreach (string name in new[] { "Head", "Arm.L", "Arm.R", "Leg.L", "Leg.R", "Shin.L", "Shin.R", "Foot.L", "Foot.R" })
            {
                var body = Body(name, name == "Head" ? 1 : .4f); var joint = body.gameObject.AddComponent<CharacterJoint>();
                joint.connectedBody = name.StartsWith("Shin.") ? nodes[name.Replace("Shin.", "Leg.")].GetComponent<Rigidbody>() :
                    name.StartsWith("Foot.") ? nodes[name.Replace("Foot.", "Shin.")].GetComponent<Rigidbody>() : torso;
                joint.anchor = Vector3.zero; joint.autoConfigureConnectedAnchor = true;
                joint.enableCollision = false; joint.enableProjection = true; joint.projectionDistance = .03f; joint.projectionAngle = 10;
                joint.lowTwistLimit = new SoftJointLimit { limit = -20 }; joint.highTwistLimit = new SoftJointLimit { limit = 20 };
                joint.swing1Limit = new SoftJointLimit { limit = 35 }; joint.swing2Limit = new SoftJointLimit { limit = 25 };
            }
            var ragdoll = new SerializedObject(root.GetComponent<EnemyRagdoll>());
            ragdoll.FindProperty("_animator").objectReferenceValue = null; ragdoll.FindProperty("_impulseScale").floatValue = 22f;
            ragdoll.FindProperty("_wholeBodyImpulseFraction").floatValue = .65f;
            ragdoll.FindProperty("_applyImpulseAtHitPoint").boolValue = true;
            ragdoll.FindProperty("_disableBoneCollidersWhileAlive").boolValue = true;
            ragdoll.FindProperty("_restorePoseOnReset").boolValue = true; ragdoll.FindProperty("_ignoreSelfCollisions").boolValue = true;
            ragdoll.FindProperty("_disableInterpolationWhileAlive").boolValue = true; ragdoll.ApplyModifiedPropertiesWithoutUndo();
        }
        public static void Require(bool valid, string message)
        { if (!valid) throw new InvalidOperationException("[Bramblekin] " + message); }
    }
}



