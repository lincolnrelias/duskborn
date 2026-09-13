using System;
using UnityEngine;
using UnityEditor;
using Duskborn.Gameplay.World;

namespace Duskborn.Editor
{
    public static class SpatialOccupancyMapTests
    {
        [MenuItem("Duskborn/Tests/Run Foliage & Resource Tests", false, 100)]
        public static void RunAllTests()
        {
            int passed = 0;
            int total = 0;

            RunTest(Test_SolidCollision, ref passed, ref total);
            RunTest(Test_CanopyInfluence, ref passed, ref total);
            RunTest(Test_Clearings, ref passed, ref total);
            RunTest(Test_CanPlacePropClearance, ref passed, ref total);
            RunTest(Test_ClusterSpacingSimulation, ref passed, ref total);
            RunTest(Test_FoliageCollisionRejection, ref passed, ref total);

            Debug.Log($"<color=#55FF55><b>[SpatialOccupancyMapTests] {passed}/{total} testes passaram com sucesso!</b></color>");
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

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception($"Assertion Failed: {message}");
            }
        }

        public static void Test_SolidCollision()
        {
            SpatialOccupancyMap map = new SpatialOccupancyMap(cellSize: 4f);
            map.Register(new Vector2(10f, 10f), solidRadius: 1.0f, canopyRadius: 0f, OccupancyType.Resource_Solid);

            // Dentro do obstáculo
            Assert(map.IsSolidOccupied(new Vector2(10f, 10f), clearanceRadius: 0f), "Centro do obstáculo deve estar ocupado.");
            Assert(map.IsSolidOccupied(new Vector2(10.5f, 10f), clearanceRadius: 0f), "Ponto a 0.5m deve estar ocupado para raio 1.0m.");
            Assert(map.IsSolidOccupied(new Vector2(10.9f, 10f), clearanceRadius: 0f), "Ponto a 0.9m deve estar ocupado para raio 1.0m.");

            // Imediatamente fora sem clearance
            Assert(!map.IsSolidOccupied(new Vector2(11.2f, 10f), clearanceRadius: 0f), "Ponto a 1.2m deve estar livre sem clearance.");

            // Com clearance de 0.3m (1.0m + 0.3m = 1.3m)
            Assert(map.IsSolidOccupied(new Vector2(11.2f, 10f), clearanceRadius: 0.3f), "Ponto a 1.2m deve estar ocupado com clearance de 0.3m.");

            // Distante
            Assert(!map.IsSolidOccupied(new Vector2(25f, 25f), clearanceRadius: 0.5f), "Ponto a 25,25 deve estar livre.");
        }

        public static void Test_CanopyInfluence()
        {
            SpatialOccupancyMap map = new SpatialOccupancyMap(cellSize: 6f);
            map.Register(new Vector2(20f, 20f), solidRadius: 0.8f, canopyRadius: 4.0f, OccupancyType.Resource_Solid);

            // Sob a copa
            bool underCanopy = map.IsUnderCanopy(new Vector2(21f, 20f), out float weight1);
            Assert(underCanopy, "Ponto a 1m do centro deve estar sob copa de 4m.");
            Assert(weight1 > 0.6f && weight1 <= 1.0f, $"Peso de copa a 1m deve ser alto, obtido: {weight1}");

            // Próximo à borda da copa (3.5m)
            underCanopy = map.IsUnderCanopy(new Vector2(20f, 23.5f), out float weight2);
            Assert(underCanopy, "Ponto a 3.5m deve estar sob copa de 4m.");
            Assert(weight2 > 0f && weight2 < weight1, "Peso na borda deve ser menor que no centro.");

            // Fora da copa (5m)
            underCanopy = map.IsUnderCanopy(new Vector2(20f, 25.5f), out float weight3);
            Assert(!underCanopy, "Ponto a 5.5m não deve estar sob a copa de 4m.");
            Assert(weight3 == 0f, "Peso fora da copa deve ser 0.");
        }

