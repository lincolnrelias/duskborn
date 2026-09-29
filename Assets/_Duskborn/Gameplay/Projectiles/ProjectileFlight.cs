using System;
using System.Collections.Generic;
using Duskborn.Gameplay.Enemies;
using Duskborn.Gameplay.Player;
using FishNet.Object;
using UnityEngine;

namespace Duskborn.Gameplay.Projectiles
{
    // Server runs swept-sphere rigidbody motion. Clients display the same ballistic
    // trajectory, then receive the authoritative impact; clients never deal damage.
    public sealed class ProjectileFlight : MonoBehaviour
    {
        private static readonly Dictionary<int, ProjectileFlight> Replicas = new();
        private static int nextId;
        private readonly RaycastHit[] hits = new RaycastHit[32];
        private readonly Collider[] overlaps = new Collider[32];
        private ProjectileDefinition definition;
        private Transform owner;
        private bool playerFaction, authoritative, stopped;
        private Rigidbody body;
        private Vector3 velocity;
        private float age;
        private int id;
        private Action<Collider, Vector3, Vector3> damage;
        private Action<int, Vector3, Vector3, NetworkObject, string> notifyImpact;
        public bool IsAuthoritative => authoritative;
        public ProjectileDefinition Definition => definition;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Replicas.Clear(); nextId = 0; }

        public static int NextId() => ++nextId;

