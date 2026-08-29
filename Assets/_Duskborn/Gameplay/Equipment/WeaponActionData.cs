using System;
using UnityEngine;

namespace Duskborn.Gameplay.Equipment
{
    /// <summary>
    /// One playable clip of an action — a combo step or a random variant — with its own
    /// hit-event timings and damage multiplier.
    /// </summary>
    [Serializable]
    public class WeaponActionClip
    {
        public AnimationClip       Clip;
        public float               DamageMultiplier = 1f;
        public WeaponActionEvent[] Events;
    }

    [Serializable]
    public class WeaponActionData
    {
        public float BaseSpeed          = 1f;
        public bool  PreserveLocomotion = true;

        // Combo: entries become sequential steps instead of a random pick. Chain advances
        // when the next attack starts within ComboResetTime after the previous one ends.
        public bool  ComboChain;
        public float ComboResetTime = 0.8f;

        public WeaponActionClip[] Entries;

        // ── Legacy (shared events, parallel arrays). Migrated to Entries by the
        //    WeaponActionDataDrawer; EnsureMigrated() covers assets never re-saved. ──
        [HideInInspector] public AnimationClip[]     Clips;
        [HideInInspector] public WeaponActionEvent[] Events;
        [HideInInspector] public float[]             ComboDamageMultipliers;

        public bool HasEntries => Entries is { Length: > 0 };

        public WeaponActionClip PickRandom() =>
            HasEntries ? Entries[UnityEngine.Random.Range(0, Entries.Length)] : null;

        // In-memory fallback for assets saved before per-clip entries existed.
        public void EnsureMigrated()
        {
            if (HasEntries || Clips is not { Length: > 0 }) return;
            Entries = new WeaponActionClip[Clips.Length];
            for (int i = 0; i < Clips.Length; i++)
            {
                float mult = ComboDamageMultipliers != null && i < ComboDamageMultipliers.Length
                    ? ComboDamageMultipliers[i] : 1f;
                Entries[i] = new WeaponActionClip
                {
                    Clip             = Clips[i],
                    DamageMultiplier = mult > 0f ? mult : 1f,
                    Events           = Events != null
                        ? (WeaponActionEvent[])Events.Clone()
                        : Array.Empty<WeaponActionEvent>(),
                };
            }
        }
    }
}
