using System.Collections;
using System.Collections.Generic;
using FishNet;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.AI;
using Duskborn.Gameplay.Player;

namespace Duskborn.Gameplay.Enemies
{
    public struct WardenActionView
    {
        public WardenState State;
        public int Sequence;
        public uint StartTick;
        public bool PhaseTwo;
        public bool FacingLocked;
        public Vector3 Origin;
        public Vector3 Forward;
        public float Rate;
    }

    public struct WardenSpikeView
    {
        public int Wave;
        public uint StartTick;
        public Vector3 Position;
        public Vector3 Normal;
        public bool Impacted;
    }

    public sealed class HollowWardenBoss : EnemyBase
    {
        [Header("Hollow Warden")]
        [SerializeField] private HollowWardenRoot rootPrefab;
        [SerializeField] private float rootbreakerLength = 8f;
        [SerializeField] private float rootbreakerWidth = 1.8f;
        [SerializeField] private float sweepRadius = 3.8f;
        [SerializeField] private int rewardGold = 75;

        private readonly HollowWardenEncounter _clock = new();
        private readonly Queue<(WardenSignal Signal, WardenState State)> _signals = new();
        private readonly SyncList<WardenSpikeView> _spikes = new(new SyncTypeSettings(0f));
        private readonly Collider[] _hits = new Collider[64];
        private readonly HashSet<PlayerStats> _hitPlayers = new();
        private readonly SyncVar<WardenActionView> _view = new();
        private readonly SyncVar<bool> _moving = new();
        private readonly SyncVar<float> _scaledMaxHP = new();
        private int _spikeWave;
        private float _targetTimer;
        private bool _nextSweep;
        private int _sequence;

        public WardenActionView ActionView => _view.Value;
        public IReadOnlyList<WardenSpikeView> Spikes => _spikes;
        public Material SpikeMaterial => rootPrefab != null ? rootPrefab.GetComponentInChildren<MeshRenderer>().sharedMaterial : null;
        public double SpikeAge(WardenSpikeView spike) => TimeManager != null ? TimeManager.TimePassed(spike.StartTick) : 0;
        public bool Moving => _moving.Value;
        public float DisplayMaxHP => _scaledMaxHP.Value > 0 ? _scaledMaxHP.Value : MaxHP;
        public float RootbreakerLength => rootbreakerLength;
        public float RootbreakerWidth => rootbreakerWidth;
        public float SweepRadius => sweepRadius;
        public int RewardGold => rewardGold;
        public const float DeathSeconds = 3f;
        public double ActionAge => TimeManager != null ? TimeManager.TimePassed(_view.Value.StartTick) : 0;
        public Transform Target => CurrentTarget;

        protected override void Awake()
        {
            base.Awake();
            if (GetComponent<HollowWardenFeedback>() == null) gameObject.AddComponent<HollowWardenFeedback>();
            _clock.Signal += CaptureSignal;
        }

        protected override bool UseGenericAudio => false;

        private void CaptureSignal(WardenSignal signal) => _signals.Enqueue((signal, _clock.State));

        public override void ResetEnemy(Vector3 position)
        {
            base.ResetEnemy(position);
            _clock.Reset();
            _signals.Clear();
            ClearSpikes();
            _spikeWave = 0;
            _nextSweep = false;
            _sequence = 0;
            _targetTimer = 0;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _scaledMaxHP.Value = MaxHP;
            Agent.updateRotation = false;
            _clock.Start();
            DrainSignals();
        }

        public override void OnStopServer()
        {
            ClearSpikes();
            base.OnStopServer();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!IsServerStarted) Agent.enabled = false;
        }

        protected override void OnDestroy()
        {
            _clock.Signal -= CaptureSignal;
            base.OnDestroy();
        }