        public static ProjectileFlight Launch(int shotId, ProjectileDefinition data, Transform caster,
            bool fromPlayer, Vector3 position, Vector3 direction, bool server,
            Action<Collider, Vector3, Vector3> dealDamage = null,
            Action<int, Vector3, Vector3, NetworkObject, string> onImpact = null)
        {
            if (data == null || data.visualPrefab == null) return null;
            var go = new GameObject("Projectile_" + shotId);
            go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction));
            Instantiate(data.visualPrefab, go.transform);
            var flight = go.AddComponent<ProjectileFlight>();
            flight.id = shotId;
            flight.definition = data;
            flight.owner = caster;
            flight.playerFaction = fromPlayer;
            flight.authoritative = server;
            flight.velocity = direction.normalized * data.speed;
            flight.damage = dealDamage;
            flight.notifyImpact = onImpact;
            flight.body = go.AddComponent<Rigidbody>();
            flight.body.isKinematic = true;
            flight.body.useGravity = false;
            flight.body.interpolation = RigidbodyInterpolation.Interpolate;
            if (!server) Replicas[shotId] = flight;
            return flight;
        }

        public static bool IsFinite(Vector3 v) =>
            !float.IsNaN(v.x) && !float.IsInfinity(v.x) &&
            !float.IsNaN(v.y) && !float.IsInfinity(v.y) &&
            !float.IsNaN(v.z) && !float.IsInfinity(v.z);

        public bool CanHit(Collider col)
        {
            if (col == null || col.isTrigger || col.transform.IsChildOf(transform)) return false;
            if (owner != null && col.transform.IsChildOf(owner)) return false;
            if (playerFaction)
            {
                if (col.GetComponentInParent<PlayerStats>() != null) return false;
                var enemy = col.GetComponentInParent<EnemyBase>();
                if (enemy != null && !enemy.IsAlive) return false;
            }
            if (!playerFaction && col.GetComponentInParent<EnemyBase>() != null) return false;
            return true;
        }

        private void FixedUpdate()
        {
            age += Time.fixedDeltaTime;
            if (age >= (stopped ? definition.embeddedLifetime : definition.lifetime))
            { Destroy(gameObject); return; }
            if (stopped) return;
            Vector3 start = body.position;
            Vector3 acceleration = Physics.gravity * definition.gravityScale;
            Vector3 step = velocity * Time.fixedDeltaTime + acceleration * (0.5f * Time.fixedDeltaTime * Time.fixedDeltaTime);
            velocity += acceleration * Time.fixedDeltaTime;
            if (authoritative)
            {
                // Casts do not report colliders containing the origin. This prevents
                // spawning through walls when the shooter's chest is obstructed.
                int count = Physics.OverlapSphereNonAlloc(start, definition.radius, overlaps,
                    definition.collisionMask, QueryTriggerInteraction.Ignore);
                var candidates = count == overlaps.Length
                    ? Physics.OverlapSphere(start, definition.radius, definition.collisionMask, QueryTriggerInteraction.Ignore)
                    : overlaps;
                int overlapCount = candidates == overlaps ? count : candidates.Length;
                for (int i = 0; i < overlapCount; i++)
                    if (CanHit(candidates[i])) { Impact(candidates[i], start); return; }

                float distance = step.magnitude;
                if (distance > 0f)
                {
                    count = Physics.SphereCastNonAlloc(start, definition.radius, step / distance,
                        hits, distance, definition.collisionMask, QueryTriggerInteraction.Ignore);
                    var results = count == hits.Length
                        ? Physics.SphereCastAll(start, definition.radius, step / distance, distance,
                            definition.collisionMask, QueryTriggerInteraction.Ignore) : hits;
                    int length = results == hits ? count : results.Length;
                    float nearest = float.PositiveInfinity;
                    int selected = -1;
                    for (int i = 0; i < length; i++)
                        if (results[i].distance < nearest && CanHit(results[i].collider))
                        { nearest = results[i].distance; selected = i; }
                    if (selected >= 0) { Impact(results[selected].collider, results[selected].point); return; }
                }
            }
            body.MovePosition(start + step);
            if (velocity.sqrMagnitude > 0.0001f) body.MoveRotation(Quaternion.LookRotation(velocity));
        }

        private void Impact(Collider col, Vector3 point)
        {
            if (stopped) return;
            stopped = true; // Set before callbacks: multi-collider targets receive one hit.
            Vector3 direction = velocity.normalized;
            transform.SetPositionAndRotation(point, Quaternion.LookRotation(direction));
            var target = col.GetComponentInParent<NetworkObject>();
            string path = target != null ? RelativePath(target.transform, col.transform) : "";
            notifyImpact?.Invoke(id, point, direction, target, path);
            var hitBody = col.attachedRigidbody;
            if (hitBody != null && !hitBody.isKinematic)
                hitBody.AddForceAtPosition(direction * definition.impactImpulse, point, ForceMode.Impulse);
            damage?.Invoke(col, point, direction);
            if (definition.impact != null) definition.impact.OnImpact(this, col != null ? col.transform : null, point);
            else Destroy(gameObject);
        }

        public void Embed(Transform parent, Vector3 point)
        {
            stopped = true;
            age = 0f;
            // Arrow model's tip is at its origin; a little penetration hides the tip.
            transform.position = point + transform.forward * 0.025f;
            if (body != null)
            {
                if (Application.isPlaying) Destroy(body); else DestroyImmediate(body);
                body = null;
            }
            transform.SetParent(parent, true);
        }

        public static void ReceiveImpact(int shotId, Vector3 point, Vector3 direction, NetworkObject target, string path)
        {
            if (!Replicas.TryGetValue(shotId, out var flight) || flight == null) return;
            Transform parent = target != null ? (string.IsNullOrEmpty(path) ? target.transform : target.transform.Find(path)) : null;
            flight.transform.rotation = Quaternion.LookRotation(direction);
            if (flight.definition.impact != null) flight.definition.impact.OnImpact(flight, parent, point);
            else Destroy(flight.gameObject);
        }

        private static string RelativePath(Transform root, Transform child)
        {
            string path = "";
            while (child != root && child != null)
            { path = string.IsNullOrEmpty(path) ? child.name : child.name + "/" + path; child = child.parent; }
            return path;
        }

        private void OnDisable()
        {
            // Pooled victims must not carry old embedded arrows into their next life.
            if (Application.isPlaying && stopped && gameObject.scene.isLoaded) Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (!authoritative && Replicas.TryGetValue(id, out var flight) && flight == this) Replicas.Remove(id);
        }
    }
}
