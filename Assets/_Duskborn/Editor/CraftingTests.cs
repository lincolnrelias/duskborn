using System;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using Duskborn.Gameplay;
using Duskborn.Gameplay.Crafting;
using Duskborn.Gameplay.Equipment;
using Duskborn.Gameplay.Loot;
using Duskborn.UI;
using InventorySystem.Core;
using InventorySystem.Data;

namespace Duskborn.Editor
{
    /// <summary>
    /// Testes automatizados de validacao do sistema de crafting, bancada e interface movel.
    /// </summary>
    public static class CraftingTests
    {
        [MenuItem("Duskborn/Tests/Run Crafting Tests", false, 104)]
        public static void RunAllTests()
        {
            int passed = 0;
            int total = 0;

            RunTest(Test_StoneAxe_RecipeIntegrity, ref passed, ref total);
            RunTest(Test_StonePickaxe_RecipeIntegrity, ref passed, ref total);
            RunTest(Test_Recipe_CanCraft_Validation, ref passed, ref total);
            RunTest(Test_Recipe_SpendIngredients_Execution, ref passed, ref total);
            RunTest(Test_Recipe_CreateOutputItem, ref passed, ref total);
            RunTest(Test_Workbench_ProximityCalculation, ref passed, ref total);
            RunTest(Test_Workbench_PrefabModelIntegrity, ref passed, ref total);
            RunTest(Test_DraggablePanel_BindingAndCanvasResolution, ref passed, ref total);
            RunTest(Test_DraggablePanel_ScreenClampingMath, ref passed, ref total);

            Debug.Log($"<color=#55FF55><b>[CraftingTests] {passed}/{total} testes passaram com sucesso!</b></color>");
        }

        private static void RunTest(Action testMethod, ref int passed, ref int total)
        {
            total++;
            try
            {
                testMethod();
                passed++;
                Debug.Log($"[PASS] {testMethod.Method.Name}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FAIL] {testMethod.Method.Name}: {ex.Message}");
            }
        }

        private static void Test_StoneAxe_RecipeIntegrity()
        {
            var recipe = Resources.Load<CraftingRecipe>("Crafting/Recipe_StoneAxe");
            if (recipe == null)
                throw new Exception("Nao foi possivel carregar 'Crafting/Recipe_StoneAxe' via Resources.");

            if (recipe.OutputItem == null)
                throw new Exception("Recipe_StoneAxe nao possui OutputItem configurado.");

            if (recipe.OutputItem.Id != "stone_axe")
                throw new Exception($"OutputItem esperado 'stone_axe', mas encontrado '{recipe.OutputItem.Id}'.");

            if (recipe.Ingredients == null || recipe.Ingredients.Count != 2)
                throw new Exception("Recipe_StoneAxe deve conter exatamente 2 ingredientes (Madeira e Pedra).");

            var woodIng = FindIngredient(recipe, "wood");
            var stoneIng = FindIngredient(recipe, "stone");

            if (woodIng.amount != 5 || stoneIng.amount != 5)
                throw new Exception($"Quantidades invalidas de ingredientes: Wood={woodIng.amount}, Stone={stoneIng.amount}");

            var weaponDef = recipe.OutputItem as WeaponDefinition;
            if (weaponDef == null)
                throw new Exception("stone_axe deve ser do tipo WeaponDefinition.");

            float mult = TypeDamageModifier.GetBestMultiplier(weaponDef.TypeModifiers, TargetType.Tree);
            if (Mathf.Abs(mult - 4.0f) > 0.05f)
                throw new Exception($"stone_axe deveria ter multiplicador de dano 4.0x (base + 300%) contra Tree, mas tem {mult}x.");
        }

        private static void Test_StonePickaxe_RecipeIntegrity()
        {
            var recipe = Resources.Load<CraftingRecipe>("Crafting/Recipe_StonePickaxe");
            if (recipe == null)
                throw new Exception("Nao foi possivel carregar 'Crafting/Recipe_StonePickaxe' via Resources.");

            if (recipe.OutputItem == null)
                throw new Exception("Recipe_StonePickaxe nao possui OutputItem configurado.");

            if (recipe.OutputItem.Id != "stone_pickaxe")
                throw new Exception($"OutputItem esperado 'stone_pickaxe', mas encontrado '{recipe.OutputItem.Id}'.");

            if (recipe.Ingredients == null || recipe.Ingredients.Count != 2)
                throw new Exception("Recipe_StonePickaxe deve conter exatamente 2 ingredientes (Madeira e Pedra).");

            var woodIng = FindIngredient(recipe, "wood");
            var stoneIng = FindIngredient(recipe, "stone");

            if (woodIng.amount != 5 || stoneIng.amount != 5)
                throw new Exception($"Quantidades invalidas de ingredientes: Wood={woodIng.amount}, Stone={stoneIng.amount}");

            var weaponDef = recipe.OutputItem as WeaponDefinition;
            if (weaponDef == null)
                throw new Exception("stone_pickaxe deve ser do tipo WeaponDefinition.");

            float mult = TypeDamageModifier.GetBestMultiplier(weaponDef.TypeModifiers, TargetType.MiningNode);
            if (Mathf.Abs(mult - 4.0f) > 0.05f)
                throw new Exception($"stone_pickaxe deveria ter multiplicador de dano 4.0x (base + 300%) contra MiningNode, mas tem {mult}x.");
        }

