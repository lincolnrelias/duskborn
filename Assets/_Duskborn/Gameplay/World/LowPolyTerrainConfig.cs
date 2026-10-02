using UnityEngine;

[CreateAssetMenu(fileName = "NewTerrainConfig", menuName = "Roguelike Terrain/Terrain Configuration")]
public class LowPolyTerrainConfig : ScriptableObject
{
    [Header("Chunk Grid Dimensions")]
    [Tooltip("Number of chunks on the X axis (e.g. 1 for 1x3, 3 for 3x3, 5 for 5x5).")]
    [Range(1, 15)] public int chunksX = 3;

    [Tooltip("Number of chunks on the Z axis (map depth).")]
    [Range(1, 15)] public int chunksZ = 3;

    [Tooltip("Chunk resolution in quads per side (e.g. 32 creates 32x32 quads = 2048 triangles; 64 creates 64x64 quads = 8192 triangles).")]
    [Range(4, 128)] public int chunkSize = 32;

    [Tooltip("Physical quad size in world units / meters. Smaller values (e.g. 0.5 to 1.0) increase geometric detail density / resolution.")]
    [Range(0.1f, 10f)] public float cellSize = 1.0f;

    [Header("Fractal Noise (fBm) & Terrain")]
    [Tooltip("Deterministic numeric seed for terrain generation. The same seed ensures identical map reproduction on all clients.")]
    public int seed = 4242;

    [Tooltip("Base Perlin noise frequency scale. Smaller values create broad, smooth hills; larger values create dense, pointed terrain.")]
    [Range(0.01f, 0.3f)] public float noiseScale = 0.07f;

    [Tooltip("Number of combined fractal noise layers (octaves). More octaves add finer detail and roughness.")]
    [Range(1, 6)] public int octaves = 3;

    [Tooltip("Terrain persistence (0 to 1). Defines how much amplitude decreases per successive octave, controlling microdetail strength.")]
    [Range(0.1f, 1f)] public float persistence = 0.5f;

    [Tooltip("Noise lacunarity. Frequency multiplier applied to each new octave (controls additional detail density).")]
    [Range(1f, 4f)] public float lacunarity = 2.0f;

    [Tooltip("Maximum vertical height (meters) of the highest terrain peaks.")]
    [Range(1f, 50f)] public float heightMultiplier = 12.0f;

    public enum MapBoundaryType
    {
        [Tooltip("Create a stylized island with outer edges falling smoothly into the ocean.")]
        Island,
        [Tooltip("Create a valley surrounded by steep, impassable mountain walls.")]
        ValleyWalls,
        [Tooltip("No radial limit (continuous infinite terrain).")]
        None
    }

    [Header("Map Limits (Boundary & World Bounds)")]
    [Tooltip("Map edge boundary type to prevent falling into the void.")]
    public MapBoundaryType boundaryType = MapBoundaryType.Island;

    [Tooltip("Normalized distance (0 to 1) from the map center where the edge starts falling / rising (e.g. 0.78).")]
    [Range(0.4f, 0.95f)] public float boundaryFalloffStart = 0.78f;

    [Tooltip("Normalized edge transition width before full falloff (e.g. 0.20).")]
    [Range(0.05f, 0.5f)] public float boundaryFalloffDistance = 0.20f;

    [Tooltip("Additional boundary mountain height when boundaryType is ValleyWalls.")]
    [Range(10f, 60f)] public float boundaryWallHeight = 28f;

    /// <summary>
    /// Return the normalized edge fraction guarded against null / zero values in older assets.
    /// </summary>
    public float EffectiveFalloffStartRatio => boundaryFalloffStart > 0.1f ? boundaryFalloffStart : 0.78f;

    /// <summary>
    /// Return the normalized edge transition width guarded against null / zero values in older assets.
    /// </summary>
    public float EffectiveFalloffDistanceRatio => boundaryFalloffDistance > 0.02f ? boundaryFalloffDistance : 0.20f;

    /// <summary>
    /// Return the effective playable radius in meters before edge falloff starts.
    /// </summary>
    public float GetPlayableBoundaryRadius(float mapRadius)
    {
        return mapRadius * EffectiveFalloffStartRatio;
    }

    [Header("Central Clearing (Sanctuary Basin)")]
    [Tooltip("Radius around (0,0) where terrain is leveled for the central plaza and workbench.")]
    [Range(4f, 30f)] public float centralSanctuaryRadius = 14f;

    [Tooltip("Central clearing leveling strength (0 = no effect, 1 = perfectly flat).")]
    [Range(0f, 1f)] public float centralSanctuaryFlattenStrength = 0.85f;

    [Tooltip("Central clearing height offset above water level.")]
    public float centralSanctuaryOffsetAboveWater = 1.8f;

    [Header("Plateaus / Combat Terraces (Terracing)")]
    [Tooltip("Height of each combat terrace / step (e.g. 1.6m creates distinct terraces; 0 = continuous).")]
    [Range(0f, 10f)] public float terraceStep = 1.6f;

    [Tooltip("Ramp smoothness between combat terraces (0 = vertical steps, 1 = continuous ramps).")]
    [Range(0.1f, 1.0f)] public float terraceRampSmoothness = 0.45f;

    [Header("Biomes by Height & Slope")]
    [Tooltip("Water level elevation (meters).")]
    public float waterLevel = 2.0f;

    [Tooltip("Color applied to terrain vertices below water level.")]
    public Color waterColor = new Color(0.12f, 0.38f, 0.72f);

    [Tooltip("Beach / sand color immediately above water.")]
    public Color sandColor = new Color(0.88f, 0.80f, 0.58f);

    [Tooltip("Lush grass color for plains and clearings.")]
    public Color grassColor = new Color(0.32f, 0.68f, 0.26f);

    [Tooltip("Dense, dark grass color for valleys and groves.")]
    public Color deepGrassColor = new Color(0.20f, 0.48f, 0.22f);

    [Tooltip("Rocky cliff and steep slope color.")]
    public Color cliffColor = new Color(0.38f, 0.36f, 0.40f);

    [Tooltip("Rock color on elevated plateaus.")]
    public Color rockColor = new Color(0.52f, 0.50f, 0.52f);

    [Tooltip("Snow color at the highest summits and peaks.")]
    public Color snowColor = new Color(0.95f, 0.96f, 0.99f);

    [Tooltip("Face slope angle threshold in degrees. Steeper faces become cliffs / rock.")]
    [Range(15f, 85f)] public float steepSlopeThreshold = 38.0f;

    [Header("Stylized Shading & Occlusion")]
    [Tooltip("Occlusion shading intensity for cavities and gaps between facets.")]
    [Range(0f, 0.8f)] public float facetAOIntensity = 0.25f;
}
