using System;
using UnityEngine;

namespace Duskborn.Core
{
    public enum DayPhase { Day, Night }

    /// <summary>
    /// Drives the 7-night day/night loop. Host-authoritative (network sync added in Phase 9).
    /// Fires events that Wave, World, and UI systems subscribe to.
    /// </summary>
    public class DayNightCycle : MonoBehaviour
    {
        public static DayNightCycle Instance { get; private set; }

        [Header("Timing")]
        [SerializeField] private float dayDuration = 180f;   // seconds — TBD (GDD open question #1)
        [SerializeField] private float nightDuration = 120f; // seconds — ends early if all enemies die

        [Header("Lighting")]
        [SerializeField] private Light directionalLight;
        [SerializeField] private Gradient dayNightLightColor;
        [SerializeField] private AnimationCurve dayNightIntensity;

        public DayPhase Phase { get; private set; } = DayPhase.Day;
        public int CurrentNight { get; private set; } = 0; // 0 = before Night 1
        public float PhaseTimeRemaining { get; private set; }
        public float PhaseDuration => Phase == DayPhase.Day ? dayDuration : nightDuration;
        public float PhaseProgress => 1f - (PhaseTimeRemaining / PhaseDuration); // 0→1

        public event Action OnDayStart;
        public event Action<int> OnNightStart;  // int = night number
        public event Action<int> OnNightEnd;    // int = night number

        private bool _running;
        private const int TotalNights = 7;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void StartCycle()
        {
            _running = true;
            CurrentNight = 0;
            BeginDay();
        }

        private void Update()
        {
            if (!_running) return;

            PhaseTimeRemaining -= Time.deltaTime;
            UpdateLighting();

            if (PhaseTimeRemaining <= 0f)
            {
                if (Phase == DayPhase.Day)
                    BeginNight();
                else
                    EndNight();
            }
        }

        private void BeginDay()
        {
            Phase = DayPhase.Day;
            PhaseTimeRemaining = dayDuration;
            OnDayStart?.Invoke();
            Debug.Log($"[DayNightCycle] Day {CurrentNight + 1} begins. ({dayDuration}s)");
        }

        private void BeginNight()
        {
            CurrentNight++;
            if (CurrentNight >= TotalNights)
            {
                BeginBossNight();
                return;
            }
            Phase = DayPhase.Night;
            PhaseTimeRemaining = nightDuration;
            OnNightStart?.Invoke(CurrentNight);
            Debug.Log($"[DayNightCycle] Night {CurrentNight} begins.");
        }

        private void EndNight()
        {
            OnNightEnd?.Invoke(CurrentNight);
            GameStateManager.Instance?.RegisterNightSurvived();
            Debug.Log($"[DayNightCycle] Night {CurrentNight} ends.");

            if (CurrentNight >= TotalNights)
            {
                _running = false;
                return;
            }

            BeginDay();
        }

        private void BeginBossNight()
        {
            // Night 7: no safety timer — fight continues until boss dies or all players die.
            // WaveManager will start the boss fight via OnNightStart. Timer is suspended.
            Phase = DayPhase.Night;
            PhaseTimeRemaining = float.MaxValue;
            OnNightStart?.Invoke(CurrentNight);
            Debug.Log("[DayNightCycle] Night 7 — Boss fight begins. Timer suspended.");
        }

        // Called by WaveManager when all enemies die before the timer.
        public void ForceEndNight()
        {
            if (Phase == DayPhase.Night)
                PhaseTimeRemaining = 0f;
        }

        // Debug / editor only — forces the current phase to end immediately regardless of type.
        public void ForceEndCurrentPhase() => PhaseTimeRemaining = 0f;

        private void UpdateLighting()
        {
            if (directionalLight == null) return;
            directionalLight.color = dayNightLightColor.Evaluate(PhaseProgress);
            directionalLight.intensity = dayNightIntensity.Evaluate(PhaseProgress);
        }
    }
}
