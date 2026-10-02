using InventorySystem.Core;
using InventorySystem.Data;
using UnityEngine;

namespace Duskborn.Gameplay.Equipment
{
    public enum ConsumableEffectType
    {
        InstantHeal = 0,
        SpeedBuff = 1,
        DamageBuff = 2,
        ThornsBuff = 3
    }

    [CreateAssetMenu(fileName = "Consumable", menuName = "Duskborn/Equipment/Consumable Definition")]
    public class ConsumableDefinition : ItemDefinitionBase
    {
        [Header("Alchemical / Consumable Effect")]
        [SerializeField] private ConsumableEffectType effectType = ConsumableEffectType.InstantHeal;
        [SerializeField] private float effectValue = 60f;
        [Tooltip("Buff duration in seconds. 0 for an instant effect.")]
        [SerializeField] private float duration = 0f;
        [SerializeField] private int maxStack = 10;
        [SerializeField, TextArea] private string effectDescription = "Restores 60 health points.";

        public ConsumableEffectType EffectType => effectType;
        public float EffectValue => effectValue;
        public float Duration => duration;
        public int MaxStack => maxStack;
        public string EffectDescription => effectDescription;

        public override IInventoryItem CreateRuntimeItem()
        {
            string iconId = Icon != null ? Icon.name : string.Empty;
            return new ConsumableItem(Id, DisplayName, Description, iconId, effectType, effectValue, duration, maxStack);
        }
    }
}
