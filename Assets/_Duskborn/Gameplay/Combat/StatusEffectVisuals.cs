using UnityEngine;

namespace Duskborn.Gameplay.Combat
{
    /// <summary>
    /// Per-effect visual feedback. Auto-added by StatusEffectController; runs on every client.
    /// Invulnerable → pulsing translucent "ghost" silhouette. Other effects plug in here later.
    /// </summary>
    public class StatusEffectVisuals : MonoBehaviour
    {
        [Header("Invulnerable Ghost")]
        [SerializeField] private Color ghostColorA  = new(0.35f, 0.75f, 1f, 0.45f);
        [SerializeField] private Color ghostColorB  = new(0.75f, 0.95f, 1f, 0.85f);
        [SerializeField] private float pulsesPerSec = 3f;

        private static Material _ghostMaterial;

        private StatusEffectController _controller;
        private Renderer[]             _renderers;
        private Material[][]           _originalMaterials;
        private Material[][]           _ghostMaterials;
        private bool                   _ghostActive;

        private void Awake()
        {
            _controller = GetComponent<StatusEffectController>();
            if (_controller != null) _controller.OnEffectChanged += HandleEffectChanged;
        }

        private void OnDestroy()
        {
            if (_controller != null) _controller.OnEffectChanged -= HandleEffectChanged;
        }

        private void HandleEffectChanged(StatusEffect effect, bool active)
        {
            if (effect != StatusEffect.Invulnerable) return;
            if (active) StartGhost();
            else        StopGhost();
        }

        private void Update()
        {
            if (!_ghostActive || _ghostMaterial == null) return;
            float t = 0.5f + 0.5f * Mathf.Sin(Time.time * pulsesPerSec * 2f * Mathf.PI);
            _ghostMaterial.color = Color.Lerp(ghostColorA, ghostColorB, t);
        }

        private void StartGhost()
        {
            if (_ghostActive) return;
            EnsureGhostMaterial();
            if (_ghostMaterial == null) return;
            if (_renderers == null) Capture();

            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null) _renderers[i].sharedMaterials = _ghostMaterials[i];
            _ghostActive = true;
        }

        private void StopGhost()
        {
            if (!_ghostActive) return;
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null) _renderers[i].sharedMaterials = _originalMaterials[i];
            _ghostActive = false;
        }

        private void Capture()
        {
            var all  = GetComponentsInChildren<Renderer>(true);
            var list = new System.Collections.Generic.List<Renderer>(all.Length);
            foreach (var r in all)
                if (r is MeshRenderer || r is SkinnedMeshRenderer) list.Add(r);

            _renderers         = list.ToArray();
            _originalMaterials = new Material[_renderers.Length][];
            _ghostMaterials    = new Material[_renderers.Length][];
            for (int i = 0; i < _renderers.Length; i++)
            {
                _originalMaterials[i] = _renderers[i].sharedMaterials;
                _ghostMaterials[i]    = new Material[_originalMaterials[i].Length];
                for (int m = 0; m < _ghostMaterials[i].Length; m++)
                    _ghostMaterials[i][m] = _ghostMaterial;
            }
        }

        private static void EnsureGhostMaterial()
        {
            if (_ghostMaterial == null) _ghostMaterial = Duskborn.Effects.GhostMaterial.Create();
        }

        private void OnDisable() => StopGhost();
    }
}
