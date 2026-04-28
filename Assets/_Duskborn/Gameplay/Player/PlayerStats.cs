using System;
using UnityEngine;

namespace Duskborn.Gameplay.Player
{
    /// <summary>
    /// Source-of-truth for all player combat stats.
    /// Class, items, and skills all write multipliers here; damage/heal reads final values.
    /// </summary>
    public class PlayerStats : MonoBehaviour
    {
        [Header("Base Stats")]
        [SerializeField] private float baseMaxHP = 100f;
        [SerializeField] private float baseMoveSpeed = 5f;
        [SerializeField] private float baseDamage = 10f;
        [SerializeField] private float baseAttackSpeed = 1f; // attacks per second

        // Multipliers applied by items/skills/class nodes (additive stacking)
        [HideInInspector] public float HPMultiplier = 1f;
        [HideInInspector] public float DamageMultiplier = 1f;
        [HideInInspector] public float MoveSpeedMultiplier = 1f;
        [HideInInspector] public float AttackSpeedMultiplier = 1f;
        [HideInInspector] public float CritChanceBonus = 0f;   // additive, 0–1
        [HideInInspector] public float IncomingDamageMultiplier = 1f; // < 1 = damage reduction

        // Final computed values
        public float MaxHP => baseMaxHP * HPMultiplier;
        public float MoveSpeed => baseMoveSpeed * MoveSpeedMultiplier;
        public float Damage => baseDamage * DamageMultiplier;
        public float AttackSpeed => baseAttackSpeed * AttackSpeedMultiplier;
        public float CritChance => Mathf.Clamp01(CritChanceBonus);

        public float CurrentHP { get; private set; }
        public bool IsAlive => CurrentHP > 0f;

        public event Action<float, float> OnHPChanged; // (current, max)
        public event Action OnDied;

        private void Awake() => CurrentHP = MaxHP;

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
            if (CurrentHP <= 0f) OnDied?.Invoke();
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
    }
}
