using FishNet.Object;
using InventorySystem.Data;
using UnityEngine;
using Duskborn.Core;

namespace Duskborn.Gameplay.Loot
{
    public class ResourceNode : NetworkBehaviour
    {
        [SerializeField] private MaterialDefinition resourceDef;
        [SerializeField] private int hitsToBreak = 3;
        [SerializeField] private int dropMin     = 1;
        [SerializeField] private int dropMax     = 3;

        [Header("Outline")]
        [SerializeField] private string   outlineLayerName = "GreenOutline";
        [SerializeField] private Renderer outlineRenderer;

        private int  _hitsRemaining; // server-only
        private uint _outlineMask;
        private uint _baseMask;

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
            _hitsRemaining = hitsToBreak;
        }

        public void SetOutline(bool show)
        {
            if (outlineRenderer == null) return;
            outlineRenderer.renderingLayerMask = show ? _baseMask | _outlineMask : _baseMask;
        }

        /// <summary>
        /// Server-only. Returns true on the final hit and populates resourceId + amount.
        /// Caller is responsible for Despawn()-ing this NetworkObject after awarding resources.
        /// </summary>
        public bool ServerHit(out string resourceId, out int amount)
        {
            resourceId = resourceDef != null ? resourceDef.Id : string.Empty;
            amount     = 0;

            if (_hitsRemaining <= 0) return false;

            _hitsRemaining--;
            DuskLog.Log(LogChannel.Loot, $"{name} hit — {_hitsRemaining}/{hitsToBreak} remaining");

            if (_hitsRemaining > 0) return false;

            amount = GameSession.Instance != null
                ? GameSession.Instance.RNG.Range(dropMin, dropMax + 1)
                : Random.Range(dropMin, dropMax + 1);
            return true;
        }
    }
}
