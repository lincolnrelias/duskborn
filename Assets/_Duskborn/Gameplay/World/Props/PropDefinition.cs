using Duskborn.Core;
using UnityEngine;

namespace Duskborn.Gameplay.World
{
    [System.Serializable]
    public class ResourceClusterSettings
    {
        [Tooltip("Se ativado, este recurso surge em bolsões agrupados.")]
        public bool enableClustering = true;

        [Tooltip("Quantidade de aglomerados/bolsões deste recurso por chunk.")]
        [Range(0, 8)] public int clustersPerChunk = 2;

        [Tooltip("Quantidade de nós gerados dentro de um mesmo aglomerado.")]
        public Vector2Int nodesPerCluster = new Vector2Int(3, 6);

        [Tooltip("Raio de espalhamento do aglomerado em torno do seu centro.")]
        [Range(2f, 15f)] public float clusterRadius = 6.0f;

        [Tooltip("Distância mínima entre nós do mesmo aglomerado (evita sobreposição física).")]
        [Range(1.2f, 5f)] public float intraClusterSpacing = 2.4f;

        [Tooltip("Distância mínima de isolamento em relação a outros tipos de recursos.")]
        [Range(2f, 10f)] public float interClusterSpacing = 5.0f;
    }

    [CreateAssetMenu(fileName = "Prop_Name", menuName = "Duskborn/World/Prop Definition")]
    public class PropDefinition : ScriptableObject
    {
        [Tooltip("Nome identificador do prop (ex: Árvore, Rocha, Minério de Ferro, Fibra).")]
        public string propName = "Tree";

        [Tooltip("Prefab base a ser instanciado.")]
        public GameObject prefab;

        [Tooltip("Variações de modelo/prefab para este prop (se preenchido, sorteia entre o base e as variações).")]
        public GameObject[] prefabVariations;

        /// <summary>
        /// Sorteia deterministicamente um prefab entre o prefab base e suas variações.
        /// </summary>
        public GameObject GetRandomPrefab(SeededRNG rng = null)
        {
            if (prefabVariations != null && prefabVariations.Length > 0)
            {
                int total = 1 + prefabVariations.Length;
                int choice = rng != null ? rng.Range(0, total) : UnityEngine.Random.Range(0, total);
                if (choice > 0)
                {
                    var variant = prefabVariations[choice - 1];
                    if (variant != null) return variant;
                }
            }
            return prefab;
        }

        [Header("Configuração de Agrupamento (Clusters)")]
        public ResourceClusterSettings clusterSettings = new ResourceClusterSettings();

        [Header("Ocupação Espacial & Folhagem")]
        [Tooltip("Raio de bloqueio físico no solo (impede que folhagem e outros objetos colidam/atravessem).")]
        public float solidRadius = 1.0f;

        [Tooltip("Raio da copa/influência (arbustos e tufos de sombra se agrupam neste anel). 0 se não possuir copa.")]
        public float canopyRadius = 0.0f;

        [Header("Zoneamento Radial")]
        [Tooltip("Distância radial mínima (a partir do centro 0,0) onde este recurso começa a aparecer.")]
        public float minRadialDistance = 0f;

        [Tooltip("Distância radial máxima (a partir do centro 0,0) para este recurso. 0 = sem limite.")]
        public float maxRadialDistance = 0f;

        [Header("Densidade por Chunk (Fallback sem Clustering)")]
        [Tooltip("Quantidade mínima deste prop por chunk.")]
        [Range(0, 50)] public int minPerChunk = 2;

        [Tooltip("Quantidade máxima deste prop por chunk.")]
        [Range(0, 50)] public int maxPerChunk = 6;

        [Header("Agrupamento Orgânico (Clustering & Clareiras)")]
        [Tooltip("Se ativado, utiliza ruído de densidade para formar bosques/veios orgânicos, preservando clareiras abertas para combate.")]
        public bool useClustering = true;

        [Tooltip("Frequência espacial do ruído de agrupamento. Valores menores (ex: 0.035) geram agrupamentos maiores.")]
        [Range(0.01f, 0.2f)] public float clusterFrequency = 0.04f;

        [Tooltip("Limiar mínimo de densidade (0 a 1). Abaixo deste valor, o local é deixado como clareira aberta de combate.")]
        [Range(0.0f, 0.8f)] public float clusterThreshold = 0.38f;

        [Header("Condições de Terreno")]
        [Tooltip("Altitude mínima (Y) no relevo para permitir o spawn.")]
        public float minHeight = 2.5f;

        [Tooltip("Altitude máxima (Y) no relevo para permitir o spawn.")]
        public float maxHeight = 20.0f;

        [Tooltip("Inclinação máxima do terreno (em graus) para permitir o spawn.")]
        [Range(0f, 60f)] public float maxSlopeAngle = 25f;

        [Header("Variação de Escala e Rotação")]
        [Tooltip("Intervalo de escala aleatória uniforme (mínimo, máximo).")]
        public Vector2 scaleRange = new Vector2(0.85f, 1.25f);

        [Tooltip("Multiplicador aleatório de altura (eixo Y). (1, 1) mantém proporção perfeitamente uniforme.")]
        public Vector2 heightScaleMultiplier = new Vector2(1f, 1f);

        [Tooltip("Aplica rotação aleatória no eixo vertical Y (0 a 360 graus).")]
        public bool randomYRotation = true;

        [Tooltip("Alinha a orientação vertical do prop à normal da face do relevo.")]
        public bool alignToNormal = false;

        [Header("Espaçamento")]
        [Tooltip("Raio mínimo de distância em relação a outros props para evitar sobreposição.")]
        public float exclusionRadius = 2.0f;
    }
}
