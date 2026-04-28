using UnityEngine;
using Duskborn.Gameplay.Player;

namespace Duskborn.Core
{
    /// <summary>
    /// Placed once in the Game scene. Starts the day/night cycle and run state.
    /// Also ensures GameSession has a seed (fallback for singleplayer / editor testing).
    /// In Phase 9 this hands off to the network manager to wait for all clients ready.
    /// </summary>
    public class GameBootstrapper : MonoBehaviour
    {
        private void Start()
        {
            PlayerRegistry.Clear();

            // Ensure a seed exists (host will have set one via network in Phase 9).
            if (GameSession.Instance != null && GameSession.Instance.RNG == null)
                GameSession.Instance.GenerateAndApplySeed();

            GameStateManager.Instance?.StartRun();
            DayNightCycle.Instance?.StartCycle();
        }
    }
}
