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
                else boneCols.Add(c);
            }
            _rootColliders = rootCols.ToArray();
            _boneColliders = boneCols.ToArray();

            SetKinematic(true);
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

        private void SetKinematic(bool value)
        {
            foreach (var rb in _bones)
                if (rb != null) rb.isKinematic = value;
        }
    }
}
