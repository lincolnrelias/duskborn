using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;
using UnityEngine.InputSystem;
using Duskborn.Core;
using Duskborn.Gameplay.ActionBar;
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
        private ActionBarInstaller actionBarInstaller;

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

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!IsOwner) return;

            if (actionBarInstaller == null)
                actionBarInstaller = FindAnyObjectByType<ActionBarInstaller>();

            if (actionBarInstaller == null)
                DuskLog.Warn(LogChannel.ActionBar, "PlayerCombat: no ActionBarInstaller found in scene.");
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
            if (!IsOwner) return;

            var enemy = other.GetComponentInParent<EnemyBase>();
            if (enemy != null) { _enemiesInRange.Add(enemy); enemy.SetOutline(true); return; }

            var node = other.GetComponentInParent<ResourceNode>();
            if (node != null) _nodesInRange.Add(node);
        }

        private void HandleTriggerExit(Collider other)
        {
            if (!IsOwner) return;

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

            if (Input.GetKeyDown(KeyCode.Q)) TryAbility();

            float scroll = Mouse.current?.scroll.ReadValue().y ?? 0f;
            if (scroll > 0f) actionBarInstaller?.Service.SelectPrevious();
            else if (scroll < 0f) actionBarInstaller?.Service.SelectNext();

            var kb = Keyboard.current;
            if (kb != null)
                for (int i = 0; i < 8; i++)
                    if (kb[(Key)(Key.Digit1 + i)].wasPressedThisFrame)
                        { actionBarInstaller?.Service.SelectSlot(i); break; }
        }

        // ── Primary Action (LMB) ─────────────────────────────────────────────

        public void OnAttack(InputValue _)
        {
            if (!IsOwner) return;
            TryPrimaryAction();
        }

        private void TryPrimaryAction()
        {
            if (!_stats.IsAlive || _cooldown > 0f) return;

            var item = actionBarInstaller?.Service.GetSelectedItem();
            if (item is ILeftClickAction action)
            {
                _cooldown = 1f / Mathf.Max(_stats.AttackSpeed, 0.01f);
                action.OnLeftClick(BuildContext());
            }
            else
            {
                TryAttack();
            }
        }

        // ── Secondary Action (RMB) ───────────────────────────────────────────

        public void OnUseSecondary(InputValue _)
        {
            if (!IsOwner) return;
            TrySecondaryAction();
        }

        private void TrySecondaryAction()
        {
            if (!_stats.IsAlive) return;
            var item = actionBarInstaller?.Service.GetSelectedItem();
            (item as IRightClickAction)?.OnRightClick(BuildContext());
        }

        // ── Slot Navigation ──────────────────────────────────────────────────

        public void OnPrevious(InputValue _)
        {
            if (!IsOwner) return;
            actionBarInstaller?.Service.SelectPrevious();
        }

        public void OnNext(InputValue _)
        {
            if (!IsOwner) return;
            actionBarInstaller?.Service.SelectNext();
        }

        // ── Basic Attack (punch / fallback) ──────────────────────────────────

        private void TryAttack()
        {
            _cooldown = 1f / Mathf.Max(_stats.AttackSpeed, 0.01f);

            if (_attackCollider != null)
            {
                Vector3 worldCenter = attackTrigger.transform.TransformPoint(_attackCollider.center);
                float   worldRadius = _attackCollider.radius * attackTrigger.transform.lossyScale.x;
                HitboxDebugger.Flash(worldCenter, worldRadius, Color.red);
            }

            if (_linkedNode != null)
                RequestNodeHitRpc(_linkedNode.NetworkObject);

            RequestAttackRpc();
        }

        // Called by WeaponItem.OnLeftClick — cooldown already set by TryPrimaryAction.
        public void TriggerAttack()
        {
            if (_linkedNode != null) RequestNodeHitRpc(_linkedNode.NetworkObject);
            RequestAttackRpc();
        }

        // Called by WeaponItem.OnRightClick — 2× damage, 2× cooldown.
        public void TriggerHeavyAttack()
        {
            if (!_stats.IsAlive || _cooldown > 0f) return;
            _cooldown = 2f / Mathf.Max(_stats.AttackSpeed, 0.01f);
            RequestHeavyAttackRpc();
        }

        [ServerRpc]
        private void RequestHeavyAttackRpc()
        {
            Vector3    origin = transform.position + transform.forward * (attackRange * 0.5f);
            Collider[] cols   = Physics.OverlapSphere(origin, attackRange, enemyLayer);
            var hitEnemies = new List<EnemyBase>();
            foreach (var col in cols)
            {
                var enemy = col.GetComponentInParent<EnemyBase>();
                if (enemy == null || !enemy.IsAlive) continue;
                bool  isCrit  = Random.value < _stats.CritChance;
                float damage  = _stats.Damage * 2f * (isCrit ? CritMultiplier : 1f);
                if (_classAbility != null) damage = _classAbility.ModifyDamage(damage, enemy);
                enemy.TakeDamage(damage);
                hitEnemies.Add(enemy);
                DuskLog.Log(LogChannel.Combat, $"Heavy hit {col.name} — {damage:F1}{(isCrit ? " CRIT" : "")}");
            }
            if (hitEnemies.Count > 0) _classAbility?.OnAttackCompleted(hitEnemies);
            else _classAbility?.OnAttackMissed();
        }

        [ServerRpc]
        private void RequestAttackRpc()
        {
            Vector3    origin = transform.position + transform.forward * (attackRange * 0.5f);
            Collider[] cols   = Physics.OverlapSphere(origin, attackRange, enemyLayer);

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

                DuskLog.Log(LogChannel.Combat, $"Hit {col.name} — {damage:F1}{(isCrit ? " CRIT" : "")}");
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
            RequestAbilityRpc();
        }

        [ServerRpc]
        private void RequestAbilityRpc()
        {
            _classAbility?.TryUseAbility();
        }

        // ── Item Consume ──────────────────────────────────────────────────────

        public void RequestConsumeItem(int slotIndex, float healAmount)
        {
            if (!IsOwner) return;
            RequestConsumeItemRpc(slotIndex, healAmount);
        }

        [ServerRpc]
        private void RequestConsumeItemRpc(int slotIndex, float healAmount)
        {
            if (!_stats.IsAlive) return;
            _stats.Heal(healAmount);
            DuskLog.Log(LogChannel.ActionBar, $"Consumed item in slot {slotIndex} for {healAmount:F1} HP.");
            RpcConfirmConsume(Owner, slotIndex);
        }

        [TargetRpc]
        private void RpcConfirmConsume(NetworkConnection conn, int slotIndex)
        {
            actionBarInstaller?.Service.Service.RemoveItem(slotIndex);
            DuskLog.Log(LogChannel.ActionBar, $"Item consumed from slot {slotIndex}.");
        }

        // ── Resource Node ─────────────────────────────────────────────────────

        [ServerRpc]
        private void RequestNodeHitRpc(NetworkObject nodeObj)
        {
            if (nodeObj == null) return;
            var node = nodeObj.GetComponent<ResourceNode>();
            if (node == null) return;

            if (node.ServerHit(out string resourceId, out int amount))
            {
                RpcReceiveResources(Owner, resourceId, amount);
                nodeObj.Despawn();
            }
        }

        [TargetRpc]
        private void RpcReceiveResources(NetworkConnection conn, string resourceId, int amount)
        {
            _resourceInventory?.Add(resourceId, amount);
        }

        // ─────────────────────────────────────────────────────────────────────

        private ActionContext BuildContext()
        {
            var bar = actionBarInstaller?.Service;
            return new ActionContext(this, _stats, bar, bar?.SelectedIndex ?? 0);
        }

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
