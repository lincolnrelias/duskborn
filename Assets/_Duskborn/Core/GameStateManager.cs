using System;
using UnityEngine;

namespace Duskborn.Core
{
    public enum GameState
    {
        None,
        Running,
        GameOver,
        Win
    }

    /// <summary>
    /// Tracks overall run state. Other systems subscribe to state change events.
    /// Host-authoritative; clients observe replicated state (hooked up in Phase 9).
    /// </summary>
    public class GameStateManager : MonoBehaviour
    {
        public static GameStateManager Instance { get; private set; }

        public GameState CurrentState { get; private set; } = GameState.None;
        public int NightsSurvived { get; private set; }

        public event Action<GameState> OnStateChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void StartRun()
        {
            NightsSurvived = 0;
            SetState(GameState.Running);
        }

        public void RegisterNightSurvived() => NightsSurvived++;

        public void TriggerGameOver()
        {
            if (CurrentState != GameState.Running) return;
            SetState(GameState.GameOver);
            Debug.Log($"[GameStateManager] Game Over — survived {NightsSurvived} nights.");
        }

        public void TriggerWin()
        {
            if (CurrentState != GameState.Running) return;
            SetState(GameState.Win);
            Debug.Log("[GameStateManager] Run complete — boss defeated!");
        }

        private void SetState(GameState state)
        {
            CurrentState = state;
            OnStateChanged?.Invoke(state);
        }
    }
}
