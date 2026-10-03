using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Duskborn.Gameplay.Enemies
{
    public class EnemyRagdoll : MonoBehaviour
    {
        [SerializeField] private Animator _animator;

        [SerializeField] private float _impulseScale = 5f;
        [SerializeField, Range(0f, 1f)] private float _wholeBodyImpulseFraction;
        [SerializeField] private bool _applyImpulseAtHitPoint;
        [SerializeField] private float _settleDuration = 2.5f;
        [SerializeField] private bool _disableBoneCollidersWhileAlive;
        [SerializeField] private bool _restorePoseOnReset;
        [SerializeField] private bool _ignoreSelfCollisions;
        [SerializeField] private bool _disableInterpolationWhileAlive;
        [SerializeField] private Transform[] _detachOnDeath = System.Array.Empty<Transform>();

        private Rigidbody[] _bones;
        private Collider[]  _boneColliders;
        private Collider[]  _rootColliders;
        private Coroutine   _freezeCoroutine;
        private Vector3[] _restPositions;
        private Quaternion[] _restRotations;
        private Transform[] _restParents;
        private bool _deathImpulseApplied;
        public bool IsRagdoll { get; private set; }

        private static readonly Dictionary<float, WaitForSeconds> WaitCache = new();

        private static WaitForSeconds GetWait(float seconds)
        {
            if (!WaitCache.TryGetValue(seconds, out var wait))
            {
                wait = new WaitForSeconds(seconds);
                WaitCache[seconds] = wait;
            }
            return wait;
        }

        private void Awake()
        {
            EnsureInitialized();
            SetKinematic(true);
            if (_disableBoneCollidersWhileAlive)
                foreach (var col in _boneColliders) if (col != null) col.enabled = false;
        }

        public void EnsureInitialized()
        {
            if (_bones != null && _boneColliders != null) return;

            if (_animator == null)
                _animator = GetComponentInChildren<Animator>();

            var all = GetComponentsInChildren<Rigidbody>();
            var list = new List<Rigidbody>(all.Length);
            foreach (var rb in all)
                if (rb.gameObject != gameObject) list.Add(rb);
            _bones = list.ToArray();

            var allCols = GetComponentsInChildren<Collider>();
            var boneCols = new List<Collider>(allCols.Length);
            var rootCols = new List<Collider>();
            foreach (var c in allCols)
            {
                if (c.gameObject == gameObject) rootCols.Add(c);
                else if (!c.isTrigger) boneCols.Add(c);
            }
            _rootColliders = rootCols.ToArray();
            _boneColliders = boneCols.ToArray();
            _restPositions = new Vector3[_bones.Length];
            _restRotations = new Quaternion[_bones.Length];
            _restParents = new Transform[_bones.Length];
            for (int i = 0; i < _bones.Length; i++)
            { _restPositions[i] = _bones[i].transform.localPosition; _restRotations[i] = _bones[i].transform.localRotation;
                _restParents[i] = _bones[i].transform.parent; }
        }

        private void OnDisable()
        {
            if (_freezeCoroutine != null)
            {
                StopCoroutine(_freezeCoroutine);
                _freezeCoroutine = null;
            }
        }

        public void EnableRagdoll()
        {
            EnsureInitialized();
            if (IsRagdoll) return;
            IsRagdoll = true;
            if (_freezeCoroutine != null)
            {
                StopCoroutine(_freezeCoroutine);
                _freezeCoroutine = null;
            }

            if (_animator != null) _animator.enabled = false;
            // Loose parts stay owned by the corpse for pooling/despawn cleanup, but no
            // longer inherit the living visual or jointed body's transforms.
            foreach (var part in _detachOnDeath)
                if (part != null) part.SetParent(transform, true);
            SetKinematic(false);
            foreach (var part in _detachOnDeath)
            {
                if (part == null) continue;
                var body = part.GetComponent<Rigidbody>();
                if (body == null) continue;
                Vector3 outward = part.position - transform.position; outward.y = 0;
                if (outward.sqrMagnitude < .0001f) outward = transform.right;
                body.AddForce(outward.normalized * .65f + Vector3.up * .25f, ForceMode.VelocityChange);
                body.AddTorque(Vector3.Cross(outward.normalized, Vector3.up) * 2, ForceMode.VelocityChange);
            }
            foreach (var col in _rootColliders)
                if (col != null) col.enabled = false;
            foreach (var col in _boneColliders)
                if (col != null) col.enabled = true;
            if (_ignoreSelfCollisions)
                for (int i = 0; i < _boneColliders.Length; i++)
                    for (int j = i + 1; j < _boneColliders.Length; j++)
                        if (_boneColliders[i] != null && _boneColliders[j] != null)
                            Physics.IgnoreCollision(_boneColliders[i], _boneColliders[j]);

            if (Application.isPlaying) _freezeCoroutine = StartCoroutine(FreezeAfterDelay());
        }

        public void DisableRagdoll()
        {
            EnsureInitialized();
            _deathImpulseApplied = false;
            IsRagdoll = false;
            if (_freezeCoroutine != null)
            {
                StopCoroutine(_freezeCoroutine);
                _freezeCoroutine = null;
            }

            foreach (var rb in _bones)
                if (rb != null && !rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
            SetKinematic(true);
            if (_restorePoseOnReset)
                for (int i = 0; i < _bones.Length; i++)
                    if (_bones[i] != null)
                    { _bones[i].transform.SetParent(_restParents[i], false);
                        _bones[i].transform.localPosition = _restPositions[i]; _bones[i].transform.localRotation = _restRotations[i]; }
            if (_animator != null) _animator.enabled = true;
            foreach (var col in _rootColliders)
                if (col != null) col.enabled = true;
            foreach (var col in _boneColliders)
                if (col != null) col.enabled = !_disableBoneCollidersWhileAlive;
        }

        private IEnumerator FreezeAfterDelay()
        {
            yield return GetWait(_settleDuration);
            SetKinematic(true);
            foreach (var col in _boneColliders)
                if (col != null) col.enabled = false;
            _freezeCoroutine = null;
        }

        public void ApplyImpulse(Vector3 hitPoint, Vector3 direction)
        {
            EnsureInitialized();
            if (_deathImpulseApplied) return;
            // The death RPC can arrive before the HP callback on a remote observer.
            if (!IsRagdoll) EnableRagdoll();
            Rigidbody nearest  = null;
            float     bestDist = float.MaxValue;
            foreach (var rb in _bones)
            {
                if (rb == null) continue;
                var collider = rb.GetComponent<Collider>();
                Vector3 contact = collider != null && collider.enabled ? collider.ClosestPoint(hitPoint) : rb.worldCenterOfMass;
                float d = (contact - hitPoint).sqrMagnitude;
                if (d < bestDist) { bestDist = d; nearest = rb; }
            }
            if (nearest == null || nearest.isKinematic || direction.sqrMagnitude < .000001f) return;
            _deathImpulseApplied = true;
            Vector3 impulse = direction * _impulseScale;
            float totalMass = 0;
            foreach (var body in _bones) if (body != null && !body.isKinematic) totalMass += body.mass;
            if (_wholeBodyImpulseFraction > 0 && totalMass > 0)
                foreach (var body in _bones)
                    if (body != null && !body.isKinematic)
                        body.AddForce(impulse * (_wholeBodyImpulseFraction * body.mass / totalMass), ForceMode.Impulse);
            Vector3 localImpulse = impulse * (1 - _wholeBodyImpulseFraction);
            if (_applyImpulseAtHitPoint) nearest.AddForceAtPosition(localImpulse, hitPoint, ForceMode.Impulse);
            else nearest.AddForce(localImpulse, ForceMode.Impulse);
        }

        public bool TryGetLimbAttachment(Vector3 hitPoint, Vector3 direction, out Transform limbTransform, out Vector3 embedPoint)
        {
            EnsureInitialized();

            limbTransform = null;
            embedPoint = hitPoint;

            Collider hitLimbCollider = null;
            float bestDistance = float.MaxValue;
            Vector3 bestPoint = hitPoint;

            // 1. Raycast along flight trajectory into bone colliders
            if (_boneColliders != null && _boneColliders.Length > 0)
            {
                Vector3 rayDir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.zero;
                if (rayDir != Vector3.zero)
                {
                    Ray ray = new Ray(hitPoint - rayDir * 0.1f, rayDir);
                    const float maxRayDist = 2.5f;
                    for (int i = 0; i < _boneColliders.Length; i++)
                    {
                        var col = _boneColliders[i];
                        if (col == null || !col.enabled) continue;
                        if (col.Raycast(ray, out RaycastHit hit, maxRayDist))
                        {
                            if (hit.distance < bestDistance)
                            {
                                bestDistance = hit.distance;
                                hitLimbCollider = col;
                                bestPoint = hit.point;
                            }
                        }
                    }
                }

                // 2. Fallback: closest point on active bone colliders
                if (hitLimbCollider == null)
                {
                    float bestSqrDist = float.MaxValue;
                    for (int i = 0; i < _boneColliders.Length; i++)
                    {
                        var col = _boneColliders[i];
                        if (col == null || !col.enabled) continue;
                        Vector3 pt = col.ClosestPoint(hitPoint);
                        float d = (pt - hitPoint).sqrMagnitude;
                        if (d < bestSqrDist)
                        {
                            bestSqrDist = d;
                            hitLimbCollider = col;
                            bestPoint = pt;
                        }
                    }
                }
            }

            if (hitLimbCollider != null)
            {
                limbTransform = hitLimbCollider.transform;
                embedPoint = bestPoint;
                return true;
            }

            // 3. Fallback: nearest bone rigidbody transform (e.g. if colliders disabled)
            if (_bones != null && _bones.Length > 0)
            {
                float bestDist = float.MaxValue;
                Transform bestBone = null;
                for (int i = 0; i < _bones.Length; i++)
                {
                    var rb = _bones[i];
                    if (rb == null) continue;
                    float d = (rb.position - hitPoint).sqrMagnitude;
                    if (d < bestDist)
                    {
                        bestDist = d;
                        bestBone = rb.transform;
                    }
                }
                if (bestBone != null)
                {
                    limbTransform = bestBone;
                    embedPoint = hitPoint;
                    return true;
                }
            }

            return false;
        }

        private void SetKinematic(bool value)
        {
            foreach (var rb in _bones)
                if (rb != null)
                {
                    // Animated child transforms must follow their owner immediately.
                    // Physics interpolation otherwise writes cached world poses over them.
                    if (_disableInterpolationWhileAlive)
                        rb.interpolation = value ? RigidbodyInterpolation.None : RigidbodyInterpolation.Interpolate;
                    rb.isKinematic = value;
                }
        }
    }
}
