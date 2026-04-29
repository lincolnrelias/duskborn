using UnityEngine;
using Duskborn.Core;

namespace Duskborn.Gameplay.Loot
{
    public class ResourceNode : MonoBehaviour
    {
        [SerializeField] private ResourceType type;
        [SerializeField] private int          hitsToBreak = 3;
        [SerializeField] private int          dropMin     = 1;
        [SerializeField] private int          dropMax     = 3;

        [Header("Outline")]
        [SerializeField] private string   outlineLayerName = "GreenOutline";
        [SerializeField] private Renderer outlineRenderer;

        private int  _hitsRemaining;
        private uint _outlineMask;
        private uint _baseMask;

        private void Awake()
        {
            _hitsRemaining = hitsToBreak;

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

        public void SetOutline(bool show)
        {
            if (outlineRenderer == null) return;
            outlineRenderer.renderingLayerMask = show ? _baseMask | _outlineMask : _baseMask;
        }

        public void Hit(ResourceInventory inventory)
        {
            if (_hitsRemaining <= 0 || inventory == null) return;

            _hitsRemaining--;
            Debug.Log($"[ResourceNode] {name} hit — {_hitsRemaining} hits remaining");

            if (_hitsRemaining <= 0)
            {
                int amount = GameSession.Instance != null
                    ? GameSession.Instance.RNG.Range(dropMin, dropMax + 1)
                    : Random.Range(dropMin, dropMax + 1);

                inventory.Add(type, amount);
                gameObject.SetActive(false);
            }
        }
    }
}
