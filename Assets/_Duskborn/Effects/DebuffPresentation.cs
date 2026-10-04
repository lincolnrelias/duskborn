using Duskborn.Gameplay.Enchanting;
using UnityEngine;

namespace Duskborn.Effects
{
    public struct DebuffTiming
    {
        public double EndsAt;
        public float Duration;
        public int Stacks;
        public DebuffTiming(double now, float duration, int stacks)
        { EndsAt = duration > 0 ? now + duration : double.PositiveInfinity; Duration = duration; Stacks = stacks; }
        public float Remaining(double now) => double.IsPositiveInfinity(EndsAt) ? 1f : Mathf.Clamp01((float)(EndsAt - now) / Mathf.Max(.001f, Duration));
    }
    public struct DebuffView
    {
        public int Stacks;
        public float Remaining;
        public bool Locked;
        public DebuffView(int stacks, float remaining, bool locked = false)
        { Stacks = stacks; Remaining = remaining; Locked = locked; }
    }
    public interface IDebuffSource
    {
        bool TryGetDebuff(RuneKind kind, out DebuffView view);
    }
}
