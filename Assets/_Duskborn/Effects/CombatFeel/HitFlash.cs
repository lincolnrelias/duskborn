using System.Collections;
using UnityEngine;

namespace Duskborn.Effects
{
    // White material flash on damage via MaterialPropertyBlock (zero material cloning, preserves SRP Batcher).
    public class HitFlash : MonoBehaviour
    {
        private static readonly int BaseColorId     = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private static MaterialPropertyBlock _flashBlock;

        private Renderer[] _renderers;
        private Coroutine  _routine;

        public void Flash()
        {
            var s = CombatFeelSettings.Instance;
            if (s == null || !s.hitFlashEnabled) return;

            if (_flashBlock == null)
            {
                _flashBlock = new MaterialPropertyBlock();
                _flashBlock.SetColor(BaseColorId, s.hitFlashColor);
                _flashBlock.SetColor(EmissionColorId, s.hitFlashColor);
            }

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

            _renderers = list.ToArray();
        }

        private IEnumerator FlashRoutine(float duration)
        {
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null) _renderers[i].SetPropertyBlock(_flashBlock);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Restore();
            _routine = null;
        }

        private void Restore()
        {
            if (_renderers == null) return;
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null) _renderers[i].SetPropertyBlock(null);
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
