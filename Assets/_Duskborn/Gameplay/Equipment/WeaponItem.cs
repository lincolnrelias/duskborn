using System;
using System.Collections.Generic;
using InventorySystem.Core;
using Duskborn.Gameplay.ActionBar;
using UnityEngine;

namespace Duskborn.Gameplay.Equipment
{
    /// <summary>
    /// Base runtime weapon item. Sits in the action bar; stats apply only while selected.
    /// Subclass and override OnLeftClick / OnRightClick for custom weapon behaviour.
    /// </summary>
    public class WeaponItem : InventoryItemBase, ILeftClickAction, IRightClickAction
    {
        public IReadOnlyList<StatBonus> Bonuses { get; }
        public GameObject               Prefab  { get; }

        public override InventoryItemKind Kind => InventoryItemKind.Equipment;

        public WeaponItem(string id, string displayName, string description,
                          string iconId, IReadOnlyList<StatBonus> bonuses, GameObject prefab)
            : base(id, displayName, description, iconId)
        {
            Bonuses = bonuses ?? Array.Empty<StatBonus>();
            Prefab  = prefab;
        }

        // Standard melee swing. Cooldown is already set by PlayerCombat before this fires.
        public virtual void OnLeftClick(ActionContext ctx) => ctx.Combat.TriggerAttack();

        // Heavy overhead strike — 2× damage, 2× cooldown.
        public virtual void OnRightClick(ActionContext ctx) => ctx.Combat.TriggerHeavyAttack();
    }
}
