using System.Collections.Generic;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.AI;
using Duskborn.Gameplay.Player;
using Duskborn.Gameplay.Loot;

namespace Duskborn.Gameplay.Enemies
{
    public struct BriarbackView
    {
        public BriarbackPhase Phase;
        public int Sequence;
        public uint StartTick;
        public Vector3 Origin;
        public Vector3 Forward;
    }

    /// <summary>Server-controlled close headbutt or straight charge with readable warnings and recovery.</summary>
    public sealed class Briarback : EnemyBase
    {
        [SerializeField] private DropLootTable lootTable;
        private readonly BriarbackCharge _clock = new();
        private readonly SyncVar<BriarbackView> _view = new(new SyncTypeSettings(0f));
        private readonly SyncVar<float> _scaledMaxHP = new(new SyncTypeSettings(0f));
        private readonly Collider[] _hits = new Collider[32];
        private readonly HashSet<PlayerStats> _hitPlayers = new();
        private float _scanTime;
        private Vector3 _origin, _forward, _destination;
        private int _sequence;
        private bool _rewarded;
        public BriarbackView View => _view.Value;
        public override float MaxHP => _scaledMaxHP.Value > 0 ? _scaledMaxHP.Value : base.MaxHP;
        public double ActionAge => TimeManager != null ? TimeManager.TimePassed(View.StartTick) : 0;
        protected override bool UseGenericAudio => false;

