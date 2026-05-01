using InventorySystem.Core;
using UnityEngine;

namespace InventorySystem.Data
{
    [CreateAssetMenu(fileName = "MaterialDefinition", menuName = "Inventory/Material Definition")]
    public sealed class MaterialDefinition : ItemDefinitionBase
    {
        [SerializeField] private string category = "common";

        public override IInventoryItem CreateRuntimeItem()
        {
            return new MaterialItem(Id, DisplayName, Description, Icon != null ? Icon.name : string.Empty, category);
        }
    }
}
