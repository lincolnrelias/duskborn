using System.Collections.Generic;
using UnityEngine;
using Duskborn.Gameplay.Player;

namespace Duskborn.Gameplay.Loot
{
    [RequireComponent(typeof(PlayerStats))]
    public class PlayerInventory : MonoBehaviour
    {
        public IReadOnlyList<ItemDefinition> Items => _items;

        private readonly List<ItemDefinition> _items = new();
        private PlayerStats _stats;

        private void Awake() => _stats = GetComponent<PlayerStats>();

        public void AddItem(ItemDefinition item)
        {
            _items.Add(item);
            ApplyAll();
            Debug.Log($"[Inventory] Picked up: {item.ItemName} ({item.Rarity}) — {item.EffectType} +{item.EffectValue}");
        }

        private void ApplyAll()
        {
            // Reset all item-driven multipliers before re-applying the full list.
            _stats.DamageMultiplier          = 1f;
            _stats.HPMultiplier              = 1f;
            _stats.MoveSpeedMultiplier       = 1f;
            _stats.AttackSpeedMultiplier     = 1f;
            _stats.CritChanceBonus           = 0f;
            _stats.IncomingDamageMultiplier  = 1f;

            foreach (var item in _items)
            {
                switch (item.EffectType)
                {
                    case ItemEffectType.BonusDamage:
                        _stats.DamageMultiplier += item.EffectValue;
                        break;
                    case ItemEffectType.BonusHP:
                        _stats.HPMultiplier += item.EffectValue;
                        break;
                    case ItemEffectType.BonusMoveSpeed:
                        _stats.MoveSpeedMultiplier += item.EffectValue;
                        break;
                    case ItemEffectType.BonusAttackSpeed:
                        _stats.AttackSpeedMultiplier += item.EffectValue;
                        break;
                    case ItemEffectType.BonusCritChance:
                        _stats.CritChanceBonus += item.EffectValue;
                        break;
                    case ItemEffectType.DamageReduction:
                        _stats.IncomingDamageMultiplier =
                            Mathf.Max(0.1f, _stats.IncomingDamageMultiplier - item.EffectValue);
                        break;
                }
            }
        }
    }
}
