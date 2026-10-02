using System;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using Duskborn.Audio;
using Duskborn.Core;
using Duskborn.Gameplay.Player;

namespace Duskborn.UI
{
    /// <summary>
    /// In-game pause and options menu with a Medieval Fantasy Low-Poly aesthetic ("Expedition Pause").
    /// Features wrought iron frames, aged gold trim, stone slab buttons with ember glow,
    /// floating mystical embers, full audio / graphics / controls settings modals, and a knowledge scroll.
    /// Works entirely through OnGUI with procedural textures, independent of prefabs or Canvas.
    /// </summary>
    public class InGameMenuController : MonoBehaviour
    {
        public static InGameMenuController Instance { get; private set; }

        [Header("Audio and Effects")]
        [Tooltip("Sound effect when clicking buttons.")]
        public AudioClip clickSfx;
        [Tooltip("Sound effect when opening a scroll / modal.")]
        public AudioClip modalOpenSfx;

        // Menu state.
        public bool IsOpen { get; private set; } = false;

        private bool _showSettingsModal = false;
        private bool _showLoreModal = false;
        private bool _showConfirmMainMenu = false;
        private bool _showConfirmQuit = false;

        // Settings values being edited.
        private float _masterVolume;
        private float _musicVolume;
        private float _sfxVolume;
        private float _ambienceVolume;
        private float _uiVolume;
        private bool  _isFullScreen;
        private float _mouseSensitivity;
        private bool  _invertPitch;

        // Mystical twilight embers.
        private struct DuskEmber
        {
            public float xRatio;
            public float yRatio;
            public float speed;
            public float size;
            public float seed;
        }
        private DuskEmber[] _embers;

        // Procedural textures generated dynamically
        private Texture2D _backdropTexture;
        private Texture2D _slateTexture;
        private Texture2D _ironFrameTexture;
        private Texture2D _goldTrimTexture;
        private Texture2D _btnNormalTex;
        private Texture2D _btnHoverTex;
        private Texture2D _btnActiveTex;
        private Texture2D _btnDangerNormalTex;
        private Texture2D _btnDangerHoverTex;
        private Texture2D _parchmentModalTex;
        private Texture2D _parchmentInnerTex;
        private Texture2D _emberTexture;
        private Texture2D _sliderTroughTex;
        private Texture2D _sliderFillTex;

        // GUI Styles
        private GUIStyle _titleStyle;
        private GUIStyle _subTitleStyle;
        private GUIStyle _statusStyle;
        private GUIStyle _btnPrimaryStyle;
        private GUIStyle _btnSecondaryStyle;
        private GUIStyle _btnDangerStyle;
        private GUIStyle _modalHeaderStyle;
        private GUIStyle _modalSectionStyle;
        private GUIStyle _modalKeyStyle;
        private GUIStyle _modalValStyle;
        private GUIStyle _modalBodyStyle;
        private bool _stylesInitialized = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            string currentScene = SceneManager.GetActiveScene().name;
            if (currentScene == "MainMenu") return;

            EnsureInstance();
        }

        public static InGameMenuController EnsureInstance()
        {
            if (Instance != null) return Instance;

            Instance = FindAnyObjectByType<InGameMenuController>();
            if (Instance != null) return Instance;

            GameObject go = new GameObject("[InGameMenuController]");
            Instance = go.AddComponent<InGameMenuController>();
            DontDestroyOnLoad(go);
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            SceneManager.sceneLoaded += OnSceneLoaded;
            InitEmbers();
            TryLoadAudio();
            SyncSettingsFromGameSettings();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            CleanupTextures();
            if (IsOpen)
            {
                Time.timeScale = 1f;
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "MainMenu")
            {
                CloseMenu(false);
                gameObject.SetActive(false);
            }
            else
            {
                gameObject.SetActive(true);
                CloseMenu(false);
            }
        }

        private void SyncSettingsFromGameSettings()
        {
            _masterVolume     = GameSettings.MasterVolume;
            _musicVolume      = GameSettings.MusicVolume;
            _sfxVolume        = GameSettings.SfxVolume;
            _ambienceVolume   = GameSettings.AmbienceVolume;
            _uiVolume         = GameSettings.UiVolume;
            _isFullScreen     = GameSettings.FullScreen;
            _mouseSensitivity = GameSettings.MouseSensitivity;
            _invertPitch      = GameSettings.InvertPitch;
        }

        private void TryLoadAudio()
        {
            if (Duskborn.Audio.AudioDatabase.Instance != null)
            {
                if (clickSfx == null) clickSfx = Duskborn.Audio.AudioDatabase.Instance.UI.buttonClickClip;
                if (modalOpenSfx == null) modalOpenSfx = Duskborn.Audio.AudioDatabase.Instance.UI.modalOpenClip;
            }

            if (clickSfx == null) clickSfx = Resources.Load<AudioClip>("SFX/ui_button_click");
            if (modalOpenSfx == null) modalOpenSfx = Resources.Load<AudioClip>("SFX/ui_modal_open");

#if UNITY_EDITOR
            if (clickSfx == null)
                clickSfx = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Duskborn/Art/SFX/ui_button_click.wav");
            if (modalOpenSfx == null)
                modalOpenSfx = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Duskborn/Art/SFX/ui_modal_open.wav");
#endif
        }

        private void PlayClickSound()
        {
            if (clickSfx == null) TryLoadAudio();
            if (clickSfx != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayUISfx(clickSfx);
            }
        }

