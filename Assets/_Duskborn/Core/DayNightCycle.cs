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
            Debug.Log($"[DayNightCycle] Day {_nightSync.Value + 1} begins. ({dayDuration}s)");
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
            Debug.Log($"[DayNightCycle] Night {_nightSync.Value} begins.");
        }

        private void EndNight()
        {
            BroadcastNightEndRpc(_nightSync.Value);
            GameStateManager.Instance?.RegisterNightSurvived();
            Debug.Log($"[DayNightCycle] Night {_nightSync.Value} ends.");

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
            Debug.Log("[DayNightCycle] Night 7 — Boss fight begins. Timer suspended.");
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
            directionalLight.color     = dayNightLightColor.Evaluate(PhaseProgress);
            directionalLight.intensity = dayNightIntensity.Evaluate(PhaseProgress);
        }
    }
}
