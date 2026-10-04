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
        public static GameHUD Instance { get; private set; }

        public bool ShowStats
        {
            get => _showStats;
            set => _showStats = value;
        }

        public void CloseStats()
        {
            _showStats = false;
        }

        private static readonly StringBuilder s_mainHudSb = new(512);
        private static readonly StringBuilder s_statPanelSb = new(512);
        private static readonly EquipmentSlot[] s_equipmentSlots = (EquipmentSlot[])System.Enum.GetValues(typeof(EquipmentSlot));

        private void Awake()
        {
            Instance = this;
        }

        private void OnEnable()
        {
            if (Application.isPlaying) WorldMapUI.Ensure();
            LocalPlayerContext.OnLocalPlayerRegistered += HandleLocalPlayerRegistered;
            LocalPlayerContext.OnLocalPlayerUnregistered += HandleLocalPlayerUnregistered;
            TryCacheLocalPlayer();
        }

        private void OnDisable()
        {
            LocalPlayerContext.OnLocalPlayerRegistered -= HandleLocalPlayerRegistered;
            LocalPlayerContext.OnLocalPlayerUnregistered -= HandleLocalPlayerUnregistered;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void HandleLocalPlayerRegistered()
        {
            TryCacheLocalPlayer();
        }

        private void HandleLocalPlayerUnregistered()
        {
            _stats = null;
            _resources = null;
            _inventory = null;
            _equipment = null;
        }

        private void Update()
        {
            if (Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.C))
                _showStats = !_showStats;
        }

        private void TryCacheLocalPlayer()
        {
            if (_inventory != null && _resources != null && _stats != null && _equipment != null) return;
            if (LocalPlayerContext.HasLocalPlayer)
            {
                _inventory ??= LocalPlayerContext.Buffs;
                _resources ??= LocalPlayerContext.Resources;
                _stats     ??= LocalPlayerContext.Stats;
                _equipment ??= LocalPlayerContext.Equipment;
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
                ? (cycle.IsDay ? $"{cycle.PhaseTimeRemaining:F0}s until Night" : $"{cycle.PhaseTimeRemaining:F0}s until Dawn")
                : "—";
            string gstate  = state  != null ? state.CurrentState.ToString()                         : "—";
            int    alive   = waveManager != null ? waveManager.AliveEnemyCount                      : 0;
            int    pending = waveManager != null ? waveManager.RemainingEvents                     : 0;
            int    gold    = GoldManager.Instance != null ? GoldManager.Instance.Gold             : 0;

            var players = PlayerRegistry.All;
            s_mainHudSb.Clear();
            s_mainHudSb.AppendLine($"Phase:   {period} (Night {night})");
            s_mainHudSb.AppendLine($"Time:    {timer}");
            if (cycle != null && cycle.IsDusk)
            {
                s_mainHudSb.AppendLine(">> WARNING: Dusk! Return to base! <<");
            }
            s_mainHudSb.AppendLine($"State:  {gstate}");
            s_mainHudSb.AppendLine($"Gold:    {gold}");
            s_mainHudSb.AppendLine($"Buffs:   {(_inventory != null ? _inventory.Buffs.Count : 0)}");
            if (_resources != null)
            {
                foreach (KeyValuePair<string, int> kv in _resources.Counts)
                    if (kv.Value > 0) s_mainHudSb.AppendLine($"{kv.Key}: {kv.Value}");
            }
            s_mainHudSb.AppendLine($"Enemies: {alive} alive  |  {pending} queued");
            s_mainHudSb.AppendLine("─────────────────");
            if (players.Count == 0)
                s_mainHudSb.AppendLine("No player registered");
            else
                for (int i = 0; i < players.Count; i++)
                    s_mainHudSb.AppendLine($"P{i + 1} HP: {players[i].CurrentHP:F0} / {players[i].MaxHP:F0}");

            s_mainHudSb.AppendLine("─────────────────");
            s_mainHudSb.AppendLine("F1 Skip day   F2 End night");
            s_mainHudSb.AppendLine("F3 Damage     F4 Timeline");
            s_mainHudSb.AppendLine("C  Character Panel");

            float w = 270f, h = (cycle != null && cycle.IsDusk) ? 252f : 234f;
            GUI.Box(new Rect(10, 10, w, h), GUIContent.none, _boxStyle);
            GUI.Label(new Rect(18, 14, w - 8, h - 4), s_mainHudSb.ToString(), _labelStyle);
        }

        private void DrawStatPanel(float virtualW, float virtualH)
        {
            s_statPanelSb.Clear();
            s_statPanelSb.AppendLine("── Player Stats ─────────");
            s_statPanelSb.AppendLine($"HP:          {_stats.CurrentHP:F0} / {_stats.MaxHP:F0}");
            s_statPanelSb.AppendLine($"Damage:      {_stats.Damage:F1}");
            s_statPanelSb.AppendLine($"Move Speed:  {_stats.MoveSpeed:F2}");
            s_statPanelSb.AppendLine($"Atk Speed:   {_stats.AttackSpeed:F2}");
            s_statPanelSb.AppendLine($"Crit:        {_stats.CritChance * 100f:F0}%");
            s_statPanelSb.AppendLine($"Dmg Reduc:   {(1f - _stats.EffectiveIncomingDamage) * 100f:F0}%");
            s_statPanelSb.AppendLine("─────────────────────────");
            s_statPanelSb.AppendLine("Equipped");

            for (int i = 0; i < s_equipmentSlots.Length; i++)
            {
                EquipmentSlot slot = s_equipmentSlots[i];
                var gear = _equipment != null ? _equipment.GetEquipped(slot) : null;
                string slotName  = FormatSlotName(slot);
                string itemLabel = gear != null ? gear.DisplayName : "—";
                s_statPanelSb.AppendLine($"{slotName,-10} {itemLabel}");
            }

            const float w = 240f, h = 400f;
            float x = virtualW - w - 10f;
            float y = virtualH - h - 10f;

            GUI.Box(new Rect(x, y, w, h), GUIContent.none, _boxStyle);
            GUI.Label(new Rect(x + 8f, y + 4f, w - 16f, h - 8f), s_statPanelSb.ToString(), _labelStyle);
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
