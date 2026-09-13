using System.IO;
using UnityEngine;
using UnityEditor;
using Duskborn.Gameplay.World.Foliage;

namespace Duskborn.Editor
{
    public static class FoliageAssetCreator
    {
        [MenuItem("Duskborn/Foliage/Generate Foliage Meshes and Prefabs", false, 40)]
        public static void GenerateFoliageAssets()
        {
            string meshFolder = "Assets/_Duskborn/Art/Meshes/Foliage";
            string prefabFolder = "Assets/_Duskborn/Prefabs/Foliage";

            if (!Directory.Exists(meshFolder))
            {
                Directory.CreateDirectory(meshFolder);
            }
            if (!Directory.Exists(prefabFolder))
            {
                Directory.CreateDirectory(prefabFolder);
            }

            AssetDatabase.Refresh();

            // 1. Gera e salva as Malhas .asset
            Mesh grassMesh = FoliageMeshUtility.CreateGrassTuftMesh(bladeCount: 6, height: 1.0f, baseWidth: 0.18f);
            string grassMeshPath = $"{meshFolder}/Mesh_GrassTuft.asset";
            AssetDatabase.CreateAsset(grassMesh, grassMeshPath);

            Mesh bushMesh = FoliageMeshUtility.CreateBushMesh(lobes: 4, baseRadius: 0.7f, height: 1.1f);
            string bushMeshPath = $"{meshFolder}/Mesh_Bush.asset";
            AssetDatabase.CreateAsset(bushMesh, bushMeshPath);

            Mesh treeCanopyMesh = FoliageMeshUtility.CreateTreeCanopyMesh(radius: 2.2f, height: 3.5f);
            string treeCanopyMeshPath = $"{meshFolder}/Mesh_TreeCanopy.asset";
            AssetDatabase.CreateAsset(treeCanopyMesh, treeCanopyMeshPath);

            // Carrega Materiais
            Material grassMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Duskborn/Art/Materials/M_Foliage_Grass.mat");
            Material bushMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Duskborn/Art/Materials/M_Foliage_Bush.mat");
            Material treeCanopyMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Duskborn/Art/Materials/M_Foliage_TreeCanopy.mat");
            Material woodMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Duskborn/Art/Models/Wood.mat");

            // 2. Prefab: Tufo de Grama
            GameObject grassGO = new GameObject("Prefab_GrassTuft");
            var mfGrass = grassGO.AddComponent<MeshFilter>();
            var mrGrass = grassGO.AddComponent<MeshRenderer>();
            mfGrass.sharedMesh = grassMesh;
            mrGrass.sharedMaterial = grassMat;
            mrGrass.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mrGrass.receiveShadows = true;

            string grassPrefabPath = $"{prefabFolder}/Prefab_GrassTuft.prefab";
            PrefabUtility.SaveAsPrefabAsset(grassGO, grassPrefabPath);
            Object.DestroyImmediate(grassGO);

            // 3. Prefab: Arbusto Estilizado
            GameObject bushGO = new GameObject("Prefab_StylizedBush");
            var mfBush = bushGO.AddComponent<MeshFilter>();
            var mrBush = bushGO.AddComponent<MeshRenderer>();
            mfBush.sharedMesh = bushMesh;
            mrBush.sharedMaterial = bushMat;
            mrBush.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mrBush.receiveShadows = true;

            string bushPrefabPath = $"{prefabFolder}/Prefab_StylizedBush.prefab";
            PrefabUtility.SaveAsPrefabAsset(bushGO, bushPrefabPath);
            Object.DestroyImmediate(bushGO);

            // 4. Prefab: Árvore Estilizada (Tronco + Copa com Shader de Folhagem)
            GameObject treeGO = new GameObject("Prefab_StylizedTree");
            
            // Tronco (Cilindro low-poly ou cubo esticado)
            GameObject trunkGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunkGO.name = "Trunk";
            trunkGO.transform.parent = treeGO.transform;
            trunkGO.transform.localPosition = new Vector3(0f, 1.35f, 0f);
            trunkGO.transform.localScale = new Vector3(0.42f, 1.4f, 0.42f);
            var mrTrunk = trunkGO.GetComponent<MeshRenderer>();
            if (woodMat != null) mrTrunk.sharedMaterial = woodMat;
            Object.DestroyImmediate(trunkGO.GetComponent<Collider>());

            // Copa Estilizada
            GameObject canopyGO = new GameObject("Canopy");
            canopyGO.transform.parent = treeGO.transform;
            canopyGO.transform.localPosition = new Vector3(0f, 2.4f, 0f);
            var mfCanopy = canopyGO.AddComponent<MeshFilter>();
            var mrCanopy = canopyGO.AddComponent<MeshRenderer>();
            mfCanopy.sharedMesh = treeCanopyMesh;
            mrCanopy.sharedMaterial = treeCanopyMat != null ? treeCanopyMat : bushMat;
            mrCanopy.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mrCanopy.receiveShadows = true;

            // Collider no tronco
            var col = treeGO.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 1.35f, 0f);
            col.radius = 0.35f;
            col.height = 2.8f;

            string treePrefabPath = $"{prefabFolder}/Prefab_StylizedTree.prefab";
            PrefabUtility.SaveAsPrefabAsset(treeGO, treePrefabPath);
            Object.DestroyImmediate(treeGO);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[FoliageAssetCreator] Folhagem criada com sucesso!\nMeshes em: {meshFolder}\nPrefabs em: {prefabFolder}");
        }
    }
}
