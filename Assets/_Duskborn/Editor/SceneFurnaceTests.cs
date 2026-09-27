using System;
using System.Reflection;
using Duskborn.Gameplay.Building;
using Duskborn.Gameplay.Crafting;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Duskborn.Editor
{
    public static class SceneFurnaceTests
    {
        [MenuItem("Duskborn/Tests/Scene Furnace Registration")]
        public static void Run()
        {
            if (Application.isPlaying || BuildingWorld.Instance != null)
                throw new InvalidOperationException("Run scene furnace tests outside a live session.");
            var previousScene = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Duskborn/Resources/Stations/Station_Forge.prefab");
                var forge = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                forge.transform.position = new Vector3(3, 2, 4);
                // FishNet can keep scene stations inactive until they spawn.
                forge.SetActive(false);
                forge.GetComponent<FishNet.Object.NetworkObject>().SetSceneId(12345);
                var originalPosition = forge.transform.position;
                var worldObject = new GameObject("Scene furnace test world");
                var world = worldObject.AddComponent<BuildingWorld>();
                Invoke(world, "Awake"); // Edit-mode CLI tests do not invoke MonoBehaviour Awake.
                var placed = forge.GetComponent<PlacedBuilding>();
                Check(placed != null && world.Buildings.Count == 1, "scene forge registered while inactive");
                Check(placed.State.instanceId.StartsWith("scene-forge:") && placed.State.instanceId.EndsWith(":" + forge.name), "scene hierarchy identity used for snapshots");
                Check(Vector3.Distance(originalPosition, forge.transform.position) < .0001f, "registration preserves location");
                Invoke(world, "RegisterSceneFurnaces");
                Check(world.Buildings.Count == 1, "registration is idempotent");
                Check(!(bool)Invoke(world, "HasChangedBuildings"), "untouched scene forge allows initial save loading");
                Check(!placed.IsProcessing, "new scene furnace is idle");
                var recipe = BuildingWorld.Recipe("Recipe_SmeltIronBar");
                placed.State.selectedRecipe = recipe.RecipeId;
                foreach (var ingredient in recipe.Ingredients) placed.AddInput(ingredient.material.Id, ingredient.amount);
                foreach (var fuel in recipe.FuelIngredients) placed.AddFuel(fuel.material.Id, fuel.amount);
                Check(placed.IsProcessing, "scene furnace becomes eligible with ore and fuel");
                placed.Tick(recipe.ProcessingSeconds / placed.Definition.processingSpeed);
                Check(placed.StoredCount == recipe.OutputAmount && placed.State.jobs.Count == 0, "registered furnace produces real recipe output");
                Check((bool)Invoke(world, "HasChangedBuildings"), "used scene furnace blocks initial save loading");
                var snapshot = JsonUtility.FromJson<BuildingSnapshot>(JsonUtility.ToJson(world.Capture()));
                world.ApplySnapshot(snapshot);
                Check(world.Buildings.Count == 1 && world.Buildings[placed.State.instanceId] == placed, "snapshot updates scene furnace without duplicating it");
                Check(!placed.IsProcessing, "finished furnace cools down");
                Debug.Log("[SceneFurnaceTests] 12 checks passed.");
            }
            finally
            {
                SceneManager.SetActiveScene(previousScene);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static object Invoke(BuildingWorld world, string method) => typeof(BuildingWorld)
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(world, null);
        private static void Check(bool value, string message)
        {
            if (!value) throw new Exception("[SceneFurnaceTests] " + message);
        }
    }
}
