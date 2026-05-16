using System;
using UnityEngine;

namespace Duskborn.Audio
{
    public class WeaponHitNotifier : MonoBehaviour
    {
        public readonly struct HitData
        {
            public readonly string    Tag;
            public readonly int       Layer;
            public readonly AudioClip AudioOverride; // non-null = skip profile lookup

            public HitData(string tag, int layer, AudioClip audioOverride = null)
            {
                Tag           = tag;
                Layer         = layer;
                AudioOverride = audioOverride;
            }
        }

        public event Action<HitData> OnHit;

        public void Raise(HitData data) => OnHit?.Invoke(data);
    }
}
