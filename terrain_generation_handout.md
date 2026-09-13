# Guia de Implementação: Sistema de Geração de Terreno Low-Poly em Chunks (Unity)

> **Destinatário:** Agente de Implementação / Desenvolvedor Unity  
> **Objetivo:** Criar e integrar um sistema procedural e modular de terreno *low-poly* em grade de *chunks* ($M \times N$), altamente configurável via `ScriptableObject` ou Inspector, com suporte a física e *NavMesh* para navegação.  
> **Nota de Escopo:** **Não** incluir lógica de baús ou inimigos nesta etapa (serão implementados posteriormente).

---

## 1. Requisitos Técnicos & Arquitetura

1. **Flat Shading Estilizado (*Low-Poly*):**
   - Vértices **não** devem ser compartilhados entre faces vizinhas.
   - Cada triângulo precisa de 3 vértices próprios com normais calculadas via produto vetorial ($\vec{N} = (\vec{B} - \vec{A}) \times (\vec{C} - \vec{A})$).
2. **Junção Perfeita entre Chunks (*Seamless Borders*):**
   - O cálculo do ruído deve utilizar coordenadas globais de mundo:
     $$\text{worldX} = (\text{chunkCoord.x} \times \text{chunkSize} + \text{localX}) \times \text{cellSize}$$
   - A matriz de alturas de cada *chunk* deve ter dimensão `(size + 1, size + 1)` para conectar perfeitamente com os vizinhos sem frestas.
3. **Cores por Vértice (*Vertex Colors*):**
   - Coloração dos biomas gravada diretamente em `mesh.colors32` (Água, Areia, Grama, Rocha, Neve), evitando dependência de múltiplas texturas e reduzindo *draw calls*.
4. **Física & Caminhabilidade (*Walkability*):**
   - Cada *chunk* deve instanciar ou atualizar seu próprio `MeshCollider`.
   - Compatibilidade com `NavMeshSurface` (`com.unity.ai.navigation`) para *baking* em *runtime*.

---

## 2. Estrutura de Arquivos Recomendada

Organize os arquivos dentro do projeto Unity no seguinte padrão:

```text
Assets/
└── Scripts/
    └── Terrain/
        ├── LowPolyTerrainConfig.cs   // ScriptableObject com todos os parâmetros
        ├── TerrainNoise.cs           // Utilitário matemático de ruído fractal (fBm)
        ├── TerrainChunk.cs           // Componente individual de cada Chunk (Mesh + Collider)
        └── ChunkGridManager.cs       // Gerenciador central da Grid MxN
```

---

## 3. Scripts C# Prontos para Implementação

### 3.1 `LowPolyTerrainConfig.cs`
Crie este arquivo em `Assets/Scripts/Terrain/LowPolyTerrainConfig.cs`:

```csharp
using UnityEngine;

[CreateAssetMenu(fileName = "NovoTerrenoConfig", menuName = "Terreno Roguelike/Configuração de Terreno")]
public class LowPolyTerrainConfig : ScriptableObject
{
    [Header("Dimensões da Grid de Chunks")]
    [Tooltip("Quantidade de chunks no eixo X (ex: 1 para 1x3, 3 para 3x3, 5 para 5x5)")]
    [Range(1, 15)] public int chunksX = 3;

    [Tooltip("Quantidade de chunks no eixo Z")]
    [Range(1, 15)] public int chunksZ = 3;

    [Tooltip("Quantidade de quads por chunk (ex: 16x16 quads)")]
    [Range(4, 32)] public int chunkSize = 16;

    [Tooltip("Tamanho de cada quad em unidades de mundo (metros)")]
    [Range(0.5f, 10f)] public float cellSize = 2.0f;

    [Header("Ruído Fractal (fBm) & Relevo")]
    public int seed = 4242;
    [Range(0.01f, 0.3f)] public float noiseScale = 0.07f;
    [Range(1, 6)] public int octaves = 3;
    [Range(0.1f, 1f)] public float persistence = 0.5f;
    [Range(1f, 4f)] public float lacunarity = 2.0f;
    [Range(1f, 50f)] public float heightMultiplier = 12.0f;

    [Header("Efeito Platô / Terracing")]
    [Tooltip("0 = Desligado. Valores maiores criam degraus estilizados.")]
    [Range(0f, 10f)] public float terraceStep = 0f;

    [Header("Biomas por Altura & Inclinação")]
    public float waterLevel = 2.0f;
    public Color waterColor = new Color(0.18f, 0.45f, 0.82f);
    public Color sandColor  = new Color(0.85f, 0.78f, 0.55f);
    public Color grassColor = new Color(0.28f, 0.65f, 0.25f);
    public Color rockColor  = new Color(0.45f, 0.45f, 0.48f);
    public Color snowColor  = new Color(0.95f, 0.95f, 0.98f);
    
    [Tooltip("Inclinação (em graus) acima da qual a face vira rocha")]
    [Range(15f, 85f)] public float steepSlopeThreshold = 40.0f;
}
```