        protected override void Update()
        {
            // Do not run EnemyBase.Update: its normal AI and melee would compete with the boss clock.
            if (!IsServerStarted || !IsSpawned) return;
            if (IsAlive && TickIdleWhenNoPlayers())
            {
                _moving.Value = Agent != null && Agent.isActiveAndEnabled && Agent.isOnNavMesh && Agent.velocity.sqrMagnitude > .04f;
                return;
            }
            if (IsAlive)
            {
                _clock.ObserveHealth(CurrentHP / Mathf.Max(1, MaxHP));
                _clock.Tick(Time.deltaTime);
            }
            DrainSignals();
            UpdateSpikes();
            if (!IsAlive) { _moving.Value = false; return; }

            _targetTimer -= Time.deltaTime;
            if (_targetTimer <= 0 || CurrentTarget == null ||
                !CurrentTarget.TryGetComponent<PlayerStats>(out var targetStats) || !targetStats.IsAlive)
            {
                AcquireTarget();
                _targetTimer = .25f;
            }
            bool ready = _clock.State == WardenState.Ready;
            bool aiming = (_clock.State == WardenState.Rootbreaker || _clock.State == WardenState.HarvestSweep)
                && !_view.Value.FacingLocked;
            if ((ready || aiming) && CurrentTarget != null)
            {
                Vector3 direction = CurrentTarget.position - transform.position;
                direction.y = 0;
                if (direction.sqrMagnitude > .01f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation,
                        Quaternion.LookRotation(direction), 180f * Time.deltaTime);
            }
            if (Agent.enabled && Agent.isOnNavMesh)
            {
                Agent.isStopped = !ready || CurrentTarget == null;
                if (ready && CurrentTarget != null)
                {
                    float range = _nextSweep ? sweepRadius - .3f : rootbreakerLength - 1f;
                    Vector3 toTarget = CurrentTarget.position - transform.position;
                    if (toTarget.magnitude <= range && Vector3.Angle(transform.forward, toTarget) < 20f)
                    {
                        Agent.ResetPath();
                        Agent.isStopped = true;
                        if (_clock.TryBeginAttack(_nextSweep)) _nextSweep = !_nextSweep;
                        DrainSignals();
                    }
                    else if (_targetTimer >= .24f) Agent.SetDestination(CurrentTarget.position);
                }
                _moving.Value = ready && Agent.velocity.sqrMagnitude > .04f;
            }
        }

        public override void TakeDamage(float amount, bool isCrit = false, Duskborn.Gameplay.Player.PlayerStats attacker = null)
        {
            if (!IsServerStarted || !IsAlive || amount <= 0 || float.IsNaN(amount) || float.IsInfinity(amount)) return;
            base.TakeDamage(amount * _clock.IncomingDamageScale, isCrit, attacker);
        }

        protected override void Die()
        {
            _clock.ObserveHealth(0);
            ClearSpikes();
            DrainSignals();
            var body = GetComponent<Collider>();
            if (body != null) body.enabled = false;
            base.Die();
        }

        protected override IEnumerator DespawnAfterDelay()
        {
            yield return new WaitForSeconds(DeathSeconds + .25f);
            if (IsSpawned) InstanceFinder.ServerManager.Despawn(NetworkObject);
        }

        private void DrainSignals()
        {
            while (_signals.Count > 0)
            {
                var item = _signals.Dequeue();
                // Thorns may kill the boss while resolving an impact. Never process a later live action.
                if (!IsAlive && item.Signal != WardenSignal.ClearSpikes && item.State != WardenState.Dead) continue;
                if (item.Signal == WardenSignal.StateChanged)
                {
                    _view.Value = new WardenActionView {
                        State = item.State, Sequence = ++_sequence, StartTick = TimeManager.Tick,
                        PhaseTwo = _clock.PhaseTwo, Origin = transform.position,
                        Forward = transform.forward, Rate = item.State == WardenState.Rootbreaker || item.State == WardenState.HarvestSweep
                            ? (float)_clock.AttackPlaybackRate : 1f
                    };
                    if (Agent.enabled && Agent.isOnNavMesh && item.State != WardenState.Ready)
                    { Agent.isStopped = true; Agent.ResetPath(); }
                }
                else if (item.Signal == WardenSignal.FacingLocked)
                {
                    var view = _view.Value;
                    view.FacingLocked = true;
                    view.Origin = transform.position;
                    view.Forward = transform.forward;
                    _view.Value = view;
                }
                else if (item.Signal == WardenSignal.Impact) ResolveImpact(item.State);
                else if (item.Signal == WardenSignal.SpikeWave && _clock.State == WardenState.Rooted) QueueSpikeWave();
                else if (item.Signal == WardenSignal.ClearSpikes) ClearSpikes();
            }
        }

        private void ResolveImpact(WardenState state)
        {
            var view = _view.Value;
            _hitPlayers.Clear();
            int count = Physics.OverlapSphereNonAlloc(view.Origin, rootbreakerLength + 2f, _hits,
                playerLayer, QueryTriggerInteraction.Ignore);
            // The registry is the bounded fallback if a crowded player setup fills the collider buffer.
            if (count == _hits.Length)
            {
                foreach (var player in PlayerRegistry.All)
                    if (player != null) HitPlayer(player, player.GetComponent<Collider>(), state, view);
            }
            else for (int i = 0; i < count && IsAlive; i++)
            {
                var collider = _hits[i];
                var player = collider.GetComponentInParent<PlayerStats>();
                HitPlayer(player, collider, state, view);
                _hits[i] = null;
            }
            System.Array.Clear(_hits, 0, count);
        }

