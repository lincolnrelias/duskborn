using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;
using UnityEngine.InputSystem;
using Duskborn.Core;
using Duskborn.Gameplay;
using Duskborn.Gameplay.ActionBar;
using Duskborn.Gameplay.Classes;
using Duskborn.Gameplay.Enemies;
using Duskborn.Gameplay.Equipment;
using Duskborn.Gameplay.Hotkeys;
using Duskborn.Gameplay.Loot;

namespace Duskborn.Gameplay.Player
{
    [RequireComponent(typeof(PlayerStats))]
    public class PlayerCombat : NetworkBehaviour, ICombatEntity
    {
        public Transform Transform => transform;
        [Header("Attack")]
        [SerializeField] private float              attackRange   = 2f;
        [SerializeField] private LayerMask          enemyLayer;
        [SerializeField] private AttackRangeTrigger attackTrigger;
        private ActionBarInstaller actionBarInstaller;

        public LayerMask EnemyLayer => enemyLayer;


        private readonly HashSet<EnemyBase>    _enemiesInRange = new();
        private readonly HashSet<ResourceNode> _nodesInRange   = new();
        private ResourceNode _linkedNode;
        private ResourceNode _prevLinkedNode;

        private PlayerStats        _stats;
        private ClassAbility       _classAbility;
        private ResourceInventory  _resourceInventory;
        private WeaponActionPlayer _weaponActionPlayer;
        private SphereCollider     _attackCollider;
        private float              _cooldown;
        private readonly float[]   _skillCooldowns = new float[3];

        // Cached delegates for stable Register/Unregister.
        private System.Action _onSkill0;
        private System.Action _onSkill1;
        private System.Action _onSkill2;

        private void Awake()
        {
            _stats              = GetComponent<PlayerStats>();
            _classAbility       = GetComponent<ClassAbility>();
            _resourceInventory  = GetComponent<ResourceInventory>();
            _weaponActionPlayer = GetComponent<WeaponActionPlayer>();
            _attackCollider     = attackTrigger != null ? attackTrigger.GetComponent<SphereCollider>() : null;

            _onSkill0 = () => TryWeaponSkill(0);
            _onSkill1 = () => TryWeaponSkill(1);
            _onSkill2 = () => TryWeaponSkill(2);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!IsOwner) return;

            if (actionBarInstaller == null)
                actionBarInstaller = FindAnyObjectByType<ActionBarInstaller>();

            if (actionBarInstaller == null)
                DuskLog.Warn(LogChannel.ActionBar, "PlayerCombat: no ActionBarInstaller found in scene.");

            var hk = HotkeyManager.Instance;
            if (hk != null)
            {
                hk.Register(HotkeyManager.Skill1, _onSkill0);
                hk.Register(HotkeyManager.Skill2, _onSkill1);
                hk.Register(HotkeyManager.Skill3, _onSkill2);
            }
            else
                DuskLog.Warn(LogChannel.ActionBar, "PlayerCombat: HotkeyManager not found in scene.");
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (!IsOwner) return;
            var hk = HotkeyManager.Instance;
            if (hk == null) return;
            hk.Unregister(HotkeyManager.Skill1, _onSkill0);
            hk.Unregister(HotkeyManager.Skill2, _onSkill1);
            hk.Unregister(HotkeyManager.Skill3, _onSkill2);
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

            for (int i = 0; i < 3; i++)
                if (_skillCooldowns[i] > 0f) _skillCooldowns[i] -= Time.deltaTime;

            RefreshLinkedNode();
            if (_linkedNode != _prevLinkedNode)
            {
                _prevLinkedNode?.SetOutline(false);
                _linkedNode?.SetOutline(true);
                _prevLinkedNode = _linkedNode;
            }

            float scroll = Mouse.current?.scroll.ReadValue().y ?? 0f;
            if (scroll > 0f) actionBarInstaller?.Service.SelectPrevious();
            else if (scroll < 0f) actionBarInstaller?.Service.SelectNext();

            var kb = Keyboard.current;
            if (kb != null)
                for (int i = 0; i < 8; i++)
                    if (kb[(Key)(Key.Digit1 + i)].wasPressedThisFrame)
                        { actionBarInstaller?.Service.SelectSlot(i); break; }
        }

