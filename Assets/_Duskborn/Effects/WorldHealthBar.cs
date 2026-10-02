using System;
using TMPro;
using Duskborn.Core;
using Duskborn.Gameplay;
using Duskborn.Gameplay.Loot;
using UnityEngine;
using UnityEngine.UI;

namespace Duskborn.Effects
{
    /// <summary>
    /// World-space health bar with a Medieval Fantasy Low-Poly aesthetic ("Duskborn").
    /// Features a wrought iron and aged gold frame, twilight ember damage trail,
    /// deep dark slate track, and emerald / amber / ruby vitality fill.
    /// Includes intelligent height anchoring (prevents tall nodes from pushing the bar into the sky)
    /// and dynamic camera viewport clamping to keep it on the visible screen.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(CanvasGroup))]
    public class WorldHealthBar : MonoBehaviour
    {
        [Header("Visual Elements")]
        [SerializeField] private Image           fill;
        [SerializeField] private Image           ghostFill;
        [SerializeField] private Image           background;
        [SerializeField] private Image           frame;
        [SerializeField] private TextMeshProUGUI nameLabel;

        [Header("Configuration")]
        [SerializeField] private HealthBarConfig config;

        private IHealthProvider _provider;
        private CanvasGroup     _canvasGroup;
        private Camera          _mainCamera;
        private Vector3         _localAnchorOffset;
        private Vector3         _localCenter;
        private Vector3         _lastParentScale;
        private float           _horizontalRadius = 0.5f;
        private float           _localAnchorOffsetY;
        private float           _targetFill;
        private float           _displayFill;
        private float           _ghostFill;
        private float           _fadeTimer;
        private bool            _hasAnchorComputed;

        private void Start()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            _mainCamera  = Camera.main;

            if (transform.parent != null)
                _lastParentScale = transform.parent.lossyScale;

            ComputeAnchorOffset();
            SetupTargetName();

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

            if (fill != null)
            {
                fill.fillAmount = ratio;
                fill.color      = GetBarColor(ratio);
            }

            if (ghostFill != null)
            {
                ghostFill.fillAmount = ratio;
                ghostFill.color      = config != null ? config.ghostColor : new Color(1f, 0.58f, 0.16f, 0.75f);
                ghostFill.enabled    = false;
            }

            if (background != null && config != null)
                background.color = config.bgColor;

            if (frame != null && config != null)
                frame.color = config.frameColor;

            _canvasGroup.alpha = 0f;

            _provider.OnHealthChanged += HandleHealthChanged;
        }

