using System;

namespace Duskborn.Gameplay.Enemies
{
    public enum BramblekinPhase { Hunt, Windup, Swing, Recover, Dead }

    /// <summary>One committed cudgel swing. A hitch never skips a newly entered phase.</summary>
    public sealed class BramblekinClock
    {
        public const float Range = 3.3f;
        public const float EngageRange = 1.2f;
        public const float AttackPlaybackSpeed = 2;
        public const float WindupSeconds = .55f / AttackPlaybackSpeed;
        public const float SwingSeconds = .18f / AttackPlaybackSpeed;
        public const float StrikeSeconds = .09f / AttackPlaybackSpeed;
        public const float RecoverSeconds = .75f / AttackPlaybackSpeed;
        public BramblekinPhase Phase { get; private set; }
        public float Age { get; private set; }
        public event Action<BramblekinPhase> Changed;

        public void Reset() => Enter(BramblekinPhase.Hunt);
        public bool TryBegin(float distance)
        {
            if (Phase != BramblekinPhase.Hunt || float.IsNaN(distance) || distance < 0 || distance > EngageRange) return false;
            Enter(BramblekinPhase.Windup);
            return true;
        }
        public void Tick(float delta)
        {
            if (float.IsNaN(delta) || float.IsInfinity(delta) || delta <= 0 || Phase == BramblekinPhase.Dead) return;
            Age += delta;
            if (Phase == BramblekinPhase.Windup && Age >= WindupSeconds) Enter(BramblekinPhase.Swing);
            else if (Phase == BramblekinPhase.Swing && Age >= SwingSeconds) Enter(BramblekinPhase.Recover);
            else if (Phase == BramblekinPhase.Recover && Age >= RecoverSeconds) Enter(BramblekinPhase.Hunt);
        }
        public void Interrupt()
        {
            if (Phase == BramblekinPhase.Windup || Phase == BramblekinPhase.Swing) Enter(BramblekinPhase.Recover);
        }
        public void Kill() => Enter(BramblekinPhase.Dead);
        private void Enter(BramblekinPhase phase) { Phase = phase; Age = 0; Changed?.Invoke(phase); }
    }
}
