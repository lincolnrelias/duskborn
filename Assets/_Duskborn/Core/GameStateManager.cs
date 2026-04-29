using System;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Duskborn.Core
{
    public enum GameState { None, Running, GameOver, Win }

    public class GameStateManager : NetworkBehaviour
    {
        public static GameStateManager Instance { get; private set; }

        private readonly SyncVar<GameState> _stateSync          = new();
        private readonly SyncVar<int>       _nightsSurvivedSync = new();

        public GameState CurrentState   => _stateSync.Value;
        public int       NightsSurvived => _nightsSurvivedSync.Value;

        public event Action<GameState> OnStateChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _stateSync.OnChange += (prev, next, asServer) => OnStateChanged?.Invoke(next);
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            if (GameSession.Instance != null && GameSession.Instance.RNG == null)
                GameSession.Instance.GenerateAndApplySeed();
            StartRun();
        }

        public void StartRun()
        {
            if (!IsServerStarted) return;
            _nightsSurvivedSync.Value = 0;
            SetState(GameState.Running);
        }

        public void RegisterNightSurvived()
        {
            if (!IsServerStarted) return;
            _nightsSurvivedSync.Value += 1;
        }

        public void TriggerGameOver()
        {
            if (!IsServerStarted) return;
            if (CurrentState != GameState.Running) return;
            SetState(GameState.GameOver);
            Debug.Log($"[GameStateManager] Game Over — survived {NightsSurvived} nights.");
        }

        public void TriggerWin()
        {
            if (!IsServerStarted) return;
            if (CurrentState != GameState.Running) return;
            SetState(GameState.Win);
            Debug.Log("[GameStateManager] Run complete — boss defeated!");
        }

        private void SetState(GameState state) => _stateSync.Value = state;
    }
}
