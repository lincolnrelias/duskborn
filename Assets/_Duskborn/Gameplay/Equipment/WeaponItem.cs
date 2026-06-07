using System;
using System.Collections.Generic;
using Duskborn.Audio;
using Duskborn.Effects;
using InventorySystem.Core;
using Duskborn.Gameplay;
using Duskborn.Gameplay.ActionBar;
using Duskborn.Core;
using UnityEngine;

namespace Duskborn.Gameplay.Equipment
{
    /// <summary>
    /// Runtime weapon item. Sits in the action bar; stats apply only while selected.
    /// Click actions route through WeaponActionPlayer, which plays the animation clip
    /// and fires WeaponBehaviour events at the normalised times defined on WeaponActionData.
    /// </summary>
    public class WeaponItem : InventoryItemBase, ILeftClickAction, IRightClickAction
    {
        public IReadOnlyList<StatBonus>   Bonuses       { get; }
        public IReadOnlyList<TypeDamageModifier> TypeModifiers { get; }
        public GameObject                 Prefab        { get; }
        public WeaponBehaviour            Behaviour     { get; }
        public WeaponActionData[]         Actions       { get; }
        public IReadOnlyList<WeaponSkill> Skills        { get; }
        public WeaponAudioProfile         AudioProfile  { get; }
        public WeaponEffectProfile        EffectProfile { get; }

        public override InventoryItemKind Kind => InventoryItemKind.Equipment;

        public WeaponItem(string id, string displayName, string description,
                          string iconId, IReadOnlyList<StatBonus> bonuses,
                          IReadOnlyList<TypeDamageModifier> typeModifiers,
                          GameObject prefab, WeaponBehaviour behaviour,
                          WeaponActionData[] actions, WeaponSkill[] skills = null,
                          WeaponAudioProfile audioProfile = null,
                          WeaponEffectProfile effectProfile = null)
            : base(id, displayName, description, iconId)
        {
            Bonuses       = bonuses ?? Array.Empty<StatBonus>();
            TypeModifiers = typeModifiers ?? Array.Empty<TypeDamageModifier>();
            Prefab        = prefab;
            Behaviour     = behaviour;
            Actions       = actions ?? Array.Empty<WeaponActionData>();
            Skills        = skills  ?? Array.Empty<WeaponSkill>();
            AudioProfile  = audioProfile;
            EffectProfile = effectProfile;
        }

        public float GetTypeDamageMultiplier(TargetType targetTypes) =>
            TypeDamageModifier.GetBestMultiplier(TypeModifiers, targetTypes);

        public virtual void OnLeftClick(CombatContext ctx)
        {
            if (ctx.WeaponAnimator != null)
                ctx.WeaponAnimator.PlayAction(0, this, ctx);
            else
                DuskLog.Warn(LogChannel.ActionBar, $"'{DisplayName}': no WeaponActionPlayer on player.");
        }

        public virtual void OnRightClick(CombatContext ctx)
        {
            if (ctx.WeaponAnimator != null)
                ctx.WeaponAnimator.PlayAction(1, this, ctx);
            else
                DuskLog.Warn(LogChannel.ActionBar, $"'{DisplayName}': no WeaponActionPlayer on player.");
        }
    }
}
