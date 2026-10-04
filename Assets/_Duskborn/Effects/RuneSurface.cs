using System.Collections.Generic;
using Duskborn.Gameplay.Enchanting;
using UnityEngine;
using UnityEngine.Rendering;

namespace Duskborn.Effects
{
    /// <summary>One composite surface pass; authored materials and hit-flash property blocks stay intact.</summary>
    public sealed class RuneSurface : MonoBehaviour
    {
        private readonly List<Renderer> _sources = new();
        private readonly List<Renderer> _shells = new();
        private Material _material;
        private bool _built;
        private readonly int[] _single = new int[9];
        public void SetSingle(RuneKind kind, int strength)
        {
            System.Array.Clear(_single, 0, _single.Length);
            if (kind > RuneKind.None && kind <= RuneKind.Radiance) _single[(int)kind] = strength;
            SetEffects(_single);
        }
        public void SetEffects(int[] strengths, float intensity = 1f)
        {
            int count = 0;
            for (int i = 1; i <= 8; i++) if (strengths[i] > 0) count++;
            if (!_built && count > 0) Build();
            if (_material == null) return;
            _material.SetVector("_RunesA", new Vector4(strengths[1], strengths[2], strengths[3], strengths[4]));
            _material.SetVector("_RunesB", new Vector4(strengths[5], strengths[6], strengths[7], strengths[8]));
            _material.SetFloat("_ActiveCount", Mathf.Max(1, count));
            _material.SetFloat("_Intensity", Mathf.Clamp01(intensity));
            _material.SetMatrix("_RuneWorldToLocal", transform.worldToLocalMatrix);
            for (int i = 0; i < _shells.Count; i++) if (_shells[i] != null)
                _shells[i].enabled = count > 0 && _sources[i] != null && _sources[i].enabled && _sources[i].gameObject.activeInHierarchy;
        }
        private void Build()
        {
            _built = true;
            var shader = Resources.Load<Shader>("Shaders/RuneSurface");
            if (shader == null) return;
            _material = new Material(shader) { name = "Rune composite surface" };
            foreach (var source in GetComponentsInChildren<Renderer>(true))
            {
                if (!(source is MeshRenderer) && !(source is SkinnedMeshRenderer)) continue;
                if (source.GetComponent<RuneSurfaceMesh>() != null) continue;
                // A body surface excludes held weapons, which have their own enchantment pass.
                var owner = source.GetComponentInParent<RuneSurface>();
                if (owner != this) continue;
                var go = new GameObject("Rune surface shell", typeof(RuneSurfaceMesh));
                go.layer = source.gameObject.layer; go.transform.SetParent(source.transform, false);
                Renderer shell;
                Mesh mesh;
                if (source is SkinnedMeshRenderer skin)
                {
                    var clone = go.AddComponent<SkinnedMeshRenderer>();
                    mesh = skin.sharedMesh; clone.sharedMesh = mesh; clone.bones = skin.bones; clone.rootBone = skin.rootBone;
                    clone.localBounds = skin.localBounds; clone.updateWhenOffscreen = skin.updateWhenOffscreen; shell = clone;
                }
                else
                {
                    mesh = source.GetComponent<MeshFilter>()?.sharedMesh;
                    go.AddComponent<MeshFilter>().sharedMesh = mesh; shell = go.AddComponent<MeshRenderer>();
                }
                if (mesh == null) { Destroy(go); continue; }
                var materials = new Material[Mathf.Max(1, mesh.subMeshCount)];
                for (int i = 0; i < materials.Length; i++) materials[i] = _material;
                shell.sharedMaterials = materials; shell.shadowCastingMode = ShadowCastingMode.Off;
                shell.receiveShadows = false; shell.enabled = false;
                _sources.Add(source); _shells.Add(shell);
            }
        }
        private void LateUpdate()
        {
            for (int i = 0; i < _shells.Count; i++)
            {
                if (_shells[i] == null || _sources[i] == null) continue;
                if (!_sources[i].enabled || !_sources[i].gameObject.activeInHierarchy) _shells[i].enabled = false;
                if (_sources[i] is SkinnedMeshRenderer source && _shells[i] is SkinnedMeshRenderer shell && source.sharedMesh != null)
                    for (int b = 0; b < source.sharedMesh.blendShapeCount; b++) shell.SetBlendShapeWeight(b, source.GetBlendShapeWeight(b));
            }
        }
        private void OnDestroy()
        {
            foreach (var shell in _shells) if (shell != null)
            { if (Application.isPlaying) Destroy(shell.gameObject); else DestroyImmediate(shell.gameObject); }
            if (_material != null) { if (Application.isPlaying) Destroy(_material); else DestroyImmediate(_material); }
        }
    }
    public sealed class RuneSurfaceMesh : MonoBehaviour { }
}
