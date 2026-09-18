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
            Mesh grassMesh = FoliageMeshUtility.CreateGrassTuftMesh(bladeCount: 5, height: 0.95f, baseWidth: 0.16f);
            AssetDatabase.CreateAsset(grassMesh, $"{meshFolder}/Mesh_GrassTuft.asset");

            Mesh carpetMesh = FoliageMeshUtility.CreateDenseCarpetMesh(bladeCount: 8, height: 0.85f, baseWidth: 0.24f);
            AssetDatabase.CreateAsset(carpetMesh, $"{meshFolder}/Mesh_DenseCarpet.asset");

            Mesh lushMesh = FoliageMeshUtility.CreateLushGrassClumpMesh(bladeCount: 9, height: 1.30f, baseWidth: 0.22f);
            AssetDatabase.CreateAsset(lushMesh, $"{meshFolder}/Mesh_LushGrassClump.asset");

            Mesh prairieMesh = FoliageMeshUtility.CreatePrairieGrassMesh(bladeCount: 5, height: 0.70f, baseWidth: 0.14f);
            AssetDatabase.CreateAsset(prairieMesh, $"{meshFolder}/Mesh_PrairieGrass.asset");

            Mesh reedMesh = FoliageMeshUtility.CreateReedGrassMesh(bladeCount: 6, height: 1.45f, baseWidth: 0.12f);
            AssetDatabase.CreateAsset(reedMesh, $"{meshFolder}/Mesh_ReedGrass.asset");

            Mesh wildflowerMesh = FoliageMeshUtility.CreateWildflowerTuftMesh(bladeCount: 5, flowerCount: 3, height: 0.90f, flowerHeight: 1.15f);
            AssetDatabase.CreateAsset(wildflowerMesh, $"{meshFolder}/Mesh_WildflowerTuft.asset");

            Mesh bushMesh = FoliageMeshUtility.CreateBushMesh(lobes: 4, baseRadius: 0.65f, height: 1.0f);
            AssetDatabase.CreateAsset(bushMesh, $"{meshFolder}/Mesh_Bush.asset");

            Mesh floweringBushMesh = FoliageMeshUtility.CreateFloweringBushMesh(lobes: 4, flowerCount: 12, baseRadius: 0.65f, height: 1.0f);
            AssetDatabase.CreateAsset(floweringBushMesh, $"{meshFolder}/Mesh_FloweringBush.asset");

            Mesh fernMesh = FoliageMeshUtility.CreateFernBushMesh(frondCount: 7, radius: 0.85f, height: 0.65f);
            AssetDatabase.CreateAsset(fernMesh, $"{meshFolder}/Mesh_FernBush.asset");

            Mesh groundShrubMesh = FoliageMeshUtility.CreateGroundShrubMesh(lobes: 5, radius: 0.95f, height: 0.45f);
            AssetDatabase.CreateAsset(groundShrubMesh, $"{meshFolder}/Mesh_GroundShrub.asset");

            Mesh treeCanopyMesh = FoliageMeshUtility.CreateTreeCanopyMesh(radius: 2.2f, height: 3.5f);
            AssetDatabase.CreateAsset(treeCanopyMesh, $"{meshFolder}/Mesh_TreeCanopy.asset");

            // Novas Malhas: Ervas Daninhas Silvestres
            Mesh wildWeedMesh = FoliageMeshUtility.CreateWildWeedTuftMesh(leafCount: 6, radius: 0.48f, height: 0.42f);
            AssetDatabase.CreateAsset(wildWeedMesh, $"{meshFolder}/Mesh_WildWeedTuft.asset");

            Mesh broadleafMesh = FoliageMeshUtility.CreateBroadleafWeedMesh(leafCount: 5, radius: 0.42f, height: 0.22f);
            AssetDatabase.CreateAsset(broadleafMesh, $"{meshFolder}/Mesh_BroadleafWeed.asset");

            Mesh tallStalkMesh = FoliageMeshUtility.CreateTallStalkWeedMesh(stalkCount: 4, height: 1.35f, baseWidth: 0.10f);
            AssetDatabase.CreateAsset(tallStalkMesh, $"{meshFolder}/Mesh_TallStalkWeed.asset");

            Mesh cloverMesh = FoliageMeshUtility.CreateCloverPatchMesh(cloverCount: 7, radius: 0.38f);
            AssetDatabase.CreateAsset(cloverMesh, $"{meshFolder}/Mesh_CloverPatch.asset");

            // Novas Malhas: Margem Aquática
            Mesh cattailBedMesh = FoliageMeshUtility.CreateWaterCattailBedMesh(reedCount: 10, cattailCount: 3, radius: 0.65f, height: 1.55f);
            AssetDatabase.CreateAsset(cattailBedMesh, $"{meshFolder}/Mesh_WaterCattailBed.asset");

            // Novas Malhas: Pequenas Pedras e Seixos
            Mesh singlePebbleMesh = FoliageMeshUtility.CreateLittlePebbleMesh(radius: 0.18f, height: 0.12f);
            AssetDatabase.CreateAsset(singlePebbleMesh, $"{meshFolder}/Mesh_LittlePebble.asset");

            Mesh pebbleClusterMesh = FoliageMeshUtility.CreatePebbleClusterMesh(pebbleCount: 4, radius: 0.42f);
            AssetDatabase.CreateAsset(pebbleClusterMesh, $"{meshFolder}/Mesh_PebbleCluster.asset");

            Mesh riverStoneMesh = FoliageMeshUtility.CreateRiverStoneMesh(radiusX: 0.30f, radiusZ: 0.20f, height: 0.08f);
            AssetDatabase.CreateAsset(riverStoneMesh, $"{meshFolder}/Mesh_RiverStone.asset");

            Mesh screeRockMesh = FoliageMeshUtility.CreateScreeRockMesh(size: 0.26f);
            AssetDatabase.CreateAsset(screeRockMesh, $"{meshFolder}/Mesh_ScreeRock.asset");

            // Carrega Materiais
            Material grassMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Duskborn/Art/Materials/M_Foliage_Grass.mat");
            Material bushMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Duskborn/Art/Materials/M_Foliage_Bush.mat");
            Material treeCanopyMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Duskborn/Art/Materials/M_Foliage_TreeCanopy.mat");
            Material woodMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Duskborn/Art/Models/Wood.mat");

            // 2. Prefabs: Tufos de Grama e Ervas Daninhas
            SaveFoliagePrefab(grassMesh, grassMat, "Prefab_GrassTuft", prefabFolder);
            SaveFoliagePrefab(carpetMesh, grassMat, "Prefab_DenseCarpetGrass", prefabFolder);
            SaveFoliagePrefab(lushMesh, grassMat, "Prefab_LushGrassClump", prefabFolder);
            SaveFoliagePrefab(prairieMesh, grassMat, "Prefab_PrairieGrass", prefabFolder);
            SaveFoliagePrefab(reedMesh, grassMat, "Prefab_ReedGrass", prefabFolder);
            SaveFoliagePrefab(wildflowerMesh, grassMat, "Prefab_WildflowerTuft", prefabFolder);
            SaveFoliagePrefab(wildWeedMesh, grassMat, "Prefab_WildWeedTuft", prefabFolder);
            SaveFoliagePrefab(broadleafMesh, grassMat, "Prefab_BroadleafWeed", prefabFolder);
            SaveFoliagePrefab(tallStalkMesh, grassMat, "Prefab_TallStalkWeed", prefabFolder);
            SaveFoliagePrefab(cloverMesh, grassMat, "Prefab_CloverPatch", prefabFolder);
            SaveFoliagePrefab(cattailBedMesh, grassMat, "Prefab_WaterCattailBed", prefabFolder);

            // 3. Prefabs: Pequenas Pedras e Seixos
            SaveFoliagePrefab(singlePebbleMesh, grassMat, "Prefab_LittlePebble", prefabFolder);
            SaveFoliagePrefab(pebbleClusterMesh, grassMat, "Prefab_PebbleCluster", prefabFolder);
            SaveFoliagePrefab(riverStoneMesh, grassMat, "Prefab_RiverStone", prefabFolder);
            SaveFoliagePrefab(screeRockMesh, grassMat, "Prefab_ScreeRock", prefabFolder);

            // 4. Prefabs: Arbustos
            SaveFoliagePrefab(bushMesh, bushMat, "Prefab_StylizedBush", prefabFolder);
            SaveFoliagePrefab(floweringBushMesh, bushMat, "Prefab_FloweringBush", prefabFolder);
            SaveFoliagePrefab(fernMesh, bushMat, "Prefab_FernBush", prefabFolder);
            SaveFoliagePrefab(groundShrubMesh, bushMat, "Prefab_GroundShrub", prefabFolder);

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

        private static void SaveFoliagePrefab(Mesh mesh, Material material, string prefabName, string folder)
        {
            GameObject go = new GameObject(prefabName);
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mf.sharedMesh = mesh;
            mr.sharedMaterial = material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;

            string path = $"{folder}/{prefabName}.prefab";
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }
    }
}
