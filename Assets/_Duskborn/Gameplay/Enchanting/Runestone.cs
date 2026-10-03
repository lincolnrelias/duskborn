using System;
using System.Collections.Generic;
using Duskborn.Gameplay.Equipment;
using Duskborn.Gameplay.Loot;
using InventorySystem.Core;
using UnityEngine;

namespace Duskborn.Gameplay.Enchanting
{
    // Stable values: saved/replicated as a compact rune + level pair.
    public enum RuneKind { None, Flame, Venom, Storm, Stone, Frost, Blood, Hex, Radiance }

    [Serializable]
    public struct WeaponEtching
    {
        public RuneKind kind;
        public int level;
        public bool IsValid => kind >= RuneKind.Flame && kind <= RuneKind.Radiance && level >= 1 && level <= 3;
        public int StacksPerHit => IsValid ? level : 0;
        public int Packed => IsValid ? ((int)kind << 4) | level : 0;
        public static WeaponEtching Unpack(int value)
        {
            var result = new WeaponEtching { kind = (RuneKind)(value >> 4), level = value & 15 };
            return result.IsValid ? result : default;
        }
        public override string ToString() => IsValid ? $"{kind} {new[] { "", "I", "II", "III" }[level]}" : "Unetched";
    }

    public static class RuneCatalog
    {
        public const int StackCap = 12;
        public const float Duration = 6f;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RegisterIcons()
        {
            foreach (var stone in Resources.LoadAll<InventorySystem.Data.MaterialDefinition>("Runestones"))
                if (stone.Icon != null) ItemIconRegistry.Register(stone.Id, stone.Icon);
        }
        public static int AddStacks(int current, WeaponEtching rune) => rune.IsValid
            ? Mathf.Clamp(Mathf.Clamp(current, 0, StackCap) + rune.StacksPerHit, 0, StackCap) : Mathf.Clamp(current, 0, StackCap);
        public static bool ShouldProc(RuneKind kind, int stacks) => kind == RuneKind.Storm ? stacks >= 6 : kind == RuneKind.Frost && stacks >= 8;
        public static bool TryParse(string id, out WeaponEtching rune)
        {
            rune = default;
            if (string.IsNullOrEmpty(id) || !id.StartsWith("runestone_", StringComparison.Ordinal)) return false;
            var parts = id.Split('_');
            if (parts.Length != 3 || !Enum.TryParse(parts[1], true, out RuneKind kind) || !int.TryParse(parts[2], out int level)) return false;
            rune = new WeaponEtching { kind = kind, level = level }; return rune.IsValid && Id(kind, level) == id;
        }
        public static float TickDamage(RuneKind kind, int stacks) => Mathf.Clamp(stacks, 0, StackCap) * (kind switch
        { RuneKind.Flame => 2f, RuneKind.Venom => 1.5f, RuneKind.Blood or RuneKind.Radiance => 1f, _ => 0f });
        public static string Id(RuneKind kind, int level) => $"runestone_{kind.ToString().ToLowerInvariant()}_{level}";
        public static string Effect(RuneKind kind) => kind switch
        {
            RuneKind.Flame => "Ablaze: 2 damage per stack each second.",
            RuneKind.Venom => "Poisoned: 1.5 damage per stack each second; persists for 9 seconds.",
            RuneKind.Storm => "Charged: at 6 stacks, discharge for 18 damage and stagger; consumes charge.",
            RuneKind.Stone => "Burdened: movement slowed 4% per stack (maximum 48%).",
            RuneKind.Frost => "Chilled: movement slowed 5% per stack; at 8 stacks, freeze briefly and consume chill.",
            RuneKind.Blood => "Bleeding: 1 damage per stack each second.",
            RuneKind.Hex => "Cursed: incoming damage increased 3% per stack (maximum 36%).",
            RuneKind.Radiance => "Seared: 1 damage per stack each second; enemy damage reduced 2% per stack.",
            _ => ""
        };
        public static Color Color(RuneKind kind) => kind switch
        {
            RuneKind.Flame => new Color(1f, .32f, .06f),
            RuneKind.Venom => new Color(.36f, .95f, .12f),
            RuneKind.Storm => new Color(.65f, .8f, 1f),
            RuneKind.Stone => new Color(.75f, .55f, .27f),
            RuneKind.Frost => new Color(.2f, .9f, 1f),
            RuneKind.Blood => new Color(.9f, .08f, .2f),
            RuneKind.Hex => new Color(.7f, .25f, 1f),
            RuneKind.Radiance => new Color(1f, .88f, .38f),
            _ => UnityEngine.Color.white
        };
    }

    public static class RuneEtchingService
    {
        private static readonly HashSet<WeaponItem> BusyWeapons = new();
        // Operates on the runtime instance, never the shared weapon definition.
        public static bool TryEtch(ResourceInventory resources, IInventoryReader backpack,
            IInventoryReader actionBar, WeaponItem weapon, WeaponEtching rune)
        {
            if (resources == null || weapon == null || !rune.IsValid ||
                (!Contains(backpack, weapon) && !Contains(actionBar, weapon))) return false;
            if (weapon.Etching.kind == rune.kind && weapon.Etching.level >= rune.level) return false;
            if (!BusyWeapons.Add(weapon)) return false;
            try
            {
                if (!resources.TrySpend(RuneCatalog.Id(rune.kind, rune.level), 1)) return false;
                // Resource listeners may move/remove the target. Refund instead of etching a stale item.
                if (!Contains(backpack, weapon) && !Contains(actionBar, weapon))
                { resources.Add(RuneCatalog.Id(rune.kind, rune.level), 1); return false; }
                weapon.Etching = rune;
                return true;
            }
            finally { BusyWeapons.Remove(weapon); }
        }
        public static bool Contains(IInventoryReader inventory, IInventoryItem item)
        {
            if (inventory == null || item == null) return false;
            foreach (var slot in inventory.GetSlots()) if (ReferenceEquals(slot.Item, item)) return true;
            return false;
        }
    }
}
