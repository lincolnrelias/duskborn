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
            _config            = config;
            transform.position = worldPos + Vector3.up * config.spawnHeightOffset;

            _label.text      = Mathf.CeilToInt(amount).ToString();
            _label.fontSize  = isCrit ? config.critFontSize   : config.normalFontSize;
            _label.color     = isCrit ? config.critColor      : config.normalColor;
            _label.fontStyle = FontStyles.Bold;
            _label.outlineColor = config.outlineColor;
            _label.outlineWidth = config.outlineWidth;

            // Crits spawn big and snap down (WHAM); normals pop in from small.
            transform.localScale = Vector3.one * (isCrit ? config.critPopStartScale : 0.5f);

            gameObject.SetActive(true);

            if (_anim != null) StopCoroutine(_anim);
            _anim = StartCoroutine(Animate(isCrit));
        }

        private IEnumerator Animate(bool isCrit)
        {
            Vector3 origin  = transform.position;
            float   height  = _config.floatHeight * (isCrit ? 1.2f : 1f);
            // Pick a drift direction once; numbers arc outward as they rise.
            float   drift   = Random.Range(-_config.driftAmount, _config.driftAmount);
            float   popEnd  = _config.critPopTime;
            float   elapsed = 0f;

            Color baseColor    = _label.color;
            Color outlineBase  = _config.outlineColor;
            float popStart     = isCrit ? _config.critPopStartScale : 0.5f;

            while (elapsed < _config.duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _config.duration);

                // Ease-out vertical: quadratic deceleration (fast start, gentle coast).
                float easedY = height * (1f - (1f - t) * (1f - t));

                // Horizontal arc: drift accumulates over lifetime.
                float easedX = drift * t;

                transform.position = origin + new Vector3(easedX, easedY, 0f);

                // Pop-in / snap: scale from startScale to 1.0 over popEnd seconds.
                transform.localScale = Vector3.one *
                    (elapsed < popEnd ? Mathf.LerpUnclamped(popStart, 1f, elapsed / popEnd) : 1f);

                // Fade: opaque until fadeStartFraction, then linear out.
                float alpha = t < _config.fadeStartFraction
                    ? 1f
                    : 1f - (t - _config.fadeStartFraction) / (1f - _config.fadeStartFraction);

                _label.color        = new Color(baseColor.r,   baseColor.g,   baseColor.b,   alpha);
                _label.outlineColor = new Color(outlineBase.r, outlineBase.g, outlineBase.b, alpha);

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