        private static void Test_Recipe_CanCraft_Validation()
        {
            var recipe = ScriptableObject.CreateInstance<CraftingRecipe>();
            var woodMat = ScriptableObject.CreateInstance<MaterialDefinition>();
            SetPrivateField(woodMat, "id", "wood");
            var stoneMat = ScriptableObject.CreateInstance<MaterialDefinition>();
            SetPrivateField(stoneMat, "id", "stone");

            var ingList = new System.Collections.Generic.List<CraftingIngredient>
            {
                new CraftingIngredient { material = woodMat, amount = 10 },
                new CraftingIngredient { material = stoneMat, amount = 5 }
            };
            SetPrivateField(recipe, "ingredients", ingList);

            var go = new GameObject("Test_ResInv_Holder");
            var resInv = go.AddComponent<ResourceInventory>();
            resInv.Add("wood", 9);
            resInv.Add("stone", 10);

            if (recipe.CanCraft(resInv))
                throw new Exception("CanCraft deveria retornar false quando falta madeira (9/10).");

            resInv.Add("wood", 1);
            if (!recipe.CanCraft(resInv))
                throw new Exception("CanCraft deveria retornar true com 10 madeira e 10 pedra.");

            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(recipe);
            UnityEngine.Object.DestroyImmediate(woodMat);
            UnityEngine.Object.DestroyImmediate(stoneMat);
        }

        private static void Test_Recipe_SpendIngredients_Execution()
        {
            var recipe = ScriptableObject.CreateInstance<CraftingRecipe>();
            var woodMat = ScriptableObject.CreateInstance<MaterialDefinition>();
            SetPrivateField(woodMat, "id", "wood");
            var stoneMat = ScriptableObject.CreateInstance<MaterialDefinition>();
            SetPrivateField(stoneMat, "id", "stone");

            var ingList = new System.Collections.Generic.List<CraftingIngredient>
            {
                new CraftingIngredient { material = woodMat, amount = 5 },
                new CraftingIngredient { material = stoneMat, amount = 3 }
            };
            SetPrivateField(recipe, "ingredients", ingList);

            var go = new GameObject("Test_ResInv_Holder_2");
            var resInv = go.AddComponent<ResourceInventory>();
            resInv.Add("wood", 8);
            resInv.Add("stone", 2);

            bool success = recipe.TrySpendIngredients(resInv);
            if (success)
                throw new Exception("TrySpendIngredients nao deveria suceder com pedra insuficiente.");
            if (resInv.GetCount("wood") != 8)
                throw new Exception("TrySpendIngredients nao deveria deduzir recursos em caso de falha.");

            resInv.Add("stone", 5);
            success = recipe.TrySpendIngredients(resInv);
            if (!success)
                throw new Exception("TrySpendIngredients falhou com recursos suficientes.");

            if (resInv.GetCount("wood") != 3)
                throw new Exception($"Saldo esperado de madeira: 3, obtido: {resInv.GetCount("wood")}");
            if (resInv.GetCount("stone") != 4)
                throw new Exception($"Saldo esperado de pedra: 4, obtido: {resInv.GetCount("stone")}");

            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(recipe);
            UnityEngine.Object.DestroyImmediate(woodMat);
            UnityEngine.Object.DestroyImmediate(stoneMat);
        }

        private static void Test_Recipe_CreateOutputItem()
        {
            var recipe = Resources.Load<CraftingRecipe>("Crafting/Recipe_StoneAxe");
            if (recipe == null)
                throw new Exception("Nao foi possivel carregar Recipe_StoneAxe.");

            IInventoryItem item = recipe.CreateOutputItem();
            if (item == null)
                throw new Exception("CreateOutputItem retornou null.");

            if (item.Id != "stone_axe")
                throw new Exception($"Id do item criado incorreto: {item.Id}");
        }

        private static void Test_Workbench_ProximityCalculation()
        {
            var wbGo = new GameObject("Workbench_Test");
            wbGo.transform.position = new Vector3(10f, 0f, 10f);
            var wb = wbGo.AddComponent<Workbench>();

            Vector3 nearPos = new Vector3(12f, 0f, 10f);
            if (!wb.IsInRange(nearPos, 3.5f))
                throw new Exception("Workbench.IsInRange deveria retornar true para distancia 2.0m.");

            Vector3 farPos = new Vector3(15f, 0f, 10f);
            if (wb.IsInRange(farPos, 3.5f))
                throw new Exception("Workbench.IsInRange deveria retornar false para distancia 5.0m.");

            UnityEngine.Object.DestroyImmediate(wbGo);
        }

        private static void Test_Workbench_PrefabModelIntegrity()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Duskborn/Prefabs/World/Workbench.prefab");
            if (prefab == null)
                throw new Exception("Nao foi possivel carregar 'Assets/_Duskborn/Prefabs/World/Workbench.prefab'.");

