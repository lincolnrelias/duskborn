using System.Collections.Generic;
using Duskborn.Audio;
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
        // Index 0 = Q, index 1 = E, index 2 = R. Max 3 skills.
        [SerializeField] private WeaponSkill[]      skills;
        [SerializeField] private WeaponAudioProfile audioProfile;

        public GameObject               Prefab        => prefab;
        public IReadOnlyList<StatBonus> Bonuses       => bonuses;
        public WeaponBehaviour          Behaviour     => behaviour;
        public WeaponActionData[]       Actions       => actions;
        public WeaponSkill[]            Skills        => skills;
        public WeaponAudioProfile       AudioProfile  => audioProfile;

        public override IInventoryItem CreateRuntimeItem()
        {
            string iconId = Icon != null ? Icon.name : string.Empty;
            return new WeaponItem(Id, DisplayName, Description, iconId, bonuses, prefab, behaviour, actions, skills, audioProfile);
        }
    }
}
