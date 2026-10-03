using Duskborn.Gameplay.Loot;
using Duskborn.Gameplay.Player;
using Duskborn.Gameplay.Projectiles;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.AI;

namespace Duskborn.Gameplay.Enemies
{
    public struct ThornwingView
    {
        public ThornwingPhase Phase;
        public int Sequence;
        public uint StartTick;
    }

    /// <summary>Low hovering skirmisher. Ground navigation keeps its damage collider in melee reach.</summary>
    public sealed class Thornwing : EnemyBase
    {
        public const float HoverHeight = 1.15f;
        [SerializeField] private ProjectileDefinition projectile;
        [SerializeField] private DropLootTable lootTable;
        [SerializeField] private Transform flightAnchor, mouth;
        private readonly ThornwingClock clock = new();
        private NavMeshPath dartPath;
        private readonly SyncVar<ThornwingView> view = new(new SyncTypeSettings(0f));
        private readonly SyncVar<float> scaledMaxHP = new(new SyncTypeSettings(0f));
        private PlayerStats targetStats;
        private Vector3 aim, destination;
        private float scanTime, dartCooldown, dartTime;
        private int sequence, side = 1;
        private bool rewarded;
        private float normalAcceleration;
        private bool normalAutoBraking;
        public ThornwingView View => view.Value;
        public double ActionAge => TimeManager != null ? TimeManager.TimePassed(View.StartTick) : 0;
        public override float MaxHP => scaledMaxHP.Value > 0 ? scaledMaxHP.Value : base.MaxHP;
        protected override bool UseGenericAudio => false;

