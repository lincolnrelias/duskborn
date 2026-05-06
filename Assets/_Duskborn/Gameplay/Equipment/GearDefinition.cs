using System.Collections.Generic;
using InventorySystem.Core;
using InventorySystem.Data;
using UnityEngine;

namespace Duskborn.Gameplay.Equipment
{
    [CreateAssetMenu(fileName = "GearDefinition", menuName = "Duskborn/Gear Definition")]
    public sealed class GearDefinition : ItemDefinitionBase
    {
        [SerializeField] private EquipmentSlot   slot;
        [SerializeField] private List<StatBonus> bonuses;

        public EquipmentSlot            Slot    => slot;
        public IReadOnlyList<StatBonus> Bonuses => bonuses;

        public override IInventoryItem CreateRuntimeItem()
        {
            string iconId = Icon != null ? Icon.name : string.Empty;
            return new GearItem(Id, DisplayName, Description, iconId, slot, bonuses);
        }
    }
}