        private void PlayModalSound()
        {
            if (modalOpenSfx == null) TryLoadAudio();
            if (modalOpenSfx != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayUISfx(modalOpenSfx);
            }
        }

        private void InitEmbers()
        {
            _embers = new DuskEmber[28];
            for (int i = 0; i < _embers.Length; i++)
            {
                _embers[i] = new DuskEmber
                {
                    xRatio = UnityEngine.Random.value,
                    yRatio = UnityEngine.Random.value,
                    speed = UnityEngine.Random.Range(0.04f, 0.11f),
                    size = UnityEngine.Random.Range(3f, 6.5f),
                    seed = UnityEngine.Random.Range(0f, 100f)
                };
            }
        }

        private void Update()
        {
            // Does not operate in the main menu scene.
            if (SceneManager.GetActiveScene().name == "MainMenu") return;

            // Animate embers with unscaledDeltaTime (works even with Time.timeScale = 0).
            if (IsOpen && _embers != null)
            {
                for (int i = 0; i < _embers.Length; i++)
                {
                    _embers[i].yRatio -= _embers[i].speed * Time.unscaledDeltaTime;
                    if (_embers[i].yRatio < -0.05f)
                    {
                        _embers[i].yRatio = 1.05f;
                        _embers[i].xRatio = UnityEngine.Random.value;
                    }
                }
            }

            // If paused and another player joins, immediately unfreeze real time.
            if (IsOpen && Time.timeScale == 0f && !CanPauseGame())
            {
                Time.timeScale = 1f;
            }

            // Capture the Escape key.
            if (IsEscapePressed())
            {
                HandleEscapeKey();
            }
        }

