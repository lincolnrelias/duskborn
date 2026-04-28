using UnityEngine;
using Duskborn.Gameplay.Loot;

namespace Duskborn.Core
{
    /// <summary>
    /// Placed once in the Game scene. Starts the day/night cycle and run state.
    /// PlayerRegistry does not need clearing here — PlayerStats.OnEnable/OnDisable
    /// handle registration, and calling Clear() in Start() would wipe players that
    /// already registered during OnEnable (which runs before Start).
    /// </summary>
    public class GameBootstrapper : MonoBehaviour
    {
        private void Start()
        {
            // Ensure a seed exists (host will have set one via network in Phase 9).
            if (GameSession.Instance != null && GameSession.Instance.RNG == null)
                GameSession.Instance.GenerateAndApplySeed();

            GoldManager.Instance?.ResetGold();
            GameStateManager.Instance?.StartRun();
            DayNightCycle.Instance?.StartCycle();
        }
    }
}
