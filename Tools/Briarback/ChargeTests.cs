using System;
using Duskborn.Gameplay.Enemies;

static class ChargeTests
{
    static int checks;
    static void Check(bool condition, string message)
    { checks++; if (!condition) throw new Exception(message); }
    static void Main()
    {
        var clock = new BriarbackCharge();
        foreach (float step in new[] { 1f/120, 1f/60, 1f/20, .2f, 2f })
        {
            clock.Reset(); Check(!clock.TryBegin(), "No spawn grace"); clock.Tick(1);
            Check(clock.TryBegin(), "Attack did not start");
            Check(!clock.TryBegin(), "Attack restart");
            float windup = 0;
            while (clock.Phase == BriarbackPhase.Windup) { clock.Tick(step); windup += step; }
            Check(windup >= BriarbackCharge.WindupSeconds, "Windup skipped");
            Check(clock.Phase == BriarbackPhase.Charge && clock.Age == 0, "Windup overshoot caused instant hit");
            float charge = 0;
            while (clock.Phase == BriarbackPhase.Charge) { clock.Tick(step); charge += step; }
            Check(charge >= BriarbackCharge.ChargeSeconds, "Early recovery");
            Check(clock.Phase == BriarbackPhase.Recover && clock.Age == 0, "Recovery skipped");
            Check(!clock.TryBegin(), "Attack during recovery");
            clock.Tick(-10); Check(clock.Age == 0, "Negative time");
            clock.Recover(); Check(clock.Phase == BriarbackPhase.Recover, "Obstacle must stop charge");
            clock.Tick(BriarbackCharge.RecoverSeconds); Check(clock.Phase == BriarbackPhase.Hunt, "No hunt after recover");
            Check(!clock.TryBegin(), "No cooldown after recover");
            clock.Tick(BriarbackCharge.CooldownSeconds); Check(clock.TryBegin(), "Cooldown did not expire");
            clock.Kill(); clock.Tick(100); clock.Recover();
            Check(clock.Phase == BriarbackPhase.Dead && !clock.TryBegin(), "Dead enemy can attack");
            clock.Reset(); Check(clock.Phase == BriarbackPhase.Hunt && clock.Cooldown == 1, "Pool retained old state");
        }
        Check(BriarbackCharge.ChargeSpeed * BriarbackCharge.ChargeSeconds == BriarbackCharge.ChargeLength, "Warning length mismatch");
        Check(BriarbackCharge.SelectAttack(0) == BriarbackAttack.Headbutt, "Point-blank attack choice");
        Check(BriarbackCharge.SelectAttack(2.2f) == BriarbackAttack.Headbutt, "Close boundary must prefer headbutt");
        Check(BriarbackCharge.SelectAttack(2.201f) == BriarbackAttack.Charge, "Distant target must prefer charge");
        Check(BriarbackCharge.SelectAttack(7) == BriarbackAttack.Charge, "Charge boundary");
        Check(BriarbackCharge.SelectAttack(7.01f) == BriarbackAttack.None, "Out-of-range attack");
        Check(BriarbackCharge.SelectAttack(1, false) == BriarbackAttack.None, "Unreachable target");
        Check(BriarbackCharge.SelectAttack(float.NaN) == BriarbackAttack.None, "Invalid target distance");
        foreach (float step in new[] { 1f/120, 1f/60, .2f, 2f })
        {
            clock.Reset(); Check(!clock.TryBegin(1), "Headbutt skipped spawn grace"); clock.Tick(1);
            Check(clock.TryBegin(1) && clock.Phase == BriarbackPhase.HeadbuttWindup, "Close attack not prioritized");
            Check(!clock.TryBegin(6), "Charge interrupted headbutt");
            float elapsed = 0;
            while (clock.Phase == BriarbackPhase.HeadbuttWindup) { clock.Tick(step); elapsed += step; }
            Check(elapsed >= BriarbackCharge.HeadbuttWindupSeconds && clock.Phase == BriarbackPhase.Headbutt && clock.Age == 0,
                "Headbutt warning or active window skipped");
            while (clock.Phase == BriarbackPhase.Headbutt) clock.Tick(step);
            Check(clock.Phase == BriarbackPhase.HeadbuttRecover && clock.Age == 0, "Headbutt recovery skipped");
            Check(!clock.TryBegin(1), "Headbutt during recovery");
            clock.Tick(BriarbackCharge.HeadbuttRecoverSeconds);
            Check(!clock.TryBegin(1) && clock.Phase == BriarbackPhase.Hunt, "Close cooldown incorrectly fell back to charge");
            clock.Tick(BriarbackCharge.HeadbuttCooldownSeconds); Check(clock.TryBegin(1), "Headbutt cooldown stuck");
            clock.Kill(); clock.Tick(100); Check(!clock.TryBegin(1) && clock.Phase == BriarbackPhase.Dead, "Dead headbutt");
            clock.Reset(); Check(clock.HeadbuttCooldown == 1 && clock.Phase == BriarbackPhase.Hunt, "Pooled headbutt state retained");
        }
        clock.Reset(); clock.Tick(1); clock.TryBegin(); clock.Recover(); clock.Tick(BriarbackCharge.RecoverSeconds);
        Check(clock.Cooldown > 0 && clock.TryBegin(1), "Charge cooldown must not block close defense");
        clock.Reset(); clock.Tick(1); clock.TryBegin(1); clock.Tick(100); clock.Tick(100); clock.Tick(.7f);
        Check(clock.HeadbuttCooldown > 0 && clock.TryBegin(6), "Headbutt cooldown must not block a distant charge");
        Console.WriteLine($"Briarback attack clock: {checks} checks passed (120-0.5 FPS, hitch, cooldown, obstacle, death, pool reuse).");
    }
}
