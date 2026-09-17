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
        [SerializeField] private float minSunPitch = 40f;
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

            if (GetComponent<Duskborn.Audio.DayNightAudio>() == null)
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
            Quaternion sunRot;
            Quaternion moonRot;

            float sunPitch = Mathf.Lerp(minSunPitch, maxSunPitch, Mathf.Sin(progress * Mathf.PI));
            float sunYaw = Mathf.Lerp(35f, 145f, progress);
            sunRot = Quaternion.Euler(sunPitch, sunYaw, 0f);

            float moonPitch = Mathf.Lerp(minMoonPitch, maxMoonPitch, Mathf.Sin(progress * Mathf.PI));
            float moonYaw = Mathf.Lerp(215f, 325f, progress);
            moonRot = Quaternion.Euler(moonPitch, moonYaw, 0f);

            // Transição suave (Slerp) do DirectionalLight entre Pôr do Sol e Nascer da Lua (e vice-versa)
            // sem descontinuidades nem estalos de sombra
            if (rotateCelestialBodies)
            {
                if (isDay)
                {
                    if (progress > 0.90f)
                    {
                        // Últimos 10% do dia: transição contínua entre poente do sol e levante da lua
                        float blend = Mathf.SmoothStep(0f, 1f, (progress - 0.90f) / 0.10f * 0.5f);
                        directionalLight.transform.rotation = Quaternion.Slerp(sunRot, Quaternion.Euler(minMoonPitch, 215f, 0f), blend);
                    }
                    else if (progress < 0.08f)
                    {
                        // Primeiros 8% do dia: transição contínua a partir do poente da lua
                        float blend = Mathf.SmoothStep(0f, 1f, 0.5f + (progress / 0.08f) * 0.5f);
                        directionalLight.transform.rotation = Quaternion.Slerp(Quaternion.Euler(minMoonPitch, 325f, 0f), sunRot, blend);
                    }
                    else
                    {
                        directionalLight.transform.rotation = sunRot;
                    }
                }
                else
                {
                    if (progress < 0.10f)
                    {
                        // Primeiros 10% da noite: continuidade perfeita com o fim do dia
                        float blend = Mathf.SmoothStep(0f, 1f, 0.5f + (progress / 0.10f) * 0.5f);
                        directionalLight.transform.rotation = Quaternion.Slerp(Quaternion.Euler(minSunPitch, 145f, 0f), moonRot, blend);
                    }
                    else if (progress > 0.92f)
                    {
                        // Últimos 8% da noite: início da passagem suave para a aurora
                        float blend = Mathf.SmoothStep(0f, 1f, (progress - 0.92f) / 0.08f * 0.5f);
                        directionalLight.transform.rotation = Quaternion.Slerp(moonRot, Quaternion.Euler(minSunPitch, 35f, 0f), blend);
                    }
                    else
                    {
                        directionalLight.transform.rotation = moonRot;
                    }
                }
            }

            // Vetores celestes contínuos para o Skybox (Sol afunda suavemente sob o horizonte à noite, Lua de dia)
            Vector3 sunSkyDir;
            Vector3 moonSkyDir;

            if (isDay)
            {
                float skySunPitch = Mathf.Sin(progress * Mathf.PI) * maxSunPitch;
                float skySunYaw = Mathf.Lerp(35f, 145f, progress);
                sunSkyDir = Quaternion.Euler(skySunPitch, skySunYaw, 0f) * Vector3.forward;

                float skyMoonPitch = -Mathf.Sin(progress * Mathf.PI) * maxMoonPitch;
                float skyMoonYaw = Mathf.Lerp(215f, 325f, progress);
                moonSkyDir = Quaternion.Euler(skyMoonPitch, skyMoonYaw, 0f) * Vector3.forward;
            }
            else
            {
                float skySunPitch = -Mathf.Sin(progress * Mathf.PI) * maxSunPitch;
                float skySunYaw = Mathf.Lerp(145f, 395f, progress);
                sunSkyDir = Quaternion.Euler(skySunPitch, skySunYaw, 0f) * Vector3.forward;

                float skyMoonPitch = Mathf.Sin(progress * Mathf.PI) * maxMoonPitch;
                float skyMoonYaw = Mathf.Lerp(215f, 325f, progress);
                moonSkyDir = Quaternion.Euler(skyMoonPitch, skyMoonYaw, 0f) * Vector3.forward;
            }

            // 2. Cor e Intensidade Progressivas da Luz Direcional
            if (dayNightLightColor != null && dayNightLightColor.colorKeys.Length > 2)
            {
                directionalLight.color = dayNightLightColor.Evaluate(isDay ? progress * 0.5f : 0.5f + progress * 0.5f);
            }
            else
            {
                directionalLight.color = EvaluateProceduralLightColor(isDay, progress);
            }

            if (dayNightIntensity != null && dayNightIntensity.keys.Length > 0)
            {
                directionalLight.intensity = dayNightIntensity.Evaluate(isDay ? progress * 0.5f : 0.5f + progress * 0.5f);
            }
            else
            {
                directionalLight.intensity = EvaluateProceduralIntensity(isDay, progress);
            }

            // 3. Fator de Transição Contínuo do Skybox (0 = Dia, 1 = Noite)
            // Continuidade exata: t=1.0 de dia coincide exatamente com t=0.0 de noite (0.65f), e vice-versa no amanhecer (0.50f)
            float nightBlend;
            if (skyboxNightBlendCurve != null && skyboxNightBlendCurve.keys.Length > 0)
            {
                nightBlend = skyboxNightBlendCurve.Evaluate(isDay ? progress * 0.5f : 0.5f + progress * 0.5f);
            }
            else
            {
                if (isDay)
                {
                    if (progress < 0.15f)
                    {
                        // Alvorecer: transita de 0.50f (aurora da madrugada) para 0.0f (dia límpido)
                        nightBlend = Mathf.Lerp(0.50f, 0.0f, Mathf.SmoothStep(0f, 1f, progress / 0.15f));
                    }
                    else if (progress > 0.80f)
                    {
                        // Crepúsculo: transita suavemente de 0.0f para 0.65f (pôr do sol avermelhado)
                        nightBlend = Mathf.Lerp(0.0f, 0.65f, Mathf.SmoothStep(0f, 1f, (progress - 0.80f) / 0.20f));
                    }
                    else
                    {
                        nightBlend = 0f;
                    }
                }
                else
                {
                    if (progress < 0.15f)
                    {
                        // Início da noite: continua de 0.65f até 1.0f (noite completa)
                        nightBlend = Mathf.Lerp(0.65f, 1.0f, Mathf.SmoothStep(0f, 1f, progress / 0.15f));
                    }
                    else if (progress > 0.82f)
                    {
                        // Madrugada: transita suavemente de 1.0f para 0.50f (primeira claridade da alvorada)
                        nightBlend = Mathf.Lerp(1.0f, 0.50f, Mathf.SmoothStep(0f, 1f, (progress - 0.82f) / 0.18f));
                    }
                    else
                    {
                        nightBlend = 1f;
                    }
                }
            }

            // 4. Atualização das propriedades do Material Skybox
            if (skyboxMaterial != null)
            {
                skyboxMaterial.SetFloat("_DayNightBlend", nightBlend);
                skyboxMaterial.SetVector("_SunDirection", sunSkyDir);
                skyboxMaterial.SetVector("_MoonDirection", moonSkyDir);

                // 5. Sincronização Contínua de Neblina com o Horizonte e Alvorada/Crepúsculo
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
                    if (RenderSettings.fogDensity <= 0.0001f)
                    {
                        RenderSettings.fogDensity = 0.0055f;
                    }

                    // Iluminação Ambiente Trilight Coerente e Suave
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
