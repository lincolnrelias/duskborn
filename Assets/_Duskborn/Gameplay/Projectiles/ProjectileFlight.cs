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
        private float rollOffset;
        private TrailRenderer trail;
        private Vector3? launchClearanceOrigin;
        private Action<Collider, Vector3, Vector3> damage;
        private Action<int, Vector3, Vector3, NetworkObject, string, bool, string> notifyImpact;
        public bool IsAuthoritative => authoritative;
        public ProjectileDefinition Definition => definition;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Replicas.Clear(); nextId = 0; }

        public static int NextId() => ++nextId;

        public static ProjectileFlight Launch(int shotId, ProjectileDefinition data, Transform caster,
            bool fromPlayer, Vector3 position, Vector3 direction, bool server,
            Action<Collider, Vector3, Vector3> dealDamage = null,
            Action<int, Vector3, Vector3, NetworkObject, string, bool, string> onImpact = null,
            Quaternion? rotation = null, Vector3? clearanceOrigin = null)
        {
            if (data == null || data.visualPrefab == null) return null;
            var go = new GameObject("Projectile_" + shotId);
            Quaternion rot = rotation ?? (direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction) : Quaternion.identity);
            go.transform.SetPositionAndRotation(position, rot);
            var visual = Instantiate(data.visualPrefab, go.transform);
            data.ApplyVisualScale(visual.transform);
            var flight = go.AddComponent<ProjectileFlight>();
            flight.id = shotId;
            flight.definition = data;
            flight.owner = caster;
            flight.playerFaction = fromPlayer;
            flight.authoritative = server;
            flight.velocity = direction.normalized * data.speed;
            flight.damage = dealDamage;
            flight.notifyImpact = onImpact;
            flight.launchClearanceOrigin = clearanceOrigin;
            flight.trail = ArrowFeedback.AddTrail(go.transform, data);
            flight.body = go.AddComponent<Rigidbody>();
            flight.body.isKinematic = true;
            flight.body.useGravity = false;
            flight.body.interpolation = RigidbodyInterpolation.Interpolate;

            if (direction.sqrMagnitude > 0.0001f)
            {
                Quaternion lookRot = Quaternion.LookRotation(direction);
                Quaternion delta = Quaternion.Inverse(lookRot) * rot;
                flight.rollOffset = delta.eulerAngles.z;
            }

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
            Vector3 end = start + step;
            velocity += acceleration * Time.fixedDeltaTime;
            if (authoritative)
            {
                // The arrow's pivot is its tip. Sweep from the drawing hand on the
                // first tick so the shaft length cannot place that tip through a wall.
                if (launchClearanceOrigin.HasValue)
                {
                    start = launchClearanceOrigin.Value;
                    step = end - start;
                    launchClearanceOrigin = null;
                }
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
            body.MovePosition(end);
            if (velocity.sqrMagnitude > 0.0001f)
            {
                Quaternion look = Quaternion.LookRotation(velocity);
                if (Mathf.Abs(rollOffset) > 0.01f)
                    look *= Quaternion.Euler(0f, 0f, rollOffset);
                body.MoveRotation(look);
            }
        }

        private void Impact(Collider col, Vector3 point)
        {
            if (stopped) return;
            stopped = true; // Set before callbacks: multi-collider targets receive one hit.
            Vector3 direction = velocity.normalized;

            Transform attachTransform = col != null ? col.transform : null;
            Vector3 embedPoint = point;

            var ragdoll = col != null ? col.GetComponentInParent<EnemyRagdoll>() : null;
            if (ragdoll != null && ragdoll.TryGetLimbAttachment(point, direction, out var limb, out var limbPoint))
            {
                attachTransform = limb;
                embedPoint = limbPoint;
            }
            else
            {
                var animator = col != null ? col.GetComponentInParent<Animator>() : null;
                if (animator != null && TryGetHumanoidLimb(animator, point, out var humLimb))
                {
                    attachTransform = humLimb;
                }
            }

            transform.SetPositionAndRotation(embedPoint, Quaternion.LookRotation(direction));
            var target = col != null ? col.GetComponentInParent<NetworkObject>() : null;
            string path = target != null && attachTransform != null && attachTransform.IsChildOf(target.transform)
                ? RelativePath(target.transform, attachTransform)
                : "";
            bool flesh = col != null && (col.GetComponentInParent<EnemyBase>() != null || col.GetComponentInParent<PlayerStats>() != null);
            string surfaceTag = col != null ? col.tag : "Default";

            notifyImpact?.Invoke(id, embedPoint, direction, target, path, flesh, surfaceTag);
            PresentImpact(embedPoint, direction, flesh, surfaceTag);

            var hitBody = (attachTransform != null ? attachTransform.GetComponent<Rigidbody>() : null)
                ?? (col != null ? col.attachedRigidbody : null);
            if (hitBody != null && !hitBody.isKinematic)
                hitBody.AddForceAtPosition(direction * definition.impactImpulse, embedPoint, ForceMode.Impulse);

            damage?.Invoke(col, embedPoint, direction);

            if (definition.impact != null) definition.impact.OnImpact(this, attachTransform, embedPoint);
            else Destroy(gameObject);
        }

        public void Embed(Transform parent, Vector3 point)
        {
            if (trail != null) trail.emitting = false;
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

        public static void ReceiveImpact(int shotId, Vector3 point, Vector3 direction, NetworkObject target, string path,
            bool flesh = false, string surfaceTag = "Default")
        {
            if (!Replicas.TryGetValue(shotId, out var flight) || flight == null || flight.stopped) return;
            flight.stopped = true;
            Transform parent = target != null ? (string.IsNullOrEmpty(path) ? target.transform : target.transform.Find(path)) : null;
            if (parent == null && target != null) parent = target.transform;
            Quaternion impactRot = Quaternion.LookRotation(direction);
            if (Mathf.Abs(flight.rollOffset) > 0.01f)
                impactRot *= Quaternion.Euler(0f, 0f, flight.rollOffset);
            flight.transform.rotation = impactRot;
            flight.PresentImpact(point, direction, flesh, surfaceTag);
            if (flight.definition.impact != null) flight.definition.impact.OnImpact(flight, parent, point);
            else Destroy(flight.gameObject);
        }

        private void PresentImpact(Vector3 point, Vector3 direction, bool flesh, string surfaceTag)
        {
            if (trail != null) trail.emitting = false;
            var shooter = owner != null ? owner.GetComponent<PlayerCombat>() : null;
            ArrowFeedback.Impact(definition, point, direction, flesh, surfaceTag, shooter != null && shooter.IsOwner);
        }

        private static string RelativePath(Transform root, Transform child)
        {
            string path = "";
            while (child != root && child != null)
            { path = string.IsNullOrEmpty(path) ? child.name : child.name + "/" + path; child = child.parent; }
            return path;
        }

        private static readonly HumanBodyBones[] HumanoidBones =
        {
            HumanBodyBones.Head,
            HumanBodyBones.Chest,
            HumanBodyBones.Spine,
            HumanBodyBones.Hips,
            HumanBodyBones.LeftUpperArm,
            HumanBodyBones.RightUpperArm,
            HumanBodyBones.LeftLowerArm,
            HumanBodyBones.RightLowerArm,
            HumanBodyBones.LeftUpperLeg,
            HumanBodyBones.RightUpperLeg,
            HumanBodyBones.LeftLowerLeg,
            HumanBodyBones.RightLowerLeg
        };

        private static bool TryGetHumanoidLimb(Animator animator, Vector3 hitPoint, out Transform limb)
        {
            limb = null;
            if (animator == null || !animator.isHuman) return false;

            float bestDist = float.MaxValue;
            for (int i = 0; i < HumanoidBones.Length; i++)
            {
                var t = animator.GetBoneTransform(HumanoidBones[i]);
                if (t == null) continue;
                float d = (t.position - hitPoint).sqrMagnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    limb = t;
                }
            }
            return limb != null;
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
