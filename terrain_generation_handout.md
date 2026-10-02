# Implementation Guide: Chunk-Based Low-Poly Terrain Generation (Unity)

> **Audience:** Implementation Agent / Unity Developer
> **Objective:** Create and integrate a procedural modular *low-poly* terrain system in an $M \times N$ chunk grid, highly configurable through `ScriptableObject` or Inspector, with physics and navigation *NavMesh* support.
> **Scope:** Do **not** include chest or enemy logic in this step (implemented later).

---

## 1. Technical Requirements & Architecture

1. **Stylized Flat Shading (*Low-Poly*):**
   - Vertices must **not** be shared between neighboring faces.
   - Each triangle needs 3 independent vertices with normals calculated by cross product ($\vec{N} = (\vec{B} - \vec{A}) \times (\vec{C} - \vec{A})$).
2. **Seamless Chunk Borders:**
   - Noise calculations must use global world coordinates:
     $$\text{worldX} = (\text{chunkCoord.x} \times \text{chunkSize} + \text{localX}) \times \text{cellSize}$$
   - Each chunk's height matrix must have dimensions `(size + 1, size + 1)` to connect seamlessly with neighbors.
3. **Vertex Colors:**
   - Store biome coloring directly in `mesh.colors32` (Water, Sand, Grass, Rock, Snow), avoiding multiple texture dependencies and reducing draw calls.
4. **Physics & Walkability:**
   - Each chunk must instantiate or update its own `MeshCollider`.
   - Support `NavMeshSurface` (`com.unity.ai.navigation`) for runtime baking.

---

## 2. Recommended File Structure

Organize Unity project files in this pattern:

```text
Assets/
└── Scripts/
    └── Terrain/
        ├── LowPolyTerrainConfig.cs   // ScriptableObject with all parameters
        ├── TerrainNoise.cs           // Fractal noise math utility (fBm)
        ├── TerrainChunk.cs           // Individual Chunk component (Mesh + Collider)
        └── ChunkGridManager.cs       // Central MxN Grid manager
```

---

## 3. C# Scripts Ready for Implementation

### 3.1 `LowPolyTerrainConfig.cs`
Create this file at `Assets/Scripts/Terrain/LowPolyTerrainConfig.cs`:

```csharp
using UnityEngine;

[CreateAssetMenu(fileName = "NewTerrainConfig", menuName = "Roguelike Terrain/Terrain Configuration")]
public class LowPolyTerrainConfig : ScriptableObject
{
    [Header("Chunk Grid Dimensions")]
    [Tooltip("Number of chunks on the X axis (e.g. 1 for 1x3, 3 for 3x3, 5 for 5x5)")]
    [Range(1, 15)] public int chunksX = 3;

    [Tooltip("Number of chunks on the Z axis")]
    [Range(1, 15)] public int chunksZ = 3;

    [Tooltip("Number of quads per chunk (e.g. 16x16 quads)")]
    [Range(4, 32)] public int chunkSize = 16;

    [Tooltip("Size of each quad in world units (meters)")]
    [Range(0.5f, 10f)] public float cellSize = 2.0f;

    [Header("Fractal Noise (fBm) & Terrain")]
    public int seed = 4242;
    [Range(0.01f, 0.3f)] public float noiseScale = 0.07f;
    [Range(1, 6)] public int octaves = 3;
    [Range(0.1f, 1f)] public float persistence = 0.5f;
    [Range(1f, 4f)] public float lacunarity = 2.0f;
    [Range(1f, 50f)] public float heightMultiplier = 12.0f;

    [Header("Plateau Effect / Terracing")]
    [Tooltip("0 = Disabled. Higher values create stylized steps.")]
    [Range(0f, 10f)] public float terraceStep = 0f;

    [Header("Biomes by Height & Slope")]
    public float waterLevel = 2.0f;
    public Color waterColor = new Color(0.18f, 0.45f, 0.82f);
    public Color sandColor  = new Color(0.85f, 0.78f, 0.55f);
    public Color grassColor = new Color(0.28f, 0.65f, 0.25f);
    public Color rockColor  = new Color(0.45f, 0.45f, 0.48f);
    public Color snowColor  = new Color(0.95f, 0.95f, 0.98f);

    [Tooltip("Slope (degrees) above which the face becomes rock")]
    [Range(15f, 85f)] public float steepSlopeThreshold = 40.0f;
}
```

---

