using System.Collections.Generic;
using InventorySystem.Core;
using InventorySystem.Data;
using UnityEngine;

namespace Duskborn.Gameplay.Equipment
{
    [CreateAssetMenu(fileName = "WeaponDefinition", menuName = "Duskborn/Weapon Definition")]
    public sealed class WeaponDefinition : ItemDefinitionBase
    {
        [SerializeField] private GameObject      prefab;
        [SerializeField] private List<StatBonus> bonuses;

        public GameObject               Prefab  => prefab;
        public IReadOnlyList<StatBonus> Bonuses => bonuses;

        public override IInventoryItem CreateRuntimeItem()
        {
            string iconId = Icon != null ? Icon.name : string.Empty;
            return new WeaponItem(Id, DisplayName, Description, iconId, bonuses, prefab);
        }
    }
}
