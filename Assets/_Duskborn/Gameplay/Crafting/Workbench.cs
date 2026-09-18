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
        [Header("Outline & Visuals")]
        [SerializeField] private string outlineLayerName = "GreenOutline";
        [SerializeField] private Renderer outlineRenderer;

        [Header("Receitas")]
        [Tooltip("Lista de receitas disponibilizadas nesta bancada. Se vazio, carrega as receitas padrões.")]
        [SerializeField] private CraftingRecipe[] recipes;

        private uint _outlineMask;
        private uint _baseMask;

        public static Workbench CurrentOpenWorkbench { get; private set; }

        public event Action<NetworkConnection> OnInteracted;
        public event Action OnClientInteracted;

        public IReadOnlyList<CraftingRecipe> Recipes => recipes;

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
