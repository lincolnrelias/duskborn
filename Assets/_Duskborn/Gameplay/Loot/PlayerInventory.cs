using System;
using System.Collections.Generic;
using Duskborn.Gameplay.Equipment;
using UnityEngine;
using Duskborn.Gameplay.Player;

namespace Duskborn.Gameplay.Loot
{
    [RequireComponent(typeof(PlayerStats))]
    public class PlayerBuffContainer : MonoBehaviour
    {
        public event Action<ItemDefinition> BuffAdded;

        // Fired after every ApplyAll(); float = MaxHP before recalculation (for HP-delta logic).
        public event Action<float> OnStatsApplied;

        private Action _gearContributor;
        public void RegisterGearContributor(Action contributor) => _gearContributor = contributor;

        private Action _weaponContributor;
        public void RegisterWeaponContributor(Action contributor) => _weaponContributor = contributor;

        public IReadOnlyList<ItemDefinition> Buffs => _buffs;

        private readonly List<ItemDefinition> _buffs = new();
        private PlayerStats _stats;
        private bool        _isApplying;

        private void Awake() => _stats = GetComponent<PlayerStats>();

        public void AddBuff(ItemDefinition item)
        {
            _buffs.Add(item);
            ApplyAll();
            BuffAdded?.Invoke(item);
            DuskLog.Log(LogChannel.Inventory,
                $"Collected: {item.ItemName} ({item.Rarity}) — {item.EffectType} [{item.EffectMode}] +{item.EffectValue}");
        }

        public void ApplyAll()
        {
            if (_isApplying) return;
            _isApplying = true;

            float prevMaxHP = _stats.MaxHP;

            // ── Reset all layers ───────────────────────────────────────────────
            _stats.HPMultiplier             = 1f;
            _stats.DamageMultiplier         = 1f;
            _stats.MoveSpeedMultiplier      = 1f;
            _stats.AttackSpeedMultiplier    = 1f;
            _stats.IncomingDamageMultiplier = 1f;
            _stats.CritChanceBonus          = 0f;

            _stats.HPBuffAdditive          = 0f;
            _stats.DamageBuffAdditive      = 0f;
            _stats.MoveSpeedBuffAdditive   = 0f;
            _stats.AttackSpeedBuffAdditive = 0f;

            _stats.HPBuffFactor          = 1f;
            _stats.DamageBuffFactor      = 1f;
            _stats.MoveSpeedBuffFactor   = 1f;
            _stats.AttackSpeedBuffFactor = 1f;

            _stats.CritChanceBuffAdditive    = 0f;
            _stats.CritChanceBuffFactor      = 1f;
            _stats.IncomingDamageBuffAdditive = 0f;
            _stats.IncomingDamageBuffFactor   = 1f;

            // ── Gear → weapon (base layer, only touch the gear multipliers) ───
            _gearContributor?.Invoke();
            _weaponContributor?.Invoke();

            // ── Pass 1: additive buffs (flat additions on top of gear base) ───
            foreach (var item in _buffs)
            {
                if (item.EffectMode != BonusMode.Additive) continue;
                switch (item.EffectType)
                {
                    case ItemEffectType.BonusHP:
                        _stats.HPBuffAdditive += item.EffectValue;
                        break;
                    case ItemEffectType.BonusDamage:
                        _stats.DamageBuffAdditive += item.EffectValue;
                        break;
                    case ItemEffectType.BonusMoveSpeed:
                        _stats.MoveSpeedBuffAdditive += item.EffectValue;
                        break;
                    case ItemEffectType.BonusAttackSpeed:
                        _stats.AttackSpeedBuffAdditive += item.EffectValue;
                        break;
                    case ItemEffectType.BonusCritChance:
                        // Flat addition to the crit accumulator (stacks with gear crit).
                        _stats.CritChanceBuffAdditive += item.EffectValue;
                        break;
                    case ItemEffectType.DamageReduction:
                        // Flat reduction stacks additively with gear reduction.
                        _stats.IncomingDamageBuffAdditive += item.EffectValue;
                        break;
                }
            }

            // ── Pass 2: multiplicative buffs (compound factor on top of base+additive) ──
            foreach (var item in _buffs)
            {
                if (item.EffectMode != BonusMode.Multiplicative) continue;
                switch (item.EffectType)
                {
                    case ItemEffectType.BonusHP:
                        _stats.HPBuffFactor *= (1f + item.EffectValue);
                        break;
                    case ItemEffectType.BonusDamage:
                        _stats.DamageBuffFactor *= (1f + item.EffectValue);
                        break;
                    case ItemEffectType.BonusMoveSpeed:
                        _stats.MoveSpeedBuffFactor *= (1f + item.EffectValue);
                        break;
                    case ItemEffectType.BonusAttackSpeed:
                        _stats.AttackSpeedBuffFactor *= (1f + item.EffectValue);
                        break;
                    case ItemEffectType.BonusCritChance:
                        // Scales the total crit chance (base + gear + additive) by a compound factor.
                        _stats.CritChanceBuffFactor *= (1f + item.EffectValue);
                        break;
                    case ItemEffectType.DamageReduction:
                        // Reduces the remaining incoming-damage fraction: Factor *= (1 - value).
                        _stats.IncomingDamageBuffFactor *= (1f - item.EffectValue);
                        break;
                }
            }

            _isApplying = false;
            OnStatsApplied?.Invoke(prevMaxHP);
        }
    }
}
