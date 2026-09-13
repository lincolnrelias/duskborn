using System.Collections.Generic;
using UnityEngine;

public static class TerrainNoise
{
    private static readonly Dictionary<int, Vector2[]> _offsetCache = new Dictionary<int, Vector2[]>();

    private static Vector2[] GetOctaveOffsets(int seed, int octaves)
    {
        int key = seed ^ (octaves << 16);
        if (!_offsetCache.TryGetValue(key, out var offsets) || offsets.Length != octaves)
        {
            System.Random prng = new System.Random(seed);
            offsets = new Vector2[octaves];
            for (int i = 0; i < octaves; i++)
            {
                float offsetX = prng.Next(-10000, 10000);
                float offsetZ = prng.Next(-10000, 10000);
                offsets[i] = new Vector2(offsetX, offsetZ);
            }
            _offsetCache[key] = offsets;
        }
        return offsets;
    }

    public static float SampleNoise(float worldX, float worldZ, LowPolyTerrainConfig config)
    {
        return SampleNoise(worldX, worldZ, config, config != null ? config.seed : 4242);
    }

    public static float SampleNoise(float worldX, float worldZ, LowPolyTerrainConfig config, int seed)
    {
        float halfX = config != null ? (config.chunksX * config.chunkSize * config.cellSize) * 0.5f : 100f;
        float halfZ = config != null ? (config.chunksZ * config.chunkSize * config.cellSize) * 0.5f : 100f;
        return SampleHeight(worldX, worldZ, config, seed, halfX, halfZ);
    }

    public static float SampleHeight(float worldX, float worldZ, LowPolyTerrainConfig config, int seed, float halfMapX, float halfMapZ)
    {
        if (config == null) return 0f;

        Vector2[] octaveOffsets = GetOctaveOffsets(seed, config.octaves);

        float totalHeight = 0f;
        float amplitude = 1f;
        float frequency = 1f;
        float maxPossibleHeight = 0f;

        for (int i = 0; i < config.octaves; i++)
        {
            float sampleX = (worldX * config.noiseScale * frequency) + octaveOffsets[i].x;
            float sampleZ = (worldZ * config.noiseScale * frequency) + octaveOffsets[i].y;

            float perlinValue = Mathf.PerlinNoise(sampleX, sampleZ);
            totalHeight += perlinValue * amplitude;
            maxPossibleHeight += amplitude;

            amplitude *= config.persistence;
            frequency *= config.lacunarity;
        }

        float normalizedHeight = totalHeight / Mathf.Max(0.0001f, maxPossibleHeight);
        float finalHeight = normalizedHeight * config.heightMultiplier;

        // 1. Patamares de Combate / Terracing com rampas suaves
        if (config.terraceStep > 0f)
        {
            float step = config.terraceStep;
            float h = finalHeight / step;
            float baseFloor = Mathf.Floor(h);
            float frac = h - baseFloor;
            float smoothness = Mathf.Clamp(config.terraceRampSmoothness, 0.05f, 0.95f);
            float ramp = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f - smoothness, 1f, frac));
            finalHeight = (baseFloor + ramp) * step;
        }

        // 2. Clareira Central / Bacia do Santuário (Área plana para spawn e bancada)
        if (config.centralSanctuaryRadius > 0f)
        {
            float distCenter = Mathf.Sqrt(worldX * worldX + worldZ * worldZ);
            if (distCenter < config.centralSanctuaryRadius)
            {
                float t = Mathf.Clamp01(distCenter / config.centralSanctuaryRadius);
                float blend = Mathf.SmoothStep(1f, 0f, t) * config.centralSanctuaryFlattenStrength;
                float targetHeight = config.waterLevel + config.centralSanctuaryOffsetAboveWater;
                finalHeight = Mathf.Lerp(finalHeight, targetHeight, blend);
            }
        }

        // 3. Limite do Mapa (Island Falloff ou Paredões de Vale)
        if (config.boundaryType != LowPolyTerrainConfig.MapBoundaryType.None && halfMapX > 0.1f && halfMapZ > 0.1f)
        {
            float nx = worldX / halfMapX;
            float nz = worldZ / halfMapZ;
            float distNorm = Mathf.Sqrt(nx * nx + nz * nz);

            float startRatio = config.EffectiveFalloffStartRatio;
            if (distNorm > startRatio)
            {
                float falloffRange = Mathf.Max(0.01f, config.EffectiveFalloffDistanceRatio);
                float t = Mathf.Clamp01((distNorm - startRatio) / falloffRange);
                float smoothT = t * t * (3f - 2f * t); // SmoothStep

                if (config.boundaryType == LowPolyTerrainConfig.MapBoundaryType.Island)
                {
                    float oceanFloor = config.waterLevel - 3.5f;
                    finalHeight = Mathf.Lerp(finalHeight, oceanFloor, smoothT);
                }
                else if (config.boundaryType == LowPolyTerrainConfig.MapBoundaryType.ValleyWalls)
                {
                    float wallHeight = config.boundaryWallHeight > 5f ? config.boundaryWallHeight : 28f;
                    finalHeight = Mathf.Lerp(finalHeight, finalHeight + wallHeight, smoothT);
                }
            }
        }

        return finalHeight;
    }
}
