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
        private bool heldDraw;
        private double recovery;

        public bool TryBegin(double now, double windup, double duration, double cooldown, bool holdDraw = false)
        {
            if (!Finite(now) || !Finite(windup) || !Finite(duration) || !Finite(cooldown) ||
                windup < 0 || duration <= 0 || windup >= duration || cooldown < 0 || now < NextAttackAt ||
                (heldDraw && Pending)) return false;
            Generation++;
            ReleaseAt = now + windup;
            NextAttackAt = now + Math.Max(duration, cooldown);
            Pending = true;
            releaseRequested = false;
            heldDraw = holdDraw;
            recovery = Math.Max(duration - windup, cooldown);
            return true;
        }

        public bool TryRequestRelease(double now)
        {
            if (!Finite(now) || !Pending || releaseRequested || (!heldDraw && now > NextAttackAt + 1)) return false;
            releaseRequested = true;
            return true;
        }

        public bool TryConsume(int generation, double now)
        {
            if (!Finite(now) || !Pending || !releaseRequested || generation != Generation || now < ReleaseAt ||
                (!heldDraw && now > NextAttackAt + 1)) return false;
            Pending = false;
            if (heldDraw) NextAttackAt = now + recovery;
            return true;
        }

        public void Cancel()
        {
            // An interrupted held draw has not fired; allow a fresh draw after the dodge.
            // Once fired, its recovery/cooldown survives cancellation and weapon switches.
            if (heldDraw && Pending) NextAttackAt = 0;
            Pending = false;
            heldDraw = false;
            Generation++;
        }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
