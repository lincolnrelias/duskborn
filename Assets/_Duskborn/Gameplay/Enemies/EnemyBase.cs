using System;
using System.Collections;
using FishNet;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.AI;
using Duskborn.Core;
using Duskborn.Gameplay;
using Duskborn.Gameplay.Equipment;
using Duskborn.Gameplay.Loot;
using Duskborn.Gameplay.Player;

namespace Duskborn.Gameplay.Enemies
{
    [RequireComponent(typeof(NavMeshAgent))]
    public abstract class EnemyBase : NetworkBehaviour, ICombatEntity
    {
        public Transform Transform => transform;
        [Header("Stats")]
        [SerializeField] private EntityStats _entity = new();

        [Header("Combat")]
        [SerializeField] protected float     attackRange = 1.5f;
        [SerializeField] protected LayerMask playerLayer;

        [Header("Weapon")]
        [SerializeField] private WeaponDefinition weapon;
        [SerializeField] private Transform        holdPoint;

        [Header("Loot")]
        [SerializeField] private int goldDropMin = 1;
        [SerializeField] private int goldDropMax = 3;

        [Header("Death")]
        [SerializeField] private float deathDelay = 1.5f;

        [Header("Animation")]
        [SerializeField] private Animator _animator;

        [Header("Outline")]
        [SerializeField] private string     outlineLayerName = "RedOutline";
        [SerializeField] private Renderer[] outlineRenderers;

        // ── Animator hashes ───────────────────────────────────────────────────
        private static readonly int HashVelocityX = Animator.StringToHash("VelocityX");
        private static readonly int HashVelocityY = Animator.StringToHash("VelocityY");
        private static readonly int HashDead      = Animator.StringToHash("Dead");

        // ── Stat accessors (delegate to EntityStats) ──────────────────────────
        public float MaxHP          => _entity.MaxHP;
        public float Damage         => _entity.Damage;
        public float AttackSpeed    => _entity.AttackSpeed;
        public float MoveSpeed      => _entity.MoveSpeed;
        public float CritChance     => _entity.CritChance;
        public float CritMultiplier => _entity.CritMultiplier;

        // Multipliers exposed for future buff/debuff systems.
        public float DamageMultiplier         { get => _entity.DamageMultiplier;         set => _entity.DamageMultiplier = value; }
        public float AttackSpeedMultiplier    { get => _entity.AttackSpeedMultiplier;    set => _entity.AttackSpeedMultiplier = value; }
        public float MoveSpeedMultiplier      { get => _entity.MoveSpeedMultiplier;      set => _entity.MoveSpeedMultiplier = value; }
        public float IncomingDamageMultiplier { get => _entity.IncomingDamageMultiplier; set => _entity.IncomingDamageMultiplier = value; }

        // ── Runtime state ─────────────────────────────────────────────────────
        protected NavMeshAgent Agent;
        protected Transform    CurrentTarget;
        protected float        MeleeCooldown;

        private WeaponActionPlayer _weaponActionPlayer;
        private WeaponItem         _weaponItem;
        private GameObject         _weaponInstance;
        private float[]            _skillCooldowns = Array.Empty<float>();
        private float    _baseMaxHP;
        private uint     _outlineMask;
        private uint[]   _rendererBaseMasks;
        private Vector3  _prevPosition;
        private Vector2  _smoothedVelocity;
        private bool     _warnedNoTarget;
        private bool     _warnedNoNavMesh;

        private readonly SyncVar<float> _currentHP = new();

        public float CurrentHP => _currentHP.Value;
        public bool  IsAlive   => _currentHP.Value > 0f;

        public event Action<EnemyBase> OnDied;
        public event Action<int>       OnDropGold;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        protected virtual void Awake()
        {
            Agent         = GetComponent<NavMeshAgent>();
            Agent.speed   = _entity.MoveSpeed;
            _baseMaxHP    = _entity.maxHP;
            _prevPosition = transform.position;
            _currentHP.Value = _baseMaxHP;
            _currentHP.OnChange += OnHPChanged;

            if (outlineRenderers == null || outlineRenderers.Length == 0)
                outlineRenderers = GetComponentsInChildren<Renderer>();

            int outlineLayerIndex = RenderingLayerMask.NameToRenderingLayer(outlineLayerName);
            _outlineMask       = outlineLayerIndex >= 0 ? (uint)(1 << outlineLayerIndex) : 0u;
            _rendererBaseMasks = new uint[outlineRenderers.Length];
            for (int i = 0; i < outlineRenderers.Length; i++)
            {
                _rendererBaseMasks[i] = outlineRenderers[i].renderingLayerMask & ~_outlineMask;
                outlineRenderers[i].renderingLayerMask = _rendererBaseMasks[i];
            }

            _weaponActionPlayer = GetComponent<WeaponActionPlayer>();
            _skillCooldowns     = new float[weapon?.Skills != null ? weapon.Skills.Length : 0];
            SpawnWeaponVisual();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            if (Agent != null && Agent.isActiveAndEnabled)
                Agent.Warp(transform.position);
        }