        private bool IsActionLocked => _weaponActionPlayer != null && _weaponActionPlayer.IsPlaying;

        private void TryWeaponSkill(int index)
        {
            if (!IsOwner || !_stats.IsAlive) return;
            if (IsActionLocked) return;
            var weapon = actionBarInstaller?.Service.GetSelectedItem() as WeaponItem;
            if (weapon == null || index >= weapon.Skills.Count || weapon.Skills[index] == null) return;
            if (_skillCooldowns[index] > 0f)
            {
                DuskLog.Log(LogChannel.Combat, $"Skill {index} on cooldown ({_skillCooldowns[index]:F1}s).");
                return;
            }
            _skillCooldowns[index] = weapon.Skills[index].cooldown;
            var skill = weapon.Skills[index];
            if (_weaponActionPlayer != null)
                _weaponActionPlayer.PlaySkillAction(skill, BuildContext());
            else
                skill.Use(BuildContext());
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
            if (IsActionLocked) return;

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
            if (IsActionLocked) return;
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

        // ICombatEntity — fired by MeleeWeaponBehaviour at HitboxOpen.
        public void ExecuteBasicMelee()
        {
            if (_linkedNode != null) RequestNodeHitRpc(_linkedNode.NetworkObject);
            RequestAttackRpc();
        }

        public void ExecuteHeavyMelee()
        {
            if (!_stats.IsAlive || _cooldown > 0f) return;
            _cooldown = 2f / Mathf.Max(_stats.AttackSpeed, 0.01f);
            RequestHeavyAttackRpc();
        }

        // Called by CleaveSkill via ICombatEntity.
        public void ExecuteCleave(float range, float arcDegrees, float damageMultiplier)
        {
            if (!_stats.IsAlive) return;
            RequestCleaveRpc(range, arcDegrees, damageMultiplier);
        }

        [ServerRpc]
        private void RequestCleaveRpc(float range, float arcDegrees, float damageMultiplier)
        {
            HitboxDebugger.Flash(transform.position, range, new Color(1f, 0.6f, 0f));

            float      cosHalfArc = Mathf.Cos(arcDegrees * 0.5f * Mathf.Deg2Rad);
            Collider[] cols       = Physics.OverlapSphere(transform.position, range, enemyLayer);
            var        hitEnemies = new List<EnemyBase>();

            foreach (var col in cols)
            {
                Vector3 toEnemy = (col.transform.position - transform.position).normalized;
                if (Vector3.Dot(transform.forward, toEnemy) < cosHalfArc) continue;

                var enemy = col.GetComponentInParent<EnemyBase>();
                if (enemy == null || !enemy.IsAlive) continue;

                bool  isCrit = Random.value < _stats.CritChance;
                float damage = _stats.Damage * damageMultiplier * (isCrit ? _stats.CritMultiplier : 1f);
                if (_classAbility != null) damage = _classAbility.ModifyDamage(damage, enemy);
                enemy.TakeDamage(damage);
                hitEnemies.Add(enemy);
                DuskLog.Log(LogChannel.Combat, $"Cleave hit {col.name} — {damage:F1}{(isCrit ? " CRIT" : "")}");
            }

            if (hitEnemies.Count > 0) _classAbility?.OnAttackCompleted(hitEnemies);
            else                       _classAbility?.OnAttackMissed();
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
                float damage  = _stats.Damage * 2f * (isCrit ? _stats.CritMultiplier : 1f);
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
                float damage = _stats.Damage * (isCrit ? _stats.CritMultiplier : 1f);
                if (_classAbility != null) damage = _classAbility.ModifyDamage(damage, enemy);
                enemy.TakeDamage(damage);
                hitEnemies.Add(enemy);
                DuskLog.Log(LogChannel.Combat, $"Hit {col.name} — {damage:F1}{(isCrit ? " CRIT" : "")}");
            }
            if (hitEnemies.Count > 0) _classAbility?.OnAttackCompleted(hitEnemies);
            else                       _classAbility?.OnAttackMissed();
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

        private CombatContext BuildContext()
        {
            var bar = actionBarInstaller?.Service;
            return new CombatContext(this, _stats, bar, bar?.SelectedIndex ?? 0, _weaponActionPlayer);
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
