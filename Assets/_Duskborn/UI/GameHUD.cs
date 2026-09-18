using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Duskborn.Core;
using Duskborn.Gameplay.Enemies;
using Duskborn.Gameplay.Equipment;
using Duskborn.Gameplay.Loot;
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

        private PlayerBuffContainer      _inventory;
        private ResourceInventory        _resources;
        private PlayerStats              _stats;
        private PlayerEquipmentContainer _equipment;

        private GUIStyle _boxStyle;
        private GUIStyle _labelStyle;
        private bool     _stylesReady;
        private bool     _showStats;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.C))
                _showStats = !_showStats;
        }

        // Deferred — players may not be spawned yet at Start.
        private void TryCacheLocalPlayer()
        {
            if (_inventory != null && _resources != null && _stats != null) return;
            foreach (var combat in FindObjectsByType<PlayerCombat>(FindObjectsSortMode.None))
            {
                if (!combat.IsOwner) continue;
                _inventory  ??= combat.GetComponent<PlayerBuffContainer>();
                _resources  ??= combat.GetComponent<ResourceInventory>();
                _stats      ??= combat.GetComponent<PlayerStats>();
                _equipment  ??= combat.GetComponent<PlayerEquipmentContainer>();
                break;
            }
        }

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

        private const float RefHeight = 1080f;

        private void OnGUI()
        {
            TryCacheLocalPlayer();
            if (!_stylesReady) BuildStyles();

            Matrix4x4 origMatrix = GUI.matrix;
            float scale = Screen.height / RefHeight;
            if (scale <= 0.001f) scale = 1f;
            float virtualW = Screen.width / scale;
            float virtualH = RefHeight;

            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

            DrawMainHUD();

            if (_showStats && _stats != null)
                DrawStatPanel(virtualW, virtualH);

            GUI.matrix = origMatrix;
        }

        private void DrawMainHUD()
        {
            var cycle = DayNightCycle.Instance;
            var state = GameStateManager.Instance;

            string period  = cycle  != null ? $"{cycle.PeriodDisplayName} [{cycle.ClockTimeString}]" : "—";
            string night   = cycle  != null ? cycle.CurrentNight.ToString()                         : "—";
            string timer   = cycle  != null 
                ? (cycle.IsDay ? $"{cycle.PhaseTimeRemaining:F0}s até Noite" : $"{cycle.PhaseTimeRemaining:F0}s até Amanhecer")
                : "—";
            string gstate  = state  != null ? state.CurrentState.ToString()                         : "—";
            int    alive   = waveManager != null ? waveManager.AliveEnemyCount                      : 0;
            int    pending = waveManager != null ? waveManager.RemainingEvents                     : 0;
            int    gold    = GoldManager.Instance != null ? GoldManager.Instance.Gold             : 0;

            var players = PlayerRegistry.All;
            var sb = new StringBuilder();
            sb.AppendLine($"Fase:    {period} (Noite {night})");
            sb.AppendLine($"Tempo:   {timer}");
            if (cycle != null && cycle.IsDusk)
            {
                sb.AppendLine(">> AVISO: Crepúsculo! Retorne à base! <<");
            }
            sb.AppendLine($"Estado:  {gstate}");
            sb.AppendLine($"Ouro:    {gold}");
            sb.AppendLine($"Buffs:   {(_inventory != null ? _inventory.Buffs.Count : 0)}");
            if (_resources != null)
            {
                foreach (KeyValuePair<string, int> kv in _resources.Counts)
                    if (kv.Value > 0) sb.AppendLine($"{kv.Key}: {kv.Value}");
            }
            sb.AppendLine($"Inimigos: {alive} vivos  |  {pending} na fila");
            sb.AppendLine("─────────────────");
            if (players.Count == 0)
                sb.AppendLine("Nenhum jogador registrado");
            else
                for (int i = 0; i < players.Count; i++)
                    sb.AppendLine($"P{i + 1} HP: {players[i].CurrentHP:F0} / {players[i].MaxHP:F0}");

            sb.AppendLine("─────────────────");
            sb.AppendLine("F1 Pular dia  F2 Encerrar noite");
            sb.AppendLine("F3 Dano       F4 Linha do tempo");
            sb.AppendLine("C  Alternar estatísticas");

            float w = 270f, h = (cycle != null && cycle.IsDusk) ? 252f : 234f;
            GUI.Box(new Rect(10, 10, w, h), GUIContent.none, _boxStyle);
            GUI.Label(new Rect(18, 14, w - 8, h - 4), sb.ToString(), _labelStyle);
        }

        private void DrawStatPanel(float virtualW, float virtualH)
        {
            var sb = new StringBuilder();
            sb.AppendLine("── Player Stats ─────────");
            sb.AppendLine($"HP:          {_stats.CurrentHP:F0} / {_stats.MaxHP:F0}");
            sb.AppendLine($"Damage:      {_stats.Damage:F1}");
            sb.AppendLine($"Move Speed:  {_stats.MoveSpeed:F2}");
            sb.AppendLine($"Atk Speed:   {_stats.AttackSpeed:F2}");
            sb.AppendLine($"Crit:        {_stats.CritChance * 100f:F0}%");
            sb.AppendLine($"Dmg Reduc:   {(1f - _stats.EffectiveIncomingDamage) * 100f:F0}%");
            sb.AppendLine("─────────────────────────");
            sb.AppendLine("Equipped");

            var slots = System.Enum.GetValues(typeof(EquipmentSlot));
            foreach (EquipmentSlot slot in slots)
            {
                var gear = _equipment != null ? _equipment.GetEquipped(slot) : null;
                string slotName  = FormatSlotName(slot);
                string itemLabel = gear != null ? gear.DisplayName : "—";
                sb.AppendLine($"{slotName,-10} {itemLabel}");
            }

            const float w = 240f, h = 400f;
            float x = virtualW - w - 10f;
            float y = virtualH - h - 10f;

            GUI.Box(new Rect(x, y, w, h), GUIContent.none, _boxStyle);
            GUI.Label(new Rect(x + 8f, y + 4f, w - 16f, h - 8f), sb.ToString(), _labelStyle);
        }

        private static string FormatSlotName(EquipmentSlot slot) => slot switch
        {
            EquipmentSlot.Ring1 => "Ring 1",
            EquipmentSlot.Ring2 => "Ring 2",
            _                   => slot.ToString(),
        };

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
