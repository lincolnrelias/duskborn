using InventorySystem.Core;
using UnityEngine;

namespace InventorySystem.Data
{
    [CreateAssetMenu(fileName = "EquipmentDefinition", menuName = "Inventory/Equipment Definition")]
    public sealed class EquipmentDefinition : ItemDefinitionBase
    {
        [SerializeField] private int powerScore = 10;

        public override IInventoryItem CreateRuntimeItem()
        {
            return new EquipmentItem(Id, DisplayName, Description, Icon != null ? Icon.name : string.Empty, powerScore);
        }
    }
}
