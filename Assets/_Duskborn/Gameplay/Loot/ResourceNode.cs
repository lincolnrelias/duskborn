using System;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using Duskborn.Core;
using Duskborn.Effects;
using Duskborn.Gameplay;

namespace Duskborn.Gameplay.Loot
{
    public class ResourceNode : NetworkBehaviour, IDamageable, IHealthProvider, ITypedTarget
    {
        [SerializeField] private float maxHP = 30f;

        [Header("Material Type")]
        [SerializeField, TargetTypeFilter(TargetTypeMasks.NodeTypes)]
        private TargetType materialTypes;
        public TargetType Types => materialTypes;

        [Header("Damage Numbers")]
        [SerializeField] private DamageNumberConfig _damageNumberConfig;

        [Header("Outline")]
        [SerializeField] private string   outlineLayerName = "GreenOutline";
        [SerializeField] private Renderer outlineRenderer;

        private readonly SyncVar<float> _currentHP = new();
        private HitFlash _hitFlash;
        private uint _outlineMask;
        private uint _baseMask;

        public float CurrentHP => _currentHP.Value;
        public float MaxHP     => maxHP;
        public bool  IsAlive   => _currentHP.Value > 0f;

        public event Action<float, float> OnHealthChanged;
        public event Action              OnDepleted;

        private void Awake()
        {
            _currentHP.Value    = maxHP;
            _currentHP.OnChange += OnHPChanged;

            _hitFlash = GetComponent<HitFlash>();
            if (_hitFlash == null) _hitFlash = gameObject.AddComponent<HitFlash>();

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
            _currentHP.Value = maxHP;
        }

        private void OnHPChanged(float prev, float next, bool asServer)
        {
            OnHealthChanged?.Invoke(next, maxHP);
        }

        public void TakeDamage(float amount, bool isCrit = false)
        {
            if (!IsServerStarted || !IsAlive) return;
            _currentHP.Value = Mathf.Max(0f, _currentHP.Value - amount);
            RpcShowDamageNumber(transform.position, amount, isCrit);
            DuskLog.Log(LogChannel.Loot, $"{name}: -{amount:F1} HP → {_currentHP.Value:F1}/{maxHP}");
            if (_currentHP.Value <= 0f)
                OnDepleted?.Invoke();
        }

        [ObserversRpc(RunLocally = true)]
        private void RpcShowDamageNumber(Vector3 pos, float amount, bool isCrit)
        {
            DamageNumberPool.Instance?.Get(pos, amount, isCrit, _damageNumberConfig);
            if (_hitFlash != null) _hitFlash.Flash();
        }

        public void SetOutline(bool show)
        {
            if (outlineRenderer == null) return;
            outlineRenderer.renderingLayerMask = show ? _baseMask | _outlineMask : _baseMask;
        }
    }
}
