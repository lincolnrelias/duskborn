using System;
using UnityEngine;
using Duskborn.Core;

namespace Duskborn.Gameplay.Player
{
    /// <summary>
    /// Source-of-truth for all player combat stats.
    /// Registers with PlayerRegistry on enable; unregisters on disable.
    /// Fires OnDied which GameStateManager listens to for game-over detection.
    /// </summary>
    public class PlayerStats : MonoBehaviour
    {
        [Header("Base Stats")]
        [SerializeField] private float baseMaxHP = 100f;
        [SerializeField] private float baseMoveSpeed = 5f;
        [SerializeField] private float baseDamage = 10f;
        [SerializeField] private float baseAttackSpeed = 1f;

        // Additive multipliers written by items/skills/class nodes.
        [HideInInspector] public float HPMultiplier = 1f;
        [HideInInspector] public float DamageMultiplier = 1f;
        [HideInInspector] public float MoveSpeedMultiplier = 1f;
        [HideInInspector] public float AttackSpeedMultiplier = 1f;
        [HideInInspector] public float CritChanceBonus = 0f;
        [HideInInspector] public float IncomingDamageMultiplier = 1f; // 1 = no reduction, 0.9 = -10%

        public float MaxHP        => baseMaxHP * HPMultiplier;
        public float MoveSpeed    => baseMoveSpeed * MoveSpeedMultiplier;
        public float Damage       => baseDamage * DamageMultiplier;
        public float AttackSpeed  => baseAttackSpeed * AttackSpeedMultiplier;
        public float CritChance   => Mathf.Clamp01(CritChanceBonus);

        public float CurrentHP { get; private set; }
        public bool IsAlive => CurrentHP > 0f;

        public event Action<float, float> OnHPChanged; // (current, max)
        public event Action OnDied;

        private void Awake() => CurrentHP = MaxHP;

        private void OnEnable()  => PlayerRegistry.Register(this);
        private void OnDisable() => PlayerRegistry.Unregister(this);

        public void SetBaseStats(float maxHP, float moveSpeed, float damage)
        {
            baseMaxHP = maxHP;
            baseMoveSpeed = moveSpeed;
            baseDamage = damage;
            CurrentHP = MaxHP;
        }

        public void TakeDamage(float amount)
        {
            if (!IsAlive) return;
            float actual = amount * IncomingDamageMultiplier;
            CurrentHP = Mathf.Max(0f, CurrentHP - actual);
            OnHPChanged?.Invoke(CurrentHP, MaxHP);
            if (CurrentHP <= 0f) HandleDeath();
        }

        public void Heal(float amount)
        {
            if (!IsAlive) return;
            CurrentHP = Mathf.Min(MaxHP, CurrentHP + amount);
            OnHPChanged?.Invoke(CurrentHP, MaxHP);
        }

        public void RestoreToFull()
        {
            CurrentHP = MaxHP;
            OnHPChanged?.Invoke(CurrentHP, MaxHP);
        }

        private void HandleDeath()
        {
            OnDied?.Invoke();

            // If no players remain alive, trigger game over.
            if (PlayerRegistry.AliveCount == 0)
                GameStateManager.Instance?.TriggerGameOver();
        }
    }
}
