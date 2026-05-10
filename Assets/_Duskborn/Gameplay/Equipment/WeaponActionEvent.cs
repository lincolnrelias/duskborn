using System;
using UnityEngine;

namespace Duskborn.Gameplay.Equipment
{
    [Serializable]
    public struct WeaponActionEvent
    {
        public WeaponEventType Type;
        [Range(0f, 1f)]
        public float NormalizedTime;
    }
}
