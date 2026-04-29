using System;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using Duskborn.Core;

namespace Duskborn.Gameplay.Player
{
    public class PlayerStats : NetworkBehaviour
    {
        [Header("Base Stats")]
        [SerializeField] private float baseMaxHP      = 100f;
        [SerializeField] private float baseMoveSpeed  = 5f;
        [SerializeField] private float baseDamage     = 10f;
        [SerializeField] private float baseAttackSpeed = 1f;

        [HideInInspector] public float HPMultiplier             = 1f;
        [HideInInspector] public float DamageMultiplier         = 1f;
        [HideInInspector] public float MoveSpeedMultiplier      = 1f;
        [HideInInspector] public float AttackSpeedMultiplier    = 1f;
        [HideInInspector] public float CritChanceBonus          = 0f;
        [HideInInspector] public float IncomingDamageMultiplier = 1f;

        public float MaxHP       => baseMaxHP * HPMultiplier;
        public float MoveSpeed   => baseMoveSpeed * MoveSpeedMultiplier;
        public float Damage      => baseDamage * DamageMultiplier;
        public float AttackSpeed => baseAttackSpeed * AttackSpeedMultiplier;
        public float CritChance  => Mathf.Clamp01(CritChanceBonus);

        private readonly SyncVar<float> _currentHP = new();

        public float CurrentHP => _currentHP.Value;
        public bool  IsAlive   => _currentHP.Value > 0f;

        public event Action<float, float> OnHPChanged; // (current, max)
        public event Action OnDied;

        private void Awake()
        {
            _currentHP.Value = MaxHP;
            _currentHP.OnChange += OnCurrentHPSync;
        }

        private void OnCurrentHPSync(float prev, float next, bool asServer)
        {
            OnHPChanged?.Invoke(next, MaxHP);
        }

        private void OnEnable()  => PlayerRegistry.Register(this);
        private void OnDisable() => PlayerRegistry.Unregister(this);

        public void SetBaseStats(float maxHP, float moveSpeed, float damage, float attackSpeed = 1f)
        {
            baseMaxHP      = maxHP;
            baseMoveSpeed  = moveSpeed;
            baseDamage     = damage;
            baseAttackSpeed = attackSpeed;
            if (IsServerStarted)
                _currentHP.Value = MaxHP;
        }

        public void TakeDamage(float amount)
        {
            if (!IsServerStarted) return;
            if (!IsAlive) return;
            float actual = amount * IncomingDamageMultiplier;
            _currentHP.Value = Mathf.Max(0f, _currentHP.Value - actual);
            if (_currentHP.Value <= 0f) HandleDeath();
        }

        public void Heal(float amount)
        {
            if (!IsServerStarted) return;
            if (!IsAlive) return;
            _currentHP.Value = Mathf.Min(MaxHP, _currentHP.Value + amount);
        }

        public void RestoreToFull()
        {
            if (!IsServerStarted) return;
            _currentHP.Value = MaxHP;
        }

        private void HandleDeath()
        {
            OnDied?.Invoke();
            if (PlayerRegistry.AliveCount == 0)
                GameStateManager.Instance?.TriggerGameOver();
        }
    }
}
