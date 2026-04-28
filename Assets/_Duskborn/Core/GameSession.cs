using UnityEngine;

namespace Duskborn.Core
{
    /// <summary>
    /// Singleton that holds session-wide state: seed, RNG instance, player count.
    /// Created in Bootstrap, persists across scene loads.
    /// </summary>
    public class GameSession : MonoBehaviour
    {
        public static GameSession Instance { get; private set; }

        public SeededRNG RNG { get; private set; }
        public int Seed { get; private set; }
        public int PlayerCount { get; set; } = 1;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void InitializeSeed(int seed)
        {
            Seed = seed;
            RNG = new SeededRNG(seed);
            Debug.Log($"[GameSession] Seed initialized: {seed}");
        }

        // Host calls this; clients receive seed via network and also call this.
        public void GenerateAndApplySeed()
        {
            // Use environment tick so the seed itself doesn't depend on UnityEngine.Random.
            int seed = System.Environment.TickCount;
            InitializeSeed(seed);
        }
    }
}
