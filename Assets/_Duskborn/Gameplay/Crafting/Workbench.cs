using System;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;
using Duskborn.Core;

namespace Duskborn.Gameplay.Crafting
{
    /// <summary>
    /// Componente de bancada de trabalho no Duskborn.
    /// Permite que jogadores interajam para abrir a interface de fabricação (Crafting UI)
    /// e forjem ferramentas e equipamentos primordiais como Machado de Pedra e Picareta de Pedra.
    /// </summary>
    public class Workbench : NetworkBehaviour
    {
        [Header("Tipo de Estação")]
        [SerializeField] private CraftingStationType stationType = CraftingStationType.Bancada;
        [SerializeField] private string stationDisplayName = "Bancada de Trabalho";

        [Header("Outline & Visuals")]
        [SerializeField] private string outlineLayerName = "GreenOutline";
        [SerializeField] private Renderer[] outlineRenderers;

        [Header("Receitas")]
        [Tooltip("Lista de receitas disponibilizadas nesta bancada. Se vazio, carrega as receitas padrões.")]
        [SerializeField] private CraftingRecipe[] recipes;

        private uint _outlineMask;
        private uint _baseMask;

        public static Workbench CurrentOpenWorkbench { get; private set; }

        public event Action<NetworkConnection> OnInteracted;
        public event Action OnClientInteracted;

        public CraftingStationType StationType => stationType;
        public string StationDisplayName => !string.IsNullOrEmpty(stationDisplayName) ? stationDisplayName : "Bancada";
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
