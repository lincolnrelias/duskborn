using System;
using UnityEngine;
using UnityEngine.AI;

namespace Duskborn.Gameplay.Enemies
{
    /// <summary>
    /// Base class for all enemy types. Handles HP, NavMesh pathfinding, melee attack, and death.
    /// Subclasses override targeting, attack behavior, and special AI patterns.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class EnemyBase : MonoBehaviour
    {
        [Header("Stats")]
        [SerializeField] protected float maxHP = 30f;
        [SerializeField] protected float attackDamage = 8f;
        [SerializeField] protected float attackRange = 1.5f;
        [SerializeField] protected float attackInterval = 1f; // seconds between attacks
        [SerializeField] protected int goldDropMin = 1;
        [SerializeField] protected int goldDropMax = 3;

        protected NavMeshAgent Agent;
        protected Transform CurrentTarget;
        protected float CurrentHP;
        protected float AttackCooldown;

        public bool IsAlive => CurrentHP > 0f;

        public event Action<EnemyBase> OnDied;
        public event Action<int> OnDropGold; // gold amount to drop

        protected virtual void Awake()
        {
            Agent = GetComponent<NavMeshAgent>();
            CurrentHP = maxHP;
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
            // Default: target nearest player. Subclasses can override for different logic.
            CurrentTarget = FindNearestPlayer();
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
            var stats = CurrentTarget.GetComponent<Duskborn.Gameplay.Player.PlayerStats>();
            stats?.TakeDamage(attackDamage);
        }

        public virtual void TakeDamage(float amount)
        {
            if (!IsAlive) return;
            CurrentHP = Mathf.Max(0f, CurrentHP - amount);
            if (CurrentHP <= 0f) Die();
        }

        // Scale HP for player count — called by WaveManager before spawning
        public void ApplyPlayerCountScaling(int playerCount)
        {
            float scale = 1f + (playerCount - 1) * 0.25f;
            maxHP *= scale;
            CurrentHP = maxHP;
        }

        protected virtual void Die()
        {
            Agent.enabled = false;
            int goldAmount = UnityEngine.Random.Range(goldDropMin, goldDropMax + 1);
            OnDropGold?.Invoke(goldAmount);
            OnDied?.Invoke(this);
            gameObject.SetActive(false); // returned to pool
        }

        protected Transform FindNearestPlayer()
        {
            // Finds all PlayerStats in scene; picks the nearest alive one.
            var players = FindObjectsByType<Duskborn.Gameplay.Player.PlayerStats>(FindObjectsSortMode.None);
            Transform nearest = null;
            float nearestDist = float.MaxValue;
            foreach (var p in players)
            {
                if (!p.IsAlive) continue;
                float d = Vector3.Distance(transform.position, p.transform.position);
                if (d < nearestDist) { nearestDist = d; nearest = p.transform; }
            }
            return nearest;
        }

        // Called by pool when recycling this object
        public virtual void ResetEnemy(Vector3 position)
        {
            transform.position = position;
            CurrentHP = maxHP;
            AttackCooldown = 0f;
            CurrentTarget = null;
            Agent.enabled = true;
            gameObject.SetActive(true);
        }
    }
}
