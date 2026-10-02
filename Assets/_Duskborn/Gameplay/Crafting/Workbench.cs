using System;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;
using Duskborn.Core;

namespace Duskborn.Gameplay.Crafting
{
    /// <summary>
    /// Workbench component in Duskborn.
    /// Allows players to interact to open the crafting interface (Crafting UI)
    /// and forge basic tools and equipment such as Stone Axe and Stone Pickaxe.
    /// </summary>
    public class Workbench : NetworkBehaviour
    {
        [Header("Station Type")]
        [SerializeField] private CraftingStationType stationType = CraftingStationType.Workbench;
        [SerializeField] private string stationDisplayName = "Workbench";

        [Header("Outline & Visuals")]
        [SerializeField] private string outlineLayerName = "GreenOutline";
        [SerializeField] private Renderer[] outlineRenderers;

        [Header("Recipes")]
        [Tooltip("Recipes available at this workbench. Loads default recipes when empty.")]
        [SerializeField] private CraftingRecipe[] recipes;

        private uint _outlineMask;
        private uint _baseMask;

        public static Workbench CurrentOpenWorkbench { get; private set; }

        public event Action<NetworkConnection> OnInteracted;
        public event Action OnClientInteracted;

        public CraftingStationType StationType => stationType;
        public string StationDisplayName => !string.IsNullOrEmpty(stationDisplayName) ? stationDisplayName : "Workbench";
        public IReadOnlyList<CraftingRecipe> Recipes => recipes;

        public void Configure(CraftingStationType type, string label)
        {
            stationType = type;
            stationDisplayName = label;
        }

        private void Awake()
        {
            if (outlineRenderers == null || outlineRenderers.Length == 0)
                outlineRenderers = GetComponentsInChildren<Renderer>(true);
            // A scene station can receive effects while FishNet keeps it inactive,
            // before this Awake. Effects must never enter the interaction outline.
            outlineRenderers = Array.FindAll(outlineRenderers, renderer => renderer != null &&
                renderer.GetComponentInParent<Duskborn.Effects.FurnaceEffects>() == null);

            if (outlineRenderers.Length > 0)
            {
                int layerIndex = RenderingLayerMask.NameToRenderingLayer(outlineLayerName);
                _outlineMask = layerIndex >= 0 ? (uint)(1 << layerIndex) : 0u;
                foreach (var renderer in outlineRenderers)
                {
                    if (renderer == null) continue;
                    _baseMask = renderer.renderingLayerMask & ~_outlineMask;
                    renderer.renderingLayerMask = _baseMask;
                }
            }
        }

        public void SetOutline(bool show)
        {
            if (outlineRenderers == null) return;
            foreach (var renderer in outlineRenderers)
            {
                if (renderer == null) continue;
                var baseMask = renderer.renderingLayerMask & ~_outlineMask;
                renderer.renderingLayerMask = show ? baseMask | _outlineMask : baseMask;
            }
        }

        public bool IsInRange(Vector3 position, float maxDistance = 3.5f)
        {
            return Vector3.Distance(transform.position, position) <= maxDistance;
        }

        public void OpenForLocalPlayer()
        {
            CurrentOpenWorkbench = this;
            OnClientInteracted?.Invoke();
        }

        public void CloseForLocalPlayer()
        {
            if (CurrentOpenWorkbench == this)
                CurrentOpenWorkbench = null;
        }

        public void ServerInteract(NetworkConnection requester)
        {
            if (!IsServerStarted) return;
            DuskLog.Log(LogChannel.Loot, $"Player interacted with Workbench at {transform.position}");
            OnInteracted?.Invoke(requester);
        }
    }
}