            var mf = prefab.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null)
                throw new Exception("Workbench.prefab nao possui MeshFilter ou Mesh valido.");

            if (mf.sharedMesh.name != "Workbench")
                throw new Exception($"Mesh esperado 'Workbench', mas encontrado '{mf.sharedMesh.name}'.");

            var mr = prefab.GetComponent<MeshRenderer>();
            if (mr == null || mr.sharedMaterials == null || mr.sharedMaterials.Length < 3)
                throw new Exception("Workbench.prefab MeshRenderer deve possuir 3 materiais (Wood, Iron, Stone).");

            var col = prefab.GetComponent<BoxCollider>();
            if (col == null)
                throw new Exception("Workbench.prefab deve possuir um BoxCollider.");

            if (col.size.x < 1.0f || col.size.y < 0.8f || col.size.z < 0.8f)
                throw new Exception($"Dimensoes do BoxCollider invalidas: {col.size}");

            var wb = prefab.GetComponent<Workbench>();
            if (wb == null)
                throw new Exception("Workbench.prefab deve possuir o componente Workbench.");
        }

        private static void Test_DraggablePanel_BindingAndCanvasResolution()
        {
            // Cria um GameObject com Canvas
            var canvasGO = new GameObject("Test_Canvas", typeof(RectTransform), typeof(Canvas));
            var canvas = canvasGO.GetComponent<Canvas>();

            // Cria um painel filho simulando a janela de Crafting/Inventário
            var panelGO = new GameObject("Test_Panel", typeof(RectTransform), typeof(CanvasRenderer));
            var panelRect = panelGO.GetComponent<RectTransform>();

            // Adiciona DraggablePanel ANTES de setar o parent (reproduz o caso dinâmico de instanciação)
            var drag = panelGO.AddComponent<DraggablePanel>();

            // Verifica auto-vinculação do TargetPanel
            if (drag.TargetPanel != panelRect)
                throw new Exception("DraggablePanel.TargetPanel deveria retornar o próprio RectTransform quando não explicitado.");

            // Agora conecta ao Canvas
            panelGO.transform.SetParent(canvasGO.transform, false);

            // Verifica resolução preguiçosa (lazy) do Canvas
            var resolvedCanvas = drag.EnsureCanvas();
            if (resolvedCanvas != canvas)
                throw new Exception("DraggablePanel.EnsureCanvas() deveria encontrar o Canvas pai após SetParent.");

            UnityEngine.Object.DestroyImmediate(panelGO);
            UnityEngine.Object.DestroyImmediate(canvasGO);
        }

        private static void Test_DraggablePanel_ScreenClampingMath()
        {
            var parentGO = new GameObject("ParentCanvas", typeof(RectTransform));
            var parentRect = parentGO.GetComponent<RectTransform>();
            parentRect.sizeDelta = new Vector2(1920, 1080);
            parentRect.pivot = new Vector2(0.5f, 0.5f);

            var childGO = new GameObject("Window", typeof(RectTransform));
            childGO.transform.SetParent(parentGO.transform, false);
            var childRect = childGO.GetComponent<RectTransform>();
            childRect.sizeDelta = new Vector2(400, 500);
            childRect.pivot = new Vector2(0.5f, 0.5f);
            childRect.anchorMin = new Vector2(0.5f, 0.5f);
            childRect.anchorMax = new Vector2(0.5f, 0.5f);

            // Tenta mover a janela para muito além da borda direita (ex: x = 2000)
            Vector2 outOfBoundsPos = new Vector2(2000, 0);
            Vector2 clamped = DraggablePanel.ClampWithinParent(outOfBoundsPos, childRect, parentRect);

            // O limite direito máximo para pivô no centro (1920/2 - 400/2 = 960 - 200 = 760)
            float expectedMaxX = (1920f * 0.5f) - (400f * 0.5f);
            if (Mathf.Abs(clamped.x - expectedMaxX) > 1.0f)
                throw new Exception($"ClampWithinParent falhou no limite direito: esperado ~{expectedMaxX}, obtido {clamped.x}");

            // Tenta mover a janela para muito além da borda inferior (ex: y = -2000)
            Vector2 outOfBoundsBottom = new Vector2(0, -2000);
            Vector2 clampedBottom = DraggablePanel.ClampWithinParent(outOfBoundsBottom, childRect, parentRect);

            float expectedMinY = (-1080f * 0.5f) + (500f * 0.5f);
            if (Mathf.Abs(clampedBottom.y - expectedMinY) > 1.0f)
                throw new Exception($"ClampWithinParent falhou no limite inferior: esperado ~{expectedMinY}, obtido {clampedBottom.y}");

            UnityEngine.Object.DestroyImmediate(childGO);
            UnityEngine.Object.DestroyImmediate(parentGO);
        }

        private static CraftingIngredient FindIngredient(CraftingRecipe recipe, string materialId)
        {
            foreach (var ing in recipe.Ingredients)
            {
                if (ing.material != null && ing.material.Id == materialId)
                    return ing;
            }
            throw new Exception($"Ingrediente com id '{materialId}' nao encontrado na receita.");
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            if (field != null)
                field.SetValue(target, value);
        }
    }
}
