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
    /// Barra de vida no espaço de mundo em estética Medieval Fantasy Low-Poly ("Duskborn").
    /// Apresenta moldura de ferro forjado e ouro envelhecido, rastro de dano em brasa crepuscular,
    /// calha de ardósia escura profunda e preenchimento de vitalidade esmeralda/âmbar/rubi.
    /// Inclui ancoragem inteligente de altura (evitando que nós altos joguem a barra no céu)
    /// e travamento dinâmico no viewport da câmera para jamais sair da tela visível.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(CanvasGroup))]
    public class WorldHealthBar : MonoBehaviour
    {
        [Header("Elementos Visuais")]
        [SerializeField] private Image           fill;
        [SerializeField] private Image           ghostFill;
        [SerializeField] private Image           background;
        [SerializeField] private Image           frame;
        [SerializeField] private TextMeshProUGUI nameLabel;

        [Header("Configuração")]
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
        /// Determina a ancoragem vertical e horizontal correta com base nos colisores e na malha visual da entidade.
        /// Respeita a altura máxima para não projetar a barra no céu em árvores altas (15m),
        /// e expande o raio horizontal para englobar galhos e folhagens volumosas (evitando clipping em árvores grandes).
        /// </summary>
        private void ComputeAnchorOffset()
        {
            if (transform.parent == null) return;

            Transform parent = transform.parent;
            float baseWorldY = parent.position.y;
            float topWorldY  = float.NegativeInfinity;

            // 1. Prioridade para Colisores sólidos na altura: o colisor delimita o topo acessível do tronco.
            var colliders = parent.GetComponentsInChildren<Collider>();
            foreach (var col in colliders)
            {
                if (col.isTrigger) continue;
                topWorldY = Mathf.Max(topWorldY, col.bounds.max.y);
            }

            // 2. Se não houver colisores sólidos válidos, avalia Renderers visíveis para a altura
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

            // 3. Fallback: altura humana média (1.8m)
            if (float.IsNegativeInfinity(topWorldY))
                topWorldY = baseWorldY + 1.8f;

            // 4. Limitação de altura máxima relativa à base:
            // Impede que folhagens de pinheiros de 15m projetem a barra fora da tela.
            float heightAboveBase = topWorldY - baseWorldY;
            float maxHeight = config != null ? config.maxHeightAboveBase : 3.2f;
            if (maxHeight > 0f && heightAboveBase > maxHeight)
            {
                heightAboveBase = maxHeight;
            }

            var rt = GetComponent<RectTransform>();
            float barHalfHeight = rt != null ? rt.sizeDelta.y * 0.5f : 0.08f;
            float yGap = config != null ? config.yOffset : 0.35f;

            // Converte offset do mundo para espaço local do pai respeitando a escala da entidade
            float parentScaleY = parent.lossyScale.y != 0f ? Mathf.Abs(parent.lossyScale.y) : 1f;
            float localY = (heightAboveBase + yGap + barHalfHeight) / parentScaleY;

            // 5. Determina raio horizontal e centro para projetar a barra à frente da superfície.
            // Avalia tanto colisores quanto renderers para cobrir copas largas de árvores que ultrapassam o colisor.
            _horizontalRadius = 0.5f;
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

            // Expande o raio horizontal com base na malha visual (Renderers) para cobrir galhos e folhagens
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

            _horizontalRadius = Mathf.Clamp(_horizontalRadius, 0.35f, 6.0f);
            _localAnchorOffsetY = localY;
            _localAnchorOffset = new Vector3(_localCenter.x, localY, _localCenter.z);
            transform.localPosition = _localAnchorOffset;
            _hasAnchorComputed = true;
        }

        /// <summary>
        /// Resolve e estiliza o nome do alvo em português para conferir identidade de RPG medieval.
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
            if (transform.parent == null) return "Alvo";

            string rawName = transform.parent.name;

            // Remoção de sufixos de clone do Unity
            if (rawName.EndsWith("(Clone)", StringComparison.OrdinalIgnoreCase))
                rawName = rawName.Substring(0, rawName.Length - 7).Trim();

            // Recursos do Mundo (árvores, minérios, plantas)
            if (rawName.IndexOf("pine", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Pinheiro";
            if (rawName.IndexOf("birch", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Bétula";
            if (rawName.IndexOf("iron_vein", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Veio de Ferro";
            if (rawName.IndexOf("iron_ridge", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Crista de Ferro";
            if (rawName.IndexOf("iron", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Minério de Ferro";
            if (rawName.IndexOf("monolith", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Monólito de Pedra";
            if (rawName.IndexOf("outcrop", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Afloramento de Pedra";
            if (rawName.IndexOf("stone", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Pedra Ancestral";
            if (rawName.IndexOf("fern", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Samambaia";
            if (rawName.IndexOf("herbs", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Ervas Silvestres";
            if (rawName.IndexOf("fiber", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Fibras Vegetais";

            // Inimigos e Criaturas das Sombras
            if (rawName.IndexOf("swarmer", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Enxameante";
            if (rawName.IndexOf("wolf", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Lobo Sombrio";
            if (rawName.IndexOf("boss", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Terror do Crepúsculo";

            // Jogador
            if (rawName.IndexOf("player", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Explorador";

            // Limpeza geral para nós customizados
            string cleaned = rawName.Replace("ResourceNode_", "").Replace("Node_", "").Replace("_", " ");
            return cleaned.Length > 0 ? cleaned : "Entidade";
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
                // Dano sofrido: rastro fantasma de brasa permanece e recua cadenciado
                _ghostFill         = _displayFill;
                _canvasGroup.alpha = 1f;
                _fadeTimer         = newFill <= 0f ? 0.2f : (config != null ? config.fadeDelay : 3.5f);
            }
            else if (newFill > _targetFill)
            {
                // Cura: ajusta fantasma para não criar atraso inverso
                _ghostFill = newFill;
            }

            _targetFill = newFill;
        }

        private void LateUpdate()
        {
            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
                if (_mainCamera == null) return;
            }

            UpdatePositionAndScreenClamping();
            UpdateBarAnimations();
        }

        /// <summary>
        /// Mantém o outdoor alinhado à câmera e aplica clamp no viewport se a entidade estiver próxima
        /// ou muito alta, garantindo que a barra de vida permaneça sempre dentro da área visível da tela.
        /// </summary>
        private void UpdatePositionAndScreenClamping()
        {
            if (transform.parent == null) return;

            // Orientação de billboard sempre voltada para a lente da câmera principal
            transform.rotation = _mainCamera.transform.rotation;

            // Garante escala uniforme de mundo independente de escalas aplicadas no pai
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

            // Ponto central do objeto na altura ideal da barra
            Vector3 anchorCenterWorld = transform.parent.TransformPoint(new Vector3(_localCenter.x, _localAnchorOffsetY, _localCenter.z));

            // Vetor tridimensional na direção da câmera / jogador (perspectiva: objeto -> barra -> jogador)
            Vector3 toCam3D = _mainCamera.transform.position - anchorCenterWorld;
            float distToCam = toCam3D.magnitude;
            Vector3 dirToCam = distToCam > 0.0001f ? toCam3D / distToCam : -_mainCamera.transform.forward;

            // Deslocamento para frente (em direção ao jogador) respeitando o raio do tronco/copa
            float extraForward = config != null ? config.forwardOffset : 0.35f;
            float totalForward = _horizontalRadius + extraForward;

            // Prevenção de aproximação excessiva da câmera
            if (distToCam > 0.6f)
            {
                totalForward = Mathf.Min(totalForward, distToCam - 0.5f);
            }

            Vector3 idealWorldPos = anchorCenterWorld + dirToCam * totalForward;

            if (config != null && config.clampToScreen)
            {
                Vector3 barVp    = _mainCamera.WorldToViewportPoint(idealWorldPos);
                Vector3 parentVp = _mainCamera.WorldToViewportPoint(transform.parent.position);

                // Se a entidade estiver na frente da câmera (z > 0)
                if (barVp.z > 0.1f && parentVp.z > 0.1f)
                {
                    // Apenas trava na tela se o corpo da entidade estiver horizontalmente no campo de visão
                    if (parentVp.x >= -0.25f && parentVp.x <= 1.25f)
                    {
                        float clampedX = Mathf.Clamp(barVp.x, config.minViewportX, config.maxViewportX);
                        float clampedY = Mathf.Clamp(barVp.y, config.minViewportY, config.maxViewportY);

                        if (Mathf.Abs(clampedX - barVp.x) > 0.001f || Mathf.Abs(clampedY - barVp.y) > 0.001f)
                        {
                            // Profundidade segura ao prender no viewport:
                            // Garante que a barra fique à frente da face frontal da árvore mais próxima da câmera
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
                    // Se estiver atrás do jogador, oculta para não projetar aberrações
                    _canvasGroup.alpha = 0f;
                    return;
                }
            }

            // Posição natural de mundo caso não seja necessário prender ao viewport
            transform.position = idealWorldPos;
        }

        private void UpdateBarAnimations()
        {
            if (config == null) return;

            // Barra principal de vitalidade drena com rapidez responsiva
            _displayFill = Mathf.Lerp(_displayFill, _targetFill, Time.deltaTime * config.drainSpeed);
            if (fill != null)
            {
                fill.fillAmount = _displayFill;
                fill.color      = GetBarColor(_displayFill);
            }

            // Rastro fantasma de brasas decai em ritmo cadenciado mostrando o dano sofrido
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

            // Temporizador de desvanecimento suave pós-combate
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
