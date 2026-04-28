using System;
using UnityEngine;
using UnityEngine.AI;
using Duskborn.Core;
using Duskborn.Gameplay.Loot;
using Duskborn.Gameplay.Player;

namespace Duskborn.Gameplay.Enemies
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class EnemyBase : MonoBehaviour
    {
        [Header("Stats")]
        [SerializeField] protected float maxHP          = 30f;
        [SerializeField] protected float attackDamage   = 8f;
        [SerializeField] protected float attackRange    = 1.5f;
        [SerializeField] protected float attackInterval = 1f;
        [SerializeField] protected int   goldDropMin    = 1;
        [SerializeField] protected int   goldDropMax    = 3;

        protected NavMeshAgent Agent;
        protected Transform    CurrentTarget;
        protected float        AttackCooldown;

        private float _baseMaxHP;
        private float _currentHP;

        // One-shot diagnostics — logged once per spawn, not every frame.
        private bool _warnedNoTarget;
        private bool _warnedNoNavMesh;

        public float CurrentHP => _currentHP;
        public bool  IsAlive   => _currentHP > 0f;

        public event Action<EnemyBase> OnDied;
        public event Action<int>       OnDropGold;

        protected virtual void Awake()
        {
            Agent      = GetComponent<NavMeshAgent>();
            _baseMaxHP = maxHP;
            _currentHP = _baseMaxHP;
        }

        protected virtual void Update()
        {
            if (!IsAlive) return;

            AcquireTarget();

            if (CurrentTarget == null)
            {
                if (!_warnedNoTarget)
                {
                    Debug.LogWarning($"[{name}] No target found — PlayerRegistry may be empty. " +
                                     "Check that PlayerStats is on the player and the player is active.");
                    _warnedNoTarget = true;
                }
                return;
            }

            if (!Agent.isOnNavMesh)
            {
                if (!_warnedNoNavMesh)
                {
                    Debug.LogWarning($"[{name}] NavMeshAgent is not on a NavMesh. " +
                                     "Bake the NavMesh: Window > AI > Navigation > Bake.");
                    _warnedNoNavMesh = true;
                }
                return;
            }

            float dist = Vector3.Distance(transform.position, CurrentTarget.position);

            if (dist <= attackRange)
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
            CurrentTarget.GetComponent<PlayerStats>()?.TakeDamage(attackDamage);
        }

        public virtual void TakeDamage(float amount)
        {
            if (!IsAlive) return;
            _currentHP = Mathf.Max(0f, _currentHP - amount);
            if (_currentHP <= 0f) Die();
        }

        public void ApplyPlayerCountScaling(int playerCount)
        {
            float scaled = _baseMaxHP * (1f + (playerCount - 1) * 0.25f);
            maxHP      = scaled;
            _currentHP = scaled;
        }

        protected virtual void Die()
        {
            Agent.enabled = false;

            SeededRNG rng  = GameSession.Instance?.RNG;
            int goldAmount = rng != null
                ? rng.Range(goldDropMin, goldDropMax + 1)
                : UnityEngine.Random.Range(goldDropMin, goldDropMax + 1);

            GoldManager.Instance?.AddGold(goldAmount);
            OnDropGold?.Invoke(goldAmount);
            OnDied?.Invoke(this);
            gameObject.SetActive(false);
        }

        public virtual void ResetEnemy(Vector3 position)
        {
            // SetActive(true) FIRST so component enables run, then re-enable the agent.
            // Enabling Agent on an inactive GameObject is a no-op in Unity.
            OnDied      = null;
            OnDropGold  = null;
            CurrentTarget   = null;
            AttackCooldown  = 0f;
            _currentHP      = _baseMaxHP;
            maxHP           = _baseMaxHP;
            _warnedNoTarget  = false;
            _warnedNoNavMesh = false;

            transform.position = position;
            gameObject.SetActive(true);   // ← activate first
            Agent.enabled = true;         // ← then enable agent (now GameObject is active)
            Agent.Warp(position);         // ← snap agent to NavMesh surface at this position
        }
    }
}
