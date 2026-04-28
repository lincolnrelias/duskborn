using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Duskborn.Core
{
    /// <summary>
    /// Simple scene loader for singleplayer flow.
    /// Replaced/extended by FishNet SceneManager calls in Phase 9 for network-aware loading.
    /// </summary>
    public static class SceneLoader
    {
        public const string Bootstrap  = "Bootstrap";
        public const string MainMenu   = "MainMenu";
        public const string Lobby      = "Lobby";
        public const string Game       = "Game";

        public static void LoadMainMenu()  => SceneManager.LoadScene(MainMenu);
        public static void LoadLobby()    => SceneManager.LoadScene(Lobby);
        public static void LoadGame()     => SceneManager.LoadScene(Game);

        public static IEnumerator LoadGameAsync(System.Action onComplete = null)
        {
            AsyncOperation op = SceneManager.LoadSceneAsync(Game);
            while (!op.isDone) yield return null;
            onComplete?.Invoke();
        }
    }
}
