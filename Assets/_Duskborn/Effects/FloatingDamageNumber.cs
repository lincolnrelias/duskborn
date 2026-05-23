using System.Collections;
using TMPro;
using UnityEngine;

namespace Duskborn.Effects
{
    [RequireComponent(typeof(TextMeshPro))]
    public class FloatingDamageNumber : MonoBehaviour
    {
        private TextMeshPro        _label;
        private DamageNumberConfig _config;
        private Coroutine          _anim;

        private void Awake() => _label = GetComponent<TextMeshPro>();

        public void Play(Vector3 worldPos, float amount, bool isCrit, DamageNumberConfig config)
        {
            _config              = config;
            transform.position   = worldPos;
            transform.localScale = Vector3.one;

            _label.text     = Mathf.CeilToInt(amount).ToString();
            _label.fontSize  = isCrit ? config.critFontSize   : config.normalFontSize;
            _label.color     = isCrit ? config.critColor      : config.normalColor;

            gameObject.SetActive(true);

            if (_anim != null) StopCoroutine(_anim);
            _anim = StartCoroutine(isCrit ? AnimateCrit() : AnimateNormal());
        }

        private IEnumerator AnimateNormal()
        {
            Vector3 start   = transform.position;
            Vector3 end     = start + Vector3.up * _config.floatHeight;
            float   elapsed = 0f;

            while (elapsed < _config.duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / _config.duration;
                transform.position = Vector3.Lerp(start, end, t);

                const float fadeStart = 0.6f;
                float alpha = t < fadeStart ? 1f : 1f - (t - fadeStart) / (1f - fadeStart);
                var c = _label.color;
                _label.color = new Color(c.r, c.g, c.b, alpha);

                yield return null;
            }

            ReturnToPool();
        }

        private IEnumerator AnimateCrit()
        {
            Vector3 start     = transform.position;
            Vector3 end       = start + Vector3.up * _config.floatHeight;
            float   elapsed   = 0f;
            float   punchEnd  = _config.critPunchTime;
            float   settleEnd = punchEnd * 2f;

            while (elapsed < _config.duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / _config.duration;
                transform.position = Vector3.Lerp(start, end, t);

                float scale;
                if (elapsed < punchEnd)
                    scale = Mathf.Lerp(1f, _config.critPunchScale, elapsed / punchEnd);
                else if (elapsed < settleEnd)
                    scale = Mathf.Lerp(_config.critPunchScale, 1f, (elapsed - punchEnd) / punchEnd);
                else
                    scale = 1f;
                transform.localScale = Vector3.one * scale;

                const float fadeStart = 0.6f;
                float alpha = t < fadeStart ? 1f : 1f - (t - fadeStart) / (1f - fadeStart);
                var c = _label.color;
                _label.color = new Color(c.r, c.g, c.b, alpha);

                yield return null;
            }

            ReturnToPool();
        }

        private void LateUpdate()
        {
            if (Camera.main != null)
                transform.rotation = Camera.main.transform.rotation;
        }

        private void ReturnToPool()
        {
            _anim = null;
            DamageNumberPool.Instance?.Return(this);
        }
    }
}
