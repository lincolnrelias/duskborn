using System;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Duskborn.Gameplay.Loot
{
    public class GoldManager : NetworkBehaviour
    {
        public static GoldManager Instance { get; private set; }

        [SerializeField, Range(1f, 200f)] private float chestPriceEscalationPercent = 5f;

        private readonly SyncVar<int> _goldSync      = new();
        private readonly SyncVar<int> _chestCostSync = new(-1);

        public int Gold => _goldSync.Value;

        public event Action<int> OnGoldChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _goldSync.OnChange += (prev, next, asServer) => OnGoldChanged?.Invoke(next);
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            ResetGold();
        }

        public void ResetGold()
        {
            if (!IsServerStarted) return;
            _goldSync.Value      = 0;
            _chestCostSync.Value = -1;
        }

        public void AddGold(int amount)
        {
            if (!IsServerStarted) return;
            _goldSync.Value += amount;
        }

        public bool TrySpend(int amount)
        {
            if (!IsServerStarted) return false;
            if (_goldSync.Value < amount) return false;
            _goldSync.Value -= amount;
            return true;
        }

        public int GetChestCost(int baseCost) =>
            _chestCostSync.Value < 0 ? baseCost : _chestCostSync.Value;

        public void OnChestOpened(int paidCost)
        {
            if (!IsServerStarted) return;
            _chestCostSync.Value = Mathf.CeilToInt(paidCost * (1f + chestPriceEscalationPercent / 100f));
        }
    }
}