        // ── Per-frame ─────────────────────────────────────────────────────────

        protected virtual void Update()
        {
            UpdateAnimation();

            if (!IsServerStarted || !IsSpawned) return;
            if (!IsAlive) return;

            AcquireTarget();

            if (CurrentTarget == null)
            {
                if (!_warnedNoTarget)
                {
                    DuskLog.Warn(LogChannel.Enemy, $"{name}: No target found — PlayerRegistry may be empty.");
                    _warnedNoTarget = true;
                }
                return;
            }

            if (!Agent.isOnNavMesh)
            {
                if (!_warnedNoNavMesh)
                {
                    DuskLog.Warn(LogChannel.Enemy, $"{name}: NavMeshAgent is not on a NavMesh.");
                    _warnedNoNavMesh = true;
                }
                return;
            }

            MeleeCooldown -= Time.deltaTime;
            for (int i = 0; i < _skillCooldowns.Length; i++)
                if (_skillCooldowns[i] > 0f) _skillCooldowns[i] -= Time.deltaTime;

            bool actionLocked = _weaponActionPlayer != null && _weaponActionPlayer.IsPlaying;
            if (actionLocked)
            {
                Agent.ResetPath();
                return;
            }

            TryUseSkill();

            float dist = Vector3.Distance(transform.position, CurrentTarget.position);
            if (dist <= attackRange)
            {
                Agent.ResetPath();
                TryBasicMelee();
            }
            else
            {
                Agent.SetDestination(CurrentTarget.position);
            }
        }

        // ── AI ────────────────────────────────────────────────────────────────

        protected virtual void AcquireTarget()
        {
            CurrentTarget = PlayerRegistry.FindNearest(transform.position);
        }

        protected virtual bool TryUseSkill()
        {
            var skills = weapon?.Skills;
            if (skills == null || skills.Length == 0) return false;
            var ctx = BuildContext();
            for (int i = 0; i < skills.Length; i++)
            {
                if (skills[i] == null || _skillCooldowns[i] > 0f) continue;
                if (!skills[i].CanUse(ctx)) continue;
                _skillCooldowns[i] = skills[i].cooldown;
                if (_weaponActionPlayer != null)
                    _weaponActionPlayer.PlaySkillAction(skills[i], ctx);
                else
                    skills[i].Use(ctx);
                return true;
            }
            return false;
        }

        protected virtual void TryBasicMelee()
        {
            if (MeleeCooldown > 0f) return;
            MeleeCooldown = 1f / Mathf.Max(AttackSpeed, 0.01f);

            if (_weaponActionPlayer != null && _weaponItem?.Actions?.Length > 0)
                _weaponActionPlayer.PlayAction(0, _weaponItem, BuildContext());
            else
                PerformBasicMelee();
        }

        protected virtual void PerformBasicMelee()
        {
            if (CurrentTarget == null) return;
            HitboxDebugger.Flash(transform.position, attackRange, Color.cyan);
            bool  isCrit = UnityEngine.Random.value < CritChance;
            float dmg    = Damage * (isCrit ? CritMultiplier : 1f);
            CurrentTarget.GetComponent<PlayerStats>()?.TakeDamage(dmg);
            DuskLog.Log(LogChannel.Enemy, $"{name} melee hit for {dmg:F1}{(isCrit ? " CRIT" : "")}");
        }

        // ── ICombatEntity ─────────────────────────────────────────────────────

        public void ExecuteBasicMelee() => PerformBasicMelee();
        public void ExecuteHeavyMelee() => PerformBasicMelee();

