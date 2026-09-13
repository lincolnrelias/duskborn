using UnityEngine;

[CreateAssetMenu(fileName = "NovoTerrenoConfig", menuName = "Terreno Roguelike/Configuração de Terreno")]
public class LowPolyTerrainConfig : ScriptableObject
{
    [Header("Dimensões da Grid de Chunks")]
    [Tooltip("Quantidade de chunks no eixo X (ex: 1 para 1x3, 3 para 3x3, 5 para 5x5).")]
    [Range(1, 15)] public int chunksX = 3;

    [Tooltip("Quantidade de chunks no eixo Z (profundidade do mapa).")]
    [Range(1, 15)] public int chunksZ = 3;

    [Tooltip("Resolução de cada chunk em quads por lado (ex: 32 gera 32x32 quads = 2048 triângulos; 64 gera 64x64 quads = 8192 triângulos).")]
    [Range(4, 128)] public int chunkSize = 32;

    [Tooltip("Tamanho físico de cada quad em unidades de mundo/metros. Valores menores (ex: 0.5 a 1.0) aumentam a densidade/resolução dos detalhes geométricos.")]
    [Range(0.1f, 10f)] public float cellSize = 1.0f;

    [Header("Ruído Fractal (fBm) & Relevo")]
    [Tooltip("Semente numérica determinística para geração do relevo. A mesma semente garante a reprodução idêntica do mapa em todos os clientes.")]
    public int seed = 4242;

    [Tooltip("Escala de frequência base do ruído Perlin. Valores menores geram colinas amplas e suaves; valores maiores geram relevos densos e pontiagudos.")]
    [Range(0.01f, 0.3f)] public float noiseScale = 0.07f;

    [Tooltip("Número de camadas (oitavas) de ruído fractal combinadas. Mais oitavas adicionam detalhes e rugosidades mais finas.")]
    [Range(1, 6)] public int octaves = 3;

    [Tooltip("Persistência do relevo (0 a 1). Define o quanto a amplitude diminui a cada oitava sucessiva, controlando a força dos micro-detalhes.")]
    [Range(0.1f, 1f)] public float persistence = 0.5f;

    [Tooltip("Lacunaridade do ruído. Multiplicador de frequência aplicado a cada nova oitava (controla a densidade dos detalhes adicionais).")]
    [Range(1f, 4f)] public float lacunarity = 2.0f;

    [Tooltip("Altura máxima vertical (em metros) que os picos mais altos do terreno podem atingir.")]
    [Range(1f, 50f)] public float heightMultiplier = 12.0f;

    public enum MapBoundaryType
    {
        [Tooltip("Cria uma ilha estilizada com as bordas externas caindo suavemente no oceano.")]
        Island,
        [Tooltip("Cria um vale cercado por paredões íngremes e intransponíveis de montanha.")]
        ValleyWalls,
        [Tooltip("Sem limite radial (terreno infinito contínuo).")]
        None
    }

    [Header("Limites do Mapa (Boundary & World Bounds)")]
    [Tooltip("Tipo de fechamento das bordas do mapa para impedir queda no vácuo.")]
    public MapBoundaryType boundaryType = MapBoundaryType.Island;

    [Tooltip("Distância normalizada (0 a 1) do centro do mapa onde a borda começa a decair/subir (ex: 0.78).")]
    [Range(0.4f, 0.95f)] public float boundaryFalloffStart = 0.78f;

    [Tooltip("Largura normalizada da faixa de transição da borda até atingir a queda total (ex: 0.20).")]
    [Range(0.05f, 0.5f)] public float boundaryFalloffDistance = 0.20f;

    [Tooltip("Altura adicional das montanhas de borda quando boundaryType for ValleyWalls.")]
    [Range(10f, 60f)] public float boundaryWallHeight = 28f;

    /// <summary>
    /// Retorna a fração normalizada da borda protegida contra valores nulos/zero de assets antigos.
    /// </summary>
    public float EffectiveFalloffStartRatio => boundaryFalloffStart > 0.1f ? boundaryFalloffStart : 0.78f;

    /// <summary>
    /// Retorna a largura normalizada da transição de borda protegida contra valores nulos/zero de assets antigos.
    /// </summary>
    public float EffectiveFalloffDistanceRatio => boundaryFalloffDistance > 0.02f ? boundaryFalloffDistance : 0.20f;

    /// <summary>
    /// Retorna o raio jogável efetivo em metros antes do início do decaimento de borda.
    /// </summary>
    public float GetPlayableBoundaryRadius(float mapRadius)
    {
        return mapRadius * EffectiveFalloffStartRatio;
    }

    [Header("Clareira Central (Sanctuary Basin)")]
    [Tooltip("Raio ao redor de (0,0) onde o terreno é nivelado para a praça central e bancada.")]
    [Range(4f, 30f)] public float centralSanctuaryRadius = 14f;

    [Tooltip("Força do nivelamento na clareira central (0 = sem efeito, 1 = perfeitamente plana).")]
    [Range(0f, 1f)] public float centralSanctuaryFlattenStrength = 0.85f;

    [Tooltip("Deslocamento de altura da clareira central acima do nível da água.")]
    public float centralSanctuaryOffsetAboveWater = 1.8f;

    [Header("Efeito Platô / Patamares de Combate (Terracing)")]
    [Tooltip("Altura de cada patamar/degrau para combate (ex: 1.6m cria terraços bem delineados; 0 = contínuo).")]
    [Range(0f, 10f)] public float terraceStep = 1.6f;

    [Tooltip("Suavidade das rampas entre patamares de combate (0 = degraus retos verticais, 1 = rampas contínuas).")]
    [Range(0.1f, 1.0f)] public float terraceRampSmoothness = 0.45f;

    [Header("Biomas por Altura & Inclinação")]
    [Tooltip("Cota de altura (em metros) do nível da água.")]
    public float waterLevel = 2.0f;

    [Tooltip("Cor aplicada aos vértices do terreno situados abaixo do nível da água.")]
    public Color waterColor = new Color(0.12f, 0.38f, 0.72f);

    [Tooltip("Cor da praia/areia imediatamente acima da água.")]
    public Color sandColor = new Color(0.88f, 0.80f, 0.58f);

    [Tooltip("Cor da grama verdejante das planícies e clareiras.")]
    public Color grassColor = new Color(0.32f, 0.68f, 0.26f);

    [Tooltip("Cor da grama densa e escura de vales e bosques.")]
    public Color deepGrassColor = new Color(0.20f, 0.48f, 0.22f);

    [Tooltip("Cor das escarpas rochosas e encostas íngremes.")]
    public Color cliffColor = new Color(0.38f, 0.36f, 0.40f);

    [Tooltip("Cor da rocha dos platôs elevados.")]
    public Color rockColor = new Color(0.52f, 0.50f, 0.52f);

    [Tooltip("Cor da neve nos cumes e picos mais elevados.")]
    public Color snowColor = new Color(0.95f, 0.96f, 0.99f);

    [Tooltip("Ângulo limite de inclinação da face em graus. Faces mais íngremes viram escarpas/rocha.")]
    [Range(15f, 85f)] public float steepSlopeThreshold = 38.0f;

    [Header("Shading Estilizado & Oclusão")]
    [Tooltip("Intensidade de sombreamento de oclusão de cavidades e fendas entre facetas.")]
    [Range(0f, 0.8f)] public float facetAOIntensity = 0.25f;
}
