using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Duskborn.Gameplay.Player;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Duskborn.Editor
{
    /// <summary>Reproducible, non-interactive visual replacement; preserves prefab identity and gameplay.</summary>
    public static class IronrootPlayerBuilder
    {
        public const string Folder = "Assets/_Duskborn/Art/Models/Ironroot";
        public const string ModelPath = Folder + "/Ironroot.fbx";
        public static readonly string[] PlayerPaths = {
            "Assets/_Duskborn/Prefabs/Player/Player.prefab",
            "Assets/_Duskborn/Prefabs/Player/Player 1.prefab"
        };
        private static readonly Dictionary<string, Color> Colors = new Dictionary<string, Color> {
            {"Skin",new Color(.58f,.365f,.235f)}, {"Shirt",new Color(.115f,.145f,.18f)},
            {"Trousers",new Color(.245f,.222f,.177f)}, {"Leather",new Color(.135f,.077f,.043f)},
            {"Hair",new Color(.075f,.047f,.03f)}, {"Iron",new Color(.28f,.30f,.32f)},
            {"Ochre",new Color(.52f,.34f,.13f)}, {"Eyes",new Color(.68f,.65f,.54f)},
            {"Pupil",new Color(.038f,.042f,.038f)}, {"Lip",new Color(.31f,.155f,.105f)}
        };

        public static void Build()
        {
            var generator = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("FishNet.Editing.PrefabCollectionGenerator.Generator"))
                .FirstOrDefault(t => t != null);
            var suppress = generator?.GetField("IgnorePostProcess", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (suppress == null) throw new InvalidOperationException("FishNet prefab import guard unavailable.");
            bool previous = (bool)suppress.GetValue(null);
            try
            {
                suppress.SetValue(null, true);
                ImportModel();
                foreach (string path in PlayerPaths) ReplaceVisual(path);
                AssetDatabase.SaveAssets();
                Validate();
            }
            finally { suppress.SetValue(null, previous); }
            Debug.Log("[Ironroot] Player build succeeded.");
        }

        private static void ImportModel()
        {
            Directory.CreateDirectory(Folder);
            File.Copy("Artifacts/Ironroot/v002/Ironroot.fbx", ModelPath, true);
            AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.globalScale = 1;
            importer.useFileScale = true;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.optimizeGameObjects = false; // Equipment and hand attachments need exposed bones.
            importer.isReadable = true; // Offline baked-mesh validation; small hero mesh.
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            var human = new List<HumanBone>();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var boneNames = new HashSet<string>(model.GetComponentsInChildren<Transform>(true).Select(t => t.name));
            foreach (string name in HumanTrait.BoneName)
            {
                string bone = name.Replace(" ", "");
                if (boneNames.Contains(bone)) human.Add(new HumanBone { humanName = name, boneName = bone, limit = new HumanLimit { useDefaultValues = true } });
            }
            var description = importer.humanDescription;
            description.human = human.ToArray();
            description.skeleton = model.GetComponentsInChildren<Transform>(true).Select(t => new SkeletonBone {
                name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale
            }).ToArray();
            description.armStretch = .05f; description.legStretch = .05f;
            description.upperArmTwist = .5f; description.lowerArmTwist = .5f;
            description.upperLegTwist = .5f; description.lowerLegTwist = .5f;
            description.feetSpacing = 0; description.hasTranslationDoF = false;
            importer.humanDescription = description;
            foreach (var entry in Colors)
            {
                string name = "IR_" + entry.Key;
                string path = Folder + "/" + name + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                    AssetDatabase.CreateAsset(material, path);
                }
                material.SetColor("_BaseColor", entry.Value);
                material.SetFloat("_Smoothness", .12f);
                material.SetFloat("_Metallic", entry.Key == "Iron" ? .65f : 0);
                EditorUtility.SetDirty(material);
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), material);
            }
            importer.SaveAndReimport();
            var avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().SingleOrDefault();
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
                throw new InvalidOperationException("Ironroot did not import as a valid Humanoid avatar.");
        }

        private static void ReplaceVisual(string path)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var old = root.GetComponentInChildren<Animator>(true);
                if (old == null) throw new InvalidOperationException(path + " has no Animator to replace.");
                // Mesh revisions reimport through the same FBX GUID. Preserve the installed
                // prefab overrides, attachment sockets and external Animator references.
                if (old.name == "IronrootVisual" &&
                    AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(old.gameObject)) == ModelPath)
                {
                    Duskborn.Gameplay.Player.IronrootAppearance.EnsureAnimationEventReceiver(old);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    Debug.Log("[Ironroot] Refreshed model in existing visual: " + path);
                    return;
                }
                var controller = old.runtimeAnimatorController;
                if (controller == null) throw new InvalidOperationException(path + " has no locomotion controller.");
                var oldVisual = old.transform;
                while (oldVisual.parent != root.transform && oldVisual.parent != null) oldVisual = oldVisual.parent;
                if (oldVisual == root.transform) throw new InvalidOperationException("Refusing to replace gameplay root.");
                // Match the existing model's actual geometry, not its deliberately oversized culling bounds.
                Bounds oldBounds = GeometryBounds(oldVisual.gameObject, root.transform);
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.scene);
                visual.transform.SetParent(root.transform, false);
                visual.name = "IronrootVisual";
                var animator = visual.GetComponent<Animator>();
                if (animator == null) throw new InvalidOperationException("Imported humanoid has no Animator.");
                Duskborn.Gameplay.Player.IronrootAppearance.EnsureAnimationEventReceiver(animator);
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = old.cullingMode;
                animator.updateMode = old.updateMode;
                Bounds newBounds = GeometryBounds(visual, root.transform);
                float scale = oldBounds.size.y / newBounds.size.y;
                if (!float.IsFinite(scale) || scale < .5f || scale > 2f)
                    throw new InvalidOperationException("Unexpected player scale: " + scale);
                visual.transform.localScale = Vector3.one * scale;
                visual.transform.localPosition = new Vector3(0, oldBounds.min.y - newBounds.min.y * scale, 0);
                int visualLayer = old.GetComponentInChildren<SkinnedMeshRenderer>(true).gameObject.layer;
                foreach (var t in visual.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = visualLayer;
                visual.AddComponent<IronrootAppearance>();

                var replacements = new Dictionary<Object, Object> { { old, animator }, { oldVisual, visual.transform }, { oldVisual.gameObject, visual } };
                if (old.isHuman)
                    for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
                    {
                        var from = old.GetBoneTransform((HumanBodyBones)i);
                        var to = animator.GetBoneTransform((HumanBodyBones)i);
                        if (from != null && to != null) replacements[from] = to;
                    }

                foreach (var hand in new[] { HumanBodyBones.LeftHand, HumanBodyBones.RightHand })
                {
                    var to = animator.GetBoneTransform(hand);
                    var from = IronrootAppearance.EquipmentBone(old, hand);
                    if (to == null || from == null) continue;
                    var correction = new GameObject("EquipmentSocket").transform;
                    correction.SetParent(to, false);
                    correction.rotation = from.rotation;
                    correction.localScale = new Vector3(from.lossyScale.x / to.lossyScale.x,
                        from.lossyScale.y / to.lossyScale.y, from.lossyScale.z / to.lossyScale.z);
                    correction.gameObject.layer = to.gameObject.layer;
                }

                // Preserve the authored fallback weapon socket relative to the corrected new hand.
                // AttachmentProfile items continue using their existing humanoid-bone configuration.
                var handler = root.GetComponent<Duskborn.Gameplay.Equipment.PlayerWeaponHandler>();
                if (handler != null)
                {
                    var so = new SerializedObject(handler);
                    var hp = so.FindProperty("holdPoint");
                    var socket = hp.objectReferenceValue as Transform;
                    if (socket != null && socket.IsChildOf(oldVisual))
                    {
                        if (socket.name == "weaponHolder")
                        {
                            // Added prefab child must be detached from the old nested instance before destruction.
                            if (PrefabUtility.IsPartOfPrefabInstance(oldVisual))
                                PrefabUtility.UnpackPrefabInstance(oldVisual.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                            socket.SetParent(IronrootAppearance.EquipmentBone(animator, HumanBodyBones.RightHand), false);
                        }
                        else
                        {
                            hp.objectReferenceValue = IronrootAppearance.EquipmentBone(animator, HumanBodyBones.RightHand);
                        }
                    }
                    so.FindProperty("animator").objectReferenceValue = animator;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                // Replace serialized Animator/bone references throughout gameplay and network components.
                foreach (var component in root.GetComponentsInChildren<Component>(true))
                {
                    if (!(component is MonoBehaviour) || component.transform.IsChildOf(oldVisual) || component.transform.IsChildOf(visual.transform)) continue;
                    var so = new SerializedObject(component);
                    var iterator = so.GetIterator();
                    while (iterator.Next(true))
                        if (iterator.propertyType == SerializedPropertyType.ObjectReference && iterator.objectReferenceValue != null && replacements.TryGetValue(iterator.objectReferenceValue, out var replacement))
                            iterator.objectReferenceValue = replacement;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                Object.DestroyImmediate(oldVisual.gameObject);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[Ironroot] Replaced {path}: height {oldBounds.size.y:F3} m, scale {scale:F3}, feet {oldBounds.min.y:F3}.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        internal static Bounds GeometryBounds(GameObject root, Transform relativeTo)
        {
            Bounds bounds = default; bool first = true;
            foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var baked = new Mesh();
                try
                {
                    renderer.BakeMesh(baked);
                    foreach (var v in baked.vertices)
                    {
                        Vector3 p = relativeTo.InverseTransformPoint(renderer.transform.TransformPoint(v));
                        if (first) { bounds = new Bounds(p, Vector3.zero); first = false; }
                        else bounds.Encapsulate(p);
                    }
                }
                finally { Object.DestroyImmediate(baked); }
            }
            if (first) throw new InvalidOperationException("No skinned geometry found.");
            return bounds;
        }

        public static void Validate()
        {
            foreach (string path in PlayerPaths)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var animators = root.GetComponentsInChildren<Animator>(true);
                    if (animators.Length != 1 || !animators[0].isHuman || !animators[0].avatar.isValid)
                        throw new InvalidOperationException(path + " must contain exactly one valid Humanoid.");
                    var animator = animators[0];
                    if (animator.runtimeAnimatorController == null || animator.applyRootMotion)
                        throw new InvalidOperationException(path + " lost its controller/root-motion settings.");
                    foreach (var bn in new[] {HumanBodyBones.Head, HumanBodyBones.LeftHand, HumanBodyBones.RightHand, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot})
                        if (animator.GetBoneTransform(bn) == null) throw new InvalidOperationException("Missing bone: " + bn);
                    var renderers = animator.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                    if (renderers.Length != 6) throw new InvalidOperationException("Expected six modular skinned meshes.");
                    foreach (var r in renderers)
                    {
                        if (r.sharedMesh == null || r.bones.Any(b => b == null) || r.sharedMaterials.Any(m => m == null || m.shader.name != "Universal Render Pipeline/Lit"))
                            throw new InvalidOperationException("Invalid skin/material binding: " + r.name);
                        if (r.sharedMesh.boneWeights.Any(w => Mathf.Abs(w.weight0+w.weight1+w.weight2+w.weight3-1) > .002f))
                            throw new InvalidOperationException("Unnormalized weights: " + r.name);
                    }
                    foreach (var component in root.GetComponentsInChildren<Component>(true))
                        if (component == null) throw new InvalidOperationException("Missing script in " + path);
                    foreach (var component in root.GetComponents<MonoBehaviour>())
                    {
                        var serialized = new SerializedObject(component);
                        foreach (string field in new[] { "animator", "_animator" })
                        {
                            var property = serialized.FindProperty(field);
                            if (property != null && property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue != null && property.objectReferenceValue != animator)
                                throw new InvalidOperationException("Stale Animator reference: " + component.GetType().Name);
                        }
                    }
                    var weapon = root.GetComponent<Duskborn.Gameplay.Equipment.PlayerWeaponHandler>();
                    var hold = new SerializedObject(weapon).FindProperty("holdPoint").objectReferenceValue as Transform;
                    if (hold == null || !hold.IsChildOf(animator.transform)) throw new InvalidOperationException("Weapon socket is not attached to Ironroot.");
                    // Exercise real Humanoid clips offline, including locomotion and weapon action clips.
                    var clips = animator.runtimeAnimatorController.animationClips.Distinct().Where(c => c != null && c.isHumanMotion).ToArray();
                    if (clips.Length == 0) throw new InvalidOperationException("No Humanoid locomotion clips.");
                    foreach (var clip in clips)
                    {
                        clip.SampleAnimation(animator.gameObject, clip.length * .4f);
                        var b = GeometryBounds(animator.gameObject, root.transform);
                        if (!float.IsFinite(b.size.x) || b.size.magnitude > 8 || b.size.y < .3f)
                            throw new InvalidOperationException("Invalid deformation on " + clip.name);
                    }
                    Debug.Log($"[Ironroot] Validated {path}: {clips.Length} retargeted locomotion clips, six skins, valid references.");
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }
    }
}
