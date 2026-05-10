using System.Collections.Generic;
using InventorySystem.Core;
using InventorySystem.Data;
using UnityEngine;

namespace Duskborn.Gameplay.Equipment
{
    [CreateAssetMenu(fileName = "WeaponDefinition", menuName = "Duskborn/Weapon Definition")]
    public sealed class WeaponDefinition : ItemDefinitionBase
    {
        [SerializeField] private GameObject       prefab;
        [SerializeField] private List<StatBonus>  bonuses;
        [SerializeField] private WeaponBehaviour  behaviour;
        // Index 0 = LMB action, index 1 = RMB action, etc.
        [SerializeField] private WeaponActionData[] actions;

        public GameObject               Prefab    => prefab;
        public IReadOnlyList<StatBonus> Bonuses   => bonuses;
        public WeaponBehaviour          Behaviour => behaviour;
        public WeaponActionData[]       Actions   => actions;

        public override IInventoryItem CreateRuntimeItem()
        {
            string iconId = Icon != null ? Icon.name : string.Empty;
            return new WeaponItem(Id, DisplayName, Description, iconId, bonuses, prefab, behaviour, actions);
        }
    }
}
