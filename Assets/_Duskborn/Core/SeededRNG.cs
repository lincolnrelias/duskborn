using System;
using UnityEngine;

namespace Duskborn.Core
{
    /// <summary>
    /// Deterministic RNG driven by a host-generated seed.
    /// All gameplay randomness must route through here — never call UnityEngine.Random directly.
    /// Identical seeds produce identical sequences across all clients.
    /// </summary>
    public class SeededRNG
    {
        private System.Random _rng;
        private int _seed;

        public int Seed => _seed;

        public SeededRNG(int seed)
        {
            _seed = seed;
            _rng = new System.Random(seed);
        }

        public void Reinitialize(int seed)
        {
            _seed = seed;
            _rng = new System.Random(seed);
        }

        // Returns a float in [0, 1)
        public float NextFloat() => (float)_rng.NextDouble();

        // Returns a float in [min, max)
        public float Range(float min, float max) => min + NextFloat() * (max - min);

        // Returns an int in [min, max) — max is exclusive
        public int Range(int min, int max) => _rng.Next(min, max);

        // Returns true with the given probability (0–1)
        public bool Chance(float probability) => NextFloat() < probability;

        // Picks a random element from an array
        public T Pick<T>(T[] array)
        {
            if (array == null || array.Length == 0)
                throw new ArgumentException("Cannot pick from empty array.");
            return array[Range(0, array.Length)];
        }

        // Weighted random pick — weights must sum > 0
        public int WeightedIndex(float[] weights)
        {
            float total = 0f;
            for (int i = 0; i < weights.Length; i++) total += weights[i];
            float roll = Range(0f, total);
            float cumulative = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                cumulative += weights[i];
                if (roll < cumulative) return i;
            }
            return weights.Length - 1;
        }

        // Shuffles an array in place (Fisher-Yates)
        public void Shuffle<T>(T[] array)
        {
            for (int i = array.Length - 1; i > 0; i--)
            {
                int j = Range(0, i + 1);
                (array[i], array[j]) = (array[j], array[i]);
            }
        }

        // Random point inside a circle of given radius
        public Vector3 InsideCircle(Vector3 center, float radius)
        {
            float angle = Range(0f, 360f) * Mathf.Deg2Rad;
            float dist = Mathf.Sqrt(NextFloat()) * radius;
            return center + new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
        }

        // Random point on the perimeter of a circle
        public Vector3 OnCirclePerimeter(Vector3 center, float radius)
        {
            float angle = Range(0f, 360f) * Mathf.Deg2Rad;
            return center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
        }
    }
}
