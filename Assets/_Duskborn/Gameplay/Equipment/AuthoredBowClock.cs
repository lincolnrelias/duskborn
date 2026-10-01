using System;
namespace Duskborn.Gameplay.Equipment
{
    // Clock for the vendor's separate Load/Hold/Release takes; never advances
    // into Release without an explicit request, even on a very long frame.
    public sealed class AuthoredBowClock
    {
        public enum Stage { Load, Hold, Release, Complete }
        public Stage Phase { get; private set; }
        public double Time { get; private set; }
        public double LoadDuration { get; }
        public double ReleaseDuration { get; }
        private bool requested;
        public double ActionTime => Phase == Stage.Load ? Time : Phase == Stage.Hold ? LoadDuration : LoadDuration + Time;
        public AuthoredBowClock(double load, double release)
        {
            if(double.IsNaN(load) || double.IsInfinity(load) || load<=0 ||
                double.IsNaN(release) || double.IsInfinity(release) || release<=0)
                throw new ArgumentOutOfRangeException(nameof(load));
            LoadDuration=load; ReleaseDuration=release;
        }
        public void RequestRelease() => requested=true;
        public void Advance(double dt)
        {
            if (double.IsNaN(dt) || double.IsInfinity(dt) || dt<0 || Phase==Stage.Complete) return;
            if (Phase==Stage.Hold && requested) { Phase=Stage.Release; Time=0; }
            Time+=dt;
            if (Phase==Stage.Load && Time>=LoadDuration)
            {
                double remainder=Time-LoadDuration;
                Phase=requested ? Stage.Release : Stage.Hold;
                Time=remainder;
            }
            if (Phase==Stage.Release && Time>=ReleaseDuration) { Time=ReleaseDuration; Phase=Stage.Complete; }
        }
    }
}
