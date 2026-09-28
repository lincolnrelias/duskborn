using System;
using System.Collections.Generic;
using Duskborn.Gameplay.Enemies;

static class EncounterTests
{
    static int assertions;
    static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new Exception(message);
    }
    static HollowWardenEncounter Ready()
    {
        var e = new HollowWardenEncounter(); e.Start(); e.Tick(2);
        Check(e.State == WardenState.Ready, "Spawn must finish.");
        return e;
    }
    static void Barrage(HollowWardenEncounter e)
    {
        for (int i = 0; i < 3; i++) { Check(e.TryBeginAttack(false), "Attack refused."); e.Tick(3); }
        e.TryBeginAttack(false); e.Tick(1.4);
        Check(e.State == WardenState.Rooted, "Expected spike cast after three attacks.");
    }
    static void Main()
    {
        Check(HollowWardenNightRules.DefinitionIndex(3, 1) == 0, "SampleScene must spawn normal mobs on night three using its configured pool.");
        Check(HollowWardenNightRules.DefinitionIndex(3, 3) == 2, "Explicit night-three definition takes precedence.");
        Check(HollowWardenNightRules.DefinitionIndex(3, 0) == -1, "Empty wave setup must not index an absent definition.");
        Check(HollowWardenNightRules.DefinitionIndex(7, 1) == -1, "Do not invent night-seven waves.");
        Check(HollowWardenNightRules.AdvanceHeldClock(120, 120, 20) == 100, "Early boss fight must preserve normal clock progression.");
        Check(HollowWardenNightRules.AdvanceHeldClock(55, 120, 2) == 54, "Held night must stop at midnight floor.");
        Check(HollowWardenNightRules.AdvanceHeldClock(54, 120, 600) == 54, "Long boss fight must leave finite remaining night after defeat.");
        Check(!HollowWardenNightRules.RepeatWaves(3, true, 119, 120), "Do not repeat waves before normal budget ends.");
        Check(HollowWardenNightRules.RepeatWaves(3, true, 120, 120), "Extended boss night must replenish normal waves.");
        Check(!HollowWardenNightRules.RepeatWaves(3, false, 150, 120), "Dawn must stop extended waves.");
        Check(!HollowWardenNightRules.RepeatWaves(7, true, 150, 120), "Night seven remains independent.");
        var e = Ready();
        var events = new List<WardenSignal>();
        e.Signal += events.Add;
        e.TryBeginAttack(false); events.Clear(); e.Tick(1.1);
        Check(events.Count == 2 && events[0] == WardenSignal.FacingLocked && events[1] == WardenSignal.Impact,
            "Skipped frames must preserve lock-before-hit order.");
        e.Tick(.2); Check(events.Count == 2, "Impact repeated.");
        e.ObserveHealth(.4f);
        Check(e.State == WardenState.Rootbreaker && !e.PhaseTwo, "Phase interrupted committed attack.");
        e.Tick(1.1);
        Check(e.State == WardenState.PhaseBreak, "Queued phase transition missing.");
        e.Tick(2); e.ObserveHealth(.3f);
        Check(e.State == WardenState.Ready && e.PhaseTwo, "Phase transition repeated.");
        e.TryBeginAttack(true); events.Clear(); e.Tick(.77);
        Check(!events.Contains(WardenSignal.Impact), "Phase two telegraph too short.");
        e.Tick(.02); Check(events.Contains(WardenSignal.Impact), "Phase two timing mismatch.");
        e.ObserveHealth(0); events.Clear(); e.Tick(1000);
        Check(e.State == WardenState.Dead && events.Count == 0, "Death did not cancel clock.");

        e = Ready();
        int waves = 0, clears = 0;
        e.Signal += s => { if (s == WardenSignal.SpikeWave) waves++; if (s == WardenSignal.ClearSpikes) clears++; };
        Barrage(e);
        Check(waves == 1, "First player-targeted warning must start with cast.");
        Check(e.IncomingDamageScale == 1.5f, "Core must expose at channel start.");
        for (int i = 0; i < 11; i++) e.Tick(.5);
        Check(waves == 12 && e.State == WardenState.Rooted, "Expected twelve spaced waves over six seconds.");
        e.Tick(.5);
        Check(waves == 12, "New warnings continued after casting cutoff.");
        e.Tick(.74);
        Check(e.State == WardenState.Rooted, "Cast ended before final warnings could erupt.");
        e.Tick(.67);
        Check(e.State == WardenState.Recover && clears == 1, "Barrage must recover directly without post-cast exposure.");
        Check(e.IncomingDamageScale == 1, "Vulnerability leaked after channel.");
        e.Tick(1);
        Check(e.State == WardenState.Ready && e.IncomingDamageScale == 1, "Recovery must return to closed-core combat.");
        e = Ready(); waves = 0;
        e.Signal += s => { if (s == WardenSignal.SpikeWave) waves++; };
        Barrage(e); e.Tick(2.2);
        Check(waves == 2, "Hitch backfilled overlapping instant warnings.");
        e.Tick(.1); Check(waves == 2, "Next wave ignored minimum spacing after hitch.");
        e.ObserveHealth(.4f);
        Check(e.State == WardenState.Rooted && !e.PhaseTwo, "Half health interrupted barrage.");
        e.Tick(20);
        Check(e.State == WardenState.Ready && e.PhaseTwo, "Barrage/phase recovery failed after a large delta.");

        e = Ready(); waves = 0;
        e.Signal += s => { if (s == WardenSignal.SpikeWave) waves++; };
        for (int i = 0; i < 3; i++) { e.TryBeginAttack(false); e.Tick(3); }
        e.TryBeginAttack(false); e.Tick(2.1);
        Check(waves == 1, "Hitch crossing plant-to-cast boundary bunched warnings together.");
        e.Tick(.1); Check(waves == 1, "Wave after plant hitch arrived too soon.");

        e = Ready(); Barrage(e); events.Clear(); e.Signal += events.Add;
        e.ObserveHealth(0); e.Tick(100);
        Check(e.State == WardenState.Dead && events.Contains(WardenSignal.ClearSpikes)
            && !events.Contains(WardenSignal.SpikeWave), "Death must cancel pending spikes and waves.");
        e.Reset(); e.Start(); e.Tick(2); Barrage(e);
        Check(e.State == WardenState.Rooted, "Restart did not reset barrage.");
        events.Clear(); e.Reset();
        Check(events.Contains(WardenSignal.ClearSpikes), "Reset leaked warnings.");

        e = Ready(); e.ObserveHealth(.5f); e.Tick(2); Barrage(e);
        Check(e.IncomingDamageScale == 1.5f, "Phase two channel must expose core.");
        e.Tick(HollowWardenEncounter.BarrageSeconds);
        Check(e.State == WardenState.Recover && e.IncomingDamageScale == 1, "Phase two retained post-channel exposure.");
        foreach (WardenState state in Enum.GetValues(typeof(WardenState)))
            Check(HollowWardenEncounter.IsCoreExposed(state) == (state == WardenState.Rooted),
                "Only the channel may expose the core: " + state);
        e = Ready(); Barrage(e); e.ObserveHealth(0);
        Check(e.IncomingDamageScale == 1, "Death retained channel vulnerability.");
        e.Reset(); Check(e.IncomingDamageScale == 1, "Reset retained channel vulnerability.");
        float runDistance = 5f * (float)HollowWardenEncounter.SpikeDelay;
        Check(!HollowWardenEncounter.SpikeOverlaps(runDistance, 0, .5f, 0, 2), "Running player cannot escape a fresh marker.");
        Check(HollowWardenEncounter.SpikeOverlaps(0, 0, .5f, 0, 2), "Stationary player avoids centered spike.");
        Check(HollowWardenEncounter.SpikeOverlaps(2.8f, 0, .5f, 0, 2), "Player body edge ignored.");
        Check(!HollowWardenEncounter.SpikeOverlaps(3, 0, .5f, 0, 2), "Warning radius and hit radius disagree.");
        Check(!HollowWardenEncounter.SpikeOverlaps(0, 0, .5f, 3, 5), "Spike hit player on another elevation.");
        Check(!HollowWardenEncounter.SpikeOverlaps(0, 0, .5f, -4, -2), "Spike hit player below the ground.");
        e = Ready(); Barrage(e); e.Tick(100000);
        Check(e.State == WardenState.Ready, "Large delta did not terminate in Ready.");
        bool invalidRejected = false;
        try { e.Tick(double.NaN); } catch (ArgumentOutOfRangeException) { invalidRejected = true; }
        Check(invalidRejected, "Non-finite time accepted.");
        Console.WriteLine($"PASS: {assertions} encounter assertions (pure C#, no Unity runtime).");
    }
}

