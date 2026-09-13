using System;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;
using Duskborn.Core;

namespace Duskborn.Gameplay.Crafting
{
    public class Workbench : NetworkBehaviour
    {
        [Header("Outline & Visuals")]
        [SerializeField] private string outlineLayerName = "GreenOutline";
        [SerializeField] private Renderer outlineRenderer;

        private uint _outlineMask;
        private uint _baseMask;

        public event Action<NetworkConnection> OnInteracted;

        private void Awake()
        {
            if (outlineRenderer == null)
                outlineRenderer = GetComponentInChildren<Renderer>();

            if (outlineRenderer != null)
            {
                int layerIndex = RenderingLayerMask.NameToRenderingLayer(outlineLayerName);
                _outlineMask = layerIndex >= 0 ? (uint)(1 << layerIndex) : 0u;
                _baseMask = outlineRenderer.renderingLayerMask & ~_outlineMask;
                outlineRenderer.renderingLayerMask = _baseMask;
            }
        }

        public void SetOutline(bool show)
        {
            if (outlineRenderer == null) return;
            outlineRenderer.renderingLayerMask = show ? _baseMask | _outlineMask : _baseMask;
        }

        public void ServerInteract(NetworkConnection requester)
        {
            if (!IsServerStarted) return;
            DuskLog.Log(LogChannel.Loot, $"Player interacted with Workbench at {transform.position}");
            OnInteracted?.Invoke(requester);
        }
    }
}