        public void ExecuteCleave(float range, float arcDegrees, float damageMultiplier)
        {
            if (!IsServerStarted) return;
            HitboxDebugger.Flash(transform.position, range, new Color(0f, 1f, 0.5f));
            float cosHalfArc = Mathf.Cos(arcDegrees * 0.5f * Mathf.Deg2Rad);
            var   cols       = Physics.OverlapSphere(transform.position, range, playerLayer);

            foreach (var col in cols)
            {
                Vector3 toTarget = (col.transform.position - transform.position).normalized;
                if (Vector3.Dot(transform.forward, toTarget) < cosHalfArc) continue;

                var ps = col.GetComponentInParent<PlayerStats>();
                if (ps == null || !ps.IsAlive) continue;

                bool  isCrit = UnityEngine.Random.value < CritChance;
                float dmg    = Damage * damageMultiplier * (isCrit ? CritMultiplier : 1f);
                ps.TakeDamage(dmg);
                DuskLog.Log(LogChannel.Enemy, $"{name} cleave hit {col.name} for {dmg:F1}{(isCrit ? " CRIT" : "")}");
            }
        }

        // ── Damage / death ────────────────────────────────────────────────────

        public virtual void TakeDamage(float amount)
        {
            if (!IsServerStarted) return;
            if (!IsAlive) return;
            float actual = amount * IncomingDamageMultiplier;
            _currentHP.Value = Mathf.Max(0f, _currentHP.Value - actual);
            if (_currentHP.Value <= 0f) Die();
        }

        private void OnHPChanged(float prev, float next, bool asServer)
        {
            if (next <= 0f)
                _animator?.SetBool(HashDead, true);
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

            StartCoroutine(DespawnAfterDelay());
        }

        private IEnumerator DespawnAfterDelay()
        {
            yield return new WaitForSeconds(deathDelay);
            InstanceFinder.ServerManager.Despawn(NetworkObject, DespawnType.Destroy);
        }

        // ── Animation ─────────────────────────────────────────────────────────

        private void UpdateAnimation()
        {
            if (_animator == null) return;

            Vector3 worldVel = IsServerStarted && Agent != null && Agent.isOnNavMesh
                ? Agent.velocity
                : (transform.position - _prevPosition) / Time.deltaTime;

            _prevPosition = transform.position;

            Vector3 localVel  = transform.InverseTransformDirection(worldVel);
            float   normSpeed = Agent != null && Agent.speed > 0f ? Agent.speed : _entity.MoveSpeed;

            _smoothedVelocity = Vector2.Lerp(_smoothedVelocity,
                new Vector2(localVel.x / normSpeed, localVel.z / normSpeed),
                10f * Time.deltaTime);

            _animator.SetFloat(HashVelocityX, _smoothedVelocity.x);
            _animator.SetFloat(HashVelocityY, _smoothedVelocity.y);
        }

        // ── Outline ───────────────────────────────────────────────────────────

        public void SetOutline(bool show)
        {
            if (outlineRenderers == null) return;
            for (int i = 0; i < outlineRenderers.Length; i++)
            {
                if (outlineRenderers[i] == null) continue;
                outlineRenderers[i].renderingLayerMask = show
                    ? _rendererBaseMasks[i] | _outlineMask
                    : _rendererBaseMasks[i];
            }
        }

        // ── Scaling / pooling ─────────────────────────────────────────────────

        public void ApplyPlayerCountScaling(int playerCount)
        {
            float scaled     = _baseMaxHP * (1f + (playerCount - 1) * 0.25f);
            _entity.maxHP    = scaled;
            _currentHP.Value = scaled;
        }

        public virtual void ResetEnemy(Vector3 position)
        {
            OnDied         = null;
            OnDropGold     = null;
            CurrentTarget  = null;
            MeleeCooldown  = 0f;
            _warnedNoTarget  = false;
            _warnedNoNavMesh = false;
            _entity.maxHP    = _baseMaxHP;
            _currentHP.Value = _baseMaxHP;
            _entity.ResetMultipliers();

            for (int i = 0; i < _skillCooldowns.Length; i++) _skillCooldowns[i] = 0f;

            SetOutline(false);
            _animator?.SetBool(HashDead, false);
            transform.position = position;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void SpawnWeaponVisual()
        {
            if (_weaponInstance != null) { Destroy(_weaponInstance); _weaponInstance = null; }
            _weaponItem = weapon?.CreateRuntimeItem() as WeaponItem;
            var prefab  = weapon?.Prefab;
            if (prefab == null) return;
            var parent = holdPoint != null ? holdPoint : transform;
            _weaponInstance = Instantiate(prefab, parent);

            // Rebuild renderer list so weapon renderers are included in outline.
            outlineRenderers   = GetComponentsInChildren<Renderer>();
            _rendererBaseMasks = new uint[outlineRenderers.Length];
            for (int i = 0; i < outlineRenderers.Length; i++)
                _rendererBaseMasks[i] = outlineRenderers[i].renderingLayerMask & ~_outlineMask;
        }

        private CombatContext BuildContext() => new(this, CurrentTarget);
    }
}
