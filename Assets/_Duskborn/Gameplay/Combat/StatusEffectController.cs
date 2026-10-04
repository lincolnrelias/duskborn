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
    public class StatusEffectController : NetworkBehaviour, Duskborn.Effects.IDebuffSource
    {
        private readonly SyncVar<int> _activeMask = new();
        private readonly SyncDictionary<int, Duskborn.Effects.DebuffTiming> _timing = new();
        private double NetworkTime => TimeManager != null ? TimeManager.TicksToTime(TimeManager.Tick) : Time.timeAsDouble;
        public bool TryGetDebuff(Duskborn.Gameplay.Enchanting.RuneKind kind, out Duskborn.Effects.DebuffView view)
        {
            view = default;
            var effect = kind switch
            {
                Duskborn.Gameplay.Enchanting.RuneKind.Flame => StatusEffect.Burn,
                Duskborn.Gameplay.Enchanting.RuneKind.Stone => StatusEffect.Slow,
                Duskborn.Gameplay.Enchanting.RuneKind.Storm => StatusEffect.Stun,
                Duskborn.Gameplay.Enchanting.RuneKind.Blood => StatusEffect.Bleed,
                _ => StatusEffect.None
            };
            var health = GetComponent<Duskborn.Gameplay.IHealthProvider>();
            if (effect == StatusEffect.None || !Has(effect) || (health != null && health.CurrentHP <= 0)) return false;
            bool hasTiming = _timing.TryGetValue((int)effect, out var timing);
            view = new Duskborn.Effects.DebuffView(hasTiming ? timing.Stacks : 1,
                hasTiming ? timing.Remaining(NetworkTime) : 1f, effect == StatusEffect.Stun);
            return true;
        }
        private readonly float[]      _endTimes   = new float[32];

        /// <summary>(effect, nowActive) — fires on server and clients.</summary>
        public event Action<StatusEffect, bool> OnEffectChanged;

        public bool Has(StatusEffect effect) => (_activeMask.Value & (int)effect) != 0;

        private void Awake()
        {
            _activeMask.OnChange += OnMaskChanged;
            if (GetComponent<StatusEffectVisuals>() == null)
                gameObject.AddComponent<StatusEffectVisuals>();
            if (GetComponent<Duskborn.Effects.EnemyRuneVisuals>() == null)
                gameObject.AddComponent<Duskborn.Effects.EnemyRuneVisuals>();
        }

        private void OnDestroy()
        {
            _activeMask.OnChange -= OnMaskChanged;
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
        public void ServerApply(StatusEffect effect, float duration, int stacks = 1)
        {
            if (!IsServerStarted || effect == StatusEffect.None) return;

            float end = duration > 0f ? Time.time + duration : float.PositiveInfinity;
            for (int bit = 0; bit < 32; bit++)
                if (((int)effect & (1 << bit)) != 0)
                    _endTimes[bit] = (_activeMask.Value & (1 << bit)) != 0
                        ? Mathf.Max(_endTimes[bit], end) : end;

            for (int bit = 0; bit < 32; bit++)
            {
                int flag = 1 << bit;
                if (((int)effect & flag) == 0) continue;
                int previous = Has((StatusEffect)flag) && _timing.TryGetValue(flag, out var oldTiming) ? oldTiming.Stacks : 0;
                float remaining = float.IsPositiveInfinity(_endTimes[bit]) ? 0 : _endTimes[bit] - Time.time;
                _timing[flag] = new Duskborn.Effects.DebuffTiming(NetworkTime, remaining, Mathf.Clamp(previous + stacks, 1, 12));
            }
            _activeMask.Value |= (int)effect;
            DuskLog.Log(LogChannel.Combat, $"{name}: +{effect} ({(duration > 0f ? $"{duration:F2}s" : "until removed")}).");
        }

        public void ServerRemove(StatusEffect effect)
        {
            if (!IsServerStarted || effect == StatusEffect.None) return;
            for (int bit = 0; bit < 32; bit++)
                if (((int)effect & (1 << bit)) != 0) _endTimes[bit] = 0f;
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