        protected override void Awake()
        {
            base.Awake(); dartPath = new NavMeshPath(); clock.Changed += Publish; clock.Release += ReleaseShot;
            normalAcceleration = Agent.acceleration; normalAutoBraking = Agent.autoBraking;
        }
        protected override void OnDestroy()
        {
            clock.Changed -= Publish; clock.Release -= ReleaseShot; base.OnDestroy();
        }
        public override void OnStartServer()
        {
            base.OnStartServer(); scaledMaxHP.Value = base.MaxHP;
            Agent.updateRotation = false; clock.Reset();
        }
        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!IsServerStarted) Agent.enabled = false;
            GetComponent<Collider>().enabled = IsAlive;
            var ragdoll = GetComponent<EnemyRagdoll>();
            if (IsAlive) ragdoll?.DisableRagdoll();
            else ragdoll?.EnableRagdoll();
            GetComponent<ThornwingPresentation>()?.ResetPresentation();
        }
        public override void ResetEnemy(Vector3 position)
        {
            base.ResetEnemy(position);
            RestoreDartMovement();
            scaledMaxHP.Value = 0; rewarded = false; targetStats = null;
            scanTime = dartTime = 0; dartCooldown = .6f; side = 1;
            aim = position + transform.forward; destination = position; clock.Reset();
            if (Agent.enabled && Agent.isOnNavMesh) Agent.isStopped = false;
            GetComponent<ThornwingPresentation>()?.ResetPresentation();
        }
        protected override void AcquireTarget()
        {
            base.AcquireTarget(); targetStats = CurrentTarget != null ? CurrentTarget.GetComponent<PlayerStats>() : null;
        }
        protected override void Update()
        {
            if (!IsServerStarted || !IsSpawned || !IsAlive || !Agent.enabled || !Agent.isOnNavMesh) return;
            if (TickIdleWhenNoPlayers()) { clock.Interrupt(); dartTime = 0; RestoreDartMovement(); return; }
            if (IsStaggered)
            {
                clock.Interrupt(); dartTime = 0; RestoreDartMovement(); TickStagger(); return;
            }
            // Dodge cooldown is wall time, including the shot's windup and recovery.
            dartCooldown -= Time.deltaTime;
            var phase = clock.Phase;
            if (phase == ThornwingPhase.Windup && clock.Age < ThornwingClock.TrackingSeconds && TargetAlive())
                aim = CurrentTarget.position + Vector3.up;
            if (phase == ThornwingPhase.Windup) Face(aim);
            clock.Tick(Time.deltaTime);
            if (clock.Phase != ThornwingPhase.Hunt) return;
            scanTime -= Time.deltaTime;
            if (scanTime <= 0 || !TargetAlive()) { AcquireTarget(); scanTime = .3f; }
            if (!TargetAlive()) { Agent.ResetPath(); dartTime = 0; RestoreDartMovement(); return; }
            Vector3 delta = CurrentTarget.position - transform.position;
            float elevation = delta.y; delta.y = 0; float distance = delta.magnitude;
            Face(CurrentTarget.position);

            // A bounded dart can create space once, then it commits to a punishable shot.
            // No speed boost or perpetual retreat while telegraphing/recovering.
            if (dartTime > 0)
            {
                dartTime -= Time.deltaTime;
                if (dartTime <= 0 || !Agent.hasPath || Agent.isPathStale ||
                    Agent.pathStatus != NavMeshPathStatus.PathComplete || Agent.remainingDistance < .2f)
                { dartTime = 0; Agent.ResetPath(); RestoreDartMovement(); }
                else return;
            }
            if (dartCooldown <= 0 && distance <= ThornwingClock.Range && Mathf.Abs(elevation) <= ThornwingClock.MaxElevation)
            {
                side = -side;
                Vector3 away = distance > .01f ? -delta.normalized : -transform.forward;
                Vector3 lateral = Vector3.Cross(Vector3.up, away) * side;
                Vector3 offset = (distance < 3f ? away * .9f + lateral * .5f : lateral * 1.25f);
                // Try both sides and shorter routes; a blocked preference isn't a dodge.
                for (int attempt = 0; attempt < 4; attempt++)
                {
                    Vector3 choice = offset * (attempt >= 2 ? .6f : 1);
                    if ((attempt & 1) != 0) choice = distance < 3f ? away * .9f - lateral * .5f : -choice;
                    if (attempt == 3 && distance < 3f) choice *= .6f;
                    if (!TryDartPath(transform.position + choice)) continue;
                    Agent.isStopped = false; Agent.speed = MoveSpeed * 1.6f;
                    // Braking over a one-metre route consumed most of the old short dash.
                    Agent.autoBraking = false; Agent.acceleration = Mathf.Max(normalAcceleration, 60f);
                    if (Agent.SetPath(dartPath)) { dartCooldown = 2.4f; dartTime = .4f; return; }
                    RestoreDartMovement();
                }
                dartCooldown = .3f;
            }
            aim = CurrentTarget.position + Vector3.up;
            if (clock.TryBegin(distance, elevation, ClearShot(aim))) return;
            if (distance <= ThornwingClock.Range && Mathf.Abs(elevation) <= ThornwingClock.MaxElevation && ClearShot(aim))
            { Agent.ResetPath(); return; }
            Agent.isStopped = false; Agent.speed = MoveSpeed;
            if (!Agent.hasPath || (destination - CurrentTarget.position).sqrMagnitude > 1)
            { destination = CurrentTarget.position; Agent.SetDestination(destination); }
        }
        private bool TargetAlive() => CurrentTarget != null && targetStats != null && targetStats.IsAlive;
        private Vector3 Mouth => mouth.position;
        private Vector3 BodyCenter => flightAnchor.position;
        private int Solids => ~((1 << gameObject.layer) | playerLayer.value | (1 << 2));
        private bool ClearShot(Vector3 end) => !Physics.Linecast(BodyCenter,
            Mouth, Solids, QueryTriggerInteraction.Ignore) && !Physics.CheckSphere(Mouth, .08f, Solids, QueryTriggerInteraction.Ignore) &&
            !Physics.SphereCast(Mouth, .08f, (end - Mouth).normalized, out _, (end - Mouth).magnitude,
                Solids, QueryTriggerInteraction.Ignore);
        private bool TryDartPath(Vector3 candidate)
        {
            var filter = new NavMeshQueryFilter { agentTypeID = Agent.agentTypeID, areaMask = Agent.areaMask };
            return ValidateDartPath(Agent.nextPosition, candidate, filter, Solids, dartPath);
        }
        private static bool ValidateDartPath(Vector3 start, Vector3 candidate, NavMeshQueryFilter filter,
            int solids, NavMeshPath path)
        {
            // Project the lateral choice onto physical terrain before searching navigation.
            // Searching only at the old elevation rejects otherwise valid hillside dodges.
            if (!Physics.Raycast(candidate + Vector3.up * .8f, Vector3.down, out var support,
                    1.6f, solids, QueryTriggerInteraction.Ignore) ||
                !NavMesh.SamplePosition(support.point, out var hit, .45f, filter) ||
                Mathf.Abs(hit.position.y - start.y) > .65f ||
                (hit.position - start).sqrMagnitude > 2.56f ||
                !NavMesh.CalculatePath(start, hit.position, filter, path) ||
                path.status != NavMeshPathStatus.PathComplete) return false;
            var corners = path.corners;
            float length = 0;
            for (int i = 1; i < corners.Length; i++)
            {
                Vector3 from = corners[i - 1], to = corners[i], delta = to - from;
                float horizontal = new Vector2(delta.x, delta.z).magnitude;
                length += delta.magnitude;
                if (length > 1.6f || Mathf.Abs(delta.y) > horizontal * .9f + .02f) return false;
                int steps = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / .15f));
                for (int step = 0; step <= steps; step++)
                {
                    Vector3 point = Vector3.Lerp(from, to, step / (float)steps);
                    // The bake can sit above/below physical terrain or connect another level.
                    // Require nearby support and clear the actual living capsule at every sample.
                    if (!Physics.Raycast(point + Vector3.up * .45f, Vector3.down, out var ground,
                        .9f, solids, QueryTriggerInteraction.Ignore) ||
                        // Bakes normally sit above terrain. Do not loosen buried-navigation rejection.
                        ground.point.y - point.y > .25f || point.y - ground.point.y > .35f || ground.normal.y < .75f ||
                        Physics.CheckCapsule(point + Vector3.up * .895f, point + Vector3.up * 1.405f,
                            .42f, solids, QueryTriggerInteraction.Ignore)) return false;
                }
            }
            return corners.Length >= 2;
        }
        private void Face(Vector3 point)
        {
            Vector3 delta = point - transform.position; delta.y = 0;
            if (delta.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(delta);
        }
        private void ReleaseShot()
        {
            if (!IsServerStarted || !IsSpawned || !IsAlive || !TargetAlive() || projectile == null || IsStaggered) return;
            Vector3 delta = CurrentTarget.position - transform.position;
            float elevation = delta.y; delta.y = 0;
            if (delta.magnitude > ThornwingClock.Range || Mathf.Abs(elevation) > ThornwingClock.MaxElevation || !ClearShot(aim)) return;
            int shot = ProjectileFlight.NextId(); Vector3 origin = Mouth, direction = (aim - origin).normalized;
            ShowShotRpc(shot, origin, direction);
            float amount = Damage * projectile.damageMultiplier;
            ProjectileFlight.Launch(shot, projectile, transform, false, origin, direction, true,
                (col, point, forward) => ProjectileDamage.Apply(col, point, forward, amount, false, this, null),
                PlayerCombat.BroadcastProjectileImpact, clearanceOrigin: BodyCenter);
            ShotSoundRpc();
        }
        [ObserversRpc] private void ShowShotRpc(int shot, Vector3 origin, Vector3 direction)
        {
            if (!IsServerStarted) ProjectileFlight.Launch(shot, projectile, transform, false, origin, direction, false);
        }
        [ObserversRpc(RunLocally = true)] private void ShotSoundRpc() => GetComponent<ThornwingPresentation>()?.PlayShot();
        private void Publish(ThornwingPhase phase)
        {
            dartTime = 0;
            if (Agent != null && Agent.enabled && Agent.isOnNavMesh)
            { Agent.ResetPath(); RestoreDartMovement(); Agent.isStopped = phase != ThornwingPhase.Hunt; }
            view.Value = new ThornwingView { Phase = phase, Sequence = ++sequence, StartTick = TimeManager != null ? TimeManager.Tick : 0 };
        }
        private void RestoreDartMovement()
        {
            Agent.speed = MoveSpeed; Agent.acceleration = normalAcceleration;
            Agent.autoBraking = normalAutoBraking;
        }
        protected override void Die()
        {
            clock.Kill();
            if (!rewarded)
            { rewarded = true; if (lootTable != null) LootManager.Instance?.ServerDropLoot(lootTable, transform.position + Vector3.up * .4f, LastAttacker); }
            base.Die();
        }
    }
}
