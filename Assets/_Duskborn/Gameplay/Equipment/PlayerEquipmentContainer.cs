using System;
using Duskborn.Core;
using Duskborn.Gameplay.Loot;
using Duskborn.Gameplay.Player;
using UnityEngine;

namespace Duskborn.Gameplay.Equipment
{
    [RequireComponent(typeof(PlayerBuffContainer))]
    [RequireComponent(typeof(PlayerStats))]
    public class PlayerEquipmentContainer : MonoBehaviour
    {
        [Header("Starting Gear")]
        [SerializeField] private GearDefinition[] startingGear;

        // (slot, previousItem or null, newItem or null)
        public event Action<EquipmentSlot, GearItem, GearItem> OnEquipmentChanged;

        private PlayerStats         _stats;
        private PlayerBuffContainer _buffs;

        // Indexed by (int)EquipmentSlot — 12 slots total.
        private readonly GearItem[] _equipped = new GearItem[12];

        private void Start()
        {
            _stats = GetComponent<PlayerStats>();
            _buffs = GetComponent<PlayerBuffContainer>();
            // Register after Awake so PlayerBuffContainer is guaranteed initialised.
            _buffs.RegisterGearContributor(ApplyGearStats);
            EquipStartingGear();
        }

        private void EquipStartingGear()
        {
            if (startingGear == null || startingGear.Length == 0) return;
            bool any = false;
            foreach (var def in startingGear)
            {
                if (def == null) continue;
                var item = def.CreateRuntimeItem() as GearItem;
                if (item == null) continue;
                _equipped[(int)item.Slot] = item;
                DuskLog.Log(LogChannel.Inventory, $"Starting gear: '{item.DisplayName}' → slot {item.Slot}");
                any = true;
            }
            if (any) _buffs.ApplyAll();
        }

        /// <summary>
        /// Equips <paramref name="item"/> to its declared slot.
        /// If the slot was occupied, the displaced item is returned via <paramref name="previousItem"/>.
        /// </summary>
        public bool TryEquip(GearItem item, out GearItem previousItem)
        {
            if (item == null)
            {
                previousItem = null;
                return false;
            }

            int idx       = (int)item.Slot;
            previousItem  = _equipped[idx];
            _equipped[idx] = item;

            DuskLog.Log(LogChannel.Inventory,
                $"Equipped '{item.DisplayName}' → slot {item.Slot}" +
                (previousItem != null ? $" (replacing '{previousItem.DisplayName}')" : string.Empty));

            _buffs.ApplyAll();
            OnEquipmentChanged?.Invoke(item.Slot, previousItem, item);
            return true;
        }

        /// <summary>
        /// Unequips whatever is in <paramref name="slot"/>. Returns the removed item, or null if empty.
        /// </summary>
        public GearItem Unequip(EquipmentSlot slot)
        {
            int     idx     = (int)slot;
            GearItem removed = _equipped[idx];
            if (removed == null) return null;

            _equipped[idx] = null;
            DuskLog.Log(LogChannel.Inventory, $"Unequipped '{removed.DisplayName}' from slot {slot}");

            _buffs.ApplyAll();
            OnEquipmentChanged?.Invoke(slot, removed, null);
            return removed;
        }

        public GearItem GetEquipped(EquipmentSlot slot) => _equipped[(int)slot];

        // Called by PlayerBuffContainer.ApplyAll() after all buffs are accumulated.
        // Stats are already reset at this point — only add, never reset here.
        private void ApplyGearStats()
        {
            foreach (var gear in _equipped)
            {
                if (gear == null) continue;
                foreach (var bonus in gear.Bonuses)
                    ApplyBonus(bonus);
            }
        }

        private void ApplyBonus(StatBonus bonus)
        {
            switch (bonus.Type)
            {
                case StatType.HP:
                    _stats.HPMultiplier += bonus.Value;
                    break;
                case StatType.Damage:
                    _stats.DamageMultiplier += bonus.Value;
                    break;
                case StatType.MoveSpeed:
                    _stats.MoveSpeedMultiplier += bonus.Value;
                    break;
                case StatType.AttackSpeed:
                    _stats.AttackSpeedMultiplier += bonus.Value;
                    break;
                case StatType.CritChance:
                    _stats.CritChanceBonus += bonus.Value;
                    break;
                case StatType.DamageReduction:
                    _stats.IncomingDamageMultiplier =
                        Mathf.Max(0.1f, _stats.IncomingDamageMultiplier - bonus.Value);
                    break;
                case StatType.MiningResourceBonus:
                    _stats.MiningResourceBonus += bonus.Value;
                    break;
                case StatType.WoodcuttingResourceBonus:
                    _stats.WoodcuttingResourceBonus += bonus.Value;
                    break;
                default:
                    DuskLog.Warn(LogChannel.Inventory, $"Unhandled StatType: {bonus.Type}");
                    break;
            }
        }
    }
}
