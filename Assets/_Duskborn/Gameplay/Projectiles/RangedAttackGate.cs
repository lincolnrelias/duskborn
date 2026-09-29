using System;

namespace Duskborn.Gameplay.Projectiles
{
    // Engine-independent server timing policy, also exercised by the offline tests.
    public sealed class RangedAttackGate
    {
        public int Generation { get; private set; }
        public double ReleaseAt { get; private set; }
        public double NextAttackAt { get; private set; }
        public bool Pending { get; private set; }
        private bool releaseRequested;

        public bool TryBegin(double now, double windup, double duration, double cooldown)
        {
            if (!Finite(now) || !Finite(windup) || !Finite(duration) || !Finite(cooldown) ||
                windup < 0 || duration <= 0 || windup >= duration || cooldown < 0 || now < NextAttackAt) return false;
            Generation++;
            ReleaseAt = now + windup;
            NextAttackAt = now + Math.Max(duration, cooldown);
            Pending = true;
            releaseRequested = false;
            return true;
        }

        public bool TryRequestRelease(double now)
        {
            if (!Finite(now) || !Pending || releaseRequested || now > NextAttackAt + 1) return false;
            releaseRequested = true;
            return true;
        }

        public bool TryConsume(int generation, double now)
        {
            if (!Finite(now) || !Pending || !releaseRequested || generation != Generation || now < ReleaseAt || now > NextAttackAt + 1) return false;
            Pending = false;
            return true;
        }

        public void Cancel() { Pending = false; Generation++; }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
