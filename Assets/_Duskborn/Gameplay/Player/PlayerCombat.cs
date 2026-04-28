using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Duskborn.Gameplay.Classes;
using Duskborn.Gameplay.Enemies;

namespace Duskborn.Gameplay.Player
{
    [RequireComponent(typeof(PlayerStats))]
    public class PlayerCombat : MonoBehaviour
    {
        [Header("Attack")]
        [SerializeField] private float     attackRange = 2f;
        [SerializeField] private LayerMask enemyLayer;

        public LayerMask EnemyLayer => enemyLayer;

        private const float CritMultiplier = 1.5f;

        private PlayerStats  _stats;
        private ClassAbility _classAbility;
        private float        _cooldown;

        private void Awake()
        {
            _stats        = GetComponent<PlayerStats>();
            _classAbility = GetComponent<ClassAbility>(); // null if no class component added
        }

        private void Update()
        {
            if (_cooldown > 0f) _cooldown -= Time.deltaTime;

            if (Input.GetMouseButtonDown(0)) TryAttack();
            if (Input.GetKeyDown(KeyCode.Q))  TryAbility();
        }

        // Also called by PlayerInput Send Messages when configured.
        public void OnAttack(InputValue _) => TryAttack();

        // ── Basic Attack ──────────────────────────────────────────────────────

        private void TryAttack()
        {
            if (!_stats.IsAlive || _cooldown > 0f) return;

            _cooldown = 1f / Mathf.Max(_stats.AttackSpeed, 0.01f);

            Vector3    origin = transform.position + transform.forward * (attackRange * 0.5f);
            Collider[] cols   = Physics.OverlapSphere(origin, attackRange, enemyLayer);

            var hitEnemies = new List<EnemyBase>();

            foreach (var col in cols)
            {
                var enemy = col.GetComponentInParent<EnemyBase>();
                if (enemy == null || !enemy.IsAlive) continue;

                bool  isCrit   = Random.value < _stats.CritChance;
                float damage   = _stats.Damage * (isCrit ? CritMultiplier : 1f);

                // Let the active class modify damage (e.g. Warrior passive stack).
                if (_classAbility != null)
                    damage = _classAbility.ModifyDamage(damage, enemy);

                enemy.TakeDamage(damage);
                hitEnemies.Add(enemy);

                Debug.Log($"[Combat] Hit {col.name} — {damage:F1}{(isCrit ? " CRIT" : "")}");
            }

            if (hitEnemies.Count > 0)
                _classAbility?.OnAttackCompleted(hitEnemies);
            else
                _classAbility?.OnAttackMissed();
        }

        // ── Class Ability (Q) ─────────────────────────────────────────────────

        private void TryAbility()
        {
            if (!_stats.IsAlive) return;
            _classAbility?.TryUseAbility();
        }

        // ─────────────────────────────────────────────────────────────────────

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            Gizmos.DrawWireSphere(
                transform.position + transform.forward * (attackRange * 0.5f), attackRange);
        }
    }
}
