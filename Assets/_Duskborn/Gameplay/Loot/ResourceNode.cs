using FishNet.Object;
using InventorySystem.Data;
using UnityEngine;
using Duskborn.Core;
using Duskborn.Gameplay;

namespace Duskborn.Gameplay.Loot
{
    public class ResourceNode : NetworkBehaviour, IDamageable
    {
        [SerializeField] private MaterialDefinition resourceDef;
        [SerializeField] private float maxHP    = 30f;
        [SerializeField] private int   dropMin  = 1;
        [SerializeField] private int   dropMax  = 3;

        [Header("Outline")]
        [SerializeField] private string   outlineLayerName = "GreenOutline";
        [SerializeField] private Renderer outlineRenderer;

        private float _currentHP; // server-only
        private uint  _outlineMask;
        private uint  _baseMask;

        public bool IsAlive => _currentHP > 0f;

        private void Awake()
        {
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
            _currentHP = maxHP;
        }

        public void TakeDamage(float amount)
        {
            if (!IsServerStarted || !IsAlive) return;
            _currentHP = Mathf.Max(0f, _currentHP - amount);
            DuskLog.Log(LogChannel.Loot, $"{name}: -{amount:F1} HP → {_currentHP:F1}/{maxHP}");
        }

        public bool TryGetDrops(out string resourceId, out int amount)
        {
            resourceId = resourceDef != null ? resourceDef.Id : string.Empty;
            amount     = GameSession.Instance != null
                ? GameSession.Instance.RNG.Range(dropMin, dropMax + 1)
                : Random.Range(dropMin, dropMax + 1);
            return !string.IsNullOrEmpty(resourceId);
        }

        public void SetOutline(bool show)
        {
            if (outlineRenderer == null) return;
            outlineRenderer.renderingLayerMask = show ? _baseMask | _outlineMask : _baseMask;
        }
    }
}
