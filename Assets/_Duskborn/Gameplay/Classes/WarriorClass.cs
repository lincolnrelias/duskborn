using System.Collections.Generic;
using UnityEngine;
using Duskborn.Gameplay.Enemies;

namespace Duskborn.Gameplay.Classes
{
    /// <summary>
    /// Warrior — melee frontliner.
    ///
    /// Passive: each consecutive hit on the same enemy stacks +8% damage (max 3×).
    ///          Resets on miss or target switch.
    ///
    /// Active (Q — Cleave): 180° arc strike hitting all enemies in front.
    ///          Short cooldown. Uses and preserves the passive stack.
    /// </summary>
    public class WarriorClass : ClassAbility
    {
        [Header("Passive")]
        [SerializeField] private float stackBonus    = 0.08f; // per stack
        [SerializeField] private int   maxStacks     = 3;

        [Header("Cleave")]
        [SerializeField] private float cleaveRange    = 3f;
        [SerializeField] private float cleaveCooldown = 4f;
        [SerializeField] private LayerMask enemyLayer;

        private EnemyBase _lastHitEnemy;
        private int       _hitStack;
        private float     _cleaveCooldownRemaining;

        private void Update()
        {
            if (_cleaveCooldownRemaining > 0f)
                _cleaveCooldownRemaining -= Time.deltaTime;
        }

        // ── Passive ──────────────────────────────────────────────────────────

        public override float ModifyDamage(float baseDamage, EnemyBase target)
        {
            int stack = (target == _lastHitEnemy) ? _hitStack : 0;
            return baseDamage * (1f + stackBonus * stack);
        }

        public override void OnAttackCompleted(List<EnemyBase> hitEnemies)
        {
            if (hitEnemies.Count == 0) return;

            EnemyBase primary = hitEnemies[0];
            if (primary == _lastHitEnemy)
                _hitStack = Mathf.Min(_hitStack + 1, maxStacks);
            else
            {
                _hitStack = 1;
                _lastHitEnemy = primary;
            }
        }

        public override void OnAttackMissed()
        {
            _hitStack = 0;
            _lastHitEnemy = null;
        }

        // ── Cleave ───────────────────────────────────────────────────────────

        public override void TryUseAbility()
        {
            if (_cleaveCooldownRemaining > 0f)
            {
                Debug.Log($"[Warrior] Cleave on cooldown ({_cleaveCooldownRemaining:F1}s)");
                return;
            }

            _cleaveCooldownRemaining = cleaveCooldown;

            Collider[] hits = Physics.OverlapSphere(transform.position, cleaveRange, enemyLayer);
            int count = 0;

            foreach (var col in hits)
            {
                // 180° arc — dot > 0 means the enemy is in the forward hemisphere.
                Vector3 toEnemy = (col.transform.position - transform.position).normalized;
                if (Vector3.Dot(transform.forward, toEnemy) <= 0f) continue;

                var enemy = col.GetComponent<EnemyBase>();
                if (enemy == null || !enemy.IsAlive) continue;

                float damage = ModifyDamage(Stats.Damage, enemy);
                enemy.TakeDamage(damage);
                count++;
            }

            Debug.Log($"[Warrior] Cleave hit {count} enemies.");
        }
    }
}
