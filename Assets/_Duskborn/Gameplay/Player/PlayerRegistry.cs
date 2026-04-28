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
            float nearestDist = float.MaxValue;
            foreach (var p in _players)
            {
                if (!p.IsAlive) continue;
                float d = Vector3.Distance(from, p.transform.position);
                if (d < nearestDist) { nearestDist = d; nearest = p.transform; }
            }
            return nearest;
        }

        // Returns the player most isolated from its teammates (largest min-distance to any ally).
        public static Transform FindMostIsolated(Vector3 from)
        {
            Transform mostIsolated = null;
            float maxMinDist = -1f;

            foreach (var p in _players)
            {
                if (!p.IsAlive) continue;
                float minDistToAlly = float.MaxValue;
                foreach (var other in _players)
                {
                    if (other == p || !other.IsAlive) continue;
                    float d = Vector3.Distance(p.transform.position, other.transform.position);
                    if (d < minDistToAlly) minDistToAlly = d;
                }
                // Solo player: treat as infinitely isolated
                if (minDistToAlly == float.MaxValue) minDistToAlly = 9999f;
                if (minDistToAlly > maxMinDist) { maxMinDist = minDistToAlly; mostIsolated = p.transform; }
            }
            return mostIsolated ?? FindNearest(from);
        }
    }
}
