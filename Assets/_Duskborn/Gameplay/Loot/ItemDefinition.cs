using UnityEngine;

namespace Duskborn.Gameplay.Loot
{
    public enum ItemRarity { Common, Uncommon, Rare, Legendary, Cursed }

    public enum ItemEffectType
    {
        BonusDamage,
        BonusHP,
        BonusMoveSpeed,
        BonusAttackSpeed,
        BonusCritChance,
        DamageReduction,
    }

    [CreateAssetMenu(fileName = "Item_Name", menuName = "Duskborn/Item Definition")]
    public class ItemDefinition : ScriptableObject
    {
        public string       ItemName;
        public ItemRarity   Rarity;
        public ItemEffectType EffectType;
        [Tooltip("Additive value applied to the matching stat multiplier (e.g. 0.2 = +20%).")]
        public float        EffectValue;
    }
}
