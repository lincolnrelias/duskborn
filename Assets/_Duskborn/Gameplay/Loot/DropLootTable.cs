using System.Collections.Generic;
using Duskborn.Core;
using UnityEngine;

namespace Duskborn.Gameplay.Loot
{
    [CreateAssetMenu(fileName = "DropTable_Name", menuName = "Duskborn/Drop Loot Table")]
    public class DropLootTable : ScriptableObject
    {
        public DropEntry[] entries = System.Array.Empty<DropEntry>();

        [Min(0)] public int goldMin = 1;
        [Min(0)] public int goldMax = 3;

        [Range(0f, 2f)]
        [Tooltip("Global multiplier applied to every entry's scalingBonus. Tune this to speed up or slow down rarity ramp.")]
        public float rarityScaleRate = 0.5f;

        // WoW-style: each entry is rolled independently against effectiveChance.
        // effectiveChance = Clamp01(baseChance + scalingBonus * nightProgress * rarityScaleRate)
        // nightProgress = 0 on night 1, 1 on night 7.
        public List<DropEntry> Roll(SeededRNG rng, int currentNight)
        {
            float nightProgress = Mathf.Clamp01((currentNight - 1) / 6f);
            var hits = new List<DropEntry>();

            foreach (var entry in entries)
            {
                if (entry == null || entry.itemDefinition == null) continue;
                float effective = Mathf.Clamp01(entry.baseChance + entry.scalingBonus * nightProgress * rarityScaleRate);
                bool dropped = rng != null ? rng.Chance(effective) : UnityEngine.Random.value < effective;
                if (dropped) hits.Add(entry);
            }

            return hits;
        }

        public int RollGold(SeededRNG rng) =>
            rng != null
                ? rng.Range(goldMin, goldMax + 1)
                : UnityEngine.Random.Range(goldMin, goldMax + 1);
    }
}
