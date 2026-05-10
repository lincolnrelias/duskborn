using System;
using UnityEngine;

namespace Duskborn.Gameplay.Equipment
{
    [Serializable]
    public class WeaponActionData
    {
        public AnimationClip      Clip;
        public WeaponActionEvent[] Events;
    }
}
