using System;
using UnityEngine;
using UnityEngine.AI;
using Duskborn.Core;
using Duskborn.Gameplay.Player;

namespace Duskborn.Gameplay.Enemies
{
    /// <summary>
    /// Base class for all enemy types. Handles HP, NavMesh pathfinding, melee attack, and death.
    /// Subclasses override targeting and attack behavior.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class EnemyBase : MonoBehaviour
    {
        [Header("Stats")]
        [SerializeField] protected float maxHP = 30f;
        [SerializeField] protected float attackDamage = 8f;
        [SerializeField] protected float attackRange = 1.5f;
        [SerializeField] protected float attackInterval = 1f;
        [SerializeField] protected int goldDropMin = 1;
        [SerializeField] protected int goldDropMax = 3;

        protected NavMeshAgent Agent;
        protected Transform CurrentTarget;
        protected float AttackCooldown;

        // Stores the prefab-configured maxHP so scaling never compounds on pool reuse.
        private float _baseMaxHP;
        private float _currentHP;

        public float CurrentHP => _currentHP;
        public bool IsAlive => _currentHP > 0f;

        public event Action<EnemyBase> OnDied;
        public event Action<int> OnDropGold;

        protected virtual void Awake()
        {
            Agent = GetComponent<NavMeshAgent>();
            _baseMaxHP = maxHP;
            _currentHP = _baseMaxHP;
        }

        protected virtual void Update()
        {
            if (!IsAlive) return;

            AcquireTarget();
            if (CurrentTarget == null) return;

            float distToTarget = Vector3.Distance(transform.position, CurrentTarget.position);

            if (distToTarget <= attackRange)
            {
                Agent.ResetPath();
                TryAttack();
            }
            else
            {
                Agent.SetDestination(CurrentTarget.position);
            }

            AttackCooldown -= Time.deltaTime;
        }

        protected virtual void AcquireTarget()
        {
            // Uses PlayerRegistry cache — O(n players), not O(n scene objects).
            CurrentTarget = PlayerRegistry.FindNearest(transform.position);
        }

        protected virtual void TryAttack()
        {
            if (AttackCooldown > 0f) return;
            AttackCooldown = attackInterval;
            PerformAttack();
        }

        protected virtual void PerformAttack()
        {
            if (CurrentTarget == null) return;
            var stats = CurrentTarget.GetComponent<PlayerStats>();
            stats?.TakeDamage(attackDamage);
        }

        public virtual void TakeDamage(float amount)
        {
            if (!IsAlive) return;
            _currentHP = Mathf.Max(0f, _currentHP - amount);
            if (_currentHP <= 0f) Die();
        }

        /// <summary>
        /// Applies player-count HP scaling. Always calculates from _baseMaxHP — never compounds.
        /// </summary>
        public void ApplyPlayerCountScaling(int playerCount)
        {
            float scale = 1f + (playerCount - 1) * 0.25f;
            float scaledHP = _baseMaxHP * scale;
            maxHP = scaledHP;
            _currentHP = scaledHP;
        }

        protected virtual void Die()
        {
            Agent.enabled = false;

            SeededRNG rng = GameSession.Instance?.RNG;
            int goldAmount = rng != null
                ? rng.Range(goldDropMin, goldDropMax + 1)
                : UnityEngine.Random.Range(goldDropMin, goldDropMax + 1);

            OnDropGold?.Invoke(goldAmount);
            OnDied?.Invoke(this);
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Called by EnemyPool when recycling. Resets from _baseMaxHP so scaling is always fresh.
        /// </summary>
        public virtual void ResetEnemy(Vector3 position)
        {
            transform.position = position;
            _currentHP = _baseMaxHP;
            maxHP = _baseMaxHP;
            AttackCooldown = 0f;
            CurrentTarget = null;
            Agent.enabled = true;
            // Clear all OnDied subscribers except the pool's own handler (pool re-adds its own in CreateAndStore).
            OnDied = null;
            OnDropGold = null;
            gameObject.SetActive(true);
        }
    }
}
