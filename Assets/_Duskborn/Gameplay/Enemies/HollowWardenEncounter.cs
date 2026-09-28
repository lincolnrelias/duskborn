using System;

namespace Duskborn.Gameplay.Enemies
{
    public enum WardenState
    {
        Dormant, Spawn, Ready, Rootbreaker, HarvestSweep, RootPlant,
        Rooted, Stagger, Exposed, Recover, PhaseBreak, Dead
    }

    public enum WardenSignal { StateChanged, FacingLocked, Impact, SpikeWave, ClearSpikes }

    /// <summary>
    /// Pure encounter clock driven by HollowWardenBoss. The server adapter must
    /// drive Tick, choose attacks from Ready, and resolve the emitted hit signals.
    /// Durations match the prototype animation manifest. No client damage authority.
    /// Signal handlers must not mutate this clock; process queued inputs after Tick.
    /// </summary>
    public sealed class HollowWardenEncounter
    {
        public WardenState State { get; private set; } = WardenState.Dormant;
        public double Elapsed { get; private set; }
        public bool PhaseTwo { get; private set; }
        public const double SpikeCastSeconds = 6;
        public const double SpikeInterval = .5;
        public const double SpikeDelay = .75;
        public const double SpikeVisibleSeconds = .65;
        public const float SpikeRadius = 2.4f;
        public const double BarrageSeconds = SpikeCastSeconds + SpikeDelay + SpikeVisibleSeconds;
        public float IncomingDamageScale => IsCoreExposed(State) ? 1.5f : 1f;
        public static bool IsCoreExposed(WardenState state) => state == WardenState.Rooted;
        public double AttackPlaybackRate => _attackRate;
        public event Action<WardenSignal> Signal;

        public static bool SpikeOverlaps(float x, float z, float bodyRadius, float bottom, float top)
        {
            float radius = SpikeRadius + bodyRadius;
            return top >= 0 && bottom <= 2.4f && x * x + z * z <= radius * radius;
        }

        private bool _phasePending;
        private bool _facingLocked;
        private bool _impactSent;
        private double _nextSpikeWave;
        private bool _spikeWaveThisTick;
        private int _attacksSinceRoots;
        private double _attackRate = 1;

        public void Start()
        {
            if (State != WardenState.Dormant)
                throw new InvalidOperationException("Reset before starting another encounter.");
            Enter(WardenState.Spawn);
        }

        public void Reset()
        {
            ClearSpikes();
            PhaseTwo = _phasePending = false;
            _attacksSinceRoots = 0;
            _attackRate = 1;
            Enter(WardenState.Dormant);
        }

        public void ObserveHealth(float normalizedHealth)
        {
            if (float.IsNaN(normalizedHealth) || float.IsInfinity(normalizedHealth))
                throw new ArgumentOutOfRangeException(nameof(normalizedHealth));
            if (State == WardenState.Dead || State == WardenState.Dormant) return;
            if (normalizedHealth <= 0)
            {
                ClearSpikes();
                _phasePending = false;
                Enter(WardenState.Dead);
                return;
            }
            if (!PhaseTwo && normalizedHealth <= .5f) _phasePending = true;
            if (State == WardenState.Ready) EnterReadyOrPhase();
        }

        public bool TryBeginAttack(bool sweep)
        {
            if (State != WardenState.Ready) return false;
            if (_phasePending) { EnterReadyOrPhase(); return false; }
            if (_attacksSinceRoots >= 3)
            {
                Enter(WardenState.RootPlant);
                return true;
            }
            _attackRate = PhaseTwo ? 1.15 : 1;
            Enter(sweep ? WardenState.HarvestSweep : WardenState.Rootbreaker);
            return true;
        }

        public void Tick(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0)
                throw new ArgumentOutOfRangeException(nameof(seconds));
            _spikeWaveThisTick = false;
            // No autonomous Ready->attack transition: navigation/targeting belongs to adapter.
            // Therefore even a very large delta reaches Ready/Dead after a bounded chain.
            while (seconds > 0 && IsTimed(State))
            {
                double duration = Duration();
                double step = Math.Min(seconds, duration - Elapsed);
                Elapsed += step;
                seconds -= step;
                if (State == WardenState.Rooted && Elapsed < SpikeCastSeconds && Elapsed >= _nextSpikeWave)
                {
                    // Never backfill missed waves after a hitch: each marker gets a fresh delay.
                    _nextSpikeWave = Elapsed + SpikeInterval;
                    if (!_spikeWaveThisTick) Signal?.Invoke(WardenSignal.SpikeWave);
                    _spikeWaveThisTick = true;
                }
                if (State == WardenState.Rootbreaker || State == WardenState.HarvestSweep)
                {
                    if (!_facingLocked && Elapsed >= .6 / _attackRate)
                    {
                        _facingLocked = true;
                        Signal?.Invoke(WardenSignal.FacingLocked);
                    }
                    double impact = (State == WardenState.Rootbreaker ? 1 : .9) / _attackRate;
                    if (!_impactSent && Elapsed >= impact)
                    {
                        _impactSent = true;
                        Signal?.Invoke(WardenSignal.Impact);
                    }
                }
                if (Elapsed < duration) break;
                CompleteState();
            }
        }

        private static bool IsTimed(WardenState state) => state != WardenState.Dormant
            && state != WardenState.Ready && state != WardenState.Dead;

        private double Duration()
        {
            switch (State)
            {
                case WardenState.Spawn: return 2;
                case WardenState.Rootbreaker: return 2.4 / _attackRate;
                case WardenState.HarvestSweep: return 2.2 / _attackRate;
                case WardenState.RootPlant: return 1.4;
                case WardenState.Rooted: return BarrageSeconds;
                case WardenState.Stagger: return 1;
                case WardenState.Exposed: return PhaseTwo ? 7 : 5;
                case WardenState.Recover: return 1;
                case WardenState.PhaseBreak: return 2;
                default: throw new InvalidOperationException("State has no clock.");
            }
        }

        private void CompleteState()
        {
            switch (State)
            {
                case WardenState.Rootbreaker:
                case WardenState.HarvestSweep:
                    _attacksSinceRoots++;
                    EnterReadyOrPhase();
                    break;
                case WardenState.RootPlant:
                    _attacksSinceRoots = 0;
                    _nextSpikeWave = SpikeInterval;
                    Enter(WardenState.Rooted);
                    Signal?.Invoke(WardenSignal.SpikeWave);
                    _spikeWaveThisTick = true;
                    break;
                case WardenState.Rooted:
                    ClearSpikes();
                    Enter(WardenState.Recover);
                    break;
                case WardenState.Stagger: Enter(WardenState.Exposed); break;
                case WardenState.Exposed: Enter(WardenState.Recover); break;
                default: EnterReadyOrPhase(); break;
            }
        }

        private void EnterReadyOrPhase()
        {
            if (_phasePending && !PhaseTwo)
            {
                PhaseTwo = true;
                _phasePending = false;
                Enter(WardenState.PhaseBreak);
            }
            else if (State != WardenState.Ready) Enter(WardenState.Ready);
        }

        private void ClearSpikes()
        {
            if (State == WardenState.Rooted) Signal?.Invoke(WardenSignal.ClearSpikes);
        }

        private void Enter(WardenState state)
        {
            State = state;
            Elapsed = 0;
            _facingLocked = _impactSent = false;
            Signal?.Invoke(WardenSignal.StateChanged);
        }
    }
}

