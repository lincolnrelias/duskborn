using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class TerrainChunk : MonoBehaviour
{
    [SerializeField] private Vector2Int _chunkCoord;
    public Vector2Int ChunkCoord
    {
        get
        {
            if (_chunkCoord == Vector2Int.zero && gameObject != null && gameObject.name.StartsWith("Chunk_"))
            {
                string[] parts = gameObject.name.Split('_');
                if (parts.Length >= 3 && int.TryParse(parts[1], out int px) && int.TryParse(parts[2], out int pz))
                {
                    _chunkCoord = new Vector2Int(px, pz);
                }
            }
            return _chunkCoord;
        }
        private set => _chunkCoord = value;
    }
    private LowPolyTerrainConfig config;
    private int activeSeed;
    private MeshFilter meshFilter;
    private MeshCollider meshCollider;

    private void Awake()
    {
        // Garante a leitura e inicialização da coordenada mesmo se o chunk foi pré-gerado pelo editor
        _ = ChunkCoord;
    }

    public void Initialize(Vector2Int coord, LowPolyTerrainConfig configuration, int seedOverride = 0, bool autoGenerateMesh = true)
    {
        ChunkCoord = coord;
        config = configuration;
        activeSeed = seedOverride != 0 ? seedOverride : (config != null ? config.seed : 4242);
        meshFilter = GetComponent<MeshFilter>();
        meshCollider = GetComponent<MeshCollider>();

        if (autoGenerateMesh)
        {
            GenerateMesh();
        }
    }

    public void GenerateMesh()
    {
        Mesh mesh = BuildMesh();
        ApplyMesh(mesh);
    }

    public void ApplyMesh(Mesh mesh)
    {
        if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
        if (meshCollider == null) meshCollider = GetComponent<MeshCollider>();

        meshFilter.sharedMesh = mesh;
        meshCollider.sharedMesh = mesh;
    }

    public Mesh BuildMesh()
    {
        int size = config.chunkSize;
        float cellSize = config.cellSize;

        float halfMapX = (config.chunksX * size * cellSize) * 0.5f;
        float halfMapZ = (config.chunksZ * size * cellSize) * 0.5f;

        // 1. Matriz de alturas com (size + 1) para junção contínua
        float[,] heightMap = new float[size + 1, size + 1];

        for (int z = 0; z <= size; z++)
        {
            for (int x = 0; x <= size; x++)
            {
                // Coordenadas globais de mundo
                float worldX = (ChunkCoord.x * size + x) * cellSize;
                float worldZ = (ChunkCoord.y * size + z) * cellSize;

                heightMap[x, z] = TerrainNoise.SampleHeight(worldX, worldZ, config, activeSeed, halfMapX, halfMapZ);
            }
        }

        // 2. Construção da Malha com Vértices Duplicados (Flat Shading)
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

                // Triângulo 1 (p00, p01, p10)
                AddFace(p00, p01, p10, vertices, triangles, colors);
                // Triângulo 2 (p10, p01, p11)
                AddFace(p10, p01, p11, vertices, triangles, colors);
            }
        }

        Mesh mesh = new Mesh();
        mesh.name = $"TerrainMesh_{ChunkCoord.x}_{ChunkCoord.y}";
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetColors(colors);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
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

        // Calcula a normal plana da face para verificar inclinação e iluminação
        Vector3 faceNormal = Vector3.Cross(b - a, c - a).normalized;
        float slopeAngle = Vector3.Angle(faceNormal, Vector3.up);

        float avgHeight = (a.y + b.y + c.y) / 3f;
        Color faceColor = EvaluateBiomeColor(avgHeight, slopeAngle, faceNormal);

        cols.Add(faceColor);
        cols.Add(faceColor);
        cols.Add(faceColor);
    }

    private Color EvaluateBiomeColor(float height, float slopeAngle, Vector3 faceNormal)
    {
        Color finalColor;

        // 1. Abaixo da água (leito submarino com areia molhada e rocha profunda)
        if (height <= config.waterLevel)
        {
            float depth = Mathf.Clamp01((config.waterLevel - height) / 3.0f);
            Color wetSand = config.sandColor * 0.72f;
            Color deepSeabed = config.rockColor * 0.52f;
            finalColor = Color.Lerp(wetSand, deepSeabed, depth);
        }
        // 2. Transição de Praia / Areia
        else if (height <= config.waterLevel + 1.0f)
        {
            float sandBlend = Mathf.Clamp01((height - config.waterLevel) / 1.0f);
            finalColor = Color.Lerp(config.sandColor, config.grassColor, sandBlend);
        }
        // 3. Paredões Íngremes / Escarpas
        else if (slopeAngle >= config.steepSlopeThreshold)
        {
            float steepness = Mathf.Clamp01((slopeAngle - config.steepSlopeThreshold) / 25f);
            finalColor = Color.Lerp(config.rockColor, config.cliffColor, steepness);
        }
        // 4. Neve nos Picos Mais Altos
        else if (height >= config.heightMultiplier * 0.85f)
        {
            float snowBlend = Mathf.Clamp01((height - config.heightMultiplier * 0.85f) / (config.heightMultiplier * 0.15f));
            finalColor = Color.Lerp(config.rockColor, config.snowColor, snowBlend);
        }
        // 5. Rocha de Montanhas Médias
        else if (height >= config.heightMultiplier * 0.62f)
        {
            float rockBlend = Mathf.Clamp01((height - config.heightMultiplier * 0.62f) / (config.heightMultiplier * 0.23f));
            finalColor = Color.Lerp(config.grassColor, config.rockColor, rockBlend);
        }
        // 6. Vegetação de Planície e Vales
        else
        {
            // Variação orgânica entre grama iluminada e grama profunda de vale
            float valleyBlend = Mathf.Clamp01(height / (config.heightMultiplier * 0.45f));
            finalColor = Color.Lerp(config.deepGrassColor, config.grassColor, valleyBlend);
        }

        // Aplicação de oclusão de fendas / facetas (Facet Crease AO)
        if (config.facetAOIntensity > 0f && height > config.waterLevel)
        {
            float ao = 1f - (Mathf.Clamp01(slopeAngle / 90f) * config.facetAOIntensity);
            finalColor.r *= ao;
            finalColor.g *= ao;
            finalColor.b *= ao;
        }

        return finalColor;
    }
}