        /// <summary>
        /// Determine correct vertical and horizontal anchoring from the entity's colliders and visual mesh.
        /// Respect maximum height to avoid projecting the bar into the sky on tall trees (15m),
        /// and expand the horizontal radius to include bulky branches and foliage (avoiding clipping on large trees).
        /// </summary>
        private void ComputeAnchorOffset()
        {
            if (transform.parent == null) return;

            Transform parent = transform.parent;
            float baseWorldY = parent.position.y;
            float topWorldY  = float.NegativeInfinity;

            // 1. Prioritize solid colliders for height: the collider defines the accessible trunk top.
            var colliders = parent.GetComponentsInChildren<Collider>();
            foreach (var col in colliders)
            {
                if (col.isTrigger) continue;
                topWorldY = Mathf.Max(topWorldY, col.bounds.max.y);
            }

            // 2. If no valid solid colliders exist, evaluate visible Renderers for height.
            var renderers = parent.GetComponentsInChildren<Renderer>();
            if (float.IsNegativeInfinity(topWorldY))
            {
                foreach (var r in renderers)
                {
                    if (r is ParticleSystemRenderer || r.transform.IsChildOf(transform)) continue;
                    if (!r.enabled) continue;
                    topWorldY = Mathf.Max(topWorldY, r.bounds.max.y);
                }
            }

            // 3. Fallback: average human height (1.8m).
            if (float.IsNegativeInfinity(topWorldY))
                topWorldY = baseWorldY + 1.8f;

            // 4. Maximum height limit relative to the base:
            // Prevent foliage on 15m pines from projecting the bar offscreen.
            float heightAboveBase = topWorldY - baseWorldY;
            float maxHeight = config != null ? config.maxHeightAboveBase : 3.2f;
            if (maxHeight > 0f && heightAboveBase > maxHeight)
            {
                heightAboveBase = maxHeight;
            }

            var rt = GetComponent<RectTransform>();
            float barHalfHeight = rt != null ? rt.sizeDelta.y * 0.5f : 0.08f;
            float yGap = config != null ? config.yOffset : 0.35f;

            // Convert the world offset to parent local space, respecting entity scale.
            float parentScaleY = parent.lossyScale.y != 0f ? Mathf.Abs(parent.lossyScale.y) : 1f;
            float localY = (heightAboveBase + yGap + barHalfHeight) / parentScaleY;

            // 5. Determine horizontal radius and center to project the bar in front of the surface.
            // Evaluate both colliders and renderers to cover wide tree canopies extending beyond the collider.
            _horizontalRadius = 0f;
            _localCenter = Vector3.zero;

            foreach (var col in colliders)
            {
                if (col.isTrigger) continue;
                Vector3 lossy = col.transform.lossyScale;
                if (col is CapsuleCollider capsule)
                {
                    float r = capsule.radius * Mathf.Max(Mathf.Abs(lossy.x), Mathf.Abs(lossy.z));
                    _horizontalRadius = Mathf.Max(_horizontalRadius, r);
                    _localCenter = parent.InverseTransformPoint(col.transform.TransformPoint(capsule.center));
                    _localCenter.y = 0f;
                }
                else if (col is SphereCollider sphere)
                {
                    float r = sphere.radius * Mathf.Max(Mathf.Abs(lossy.x), Mathf.Abs(lossy.z));
                    _horizontalRadius = Mathf.Max(_horizontalRadius, r);
                    _localCenter = parent.InverseTransformPoint(col.transform.TransformPoint(sphere.center));
                    _localCenter.y = 0f;
                }
                else if (col is BoxCollider box)
                {
                    float r = Mathf.Max(box.size.x * Mathf.Abs(lossy.x), box.size.z * Mathf.Abs(lossy.z)) * 0.5f;
                    _horizontalRadius = Mathf.Max(_horizontalRadius, r);
                    _localCenter = parent.InverseTransformPoint(col.transform.TransformPoint(box.center));
                    _localCenter.y = 0f;
                }
                else
                {
                    float r = Mathf.Max(col.bounds.extents.x, col.bounds.extents.z);
                    _horizontalRadius = Mathf.Max(_horizontalRadius, r);
                }
            }

            // Expand horizontal radius based on visual meshes (Renderers) to cover branches and foliage.
            foreach (var r in renderers)
            {
                if (r is ParticleSystemRenderer || r.transform.IsChildOf(transform)) continue;
                if (!r.enabled) continue;

                Vector3 ext = r.bounds.extents;
                float rExt = Mathf.Max(ext.x, ext.z);
                Vector3 offset = r.bounds.center - parent.position;
                offset.y = 0f;
                float totalR = offset.magnitude + rExt;
                _horizontalRadius = Mathf.Max(_horizontalRadius, totalR);
            }

            // Use the default only when no bounds were found, not as a minimum trunk radius.
            if (_horizontalRadius <= 0f) _horizontalRadius = 0.5f;
            _horizontalRadius = Mathf.Clamp(_horizontalRadius, 0.35f, 6.0f);
            _localAnchorOffsetY = localY;
            _localAnchorOffset = new Vector3(_localCenter.x, localY, _localCenter.z);
            transform.localPosition = _localAnchorOffset;
            _hasAnchorComputed = true;
        }

