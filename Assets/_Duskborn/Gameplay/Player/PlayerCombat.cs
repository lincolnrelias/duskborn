using UnityEngine;
using UnityEngine.InputSystem;
using Duskborn.Gameplay.Enemies;

namespace Duskborn.Gameplay.Player
{
    [RequireComponent(typeof(PlayerStats))]
    public class PlayerCombat : MonoBehaviour
    {
        [Header("Attack")]
        [SerializeField] private float     attackRange = 2f;
        [SerializeField] private LayerMask enemyLayer;

        private const float CritMultiplier = 1.5f;

        private PlayerStats _stats;
        private float       _cooldown;

        private void Awake() => _stats = GetComponent<PlayerStats>();

        private void Update()
        {
            if (_cooldown > 0f)
                _cooldown -= Time.deltaTime;

            // Direct input — reliable regardless of PlayerInput configuration.
            if (Input.GetMouseButtonDown(0))
                TryAttack();
        }

        // Also receives from PlayerInput (Send Messages) if that is configured.
        public void OnAttack(InputValue _) => TryAttack();

        private void TryAttack()
        {
            if (!_stats.IsAlive || _cooldown > 0f) return;

            _cooldown = 1f / Mathf.Max(_stats.AttackSpeed, 0.01f);

            Vector3    origin = transform.position + transform.forward * (attackRange * 0.5f);
            Collider[] hits   = Physics.OverlapSphere(origin, attackRange, enemyLayer);

            Debug.Log($"[Combat] Attack — {hits.Length} colliders in range.");

            foreach (var col in hits)
            {
                var enemy = col.GetComponent<EnemyBase>();
                if (enemy == null || !enemy.IsAlive) continue;

                bool  isCrit = Random.value < _stats.CritChance;
                float damage = _stats.Damage * (isCrit ? CritMultiplier : 1f);

                enemy.TakeDamage(damage);
                Debug.Log($"[Combat] Hit {col.name} for {damage:F1}{(isCrit ? " CRIT" : "")}");
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            Vector3 origin = transform.position + transform.forward * (attackRange * 0.5f);
            Gizmos.DrawWireSphere(origin, attackRange);
        }
    }
}