### 3.2 `TerrainNoise.cs`
Create this file at `Assets/Scripts/Terrain/TerrainNoise.cs`:

```csharp
using UnityEngine;

public static class TerrainNoise
{
    public static float SampleNoise(float worldX, float worldZ, LowPolyTerrainConfig config)
    {
        System.Random prng = new System.Random(config.seed);
        Vector2[] octaveOffsets = new Vector2[config.octaves];
        for (int i = 0; i < config.octaves; i++)
        {
            float offsetX = prng.Next(-10000, 10000);
            float offsetZ = prng.Next(-10000, 10000);
            octaveOffsets[i] = new Vector2(offsetX, offsetZ);
        }

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

        float normalizedHeight = totalHeight / maxPossibleHeight;
        float finalHeight = normalizedHeight * config.heightMultiplier;

        // Optional terrace application (Terracing).
        if (config.terraceStep > 0f)
        {
            finalHeight = Mathf.Round(finalHeight / config.terraceStep) * config.terraceStep;
        }

        return finalHeight;
    }
}
```

---

### 3.3 `TerrainChunk.cs`
Create this file at `Assets/Scripts/Terrain/TerrainChunk.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class TerrainChunk : MonoBehaviour
{
    public Vector2Int ChunkCoord { get; private set; }
    private LowPolyTerrainConfig config;
    private MeshFilter meshFilter;
    private MeshCollider meshCollider;

    public void Initialize(Vector2Int coord, LowPolyTerrainConfig configuration)
    {
        ChunkCoord = coord;
        config = configuration;
        meshFilter = GetComponent<MeshFilter>();
        meshCollider = GetComponent<MeshCollider>();

        GenerateMesh();
    }

    public void GenerateMesh()
    {
        int size = config.chunkSize;
        float cellSize = config.cellSize;

        // 1. Height matrix with (size + 1) for seamless joins.
        float[,] heightMap = new float[size + 1, size + 1];

        for (int z = 0; z <= size; z++)
        {
            for (int x = 0; x <= size; x++)
            {
                // Global world coordinates.
                float worldX = (ChunkCoord.x * size + x) * cellSize;
                float worldZ = (ChunkCoord.y * size + z) * cellSize;

                heightMap[x, z] = TerrainNoise.SampleNoise(worldX, worldZ, config);
            }
        }

        // 2. Mesh Construction with Duplicated Vertices (Flat Shading).
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Color> colors = new List<Color>();

        for (int z = 0; z < size; z++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector3 p00 = new Vector3(x * cellSize, heightMap[x, z], z * cellSize);
                Vector3 p10 = new Vector3((x + 1) * cellSize, heightMap[x + 1, z], z * cellSize);
                Vector3 p01 = new Vector3(x * cellSize, heightMap[x, z + 1], (z + 1) * cellSize);
                Vector3 p11 = new Vector3((x + 1) * cellSize, heightMap[x + 1, z + 1], (z + 1) * cellSize);

                // Triangle 1 (p00, p01, p10).
                AddFace(p00, p01, p10, vertices, triangles, colors);
                // Triangle 2 (p10, p01, p11).
                AddFace(p10, p01, p11, vertices, triangles, colors);
            }
        }

        Mesh mesh = new Mesh();
        mesh.name = $"TerrainMesh_{ChunkCoord.x}_{ChunkCoord.y}";
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetColors(colors);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        meshFilter.sharedMesh = mesh;
        meshCollider.sharedMesh = mesh;
    }

    private void AddFace(Vector3 a, Vector3 b, Vector3 c, List<Vector3> verts, List<int> tris, List<Color> cols)
    {
        int startIndex = verts.Count;
        verts.Add(a);
        verts.Add(b);
        verts.Add(c);

        tris.Add(startIndex);
        tris.Add(startIndex + 1);
        tris.Add(startIndex + 2);

        // Calculate face normal to check slope.
        Vector3 faceNormal = Vector3.Cross(b - a, c - a).normalized;
        float slopeAngle = Vector3.Angle(faceNormal, Vector3.up);

        float avgHeight = (a.y + b.y + c.y) / 3f;
        Color faceColor = EvaluateBiomeColor(avgHeight, slopeAngle);

        cols.Add(faceColor);
        cols.Add(faceColor);
        cols.Add(faceColor);
    }

    private Color EvaluateBiomeColor(float height, float slopeAngle)
    {
        if (height <= config.waterLevel) return config.waterColor;
        if (height <= config.waterLevel + 0.8f) return config.sandColor;
        if (slopeAngle >= config.steepSlopeThreshold) return config.rockColor;
        if (height < config.heightMultiplier * 0.58f) return config.grassColor;
        if (height < config.heightMultiplier * 0.82f) return config.rockColor;
        return config.snowColor;
    }
}
```

