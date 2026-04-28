using System.Collections.Generic;

namespace Duskborn.Gameplay.Enemies
{
    /// <summary>
    /// A single scheduled spawn event in the night timeline.
    /// </summary>
    public class SpawnEvent
    {
        public readonly float Timestamp;  // seconds into the night when this enemy should spawn
        public readonly EnemyType EnemyType;

        public SpawnEvent(float timestamp, EnemyType enemyType)
        {
            Timestamp  = timestamp;
            EnemyType  = enemyType;
        }

        public override string ToString() => $"[{Timestamp:F2}s] {EnemyType}";
    }

    /// <summary>
    /// The complete spawn schedule for one night. Generated once by TimelineGenerator
    /// and then observed frame-by-frame by WaveManager.
    /// Events are always sorted ascending by Timestamp.
    /// </summary>
    public class SpawnTimeline
    {
        public readonly IReadOnlyList<SpawnEvent> Events;
        public readonly float TotalBudgetSpent;
        public readonly int   NightNumber;

        public int TotalEnemies => Events.Count;

        public SpawnTimeline(List<SpawnEvent> events, float budgetSpent, int nightNumber)
        {
            events.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));
            Events          = events.AsReadOnly();
            TotalBudgetSpent = budgetSpent;
            NightNumber      = nightNumber;
        }

        public override string ToString() =>
            $"Night {NightNumber} timeline — {TotalEnemies} enemies, {TotalBudgetSpent} budget spent";
    }
}
