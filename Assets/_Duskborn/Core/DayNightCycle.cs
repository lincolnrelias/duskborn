using System;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Duskborn.Core
{
    public enum DayPhase { Day, Night }

    public class DayNightCycle : NetworkBehaviour
    {
        public static DayNightCycle Instance { get; private set; }

        [Header("Timing")]
        [SerializeField] private float dayDuration   = 180f;
        [SerializeField] private float nightDuration = 120f;

        [Header("Lighting")]
        [SerializeField] private Light          directionalLight;
        [SerializeField] private Gradient       dayNightLightColor;
        [SerializeField] private AnimationCurve dayNightIntensity;

        [Header("Skybox & Atmosfera Estilizada")]
        [Tooltip("Material do Skybox Estilizado (ex: M_Skybox_Stylized com shader Duskborn/StylizedSkybox).")]
        [SerializeField] private Material       skyboxMaterial;
        [Tooltip("Sincroniza a cor do Fog com a cor do horizonte do skybox em tempo real.")]
        [SerializeField] private bool           syncFogWithHorizon = true;
        [Tooltip("Controla a rotação dos corpos celestes (arco do Sol e da Lua).")]
        [SerializeField] private bool           rotateCelestialBodies = true;
        [Tooltip("Curva de transição Dia -> Noite (0 = Dia, 1 = Noite). Se vazia, utiliza transição suave automática.")]
        [SerializeField] private AnimationCurve skyboxNightBlendCurve;

        [Header("Elevação Solar & Lunar (Anti-Sombras Esticadas)")]
        [Tooltip("Ângulo mínimo de elevação do Sol (evita sombras esticadas e distorcidas no amanhecer/entardecer).")]
        [Range(30f, 60f)]
        [SerializeField] private float minSunPitch = 42f;
        [Tooltip("Ângulo máximo de elevação do Sol ao meio-dia.")]
        [Range(50f, 85f)]
        [SerializeField] private float maxSunPitch = 68f;
        [Tooltip("Ângulo mínimo de elevação da Lua à noite.")]
        [Range(30f, 60f)]
        [SerializeField] private float minMoonPitch = 38f;
        [Tooltip("Ângulo máximo de elevação da Lua à meia-noite.")]
        [Range(50f, 85f)]
        [SerializeField] private float maxMoonPitch = 62f;

        private readonly SyncVar<float> _timeRemaining = new();
        private readonly SyncVar<int>   _nightSync     = new();
        private readonly SyncVar<bool>  _isDaySync     = new(true);

        public DayPhase Phase             => _isDaySync.Value ? DayPhase.Day : DayPhase.Night;
        public int      CurrentNight      => _nightSync.Value;
        public float    PhaseTimeRemaining => _timeRemaining.Value;
        public float    PhaseDuration      => _isDaySync.Value ? dayDuration : nightDuration;
        public float    PhaseProgress      => PhaseDuration > 0f
                                              ? 1f - (_timeRemaining.Value / PhaseDuration)
                                              : 1f;

        public event Action          OnDayStart;
        public event Action<int>     OnNightStart;
        public event Action<int>     OnNightEnd;

        private bool _running;
        private const int TotalNights = 7;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            if (directionalLight == null)
            {
                directionalLight = RenderSettings.sun;
                if (directionalLight == null)
                {
                    directionalLight = FindAnyObjectByType<Light>();
                }
            }

            if (skyboxMaterial == null)
            {
                skyboxMaterial = RenderSettings.skybox;
            }
#if UNITY_EDITOR
            if (skyboxMaterial == null || skyboxMaterial.shader == null || skyboxMaterial.shader.name != "Duskborn/StylizedSkybox")
            {
                var loaded = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/_Duskborn/Art/Materials/M_Skybox_Stylized.mat");
                if (loaded != null)
                {
                    skyboxMaterial = loaded;
                    RenderSettings.skybox = skyboxMaterial;
                }
            }
#endif
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            StartCycle();
        }

        public void StartCycle()
        {
            _running = true;
            _nightSync.Value = 0;
            BeginDay();
        }

        private void Update()
        {
            UpdateLighting();

            if (!_running || !IsServerStarted) return;

            _timeRemaining.Value -= Time.deltaTime;

            if (_timeRemaining.Value <= 0f)
            {
                if (_isDaySync.Value)
                    BeginNight();
                else
                    EndNight();
            }
        }

        private void BeginDay()
        {
            _isDaySync.Value     = true;
            _timeRemaining.Value = dayDuration;
            BroadcastDayStartRpc();
            DuskLog.Log(LogChannel.DayNightCycle, $"Day {_nightSync.Value + 1} begins. ({dayDuration}s)");
        }

        private void BeginNight()
        {
            _nightSync.Value += 1;

            if (_nightSync.Value >= TotalNights)
            {
                BeginBossNight();
                return;
            }

            _isDaySync.Value     = false;
            _timeRemaining.Value = nightDuration;
            BroadcastNightStartRpc(_nightSync.Value);
            DuskLog.Log(LogChannel.DayNightCycle, $"Night {_nightSync.Value} begins.");
        }

        private void EndNight()
        {
            BroadcastNightEndRpc(_nightSync.Value);
            GameStateManager.Instance?.RegisterNightSurvived();
            DuskLog.Log(LogChannel.DayNightCycle, $"Night {_nightSync.Value} ends.");

            if (_nightSync.Value >= TotalNights)
            {
                _running = false;
                return;
            }

            BeginDay();
        }

        private void BeginBossNight()
        {
            _isDaySync.Value     = false;
            _timeRemaining.Value = float.MaxValue;
            BroadcastNightStartRpc(_nightSync.Value);
            DuskLog.Log(LogChannel.DayNightCycle, "Night 7 — Boss fight begins. Timer suspended.");
        }

        // ── ObserversRpcs fire events on all clients (RunLocally = true includes the server) ──

        [ObserversRpc(RunLocally = true)]
        private void BroadcastDayStartRpc() => OnDayStart?.Invoke();

        [ObserversRpc(RunLocally = true)]
        private void BroadcastNightStartRpc(int nightNumber) => OnNightStart?.Invoke(nightNumber);

        [ObserversRpc(RunLocally = true)]
        private void BroadcastNightEndRpc(int nightNumber) => OnNightEnd?.Invoke(nightNumber);

        // ── Debug / editor helpers ──

        public void ForceEndNight()
        {
            if (!_isDaySync.Value)
                _timeRemaining.Value = 0f;
        }

        public void ForceEndCurrentPhase() => _timeRemaining.Value = 0f;

        private void UpdateLighting()
        {
            if (directionalLight == null) return;

            bool isDay = Phase == DayPhase.Day;
            float progress = PhaseProgress;

            // 1. Rotação Orbital Celeste (Sol de dia, Lua à noite)
            // Elevação calibrada para visão de jogo/isométrica (evita sombras esticadas de 12 graus)
            Vector3 sunSkyDir;
            Vector3 moonSkyDir;

            if (rotateCelestialBodies)
            {
                if (isDay)
                {
                    // Arco do Sol: elevação suave entre minSunPitch (42°) e maxSunPitch (68°)
                    float sunPitch = Mathf.Lerp(minSunPitch, maxSunPitch, Mathf.Sin(progress * Mathf.PI));
                    float sunYaw = Mathf.Lerp(35f, 145f, progress);
                    directionalLight.transform.rotation = Quaternion.Euler(sunPitch, sunYaw, 0f);

                    sunSkyDir = -directionalLight.transform.forward;
                    moonSkyDir = -sunSkyDir;
                }
                else
                {
                    // Arco da Lua à noite: elevação entre minMoonPitch (38°) e maxMoonPitch (62°)
                    float moonPitch = Mathf.Lerp(minMoonPitch, maxMoonPitch, Mathf.Sin(progress * Mathf.PI));
                    float moonYaw = Mathf.Lerp(215f, 325f, progress);
                    directionalLight.transform.rotation = Quaternion.Euler(moonPitch, moonYaw, 0f);

                    moonSkyDir = -directionalLight.transform.forward;
                    sunSkyDir = -moonSkyDir;
                }
            }
            else
            {
                sunSkyDir = -directionalLight.transform.forward;
                moonSkyDir = -sunSkyDir;
            }

            // 2. Cor e Intensidade da Luz Direcional
            if (dayNightLightColor != null && dayNightLightColor.colorKeys.Length > 0)
            {
                directionalLight.color = dayNightLightColor.Evaluate(progress);
            }
            else
            {
                directionalLight.color = isDay ? new Color(1.0f, 0.96f, 0.88f) : new Color(0.60f, 0.75f, 1.0f);
            }

            if (dayNightIntensity != null && dayNightIntensity.keys.Length > 0)
            {
                directionalLight.intensity = dayNightIntensity.Evaluate(progress);
            }
            else
            {
                // Intensidade equilibrada: 1.25f de dia (evita sombras superescuras por contraste excessivo) e 0.35f de noite
                directionalLight.intensity = isDay ? 1.25f : 0.35f;
            }

            // 3. Fator de Transição Dia/Noite do Skybox
            float nightBlend;
            if (skyboxNightBlendCurve != null && skyboxNightBlendCurve.keys.Length > 0)
            {
                nightBlend = skyboxNightBlendCurve.Evaluate(isDay ? progress : (1f + progress));
            }
            else
            {
                if (isDay)
                {
                    if (progress > 0.82f)
                        nightBlend = Mathf.SmoothStep(0f, 1f, (progress - 0.82f) / 0.18f);
                    else if (progress < 0.10f)
                        nightBlend = Mathf.SmoothStep(1f, 0f, progress / 0.10f);
                    else
                        nightBlend = 0f;
                }
                else
                {
                    if (progress > 0.85f)
                        nightBlend = Mathf.SmoothStep(1f, 0f, (progress - 0.85f) / 0.15f);
                    else if (progress < 0.10f)
                        nightBlend = Mathf.SmoothStep(0f, 1f, progress / 0.10f);
                    else
                        nightBlend = 1f;
                }
            }

            // 4. Atualização das propriedades do Material Skybox
            if (skyboxMaterial != null)
            {
                skyboxMaterial.SetFloat("_DayNightBlend", nightBlend);
                skyboxMaterial.SetVector("_SunDirection", sunSkyDir);
                skyboxMaterial.SetVector("_MoonDirection", moonSkyDir);

                // 5. Sincronização Perfeita de Neblina (MixFog) com o Horizonte do Skybox
                if (syncFogWithHorizon)
                {
                    Color dayHoriz = skyboxMaterial.HasProperty("_DayHorizonColor") 
                        ? skyboxMaterial.GetColor("_DayHorizonColor") 
                        : new Color(0.72f, 0.88f, 0.98f);
                    Color nightHoriz = skyboxMaterial.HasProperty("_NightHorizonColor") 
                        ? skyboxMaterial.GetColor("_NightHorizonColor") 
                        : new Color(0.12f, 0.18f, 0.32f);
                    Color duskCol = skyboxMaterial.HasProperty("_DuskDawnColor") 
                        ? skyboxMaterial.GetColor("_DuskDawnColor") 
                        : new Color(1.0f, 0.48f, 0.22f);

                    Color horizonCol = Color.Lerp(dayHoriz, nightHoriz, nightBlend);

                    // Alvorecer e Crepúsculo no horizonte baseado na transição do dia
                    float dayTwilight = isDay ? Mathf.Clamp01(1f - Mathf.Sin(progress * Mathf.PI)) : 0f;
                    horizonCol = Color.Lerp(horizonCol, duskCol, dayTwilight * 0.65f);

                    RenderSettings.fog = true;
                    RenderSettings.fogMode = FogMode.ExponentialSquared;
                    RenderSettings.fogColor = horizonCol;
                    if (RenderSettings.fogDensity <= 0.0001f)
                    {
                        RenderSettings.fogDensity = 0.0055f;
                    }

                    // Iluminação Ambiente Trilight Coerente (clareia sombras, eliminando dureza/escuridão extrema)
                    Color dayZenith = skyboxMaterial.HasProperty("_DayZenithColor") ? skyboxMaterial.GetColor("_DayZenithColor") : new Color(0.28f, 0.58f, 0.95f);
                    Color nightZenith = skyboxMaterial.HasProperty("_NightZenithColor") ? skyboxMaterial.GetColor("_NightZenithColor") : new Color(0.04f, 0.06f, 0.16f);
                    Color dayGround = skyboxMaterial.HasProperty("_DayGroundColor") ? skyboxMaterial.GetColor("_DayGroundColor") : new Color(0.35f, 0.45f, 0.38f);
                    Color nightGround = skyboxMaterial.HasProperty("_NightGroundColor") ? skyboxMaterial.GetColor("_NightGroundColor") : new Color(0.03f, 0.04f, 0.08f);

                    RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
                    RenderSettings.ambientSkyColor = Color.Lerp(dayZenith * 0.65f, nightZenith * 0.45f, nightBlend);
                    RenderSettings.ambientEquatorColor = Color.Lerp(horizonCol * 0.55f, nightHoriz * 0.35f, nightBlend);
                    RenderSettings.ambientGroundColor = Color.Lerp(dayGround * 0.45f, nightGround * 0.25f, nightBlend);
                }
            }
        }
    }
}
