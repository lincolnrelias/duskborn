using System;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using Duskborn.Core;
using Duskborn.Effects;
using Duskborn.Gameplay;
using Duskborn.Gameplay.Classes;
using Duskborn.Gameplay.Loot;

namespace Duskborn.Gameplay.Player
{
    public class PlayerStats : NetworkBehaviour, IDamageable, IHealthProvider
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

        // ── Gear/weapon layer multipliers (forwarded for external callers) ────
        public float HPMultiplier             { get => _entity.HPMultiplier;             set => _entity.HPMultiplier = value; }
        public float DamageMultiplier         { get => _entity.DamageMultiplier;         set => _entity.DamageMultiplier = value; }
        public float MoveSpeedMultiplier      { get => _entity.MoveSpeedMultiplier;      set => _entity.MoveSpeedMultiplier = value; }
        public float AttackSpeedMultiplier    { get => _entity.AttackSpeedMultiplier;    set => _entity.AttackSpeedMultiplier = value; }
        public float CritChanceBonus          { get => _entity.CritChanceBonus;          set => _entity.CritChanceBonus = value; }
        public float IncomingDamageMultiplier { get => _entity.IncomingDamageMultiplier; set => _entity.IncomingDamageMultiplier = value; }

        // ── Buff additive layer (flat additions after gear base) ──────────────
        public float HPBuffAdditive          { get => _entity.HPBuffAdditive;          set => _entity.HPBuffAdditive = value; }
        public float DamageBuffAdditive      { get => _entity.DamageBuffAdditive;      set => _entity.DamageBuffAdditive = value; }
        public float MoveSpeedBuffAdditive   { get => _entity.MoveSpeedBuffAdditive;   set => _entity.MoveSpeedBuffAdditive = value; }
        public float AttackSpeedBuffAdditive { get => _entity.AttackSpeedBuffAdditive; set => _entity.AttackSpeedBuffAdditive = value; }

        // ── Buff multiplicative layer (compound factor: Factor *= 1+value) ────
        public float HPBuffFactor          { get => _entity.HPBuffFactor;          set => _entity.HPBuffFactor = value; }
        public float DamageBuffFactor      { get => _entity.DamageBuffFactor;      set => _entity.DamageBuffFactor = value; }
        public float MoveSpeedBuffFactor   { get => _entity.MoveSpeedBuffFactor;   set => _entity.MoveSpeedBuffFactor = value; }
        public float AttackSpeedBuffFactor { get => _entity.AttackSpeedBuffFactor; set => _entity.AttackSpeedBuffFactor = value; }

        // ── CritChance buff layers ────────────────────────────────────────────
        public float CritChanceBuffAdditive { get => _entity.CritChanceBuffAdditive; set => _entity.CritChanceBuffAdditive = value; }
        public float CritChanceBuffFactor   { get => _entity.CritChanceBuffFactor;   set => _entity.CritChanceBuffFactor = value; }

        // ── DamageReduction buff layers ───────────────────────────────────────
        public float IncomingDamageBuffAdditive { get => _entity.IncomingDamageBuffAdditive; set => _entity.IncomingDamageBuffAdditive = value; }
        public float IncomingDamageBuffFactor   { get => _entity.IncomingDamageBuffFactor;   set => _entity.IncomingDamageBuffFactor = value; }

        // Final damage fraction actually applied; accounts for all three reduction layers.
        public float EffectiveIncomingDamage => _entity.EffectiveIncomingDamage;

        // ── Buff container (for HP-delta tracking on stat changes) ───────────
        private PlayerBuffContainer _buffs;

        // ── HP (networked) ────────────────────────────────────────────────────
        private readonly SyncVar<float> _currentHP = new();

        public float CurrentHP => _currentHP.Value;
        public bool  IsAlive   => _currentHP.Value > 0f;

        public event Action<float, float> OnHealthChanged; // (current, max) — IHealthProvider
        public event Action<float, float> OnHPChanged;     // (current, max) — legacy, kept for compatibility
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
            OnHealthChanged?.Invoke(next, MaxHP);
        }

        private void Start()
        {
            _buffs = GetComponent<PlayerBuffContainer>();
            if (_buffs != null) _buffs.OnStatsApplied += HandleStatsApplied;
        }

        private void OnEnable()  => PlayerRegistry.Register(this);
        private void OnDisable() => PlayerRegistry.Unregister(this);

        private void OnDestroy()
        {
            if (_buffs != null) _buffs.OnStatsApplied -= HandleStatsApplied;
        }

        // Called by PlayerBuffContainer after every ApplyAll(). Maintains the invariant that
        // gaining MaxHP fills current HP by the same delta (so 150/190 + 20 max → 170/210).
        private void HandleStatsApplied(float prevMaxHP)
        {
            if (!IsServerStarted) return;
            float newMax = MaxHP;
            float delta  = newMax - prevMaxHP;
            if (Mathf.Approximately(delta, 0f)) return;

            float prevHP = _currentHP.Value;
            _currentHP.Value = delta > 0f
                ? Mathf.Min(newMax, prevHP + delta)  // gained max HP → fill by delta
                : Mathf.Min(prevHP, newMax);          // lost max HP → clamp, don't subtract

            // If current HP didn't change (e.g. already below new lower cap), SyncVar won't
            // fire — notify health bars manually so they reflect the new MaxHP ratio.
            if (Mathf.Approximately(_currentHP.Value, prevHP))
                OnHealthChanged?.Invoke(_currentHP.Value, newMax);
        }

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
            float actual = amount * EffectiveIncomingDamage;
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
