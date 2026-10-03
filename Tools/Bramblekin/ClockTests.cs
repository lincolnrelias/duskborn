using System;
using Duskborn.Gameplay.Enemies;

public static class ClockTests
{
    private static int checks;
    private static void Check(bool value, string name) { checks++; if (!value) throw new Exception(name); }
    public static void Main()
    {
        var clock = new BramblekinClock(); clock.Reset();
        Check(!clock.TryBegin(2), "close before committing even inside extended reach");
        Check(BramblekinClock.Range == 3.3f && BramblekinClock.Range - BramblekinClock.EngageRange > 2, "deliberate retreat margin");
        Check(!clock.TryBegin(float.NaN) && !clock.TryBegin(-1), "invalid target");
        Check(clock.TryBegin(BramblekinClock.EngageRange), "boundary target");
        Check(!clock.TryBegin(0), "cannot restart windup");
        clock.Tick(BramblekinClock.WindupSeconds - .01f); Check(clock.Phase == BramblekinPhase.Windup, "full warning");
        clock.Tick(10); Check(clock.Phase == BramblekinPhase.Swing && clock.Age == 0, "hitch preserves swing");
        clock.Tick(10); Check(clock.Phase == BramblekinPhase.Recover && clock.Age == 0, "hitch preserves recovery");
        Check(!clock.TryBegin(1), "cannot attack during recovery");
        clock.Tick(BramblekinClock.RecoverSeconds - .01f); Check(clock.Phase == BramblekinPhase.Recover, "full punish window");
        clock.Tick(.02f); Check(clock.Phase == BramblekinPhase.Hunt, "resume hunt");
        clock.TryBegin(1); clock.Interrupt(); Check(clock.Phase == BramblekinPhase.Recover, "stagger cancels swing");
        clock.Kill(); clock.Tick(100); Check(clock.Phase == BramblekinPhase.Dead && !clock.TryBegin(1), "corpse cannot attack");
        clock.Reset(); Check(clock.Phase == BramblekinPhase.Hunt && clock.Age == 0, "pool reuse");
        clock.Tick(float.NaN); clock.Tick(float.PositiveInfinity); clock.Tick(-1);
        Check(clock.Age == 0, "invalid elapsed time");
        Check(Math.Abs(BramblekinClock.WindupSeconds + BramblekinClock.SwingSeconds + BramblekinClock.RecoverSeconds - .74f) < .0001f, "twice attack cadence");
        int swings = 0; clock.Changed += p => { if (p == BramblekinPhase.Swing) swings++; };
        for (int n = 0; n < 100; n++)
        { clock.TryBegin(1); clock.Tick(.56f); clock.Tick(.2f); clock.Tick(.76f); }
        Check(swings == 100, "exactly one hit event per attack");
        Console.WriteLine($"Bramblekin: {checks} attack clock checks passed.");
    }
}
