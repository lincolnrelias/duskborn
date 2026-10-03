using FishNet.Object.Synchronizing;
using FishNet.Object;
using UnityEngine;
using Duskborn.Gameplay.Player;

namespace Duskborn.Gameplay.Enemies
{
    public struct BramblekinView
    {
        public BramblekinPhase Phase;
        public int Sequence;
        public uint StartTick;
    }

    /// <summary>Basic woodland melee enemy; uses the existing Swarmer spawn slot.</summary>
    public sealed class Bramblekin : EnemyBase
    {
        private readonly BramblekinClock clock = new();
        private readonly SyncVar<BramblekinView> view = new(new SyncTypeSettings(0f));
        private readonly SyncVar<float> scaledMaxHP = new(new SyncTypeSettings(0f));
        private readonly Collider[] hits = new Collider[32];
        private float scan;
        private int sequence;
        private bool struck;
        private Vector3 destination;
        public BramblekinView View => view.Value;
        public double ActionAge => TimeManager != null ? TimeManager.TimePassed(View.StartTick) : 0;
        public override float MaxHP => scaledMaxHP.Value > 0 ? scaledMaxHP.Value : base.MaxHP;
        protected override bool UseGenericAudio => false;

        protected override void Awake() { base.Awake(); clock.Changed += Publish; }
        protected override void OnDestroy() { clock.Changed -= Publish; base.OnDestroy(); }
        public override void OnStartServer()
        {
            base.OnStartServer(); scaledMaxHP.Value = base.MaxHP;
            Agent.updateRotation = false; clock.Reset();
        }
        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!IsServerStarted) Agent.enabled = false;
            GetComponent<BramblekinPresentation>()?.ResetPresentation();
        }
        public override void ResetEnemy(Vector3 position)
        {
            base.ResetEnemy(position);
            scaledMaxHP.Value = 0; scan = 0;
            destination = new Vector3(float.PositiveInfinity, 0, 0);
            clock.Reset(); GetComponent<BramblekinPresentation>()?.ResetPresentation();
        }
        protected override void Update()
        {
            if (!IsServerStarted || !IsSpawned || !IsAlive || !Agent.enabled || !Agent.isOnNavMesh) return;
            if (TickIdleWhenNoPlayers())
            {
                if (clock.Phase != BramblekinPhase.Hunt) clock.Reset();
                return;
            }
            if (IsStaggered)
            {
                clock.Interrupt(); Agent.isStopped = false; TickStagger(); return;
            }
            // Strike halfway through the visible sweep, once. Slow frames still resolve
            // that existing swing before transitioning into a full recovery window.
            if (clock.Phase == BramblekinPhase.Swing && !struck && clock.Age + Time.deltaTime >= BramblekinClock.StrikeSeconds)
            { struck = true; HitOnce(); }
            // Only one transition per frame.
            clock.Tick(Time.deltaTime);
            if (clock.Phase != BramblekinPhase.Hunt) return;
            Agent.isStopped = false;
            scan -= Time.deltaTime;
            if (scan <= 0 || CurrentTarget == null) { AcquireTarget(); scan = .3f; }
            if (CurrentTarget == null) { Agent.ResetPath(); return; }
            Vector3 offset = CurrentTarget.position - transform.position;
            Vector3 horizontal = offset; horizontal.y = 0;
            if (horizontal.sqrMagnitude > .001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(horizontal), 360 * Time.deltaTime);
            // Chase into close range; the longer damage reach provides room for a real retreat.
            if (InSwing(offset, transform.forward) && ClearLine(CurrentTarget.position) && clock.TryBegin(horizontal.magnitude)) return;
            if (!Agent.hasPath || (destination - CurrentTarget.position).sqrMagnitude > .25f)
            { destination = CurrentTarget.position; Agent.SetDestination(destination); }
        }
        public static bool InSwing(Vector3 offset, Vector3 forward)
        {
            if (Mathf.Abs(offset.y) > 1.1f) return false;
            offset.y = 0;
            return offset.sqrMagnitude <= BramblekinClock.Range * BramblekinClock.Range &&
                (offset.sqrMagnitude < .001f || Vector3.Dot(offset.normalized, forward) >= .7071068f);
        }
        private bool ClearLine(Vector3 target)
        {
            int solids = ~((1 << gameObject.layer) | playerLayer.value | (1 << 2));
            return !Physics.Linecast(transform.position + Vector3.up * .65f,
                target + Vector3.up * .65f, solids, QueryTriggerInteraction.Ignore);
        }
        private void HitOnce()
        {
            // The cudgel sweeps a short 90-degree arc, striking only the nearest living player.
            int count = Physics.OverlapSphereNonAlloc(transform.position + Vector3.up * .55f,
                BramblekinClock.Range, hits, playerLayer, QueryTriggerInteraction.Ignore);
            PlayerStats victim = null; float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var player = hits[i].GetComponentInParent<PlayerStats>(); hits[i] = null;
                if (player == null || !player.IsAlive) continue;
                Vector3 offset = player.transform.position - transform.position;
                if (!InSwing(offset, transform.forward) || !ClearLine(player.transform.position) || offset.sqrMagnitude >= nearest) continue;
                victim = player; nearest = offset.sqrMagnitude;
            }
            if (victim != null)
            { victim.TakeDamage(Damage, this, false); RpcClubHit(); }
        }
        [ObserversRpc]
        private void RpcClubHit() => GetComponent<BramblekinPresentation>()?.PlayClubHit();
        private void Publish(BramblekinPhase phase)
        {
            if (Agent != null && Agent.enabled && Agent.isOnNavMesh)
            { Agent.ResetPath(); Agent.isStopped = phase != BramblekinPhase.Hunt; }
            view.Value = new BramblekinView { Phase = phase, Sequence = ++sequence,
                StartTick = TimeManager != null ? TimeManager.Tick : 0 };
            if (phase == BramblekinPhase.Windup || phase == BramblekinPhase.Hunt) struck = false;
        }
        protected override void Die() { clock.Kill(); base.Die(); }
    }
}
