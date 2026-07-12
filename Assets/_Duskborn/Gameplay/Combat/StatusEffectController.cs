using System;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using Duskborn.Core;

namespace Duskborn.Gameplay.Combat
{
    /// <summary>
    /// Server-authoritative status effects. Call ServerApply/ServerRemove on the server;
    /// Has() and OnEffectChanged are valid everywhere (active mask is a SyncVar).
    /// </summary>
    public class StatusEffectController : NetworkBehaviour
    {
        private readonly SyncVar<int> _activeMask = new();
        private readonly float[]      _endTimes   = new float[32];

        /// <summary>(effect, nowActive) — fires on server and clients.</summary>
        public event Action<StatusEffect, bool> OnEffectChanged;

        public bool Has(StatusEffect effect) => (_activeMask.Value & (int)effect) != 0;

        private void Awake()
        {
            _activeMask.OnChange += OnMaskChanged;
            if (GetComponent<StatusEffectVisuals>() == null)
                gameObject.AddComponent<StatusEffectVisuals>();
        }

        private void OnMaskChanged(int prev, int next, bool asServer)
        {
            int changed = prev ^ next;
            if (changed == 0) return;
            for (int bit = 0; bit < 32; bit++)
            {
                int flag = 1 << bit;
                if ((changed & flag) == 0) continue;
                OnEffectChanged?.Invoke((StatusEffect)flag, (next & flag) != 0);
            }
        }

        /// <summary>Server-only. duration &lt;= 0 keeps the effect until ServerRemove.</summary>
        public void ServerApply(StatusEffect effect, float duration)
        {
            if (!IsServerStarted || effect == StatusEffect.None) return;

            float end = duration > 0f ? Time.time + duration : float.PositiveInfinity;
            for (int bit = 0; bit < 32; bit++)
                if (((int)effect & (1 << bit)) != 0)
                    _endTimes[bit] = Mathf.Max(_endTimes[bit], end);

            _activeMask.Value |= (int)effect;
            DuskLog.Log(LogChannel.Combat, $"{name}: +{effect} ({(duration > 0f ? $"{duration:F2}s" : "until removed")}).");
        }

        public void ServerRemove(StatusEffect effect)
        {
            if (!IsServerStarted || effect == StatusEffect.None) return;
            _activeMask.Value &= ~(int)effect;
        }

        private void Update()
        {
            if (!IsServerStarted) return;
            int mask = _activeMask.Value;
            if (mask == 0) return;

            int newMask = mask;
            for (int bit = 0; bit < 32; bit++)
                if ((mask & (1 << bit)) != 0 && Time.time >= _endTimes[bit])
                    newMask &= ~(1 << bit);

            if (newMask != mask) _activeMask.Value = newMask;
        }
    }
}
