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
                Debug.Log("[Debug] Not in day phase.");
                return;
            }
            Debug.Log("[Debug] F1 — Skipping day.");
            cycle.ForceEndCurrentPhase();
        }

        private void ForceEndNight()
        {
            var cycle = DayNightCycle.Instance;
            if (cycle == null || cycle.Phase != DayPhase.Night)
            {
                Debug.Log("[Debug] Not in night phase.");
                return;
            }
            Debug.Log("[Debug] F2 — Forcing night end.");
            cycle.ForceEndNight();
        }

        private void DamageAllPlayers(float amount)
        {
            Debug.Log($"[Debug] F3 — Dealing {amount} damage to all players.");
            foreach (var p in Duskborn.Gameplay.Player.PlayerRegistry.All)
                p.TakeDamage(amount);
        }

        private void PrintTimeline()
        {
            if (waveManager == null) { Debug.Log("[Debug] No WaveManager assigned."); return; }
            var tl = waveManager.ActiveTimeline;
            if (tl == null) { Debug.Log("[Debug] No active timeline."); return; }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[Debug] {tl}");
            foreach (var evt in tl.Events)
                sb.AppendLine($"  {evt}");
            Debug.Log(sb.ToString());
        }
    }
}
