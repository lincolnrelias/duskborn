using System;

namespace Duskborn.Gameplay.Enemies
{
    public enum ThornwingPhase { Hunt, Windup, Recover, Dead }

    // One transition per tick: hitches cannot erase warnings or recovery.
    public sealed class ThornwingClock
    {
        public const float WindupSeconds = .5f, RecoverSeconds = .8f, CooldownSeconds = 1.7f;
        public const float Range = 9f, MaxElevation = 1.5f, TrackingSeconds = .4f;
        public ThornwingPhase Phase { get; private set; }
        public float Age { get; private set; }
        public float Cooldown { get; private set; }
        public event Action<ThornwingPhase> Changed;
        public event Action Release;

        public void Reset() { Cooldown = 1.1f; Enter(ThornwingPhase.Hunt); }
        public bool TryBegin(float distance, float elevation, bool clear)
        {
            if (Phase != ThornwingPhase.Hunt || Cooldown > 0 || !clear ||
                !float.IsFinite(distance) || !float.IsFinite(elevation) || distance < 0 ||
                distance > Range || Math.Abs(elevation) > MaxElevation) return false;
            Enter(ThornwingPhase.Windup); return true;
        }
        public void Tick(float seconds)
        {
            if (!float.IsFinite(seconds) || seconds <= 0 || Phase == ThornwingPhase.Dead) return;
            Age += seconds;
            if (Phase == ThornwingPhase.Hunt) Cooldown = Math.Max(0, Cooldown - seconds);
            else if (Phase == ThornwingPhase.Windup && Age >= WindupSeconds)
            {
                // Enter recovery even if the shot is rejected by range/obstacles at release.
                Cooldown = CooldownSeconds;
                Enter(ThornwingPhase.Recover);
                Release?.Invoke();
            }
            else if (Phase == ThornwingPhase.Recover && Age >= RecoverSeconds) Enter(ThornwingPhase.Hunt);
        }
        public void Interrupt()
        {
            if (Phase != ThornwingPhase.Windup) return;
            Cooldown = CooldownSeconds; Enter(ThornwingPhase.Recover);
        }
        public void Kill() { Enter(ThornwingPhase.Dead); }
        private void Enter(ThornwingPhase phase) { Phase = phase; Age = 0; Changed?.Invoke(phase); }
    }
}