        /// <summary>
        /// Resolve and style the target's English name for a medieval RPG identity.
        /// </summary>
        private void SetupTargetName()
        {
            if (nameLabel == null) return;

            if (config != null && !config.showName)
            {
                nameLabel.gameObject.SetActive(false);
                return;
            }

            string displayName = ResolveDisplayName();
            nameLabel.text = displayName;
            nameLabel.gameObject.SetActive(true);

            float targetSize = config != null && config.nameFontSize > 0f ? config.nameFontSize : 0.18f;
            nameLabel.enableAutoSizing = true;
            nameLabel.fontSizeMin = targetSize * 0.5f;
            nameLabel.fontSizeMax = targetSize;
            nameLabel.fontSize    = targetSize;

            if (config != null)
            {
                nameLabel.color = config.nameTextColor;
            }
        }

        private string ResolveDisplayName()
        {
            if (transform.parent == null) return "Target";

            string rawName = transform.parent.name;

            // Remove Unity clone suffixes.
            if (rawName.EndsWith("(Clone)", StringComparison.OrdinalIgnoreCase))
                rawName = rawName.Substring(0, rawName.Length - 7).Trim();

            // World resources (trees, ores, plants).
            if (rawName.IndexOf("pine", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Pine";
            if (rawName.IndexOf("birch", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Birch";
            if (rawName.IndexOf("iron_vein", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Iron Vein";
            if (rawName.IndexOf("iron_ridge", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Iron Ridge";
            if (rawName.IndexOf("iron", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Iron Ore";
            if (rawName.IndexOf("monolith", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Stone Monolith";
            if (rawName.IndexOf("outcrop", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Stone Outcrop";
            if (rawName.IndexOf("stone", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Ancestral Stone";
            if (rawName.IndexOf("fern", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Fern";
            if (rawName.IndexOf("herbs", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Wild Herbs";
            if (rawName.IndexOf("fiber", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Plant Fiber";

            // Enemies and Shadow Creatures.
            if (rawName.IndexOf("swarmer", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Enxameante";
            if (rawName.IndexOf("wolf", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Shadow Wolf";
            if (rawName.IndexOf("boss", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Twilight Terror";

            // Player.
            if (rawName.IndexOf("player", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Explorador";

            // General cleanup for custom nodes.
            string cleaned = rawName.Replace("ResourceNode_", "").Replace("Node_", "").Replace("_", " ");
            return cleaned.Length > 0 ? cleaned : "Entity";
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
                // Damage taken: the ember ghost trail remains and recedes steadily.
                _ghostFill         = _displayFill;
                _canvasGroup.alpha = 1f;
                _fadeTimer         = newFill <= 0f ? 0.2f : (config != null ? config.fadeDelay : 3.5f);
            }
            else if (newFill > _targetFill)
            {
                // Healing: adjust the ghost trail to avoid inverse lag.
                _ghostFill = newFill;
            }

            _targetFill = newFill;
        }

        private void LateUpdate()
        {
            if (_canvasGroup != null && _canvasGroup.alpha <= 0.001f && _fadeTimer <= 0f && Mathf.Abs(_displayFill - _targetFill) < 0.001f)
                return;

            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
                if (_mainCamera == null) return;
            }

            UpdatePositionAndScreenClamping();
            UpdateBarAnimations();
        }

        /// <summary>
        /// Keep the billboard aligned with the camera and clamp to the viewport when the entity is nearby
        /// or very tall, ensuring the health bar remains within the visible screen area.
        /// </summary>
        private void UpdatePositionAndScreenClamping()
        {
            if (transform.parent == null) return;

            // Billboard orientation always faces the main camera lens.
            transform.rotation = _mainCamera.transform.rotation;

            // Ensure uniform world scale regardless of parent scaling.
            Vector3 pScale = transform.parent.lossyScale;
            transform.localScale = new Vector3(
                pScale.x != 0f ? 1f / Mathf.Abs(pScale.x) : 1f,
                pScale.y != 0f ? 1f / Mathf.Abs(pScale.y) : 1f,
                pScale.z != 0f ? 1f / Mathf.Abs(pScale.z) : 1f
            );

            if (!_hasAnchorComputed || pScale != _lastParentScale)
            {
                _lastParentScale = pScale;
                ComputeAnchorOffset();
            }

            // Object center point at the ideal bar height.
            Vector3 anchorCenterWorld = transform.parent.TransformPoint(new Vector3(_localCenter.x, _localAnchorOffsetY, _localCenter.z));

            // Three-dimensional vector toward the camera / player (perspective: object -> bar -> player).
            Vector3 toCam3D = _mainCamera.transform.position - anchorCenterWorld;
            float distToCam = toCam3D.magnitude;
            Vector3 dirToCam = distToCam > 0.0001f ? toCam3D / distToCam : -_mainCamera.transform.forward;

            // Forward offset (toward the player) respecting trunk / canopy radius.
            float extraForward = config != null ? config.forwardOffset : 0.35f;
            float totalForward = _horizontalRadius + extraForward;

            // Prevent excessive camera proximity.
            if (distToCam > 0.6f)
            {
                totalForward = Mathf.Min(totalForward, distToCam - 0.5f);
            }

            Vector3 idealWorldPos = anchorCenterWorld + dirToCam * totalForward;

            if (config != null && config.clampToScreen)
            {
                Vector3 barVp    = _mainCamera.WorldToViewportPoint(idealWorldPos);
                Vector3 parentVp = _mainCamera.WorldToViewportPoint(transform.parent.position);

                // If the entity is in front of the camera (z > 0).
                if (barVp.z > 0.1f && parentVp.z > 0.1f)
                {
                    // Only clamp to the screen when the entity body is horizontally within view.
                    if (parentVp.x >= -0.25f && parentVp.x <= 1.25f)
                    {
                        float clampedX = Mathf.Clamp(barVp.x, config.minViewportX, config.maxViewportX);
                        float clampedY = Mathf.Clamp(barVp.y, config.minViewportY, config.maxViewportY);

                        if (Mathf.Abs(clampedX - barVp.x) > 0.001f || Mathf.Abs(clampedY - barVp.y) > 0.001f)
                        {
                            // Safe depth when clamping to the viewport:
                            // Ensure the bar stays in front of the tree face nearest the camera.
                            float safeZ = Mathf.Min(barVp.z, parentVp.z - _horizontalRadius - extraForward);
                            safeZ = Mathf.Max(0.5f, safeZ);
                            Vector3 clampedWorld = _mainCamera.ViewportToWorldPoint(new Vector3(clampedX, clampedY, safeZ));
                            transform.position = clampedWorld;
                            return;
                        }
                    }
                }
                else if (barVp.z <= 0.1f)
                {
                    // If behind the player, hide it to avoid projection artifacts.
                    _canvasGroup.alpha = 0f;
                    return;
                }
            }

            // Natural world position when viewport clamping is unnecessary.
            transform.position = idealWorldPos;
        }

        private void UpdateBarAnimations()
        {
            if (config == null) return;

            // The main vitality bar drains responsively.
            _displayFill = Mathf.Lerp(_displayFill, _targetFill, Time.deltaTime * config.drainSpeed);
            if (fill != null)
            {
                fill.fillAmount = _displayFill;
                fill.color      = GetBarColor(_displayFill);
            }

            // The ember ghost trail decays steadily to show damage taken.
            if (ghostFill != null)
            {
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
            }

            // Smooth post-combat fade timer.
            if (_fadeTimer > 0f)
            {
                _fadeTimer -= Time.deltaTime;
            }
            else if (_canvasGroup.alpha > 0f)
            {
                _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, 0f, Time.deltaTime / config.fadeDuration);
            }
        }

        private Color GetBarColor(float t)
        {
            if (config == null) return Color.green;

            if (t > config.midThreshold)
            {
                float localT = (t - config.midThreshold) / Mathf.Max(0.001f, 1f - config.midThreshold);
                return Color.Lerp(config.midColor, config.fullColor, localT);
            }
            if (t > config.lowThreshold)
            {
                float localT = (t - config.lowThreshold) / Mathf.Max(0.001f, config.midThreshold - config.lowThreshold);
                return Color.Lerp(config.lowColor, config.midColor, localT);
            }
            return config.lowColor;
        }
    }
}
