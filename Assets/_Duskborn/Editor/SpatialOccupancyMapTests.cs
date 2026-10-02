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
            RunTest(Test_FoliageArchetypeMeshes, ref passed, ref total);
            RunTest(Test_FoliageDensityPresets, ref passed, ref total);
            RunTest(Test_WildflowerPaletteVariety, ref passed, ref total);
            RunTest(Test_NewFoliageArchetypes, ref passed, ref total);
            RunTest(Test_RockArchetypeMeshes, ref passed, ref total);
            RunTest(Test_WeedAndWaterArchetypeMeshes, ref passed, ref total);
            RunTest(Test_GenerationBudget, ref passed, ref total);

            Debug.Log($"<color=#55FF55><b>[SpatialOccupancyMapTests] {passed}/{total} tests passed!</b></color>");
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

            // Inside the obstacle.
            Assert(map.IsSolidOccupied(new Vector2(10f, 10f), clearanceRadius: 0f), "The obstacle center must be occupied.");
            Assert(map.IsSolidOccupied(new Vector2(10.5f, 10f), clearanceRadius: 0f), "A point at 0.5m must be occupied for radius 1.0m.");
            Assert(map.IsSolidOccupied(new Vector2(10.9f, 10f), clearanceRadius: 0f), "A point at 0.9m must be occupied for radius 1.0m.");

            // Immediately outside without clearance.
            Assert(!map.IsSolidOccupied(new Vector2(11.2f, 10f), clearanceRadius: 0f), "A point at 1.2m must be free without clearance.");

            // With 0.3m clearance (1.0m + 0.3m = 1.3m).
            Assert(map.IsSolidOccupied(new Vector2(11.2f, 10f), clearanceRadius: 0.3f), "A point at 1.2m must be occupied with 0.3m clearance.");

            // Distant
            Assert(!map.IsSolidOccupied(new Vector2(25f, 25f), clearanceRadius: 0.5f), "The point at 25,25 must be free.");
        }

        public static void Test_CanopyInfluence()
        {
            SpatialOccupancyMap map = new SpatialOccupancyMap(cellSize: 6f);
            map.Register(new Vector2(20f, 20f), solidRadius: 0.8f, canopyRadius: 4.0f, OccupancyType.Resource_Solid);

            // Under the canopy
            bool underCanopy = map.IsUnderCanopy(new Vector2(21f, 20f), out float weight1);
            Assert(underCanopy, "A point 1m from the center must be under a 4m canopy.");
            Assert(weight1 > 0.6f && weight1 <= 1.0f, $"Canopy weight at 1m must be high, got: {weight1}");

            // Near the canopy edge (3.5m).
            underCanopy = map.IsUnderCanopy(new Vector2(20f, 23.5f), out float weight2);
            Assert(underCanopy, "A point at 3.5m must be under a 4m canopy.");
            Assert(weight2 > 0f && weight2 < weight1, "Weight at the edge must be lower than at the center.");

            // Outside the canopy (5m).
            underCanopy = map.IsUnderCanopy(new Vector2(20f, 25.5f), out float weight3);
            Assert(!underCanopy, "A point at 5.5m must not be under a 4m canopy.");
            Assert(weight3 == 0f, "Weight outside the canopy must be 0.");
        }

        public static void Test_Clearings()
        {
            SpatialOccupancyMap map = new SpatialOccupancyMap();
            map.RegisterClearing(Vector2.zero, radius: 10f, OccupancyType.Player_Sanctuary);
            map.RegisterClearing(new Vector2(60f, 60f), radius: 8f, OccupancyType.Combat_Clearing);

            // Sanctuary.
            Assert(map.IsInClearing(Vector2.zero, out OccupancyType type1), "The center must be in a clearing.");
            Assert(type1 == OccupancyType.Player_Sanctuary, "Type must be Player_Sanctuary.");
            Assert(map.IsInClearing(new Vector2(7f, 0f)), "A point at 7m must be in the Sanctuary.");
            Assert(!map.IsInClearing(new Vector2(12f, 0f)), "A point at 12m must be outside the Sanctuary.");

            // Combat clearing.
            Assert(map.IsInClearing(new Vector2(62f, 62f), out OccupancyType type2), "The point at 62,62 must be in the combat arena.");
            Assert(type2 == OccupancyType.Combat_Clearing, "Type must be Combat_Clearing.");

            // Intermediate open area.
            Assert(!map.IsInClearing(new Vector2(30f, 30f)), "The point at 30,30 must be outside any clearing.");
        }

        public static void Test_CanPlacePropClearance()
        {
            SpatialOccupancyMap map = new SpatialOccupancyMap();
            map.RegisterClearing(Vector2.zero, radius: 10f, OccupancyType.Player_Sanctuary);
            map.Register(new Vector2(20f, 20f), solidRadius: 1.0f, canopyRadius: 4.0f, OccupancyType.Resource_Solid);

            // Cannot place a prop inside the sanctuary.
            Assert(!map.CanPlaceProp(new Vector2(5f, 5f), solidRadius: 1.0f, requiredSpacing: 2.0f),
                "A prop must not be allowed inside the sanctuary.");

            // Cannot place a prop too close to another existing prop (spacing violation).
            Assert(!map.CanPlaceProp(new Vector2(21.5f, 20f), solidRadius: 1.0f, requiredSpacing: 2.5f),
                "A prop must not be allowed inside the spacing radius.");

            // Can place a prop with sufficient distance.
            Assert(map.CanPlaceProp(new Vector2(26f, 20f), solidRadius: 1.0f, requiredSpacing: 2.5f),
                "A prop must be allowed 6m away with 2.5m spacing.");
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

            Assert(spawned == nodeCount, $"All {nodeCount} cluster nodes must be placed respecting intraSpacing. Spawned: {spawned}");
        }

        public static void Test_FoliageCollisionRejection()
        {
            SpatialOccupancyMap map = new SpatialOccupancyMap();
            Vector2 treePos = new Vector2(30f, 30f);
            map.Register(treePos, solidRadius: 1.0f, canopyRadius: 4.0f, OccupancyType.Resource_Solid);

            // Point inside the tree trunk.
            Vector2 trunkPoint = treePos + new Vector2(0.3f, 0.2f);
            Assert(map.IsSolidOccupied(trunkPoint, clearanceRadius: 0.2f), "Foliage inside the tree trunk must be rejected.");

            // Point in the foliage zone beneath the canopy (outside the trunk).
            Vector2 canopyPoint = treePos + new Vector2(2.5f, 0f);
            Assert(!map.IsSolidOccupied(canopyPoint, clearanceRadius: 0.2f), "A point at 2.5m must be free of solid collision.");
            Assert(map.IsUnderCanopy(canopyPoint, out float canopyWeight), "A point at 2.5m must be recognized as under the canopy.");
            Assert(canopyWeight > 0f, "Canopy weight must be positive under the canopy.");
        }

        public static void Test_FoliageArchetypeMeshes()
        {
            // 1. Lush Clump
            Mesh lushMesh = Gameplay.World.Foliage.FoliageMeshUtility.CreateLushGrassClumpMesh(bladeCount: 9);
            Assert(lushMesh != null, "LushGrassClumpMesh must not be null.");
            Assert(lushMesh.vertexCount > 35, $"LushGrassClump must have at least 36 vertices. Got: {lushMesh.vertexCount}");
            Assert(lushMesh.triangles.Length > 0, "LushGrassClump must contain triangles.");

            // 2. Wildflower Tuft
            Mesh flowerMesh = Gameplay.World.Foliage.FoliageMeshUtility.CreateWildflowerTuftMesh(bladeCount: 5, flowerCount: 3);
            Assert(flowerMesh != null, "WildflowerTuftMesh must not be null.");
            Assert(flowerMesh.vertexCount > 30, $"WildflowerTuft must have at least 31 vertices. Got: {flowerMesh.vertexCount}");

            // Check for petal vertices with uv.x >= 2.0f.
            Vector2[] uvs = flowerMesh.uv;
            int petalVertices = 0;
            for (int i = 0; i < uvs.Length; i++)
            {
                if (uvs[i].x >= 2.0f) petalVertices++;
            }
            Assert(petalVertices >= 12, $"WildflowerTuft must have at least 12 petal vertices with UV.x >= 2.0f. Got: {petalVertices}");

            // 3. Fern Bush
            Mesh fernMesh = Gameplay.World.Foliage.FoliageMeshUtility.CreateFernBushMesh(frondCount: 7);
            Assert(fernMesh != null, "FernBushMesh must not be null.");
            Assert(fernMesh.vertexCount >= 35, $"FernBush must have at least 35 vertices. Got: {fernMesh.vertexCount}");
        }

        public static void Test_FoliageDensityPresets()
        {
            GameObject testGo = new GameObject("TestFoliagePlacer");
            try
            {
                // Add a dummy TerrainChunk to satisfy RequireComponent.
                testGo.AddComponent<TerrainChunk>();
                var placer = testGo.AddComponent<Gameplay.World.Foliage.ChunkFoliagePlacer>();

                // High (Default).
                placer.SetDensityPreset(Gameplay.World.Foliage.FoliageDensityPreset.High);
                Assert(placer.GrassTuftsPerChunk == 1800, $"High preset must have 1800 grass tufts. Got: {placer.GrassTuftsPerChunk}");
                Assert(placer.LittleRocksPerChunk == 85, $"High preset must have 85 rocks. Got: {placer.LittleRocksPerChunk}");
                Assert(placer.BushesPerChunk == 0, $"High preset must have 0 shrubs. Got: {placer.BushesPerChunk}");

                // Ultra Genshin
                placer.SetDensityPreset(Gameplay.World.Foliage.FoliageDensityPreset.Ultra_Genshin);
                Assert(placer.GrassTuftsPerChunk == 3200, $"Ultra_Genshin preset must have 3200 grass tufts. Got: {placer.GrassTuftsPerChunk}");
                Assert(placer.LittleRocksPerChunk == 140, $"Ultra_Genshin preset must have 140 rocks. Got: {placer.LittleRocksPerChunk}");
                Assert(placer.BushesPerChunk == 0, $"Ultra_Genshin preset must have 0 shrubs. Got: {placer.BushesPerChunk}");

                // Cinematic Lush
                placer.SetDensityPreset(Gameplay.World.Foliage.FoliageDensityPreset.Cinematic_Lush);
                Assert(placer.GrassTuftsPerChunk == 5000, $"Cinematic_Lush preset must have 5000 grass tufts. Got: {placer.GrassTuftsPerChunk}");
                Assert(placer.LittleRocksPerChunk == 200, $"Cinematic_Lush preset must have 200 rocks. Got: {placer.LittleRocksPerChunk}");
                Assert(placer.BushesPerChunk == 0, $"Cinematic_Lush preset must have 0 shrubs. Got: {placer.BushesPerChunk}");

                // Medium
                placer.SetDensityPreset(Gameplay.World.Foliage.FoliageDensityPreset.Medium);
                Assert(placer.GrassTuftsPerChunk == 950, $"Medium preset must have 950 grass tufts. Got: {placer.GrassTuftsPerChunk}");
                Assert(placer.LittleRocksPerChunk == 45, $"Medium preset must have 45 rocks. Got: {placer.LittleRocksPerChunk}");
                Assert(placer.BushesPerChunk == 0, $"Medium preset must have 0 shrubs. Got: {placer.BushesPerChunk}");

                // Low
                placer.SetDensityPreset(Gameplay.World.Foliage.FoliageDensityPreset.Low);
                Assert(placer.GrassTuftsPerChunk == 400, $"Low preset must have 400 grass tufts. Got: {placer.GrassTuftsPerChunk}");
                Assert(placer.LittleRocksPerChunk == 20, $"Low preset must have 20 rocks. Got: {placer.LittleRocksPerChunk}");
                Assert(placer.BushesPerChunk == 0, $"Low preset must have 0 shrubs. Got: {placer.BushesPerChunk}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(testGo);
            }
        }

        public static void Test_WildflowerPaletteVariety()
        {
            var palettes = Gameplay.World.Foliage.ChunkFoliagePlacer.WildflowerPalettes;
            Assert(palettes != null && palettes.Length >= 4, "There must be at least 4 wildflower color variations.");

            for (int i = 0; i < palettes.Length; i++)
            {
                Color c = palettes[i];
                Assert(c.r >= 0f && c.r <= 1f, "R channel must be in [0,1].");
                Assert(c.g >= 0f && c.g <= 1f, "G channel must be in [0,1].");
                Assert(c.b >= 0f && c.b <= 1f, "B channel must be in [0,1].");
            }
        }

        public static void Test_NewFoliageArchetypes()
        {
            // 1. Dense Carpet Grass
            Mesh carpet = Gameplay.World.Foliage.FoliageMeshUtility.CreateDenseCarpetMesh(bladeCount: 8);
            Assert(carpet != null, "CreateDenseCarpetMesh must not be null.");
            Assert(carpet.vertexCount >= 40, $"DenseCarpet must have at least 40 vertices. Got: {carpet.vertexCount}");
            Assert(carpet.triangles.Length > 0, "DenseCarpet must have triangles.");

            Vector3[] carpetNormals = carpet.normals;
            for (int i = 0; i < carpetNormals.Length; i++)
            {
                Assert(carpetNormals[i].y >= 0.90f, $"DenseCarpet normals must point predominantly upward (Y >= 0.90). Got: {carpetNormals[i].y}");
            }

            // 2. Prairie Grass
            Mesh prairie = Gameplay.World.Foliage.FoliageMeshUtility.CreatePrairieGrassMesh(bladeCount: 5);
            Assert(prairie != null, "CreatePrairieGrassMesh must not be null.");
            Assert(prairie.vertexCount >= 25, $"PrairieGrass must have at least 25 vertices. Got: {prairie.vertexCount}");

            // 3. Reed Grass
            Mesh reed = Gameplay.World.Foliage.FoliageMeshUtility.CreateReedGrassMesh(bladeCount: 6);
            Assert(reed != null, "CreateReedGrassMesh must not be null.");
            Assert(reed.vertexCount >= 30, $"ReedGrass must have at least 30 vertices. Got: {reed.vertexCount}");

            // 4. Flowering Bush (with UV.x >= 2.0f on flower buds).
            Mesh flowerBush = Gameplay.World.Foliage.FoliageMeshUtility.CreateFloweringBushMesh(lobes: 4, flowerCount: 10);
            Assert(flowerBush != null, "CreateFloweringBushMesh must not be null.");
            Vector2[] bushUVs = flowerBush.uv;
            int budVerts = 0;
            for (int i = 0; i < bushUVs.Length; i++)
            {
                if (bushUVs[i].x >= 2.0f) budVerts++;
            }
            Assert(budVerts >= 20, $"FloweringBush must contain flower bud vertices with UV.x >= 2.0f. Got: {budVerts}");

            // 5. Ground Shrub
            Mesh groundShrub = Gameplay.World.Foliage.FoliageMeshUtility.CreateGroundShrubMesh(lobes: 5);
            Assert(groundShrub != null, "CreateGroundShrubMesh must not be null.");
            Assert(groundShrub.vertexCount >= 40, $"GroundShrub must contain at least 40 vertices. Got: {groundShrub.vertexCount}");
        }

        public static void Test_RockArchetypeMeshes()
        {
            Mesh[] rockMeshes = new Mesh[]
            {
                Gameplay.World.Foliage.FoliageMeshUtility.CreateLittlePebbleMesh(),
                Gameplay.World.Foliage.FoliageMeshUtility.CreatePebbleClusterMesh(),
                Gameplay.World.Foliage.FoliageMeshUtility.CreateRiverStoneMesh(),
                Gameplay.World.Foliage.FoliageMeshUtility.CreateScreeRockMesh()
            };

            string[] rockNames = new string[] { "LittlePebble", "PebbleCluster", "RiverStone", "ScreeRock" };

            for (int r = 0; r < rockMeshes.Length; r++)
            {
                Mesh rock = rockMeshes[r];
                string name = rockNames[r];

                Assert(rock != null, $"Rock mesh {name} must not be null.");
                Assert(rock.vertexCount >= 4, $"Rock {name} must have at least 4 vertices. Got: {rock.vertexCount}");
                Assert(rock.triangles.Length > 0, $"Rock {name} must contain triangles.");

                Color[] colors = rock.colors;
                Assert(colors != null && colors.Length == rock.vertexCount, $"Rock {name} must have valid vertex colors.");

                // All rocks must have alpha <= 0.001f (strictly zero wind weight and preserved hard shader normals).
                for (int i = 0; i < colors.Length; i++)
                {
                    Assert(colors[i].a <= 0.001f, $"Vertex {i} of {name} must have alpha 0 (no wind). Got: {colors[i].a}");
                }

                Vector3[] normals = rock.normals;
                Assert(normals != null && normals.Length == rock.vertexCount, $"Rock {name} must have valid normals.");
            }
        }

        public static void Test_WeedAndWaterArchetypeMeshes()
        {
            // 1. Wild Weed Tuft
            Mesh wildWeed = Gameplay.World.Foliage.FoliageMeshUtility.CreateWildWeedTuftMesh(leafCount: 6);
            Assert(wildWeed != null, "CreateWildWeedTuftMesh must not be null.");
            Assert(wildWeed.vertexCount >= 20, $"WildWeedTuft must have at least 20 vertices. Got: {wildWeed.vertexCount}");

            // 2. Broadleaf Weed
            Mesh broadleaf = Gameplay.World.Foliage.FoliageMeshUtility.CreateBroadleafWeedMesh(leafCount: 5);
            Assert(broadleaf != null, "CreateBroadleafWeedMesh must not be null.");
            Assert(broadleaf.vertexCount >= 20, $"BroadleafWeed must have at least 20 vertices. Got: {broadleaf.vertexCount}");

            // 3. Tall Stalk Weed
            Mesh tallStalk = Gameplay.World.Foliage.FoliageMeshUtility.CreateTallStalkWeedMesh(stalkCount: 3);
            Assert(tallStalk != null, "CreateTallStalkWeedMesh must not be null.");
            // Each stalk has three vertex pairs and a single plume tip.
            Assert(tallStalk.vertexCount == 3 * 7, $"TallStalkWeed must have 7 vertices per stalk. Got: {tallStalk.vertexCount}");
            Assert(tallStalk.triangles.Length > 0 && Array.TrueForAll(tallStalk.triangles, index => index >= 0 && index < tallStalk.vertexCount), "TallStalkWeed mesh indices must be valid.");

            // 4. Clover Patch
            Mesh clover = Gameplay.World.Foliage.FoliageMeshUtility.CreateCloverPatchMesh(cloverCount: 4);
            Assert(clover != null, "CreateCloverPatchMesh must not be null.");
            Assert(clover.vertexCount >= 30, $"CloverPatch must have at least 30 vertices. Got: {clover.vertexCount}");

            // 5. Water Cattail Bed
            Mesh cattail = Gameplay.World.Foliage.FoliageMeshUtility.CreateWaterCattailBedMesh(reedCount: 6, cattailCount: 3);
            Assert(cattail != null, "CreateWaterCattailBedMesh must not be null.");
            Assert(cattail.vertexCount >= 40, $"WaterCattailBed must have at least 40 vertices. Got: {cattail.vertexCount}");

            // Check that all weeds and reeds have wind-responsive vertices (alpha > 0.05f).
            Mesh[] weedMeshes = new Mesh[] { wildWeed, broadleaf, tallStalk, clover, cattail };
            string[] weedNames = new string[] { "WildWeed", "Broadleaf", "TallStalk", "Clover", "CattailBed" };

            for (int w = 0; w < weedMeshes.Length; w++)
            {
                Mesh m = weedMeshes[w];
                Color[] colors = m.colors;
                Assert(colors != null && colors.Length == m.vertexCount, $"Weed {weedNames[w]} must have vertex colors.");

                int windyVerts = 0;
                for (int i = 0; i < colors.Length; i++)
                {
                    if (colors[i].a > 0.05f) windyVerts++;
                }
                Assert(windyVerts > 0, $"Weed {weedNames[w]} must have wind-affected vertices (alpha > 0.05). Got: {windyVerts}");
            }
        }

        public static void Test_GenerationBudget()
        {
            var budget = new GenerationBudget(maxMillisecondsPerFrame: 5f);
            Assert(!budget.ShouldYield(), "A newly started budget must not signal yield immediately.");

            System.Threading.Thread.Sleep(8);
            Assert(budget.ShouldYield(), "After 8ms with a 5ms budget, ShouldYield must be true.");
            Assert(!budget.ShouldYield(), "Immediately after yield, ShouldYield must reset to false.");
        }
    }
}
