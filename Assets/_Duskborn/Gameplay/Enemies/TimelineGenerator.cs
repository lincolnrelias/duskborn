using System.Collections.Generic;
using UnityEngine;
using Duskborn.Core;

namespace Duskborn.Gameplay.Enemies
{
    /// <summary>
    /// Generates a SpawnTimeline from a NightDefinition.
    ///
    /// Algorithm:
    ///   1. Scale budget by player count.
    ///   2. Merge all pool entries into one flat list.
    ///   3. Spend budget: weighted-random pick among affordable entries until budget runs out.
    ///   4. Shuffle the resulting enemy list.
    ///   5. Distribute enemies across nightDuration with per-slot jitter.
    ///   6. Sort by timestamp and return a SpawnTimeline.
    /// </summary>
    public static class TimelineGenerator
    {
        /// <param name="def">Night definition with budget and pools.</param>
        /// <param name="playerCount">Used to scale budget up.</param>
        /// <param name="nightDuration">Total seconds the night lasts — events fill this window.</param>
        /// <param name="rng">Seeded RNG — must be the shared session RNG for determinism.</param>
        public static SpawnTimeline Generate(
            NightDefinition def,
            int playerCount,
            float nightDuration,
            SeededRNG rng)
        {
            float budget = def.BaseBudget * BudgetScale(playerCount);

            // Step 1: Merge all pool entries into one flat list.
            var allEntries = MergeEntries(def.Pools);
            if (allEntries.Count == 0)
            {
                Debug.LogWarning($"[TimelineGenerator] Night {def.NightNumber} has no valid pool entries.");
                return new SpawnTimeline(new List<SpawnEvent>(), 0f, def.NightNumber);
            }

            // Step 2: Spend budget — pick enemies until no budget remains or no enemy is affordable.
            var spawnList = SpendBudget(allEntries, budget, rng, out float spent);

            // Step 3: Shuffle for variety so the same enemy type isn't spawned in clumps.
            EnemyType[] shuffled = spawnList.ToArray();
            rng.Shuffle(shuffled);

            // Step 4: Distribute across nightDuration with jitter.
            var events = DistributeAcrossNight(shuffled, nightDuration, rng);

            Debug.Log($"[TimelineGenerator] Night {def.NightNumber}: {events.Count} enemies, " +
                      $"{spent:F0}/{budget:F0} budget spent.");

            return new SpawnTimeline(events, spent, def.NightNumber);
        }

        // ---------------------------------------------------------------------------

        private static float BudgetScale(int playerCount) =>
            1f + (playerCount - 1) * 0.4f;

        private static List<EnemySpawnPoolEntry> MergeEntries(EnemySpawnPool[] pools)
        {
            var merged = new List<EnemySpawnPoolEntry>();
            if (pools == null) return merged;
            foreach (var pool in pools)
            {
                if (pool == null || pool.Entries == null) continue;
                foreach (var entry in pool.Entries)
                    if (entry.Weight > 0f && entry.Cost > 0) merged.Add(entry);
            }
            return merged;
        }

        private static List<EnemyType> SpendBudget(
            List<EnemySpawnPoolEntry> allEntries,
            float budget,
            SeededRNG rng,
            out float spent)
        {
            var result = new List<EnemyType>();
            float remaining = budget;
            spent = 0f;

            // Cache cheapest entry cost so we know when to stop.
            int cheapest = int.MaxValue;
            foreach (var e in allEntries) if (e.Cost < cheapest) cheapest = e.Cost;

            while (remaining >= cheapest)
            {
                // Build weight array for entries we can still afford.
                var affordable = new List<EnemySpawnPoolEntry>(allEntries.Count);
                var weights    = new List<float>(allEntries.Count);
                foreach (var e in allEntries)
                {
                    if (e.Cost <= remaining) { affordable.Add(e); weights.Add(e.Weight); }
                }

                if (affordable.Count == 0) break;

                int idx = rng.WeightedIndex(weights.ToArray());
                EnemySpawnPoolEntry chosen = affordable[idx];

                result.Add(chosen.Type);
                remaining -= chosen.Cost;
                spent     += chosen.Cost;
            }

            return result;
        }

        private static List<SpawnEvent> DistributeAcrossNight(
            EnemyType[] shuffled,
            float nightDuration,
            SeededRNG rng)
        {
            var events = new List<SpawnEvent>(shuffled.Length);
            if (shuffled.Length == 0) return events;

            // Divide the night into equal slots, one per enemy.
            // Each enemy spawns somewhere within its slot (uniform jitter within the slot).
            // This prevents bunching while keeping spawns unpredictable.
            float slotSize = nightDuration / shuffled.Length;

            for (int i = 0; i < shuffled.Length; i++)
            {
                float slotStart = i * slotSize;
                float jitter    = rng.Range(0f, slotSize * 0.85f); // use up to 85% of slot
                float timestamp = Mathf.Min(slotStart + jitter, nightDuration - 0.1f);
                events.Add(new SpawnEvent(timestamp, shuffled[i]));
            }

            return events; // caller sorts before wrapping in SpawnTimeline
        }
    }
}
