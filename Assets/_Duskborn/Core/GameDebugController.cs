using UnityEngine;
using Duskborn.Gameplay.Enemies;

namespace Duskborn.Core
{
    /// <summary>
    /// Editor/testing shortcuts. Remove or strip from shipping build.
    /// F1 — skip day (force night to start immediately)
    /// F2 — force end current night (all enemies despawn, next day begins)
    /// F3 — deal 25 damage to all players (test death / game-over)
    /// F4 — print active spawn timeline to console
    /// </summary>
    public class GameDebugController : MonoBehaviour
    {
        [SerializeField] private WaveManager waveManager;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1)) SkipDay();
            if (Input.GetKeyDown(KeyCode.F2)) ForceEndNight();
            if (Input.GetKeyDown(KeyCode.F3)) DamageAllPlayers(25f);
            if (Input.GetKeyDown(KeyCode.F4)) PrintTimeline();
        }

        private void SkipDay()
        {
            var cycle = DayNightCycle.Instance;
            if (cycle == null || cycle.Phase != DayPhase.Day)
            {
                DuskLog.Log(LogChannel.DebugController, "Not in day phase.");
                return;
            }
            DuskLog.Log(LogChannel.DebugController, "F1 — Skipping day.");
            cycle.ForceEndCurrentPhase();
        }

        private void ForceEndNight()
        {
            var cycle = DayNightCycle.Instance;
            if (cycle == null || cycle.Phase != DayPhase.Night)
            {
                DuskLog.Log(LogChannel.DebugController, "Not in night phase.");
                return;
            }
            DuskLog.Log(LogChannel.DebugController, "F2 — Forcing night end.");
            cycle.ForceEndNight();
        }

        private void DamageAllPlayers(float amount)
        {
            DuskLog.Log(LogChannel.DebugController, $"F3 — Dealing {amount} damage to all players.");
            foreach (var p in Duskborn.Gameplay.Player.PlayerRegistry.All)
                p.TakeDamage(amount);
        }

        private void PrintTimeline()
        {
            if (waveManager == null) { DuskLog.Log(LogChannel.DebugController, "No WaveManager assigned."); return; }
            var tl = waveManager.ActiveTimeline;
            if (tl == null) { DuskLog.Log(LogChannel.DebugController, "No active timeline."); return; }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"{tl}");
            foreach (var evt in tl.Events)
                sb.AppendLine($"  {evt}");
            DuskLog.Log(LogChannel.DebugController, sb.ToString());
        }
    }
}