        private bool IsEscapePressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                return true;
#endif
            return Input.GetKeyDown(KeyCode.Escape);
        }

        private void HandleEscapeKey()
        {
            // 1. If the pause menu itself is open: close submodals or close the pause menu.
            if (IsOpen)
            {
                if (_showConfirmMainMenu)
                {
                    _showConfirmMainMenu = false;
                    PlayClickSound();
                }
                else if (_showConfirmQuit)
                {
                    _showConfirmQuit = false;
                    PlayClickSound();
                }
                else if (_showSettingsModal)
                {
                    _showSettingsModal = false;
                    PlayClickSound();
                }
                else if (_showLoreModal)
                {
                    _showLoreModal = false;
                    PlayClickSound();
                }
                else
                {
                    CloseMenu();
                }
                return;
            }

            // 2. If another menu is open (or just closed in this frame through ESC):
            // Close that menu and do NOT open the pause menu!
            bool closedOtherMenu = false;

            // Workbench / Crafting.
            var crafting = CraftingUIManager.Instance ?? FindAnyObjectByType<CraftingUIManager>();
            if (crafting != null && (crafting.IsOpen || crafting.LastClosedFrame == Time.frameCount))
            {
                if (crafting.IsOpen)
                {
                    crafting.Close();
                }
                closedOtherMenu = true;
            }

            // Backpack / Inventory.
            var inv = InventoryUIManager.Instance ?? FindAnyObjectByType<InventoryUIManager>();
            if (inv != null && (inv.IsOpen || inv.LastClosedFrame == Time.frameCount))
            {
                if (inv.IsOpen)
                {
                    inv.Close();
                }
                closedOtherMenu = true;
            }

            // Character panel.
            var charUI = CharacterUIManager.Instance ?? FindAnyObjectByType<CharacterUIManager>();
            if (charUI != null && (charUI.IsOpen || charUI.LastClosedFrame == Time.frameCount))
            {
                if (charUI.IsOpen)
                {
                    charUI.Close();
                }
                closedOtherMenu = true;
            }

            // HUD attributes sheet.
            var hud = GameHUD.Instance ?? FindAnyObjectByType<GameHUD>();
            if (hud != null && hud.ShowStats)
            {
                hud.CloseStats();
                closedOtherMenu = true;
            }

            if (closedOtherMenu)
            {
                return;
            }

            // 3. No other menu open: open the expedition pause menu now!
            OpenMenu();
        }

        public void OpenMenu()
        {
            IsOpen = true;
            _showSettingsModal = false;
            _showLoreModal = false;
            _showConfirmMainMenu = false;
            _showConfirmQuit = false;

            SyncSettingsFromGameSettings();

            if (CanPauseGame())
            {
                Time.timeScale = 0f;
            }
            else
            {
                Time.timeScale = 1f;
            }

            PlayerCameraController.LocalInstance?.SetRotationLocked(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            PlayModalSound();
        }

        public void CloseMenu(bool playSound = true)
        {
            IsOpen = false;
            _showSettingsModal = false;
            _showLoreModal = false;
            _showConfirmMainMenu = false;
            _showConfirmQuit = false;

            Time.timeScale = 1f;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (PlayerCameraController.LocalInstance != null)
            {
                PlayerCameraController.LocalInstance.SetRotationLocked(false);
                PlayerCameraController.LocalInstance.SetCursorLocked(true);
            }

            if (playSound)
            {
                PlayClickSound();
            }
        }

        /// <summary>
        /// Check whether the player is completely alone in the session to permit pausing Time.timeScale.
        /// In multiplayer sessions (connected client or host with multiple players), time never freezes.
        /// </summary>
        public bool CanPauseGame()
        {
            // If FishNet is absent or inactive, this is offline single-player in the Editor / locally.
            if (FishNet.InstanceFinder.NetworkManager == null) return true;
            if (!FishNet.InstanceFinder.IsServerStarted && !FishNet.InstanceFinder.IsClientStarted) return true;

            // A client connected to a remote server cannot pause the session.
            if (FishNet.InstanceFinder.IsClientStarted && !FishNet.InstanceFinder.IsServerStarted)
                return false;

            // A Host (Server) can pause only when completely alone.
            if (FishNet.InstanceFinder.IsServerStarted)
            {
                if (FishNet.InstanceFinder.ServerManager != null && FishNet.InstanceFinder.ServerManager.Clients.Count > 1)
                    return false;

                if (Duskborn.Gameplay.Player.PlayerRegistry.All.Count > 1)
                    return false;

                return true;
            }

            return false;
        }

        private const float RefHeight = 1080f;

        private void OnGUI()
        {
            if (!IsOpen) return;

            InitStyles();

            Matrix4x4 origMatrix = GUI.matrix;
            float scale = Screen.height / RefHeight;
            if (scale <= 0.001f) scale = 1f;
            float virtualW = Screen.width / scale;
            float virtualH = RefHeight;

            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

            // 1. Darken the 3D world with semitransparent slate.
            GUI.DrawTexture(new Rect(0, 0, virtualW, virtualH), _backdropTexture, ScaleMode.StretchToFill);

            // 2. Floating embers
            DrawEmbers(virtualW, virtualH);

            // 3. Medieval screen frame.
            DrawMedievalScreenBorders(virtualW, virtualH);

            // 4. If a modal is active, draw it as an overlay.
            if (_showConfirmMainMenu)
            {
                DrawConfirmMainMenuModal(virtualW, virtualH);
            }
            else if (_showConfirmQuit)
            {
                DrawConfirmQuitModal(virtualW, virtualH);
            }
            else if (_showSettingsModal)
            {
                DrawSettingsModal(virtualW, virtualH);
            }
            else if (_showLoreModal)
            {
                DrawLoreModal(virtualW, virtualH);
            }
            else
            {
                DrawMainPausePlaque(virtualW, virtualH);
            }

            GUI.matrix = origMatrix;
        }

        private void DrawMainPausePlaque(float screenW, float screenH)
        {
            float plaqueW = Mathf.Min(screenW * 0.44f, 440f);
            float plaqueH = 490f;
            float plaqueX = (screenW - plaqueW) * 0.5f;
            float plaqueY = (screenH - plaqueH) * 0.5f;

            // Slate slab background with iron and gold frame.
            GUI.DrawTexture(new Rect(plaqueX - 5, plaqueY - 5, plaqueW + 10, plaqueH + 10), _ironFrameTexture);
            GUI.DrawTexture(new Rect(plaqueX - 2, plaqueY - 2, plaqueW + 4, plaqueH + 4), _goldTrimTexture);
            GUI.DrawTexture(new Rect(plaqueX, plaqueY, plaqueW, plaqueH), _slateTexture);

            DrawCornerRivets(plaqueX, plaqueY, plaqueW, plaqueH);

            // Heraldic header.
            GUI.Label(new Rect(plaqueX, plaqueY + 22, plaqueW, 36), "⚔   EXPEDITION PAUSED   ⚔", _titleStyle);
            string subText = CanPauseGame() ? "The Twilight awaits your orders (Paused)" : "The Twilight waits for no one (Real-time Multiplayer)";
            GUI.Label(new Rect(plaqueX, plaqueY + 56, plaqueW, 20), subText, _subTitleStyle);

            DrawRunicDivider(plaqueX + 35, plaqueY + 84, plaqueW - 70);

            // Expedition status.
            DrawExpeditionStatus(plaqueX + 25, plaqueY + 98, plaqueW - 50);

            DrawRunicDivider(plaqueX + 35, plaqueY + 168, plaqueW - 70);

            // Action buttons.
            float btnW = plaqueW - 80;
            float btnH = 46f;
            float btnX = plaqueX + 40;
            float startY = plaqueY + 186;
            float spacing = 54f;

            // 1. Continue Expedition.
            if (DrawStoneButton(new Rect(btnX, startY, btnW, btnH), "▶   CONTINUE EXPEDITION", true, false))
            {
                CloseMenu();
            }

            // 2. Settings.
            if (DrawStoneButton(new Rect(btnX, startY + spacing, btnW, btnH), "⚙   SETTINGS", false, false))
            {
                PlayModalSound();
                _showSettingsModal = true;
            }

            // 3. Lore and Controls
            if (DrawStoneButton(new Rect(btnX, startY + spacing * 2, btnW, btnH), "📜   LORE AND CONTROLS", false, false))
            {
                PlayModalSound();
                _showLoreModal = true;
            }

            // 4. Return to Main Menu.
            if (DrawStoneButton(new Rect(btnX, startY + spacing * 3, btnW, btnH), "🏰   MAIN MENU", false, false))
            {
                PlayClickSound();
                _showConfirmMainMenu = true;
            }

            // 5. Leave the Realm (Quit Game).
            if (DrawStoneButton(new Rect(btnX, startY + spacing * 4, btnW, btnH), "✕   LEAVE THE REALM", false, true))
            {
                PlayClickSound();
                _showConfirmQuit = true;
            }

            // Slab footer.
            string escTip = CanPauseGame()
                ? "Press [ESC] to return to combat (Game Paused)"
                : "Press [ESC] to return to combat (Multiplayer Active · Real Time)";
            GUI.Label(new Rect(plaqueX, plaqueY + plaqueH - 24, plaqueW, 20), escTip, _statusStyle);
        }

        private void DrawExpeditionStatus(float x, float y, float w)
        {
            var cycle = Duskborn.Core.DayNightCycle.Instance;
            string phaseName = cycle != null ? cycle.PeriodDisplayName : "Ancestral Twilight";
            string clock = cycle != null ? cycle.ClockTimeString : "12:00";
            int night = cycle != null ? cycle.CurrentNight : 1;

            int terrainSeed = ChunkGridManager.Instance != null 
                ? ChunkGridManager.Instance.ActiveSeed 
                : 4242;

            int gold = Duskborn.Gameplay.Loot.GoldManager.Instance != null 
                ? Duskborn.Gameplay.Loot.GoldManager.Instance.Gold 
                : 0;

            GUI.Label(new Rect(x, y, w, 20), $"✦ Period: <color=#fcd34d>{phaseName} [{clock}]</color>  ·  Night: <color=#93c5fd>{night}</color>", _statusStyle);
            GUI.Label(new Rect(x, y + 22, w, 20), $"✦ Terrain Seed: <color=#a7f3d0>{terrainSeed}</color>  ·  Collected Gold: <color=#fef08a>{gold} Coins</color>", _statusStyle);
        }

        // ── Overlay Modals ──

        private void DrawSettingsModal(float screenW, float screenH)
        {
            float modalW = Mathf.Min(screenW * 0.58f, 620f);
            float modalH = 540f;
            float modalX = (screenW - modalW) * 0.5f;
            float modalY = (screenH - modalH) * 0.5f;

            GUI.DrawTexture(new Rect(modalX - 5, modalY - 5, modalW + 10, modalH + 10), _ironFrameTexture);
            GUI.DrawTexture(new Rect(modalX - 2, modalY - 2, modalW + 4, modalH + 4), _goldTrimTexture);
            GUI.DrawTexture(new Rect(modalX, modalY, modalW, modalH), _parchmentInnerTex);

            DrawCornerRivets(modalX, modalY, modalW, modalH);

            GUI.Label(new Rect(modalX, modalY + 20, modalW, 32), "⚙   REALM SETTINGS   ⚙", _modalHeaderStyle);
            DrawRunicDivider(modalX + 40, modalY + 54, modalW - 80);

            float cy = modalY + 70;
            float labelW = 200f;
            float sliderW = modalW - labelW - 130f;
            float sliderX = modalX + labelW + 30f;

            // Audio section.
            GUI.Label(new Rect(modalX + 35, cy, modalW - 70, 22), "✦ AUDIO & HORNS SUBSYSTEM", _modalSectionStyle);
            cy += 28;

            DrawAudioSlider("Master Volume:", ref _masterVolume, modalX + 40, cy, labelW, sliderX, sliderW, v => GameSettings.SetMasterVolume(v));
            cy += 34;

            DrawAudioSlider("Music & Hymns:", ref _musicVolume, modalX + 40, cy, labelW, sliderX, sliderW, v => GameSettings.SetMusicVolume(v));
            cy += 34;

            DrawAudioSlider("Battle Effects (SFX):", ref _sfxVolume, modalX + 40, cy, labelW, sliderX, sliderW, v => GameSettings.SetSfxVolume(v));
            cy += 34;

            DrawAudioSlider("Atmosphere and Wilderness:", ref _ambienceVolume, modalX + 40, cy, labelW, sliderX, sliderW, v => GameSettings.SetAmbienceVolume(v));
            cy += 34;

            DrawAudioSlider("Interface and Runes:", ref _uiVolume, modalX + 40, cy, labelW, sliderX, sliderW, v => GameSettings.SetUiVolume(v));
            cy += 44;

            DrawRunicDivider(modalX + 40, cy, modalW - 80);
            cy += 14;

            // Video & camera section.
            GUI.Label(new Rect(modalX + 35, cy, modalW - 70, 22), "✦ DISPLAY & LOOK CONTROLS", _modalSectionStyle);
            cy += 28;

            // Fullscreen.
            GUI.Label(new Rect(modalX + 40, cy, labelW, 26), "Fullscreen (Immersion):", _modalKeyStyle);
            string fsText = _isFullScreen ? "☑   ENABLED" : "☐   DISABLED";
            if (GUI.Button(new Rect(sliderX, cy - 2, 160, 30), fsText, _btnSecondaryStyle))
            {
                PlayClickSound();
                _isFullScreen = !_isFullScreen;
                GameSettings.SetFullScreen(_isFullScreen);
            }
            cy += 36;

            // Mouse sensitivity.
            GUI.Label(new Rect(modalX + 40, cy, labelW, 26), "Look Sensitivity:", _modalKeyStyle);
            float newSens = GUI.HorizontalSlider(new Rect(sliderX, cy + 6, sliderW, 18), _mouseSensitivity, 0.03f, 0.45f);
            GUI.Label(new Rect(sliderX + sliderW + 15, cy, 60, 26), $"{newSens:F2}x", _modalValStyle);
            if (Math.Abs(newSens - _mouseSensitivity) > 0.005f)
            {
                _mouseSensitivity = newSens;
                GameSettings.SetMouseSensitivity(_mouseSensitivity);
            }
            cy += 34;

            // Invert Y Axis
            GUI.Label(new Rect(modalX + 40, cy, labelW, 26), "Invert Vertical Axis (Pitch):", _modalKeyStyle);
            string invText = _invertPitch ? "☑   INVERTIDO" : "☐   NORMAL";
            if (GUI.Button(new Rect(sliderX, cy - 2, 160, 30), invText, _btnSecondaryStyle))
            {
                PlayClickSound();
                _invertPitch = !_invertPitch;
                GameSettings.SetInvertPitch(_invertPitch);
            }

            // Done button.
            float closeBtnW = 200f;
            float closeBtnH = 44f;
            float closeBtnX = modalX + (modalW - closeBtnW) * 0.5f;
            float closeBtnY = modalY + modalH - closeBtnH - 20f;

            if (DrawStoneButton(new Rect(closeBtnX, closeBtnY, closeBtnW, closeBtnH), "✔   DONE", true, false))
            {
                PlayClickSound();
                GameSettings.Save();
                _showSettingsModal = false;
            }
        }

        private void DrawAudioSlider(string label, ref float value, float labelX, float y, float labelW, float sliderX, float sliderW, Action<float> onApply)
        {
            GUI.Label(new Rect(labelX, y, labelW, 26), label, _modalKeyStyle);
            float newVal = GUI.HorizontalSlider(new Rect(sliderX, y + 6, sliderW, 18), value, 0f, 1f);
            GUI.Label(new Rect(sliderX + sliderW + 15, y, 60, 26), $"{Mathf.RoundToInt(newVal * 100)}%", _modalValStyle);

            if (Math.Abs(newVal - value) > 0.01f)
            {
                value = newVal;
                onApply?.Invoke(value);
            }
        }

        private void DrawLoreModal(float screenW, float screenH)
        {
            float modalW = Mathf.Min(screenW * 0.65f, 700f);
            float modalH = 550f;
            float modalX = (screenW - modalW) * 0.5f;
            float modalY = (screenH - modalH) * 0.5f;

            GUI.DrawTexture(new Rect(modalX - 5, modalY - 5, modalW + 10, modalH + 10), _ironFrameTexture);
            GUI.DrawTexture(new Rect(modalX - 2, modalY - 2, modalW + 4, modalH + 4), _goldTrimTexture);
            GUI.DrawTexture(new Rect(modalX, modalY, modalW, modalH), _parchmentInnerTex);

            DrawCornerRivets(modalX, modalY, modalW, modalH);

            GUI.Label(new Rect(modalX, modalY + 18, modalW, 32), "📜   REALM TEACHINGS & CONTROLS   📜", _modalHeaderStyle);
            DrawRunicDivider(modalX + 40, modalY + 52, modalW - 80);

            float colW = (modalW - 90) * 0.5f;
            float col1X = modalX + 40;
            float col2X = col1X + colW + 10;
            float startY = modalY + 68;

            // Column 1: Combat and Movement Controls.
            GUI.Label(new Rect(col1X, startY, colW, 22), "✦ COMBAT AND MOBILITY", _modalSectionStyle);
            DrawControlRow(col1X, startY + 28, colW, "W, A, S, D", "Walking and tactical movement");
            DrawControlRow(col1X, startY + 62, colW, "Shift (Segurar)", "Fast sprinting");
            DrawControlRow(col1X, startY + 96, colW, "Space", "Agile dodge with invulnerability (I-Frames)");
            DrawControlRow(col1X, startY + 130, colW, "Left Mouse Button (LMB)", "Fast attack with the wielded weapon");
            DrawControlRow(col1X, startY + 164, colW, "Right Mouse Button (RMB)", "Heavy circular strike (Cleave)");
            DrawControlRow(col1X, startY + 198, colW, "Q", "Special runic weapon ability");

            // Column 2: Interface and Management.
            GUI.Label(new Rect(col2X, startY, colW, 22), "✦ MANAGEMENT & SURVIVAL", _modalSectionStyle);
            DrawControlRow(col2X, startY + 28, colW, "I  /  Tab", "Open the supply backpack and inventory");
            DrawControlRow(col2X, startY + 62, colW, "1 a 8", "Quick action bar shortcuts");
            DrawControlRow(col2X, startY + 96, colW, "C", "Display attributes and vital status");
            DrawControlRow(col2X, startY + 130, colW, "Esc", "Pause the expedition and open this sanctuary");
            DrawControlRow(col2X, startY + 164, colW, "Campfires and Light", "Repel shadows and disperse ravenous mists");
            DrawControlRow(col2X, startY + 198, colW, "Runic Workbench", "Forge superior tools with iron and oak");

            DrawRunicDivider(modalX + 40, modalY + 316, modalW - 80);

            // Twilight doctrine.
            GUI.Label(new Rect(modalX + 40, modalY + 332, modalW - 80, 22), "✦ ANCESTRAL TWILIGHT WISDOM", _modalSectionStyle);
            string wisdom = "During the golden hours of dawn and daylight, gather fine wood, stones, and ore from deposits. " +
                            "When the night horns echo across the terrain, abominations awaken from the shadows. " +
                            "Protect the Sanctuary at any cost and use clearings to break the hordes' charge.";
            GUI.Label(new Rect(modalX + 40, modalY + 360, modalW - 80, 70), wisdom, _modalBodyStyle);

            // Understood button.
            float closeBtnW = 200f;
            float closeBtnH = 44f;
            float closeBtnX = modalX + (modalW - closeBtnW) * 0.5f;
            float closeBtnY = modalY + modalH - closeBtnH - 20f;

            if (DrawStoneButton(new Rect(closeBtnX, closeBtnY, closeBtnW, closeBtnH), "✔   UNDERSTOOD", true, false))
            {
                PlayClickSound();
                _showLoreModal = false;
            }
        }

        private void DrawControlRow(float x, float y, float w, string key, string desc)
        {
            GUI.Label(new Rect(x, y, w, 18), $"<color=#fef08a><b>[{key}]</b></color>", _modalKeyStyle);
            GUI.Label(new Rect(x, y + 15, w, 18), desc, _modalBodyStyle);
        }

        private void DrawConfirmMainMenuModal(float screenW, float screenH)
        {
            float modalW = 480f;
            float modalH = 240f;
            float modalX = (screenW - modalW) * 0.5f;
            float modalY = (screenH - modalH) * 0.5f;

            GUI.DrawTexture(new Rect(modalX - 5, modalY - 5, modalW + 10, modalH + 10), _ironFrameTexture);
            GUI.DrawTexture(new Rect(modalX - 2, modalY - 2, modalW + 4, modalH + 4), _goldTrimTexture);
            GUI.DrawTexture(new Rect(modalX, modalY, modalW, modalH), _parchmentInnerTex);

            DrawCornerRivets(modalX, modalY, modalW, modalH);

            GUI.Label(new Rect(modalX, modalY + 22, modalW, 28), "🏰   RETURN TO THE TWILIGHT PORTAL?   🏰", _modalHeaderStyle);
            DrawRunicDivider(modalX + 30, modalY + 54, modalW - 60);

            string warn = "The current expedition will end and the realm will unload. " +
                          "Are you sure you want to return to the Main Menu?";
            GUI.Label(new Rect(modalX + 35, modalY + 70, modalW - 70, 50), warn, _modalBodyStyle);

            float btnW = 180f;
            float btnH = 42f;
            float btnY = modalY + modalH - btnH - 22f;

            // Confirm return.
            if (DrawStoneButton(new Rect(modalX + 45, btnY, btnW, btnH), "✔   YES, RETURN", false, true))
            {
                PlayClickSound();
                ReturnToMainMenu();
            }

            // Cancel.
            if (DrawStoneButton(new Rect(modalX + modalW - btnW - 45, btnY, btnW, btnH), "✕   CANCEL", true, false))
            {
                PlayClickSound();
                _showConfirmMainMenu = false;
            }
        }

        private void DrawConfirmQuitModal(float screenW, float screenH)
        {
            float modalW = 480f;
            float modalH = 220f;
            float modalX = (screenW - modalW) * 0.5f;
            float modalY = (screenH - modalH) * 0.5f;

            GUI.DrawTexture(new Rect(modalX - 5, modalY - 5, modalW + 10, modalH + 10), _ironFrameTexture);
            GUI.DrawTexture(new Rect(modalX - 2, modalY - 2, modalW + 4, modalH + 4), _goldTrimTexture);
            GUI.DrawTexture(new Rect(modalX, modalY, modalW, modalH), _parchmentInnerTex);

            DrawCornerRivets(modalX, modalY, modalW, modalH);

            GUI.Label(new Rect(modalX, modalY + 22, modalW, 28), "✕   LEAVE THE REALM?   ✕", _modalHeaderStyle);
            DrawRunicDivider(modalX + 30, modalY + 54, modalW - 60);

            string warn = "Quit Duskborn and return to your desktop?";
            GUI.Label(new Rect(modalX + 35, modalY + 70, modalW - 70, 40), warn, _modalBodyStyle);

            float btnW = 180f;
            float btnH = 42f;
            float btnY = modalY + modalH - btnH - 22f;

            // Confirm exit.
            if (DrawStoneButton(new Rect(modalX + 45, btnY, btnW, btnH), "✔   YES, QUIT", false, true))
            {
                PlayClickSound();
                QuitGame();
            }

            // Cancel.
            if (DrawStoneButton(new Rect(modalX + modalW - btnW - 45, btnY, btnW, btnH), "✕   CANCEL", true, false))
            {
                PlayClickSound();
                _showConfirmQuit = false;
            }
        }

        private void ReturnToMainMenu()
        {
            Time.timeScale = 1f;
            IsOpen = false;

            // Safely disconnect FishNet if connected.
            if (FishNet.InstanceFinder.NetworkManager != null)
            {
                try
                {
                    FishNet.InstanceFinder.NetworkManager.ServerManager?.StopConnection(true);
                    FishNet.InstanceFinder.NetworkManager.ClientManager?.StopConnection();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[InGameMenuController] Warning while shutting down networking: {ex.Message}");
                }
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            SceneManager.LoadScene("MainMenu");
        }

        private void QuitGame()
        {
            Time.timeScale = 1f;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // Medieval Graphics Primitives

        private bool DrawStoneButton(Rect rect, string text, bool isPrimary, bool isDanger)
        {
            Vector2 mousePos = Event.current != null ? GUI.matrix.inverse.MultiplyPoint(Event.current.mousePosition) : Vector2.zero;
            bool isHover = rect.Contains(mousePos);

            // Outer wrought iron border.
            GUI.DrawTexture(new Rect(rect.x - 3, rect.y - 3, rect.width + 6, rect.height + 6), _ironFrameTexture);

            // Gold or ruby frame.
            Color oldCol = GUI.color;
            if (isDanger)
            {
                GUI.color = isHover ? new Color(1f, 0.45f, 0.45f, 1f) : new Color(0.7f, 0.25f, 0.25f, 0.9f);
            }
            else
            {
                GUI.color = isHover ? new Color(1f, 0.92f, 0.55f, 1f) : new Color(0.85f, 0.72f, 0.35f, 0.95f);
            }
            GUI.DrawTexture(new Rect(rect.x - 1, rect.y - 1, rect.width + 2, rect.height + 2), _goldTrimTexture);
            GUI.color = oldCol;

            // Stone slab texture.
            Texture2D fillTex;
            if (isDanger)
                fillTex = isHover ? _btnDangerHoverTex : _btnDangerNormalTex;
            else
                fillTex = isHover ? _btnHoverTex : _btnNormalTex;

            GUI.DrawTexture(rect, fillTex);

            // Text style.
            GUIStyle btnStyle;
            if (isDanger)
                btnStyle = _btnDangerStyle;
            else
                btnStyle = isPrimary ? _btnPrimaryStyle : _btnSecondaryStyle;

            return GUI.Button(rect, text, btnStyle);
        }

        private void DrawRunicDivider(float x, float y, float w)
        {
            float halfW = (w - 24f) * 0.5f;
            Color oldCol = GUI.color;
            GUI.color = new Color(0.85f, 0.72f, 0.35f, 0.65f);

            GUI.DrawTexture(new Rect(x, y + 6, halfW, 1.5f), _goldTrimTexture);
            GUI.Label(new Rect(x + halfW, y - 4, 24, 20), "◆", _subTitleStyle);
            GUI.DrawTexture(new Rect(x + halfW + 24, y + 6, halfW, 1.5f), _goldTrimTexture);

            GUI.color = oldCol;
        }

        private void DrawCornerRivets(float x, float y, float w, float h)
        {
            Color oldCol = GUI.color;
            GUI.color = new Color(0.85f, 0.72f, 0.35f, 0.85f);

            float rSize = 5f;
            GUI.DrawTexture(new Rect(x + 4, y + 4, rSize, rSize), _goldTrimTexture);
            GUI.DrawTexture(new Rect(x + w - 9, y + 4, rSize, rSize), _goldTrimTexture);
            GUI.DrawTexture(new Rect(x + 4, y + h - 9, rSize, rSize), _goldTrimTexture);
            GUI.DrawTexture(new Rect(x + w - 9, y + h - 9, rSize, rSize), _goldTrimTexture);

            GUI.color = oldCol;
        }

        private void DrawMedievalScreenBorders(float screenW, float screenH)
        {
            float borderThickness = 4f;
            Color oldCol = GUI.color;
            GUI.color = new Color(0.85f, 0.72f, 0.35f, 0.45f);

            GUI.DrawTexture(new Rect(0, 0, screenW, borderThickness), _goldTrimTexture);
            GUI.DrawTexture(new Rect(0, screenH - borderThickness, screenW, borderThickness), _goldTrimTexture);
            GUI.DrawTexture(new Rect(0, 0, borderThickness, screenH), _goldTrimTexture);
            GUI.DrawTexture(new Rect(screenW - borderThickness, 0, borderThickness, screenH), _goldTrimTexture);

            GUI.color = oldCol;
        }

        private void DrawEmbers(float screenW, float screenH)
        {
            if (_embers == null || _emberTexture == null) return;

            Color oldCol = GUI.color;
            for (int i = 0; i < _embers.Length; i++)
            {
                float x = (_embers[i].xRatio + Mathf.Sin(Time.unscaledTime * 0.9f + _embers[i].seed) * 0.035f) * screenW;
                float y = _embers[i].yRatio * screenH;
                float size = _embers[i].size;
                float alpha = Mathf.PingPong(Time.unscaledTime * 1.2f + _embers[i].seed, 0.75f) + 0.25f;

                GUI.color = new Color(1f, 0.65f, 0.22f, alpha * 0.65f);
                GUI.DrawTexture(new Rect(x, y, size, size), _emberTexture);
            }
            GUI.color = oldCol;
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;

            _backdropTexture = CreateSolidTexture(new Color(0.03f, 0.04f, 0.06f, 0.82f));
            _slateTexture = CreateSlateTexture(32, 32, new Color(0.08f, 0.09f, 0.12f), new Color(0.05f, 0.06f, 0.08f));
            _ironFrameTexture = CreateSolidTexture(new Color(0.14f, 0.16f, 0.20f, 1f));
            _goldTrimTexture = CreateSolidTexture(new Color(0.85f, 0.72f, 0.35f, 0.95f));
            _btnNormalTex = CreateSolidTexture(new Color(0.12f, 0.14f, 0.18f, 0.96f));
            _btnHoverTex = CreateSolidTexture(new Color(0.22f, 0.18f, 0.12f, 0.98f));
            _btnActiveTex = CreateSolidTexture(new Color(0.08f, 0.09f, 0.11f, 1f));
            _btnDangerNormalTex = CreateSolidTexture(new Color(0.18f, 0.08f, 0.08f, 0.96f));
            _btnDangerHoverTex = CreateSolidTexture(new Color(0.28f, 0.10f, 0.10f, 0.98f));
            _parchmentModalTex = CreateSolidTexture(new Color(0.02f, 0.02f, 0.04f, 0.85f));
            _parchmentInnerTex = CreateSolidTexture(new Color(0.10f, 0.11f, 0.15f, 0.98f));
            _emberTexture = CreateEmberTexture(8);
            _sliderTroughTex = CreateSolidTexture(new Color(0.05f, 0.06f, 0.08f, 1f));
            _sliderFillTex = CreateSolidTexture(new Color(0.85f, 0.72f, 0.35f, 1f));

            _titleStyle = new GUIStyle
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _titleStyle.normal.textColor = new Color(0.96f, 0.85f, 0.48f);

            _subTitleStyle = new GUIStyle
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _subTitleStyle.normal.textColor = new Color(0.55f, 0.75f, 0.95f);

            _statusStyle = new GUIStyle
            {
                fontSize = 11,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                richText = true
            };
            _statusStyle.normal.textColor = new Color(0.75f, 0.78f, 0.82f);

            _btnPrimaryStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _btnPrimaryStyle.normal.background = null;
            _btnPrimaryStyle.normal.textColor = new Color(1f, 0.92f, 0.65f);
            _btnPrimaryStyle.hover.textColor = new Color(1f, 1f, 0.85f);
            _btnPrimaryStyle.active.textColor = new Color(0.9f, 0.7f, 0.3f);

            _btnSecondaryStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _btnSecondaryStyle.normal.background = null;
            _btnSecondaryStyle.normal.textColor = new Color(0.85f, 0.88f, 0.92f);
            _btnSecondaryStyle.hover.textColor = Color.white;
            _btnSecondaryStyle.active.textColor = new Color(0.7f, 0.75f, 0.8f);

            _btnDangerStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _btnDangerStyle.normal.background = null;
            _btnDangerStyle.normal.textColor = new Color(0.98f, 0.65f, 0.65f);
            _btnDangerStyle.hover.textColor = new Color(1f, 0.85f, 0.85f);
            _btnDangerStyle.active.textColor = new Color(0.8f, 0.4f, 0.4f);

            _modalHeaderStyle = new GUIStyle
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _modalHeaderStyle.normal.textColor = new Color(0.96f, 0.82f, 0.38f);

            _modalSectionStyle = new GUIStyle
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            _modalSectionStyle.normal.textColor = new Color(0.55f, 0.78f, 0.98f);

            _modalKeyStyle = new GUIStyle
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                richText = true
            };
            _modalKeyStyle.normal.textColor = new Color(0.95f, 0.85f, 0.55f);

            _modalValStyle = new GUIStyle
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight
            };
            _modalValStyle.normal.textColor = Color.white;

            _modalBodyStyle = new GUIStyle
            {
                fontSize = 12,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true,
                richText = true
            };
            _modalBodyStyle.normal.textColor = new Color(0.85f, 0.87f, 0.90f);

            _stylesInitialized = true;
        }

        private Texture2D CreateSolidTexture(Color c)
        {
            Texture2D tex = new Texture2D(2, 2);
            Color[] pix = new Color[4];
            for (int i = 0; i < pix.Length; i++) pix[i] = c;
            tex.SetPixels(pix);
            tex.Apply();
            return tex;
        }

        private Texture2D CreateSlateTexture(int w, int h, Color baseCol, Color darkCol)
        {
            Texture2D tex = new Texture2D(w, h);
            Color[] cols = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float noise = Mathf.PerlinNoise(x * 0.18f, y * 0.18f);
                    cols[y * w + x] = Color.Lerp(darkCol, baseCol, noise);
                }
            }
            tex.SetPixels(cols);
            tex.Apply();
            return tex;
        }

        private Texture2D CreateEmberTexture(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = size * 0.5f;
            Color[] cols = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / center;
                    float alpha = Mathf.Clamp01(1f - dist);
                    alpha = Mathf.Pow(alpha, 2f);
                    cols[y * size + x] = new Color(1f, 0.8f, 0.4f, alpha);
                }
            }
            tex.SetPixels(cols);
            tex.Apply();
            return tex;
        }

        private void CleanupTextures()
        {
            if (_backdropTexture != null) Destroy(_backdropTexture);
            if (_slateTexture != null) Destroy(_slateTexture);
            if (_ironFrameTexture != null) Destroy(_ironFrameTexture);
            if (_goldTrimTexture != null) Destroy(_goldTrimTexture);
            if (_btnNormalTex != null) Destroy(_btnNormalTex);
            if (_btnHoverTex != null) Destroy(_btnHoverTex);
            if (_btnActiveTex != null) Destroy(_btnActiveTex);
            if (_btnDangerNormalTex != null) Destroy(_btnDangerNormalTex);
            if (_btnDangerHoverTex != null) Destroy(_btnDangerHoverTex);
            if (_parchmentModalTex != null) Destroy(_parchmentModalTex);
            if (_parchmentInnerTex != null) Destroy(_parchmentInnerTex);
            if (_emberTexture != null) Destroy(_emberTexture);
            if (_sliderTroughTex != null) Destroy(_sliderTroughTex);
            if (_sliderFillTex != null) Destroy(_sliderFillTex);
            _stylesInitialized = false;
        }
    }
}