        private void HitPlayer(PlayerStats player, Collider collider, WardenState state, WardenActionView view)
        {
            if (!IsAlive || player == null || !player.IsAlive || _hitPlayers.Contains(player)) return;
            Vector3 position = collider != null ? collider.bounds.center : player.transform.position + Vector3.up;
            float radius = collider != null ? Mathf.Max(collider.bounds.extents.x, collider.bounds.extents.z) : .4f;
            float vertical = collider != null ? collider.bounds.extents.y : 1f;
            if (Mathf.Abs(position.y - (view.Origin.y + 1f)) > vertical + 4.5f) return;
            Vector3 offset = position - view.Origin;
            offset.y = 0;
            float forward = Vector3.Dot(offset, view.Forward);
            float side = Vector3.Dot(offset, Vector3.Cross(Vector3.up, view.Forward));
            bool hit = state == WardenState.Rootbreaker
                ? forward >= -radius && forward <= rootbreakerLength + radius && Mathf.Abs(side) <= rootbreakerWidth * .5f + radius
                : forward >= -radius && offset.sqrMagnitude <= (sweepRadius + radius) * (sweepRadius + radius);
            if (!hit) return;
            _hitPlayers.Add(player);
            player.TakeDamage(Damage * (state == WardenState.Rootbreaker ? 1.2f : 1), this);
        }

        private void QueueSpikeWave()
        {
            int wave = ++_spikeWave;
            uint tick = TimeManager.Tick;
            int groundMask = ~((1 << LayerMask.NameToLayer("Enemy")) | (1 << LayerMask.NameToLayer("Player")) | (1 << 2));
            foreach (var player in PlayerRegistry.All)
            {
                if (player == null || !player.IsAlive) continue;
                // Lock the actual ground beneath this player once. The warning never chases them.
                if (!Physics.Raycast(player.transform.position + Vector3.up * 2f, Vector3.down,
                    out var ground, 12f, groundMask, QueryTriggerInteraction.Ignore)) continue;
                _spikes.Add(new WardenSpikeView {
                    Wave = wave, StartTick = tick, Position = ground.point, Normal = ground.normal
                });
            }
        }

        private void UpdateSpikes()
        {
            if (!IsAlive || _clock.State != WardenState.Rooted) { ClearSpikes(); return; }
            int hitWave = -1;
            for (int i = 0; i < _spikes.Count && IsAlive; i++)
            {
                var spike = _spikes[i];
                double age = SpikeAge(spike);
                if (!spike.Impacted && age >= HollowWardenEncounter.SpikeDelay
                    && age < HollowWardenEncounter.SpikeDelay + HollowWardenEncounter.SpikeVisibleSeconds)
                {
                    // All markers in a wave share a tick. Overlapping teammates' markers hit once.
                    if (hitWave != spike.Wave) { _hitPlayers.Clear(); hitWave = spike.Wave; }
                    spike.Impacted = true;
                    _spikes[i] = spike;
                    ResolveSpike(spike.Position);
                }
            }
            // Damage (e.g. thorns) can kill the boss and clear this list above.
            for (int i = _spikes.Count - 1; i >= 0; i--)
                if (SpikeAge(_spikes[i]) >= HollowWardenEncounter.SpikeDelay + HollowWardenEncounter.SpikeVisibleSeconds)
                    _spikes.RemoveAt(i);
        }

        private void ResolveSpike(Vector3 origin)
        {
            // Use the player registry to avoid trigger/sensor colliders and collider-buffer truncation.
            for (int i = 0; i < PlayerRegistry.All.Count && IsAlive; i++)
            {
                var player = PlayerRegistry.All[i];
                if (player == null || !player.IsAlive || _hitPlayers.Contains(player)) continue;
                var body = player.GetComponent<CharacterController>();
                Collider collider = body != null ? body : player.GetComponent<Collider>();
                Bounds bounds = collider != null ? collider.bounds
                    : new Bounds(player.transform.position + Vector3.up, new Vector3(.8f, 2, .8f));
                Vector3 offset = bounds.center - origin;
                if (!HollowWardenEncounter.SpikeOverlaps(offset.x, offset.z,
                    Mathf.Max(bounds.extents.x, bounds.extents.z), bounds.min.y - origin.y, bounds.max.y - origin.y)) continue;
                _hitPlayers.Add(player);
                player.TakeDamage(Damage, this);
            }
        }

        private void ClearSpikes()
        {
            if (_spikes.Count > 0) _spikes.Clear();
        }
    }
}