---

### 3.4 `ChunkGridManager.cs`
Create this file at `Assets/Scripts/Terrain/ChunkGridManager.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

#if UNITY_AI_NAVIGATION
using Unity.AI.Navigation;
#endif

public class ChunkGridManager : MonoBehaviour
{
    [Header("Active Configuration")]
    public LowPolyTerrainConfig config;

    [Header("Rendering & Material")]
    public Material vertexColorMaterial;

    private readonly Dictionary<Vector2Int, TerrainChunk> loadedChunks = new Dictionary<Vector2Int, TerrainChunk>();

    private void Start()
    {
        if (config != null)
        {
            GenerateGrid();
        }
    }

    [ContextMenu("Regenerate Terrain Grid")]
    public void GenerateGrid()
    {
        ClearGrid();

        if (config == null)
        {
            Debug.LogError("[ChunkGridManager] No configuration assigned!");
            return;
        }

        int startX = -config.chunksX / 2;
        int startZ = -config.chunksZ / 2;
        float chunkWorldLength = config.chunkSize * config.cellSize;

        for (int cz = startZ; cz < startZ + config.chunksZ; cz++)
        {
            for (int cx = startX; cx < startX + config.chunksX; cx++)
            {
                Vector2Int coord = new Vector2Int(cx, cz);
                Vector3 worldPos = new Vector3(cx * chunkWorldLength, 0f, cz * chunkWorldLength);

                GameObject chunkGO = new GameObject($"Chunk_{cx}_{cz}");
                chunkGO.transform.parent = transform;
                chunkGO.transform.position = worldPos;

                MeshRenderer renderer = chunkGO.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = vertexColorMaterial;

                TerrainChunk chunk = chunkGO.AddComponent<TerrainChunk>();
                chunk.Initialize(coord, config);

                loadedChunks.Add(coord, chunk);
            }
        }

        RebuildNavMeshIfAvailable();
        Debug.Log($"[ChunkGridManager] Grid {config.chunksX}x{config.chunksZ} generated successfully ({loadedChunks.Count} chunks).");
    }

    public void ClearGrid()
    {
        foreach (var kvp in loadedChunks)
        {
            if (kvp.Value != null)
            {
                DestroyImmediate(kvp.Value.gameObject);
            }
        }
        loadedChunks.Clear();
    }

    private void RebuildNavMeshIfAvailable()
    {
#if UNITY_AI_NAVIGATION
        NavMeshSurface surface = GetComponent<NavMeshSurface>();
        if (surface != null)
        {
            surface.BuildNavMesh();
        }
#endif
    }
}
```

---

## 4. Step-by-Step Instructions for the Implementing Agent

1. **Material Configuration (Vertex Colors):**
   - Create a new Material at `Assets/Materials/M_TerrainLowPoly.mat`.
   - Configure the Shader to support vertex colors:
     - In **URP (Universal Render Pipeline)**: Use `Universal Render Pipeline/Unlit` or `Universal Render Pipeline/Lit` and enable *Vertex Color*.
     - In the **Built-in Pipeline**: Use `Mobile/Particles/VertexLit Blended` or `Standard` with *Vertex Colors*.
2. **Create the ScriptableObject:**
   - In Unity, right-click the *Project* window $\rightarrow$ `Create` $\rightarrow$ `Roguelike Terrain` $\rightarrow$ `Terrain Configuration`.
   - Set `chunksX = 3`, `chunksZ = 3` (or a desired size such as `1x3`, `5x5`), `chunkSize = 16`, and `cellSize = 2.0`.
3. **Scene Configuration:**
   - Create an empty scene GameObject named `[TerrainManager]`.
   - Add the component `ChunkGridManager`.
   - Drag the created `ScriptableObject` to `Config` and the material to `VertexColorMaterial`.
   - Manual user check: use the Inspector context menu (`Regenerate Terrain Grid`) to generate the mesh. Agents use the project's non-interactive CLI.
4. **Walkability Verification:**
   - Ensure the player has a `CharacterController` or `Rigidbody` + `CapsuleCollider`.
   - Test movement over mesh faces. Each chunk's generated `MeshCollider` provides immediate collision and Raycast support.
