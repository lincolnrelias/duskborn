using System;

namespace Duskborn.Gameplay.Enemies
{
    public enum BriarbackPhase { Hunt, Windup, Charge, Recover, Dead, HeadbuttWindup, Headbutt, HeadbuttRecover }
    public enum BriarbackAttack { None, Charge, Headbutt }

    /// <summary>Independent attack clock. Excess frame time never consumes a new telegraph.</summary>
    public sealed class BriarbackCharge
    {
        public const float WindupSeconds = .9f;
        public const float ChargeSeconds = .8f;
        public const float RecoverSeconds = 1.4f;
        public const float CooldownSeconds = 3f;
        public const float ChargeSpeed = 10f;
        public const float ChargeLength = ChargeSpeed * ChargeSeconds;
        public const float HitRadius = .7f;
        public const float HeadbuttRange = 2.2f;
        public const float HeadbuttWindupSeconds = .4f;
        public const float HeadbuttSeconds = .18f;
        public const float HeadbuttRecoverSeconds = .7f;
        public const float HeadbuttCooldownSeconds = 1.5f;
        public const float HeadbuttLockSeconds = .2f;
        public BriarbackPhase Phase { get; private set; }
        public float Age { get; private set; }
        public float Cooldown { get; private set; }
        public float HeadbuttCooldown { get; private set; }
        public event Action<BriarbackPhase> Changed;

        public void Reset() { Cooldown = HeadbuttCooldown = 1f; Enter(BriarbackPhase.Hunt); }
        public static BriarbackAttack SelectAttack(float distance, bool reachable = true)
        {
            if (!reachable || distance < 0 || float.IsNaN(distance)) return BriarbackAttack.None;
            return distance <= HeadbuttRange ? BriarbackAttack.Headbutt : distance <= 7 ? BriarbackAttack.Charge : BriarbackAttack.None;
        }
        public bool TryBegin(float distance, bool reachable = true)
        {
            if (Phase != BriarbackPhase.Hunt) return false;
            switch (SelectAttack(distance, reachable))
            {
                case BriarbackAttack.Headbutt:
                    if (HeadbuttCooldown > 0) return false;
                    Enter(BriarbackPhase.HeadbuttWindup); return true;
                case BriarbackAttack.Charge: return TryBegin();
                default: return false;
            }
        }
        public bool TryBegin()
        {
            if (Phase != BriarbackPhase.Hunt || Cooldown > 0) return false;
            Enter(BriarbackPhase.Windup);
            return true;
        }
        public void Tick(float delta)
        {
            delta = Math.Max(0, delta);
            Age += delta;
            Cooldown = Math.Max(0, Cooldown - delta);
            HeadbuttCooldown = Math.Max(0, HeadbuttCooldown - delta);
            if (Phase == BriarbackPhase.Windup && Age >= WindupSeconds) Enter(BriarbackPhase.Charge);
            else if (Phase == BriarbackPhase.Charge && Age >= ChargeSeconds) Recover();
            else if (Phase == BriarbackPhase.Recover && Age >= RecoverSeconds) Enter(BriarbackPhase.Hunt);
            else if (Phase == BriarbackPhase.HeadbuttWindup && Age >= HeadbuttWindupSeconds) Enter(BriarbackPhase.Headbutt);
            else if (Phase == BriarbackPhase.Headbutt && Age >= HeadbuttSeconds)
            { HeadbuttCooldown = HeadbuttCooldownSeconds; Enter(BriarbackPhase.HeadbuttRecover); }
            else if (Phase == BriarbackPhase.HeadbuttRecover && Age >= HeadbuttRecoverSeconds) Enter(BriarbackPhase.Hunt);
        }
        public void Recover()
        {
            if (Phase == BriarbackPhase.Dead) return;
            Cooldown = CooldownSeconds;
            Enter(BriarbackPhase.Recover);
        }
        public void Kill() => Enter(BriarbackPhase.Dead);
        private void Enter(BriarbackPhase phase) { Phase = phase; Age = 0; Changed?.Invoke(phase); }
    }
}
