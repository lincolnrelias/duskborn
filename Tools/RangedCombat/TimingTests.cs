using System;
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
        Console.WriteLine($"{passed} ranged timing assertions passed.");
    }
}
