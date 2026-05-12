using System;
using UnityEngine;

namespace Duskborn.Gameplay.Equipment
{
    [Serializable]
    public class WeaponActionData
    {
        public bool                PreserveLocomotion = true;
        public AnimationClip[]     Clips;
        public WeaponActionEvent[] Events;

        public AnimationClip PickClip() =>
            Clips is { Length: > 0 } ? Clips[UnityEngine.Random.Range(0, Clips.Length)] : null;
    }
}
