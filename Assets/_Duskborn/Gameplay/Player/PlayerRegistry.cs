using System.Collections.Generic;
using UnityEngine;

namespace Duskborn.Gameplay.Player
{
    /// <summary>
    /// Static registry of all active players. Enemies query this instead of
    /// calling FindObjectsByType every frame.
    /// </summary>
    public static class PlayerRegistry
    {
        private static readonly List<PlayerStats> _players = new List<PlayerStats>();

        public static IReadOnlyList<PlayerStats> All => _players;

        public static void Register(PlayerStats p)
        {
            if (!_players.Contains(p)) _players.Add(p);
        }

        public static void Unregister(PlayerStats p) => _players.Remove(p);

        public static void Clear() => _players.Clear();

        public static int AliveCount
        {
            get
            {
                int count = 0;
                foreach (var p in _players) if (p.IsAlive) count++;
                return count;
            }
        }

        public static Transform FindNearest(Vector3 from)
        {
            Transform nearest = null;
            float nearestSqrDist = float.MaxValue;
            for (int i = 0; i < _players.Count; i++)
            {
                var p = _players[i];
                if (p == null || !p.IsAlive) continue;
                float sqrD = (from - p.transform.position).sqrMagnitude;
                if (sqrD < nearestSqrDist)
                {
                    nearestSqrDist = sqrD;
                    nearest = p.transform;
                }
            }
            return nearest;
        }

        // Returns the player most isolated from its teammates (largest min-distance to any ally).
        public static Transform FindMostIsolated(Vector3 from)
        {
            Transform mostIsolated = null;
            float maxMinSqrDist = -1f;

            for (int i = 0; i < _players.Count; i++)
            {
                var p = _players[i];
                if (p == null || !p.IsAlive) continue;
                float minSqrDistToAlly = float.MaxValue;
                for (int j = 0; j < _players.Count; j++)
                {
                    var other = _players[j];
                    if (other == null || other == p || !other.IsAlive) continue;
                    float sqrD = (p.transform.position - other.transform.position).sqrMagnitude;
                    if (sqrD < minSqrDistToAlly) minSqrDistToAlly = sqrD;
                }
                // Solo player: treat as infinitely isolated
                if (minSqrDistToAlly == float.MaxValue) minSqrDistToAlly = 9999f * 9999f;
                if (minSqrDistToAlly > maxMinSqrDist)
                {
                    maxMinSqrDist = minSqrDistToAlly;
                    mostIsolated = p.transform;
                }
            }
            return mostIsolated ?? FindNearest(from);
        }
    }
}
