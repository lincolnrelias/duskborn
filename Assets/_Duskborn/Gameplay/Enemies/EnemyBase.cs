using System;
using FishNet;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.AI;
using Duskborn.Core;
using Duskborn.Gameplay.Loot;
using Duskborn.Gameplay.Player;

namespace Duskborn.Gameplay.Enemies
{
    [RequireComponent(typeof(NavMeshAgent))]
    public abstract class EnemyBase : NetworkBehaviour
    {
        [Header("Stats")]
        [SerializeField] protected float maxHP          = 30f;
        [SerializeField] protected float attackDamage   = 8f;
        [SerializeField] protected float attackRange    = 1.5f;
        [SerializeField] protected float attackInterval = 1f;
        [SerializeField] protected int   goldDropMin    = 1;
        [SerializeField] protected int   goldDropMax    = 3;

        [Header("Outline")]
        [SerializeField] private string   outlineLayerName = "RedOutline";
        [SerializeField] private Renderer outlineRenderer;

        protected NavMeshAgent Agent;
        protected Transform    CurrentTarget;
        protected float        AttackCooldown;

        private float _baseMaxHP;
        private uint  _outlineMask;
        private uint  _baseMask;

        private bool _warnedNoTarget;
        private bool _warnedNoNavMesh;

        private readonly SyncVar<float> _currentHP = new();

        public float CurrentHP => _currentHP.Value;
        public bool  IsAlive   => _currentHP.Value > 0f;

        public event Action<EnemyBase> OnDied;
        public event Action<int>       OnDropGold;

        protected virtual void Awake()
        {
            Agent      = GetComponent<NavMeshAgent>();
            _baseMaxHP = maxHP;
            _currentHP.Value = _baseMaxHP;

            if (outlineRenderer == null)
                outlineRenderer = GetComponentInChildren<Renderer>();

            if (outlineRenderer != null)
            {
                int layerIndex = RenderingLayerMask.NameToRenderingLayer(outlineLayerName);
                _outlineMask = layerIndex >= 0 ? (uint)(1 << layerIndex) : 0u;
                _baseMask    = outlineRenderer.renderingLayerMask & ~_outlineMask;
                outlineRenderer.renderingLayerMask = _baseMask;
            }
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            // Snap the NavMeshAgent to the NavMesh surface at the spawn position.
            if (Agent != null && Agent.isActiveAndEnabled)
                Agent.Warp(transform.position);
        }

        public void SetOutline(bool show)
        {
            if (outlineRenderer == null) return;
            outlineRenderer.renderingLayerMask = show ? _baseMask | _outlineMask : _baseMask;
        }

        protected virtual void Update()
        {
            if (!IsServerStarted || !IsSpawned) return;
            if (!IsAlive) return;

            AcquireTarget();

            if (CurrentTarget == null)
            {
                if (!_warnedNoTarget)
                {
                    Debug.LogWarning($"[{name}] No target found — PlayerRegistry may be empty.");
                    _warnedNoTarget = true;
                }
                return;
            }

            if (!Agent.isOnNavMesh)
            {
                if (!_warnedNoNavMesh)
                {
                    Debug.LogWarning($"[{name}] NavMeshAgent is not on a NavMesh.");
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
            HitboxDebugger.Flash(transform.position, attackRange, Color.cyan);
            CurrentTarget.GetComponent<PlayerStats>()?.TakeDamage(attackDamage);
        }

        public virtual void TakeDamage(float amount)
        {
            if (!IsServerStarted) return;
            if (!IsAlive) return;
            _currentHP.Value = Mathf.Max(0f, _currentHP.Value - amount);
            if (_currentHP.Value <= 0f) Die();
        }

        public void ApplyPlayerCountScaling(int playerCount)
        {
            float scaled = _baseMaxHP * (1f + (playerCount - 1) * 0.25f);
            maxHP            = scaled;
            _currentHP.Value = scaled;
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

            InstanceFinder.ServerManager.Despawn(NetworkObject, DespawnType.Destroy);
        }

        // Still called by pooling paths that pre-existed networking; no longer the primary spawn init.
        public virtual void ResetEnemy(Vector3 position)
        {
            OnDied           = null;
            OnDropGold       = null;
            SetOutline(false);
            CurrentTarget    = null;
            AttackCooldown   = 0f;
            _currentHP.Value = _baseMaxHP;
            maxHP            = _baseMaxHP;
            _warnedNoTarget  = false;
            _warnedNoNavMesh = false;
            transform.position = position;
        }
    }
}
