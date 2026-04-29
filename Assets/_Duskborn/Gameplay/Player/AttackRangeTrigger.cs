using System;
using UnityEngine;

namespace Duskborn.Gameplay.Player
{
    // Child object on the player prefab. Holds the SphereCollider (IsTrigger) that
    // defines melee attack range. Forwards trigger events to PlayerCombat via events.
    public class AttackRangeTrigger : MonoBehaviour
    {
        public event Action<Collider> OnEnter;
        public event Action<Collider> OnExit;

        private void OnTriggerEnter(Collider other) => OnEnter?.Invoke(other);
        private void OnTriggerExit(Collider other)  => OnExit?.Invoke(other);
    }
}
