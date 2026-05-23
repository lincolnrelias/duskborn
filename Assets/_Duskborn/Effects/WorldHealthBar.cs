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

            PositionAboveMesh();

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

        private void PositionAboveMesh()
        {
            if (transform.parent == null) return;

            float meshTopWorld = float.NegativeInfinity;

            // Renderer.bounds: pose-aware for SkinnedMeshRenderer; valid for active MeshRenderers.
            foreach (var r in transform.parent.GetComponentsInChildren<Renderer>())
                meshTopWorld = Mathf.Max(meshTopWorld, r.bounds.max.y);

            // MeshFilter.sharedMesh: explicit corner transform — reliable before the first render.
            foreach (var mf in transform.parent.GetComponentsInChildren<MeshFilter>())
                AccumulateMeshTop(mf, ref meshTopWorld);

            // Collider.bounds: primitive colliders (CapsuleCollider, BoxCollider, etc.) are
            // computed geometrically by Unity and are valid immediately — covers Unity built-in
            // meshes (cylinder, cube, sphere) that may not have been rendered yet.
            foreach (var col in transform.parent.GetComponentsInChildren<Collider>())
                meshTopWorld = Mathf.Max(meshTopWorld, col.bounds.max.y);

            if (meshTopWorld == float.NegativeInfinity)
                meshTopWorld = transform.parent.position.y;

            // InverseTransformPoint converts world Y to the entity's LOCAL space correctly,
            // handling any scale on the entity (unlike plain subtraction which assumes scale=1).
            float localY = transform.parent.InverseTransformPoint(
                new Vector3(transform.parent.position.x, meshTopWorld, transform.parent.position.z)).y;

            var   rt            = GetComponent<RectTransform>();
            float barHalfHeight = rt != null ? rt.sizeDelta.y * 0.5f : 0f;

            // localY = mesh top in local space. Add gap + half bar height so the bottom
            // edge of the canvas lands at meshTop + yOffset, not the centre.
            transform.localPosition = new Vector3(0f, localY + config.yOffset + barHalfHeight, 0f);
        }

        private static void AccumulateMeshTop(MeshFilter mf, ref float maxWorldY)
        {
            if (mf.sharedMesh == null) return;
            Bounds    b = mf.sharedMesh.bounds;
            Transform t = mf.transform;
            for (int xi = 0; xi < 2; xi++)
            for (int yi = 0; yi < 2; yi++)
            for (int zi = 0; zi < 2; zi++)
            {
                float lx = xi == 0 ? b.min.x : b.max.x;
                float ly = yi == 0 ? b.min.y : b.max.y;
                float lz = zi == 0 ? b.min.z : b.max.z;
                maxWorldY = Mathf.Max(maxWorldY, t.TransformPoint(lx, ly, lz).y);
            }
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
                _fadeTimer         = newFill <= 0f ? 0f : config.fadeDelay;
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
