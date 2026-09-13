using UnityEngine;
using Duskborn.Gameplay.Loot;

namespace Duskborn.Gameplay.World
{
    [CreateAssetMenu(fileName = "WorldPropsConfig", menuName = "Duskborn/World/World Props Config")]
    public class WorldPropsConfig : ScriptableObject
    {
        [Header("Clareira Central (Safe Spawn Zone)")]
        [Tooltip("Raio em torno de (0,0) onde não serão geradas árvores ou rochas densas.")]
        public float centerClearingRadius = 10f;

        [Tooltip("Prefab da bancada de trabalho inicial a ser posicionada na clareira central.")]
        public GameObject workbenchPrefab;

        [Tooltip("Prefab opcional para instanciar pontos de spawn de jogadores na clareira se não existirem na cena.")]
        public GameObject playerSpawnPointPrefab;

        [Header("Recursos Naturais (Resource Nodes)")]
        [Tooltip("Definição ecológica de árvores (Madeira).")]
        public PropDefinition treeProp;

        [Tooltip("Definição ecológica de rochas comuns (Pedra).")]
        public PropDefinition stoneProp;

        [Tooltip("Definição ecológica de minério de ferro (Iron Ore).")]
        public PropDefinition ironProp;

        [Tooltip("Definição ecológica de arbustos de fibra (Fiber).")]
        public PropDefinition fiberProp;

        [Tooltip("Props adicionais decorativos ou naturais (opcional).")]
        public PropDefinition[] extraProps;

        [Header("Configuração de Baús (Chests)")]
        [Tooltip("Prefab do baú interativo contendo Chest.cs.")]
        public GameObject chestPrefab;

        [Tooltip("Quantidade total de baús a serem distribuídos pelo mapa.")]
        [Range(1, 50)] public int totalChests = 8;

        [Tooltip("Tabela de loot para baús básicos (próximos ao centro / raio <= 50%).")]
        public LootTable basicLootTable;

        [Tooltip("Tabela de loot para baús raros (distantes do centro / raio > 50%).")]
        public LootTable rareLootTable;

        [Tooltip("Custo mínimo em ouro para os baús mais próximos do centro.")]
        public int minChestCost = 35;

        [Tooltip("Custo máximo em ouro para os baús mais distantes ou nas bordas.")]
        public int maxChestCost = 150;
    }
}