        protected override void Awake() { base.Awake(); _clock.Changed += Publish; }
        protected override void OnDestroy() { _clock.Changed -= Publish; base.OnDestroy(); }
        public override void OnStartServer()
        {
            base.OnStartServer();
            _scaledMaxHP.Value = base.MaxHP;
            Agent.updateRotation = false;
            _clock.Reset();
        }
        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!IsServerStarted) Agent.enabled = false;
            // Observers can receive an existing corpse or a reused pooled corpse.
            var ragdoll = GetComponent<EnemyRagdoll>();
            if (ragdoll != null)
            {
                if (!IsAlive && !ragdoll.IsRagdoll) ragdoll.EnableRagdoll();
                else if (IsAlive && ragdoll.IsRagdoll) ragdoll.DisableRagdoll();
            }
        }
        public override void ResetEnemy(Vector3 position)
        {
            base.ResetEnemy(position);
            _scaledMaxHP.Value = 0;
            _rewarded = false;
            _scanTime = 0;
            _destination = new Vector3(float.PositiveInfinity, 0, 0);
            _origin = position;
            _forward = transform.forward;
            _hitPlayers.Clear();
            _clock.Reset();
            if (Agent.enabled && Agent.isOnNavMesh) Agent.isStopped = false;
        }
        protected override void Update()
        {
            if (!IsServerStarted || !IsSpawned || !IsAlive) return;
            if (!Agent.enabled || !Agent.isOnNavMesh) return;
            if (TickIdleWhenNoPlayers()) { _clock.Reset(); return; }
            // Movement uses the phase at frame start. A long windup frame cannot instantly hit.
            var phase = _clock.Phase;
            if (phase == BriarbackPhase.Charge) TickCharge();
            else if (phase == BriarbackPhase.HeadbuttWindup) TrackHeadbutt();
            else if (phase == BriarbackPhase.Headbutt) TickHeadbutt();
            // An obstacle may have just entered recovery. Preserve that complete window.
            if (_clock.Phase == phase) _clock.Tick(Time.deltaTime);
            if (_clock.Phase != BriarbackPhase.Hunt) return;

            _scanTime -= Time.deltaTime;
            if (_scanTime <= 0 || CurrentTarget == null)
            { AcquireTarget(); _scanTime = .3f; }
            if (CurrentTarget == null) { Agent.ResetPath(); return; }
            Vector3 toTarget = CurrentTarget.position - transform.position;
            toTarget.y = 0;
            if (toTarget.sqrMagnitude > .001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(toTarget), 240f * Time.deltaTime);

            // The target must be reachable, at similar elevation, and inside the committed lane.
            if (toTarget.sqrMagnitude <= 49 && Mathf.Abs(CurrentTarget.position.y - transform.position.y) < 1.5f &&
                !NavMesh.Raycast(transform.position, CurrentTarget.position, out _, Agent.areaMask))
            {
                _origin = transform.position;
                _forward = toTarget.sqrMagnitude > .001f ? toTarget.normalized : transform.forward;
                if (ClearAttackLine(CurrentTarget.position) && _clock.TryBegin(toTarget.magnitude)) return;
            }
            Agent.isStopped = false;
            if (!Agent.hasPath || (_destination - CurrentTarget.position).sqrMagnitude > 1)
            { _destination = CurrentTarget.position; Agent.SetDestination(_destination); }
        }
        private bool ClearAttackLine(Vector3 target)
        {
            int solids = ~((1 << gameObject.layer) | playerLayer.value | (1 << 2));
            return !Physics.Linecast(transform.position + Vector3.up * .85f,
                target + Vector3.up * .85f, solids, QueryTriggerInteraction.Ignore);
        }
        private void TrackHeadbutt()
        {
            // Follow close circling briefly, then give a fixed final 0.2s dodge window.
            if (_clock.Age >= BriarbackCharge.HeadbuttLockSeconds || CurrentTarget == null) return;
            Vector3 toTarget = CurrentTarget.position - transform.position; toTarget.y = 0;
            if (toTarget.sqrMagnitude < .001f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation,
                Quaternion.LookRotation(toTarget), 540f * Time.deltaTime);
            _forward = transform.forward;
        }
        public static bool InHeadbuttArc(Vector3 offset, Vector3 forward)
        {
            if (Mathf.Abs(offset.y) > 1.5f) return false;
            offset.y = 0;
            return offset.sqrMagnitude <= BriarbackCharge.HeadbuttRange * BriarbackCharge.HeadbuttRange &&
                (offset.sqrMagnitude < .001f || Vector3.Dot(offset.normalized, forward) >= .5f);
        }
        private void TickHeadbutt()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position + Vector3.up * .65f,
                BriarbackCharge.HeadbuttRange, _hits, playerLayer, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var player = _hits[i].GetComponentInParent<PlayerStats>(); _hits[i] = null;
                if (player == null || !player.IsAlive || _hitPlayers.Contains(player)) continue;
                Vector3 offset = player.transform.position - transform.position;
                if (!InHeadbuttArc(offset, _forward) || !ClearAttackLine(player.transform.position)) continue;
                _hitPlayers.Add(player);
                player.TakeDamage(Damage * (2f / 3f), this, false);
            }
        }
        private void TickCharge()
        {
            Vector3 before = transform.position;
            float remaining = Mathf.Max(0, BriarbackCharge.ChargeLength - Vector3.Dot(before - _origin, _forward));
            float step = Mathf.Min(BriarbackCharge.ChargeSpeed * Time.deltaTime,
                Mathf.Min(remaining, BriarbackCharge.ChargeSpeed * Mathf.Max(0, BriarbackCharge.ChargeSeconds - _clock.Age)));
            Vector3 next = before + _forward * step;
            bool blocked = NavMesh.Raycast(before, next, out var edge, Agent.areaMask);
            if (blocked) next = edge.position;
            // Some trees/buildings have solid colliders without carving the navigation mesh.
            int solids = ~((1 << gameObject.layer) | playerLayer.value | (1 << 2));
            Vector3 travel = next - before;
            if (travel.sqrMagnitude > .000001f && Physics.SphereCast(before + Vector3.up * .85f,
                .5f, travel.normalized, out var obstacle, travel.magnitude, solids, QueryTriggerInteraction.Ignore))
            { next = before + travel.normalized * Mathf.Max(0, obstacle.distance - .02f); blocked = true; }
            Agent.Move(next - before);
            Vector3 after = Agent.nextPosition;
            // Swept capsule prevents missed hits through a player on a slow server frame.
            int count = Physics.OverlapCapsuleNonAlloc(before + Vector3.up * .65f,
                after + Vector3.up * .65f, BriarbackCharge.HitRadius, _hits, playerLayer, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var player = _hits[i].GetComponentInParent<PlayerStats>();
                _hits[i] = null;
                if (player != null && player.IsAlive && _hitPlayers.Add(player))
                    player.TakeDamage(Damage, this, false);
            }
            if (blocked || step <= .001f)
                _clock.Recover();
        }
        private void Publish(BriarbackPhase phase)
        {
            if (phase == BriarbackPhase.Windup || phase == BriarbackPhase.HeadbuttWindup)
            {
                _hitPlayers.Clear();
                transform.rotation = Quaternion.LookRotation(_forward);
            }
            // Replicate the final facing once the short tracking window has closed.
            if (phase == BriarbackPhase.Headbutt) _forward = transform.forward;
            if (Agent != null && Agent.enabled && Agent.isOnNavMesh)
            { Agent.ResetPath(); Agent.isStopped = phase != BriarbackPhase.Hunt; }
            _view.Value = new BriarbackView { Phase = phase, Sequence = ++_sequence,
                StartTick = TimeManager != null ? TimeManager.Tick : 0, Origin = _origin, Forward = _forward };
        }
        protected override void Die()
        {
            _clock.Kill();
            if (!_rewarded)
            {
                _rewarded = true;
                if (lootTable != null) LootManager.Instance?.ServerDropLoot(lootTable,
                    transform.position + Vector3.up * .5f, LastAttacker);
            }
            base.Die();
        }
    }
}
