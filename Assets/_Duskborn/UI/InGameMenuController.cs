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
    /// Menu de pausa e opções in-game em estética Medieval Fantasy Low-Poly ("Pausa da Expedição").
    /// Apresenta molduras de ferro forjado, frisos de ouro envelhecido, botões de laje de pedra com brilho de brasa,
    /// brasas místicas flutuantes, modais de configurações completas de áudio/gráficos/controles e pergaminho de saber.
    /// Funciona 100% via OnGUI com texturas procedurais sem dependência de prefabs ou Canvas.
    /// </summary>
    public class InGameMenuController : MonoBehaviour
    {
        public static InGameMenuController Instance { get; private set; }

        [Header("Áudio e Efeitos")]
        [Tooltip("Efeito sonoro ao clicar em botões.")]
        public AudioClip clickSfx;
        [Tooltip("Efeito sonoro ao abrir pergaminho/modal.")]
        public AudioClip modalOpenSfx;

        // Estado do Menu
        public bool IsOpen { get; private set; } = false;

        private bool _showSettingsModal = false;
        private bool _showLoreModal = false;
        private bool _showConfirmMainMenu = false;
        private bool _showConfirmQuit = false;

        // Valores de Configuração em Edição
        private float _masterVolume;
        private float _musicVolume;
        private float _sfxVolume;
        private float _ambienceVolume;
        private float _uiVolume;
        private bool  _isFullScreen;
        private float _mouseSensitivity;
        private bool  _invertPitch;

        // Brasas místicas do crepúsculo
        private struct DuskEmber
        {
            public float xRatio;
            public float yRatio;
            public float speed;
            public float size;
            public float seed;
        }
        private DuskEmber[] _embers;

        // Texturas procedurais geradas dinamicamente
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

        // Estilos GUI
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
            // Não opera na cena de menu principal
            if (SceneManager.GetActiveScene().name == "MainMenu") return;

            // Animação das brasas com unscaledDeltaTime (funciona mesmo com Time.timeScale = 0)
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

            // Se estiver em pausa e outro jogador ingressar na sessão, descongela o tempo real imediatamente
            if (IsOpen && Time.timeScale == 0f && !CanPauseGame())
            {
                Time.timeScale = 1f;
            }

            // Captura da tecla Escape
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
            // Se o inventário estiver aberto no mesmo momento, deixa o inventário fechar primeiro
            var inv = FindAnyObjectByType<InventoryUIManager>();
            if (inv != null && inv.IsOpen)
            {
                return;
            }

            if (!IsOpen)
            {
                OpenMenu();
            }
            else
            {
                // Hierarquia defensiva de fechamento de modais
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
            }
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

            PlayerCameraController.LocalInstance?.SetRotationLocked(false);

            if (playSound)
            {
                PlayClickSound();
            }
        }

        /// <summary>
        /// Verifica se o jogador está completamente sozinho na partida para permitir pausar o Time.timeScale.
        /// Em partidas multiplayer (como cliente conectado ou host com múltiplos jogadores), o tempo nunca é congelado.
        /// </summary>
        public bool CanPauseGame()
        {
            // Se FishNet não existir ou não estiver ativo, é single-player offline no Editor/local
            if (FishNet.InstanceFinder.NetworkManager == null) return true;
            if (!FishNet.InstanceFinder.IsServerStarted && !FishNet.InstanceFinder.IsClientStarted) return true;

            // Se for um cliente conectado a um servidor remoto, não pode pausar a sessão
            if (FishNet.InstanceFinder.IsClientStarted && !FishNet.InstanceFinder.IsServerStarted)
                return false;

            // Se for o Host (Server), só pode pausar se estiver estritamente sozinho
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

            // 1. Escurecimento do mundo 3D com ardósia semitransparente
            GUI.DrawTexture(new Rect(0, 0, virtualW, virtualH), _backdropTexture, ScaleMode.StretchToFill);

            // 2. Brasas flutuantes
            DrawEmbers(virtualW, virtualH);

            // 3. Moldura medieval de tela
            DrawMedievalScreenBorders(virtualW, virtualH);

            // 4. Se algum modal estiver ativo, desenha sobreposto
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

            // Fundo de laje de ardósia com moldura de ferro e ouro
            GUI.DrawTexture(new Rect(plaqueX - 5, plaqueY - 5, plaqueW + 10, plaqueH + 10), _ironFrameTexture);
            GUI.DrawTexture(new Rect(plaqueX - 2, plaqueY - 2, plaqueW + 4, plaqueH + 4), _goldTrimTexture);
            GUI.DrawTexture(new Rect(plaqueX, plaqueY, plaqueW, plaqueH), _slateTexture);

            DrawCornerRivets(plaqueX, plaqueY, plaqueW, plaqueH);

            // Cabeçalho Heráldico
            GUI.Label(new Rect(plaqueX, plaqueY + 22, plaqueW, 36), "⚔   EXPEDIÇÃO EM PAUSA   ⚔", _titleStyle);
            string subText = CanPauseGame() ? "O Crepúsculo aguarda suas ordens (Pausado)" : "O Crepúsculo não espera ninguém (Multiplayer em tempo real)";
            GUI.Label(new Rect(plaqueX, plaqueY + 56, plaqueW, 20), subText, _subTitleStyle);

            DrawRunicDivider(plaqueX + 35, plaqueY + 84, plaqueW - 70);

            // Status da Expedição
            DrawExpeditionStatus(plaqueX + 25, plaqueY + 98, plaqueW - 50);

            DrawRunicDivider(plaqueX + 35, plaqueY + 168, plaqueW - 70);

            // Botões de Ação
            float btnW = plaqueW - 80;
            float btnH = 46f;
            float btnX = plaqueX + 40;
            float startY = plaqueY + 186;
            float spacing = 54f;

            // 1. Continuar Expedição
            if (DrawStoneButton(new Rect(btnX, startY, btnW, btnH), "▶   CONTINUAR EXPEDIÇÃO", true, false))
            {
                CloseMenu();
            }

            // 2. Configurações
            if (DrawStoneButton(new Rect(btnX, startY + spacing, btnW, btnH), "⚙   CONFIGURAÇÕES", false, false))
            {
                PlayModalSound();
                _showSettingsModal = true;
            }

            // 3. Sabedoria & Controles
            if (DrawStoneButton(new Rect(btnX, startY + spacing * 2, btnW, btnH), "📜   SABEDORIA & CONTROLES", false, false))
            {
                PlayModalSound();
                _showLoreModal = true;
            }

            // 4. Retornar ao Menu Principal
            if (DrawStoneButton(new Rect(btnX, startY + spacing * 3, btnW, btnH), "🏰   MENU PRINCIPAL", false, false))
            {
                PlayClickSound();
                _showConfirmMainMenu = true;
            }

            // 5. Abandonar o Reino (Sair do Jogo)
            if (DrawStoneButton(new Rect(btnX, startY + spacing * 4, btnW, btnH), "✕   ABANDONAR O REINO", false, true))
            {
                PlayClickSound();
                _showConfirmQuit = true;
            }

            // Rodapé da laje
            string escTip = CanPauseGame()
                ? "Pressione [ESC] para retornar ao combate (Jogo Pausado)"
                : "Pressione [ESC] para retornar ao combate (Multiplayer Ativo · Tempo Real)";
            GUI.Label(new Rect(plaqueX, plaqueY + plaqueH - 24, plaqueW, 20), escTip, _statusStyle);
        }

        private void DrawExpeditionStatus(float x, float y, float w)
        {
            var cycle = Duskborn.Core.DayNightCycle.Instance;
            string phaseName = cycle != null ? cycle.PeriodDisplayName : "Crepúsculo Ancestral";
            string clock = cycle != null ? cycle.ClockTimeString : "12:00";
            int night = cycle != null ? cycle.CurrentNight : 1;

            int terrainSeed = ChunkGridManager.Instance != null 
                ? ChunkGridManager.Instance.ActiveSeed 
                : 4242;

            int gold = Duskborn.Gameplay.Loot.GoldManager.Instance != null 
                ? Duskborn.Gameplay.Loot.GoldManager.Instance.Gold 
                : 0;

            GUI.Label(new Rect(x, y, w, 20), $"✦ Período: <color=#fcd34d>{phaseName} [{clock}]</color>  ·  Noite: <color=#93c5fd>{night}</color>", _statusStyle);
            GUI.Label(new Rect(x, y + 22, w, 20), $"✦ Semente do Relevo: <color=#a7f3d0>{terrainSeed}</color>  ·  Ouro Acumulado: <color=#fef08a>{gold} Moedas</color>", _statusStyle);
        }

        // ── Modais Sobrepostos ────────────────────────────────────────────────

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

            GUI.Label(new Rect(modalX, modalY + 20, modalW, 32), "⚙   CONFIGURAÇÕES DO REINO   ⚙", _modalHeaderStyle);
            DrawRunicDivider(modalX + 40, modalY + 54, modalW - 80);

            float cy = modalY + 70;
            float labelW = 200f;
            float sliderW = modalW - labelW - 130f;
            float sliderX = modalX + labelW + 30f;

            // Seção de Áudio
            GUI.Label(new Rect(modalX + 35, cy, modalW - 70, 22), "✦ SUBSISTEMA DE ÁUDIO & TROMBETAS", _modalSectionStyle);
            cy += 28;

            DrawAudioSlider("Volume Geral (Master):", ref _masterVolume, modalX + 40, cy, labelW, sliderX, sliderW, v => GameSettings.SetMasterVolume(v));
            cy += 34;

            DrawAudioSlider("Música & Hinos:", ref _musicVolume, modalX + 40, cy, labelW, sliderX, sliderW, v => GameSettings.SetMusicVolume(v));
            cy += 34;

            DrawAudioSlider("Efeitos de Batalha (SFX):", ref _sfxVolume, modalX + 40, cy, labelW, sliderX, sliderW, v => GameSettings.SetSfxVolume(v));
            cy += 34;

            DrawAudioSlider("Atmosfera & Ermos:", ref _ambienceVolume, modalX + 40, cy, labelW, sliderX, sliderW, v => GameSettings.SetAmbienceVolume(v));
            cy += 34;

            DrawAudioSlider("Interface & Runas:", ref _uiVolume, modalX + 40, cy, labelW, sliderX, sliderW, v => GameSettings.SetUiVolume(v));
            cy += 44;

            DrawRunicDivider(modalX + 40, cy, modalW - 80);
            cy += 14;

            // Seção de Vídeo & Câmera
            GUI.Label(new Rect(modalX + 35, cy, modalW - 70, 22), "✦ EXIBIÇÃO & CONTROLE DO OLHAR", _modalSectionStyle);
            cy += 28;

            // Tela Inteira
            GUI.Label(new Rect(modalX + 40, cy, labelW, 26), "Tela Inteira (Imersão):", _modalKeyStyle);
            string fsText = _isFullScreen ? "☑   ATIVADA" : "☐   DESATIVADA";
            if (GUI.Button(new Rect(sliderX, cy - 2, 160, 30), fsText, _btnSecondaryStyle))
            {
                PlayClickSound();
                _isFullScreen = !_isFullScreen;
                GameSettings.SetFullScreen(_isFullScreen);
            }
            cy += 36;

            // Sensibilidade do Mouse
            GUI.Label(new Rect(modalX + 40, cy, labelW, 26), "Sensibilidade do Olhar:", _modalKeyStyle);
            float newSens = GUI.HorizontalSlider(new Rect(sliderX, cy + 6, sliderW, 18), _mouseSensitivity, 0.03f, 0.45f);
            GUI.Label(new Rect(sliderX + sliderW + 15, cy, 60, 26), $"{newSens:F2}x", _modalValStyle);
            if (Math.Abs(newSens - _mouseSensitivity) > 0.005f)
            {
                _mouseSensitivity = newSens;
                GameSettings.SetMouseSensitivity(_mouseSensitivity);
            }
            cy += 34;

            // Inverter Eixo Y
            GUI.Label(new Rect(modalX + 40, cy, labelW, 26), "Inverter Eixo Vertical (Pitch):", _modalKeyStyle);
            string invText = _invertPitch ? "☑   INVERTIDO" : "☐   NORMAL";
            if (GUI.Button(new Rect(sliderX, cy - 2, 160, 30), invText, _btnSecondaryStyle))
            {
                PlayClickSound();
                _invertPitch = !_invertPitch;
                GameSettings.SetInvertPitch(_invertPitch);
            }

            // Botão Concluir
            float closeBtnW = 200f;
            float closeBtnH = 44f;
            float closeBtnX = modalX + (modalW - closeBtnW) * 0.5f;
            float closeBtnY = modalY + modalH - closeBtnH - 20f;

            if (DrawStoneButton(new Rect(closeBtnX, closeBtnY, closeBtnW, closeBtnH), "✔   CONCLUIR", true, false))
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

            GUI.Label(new Rect(modalX, modalY + 18, modalW, 32), "📜   ENSINAMENTOS & CONTROLES DO REINO   📜", _modalHeaderStyle);
            DrawRunicDivider(modalX + 40, modalY + 52, modalW - 80);

            float colW = (modalW - 90) * 0.5f;
            float col1X = modalX + 40;
            float col2X = col1X + colW + 10;
            float startY = modalY + 68;

            // Coluna 1: Controles de Combate e Movimento
            GUI.Label(new Rect(col1X, startY, colW, 22), "✦ COMBATE & MOBILIDADE", _modalSectionStyle);
            DrawControlRow(col1X, startY + 28, colW, "W, A, S, D", "Marcha e movimentação tática");
            DrawControlRow(col1X, startY + 62, colW, "Shift (Segurar)", "Disparada em corrida rápida");
            DrawControlRow(col1X, startY + 96, colW, "Espaço", "Esquiva ágil com invulnerabilidade (I-Frames)");
            DrawControlRow(col1X, startY + 130, colW, "Botão Esquerdo (LMB)", "Ataque veloz com a arma empunhada");
            DrawControlRow(col1X, startY + 164, colW, "Botão Direito (RMB)", "Golpe circular pesado (Cleave)");
            DrawControlRow(col1X, startY + 198, colW, "Q", "Habilidade rúnica especial da arma");

            // Coluna 2: Interface e Gestão
            GUI.Label(new Rect(col2X, startY, colW, 22), "✦ GESTÃO & SOBREVIVÊNCIA", _modalSectionStyle);
            DrawControlRow(col2X, startY + 28, colW, "I  /  Tab", "Abre a mochila de suprimentos e inventário");
            DrawControlRow(col2X, startY + 62, colW, "1 a 8", "Atalhos rápidos da barra de ação");
            DrawControlRow(col2X, startY + 96, colW, "C", "Exibe a ficha de atributos e status vital");
            DrawControlRow(col2X, startY + 130, colW, "Esc", "Pausa a expedição e abre este santuário");
            DrawControlRow(col2X, startY + 164, colW, "Fogueiras & Luz", "Afastam sombras e dissipam névoas vorazes");
            DrawControlRow(col2X, startY + 198, colW, "Bancada Rúnica", "Forja ferramentas superiores com ferro e carvalho");

            DrawRunicDivider(modalX + 40, modalY + 316, modalW - 80);

            // Doutrina do Crepúsculo
            GUI.Label(new Rect(modalX + 40, modalY + 332, modalW - 80, 22), "✦ SABEDORIA ANCESTRAL DO CREPÚSCULO", _modalSectionStyle);
            string wisdom = "Durante as horas douradas da alvorada e dia, extraia madeira nobre, pedregulhos e minério das jazidas. " +
                            "Quando as trombetas da noite ecoarem pelo relevo, as abominações despertarão das sombras. " +
                            "Proteja o Santuário a qualquer custo e utilize as clareiras para quebrar a investida das hordas.";
            GUI.Label(new Rect(modalX + 40, modalY + 360, modalW - 80, 70), wisdom, _modalBodyStyle);

            // Botão Entendido
            float closeBtnW = 200f;
            float closeBtnH = 44f;
            float closeBtnX = modalX + (modalW - closeBtnW) * 0.5f;
            float closeBtnY = modalY + modalH - closeBtnH - 20f;

            if (DrawStoneButton(new Rect(closeBtnX, closeBtnY, closeBtnW, closeBtnH), "✔   ENTENDIDO", true, false))
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

            GUI.Label(new Rect(modalX, modalY + 22, modalW, 28), "🏰   RETORNAR AO PORTAL DO CREPÚSCULO?   🏰", _modalHeaderStyle);
            DrawRunicDivider(modalX + 30, modalY + 54, modalW - 60);

            string warn = "A expedição atual será interrompida e o reino será descarregado. " +
                          "Deseja realmente voltar ao Menu Principal?";
            GUI.Label(new Rect(modalX + 35, modalY + 70, modalW - 70, 50), warn, _modalBodyStyle);

            float btnW = 180f;
            float btnH = 42f;
            float btnY = modalY + modalH - btnH - 22f;

            // Confirmar retorno
            if (DrawStoneButton(new Rect(modalX + 45, btnY, btnW, btnH), "✔   SIM, RETORNAR", false, true))
            {
                PlayClickSound();
                ReturnToMainMenu();
            }

            // Cancelar
            if (DrawStoneButton(new Rect(modalX + modalW - btnW - 45, btnY, btnW, btnH), "✕   CANCELAR", true, false))
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

            GUI.Label(new Rect(modalX, modalY + 22, modalW, 28), "✕   ABANDONAR O REINO?   ✕", _modalHeaderStyle);
            DrawRunicDivider(modalX + 30, modalY + 54, modalW - 60);

            string warn = "Deseja encerrar o jogo Duskborn e retornar à sua área de trabalho?";
            GUI.Label(new Rect(modalX + 35, modalY + 70, modalW - 70, 40), warn, _modalBodyStyle);

            float btnW = 180f;
            float btnH = 42f;
            float btnY = modalY + modalH - btnH - 22f;

            // Confirmar saída
            if (DrawStoneButton(new Rect(modalX + 45, btnY, btnW, btnH), "✔   SIM, SAIR", false, true))
            {
                PlayClickSound();
                QuitGame();
            }

            // Cancelar
            if (DrawStoneButton(new Rect(modalX + modalW - btnW - 45, btnY, btnW, btnH), "✕   CANCELAR", true, false))
            {
                PlayClickSound();
                _showConfirmQuit = false;
            }
        }

        private void ReturnToMainMenu()
        {
            Time.timeScale = 1f;
            IsOpen = false;

            // Desconecta FishNet com segurança se estiver conectado
            if (FishNet.InstanceFinder.NetworkManager != null)
            {
                try
                {
                    FishNet.InstanceFinder.NetworkManager.ServerManager?.StopConnection(true);
                    FishNet.InstanceFinder.NetworkManager.ClientManager?.StopConnection();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[InGameMenuController] Aviso ao encerrar rede: {ex.Message}");
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

        // ── Primitivas Gráficas Medievais ─────────────────────────────────────

        private bool DrawStoneButton(Rect rect, string text, bool isPrimary, bool isDanger)
        {
            Vector2 mousePos = Event.current != null ? GUI.matrix.inverse.MultiplyPoint(Event.current.mousePosition) : Vector2.zero;
            bool isHover = rect.Contains(mousePos);

            // Borda externa de ferro forjado
            GUI.DrawTexture(new Rect(rect.x - 3, rect.y - 3, rect.width + 6, rect.height + 6), _ironFrameTexture);

            // Moldura dourada ou rubi
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

            // Textura da laje de pedra
            Texture2D fillTex;
            if (isDanger)
                fillTex = isHover ? _btnDangerHoverTex : _btnDangerNormalTex;
            else
                fillTex = isHover ? _btnHoverTex : _btnNormalTex;

            GUI.DrawTexture(rect, fillTex);

            // Estilo do texto
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
