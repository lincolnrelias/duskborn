using Duskborn.Gameplay.Enchanting;
using Duskborn.Gameplay.Player;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Duskborn.Gameplay.Enemies
{
    public abstract partial class EnemyBase
    {
        private readonly SyncVar<ulong> _runeStacks = new();
        private readonly SyncVar<bool> _runeFrozen = new();
        private readonly SyncVar<RuneKind> _runeLockKind = new();
        private float _runeFrozenUntil;
        private readonly float[] _runeExpires = new float[9];
        private readonly PlayerStats[] _runeAttackers = new PlayerStats[9];
        private float _runeTickAt;
        private int _runeTickFrame = -1;
        public int RuneStacks(RuneKind kind) => kind > RuneKind.None && kind <= RuneKind.Radiance
            ? (int)((_runeStacks.Value >> (((int)kind - 1) * 8)) & 255UL) : 0;
        public bool RuneFrozen => _runeFrozen.Value;
        public RuneKind RuneLockKind => _runeLockKind.Value;
        public float RuneMoveScale => RuneFrozen ? 0f : Mathf.Max(.25f,
            1f - .04f * RuneStacks(RuneKind.Stone) - .05f * RuneStacks(RuneKind.Frost));

        private void SetRuneStacks(RuneKind kind, int value)
        {
            int shift = ((int)kind - 1) * 8;
            _runeStacks.Value = (_runeStacks.Value & ~(255UL << shift)) | ((ulong)Mathf.Clamp(value, 0, RuneCatalog.StackCap) << shift);
        }
        public void ApplyWeaponEtching(WeaponEtching rune, PlayerStats attacker)
        {
            if (!IsServerStarted || !IsAlive || !rune.IsValid || attacker == null) return;
            bool hadStacks = _runeStacks.Value != 0;
            int stacks = RuneCatalog.AddStacks(RuneStacks(rune.kind), rune);
            SetRuneStacks(rune.kind, stacks);
            _runeExpires[(int)rune.kind] = Time.time + (rune.kind == RuneKind.Venom ? 9f : RuneCatalog.Duration);
            _runeAttackers[(int)rune.kind] = attacker;
            if (!hadStacks) _runeTickAt = Time.time + 1f;
            if (rune.kind == RuneKind.Storm && RuneCatalog.ShouldProc(rune.kind, stacks))
            {
                SetRuneStacks(rune.kind, 0);
                TakeDamage(18f, false, attacker);
                if (!IsAlive) return;
                ShowRuneBurstRpc(RuneKind.Storm);
                _runeFrozen.Value = true;
                _runeLockKind.Value = RuneKind.Storm;
                _runeFrozenUntil = Time.time + .4f;
                _staggerTimer = Mathf.Max(_staggerTimer, .4f);
            }
            if (rune.kind == RuneKind.Frost && RuneCatalog.ShouldProc(rune.kind, stacks))
            {
                SetRuneStacks(rune.kind, 0);
                ShowRuneBurstRpc(RuneKind.Frost);
                // Bosses still receive chill, with a shorter freeze to preserve encounters.
                _runeFrozen.Value = true;
                _runeLockKind.Value = RuneKind.Frost;
                _runeFrozenUntil = Time.time + (this is HollowWardenBoss ? .35f : 1.2f);
                _staggerTimer = Mathf.Max(_staggerTimer, .35f);
            }
        }
        [FishNet.Object.ObserversRpc(RunLocally = true)]
        private void ShowRuneBurstRpc(RuneKind kind)
        {
            var go = new GameObject(kind + " discharge"); go.transform.SetParent(transform, false);
            go.AddComponent<Duskborn.Effects.RuneAura>().Configure(kind, 8, Duskborn.Effects.RuneAura.LocalBounds(transform));
            Destroy(go, .5f);
        }

        internal void TickRuneEffects()
        {
            if (!IsServerStarted || !IsAlive) return;
            if (_runeTickFrame == Time.frameCount) return;
            _runeTickFrame = Time.frameCount;
            if (Time.time >= _runeFrozenUntil) { _runeFrozen.Value = false; _runeLockKind.Value = RuneKind.None; }
            if (_runeStacks.Value == 0) return;
            for (int i = 1; i <= 8; i++)
                if (Time.time >= _runeExpires[i]) SetRuneStacks((RuneKind)i, 0);
            if (Time.time < _runeTickAt) return;
            _runeTickAt = Time.time + 1f;
            foreach (var kind in new[] { RuneKind.Flame, RuneKind.Venom, RuneKind.Blood, RuneKind.Radiance })
            {
                int stacks = RuneStacks(kind);
                if (stacks == 0 || !IsAlive) continue;
                TakeDamage(RuneCatalog.TickDamage(kind, stacks), false, _runeAttackers[(int)kind]);
            }
        }
        private void ResetRuneEffects()
        {
            _runeStacks.Value = 0;
            _runeFrozen.Value = false;
            _runeLockKind.Value = RuneKind.None;
            _runeFrozenUntil = 0;
            System.Array.Clear(_runeExpires, 0, _runeExpires.Length);
            System.Array.Clear(_runeAttackers, 0, _runeAttackers.Length);
            _runeTickAt = Time.time + 1f;
            _runeTickFrame = -1;
        }
    }
}
