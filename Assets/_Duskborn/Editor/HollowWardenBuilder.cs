using System;
using System.IO;
using System.Linq;
using FishNet.Component.Transforming;
using FishNet.Object;
using FishNet.Managing.Object;
using GameKit.Dependencies.Utilities;
using Duskborn.Gameplay.Enemies;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Duskborn.Editor
{
    public static class HollowWardenBuilder
    {
        public const string ModelFolder = "Assets/_Duskborn/Art/Models/HollowWarden";
        public const string BossPath = "Assets/_Duskborn/Resources/Bosses/HollowWarden.prefab";
        public const string RootPath = "Assets/_Duskborn/Prefabs/Enemies/HollowWardenRoot.prefab";
        public const string ControllerPath = ModelFolder + "/HollowWarden.controller";
        // SampleScene's NetworkManager uses this collection, not Assets/DefaultPrefabObjects.asset.
        public const string GameplayPrefabsPath = "Assets/_Duskborn/Network/DefaultPrefabObjects.asset";
        private const string ModelPath = ModelFolder + "/HollowWarden.fbx";
        private static readonly string[] ClipNames = {"Idle","Walk","Spawn","Rootbreaker","HarvestSweep","RootPlant","Rooted","Stagger","Exposed","Recover","PhaseBreak","Death"};

        [MenuItem("Duskborn/Bosses/Build Hollow Warden")]
        public static void Build()
        {
            var generator = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a=>a.GetType("FishNet.Editing.PrefabCollectionGenerator.Generator"))
                .FirstOrDefault(t=>t!=null);
            var suppress = generator?.GetField("IgnorePostProcess",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static);
            if(suppress==null)throw new InvalidOperationException("FishNet prefab generation API unavailable.");
            bool previous=(bool)suppress.GetValue(null);
            try
            {
                // Atomic batch: multiple dependent prefab reimports in the same editor frame
                // otherwise trigger FishNet's repeated-import/save-error heuristic.
                suppress.SetValue(null,true);
                BuildAssets(generator);
            }
            finally { suppress.SetValue(null,previous); }
        }

        private static void BuildAssets(Type generator)
        {
            Directory.CreateDirectory(ModelFolder);
            Directory.CreateDirectory(Path.GetDirectoryName(BossPath));
            Directory.CreateDirectory(Path.GetDirectoryName(RootPath));
            File.Copy("Artifacts/HollowWarden/v002/HollowWarden.fbx", ModelPath, true);
            File.Copy("Artifacts/HollowWarden/v002/HW_Palette.png", ModelFolder+"/HW_Palette.png", true);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.isReadable = true; // Death fragments use the rigid bone weights in player builds.
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.importNormals = ModelImporterNormals.Import;
            var definitions = importer.defaultClipAnimations;
            foreach (var clip in definitions)
            {
                string name = clip.name.Substring(clip.name.LastIndexOf('|')+1);
                clip.name = name;
                clip.loopTime = name == "HW_Idle" || name == "HW_Walk" || name == "HW_Rooted" || name == "HW_Exposed";
                clip.loopPose = false;
                clip.keepOriginalOrientation = true;
                clip.keepOriginalPositionY = true;
                clip.keepOriginalPositionXZ = true;
                clip.lockRootRotation = true;
                clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = true;
            }
            importer.clipAnimations = definitions;
            importer.SaveAndReimport();
            var textureImporter = (TextureImporter)AssetImporter.GetAtPath(ModelFolder+"/HW_Palette.png");
            textureImporter.filterMode = FilterMode.Point;
            textureImporter.mipmapEnabled = false;
            textureImporter.textureCompression = TextureImporterCompression.Uncompressed;
            textureImporter.SaveAndReimport();

            var palette = MaterialAsset("HW_Palette", "Universal Render Pipeline/Lit");
            palette.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(ModelFolder+"/HW_Palette.png"));
            palette.SetColor("_BaseColor",Color.white);
            palette.SetFloat("_Smoothness",.05f);
            var amber = MaterialAsset("HW_Amber", "Universal Render Pipeline/Lit");
            amber.SetColor("_BaseColor",new Color(1,.48f,.025f));
            amber.EnableKeyword("_EMISSION");
            amber.SetColor("_EmissionColor",new Color(1,.26f,.015f)*2);
            var rootMaterial = MaterialAsset("HW_Root", "Universal Render Pipeline/Lit");
            rootMaterial.SetColor("_BaseColor",new Color(.29f,.18f,.075f));
            rootMaterial.SetFloat("_Smoothness",.05f);
            var warning = MaterialAsset("HW_Warning", "Universal Render Pipeline/Unlit");
            warning.SetFloat("_Surface",1);
            warning.SetFloat("_Blend",0);
            warning.SetFloat("_SrcBlend",(float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            warning.SetFloat("_DstBlend",(float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            warning.SetFloat("_ZWrite",0);
            warning.SetFloat("_Cull",0);
            warning.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            warning.renderQueue=3000;

            var clips = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>()
                .Where(c=>!c.name.StartsWith("__preview__")).ToDictionary(c=>c.name);
            foreach (var name in ClipNames) if (!clips.ContainsKey("HW_"+name)) throw new InvalidOperationException("Missing clip HW_"+name);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath)
                ?? AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var states = controller.layers[0].stateMachine;
            foreach (var state in states.states) states.RemoveState(state.state);
            controller.parameters = new[] { new AnimatorControllerParameter {name="Dead",type=AnimatorControllerParameterType.Bool} };
            foreach (var name in ClipNames)
            {
                var state=states.AddState("HW_"+name);
                state.motion=clips["HW_"+name];
                state.writeDefaultValues=true;
                if (name=="Idle") states.defaultState=state;
            }

            var rootGO = new GameObject("Hollow Warden Root");
            try
            {
                rootGO.layer=LayerMask.NameToLayer("Enemy");rootGO.tag="Enemy";
                rootGO.AddComponent<NetworkObject>().SetAssetPathHash(AssetHash(RootPath,rootGO.name));
                var agent=rootGO.AddComponent<NavMeshAgent>();agent.enabled=false;
                var collider=rootGO.AddComponent<CapsuleCollider>();collider.height=1.3f;collider.radius=.55f;collider.center=Vector3.up*.65f;
                var root=rootGO.AddComponent<HollowWardenRoot>();
                var visual=new GameObject("Root cluster",typeof(MeshFilter),typeof(MeshRenderer));visual.transform.SetParent(rootGO.transform,false);
                visual.GetComponent<MeshFilter>().sharedMesh=RootMesh();
                visual.GetComponent<MeshRenderer>().sharedMaterial=rootMaterial;
                EnemyStats(root,35,0,0);
                PrefabUtility.SaveAsPrefabAsset(rootGO,RootPath);
            }
            finally { Object.DestroyImmediate(rootGO); }

            var bossGO=new GameObject("Hollow Warden");
            try
            {
                bossGO.layer=LayerMask.NameToLayer("Enemy");bossGO.tag="Enemy";
                bossGO.AddComponent<NetworkObject>().SetAssetPathHash(AssetHash(BossPath,bossGO.name));
                var networkTransform=bossGO.AddComponent<NetworkTransform>();
                var networkSettings=new SerializedObject(networkTransform);
                networkSettings.FindProperty("_clientAuthoritative").boolValue=false;
                networkSettings.ApplyModifiedPropertiesWithoutUndo();
                var agent=bossGO.AddComponent<NavMeshAgent>();
                agent.radius=1;agent.height=4.1f;agent.speed=2.3f;agent.acceleration=6;agent.stoppingDistance=.3f;agent.enabled=false;
                var collider=bossGO.AddComponent<CapsuleCollider>();collider.height=3.7f;collider.radius=1;collider.center=Vector3.up*1.85f;
                var boss=bossGO.AddComponent<HollowWardenBoss>();
                var visual=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
                visual.name="Visual";visual.transform.SetParent(bossGO.transform,false);
                // FBX -Z forward becomes the character's +Z facing in Unity's left-handed coordinates.
                visual.transform.localRotation=Quaternion.identity;
                var animator=visual.GetComponent<Animator>() ?? visual.AddComponent<Animator>();
                animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;
                animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                foreach (var renderer in visual.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if (renderer.sharedMesh.subMeshCount!=2) throw new InvalidOperationException("Expected palette + amber submeshes.");
                    renderer.sharedMaterials=new[]{palette,amber};
                    renderer.updateWhenOffscreen=true;
                }
                EnemyStats(boss,900,18,2.3f);
                var bossSettings=new SerializedObject(boss);
                bossSettings.FindProperty("_animator").objectReferenceValue=animator;
                bossSettings.FindProperty("rootPrefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>(RootPath).GetComponent<HollowWardenRoot>();
                bossSettings.ApplyModifiedPropertiesWithoutUndo();

                var presentation=bossGO.AddComponent<HollowWardenPresentation>();
                var right=visual.GetComponentsInChildren<Transform>().Single(t=>t.name=="Plate.R");
                var left=visual.GetComponentsInChildren<Transform>().Single(t=>t.name=="Plate.L");
                clips["HW_PhaseBreak"].SampleAnimation(visual,clips["HW_PhaseBreak"].length);
                Quaternion openR=right.localRotation, openL=left.localRotation;
                clips["HW_Idle"].SampleAnimation(visual,0);
                var canvas=BuildHud(bossGO.transform,out var health,out var label);
                var settings=new SerializedObject(presentation);
                SetRef(settings,"boss",boss);SetRef(settings,"animator",animator);SetRef(settings,"telegraphMaterial",warning);
                SetRef(settings,"hud",canvas);SetRef(settings,"healthFill",health);SetRef(settings,"statusLabel",label);
                SetRef(settings,"rightPlate",right);SetRef(settings,"leftPlate",left);
                settings.FindProperty("openRight").quaternionValue=openR;settings.FindProperty("openLeft").quaternionValue=openL;
                settings.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(bossGO,BossPath);
            }
            finally { Object.DestroyImmediate(bossGO); }

            foreach (var asset in new Object[]{palette,amber,rootMaterial,warning,controller}) EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            // This installed FishNet version exposes the refresh method on an internal type.
            // Invoke the package's own generator so path hashes/IDs follow its normal rules.
            var refresh=generator?.GetMethod("GenerateFull",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static);
            if(refresh==null)throw new InvalidOperationException("FishNet prefab refresh entry point unavailable.");
            refresh.Invoke(null,new object[]{null,true,true});
            var gameplayPrefabs = AssetDatabase.LoadAssetAtPath<SinglePrefabObjects>(GameplayPrefabsPath);
            if (gameplayPrefabs == null) throw new InvalidOperationException("Gameplay network prefab collection missing.");
            foreach (var path in new[] { BossPath, RootPath })
                gameplayPrefabs.AddObject(AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<NetworkObject>(),
                    checkForDuplicates: true, initializeAdded: false);
            EditorUtility.SetDirty(gameplayPrefabs);
            AssetDatabase.SaveAssets();
            ValidateAssets();
            Debug.Log("[HollowWarden] Build succeeded.");
        }

        private static void SetRef(SerializedObject owner,string name,Object value) => owner.FindProperty(name).objectReferenceValue=value;

        private static ulong AssetHash(string path,string objectName)
        {
            // Match DefaultPrefabObjects.SetAssetPathHashes before saving replacement prefabs.
            // Otherwise FishNet's incremental import sees both replacement hashes as zero.
            string clean=new string((path+objectName).Trim().ToLowerInvariant()
                .Where(c=>(c>='a' && c<='z') || (c>='0' && c<='9')).ToArray());
            return clean.GetStableHashU64();
        }

        private static void EnemyStats(EnemyBase enemy,float hp,float damage,float speed)
        {
            var settings=new SerializedObject(enemy);
            settings.FindProperty("_entity.maxHP").floatValue=hp;
            settings.FindProperty("_entity.damage").floatValue=damage;
            settings.FindProperty("_entity.moveSpeed").floatValue=speed;
            settings.FindProperty("playerLayer").intValue=1<<LayerMask.NameToLayer("Player");
            settings.FindProperty("creatureTypes").intValue=(int)Duskborn.Gameplay.TargetType.Beast;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Material MaterialAsset(string name,string shaderName)
        {
            string path=ModelFolder+"/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material==null)
            {
                var shader=Shader.Find(shaderName);
                if (shader==null) throw new InvalidOperationException("Missing shader "+shaderName);
                material=new Material(shader){name=name};AssetDatabase.CreateAsset(material,path);
            }
            return material;
        }

        private static Mesh RootMesh()
        {
            string path=ModelFolder+"/RootCluster.asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh!=null) return mesh;
            var vertices=new System.Collections.Generic.List<Vector3>();
            var triangles=new System.Collections.Generic.List<int>();
            for (int root=0;root<3;root++)
            {
                float x=(root-1)*.32f;int start=vertices.Count;
                for (int i=0;i<5;i++) {float a=i*Mathf.PI*2/5;vertices.Add(new Vector3(x+Mathf.Cos(a)*.27f,0,Mathf.Sin(a)*.27f));}
                vertices.Add(new Vector3(x*.7f,root==1?1.3f:.9f,0));
                for (int i=0;i<5;i++){triangles.Add(start+i);triangles.Add(start+5);triangles.Add(start+(i+1)%5);}
                for (int i=1;i<4;i++){triangles.Add(start);triangles.Add(start+i);triangles.Add(start+i+1);}
            }
            // Duplicate triangle vertices for the same deliberately faceted style as the boss.
            var flat=triangles.Select(i=>vertices[i]).ToArray();
            mesh=new Mesh{name="Hollow Warden Roots"};mesh.vertices=flat;mesh.triangles=Enumerable.Range(0,flat.Length).ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh,path);return mesh;
        }

        private static Canvas BuildHud(Transform parent,out Image health,out TextMeshProUGUI label)
        {
            var go=new GameObject("Boss HUD",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
            go.transform.SetParent(parent,false);var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=25;
            var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            var panel=new GameObject("Boss health",typeof(RectTransform),typeof(Image));panel.transform.SetParent(go.transform,false);
            var rect=panel.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(.5f,1);rect.pivot=new Vector2(.5f,1);rect.anchoredPosition=new Vector2(0,-65);rect.sizeDelta=new Vector2(520,18);
            panel.GetComponent<Image>().color=new Color(.10f,.08f,.055f,.95f);panel.GetComponent<Image>().raycastTarget=false;
            var fill=new GameObject("Health",typeof(RectTransform),typeof(Image));fill.transform.SetParent(panel.transform,false);
            var fr=fill.GetComponent<RectTransform>();fr.anchorMin=Vector2.zero;fr.anchorMax=Vector2.one;fr.offsetMin=new Vector2(3,3);fr.offsetMax=new Vector2(-3,-3);
            health=fill.GetComponent<Image>();health.color=new Color(.82f,.45f,.12f);health.raycastTarget=false;
            health.sprite=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");health.type=Image.Type.Filled;health.fillMethod=Image.FillMethod.Horizontal;health.fillOrigin=0;
            var text=new GameObject("Status",typeof(RectTransform),typeof(TextMeshProUGUI));text.transform.SetParent(panel.transform,false);
            var tr=text.GetComponent<RectTransform>();tr.anchorMin=tr.anchorMax=new Vector2(.5f,1);tr.pivot=new Vector2(.5f,0);tr.anchoredPosition=new Vector2(0,6);tr.sizeDelta=new Vector2(620,34);
            label=text.GetComponent<TextMeshProUGUI>();label.text="Hollow Warden";label.fontSize=24;label.alignment=TextAlignmentOptions.Center;label.color=new Color(1,.83f,.53f);label.raycastTarget=false;
            if (TMP_Settings.defaultFontAsset!=null) label.font=TMP_Settings.defaultFontAsset;
            return canvas;
        }

        public static void ValidateAssets()
        {
            var boss=AssetDatabase.LoadAssetAtPath<GameObject>(BossPath);
            var root=AssetDatabase.LoadAssetAtPath<GameObject>(RootPath);
            if (boss==null || root==null) throw new InvalidOperationException("Warden prefabs missing; run build-warden.");
            if (boss.GetComponent<HollowWardenBoss>()==null || boss.GetComponent<HollowWardenPresentation>()==null || boss.GetComponent<NetworkTransform>()==null)
                throw new InvalidOperationException("Boss components missing.");
            var animator=boss.GetComponentInChildren<Animator>();
            if (animator==null || animator.runtimeAnimatorController==null || animator.applyRootMotion)
                throw new InvalidOperationException("Invalid boss Animator.");
            if (animator.runtimeAnimatorController.animationClips.Length!=12) throw new InvalidOperationException("Expected twelve Animator clips.");
            var settings=new SerializedObject(boss.GetComponent<HollowWardenBoss>());
            if (settings.FindProperty("rootPrefab").objectReferenceValue!=root.GetComponent<HollowWardenRoot>()) throw new InvalidOperationException("Root reference missing.");
            var collection=AssetDatabase.LoadAssetAtPath<SinglePrefabObjects>(GameplayPrefabsPath);
            if (collection==null) throw new InvalidOperationException("Gameplay network prefab collection missing.");
            foreach (var prefab in new[]{boss,root})
            {
                if (prefab.GetComponent<NetworkObject>().AssetPathHash==0) throw new InvalidOperationException("Missing prefab path hash: "+prefab.name);
                bool registered=Enumerable.Range(0,collection.GetObjectCount()).Any(i=>collection.GetObject(true,i)==prefab.GetComponent<NetworkObject>());
                if (!registered) throw new InvalidOperationException("Network prefab missing from gameplay collection " + GameplayPrefabsPath + ": " + prefab.name);
            }
        }
    }
}
