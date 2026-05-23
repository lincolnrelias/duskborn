using Duskborn.Core;
using Duskborn.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Duskborn.Effects
{
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(CanvasGroup))]
    public class WorldHealthBar : MonoBehaviour
    {
        [SerializeField] private Image           fill;
        [SerializeField] private Image           ghostFill;
        [SerializeField] private Image           background;
        [SerializeField] private HealthBarConfig config;

        private IHealthProvider _provider;
        private CanvasGroup     _canvasGroup;
        private float           _targetFill;
        private float           _displayFill;
        private float           _ghostFill;
        private float           _fadeTimer;

        private void Start()
        {
            _canvasGroup = GetComponent<CanvasGroup>();

            transform.localPosition = Vector3.up * config.yOffset;

            _provider = GetComponentInParent<IHealthProvider>();
            if (_provider == null)
            {
                DuskLog.Warn(LogChannel.Effects, $"WorldHealthBar on '{name}': no IHealthProvider found in parent.");
                enabled = false;
                return;
            }

            float ratio          = _provider.MaxHP > 0f ? _provider.CurrentHP / _provider.MaxHP : 1f;
            _targetFill          = ratio;
            _displayFill         = ratio;
            _ghostFill           = ratio;
            fill.fillAmount      = ratio;
            ghostFill.fillAmount = ratio;
            ghostFill.enabled    = false;

            if (background != null) background.color = config.bgColor;
            if (ghostFill  != null) ghostFill.color  = config.ghostColor;
            fill.color = GetBarColor(ratio);

            _canvasGroup.alpha = 0f;

            _provider.OnHealthChanged += HandleHealthChanged;
        }

        private void OnDestroy()
        {
            if (_provider != null)
                _provider.OnHealthChanged -= HandleHealthChanged;
        }

        private void HandleHealthChanged(float current, float max)
        {
            float newFill = max > 0f ? current / max : 0f;

            if (newFill < _targetFill)
            {
                // Damage: ghost stays at current display level and trails behind.
                _ghostFill         = _displayFill;
                _canvasGroup.alpha = 1f;
                _fadeTimer         = config.fadeDelay;
            }

            _targetFill = newFill;
        }

        private void LateUpdate()
        {
            if (config == null) return;

            if (Camera.main != null)
                transform.rotation = Camera.main.transform.rotation;

            // Main fill drains quickly to target.
            _displayFill    = Mathf.Lerp(_displayFill, _targetFill, Time.deltaTime * config.drainSpeed);
            fill.fillAmount = _displayFill;
            fill.color      = GetBarColor(_displayFill);

            // Ghost fill trails slowly — shows "damage taken" gap until it catches up.
            if (_ghostFill > _displayFill + 0.001f)
            {
                _ghostFill           = Mathf.Lerp(_ghostFill, _targetFill, Time.deltaTime * config.ghostDrainSpeed);
                ghostFill.fillAmount = _ghostFill;
                ghostFill.enabled    = true;
            }
            else
            {
                ghostFill.enabled = false;
            }

            // Fade out after delay.
            if (_fadeTimer > 0f)
                _fadeTimer -= Time.deltaTime;
            else if (_canvasGroup.alpha > 0f)
                _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, 0f, Time.deltaTime / config.fadeDuration);
        }

        private Color GetBarColor(float t)
        {
            if (t > config.midThreshold)
            {
                float localT = (t - config.midThreshold) / (1f - config.midThreshold);
                return Color.Lerp(config.midColor, config.fullColor, localT);
            }
            if (t > config.lowThreshold)
            {
                float localT = (t - config.lowThreshold) / (config.midThreshold - config.lowThreshold);
                return Color.Lerp(config.lowColor, config.midColor, localT);
            }
            return config.lowColor;
        }
    }
}
