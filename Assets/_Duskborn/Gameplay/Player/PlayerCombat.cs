using System.Collections.Generic;
using FishNet.Object;
using UnityEngine;
using UnityEngine.InputSystem;
using Duskborn.Core;
using Duskborn.Gameplay.Classes;
using Duskborn.Gameplay.Enemies;
using Duskborn.Gameplay.Loot;

namespace Duskborn.Gameplay.Player
{
    [RequireComponent(typeof(PlayerStats))]
    public class PlayerCombat : NetworkBehaviour
    {
        [Header("Attack")]
        [SerializeField] private float              attackRange   = 2f;
        [SerializeField] private LayerMask          enemyLayer;
        [SerializeField] private AttackRangeTrigger attackTrigger;

        public LayerMask EnemyLayer => enemyLayer;

        private const float CritMultiplier = 1.5f;

        private readonly HashSet<EnemyBase>    _enemiesInRange = new();
        private readonly HashSet<ResourceNode> _nodesInRange   = new();
        private ResourceNode _linkedNode;
        private ResourceNode _prevLinkedNode;

        private PlayerStats       _stats;
        private ClassAbility      _classAbility;
        private ResourceInventory _resourceInventory;
        private SphereCollider    _attackCollider;
        private float             _cooldown;

        private void Awake()
        {
            _stats             = GetComponent<PlayerStats>();
            _classAbility      = GetComponent<ClassAbility>();
            _resourceInventory = GetComponent<ResourceInventory>();
            _attackCollider    = attackTrigger != null ? attackTrigger.GetComponent<SphereCollider>() : null;
        }

        private void OnEnable()
        {
            if (attackTrigger == null) return;
            attackTrigger.OnEnter += HandleTriggerEnter;
            attackTrigger.OnExit  += HandleTriggerExit;
        }

        private void OnDisable()
        {
            if (attackTrigger != null)
            {
                attackTrigger.OnEnter -= HandleTriggerEnter;
                attackTrigger.OnExit  -= HandleTriggerExit;
            }
            foreach (var e in _enemiesInRange) e?.SetOutline(false);
            _enemiesInRange.Clear();
        }

        private void HandleTriggerEnter(Collider other)
        {
            var enemy = other.GetComponentInParent<EnemyBase>();
            if (enemy != null) { _enemiesInRange.Add(enemy); enemy.SetOutline(true); return; }

            var node = other.GetComponentInParent<ResourceNode>();
            if (node != null) _nodesInRange.Add(node);
        }

        private void HandleTriggerExit(Collider other)
        {
            var enemy = other.GetComponentInParent<EnemyBase>();
            if (enemy != null) { _enemiesInRange.Remove(enemy); enemy.SetOutline(false); return; }

            var node = other.GetComponentInParent<ResourceNode>();
            if (node != null) _nodesInRange.Remove(node);
        }

        private void Update()
        {
            if (!IsOwner) return;
            if (_cooldown > 0f) _cooldown -= Time.deltaTime;

            RefreshLinkedNode();
            if (_linkedNode != _prevLinkedNode)
            {
                _prevLinkedNode?.SetOutline(false);
                _linkedNode?.SetOutline(true);
                _prevLinkedNode = _linkedNode;
            }

            if (Input.GetMouseButtonDown(0)) TryAttack();
            if (Input.GetKeyDown(KeyCode.Q))  TryAbility();
        }

        public void OnAttack(InputValue _) => TryAttack();

        // ── Basic Attack ──────────────────────────────────────────────────────

        private void TryAttack()
        {
            if (!_stats.IsAlive || _cooldown > 0f) return;

            _cooldown = 1f / Mathf.Max(_stats.AttackSpeed, 0.01f);

            Vector3 origin = transform.position + transform.forward * (attackRange * 0.5f);

            if (_attackCollider != null)
            {
                Vector3 worldCenter = attackTrigger.transform.TransformPoint(_attackCollider.center);
                float   worldRadius = _attackCollider.radius * attackTrigger.transform.lossyScale.x;
                HitboxDebugger.Flash(worldCenter, worldRadius, Color.red);
            }

            Collider[] cols = Physics.OverlapSphere(origin, attackRange, enemyLayer);

            var hitEnemies = new List<EnemyBase>();

            foreach (var col in cols)
            {
                var enemy = col.GetComponentInParent<EnemyBase>();
                if (enemy == null || !enemy.IsAlive) continue;

                bool  isCrit = Random.value < _stats.CritChance;
                float damage = _stats.Damage * (isCrit ? CritMultiplier : 1f);

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

            _linkedNode?.Hit(_resourceInventory);
        }

        // ── Class Ability (Q) ─────────────────────────────────────────────────

        private void TryAbility()
        {
            if (!_stats.IsAlive) return;
            _classAbility?.TryUseAbility();
        }

        // ─────────────────────────────────────────────────────────────────────

        private void RefreshLinkedNode()
        {
            _linkedNode = null;
            float best = float.MaxValue;
            foreach (var node in _nodesInRange)
            {
                if (node == null || !node.gameObject.activeSelf) continue;
                float sq = (node.transform.position - transform.position).sqrMagnitude;
                if (sq < best) { best = sq; _linkedNode = node; }
            }
        }

        private void OnDrawGizmos()
        {
            if (!HitboxDebugger.IsEnabled) return;
            if (_attackCollider == null) return;
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            Vector3 worldCenter = attackTrigger.transform.TransformPoint(_attackCollider.center);
            float   worldRadius = _attackCollider.radius * attackTrigger.transform.lossyScale.x;
            Gizmos.DrawWireSphere(worldCenter, worldRadius);
        }
    }
}
