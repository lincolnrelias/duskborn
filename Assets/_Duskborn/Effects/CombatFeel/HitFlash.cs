using System.Collections;
using UnityEngine;

namespace Duskborn.Effects
{
    // White material flash on damage. Added at runtime by EnemyBase; runs on every client.
    public class HitFlash : MonoBehaviour
    {
        private static Material _flashMaterial;

        private Renderer[]   _renderers;
        private Material[][] _originalMaterials;
        private Material[][] _flashMaterials;
        private Coroutine    _routine;

        public void Flash()
        {
            var s = CombatFeelSettings.Instance;
            if (s == null || !s.hitFlashEnabled) return;

            if (_flashMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) return;
                _flashMaterial = new Material(shader);
            }
            _flashMaterial.color = s.hitFlashColor;

            if (_renderers == null) Capture();

            if (_routine != null)
            {
                StopCoroutine(_routine);
                Restore();
            }
            _routine = StartCoroutine(FlashRoutine(s.hitFlashDuration));
        }

        private void Capture()
        {
            var all  = GetComponentsInChildren<Renderer>(true);
            var list = new System.Collections.Generic.List<Renderer>(all.Length);
            foreach (var r in all)
                if (r is MeshRenderer || r is SkinnedMeshRenderer) list.Add(r);

            _renderers         = list.ToArray();
            _originalMaterials = new Material[_renderers.Length][];
            _flashMaterials    = new Material[_renderers.Length][];
            for (int i = 0; i < _renderers.Length; i++)
            {
                _originalMaterials[i] = _renderers[i].sharedMaterials;
                _flashMaterials[i]    = new Material[_originalMaterials[i].Length];
                for (int m = 0; m < _flashMaterials[i].Length; m++)
                    _flashMaterials[i][m] = _flashMaterial;
            }
        }

        private IEnumerator FlashRoutine(float duration)
        {
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null) _renderers[i].sharedMaterials = _flashMaterials[i];

            yield return new WaitForSecondsRealtime(duration);

            Restore();
            _routine = null;
        }

        private void Restore()
        {
            if (_renderers == null) return;
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null) _renderers[i].sharedMaterials = _originalMaterials[i];
        }

        private void OnDisable()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }
            Restore();
        }
    }
}
