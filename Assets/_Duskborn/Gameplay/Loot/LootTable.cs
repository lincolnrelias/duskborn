using UnityEngine;

namespace Duskborn.Gameplay.Loot
{
    [CreateAssetMenu(fileName = "LootTable_Name", menuName = "Duskborn/Loot Table")]
    public class LootTable : ScriptableObject
    {
        public ItemDefinition[] Items;
    }
}
