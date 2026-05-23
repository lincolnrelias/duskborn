using System;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using Duskborn.Core;
using Duskborn.Effects;
using Duskborn.Gameplay;
using Duskborn.Gameplay.Classes;

namespace Duskborn.Gameplay.Player
{
    public class PlayerStats : NetworkBehaviour, IDamageable
    {
        [Header("Class")]
        [SerializeField] private ClassDefinition classDefinition;

        [Header("Stats")]
        [SerializeField] private EntityStats _entity = new();

        [Header("Damage Numbers")]
        [SerializeField] private DamageNumberConfig _damageNumberConfig;

        // ── Computed stats ────────────────────────────────────────────────────
        public float MaxHP          => _entity.MaxHP;
        public float MoveSpeed      => _entity.MoveSpeed;
        public float Damage         => _entity.Damage;
        public float AttackSpeed    => _entity.AttackSpeed;
        public float CritChance     => _entity.CritChance;
        public float CritMultiplier => _entity.CritMultiplier;

        // ── Runtime multipliers — forwarded so all callers compile unchanged ──
        public float HPMultiplier             { get => _entity.HPMultiplier;             set => _entity.HPMultiplier = value; }
        public float DamageMultiplier         { get => _entity.DamageMultiplier;         set => _entity.DamageMultiplier = value; }
        public float MoveSpeedMultiplier      { get => _entity.MoveSpeedMultiplier;      set => _entity.MoveSpeedMultiplier = value; }
        public float AttackSpeedMultiplier    { get => _entity.AttackSpeedMultiplier;    set => _entity.AttackSpeedMultiplier = value; }
        public float CritChanceBonus          { get => _entity.CritChanceBonus;          set => _entity.CritChanceBonus = value; }
        public float IncomingDamageMultiplier { get => _entity.IncomingDamageMultiplier; set => _entity.IncomingDamageMultiplier = value; }

        // ── HP (networked) ────────────────────────────────────────────────────
        private readonly SyncVar<float> _currentHP = new();

        public float CurrentHP => _currentHP.Value;
        public bool  IsAlive   => _currentHP.Value > 0f;

        public event Action<float, float> OnHPChanged; // (current, max)
        public event Action               OnDied;

        private void Awake()
        {
            if (classDefinition != null)
            {
                _entity.maxHP       = classDefinition.MaxHP;
                _entity.moveSpeed   = classDefinition.MoveSpeed;
                _entity.damage      = classDefinition.Damage;
                _entity.attackSpeed = classDefinition.AttackSpeed;
                DuskLog.Log(LogChannel.PlayerClass, $"Applied: {classDefinition.ClassName}");
            }
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
            _entity.maxHP       = maxHP;
            _entity.moveSpeed   = moveSpeed;
            _entity.damage      = damage;
            _entity.attackSpeed = attackSpeed;
            if (IsServerStarted)
                _currentHP.Value = MaxHP;
        }

        public void TakeDamage(float amount, bool isCrit = false)
        {
            if (!IsServerStarted) return;
            if (!IsAlive) return;
            float actual = amount * IncomingDamageMultiplier;
            _currentHP.Value = Mathf.Max(0f, _currentHP.Value - actual);
            RpcShowDamageNumber(transform.position, actual, isCrit);
            if (_currentHP.Value <= 0f) HandleDeath();
        }

        [ObserversRpc(RunLocally = true)]
        private void RpcShowDamageNumber(Vector3 pos, float amount, bool isCrit)
            => DamageNumberPool.Instance?.Get(pos, amount, isCrit, _damageNumberConfig);

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
