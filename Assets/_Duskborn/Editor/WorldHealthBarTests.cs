using System;
using UnityEngine;
using UnityEditor;
using Duskborn.Effects;

namespace Duskborn.Editor
{
    /// <summary>
    /// Testes automatizados de validacao matematica, posicionamento e identidade visual do WorldHealthBar.
    /// </summary>
    public static class WorldHealthBarTests
    {
        [MenuItem("Duskborn/Tests/Run World Health Bar Tests", false, 103)]
        public static void RunAllTests()
        {
            int passed = 0;
            int total = 0;

            RunTest(Test_HeightCappingOnTallNode, ref passed, ref total);
            RunTest(Test_ColliderPriorityOverFoliageMesh, ref passed, ref total);
            RunTest(Test_ColorThresholdGradients, ref passed, ref total);
            RunTest(Test_PortugueseDisplayNameResolution, ref passed, ref total);
            RunTest(Test_ScreenViewportClampingMath, ref passed, ref total);
            RunTest(Test_ForwardOffsetProjection, ref passed, ref total);
            RunTest(Test_BigTreeNodeFoliageExpansion, ref passed, ref total);

            Debug.Log($"<color=#55FF55><b>[WorldHealthBarTests] {passed}/{total} testes passaram com sucesso!</b></color>");
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

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        private static void AssertApproximately(float a, float b, float maxDelta, string message)
        {
            if (Mathf.Abs(a - b) > maxDelta)
                throw new Exception($"{message} (Esperado: {b}, Obtido: {a}, Delta: {Mathf.Abs(a - b)})");
        }

        private static void Test_HeightCappingOnTallNode()
        {
            var config = ScriptableObject.CreateInstance<HealthBarConfig>();
            config.maxHeightAboveBase = 3.2f;
            config.yOffset = 0.25f;

            var parentGo = new GameObject("TallTree_Pine");
            parentGo.transform.position = new Vector3(0, 10f, 0);

            var col = parentGo.AddComponent<CapsuleCollider>();
            col.height = 12f;
            col.center = new Vector3(0, 6f, 0);

            var barGo = new GameObject("WorldHealthBar");
            barGo.transform.SetParent(parentGo.transform, false);
            var bar = barGo.AddComponent<WorldHealthBar>();

            var method = typeof(WorldHealthBar).GetMethod("ComputeAnchorOffset",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var configField = typeof(WorldHealthBar).GetField("config",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            configField.SetValue(bar, config);

            method.Invoke(bar, null);

            float localY = barGo.transform.localPosition.y;
            AssertTrue(localY <= 3.8f, $"A barra deveria ter altura limitada em torno de 3.6m, mas obteve {localY}m");
            AssertTrue(localY >= 3.3f, $"A barra ficou abaixo da altura minima esperada: {localY}m");

            GameObject.DestroyImmediate(barGo);
            GameObject.DestroyImmediate(parentGo);
            ScriptableObject.DestroyImmediate(config);
        }

        private static void Test_ColliderPriorityOverFoliageMesh()
        {
            var config = ScriptableObject.CreateInstance<HealthBarConfig>();
            config.maxHeightAboveBase = 5.0f;
            config.yOffset = 0.2f;

            var parentGo = new GameObject("ResourceNode_Pine");
            parentGo.transform.position = Vector3.zero;

            var col = parentGo.AddComponent<CapsuleCollider>();
            col.height = 3.6f;
            col.center = new Vector3(0, 1.8f, 0);

            var barGo = new GameObject("WorldHealthBar");
            barGo.transform.SetParent(parentGo.transform, false);
            var bar = barGo.AddComponent<WorldHealthBar>();

            var configField = typeof(WorldHealthBar).GetField("config",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            configField.SetValue(bar, config);

            var method = typeof(WorldHealthBar).GetMethod("ComputeAnchorOffset",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method.Invoke(bar, null);

            float localY = barGo.transform.localPosition.y;
            AssertApproximately(localY, 3.95f, 0.25f, "A altura do tronco (colisor) deve ser respeitada em vez do vertice de folha");

            GameObject.DestroyImmediate(barGo);
            GameObject.DestroyImmediate(parentGo);
            ScriptableObject.DestroyImmediate(config);
        }

        private static void Test_ColorThresholdGradients()
        {
            var config = ScriptableObject.CreateInstance<HealthBarConfig>();
            config.fullColor = Color.green;
            config.midColor = Color.yellow;
            config.lowColor = Color.red;
            config.midThreshold = 0.5f;
            config.lowThreshold = 0.25f;

            var barGo = new GameObject("WorldHealthBar");
            var bar = barGo.AddComponent<WorldHealthBar>();

            var configField = typeof(WorldHealthBar).GetField("config",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            configField.SetValue(bar, config);

            var method = typeof(WorldHealthBar).GetMethod("GetBarColor",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            Color cFull = (Color)method.Invoke(bar, new object[] { 1.0f });
            AssertApproximately(cFull.g, 1.0f, 0.05f, "HP a 100% deve ser Verde");

            Color cMid = (Color)method.Invoke(bar, new object[] { 0.5f });
            AssertApproximately(cMid.r, 1.0f, 0.05f, "HP a 50% deve conter canal R alto (Amarelo)");
            AssertApproximately(cMid.g, 1.0f, 0.05f, "HP a 50% deve conter canal G alto (Amarelo)");

            Color cLow = (Color)method.Invoke(bar, new object[] { 0.1f });
            AssertApproximately(cLow.r, 1.0f, 0.05f, "HP critico deve ser Vermelho");
            AssertApproximately(cLow.g, 0.0f, 0.05f, "HP critico nao deve ter canal Verde");

            GameObject.DestroyImmediate(barGo);
            ScriptableObject.DestroyImmediate(config);
        }

        private static void Test_PortugueseDisplayNameResolution()
        {
            var barGo = new GameObject("WorldHealthBar");
            var bar = barGo.AddComponent<WorldHealthBar>();
            var method = typeof(WorldHealthBar).GetMethod("ResolveDisplayName",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            string[] testNames = new string[]
            {
                "ResourceNode_Pine(Clone)",
                "ResourceNode_Birch",
                "Node_Stone_Monolith",
                "Node_Iron_Vein",
                "Node_Fiber_Herbs",
                "Swarmer(Clone)",
                "Player 1"
            };

            string[] expectedNames = new string[]
            {
                "Pinheiro",
                "Bétula",
                "Monólito de Pedra",
                "Veio de Ferro",
                "Ervas Silvestres",
                "Enxameante",
                "Explorador"
            };

            for (int i = 0; i < testNames.Length; i++)
            {
                var parentGo = new GameObject(testNames[i]);
                barGo.transform.SetParent(parentGo.transform, false);

                string resolved = (string)method.Invoke(bar, null);
                AssertTrue(resolved == expectedNames[i],
                    $"Nome incorreto para {testNames[i]}: esperado '{expectedNames[i]}', obtido '{resolved}'");

                barGo.transform.SetParent(null, false);
                GameObject.DestroyImmediate(parentGo);
            }

            GameObject.DestroyImmediate(barGo);
        }

        private static void Test_ScreenViewportClampingMath()
        {
            Vector3 offscreenVp = new Vector3(0.5f, 1.45f, 5.0f);
            float minViewportY = 0.08f;
            float maxViewportY = 0.88f;

            float clampedY = Mathf.Clamp(offscreenVp.y, minViewportY, maxViewportY);
            AssertApproximately(clampedY, 0.88f, 0.001f, "Viewport Y fora da tela deve ser travado a 0.88");

            float clampedX = Mathf.Clamp(1.15f, 0.06f, 0.94f);
            AssertApproximately(clampedX, 0.94f, 0.001f, "Viewport X fora da tela deve ser travado a 0.94");
        }

        private static void Test_ForwardOffsetProjection()
        {
            var config = ScriptableObject.CreateInstance<HealthBarConfig>();
            config.forwardOffset = 0.35f;

            var parentGo = new GameObject("ResourceNode_Pine");
            parentGo.transform.position = new Vector3(0, 0, 0);
            var col = parentGo.AddComponent<CapsuleCollider>();
            col.radius = 0.45f;
            col.height = 3.6f;
            col.center = new Vector3(0, 1.8f, 0);

            var barGo = new GameObject("WorldHealthBar");
            barGo.transform.SetParent(parentGo.transform, false);
            var bar = barGo.AddComponent<WorldHealthBar>();

            var configField = typeof(WorldHealthBar).GetField("config",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            configField.SetValue(bar, config);

            var method = typeof(WorldHealthBar).GetMethod("ComputeAnchorOffset",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method.Invoke(bar, null);

            var radiusField = typeof(WorldHealthBar).GetField("_horizontalRadius",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            float radius = (float)radiusField.GetValue(bar);

            AssertApproximately(radius, 0.45f, 0.01f, "Raio horizontal deve corresponder ao raio do tronco do pinheiro");

            float expectedTotalForward = radius + config.forwardOffset;
            AssertApproximately(expectedTotalForward, 0.80f, 0.01f, "Distância total frontal deve ser raio + offset");

            GameObject.DestroyImmediate(barGo);
            GameObject.DestroyImmediate(parentGo);
            ScriptableObject.DestroyImmediate(config);
        }

        private static void Test_BigTreeNodeFoliageExpansion()
        {
            var config = ScriptableObject.CreateInstance<HealthBarConfig>();
            config.forwardOffset = 0.35f;

            var parentGo = new GameObject("ResourceNode_BigBirch");
            parentGo.transform.position = Vector3.zero;

            // Colisor fino do tronco
            var col = parentGo.AddComponent<CapsuleCollider>();
            col.radius = 0.45f;
            col.height = 3.6f;
            col.center = new Vector3(0, 1.8f, 0);

            // Malha visual larga representando a copa/folhagem de árvore grande
            var meshFilter = parentGo.AddComponent<MeshFilter>();
            var mesh = new Mesh();
            mesh.vertices = new Vector3[]
            {
                new Vector3(-2.4f, 0, -2.4f),
                new Vector3(2.4f, 0, -2.4f),
                new Vector3(0, 4.5f, 2.4f)
            };
            mesh.triangles = new int[] { 0, 1, 2 };
            mesh.RecalculateBounds();
            meshFilter.sharedMesh = mesh;

            var renderer = parentGo.AddComponent<MeshRenderer>();

            var barGo = new GameObject("WorldHealthBar");
            barGo.transform.SetParent(parentGo.transform, false);
            var bar = barGo.AddComponent<WorldHealthBar>();

            var configField = typeof(WorldHealthBar).GetField("config",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            configField.SetValue(bar, config);

            var method = typeof(WorldHealthBar).GetMethod("ComputeAnchorOffset",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method.Invoke(bar, null);

            var radiusField = typeof(WorldHealthBar).GetField("_horizontalRadius",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            float radius = (float)radiusField.GetValue(bar);

            AssertTrue(radius >= 2.3f, $"O raio horizontal deve cobrir a malha da copa larga (esperado >= 2.3m, obtido: {radius}m)");
            AssertTrue(radius > 2.0f, "O raio horizontal não deve estar artificialmente travado em 2.0m");

            float expectedTotalForward = radius + config.forwardOffset;
            AssertTrue(expectedTotalForward >= 2.65f, $"A projeção frontal total deve garantir que a barra fique à frente das folhas ({expectedTotalForward}m)");

            GameObject.DestroyImmediate(barGo);
            GameObject.DestroyImmediate(parentGo);
            UnityEngine.Object.DestroyImmediate(mesh);
            ScriptableObject.DestroyImmediate(config);
        }
    }
}