using System;
using UnityEngine;

namespace Duskborn.Gameplay.Loot
{
    /// <summary>
    /// Tracks the shared in-run gold pool. Resets each run. Host-authoritative in Phase 9.
    /// </summary>
    public class GoldManager : MonoBehaviour
    {
        public static GoldManager Instance { get; private set; }

        public int Gold { get; private set; }

        public event Action<int> OnGoldChanged; // current total

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void ResetGold()
        {
            Gold = 0;
            OnGoldChanged?.Invoke(Gold);
        }

        public void AddGold(int amount)
        {
            Gold += amount;
            OnGoldChanged?.Invoke(Gold);
        }

        // Returns false and does nothing if insufficient funds.
        public bool TrySpend(int amount)
        {
            if (Gold < amount) return false;
            Gold -= amount;
            OnGoldChanged?.Invoke(Gold);
            return true;
        }
    }
}
