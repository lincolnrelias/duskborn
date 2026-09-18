using Duskborn.Gameplay.Equipment;
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
        public Texture2D    Icon;
        public ItemRarity   Rarity;
        public ItemEffectType EffectType;
        // Multiplicative = 0, so existing assets that have no EffectMode field default to Multiplicative.
        [Tooltip("Additive: flat value added after gear base (e.g. 20 = +20 HP). Multiplicative: compound factor (e.g. 0.2 = ×1.2).")]
        public BonusMode      EffectMode;
        public float          EffectValue;
    }
}
