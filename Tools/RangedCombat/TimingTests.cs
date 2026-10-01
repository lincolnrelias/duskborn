using System;
using Duskborn.Gameplay.Equipment;
using Duskborn.Gameplay.Projectiles;

static class TimingTests
{
    static int passed;
    static void Check(bool condition, string name)
    { if (!condition) throw new Exception(name); passed++; }
    static void Main()
    {
        var gate = new RangedAttackGate();
        Check(!gate.TryRequestRelease(0), "Release without begin");
        Check(!gate.TryBegin(0, double.NaN, 1, 1), "Reject NaN configuration");
        Check(!gate.TryBegin(0, .8, .5, 1), "Reject release after animation end");
        Check(gate.TryBegin(0, .6, 1, 1.5), "Begin valid attack");
        int first = gate.Generation;
        Check(!gate.TryBegin(.1, .6, 1, 1), "Server cooldown blocks begin spam");
        Check(gate.TryRequestRelease(.1), "Early request queues once");
        Check(!gate.TryRequestRelease(.2), "Duplicate release rejected");
        Check(!gate.TryConsume(first,.59), "Windup enforced");
        Check(gate.TryConsume(first,.6), "Release at authored time");
        Check(!gate.TryConsume(first,.7), "Only one projectile per attack");
        Check(!gate.TryBegin(1.49,.6,1,1), "Attack speed cooldown enforced");
        Check(gate.TryBegin(1.5,.6,1,1), "Next attack allowed");
        int cancelled=gate.Generation;
        Check(gate.TryRequestRelease(1.6), "Second request");
        gate.Cancel();
        Check(!gate.TryConsume(cancelled,2.2), "Switch/death cancels queued shot");
        Check(!gate.TryBegin(2,.6,1,1), "Cancellation does not reset cooldown");
        Check(gate.TryBegin(2.5,.6,1,1), "New attack after cancellation");
        Check(gate.TryRequestRelease(2.6), "New request after cancellation");
        Check(!gate.TryConsume(cancelled,3.2), "Stale coroutine cannot release a new attack");
        Check(!gate.TryConsume(gate.Generation,double.PositiveInfinity), "Nonfinite clock rejected");
        Check(!gate.TryConsume(gate.Generation,4.6), "Stale release expires");
        gate = new RangedAttackGate();
        Check(gate.TryBegin(0, .6, 1, 1.5, true), "Begin held draw");
        int held = gate.Generation;
        Check(!gate.TryBegin(60, .6, 1, 1.5, true), "Held draw cannot be replaced by begin spam");
        Check(!gate.TryConsume(held, 60), "Holding aim never auto-fires");
        Check(gate.TryRequestRelease(60), "Held aim can release after a long wait");
        Check(gate.TryConsume(held, 60), "Long hold releases once");
        Check(!gate.TryConsume(held, 60), "Held shot cannot duplicate projectile");
        gate.Cancel();
        Check(!gate.TryBegin(61.49, .6, 1, 1.5, true), "Leaving aim after firing preserves cooldown");
        Check(gate.TryBegin(61.5, .6, 1, 1.5, true), "Held shot recovery expires");
        held = gate.Generation;
        Check(gate.TryRequestRelease(61.6), "Early aimed click queues release");
        Check(!gate.TryConsume(held, 61.6), "Aimed click cannot bypass windup");
        gate.Cancel();
        Check(!gate.TryConsume(held, 62.2), "Dodge cancels queued aimed projectile");
        Check(gate.TryBegin(61.8, .6, 1, 1.5, true), "Interrupted draw can restart after dodge");
        Check(!gate.TryConsume(held, 62.5), "Pre-dodge generation cannot fire after resume");
        Check(gate.TryRequestRelease(62), "Resumed draw accepts fire request");
        Check(!gate.TryConsume(gate.Generation, 62.39), "Resumed draw needs a fresh windup");
        Check(gate.TryConsume(gate.Generation, 62.4), "Resumed draw releases after windup");
        var bowClock = new AuthoredBowClock(40.0/30,23.0/30);
        bowClock.Advance(100);
        Check(bowClock.Phase == AuthoredBowClock.Stage.Hold, "RMB only: long frame stops at Hold");
        Check(bowClock.ActionTime < 1.4157916, "RMB only: never reaches projectile event");
        for(int i=0;i<1000;i++) bowClock.Advance(.016);
        Check(bowClock.Phase == AuthoredBowClock.Stage.Hold, "RMB only: hold loop never enters Release");
        bowClock.RequestRelease(); bowClock.Advance(.01);
        Check(bowClock.Phase == AuthoredBowClock.Stage.Release, "Explicit LMB release starts authored Release");
        Check(Math.Abs(bowClock.Time-.01)<.000001, "Release does not inherit elapsed hold time");
        bowClock.Advance(2);
        Check(bowClock.Phase == AuthoredBowClock.Stage.Complete, "Release completes once");
        bowClock.RequestRelease(); bowClock.Advance(1);
        Check(bowClock.Phase == AuthoredBowClock.Stage.Complete, "Duplicate request cannot replay release");
        bowClock = new AuthoredBowClock(40.0/30,23.0/30);
        bowClock.RequestRelease(); bowClock.Advance(.1);
        Check(bowClock.Phase == AuthoredBowClock.Stage.Load, "Early LMB waits for load");
        bowClock.Advance(1.25);
        Check(bowClock.Phase == AuthoredBowClock.Stage.Release, "Queued LMB releases after load");
        bowClock = new AuthoredBowClock(40.0/30,23.0/30);
        bowClock.Advance(double.NaN); bowClock.Advance(-1);
        Check(bowClock.Time==0, "Invalid frame time ignored");
        bowClock.Advance(5);
        Check(bowClock.Phase == AuthoredBowClock.Stage.Hold, "Fresh redraw drops previous shot request");
        Console.WriteLine($"{passed} ranged timing assertions passed.");
    }
}
