using System.Collections.Generic;
using UnityEngine;

namespace Duskborn.Gameplay.Enemies
{
    public class EnemyRagdoll : MonoBehaviour
    {
        [SerializeField] private Animator _animator;

        [SerializeField] private float _impulseScale = 5f;

        private Rigidbody[] _bones;
        private Collider[]  _rootColliders;

        private void Awake()
        {
            if (_animator == null)
                _animator = GetComponentInChildren<Animator>();

            var all = GetComponentsInChildren<Rigidbody>();
            var list = new List<Rigidbody>(all.Length);
            foreach (var rb in all)
                if (rb.gameObject != gameObject) list.Add(rb);
            _bones = list.ToArray();

            _rootColliders = GetComponents<Collider>();
            SetKinematic(true);
        }

        public void EnableRagdoll()
        {
            if (_animator != null) _animator.enabled = false;
            SetKinematic(false);
            foreach (var col in _rootColliders)
                if (col != null) col.enabled = false;
        }

        public void DisableRagdoll()
        {
            SetKinematic(true);
            if (_animator != null) _animator.enabled = true;
            foreach (var col in _rootColliders)
                if (col != null) col.enabled = true;
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
