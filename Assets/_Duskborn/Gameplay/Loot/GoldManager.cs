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

        [SerializeField, Range(1f, 200f)] private float chestPriceEscalationPercent = 5f;

        // -1 = no chest opened yet; subsequent opens compound from the last paid integer price.
        private int _currentChestCost = -1;

        public event Action<int> OnGoldChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void ResetGold()
        {
            Gold              = 0;
            _currentChestCost = -1;
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

        // First call uses the chest's own base cost; every subsequent call compounds
        // from the last rounded integer price, so rounding error never resets.
        public int GetChestCost(int baseCost) =>
            _currentChestCost < 0 ? baseCost : _currentChestCost;

        // Call with the cost that was actually paid; next price compounds from that integer.
        public void OnChestOpened(int paidCost) =>
            _currentChestCost = Mathf.CeilToInt(paidCost * (1f + chestPriceEscalationPercent / 100f));
    }
}
