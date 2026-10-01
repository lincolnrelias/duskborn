using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Duskborn.Gameplay.Enemies
{
    public class EnemyRagdoll : MonoBehaviour
    {
        [SerializeField] private Animator _animator;

        [SerializeField] private float _impulseScale = 5f;
        [SerializeField] private float _settleDuration = 2.5f;

        private Rigidbody[] _bones;
        private Collider[]  _boneColliders;
        private Collider[]  _rootColliders;
        private Coroutine   _freezeCoroutine;

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
            if (_freezeCoroutine != null)
            {
                StopCoroutine(_freezeCoroutine);
                _freezeCoroutine = null;
            }

            if (_animator != null) _animator.enabled = false;
            SetKinematic(false);
            foreach (var col in _rootColliders)
                if (col != null) col.enabled = false;
            foreach (var col in _boneColliders)
                if (col != null) col.enabled = true;

            _freezeCoroutine = StartCoroutine(FreezeAfterDelay());
        }

        public void DisableRagdoll()
        {
            if (_freezeCoroutine != null)
            {
                StopCoroutine(_freezeCoroutine);
                _freezeCoroutine = null;
            }

            SetKinematic(true);
            if (_animator != null) _animator.enabled = true;
            foreach (var col in _rootColliders)
                if (col != null) col.enabled = true;
            foreach (var col in _boneColliders)
                if (col != null) col.enabled = true;
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
            Rigidbody nearest  = null;
            float     bestDist = float.MaxValue;
            foreach (var rb in _bones)
            {
                if (rb == null) continue;
                float d = (rb.position - hitPoint).sqrMagnitude;
                if (d < bestDist) { bestDist = d; nearest = rb; }
            }
            nearest?.AddForce(direction * _impulseScale, ForceMode.Impulse);
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
                if (rb != null) rb.isKinematic = value;
        }
    }
}
