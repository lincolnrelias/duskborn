using UnityEngine;
using Duskborn.Core;

namespace Duskborn.Gameplay.World
{
    /// <summary>
    /// Controlador de partículas e ambiente atmosférico (partículas de pólen dourado de dia, vaga-lumes/brasas místicas à noite).
    /// Segue a câmera do jogador para dar imersão e vida orgânica ao mundo low-poly sem peso de renderização.
    /// </summary>
    public class WorldAtmosphereController : MonoBehaviour
    {
        [Header("Configuração de Partículas")]
        [Tooltip("Sistema de partículas que acompanha a câmera. Criado automaticamente se nulo.")]
        [SerializeField] private ParticleSystem atmosphereParticles;

        [Header("Cores e Vibe")]
        [Tooltip("Cor das partículas durante o dia (pólen dourado ensolarado).")]
        [SerializeField] private Color dayParticleColor = new Color(1f, 0.92f, 0.65f, 0.65f);

        [Tooltip("Cor das partículas durante a noite (vaga-lumes / energia etérea).")]
        [SerializeField] private Color nightParticleColor = new Color(0.45f, 0.85f, 1f, 0.75f);

        [Tooltip("Velocidade de transição de cor entre dia e noite.")]
        [SerializeField] private float transitionSpeed = 1.5f;

        private Camera _mainCamera;
        private ParticleSystem.MainModule _mainModule;
        private Color _currentColor;

        private void Awake()
        {
            _currentColor = dayParticleColor;
            EnsureParticleSystem();
        }

        private void Start()
        {
            _mainCamera = Camera.main;
        }

        private void LateUpdate()
        {
            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
                if (_mainCamera == null) return;
            }

            // Posiciona o emissor centrado na câmera com offset frontal
            transform.position = _mainCamera.transform.position + _mainCamera.transform.forward * 4f;

            // Transição orgânica de cor entre dia e noite
            bool isDay = DayNightCycle.Instance == null || DayNightCycle.Instance.Phase == DayPhase.Day;
            Color targetColor = isDay ? dayParticleColor : nightParticleColor;
            _currentColor = Color.Lerp(_currentColor, targetColor, Time.deltaTime * transitionSpeed);

            _mainModule.startColor = new ParticleSystem.MinMaxGradient(_currentColor);
        }

        private void EnsureParticleSystem()
        {
            if (atmosphereParticles == null)
            {
                atmosphereParticles = GetComponent<ParticleSystem>();
            }

            if (atmosphereParticles == null)
            {
                atmosphereParticles = gameObject.AddComponent<ParticleSystem>();
                var psRenderer = GetComponent<ParticleSystemRenderer>();

                // Configuração padrão limpa
                _mainModule = atmosphereParticles.main;
                _mainModule.startLifetime = 6.0f;
                _mainModule.startSpeed = 0.35f;
                _mainModule.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
                _mainModule.maxParticles = 80;
                _mainModule.simulationSpace = ParticleSystemSimulationSpace.World;
                _mainModule.loop = true;
                _mainModule.startColor = new ParticleSystem.MinMaxGradient(dayParticleColor);

                var emission = atmosphereParticles.emission;
                emission.rateOverTime = 12f;

                var shape = atmosphereParticles.shape;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(25f, 12f, 25f);

                var noise = atmosphereParticles.noise;
                noise.enabled = true;
                noise.strength = 0.45f;
                noise.frequency = 0.3f;
                noise.scrollSpeed = 0.2f;

                var colorOverLifetime = atmosphereParticles.colorOverLifetime;
                colorOverLifetime.enabled = true;
                Gradient alphaGrad = new Gradient();
                alphaGrad.alphaKeys = new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.2f),
                    new GradientAlphaKey(1f, 0.8f),
                    new GradientAlphaKey(0f, 1f)
                };
                colorOverLifetime.color = new ParticleSystem.MinMaxGradient(alphaGrad);

                // Material padrão de partículas
                Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (particleShader == null) particleShader = Shader.Find("Particles/Standard Unlit");
                if (particleShader != null && psRenderer != null)
                {
                    Material mat = new Material(particleShader);
                    mat.name = "M_Atmosphere_Runtime";
                    psRenderer.sharedMaterial = mat;
                }
            }
            else
            {
                _mainModule = atmosphereParticles.main;
            }
        }
    }
}
