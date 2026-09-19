using System;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Duskborn.Core
{
    public enum DayPhase { Day, Night }

    public enum CyclePeriod
    {
        Dawn,       // Alvorecer / Amanhecer
        Morning,    // Manhã
        Midday,     // Meio-dia
        Afternoon,  // Entardecer / Tarde
        Dusk,       // Crepúsculo (Aviso tático)
        Nightfall,  // Anoitecer (Início da Noite e das hordas)
        Midnight,   // Meia-noite (Pico)
        PreDawn     // Madrugada (Fim da noite)
    }

    [ExecuteAlways]
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

        [Header("Neblina Atmosférica Dinâmica")]
        [Tooltip("Ativa a modulação contínua da densidade da neblina de acordo com a fase do dia/noite.")]
        [SerializeField] private bool           dynamicFogDensity = true;
        [Tooltip("Densidade da neblina durante o dia claro (visibilidade ampla e límpida).")]
        [Range(0.001f, 0.03f)]
        [SerializeField] private float          dayFogDensity = 0.0038f;
        [Tooltip("Densidade da neblina no alvorecer e crepúsculo (névoa atmosférica dourada/âmbar).")]
        [Range(0.001f, 0.03f)]
        [SerializeField] private float          duskDawnFogDensity = 0.0068f;
        [Tooltip("Densidade da neblina à noite (profundidade misteriosa e atmosfera enluarada).")]
        [Range(0.001f, 0.04f)]
        [SerializeField] private float          nightFogDensity = 0.0095f;
        [Tooltip("Densidade da neblina na Noite 7 do Chefe (bruma carmesim opressiva).")]
        [Range(0.001f, 0.05f)]
        [SerializeField] private float          bossNightFogDensity = 0.0145f;

        [Header("Elevação Solar & Lunar (Anti-Sombras Esticadas)")]
        [Tooltip("Ângulo mínimo de elevação do Sol (evita sombras esticadas e distorcidas no amanhecer/entardecer).")]
        [Range(15f, 60f)]
        [SerializeField] private float minSunPitch = 32f;
        [Tooltip("Ângulo máximo de elevação do Sol ao meio-dia.")]
        [Range(45f, 85f)]
        [SerializeField] private float maxSunPitch = 68f;
        [Tooltip("Ângulo mínimo de elevação da Lua à noite.")]
        [Range(15f, 60f)]
        [SerializeField] private float minMoonPitch = 28f;
        [Tooltip("Ângulo máximo de elevação da Lua à meia-noite.")]
        [Range(45f, 85f)]
        [SerializeField] private float maxMoonPitch = 62f;

        private readonly SyncVar<float> _timeRemaining = new();
        private readonly SyncVar<int>   _nightSync     = new();
        private readonly SyncVar<bool>  _isDaySync     = new(true);

        public DayPhase    Phase              => _isDaySync.Value ? DayPhase.Day : DayPhase.Night;
        public int         CurrentNight       => _nightSync.Value;
        public float       PhaseTimeRemaining => _timeRemaining.Value;
        public float       PhaseDuration      => _isDaySync.Value ? dayDuration : nightDuration;
        public float       PhaseProgress      => PhaseDuration > 0f
                                                 ? Mathf.Clamp01(1f - (_timeRemaining.Value / PhaseDuration))
                                                 : 1f;

        public CyclePeriod CurrentPeriod      { get; private set; } = CyclePeriod.Dawn;
        public float       CycleProgress      { get; private set; }
        public float       ClockHours         { get; private set; }
        public string      ClockTimeString    { get; private set; } = "06:00";
        public string      PeriodDisplayName  { get; private set; } = "Alvorecer";
        public bool        IsDay              => Phase == DayPhase.Day;
        public bool        IsNight            => Phase == DayPhase.Night;
        public bool        IsDusk             => CurrentPeriod == CyclePeriod.Dusk;
        public bool        IsDawn             => CurrentPeriod == CyclePeriod.Dawn;
        public float       CurrentFogDensity  => RenderSettings.fogDensity;
        public Color       CurrentFogColor    => RenderSettings.fogColor;

        public event Action              OnDayStart;
        public event Action<int>         OnNightStart;
        public event Action<int>         OnNightEnd;
        public event Action<CyclePeriod> OnPeriodChanged;
        public event Action              OnDuskStart;
        public event Action              OnDawnStart;

        private bool _running;
        private const int TotalNights = 7;
        private CyclePeriod _lastPeriod = (CyclePeriod)(-1);

        private void Awake()
        {
            if (Application.isPlaying && Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            if (directionalLight == null)
            {
                directionalLight = RenderSettings.sun;
                if (directionalLight == null)
                {
                    directionalLight = FindAnyObjectByType<Light>();
                }
            }

            if (directionalLight != null && RenderSettings.sun == null)
            {
                RenderSettings.sun = directionalLight;
            }

            if (skyboxMaterial == null)
            {
                skyboxMaterial = RenderSettings.skybox;
            }

            if (Application.isPlaying && GetComponent<Duskborn.Audio.DayNightAudio>() == null)
                gameObject.AddComponent<Duskborn.Audio.DayNightAudio>();
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

        private void OnEnable()
        {
            Instance = this;
            if (directionalLight == null)
            {
                directionalLight = RenderSettings.sun != null ? RenderSettings.sun : FindAnyObjectByType<Light>();
            }
            if (directionalLight != null && RenderSettings.sun == null)
            {
                RenderSettings.sun = directionalLight;
            }
            if (skyboxMaterial == null)
            {
                skyboxMaterial = RenderSettings.skybox;
            }
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
            UpdateProgressiveTime();
            UpdateLighting();

            if (!Application.isPlaying) return;
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

        private void UpdateProgressiveTime()
        {
            float p = PhaseProgress;
            bool isDay = _isDaySync.Value;

            float totalCycleTime = Mathf.Max(1f, dayDuration + nightDuration);
            float dayFraction = dayDuration / totalCycleTime;
            float nightFraction = nightDuration / totalCycleTime;

            if (isDay)
            {
                CycleProgress = p * dayFraction;
                // Dia cobre 06:00 às 20:00 (14 horas de claridade)
                ClockHours = 6f + p * 14f;

                if (p < 0.15f)
                    CurrentPeriod = CyclePeriod.Dawn;
                else if (p < 0.50f)
                    CurrentPeriod = CyclePeriod.Morning;
                else if (p < 0.72f)
                    CurrentPeriod = CyclePeriod.Midday;
                else if (p < 0.85f)
                    CurrentPeriod = CyclePeriod.Afternoon;
                else
                    CurrentPeriod = CyclePeriod.Dusk;
            }
            else
            {
                CycleProgress = dayFraction + p * nightFraction;
                // Noite cobre 20:00 às 06:00 (10 horas de escuridão)
                ClockHours = (20f + p * 10f) % 24f;

                if (p < 0.25f)
                    CurrentPeriod = CyclePeriod.Nightfall;
                else if (p < 0.75f)
                    CurrentPeriod = CyclePeriod.Midnight;
                else
                    CurrentPeriod = CyclePeriod.PreDawn;
            }

            int hours = Mathf.FloorToInt(ClockHours);
            int minutes = Mathf.FloorToInt((ClockHours - hours) * 60f);
            ClockTimeString = $"{hours:00}:{minutes:00}";

            PeriodDisplayName = CurrentPeriod switch
            {
                CyclePeriod.Dawn      => "Alvorecer",
                CyclePeriod.Morning   => "Manhã",
                CyclePeriod.Midday    => "Meio-dia",
                CyclePeriod.Afternoon => "Entardecer",
                CyclePeriod.Dusk      => "Crepúsculo",
                CyclePeriod.Nightfall => "Anoitecer",
                CyclePeriod.Midnight  => "Meia-noite",
                CyclePeriod.PreDawn   => "Madrugada",
                _                     => "Dia"
            };

            if (CurrentPeriod != _lastPeriod)
            {
                _lastPeriod = CurrentPeriod;
                OnPeriodChanged?.Invoke(CurrentPeriod);
                if (CurrentPeriod == CyclePeriod.Dusk) OnDuskStart?.Invoke();
                if (CurrentPeriod == CyclePeriod.Dawn) OnDawnStart?.Invoke();
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

            // 1. Rotação Orbital Celeste Contínua e Sem Saltos
            CalculateCelestialVectors(
                isDay, progress, minSunPitch, maxSunPitch, minMoonPitch, maxMoonPitch,
                out Vector3 sunSkyDir, out Vector3 moonSkyDir, out Vector3 lightForward
            );

            if (rotateCelestialBodies)
            {
                directionalLight.transform.rotation = Quaternion.LookRotation(lightForward, Vector3.up);
            }

            // 2. Cor e Intensidade Progressivas da Luz Direcional
            Color lightColor;
            if (dayNightLightColor != null && dayNightLightColor.colorKeys.Length > 2)
            {
                lightColor = dayNightLightColor.Evaluate(isDay ? progress * 0.5f : 0.5f + progress * 0.5f);
            }
            else
            {
                lightColor = EvaluateProceduralLightColor(isDay, progress);
            }

            // Boss Night 7: Matiz carmesim do chefe
            bool isBossNight = !isDay && CurrentNight >= TotalNights;
            if (isBossNight)
            {
                lightColor = Color.Lerp(lightColor, new Color(1.0f, 0.28f, 0.22f), 0.75f);
            }
            directionalLight.color = lightColor;

            float intensity;
            if (dayNightIntensity != null && dayNightIntensity.keys.Length > 0)
            {
                intensity = dayNightIntensity.Evaluate(isDay ? progress * 0.5f : 0.5f + progress * 0.5f);
            }
            else
            {
                intensity = EvaluateProceduralIntensity(isDay, progress);
            }
            if (isBossNight)
            {
                intensity = Mathf.Max(intensity, 0.42f);
            }
            directionalLight.intensity = intensity;

            // Ajuste dinâmico de sombra (sombras nítidas de dia, etéreas e suaves à noite)
            directionalLight.shadowStrength = isDay 
                ? Mathf.Lerp(0.75f, 0.85f, Mathf.Sin(progress * Mathf.PI))
                : (isBossNight ? 0.70f : 0.55f);

            // 3. Fator de Transição Contínuo do Skybox (0 = Dia, 1 = Noite)
            float nightBlend = CalculateNightBlendFactor(isDay, progress, skyboxNightBlendCurve);

            // 4. Atualização das propriedades do Material Skybox
            if (skyboxMaterial != null)
            {
                skyboxMaterial.SetFloat("_DayNightBlend", nightBlend);
                skyboxMaterial.SetFloat("_BossNightBlend", isBossNight ? 1.0f : 0.0f);
                skyboxMaterial.SetVector("_SunDirection", sunSkyDir);
                skyboxMaterial.SetVector("_MoonDirection", moonSkyDir);
            }

            // 5. Sincronização Contínua de Neblina com o Horizonte e Alvorada/Crepúsculo
            if (syncFogWithHorizon)
            {
                Color dayHoriz = (skyboxMaterial != null && skyboxMaterial.HasProperty("_DayHorizonColor")) 
                    ? skyboxMaterial.GetColor("_DayHorizonColor") 
                    : new Color(0.72f, 0.88f, 0.98f);
                Color nightHoriz = (skyboxMaterial != null && skyboxMaterial.HasProperty("_NightHorizonColor")) 
                    ? skyboxMaterial.GetColor("_NightHorizonColor") 
                    : new Color(0.12f, 0.18f, 0.32f);
                Color duskCol = (skyboxMaterial != null && skyboxMaterial.HasProperty("_DuskDawnColor")) 
                    ? skyboxMaterial.GetColor("_DuskDawnColor") 
                    : new Color(1.0f, 0.48f, 0.22f);

                if (isBossNight)
                {
                    nightHoriz = Color.Lerp(nightHoriz, new Color(0.55f, 0.12f, 0.10f), 0.85f);
                }

                Color horizonCol = Color.Lerp(dayHoriz, nightHoriz, nightBlend);

                // Realce de Crepúsculo e Alvorecer na névoa com curva parabólica contínua
                float twilightFactor = 0f;
                if (isDay)
                {
                    if (progress > 0.80f)
                        twilightFactor = Mathf.Sin((progress - 0.80f) / 0.20f * Mathf.PI);
                    else if (progress < 0.15f)
                        twilightFactor = Mathf.Sin(progress / 0.15f * Mathf.PI);
                }
                else
                {
                    if (progress > 0.82f)
                        twilightFactor = Mathf.Sin((progress - 0.82f) / 0.18f * Mathf.PI) * 0.45f;
                }

                horizonCol = Color.Lerp(horizonCol, duskCol, twilightFactor * 0.55f);

                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogColor = horizonCol;
                if (dynamicFogDensity)
                {
                    float baseDensity = Mathf.Lerp(dayFogDensity, nightFogDensity, nightBlend);
                    if (twilightFactor > 0.001f)
                    {
                        baseDensity = Mathf.Lerp(baseDensity, duskDawnFogDensity, twilightFactor);
                    }
                    if (isBossNight)
                    {
                        baseDensity = Mathf.Lerp(baseDensity, bossNightFogDensity, 0.85f);
                    }
                    RenderSettings.fogDensity = baseDensity;
                }
                else if (RenderSettings.fogDensity <= 0.0001f)
                {
                    RenderSettings.fogDensity = 0.0055f;
                }

                // Iluminação Ambiente Trilight Coerente e Suave
                Color dayZenith = (skyboxMaterial != null && skyboxMaterial.HasProperty("_DayZenithColor")) ? skyboxMaterial.GetColor("_DayZenithColor") : new Color(0.28f, 0.58f, 0.95f);
                Color nightZenith = (skyboxMaterial != null && skyboxMaterial.HasProperty("_NightZenithColor")) ? skyboxMaterial.GetColor("_NightZenithColor") : new Color(0.04f, 0.06f, 0.16f);
                Color dayGround = (skyboxMaterial != null && skyboxMaterial.HasProperty("_DayGroundColor")) ? skyboxMaterial.GetColor("_DayGroundColor") : new Color(0.35f, 0.45f, 0.38f);
                Color nightGround = (skyboxMaterial != null && skyboxMaterial.HasProperty("_NightGroundColor")) ? skyboxMaterial.GetColor("_NightGroundColor") : new Color(0.03f, 0.04f, 0.08f);

                if (isBossNight)
                {
                    nightZenith = Color.Lerp(nightZenith, new Color(0.25f, 0.05f, 0.07f), 0.85f);
                    nightGround = Color.Lerp(nightGround, new Color(0.12f, 0.02f, 0.03f), 0.85f);
                }

                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = Color.Lerp(dayZenith * 0.65f, nightZenith * 0.45f, nightBlend);
                RenderSettings.ambientEquatorColor = Color.Lerp(horizonCol * 0.55f, nightHoriz * 0.35f, nightBlend);
                RenderSettings.ambientGroundColor = Color.Lerp(dayGround * 0.45f, nightGround * 0.25f, nightBlend);

                // Sincronização da cor de sombra subtrativa
                Color dayShadowColor = new Color(0.42f, 0.48f, 0.63f, 1.0f);
                Color nightShadowColor = isBossNight ? new Color(0.35f, 0.10f, 0.12f, 1.0f) : new Color(0.12f, 0.14f, 0.25f, 1.0f);
                RenderSettings.subtractiveShadowColor = Color.Lerp(dayShadowColor, nightShadowColor, nightBlend);
            }
        }

        public static void CalculateCelestialVectors(
            bool isDay,
            float progress,
            float minSunPitch,
            float maxSunPitch,
            float minMoonPitch,
            float maxMoonPitch,
            out Vector3 sunSkyDir,
            out Vector3 moonSkyDir,
            out Vector3 lightForward)
        {
            // O Sol nasce a Leste (yaw 75°), passa pelo Sul ao meio-dia (yaw 180°), e se põe a Oeste (yaw 285°)
            float daySunYaw = Mathf.Lerp(75f, 285f, progress);
            float daySunPitch = Mathf.Sin(progress * Mathf.PI) * (maxSunPitch + 6f) - 3f;

            // À noite, o Sol viaja sob a terra pelo Norte
            float nightSunYaw = Mathf.Lerp(285f, 435f, progress) % 360f;
            float nightSunPitch = -Mathf.Sin(progress * Mathf.PI) * (maxSunPitch + 6f) - 3f;

            // A Lua nasce a Leste ao anoitecer, passa pelo Sul à meia-noite, e se põe a Oeste ao amanhecer
            float nightMoonYaw = Mathf.Lerp(75f, 285f, progress);
            float nightMoonPitch = Mathf.Sin(progress * Mathf.PI) * (maxMoonPitch + 6f) - 3f;

            // De dia, a Lua viaja sob a terra pelo Norte
            float dayMoonYaw = Mathf.Lerp(285f, 435f, progress) % 360f;
            float dayMoonPitch = -Mathf.Sin(progress * Mathf.PI) * (maxMoonPitch + 6f) - 3f;

            float sunPitch = isDay ? daySunPitch : nightSunPitch;
            float sunYaw   = isDay ? daySunYaw   : nightSunYaw;

            float moonPitch = isDay ? dayMoonPitch : nightMoonPitch;
            float moonYaw   = isDay ? dayMoonYaw   : nightMoonYaw;

            // Vetores celestes tridimensionais absolutos (Y > 0 quando visível no céu)
            sunSkyDir = SphericalToDirection(sunPitch, sunYaw);
            moonSkyDir = SphericalToDirection(moonPitch, moonYaw);

            // Cálculo dos vetores de luz direcional para o solo (com pitch mínimo anti-sombras esticadas)
            float sunLightPitch = Mathf.Max(minSunPitch, sunPitch);
            Vector3 sunLightForward = -SphericalToDirection(sunLightPitch, daySunYaw);

            float moonLightPitch = Mathf.Max(minMoonPitch, nightMoonPitch);
            Vector3 moonLightForward = -SphericalToDirection(moonLightPitch, nightMoonYaw);

            // Vetores âncora para transição sem estalos nem descontinuidades nas passagens Dia/Noite
            // No poente: o Sol se põe a Oeste (285°), e a Lua nasce a Leste (75°)
            Vector3 sunsetSunLightFwd    = -SphericalToDirection(minSunPitch, 285f);
            Vector3 moonriseMoonLightFwd = -SphericalToDirection(minMoonPitch, 75f);

            // Na alvorada: a Lua se põe a Oeste (285°), e o Sol nasce a Leste (75°)
            Vector3 moonsetMoonLightFwd  = -SphericalToDirection(minMoonPitch, 285f);
            Vector3 sunriseSunLightFwd   = -SphericalToDirection(minSunPitch, 75f);

            // Continuidade perfeita entre Pôr do Sol e Nascer da Lua (e vice-versa)
            if (isDay)
            {
                if (progress > 0.90f)
                {
                    // Últimos 10% do dia: Slerp contínuo entre o poente do Sol (Oeste) e o levante da Lua (Leste)
                    float t = Mathf.SmoothStep(0f, 1f, (progress - 0.90f) / 0.10f * 0.5f);
                    lightForward = Vector3.Slerp(sunLightForward, moonriseMoonLightFwd, t);
                }
                else if (progress < 0.08f)
                {
                    // Primeiros 8% do dia: transição contínua a partir do poente da Lua (Oeste) para o Sol nascente (Leste)
                    float t = Mathf.SmoothStep(0f, 1f, 0.5f + (progress / 0.08f) * 0.5f);
                    lightForward = Vector3.Slerp(moonsetMoonLightFwd, sunLightForward, t);
                }
                else
                {
                    lightForward = sunLightForward;
                }
            }
            else
            {
                if (progress < 0.10f)
                {
                    // Primeiros 10% da noite: continuidade exata com o fim do dia
                    float t = Mathf.SmoothStep(0f, 1f, 0.5f + (progress / 0.10f) * 0.5f);
                    lightForward = Vector3.Slerp(sunsetSunLightFwd, moonLightForward, t);
                }
                else if (progress > 0.92f)
                {
                    // Últimos 8% da noite: transição suave e contínua para o nascer do Sol (Leste)
                    float t = Mathf.SmoothStep(0f, 1f, (progress - 0.92f) / 0.08f * 0.5f);
                    lightForward = Vector3.Slerp(moonLightForward, sunriseSunLightFwd, t);
                }
                else
                {
                    lightForward = moonLightForward;
                }
            }
        }

        public static Vector3 SphericalToDirection(float pitchDeg, float yawDeg)
        {
            float radPitch = pitchDeg * Mathf.Deg2Rad;
            float radYaw = yawDeg * Mathf.Deg2Rad;
            float cosP = Mathf.Cos(radPitch);
            return new Vector3(
                cosP * Mathf.Sin(radYaw),
                Mathf.Sin(radPitch),
                cosP * Mathf.Cos(radYaw)
            ).normalized;
        }

        public static float CalculateNightBlendFactor(bool isDay, float progress, AnimationCurve customCurve = null)
        {
            if (customCurve != null && customCurve.keys.Length > 0)
            {
                return customCurve.Evaluate(isDay ? progress * 0.5f : 0.5f + progress * 0.5f);
            }

            if (isDay)
            {
                if (progress < 0.15f)
                {
                    // Alvorecer: transita de 0.50f (aurora da madrugada) para 0.0f (dia límpido)
                    return Mathf.Lerp(0.50f, 0.0f, Mathf.SmoothStep(0f, 1f, progress / 0.15f));
                }
                if (progress > 0.80f)
                {
                    // Crepúsculo: transita suavemente de 0.0f para 0.65f (pôr do sol avermelhado)
                    return Mathf.Lerp(0.0f, 0.65f, Mathf.SmoothStep(0f, 1f, (progress - 0.80f) / 0.20f));
                }
                return 0f;
            }
            else
            {
                if (progress < 0.15f)
                {
                    // Início da noite: continua de 0.65f até 1.0f (noite completa)
                    return Mathf.Lerp(0.65f, 1.0f, Mathf.SmoothStep(0f, 1f, progress / 0.15f));
                }
                if (progress > 0.82f)
                {
                    // Madrugada: transita suavemente de 1.0f para 0.50f (primeira claridade da alvorada)
                    return Mathf.Lerp(1.0f, 0.50f, Mathf.SmoothStep(0f, 1f, (progress - 0.82f) / 0.18f));
                }
                return 1f;
            }
        }

#if UNITY_EDITOR
        public void SetPhaseAndProgressForEditor(bool isDay, float progress, int nightNumber = 1)
        {
            _isDaySync.Value = isDay;
            _nightSync.Value = nightNumber;
            float duration = isDay ? dayDuration : nightDuration;
            _timeRemaining.Value = Mathf.Clamp01(1f - progress) * duration;
            UpdateProgressiveTime();
            UpdateLighting();
        }
#endif

        private Color EvaluateProceduralLightColor(bool isDay, float progress)
        {
            Color dawnColor      = new Color(1.0f, 0.82f, 0.68f); // Alvorecer dourado suave
            Color dayColor       = new Color(1.0f, 0.96f, 0.88f); // Dia límpido e quente
            Color afternoonColor = new Color(1.0f, 0.90f, 0.74f); // Tarde dourada
            Color duskColor      = new Color(1.0f, 0.52f, 0.24f); // Crepúsculo carmesim/âmbar
            Color twilightColor  = new Color(0.48f, 0.52f, 0.72f); // Transição crepúsculo/noite
            Color nightColor     = new Color(0.60f, 0.75f, 1.0f);  // Luar etéreo
            Color preDawnColor   = new Color(0.70f, 0.65f, 0.88f); // Madrugada violeta suave

            if (isDay)
            {
                if (progress < 0.15f)
                {
                    float t = Mathf.SmoothStep(0f, 1f, progress / 0.15f);
                    return Color.Lerp(dawnColor, dayColor, t);
                }
                if (progress < 0.70f)
                {
                    return dayColor;
                }
                if (progress < 0.85f)
                {
                    float t = Mathf.SmoothStep(0f, 1f, (progress - 0.70f) / 0.15f);
                    return Color.Lerp(dayColor, afternoonColor, t);
                }
                if (progress < 0.95f)
                {
                    float t = Mathf.SmoothStep(0f, 1f, (progress - 0.85f) / 0.10f);
                    return Color.Lerp(afternoonColor, duskColor, t);
                }
                float tDuskToNight = Mathf.SmoothStep(0f, 1f, (progress - 0.95f) / 0.05f);
                return Color.Lerp(duskColor, twilightColor, tDuskToNight);
            }
            else
            {
                if (progress < 0.10f)
                {
                    float t = Mathf.SmoothStep(0f, 1f, progress / 0.10f);
                    return Color.Lerp(twilightColor, nightColor, t);
                }
                if (progress < 0.80f)
                {
                    return nightColor;
                }
                if (progress < 0.95f)
                {
                    float t = Mathf.SmoothStep(0f, 1f, (progress - 0.80f) / 0.15f);
                    return Color.Lerp(nightColor, preDawnColor, t);
                }
                float tNightToDay = Mathf.SmoothStep(0f, 1f, (progress - 0.95f) / 0.05f);
                return Color.Lerp(preDawnColor, dawnColor, tNightToDay);
            }
        }

        private float EvaluateProceduralIntensity(bool isDay, float progress)
        {
            const float dayPeak     = 1.25f;
            const float dawnStart   = 0.55f;
            const float duskEnd     = 0.35f;
            const float nightNormal = 0.35f;
            const float midnight    = 0.28f;

            if (isDay)
            {
                if (progress < 0.15f)
                {
                    float t = Mathf.SmoothStep(0f, 1f, progress / 0.15f);
                    return Mathf.Lerp(dawnStart, dayPeak, t);
                }
                if (progress < 0.70f)
                {
                    return dayPeak;
                }
                if (progress < 0.85f)
                {
                    float t = Mathf.SmoothStep(0f, 1f, (progress - 0.70f) / 0.15f);
                    return Mathf.Lerp(dayPeak, 1.10f, t);
                }
                float tDusk = Mathf.SmoothStep(0f, 1f, (progress - 0.85f) / 0.15f);
                return Mathf.Lerp(1.10f, duskEnd, tDusk);
            }
            else
            {
                if (progress < 0.15f)
                {
                    float t = Mathf.SmoothStep(0f, 1f, progress / 0.15f);
                    return Mathf.Lerp(duskEnd, nightNormal, t);
                }
                if (progress < 0.50f)
                {
                    float t = Mathf.SmoothStep(0f, 1f, (progress - 0.15f) / 0.35f);
                    return Mathf.Lerp(nightNormal, midnight, t);
                }
                if (progress < 0.80f)
                {
                    float t = Mathf.SmoothStep(0f, 1f, (progress - 0.50f) / 0.30f);
                    return Mathf.Lerp(midnight, nightNormal, t);
                }
                float tDawn = Mathf.SmoothStep(0f, 1f, (progress - 0.80f) / 0.20f);
                return Mathf.Lerp(nightNormal, dawnStart, tDawn);
            }
        }
    }
}