---

### 3.2 `TerrainNoise.cs`
Crie este arquivo em `Assets/Scripts/Terrain/TerrainNoise.cs`:

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

        // Aplicação opcional de platôs (Terracing)
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
Crie este arquivo em `Assets/Scripts/Terrain/TerrainChunk.cs`:

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

        // 1. Matriz de alturas com (size + 1) para junção contínua
        float[,] heightMap = new float[size + 1, size + 1];

        for (int z = 0; z <= size; z++)
        {
            for (int x = 0; x <= size; x++)
            {
                // Coordenadas globais de mundo
                float worldX = (ChunkCoord.x * size + x) * cellSize;
                float worldZ = (ChunkCoord.y * size + z) * cellSize;

                heightMap[x, z] = TerrainNoise.SampleNoise(worldX, worldZ, config);
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

        // Calcula a normal da face para verificar inclinação
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
Crie este arquivo em `Assets/Scripts/Terrain/ChunkGridManager.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

#if UNITY_AI_NAVIGATION
using Unity.AI.Navigation;
#endif

public class ChunkGridManager : MonoBehaviour
{
    [Header("Configuração Ativa")]
    public LowPolyTerrainConfig config;

    [Header("Renderização & Material")]
    public Material vertexColorMaterial;

    private readonly Dictionary<Vector2Int, TerrainChunk> loadedChunks = new Dictionary<Vector2Int, TerrainChunk>();

    private void Start()
    {
        if (config != null)
        {
            GenerateGrid();
        }
    }

    [ContextMenu("Regerar Grid de Terreno")]
    public void GenerateGrid()
    {
        ClearGrid();

        if (config == null)
        {
            Debug.LogError("[ChunkGridManager] Nenhuma configuração atribuída!");
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
        Debug.Log($"[ChunkGridManager] Grid {config.chunksX}x{config.chunksZ} gerada com sucesso ({loadedChunks.Count} chunks).");
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

## 4. Instruções Passo a Passo para o Agente Executor

1. **Configuração do Material (Vertex Colors):**
   - Crie um novo Material em `Assets/Materials/M_TerrainLowPoly.mat`.
   - Configure o Shader para suportar cores de vértice:
     - No **URP (Universal Render Pipeline)**: Use `Universal Render Pipeline/Unlit` ou `Universal Render Pipeline/Lit` e habilite *Vertex Color*.
     - No **Built-in Pipeline**: Use `Mobile/Particles/VertexLit Blended` ou `Standard` com *Vertex Colors*.
2. **Criação do ScriptableObject:**
   - No Unity, clique com o botão direito na janela *Project* $\rightarrow$ `Create` $\rightarrow$ `Terreno Roguelike` $\rightarrow$ `Configuração de Terreno`.
   - Ajuste `chunksX = 3`, `chunksZ = 3` (ou a dimensão desejada, como `1x3`, `5x5`), `chunkSize = 16`, `cellSize = 2.0`.
3. **Configuração da Cena:**
   - Crie um GameObject vazio na cena chamado `[TerrainManager]`.
   - Adicione o componente `ChunkGridManager`.
   - Arraste o `ScriptableObject` criado para o campo `Config` e o material para `VertexColorMaterial`.
   - Pressione Play ou use o menu de contexto (`Regerar Grid de Terreno`) no Inspector para gerar a malha instantaneamente.
4. **Verificação de Caminhabilidade:**
   - Certifique-se de que o jogador possui um `CharacterController` ou `Rigidbody` + `CapsuleCollider`.
   - Teste a locomoção sobre as faces da malha. O `MeshCollider` gerado em cada *chunk* garante colisão imediata e suporte a *Raycasts*.
