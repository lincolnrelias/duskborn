using System.Collections.Generic;
using System.Text;
using Duskborn.Audio;
using Duskborn.Effects;
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
        [SerializeField] private List<TypeDamageModifier> typeModifiers;
        [SerializeField] private WeaponBehaviour  behaviour;
        // Index 0 = LMB action, index 1 = RMB action, etc.
        [SerializeField] private WeaponActionData[] actions;
        // Index 0 = Q, index 1 = E, index 2 = R. Max 3 skills.
        [SerializeField] private WeaponSkill[]      skills;
        [SerializeField] private WeaponAudioProfile  audioProfile;
        [SerializeField] private WeaponEffectProfile effectProfile;

        public GameObject               Prefab         => prefab;
        public IReadOnlyList<StatBonus> Bonuses        => bonuses;
        public IReadOnlyList<TypeDamageModifier> TypeModifiers => typeModifiers;
        public WeaponBehaviour          Behaviour      => behaviour;
        public WeaponActionData[]       Actions        => actions;
        public WeaponSkill[]            Skills         => skills;
        public WeaponAudioProfile       AudioProfile   => audioProfile;
        public WeaponEffectProfile      EffectProfile  => effectProfile;

        public override IInventoryItem CreateRuntimeItem()
        {
            string iconId = Icon != null ? Icon.name : string.Empty;
            string description = BuildDescription(Description, bonuses, typeModifiers);
            return new WeaponItem(Id, DisplayName, description, iconId, bonuses, typeModifiers, prefab, behaviour, actions, skills, audioProfile, effectProfile);
        }

        // Composes the displayed description from, in order: stat bonuses (line by line),
        // type-damage modifiers (line by line), then the hand-written flavour description —
        // each non-empty section separated by a blank line.
        private static string BuildDescription(string customDescription, IReadOnlyList<StatBonus> bonuses,
                                                IReadOnlyList<TypeDamageModifier> typeModifiers)
        {
            var sections = new List<string>();

            if (bonuses != null && bonuses.Count > 0)
            {
                var lines = new StringBuilder();
                foreach (var bonus in bonuses)
                    lines.AppendLine(bonus.FormatLine());
                sections.Add(lines.ToString().TrimEnd());
            }

            if (typeModifiers != null && typeModifiers.Count > 0)
            {
                var lines = new StringBuilder();
                foreach (var modifier in typeModifiers)
                    lines.AppendLine(modifier.FormatLine());
                sections.Add(lines.ToString().TrimEnd());
            }

            if (!string.IsNullOrEmpty(customDescription))
                sections.Add(customDescription);

            return string.Join("\n\n", sections);
        }
    }
}