        public static void Test_Clearings()
        {
            SpatialOccupancyMap map = new SpatialOccupancyMap();
            map.RegisterClearing(Vector2.zero, radius: 10f, OccupancyType.Player_Sanctuary);
            map.RegisterClearing(new Vector2(60f, 60f), radius: 8f, OccupancyType.Combat_Clearing);

            // Santuário
            Assert(map.IsInClearing(Vector2.zero, out OccupancyType type1), "Centro deve estar em clareira.");
            Assert(type1 == OccupancyType.Player_Sanctuary, "Tipo deve ser Player_Sanctuary.");
            Assert(map.IsInClearing(new Vector2(7f, 0f)), "Ponto a 7m deve estar no Santuário.");
            Assert(!map.IsInClearing(new Vector2(12f, 0f)), "Ponto a 12m deve estar fora do Santuário.");

            // Clareira de combate
            Assert(map.IsInClearing(new Vector2(62f, 62f), out OccupancyType type2), "Ponto a 62,62 deve estar na arena de combate.");
            Assert(type2 == OccupancyType.Combat_Clearing, "Tipo deve ser Combat_Clearing.");

            // Área aberta intermediária
            Assert(!map.IsInClearing(new Vector2(30f, 30f)), "Ponto a 30,30 deve estar fora de qualquer clareira.");
        }

        public static void Test_CanPlacePropClearance()
        {
            SpatialOccupancyMap map = new SpatialOccupancyMap();
            map.RegisterClearing(Vector2.zero, radius: 10f, OccupancyType.Player_Sanctuary);
            map.Register(new Vector2(20f, 20f), solidRadius: 1.0f, canopyRadius: 4.0f, OccupancyType.Resource_Solid);

            // Não pode colocar prop dentro do santuário
            Assert(!map.CanPlaceProp(new Vector2(5f, 5f), solidRadius: 1.0f, requiredSpacing: 2.0f),
                "Não deve permitir prop dentro do santuário.");

            // Não pode colocar prop colado a outro prop existente (spacing violado)
            Assert(!map.CanPlaceProp(new Vector2(21.5f, 20f), solidRadius: 1.0f, requiredSpacing: 2.5f),
                "Não deve permitir prop dentro do raio de espaçamento.");

            // Pode colocar prop com distância suficiente
            Assert(map.CanPlaceProp(new Vector2(26f, 20f), solidRadius: 1.0f, requiredSpacing: 2.5f),
                "Deve permitir prop a 6m de distância com spacing de 2.5m.");
        }

        public static void Test_ClusterSpacingSimulation()
        {
            SpatialOccupancyMap map = new SpatialOccupancyMap();
            Vector2 clusterCenter = new Vector2(40f, 40f);
            float clusterRadius = 6.0f;
            float intraSpacing = 2.0f;
            int nodeCount = 5;

            var rng = new Core.SeededRNG(12345);
            int spawned = 0;

            for (int i = 0; i < nodeCount; i++)
            {
                for (int attempt = 0; attempt < 30; attempt++)
                {
                    float angle = rng.Range(0f, Mathf.PI * 2f);
                    float r = clusterRadius * Mathf.Sqrt(rng.Range(0.05f, 1f));
                    Vector2 candidate = clusterCenter + new Vector2(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r);

                    if (!map.IsSolidOccupied(candidate, intraSpacing))
                    {
                        map.Register(candidate, solidRadius: 0.8f, canopyRadius: 0f, OccupancyType.Resource_Solid);
                        spawned++;
                        break;
                    }
                }
            }

            Assert(spawned == nodeCount, $"Todos os {nodeCount} nós do cluster devem ter sido posicionados respeitando intraSpacing. Spawned: {spawned}");
        }

        public static void Test_FoliageCollisionRejection()
        {
            SpatialOccupancyMap map = new SpatialOccupancyMap();
            Vector2 treePos = new Vector2(30f, 30f);
            map.Register(treePos, solidRadius: 1.0f, canopyRadius: 4.0f, OccupancyType.Resource_Solid);

            // Ponto dentro do tronco da árvore
            Vector2 trunkPoint = treePos + new Vector2(0.3f, 0.2f);
            Assert(map.IsSolidOccupied(trunkPoint, clearanceRadius: 0.2f), "Folhagem dentro do tronco da árvore deve ser rejeitada.");

            // Ponto na zona de folhagem sob a copa (fora do tronco)
            Vector2 canopyPoint = treePos + new Vector2(2.5f, 0f);
            Assert(!map.IsSolidOccupied(canopyPoint, clearanceRadius: 0.2f), "Ponto a 2.5m deve ser livre de colisão sólida.");
            Assert(map.IsUnderCanopy(canopyPoint, out float canopyWeight), "Ponto a 2.5m deve ser reconhecido como sob a copa.");
            Assert(canopyWeight > 0f, "Peso da copa deve ser positivo sob a copa.");
        }
    }
}
