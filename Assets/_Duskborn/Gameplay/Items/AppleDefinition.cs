using InventorySystem.Core;
using InventorySystem.Data;
using UnityEngine;

namespace Duskborn.Gameplay.Items
{
    [CreateAssetMenu(fileName = "Apple", menuName = "Duskborn/Items/Apple Definition")]
    public sealed class AppleDefinition : ItemDefinitionBase
    {
        [SerializeField] private float healAmount = 10f;

        public override IInventoryItem CreateRuntimeItem()
            => new AppleItem(Id, DisplayName, Description, healAmount);
    }
}
