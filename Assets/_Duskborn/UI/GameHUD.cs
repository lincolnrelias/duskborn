using UnityEngine;
using Duskborn.Core;
using Duskborn.Gameplay.Enemies;
using Duskborn.Gameplay.Player;

namespace Duskborn.UI
{
    /// <summary>
    /// Minimal debug HUD drawn via OnGUI. No Canvas or prefabs required.
    /// Replace with proper pixel-art UI in Phase 10.
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        [SerializeField] private WaveManager waveManager;

        private GUIStyle _boxStyle;
        private GUIStyle _labelStyle;
        private bool _stylesReady;

        private void BuildStyles()
        {
            _boxStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize  = 14,
                alignment = TextAnchor.UpperLeft,
                padding   = new RectOffset(8, 8, 6, 6),
            };
            _boxStyle.normal.background = MakeTex(2, 2, new Color(0f, 0f, 0f, 0.55f));

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 14,
                alignment = TextAnchor.UpperLeft,
            };
            _labelStyle.normal.textColor = Color.white;

            _stylesReady = true;
        }

        private void OnGUI()
        {
            if (!_stylesReady) BuildStyles();

            var cycle = DayNightCycle.Instance;
            var state = GameStateManager.Instance;

            string phase   = cycle  != null ? cycle.Phase.ToString()                : "—";
            string night   = cycle  != null ? cycle.CurrentNight.ToString()         : "—";
            string timer   = cycle  != null ? $"{cycle.PhaseTimeRemaining:F0}s"     : "—";
            string gstate  = state  != null ? state.CurrentState.ToString()         : "—";
            int    alive   = waveManager != null ? waveManager.AliveEnemyCount      : 0;
            int    pending = waveManager != null ? waveManager.RemainingEvents       : 0;

            // Per-player HP
            var players = PlayerRegistry.All;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Phase:   {phase}  (Night {night})");
            sb.AppendLine($"Timer:   {timer}");
            sb.AppendLine($"State:   {gstate}");
            sb.AppendLine($"Enemies: {alive} alive  |  {pending} queued");
            sb.AppendLine("─────────────────");
            if (players.Count == 0)
                sb.AppendLine("No players registered");
            else
                for (int i = 0; i < players.Count; i++)
                    sb.AppendLine($"P{i + 1} HP: {players[i].CurrentHP:F0} / {players[i].MaxHP:F0}");

            sb.AppendLine("─────────────────");
            sb.AppendLine("F1 Skip day  F2 End night");
            sb.AppendLine("F3 Damage    F4 Print timeline");

            float w = 260f, h = 200f;
            GUI.Box(new Rect(10, 10, w, h), GUIContent.none, _boxStyle);
            GUI.Label(new Rect(18, 14, w - 8, h - 4), sb.ToString(), _labelStyle);
        }

        private static Texture2D MakeTex(int width, int height, Color col)
        {
            var pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = col;
            var tex = new Texture2D(width, height);
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }
    }
}
