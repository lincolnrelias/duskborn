using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Duskborn.UI
{
    /// <summary>
    /// Controlador da cena de Menu Principal em estética Medieval Fantasy Low-Poly ("Portal do Crepúsculo").
    /// Apresenta brasão heráldico dourado, botões de laje de pedra com bordas de ouro e runas reativas,
    /// brasas místicas flutuantes, pergaminho iluminado com guia de controles/sabedoria e painel de opções.
    /// Funciona 100% via OnGUI com texturas geradas dinamicamente, sem dependência de prefabs ou Canvas.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("Cena Alvo")]
        [Tooltip("Nome da cena do jogo a ser carregada ao clicar em Iniciar Expedição.")]
        public string targetGameScene = "SampleScene";

        [Header("Áudio")]
        [Tooltip("Trilha sonora temática do Menu Principal.")]
        public AudioClip menuMusic;
        [Tooltip("Efeito sonoro ao clicar em botões.")]
        public AudioClip clickSfx;
        [Tooltip("Efeito sonoro ao abrir pergaminho/modal.")]
        public AudioClip modalOpenSfx;

        // Estado dos Modais
        private bool _showLoreModal = false;
        private bool _showSettingsModal = false;
        private float _masterVolume = 1.0f;
        private bool _isFullScreen = true;

        // Partículas atmosféricas de brasas do crepúsculo
        private struct DuskEmber
        {
            public float xRatio;
            public float yRatio;
            public float speed;
            public float size;
            public float seed;
        }
        private DuskEmber[] _embers;

        // Texturas procedurais Medieval Fantasy
        private Texture2D _slateTexture;
        private Texture2D _ironFrameTexture;
        private Texture2D _goldTrimTexture;
        private Texture2D _btnNormalTex;
        private Texture2D _btnHoverTex;
        private Texture2D _btnActiveTex;
        private Texture2D _parchmentModalTex;
        private Texture2D _parchmentInnerTex;
        private Texture2D _emberTexture;

        // Estilos GUI
        private GUIStyle _titleStyle;
        private GUIStyle _subTitleStyle;
        private GUIStyle _mottoStyle;
        private GUIStyle _btnPrimaryStyle;
        private GUIStyle _btnSecondaryStyle;
        private GUIStyle _modalHeaderStyle;
        private GUIStyle _modalBodyStyle;
        private GUIStyle _modalKeyStyle;
        private GUIStyle _versionStyle;
        private bool _stylesInitialized = false;

        private void Awake()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            Duskborn.Core.GameSettings.LoadAll();
            _masterVolume = Duskborn.Core.GameSettings.MasterVolume;
            _isFullScreen = Duskborn.Core.GameSettings.FullScreen;

            InitEmbers();
            TryLoadAudio();
        }

        private void OnDestroy()
        {
            CleanupTextures();
        }

        private void Start()
        {
            TryLoadAudio();

            if (menuMusic != null && Duskborn.Audio.AudioManager.Instance != null)
            {
                Duskborn.Audio.AudioManager.Instance.PlayMusic(menuMusic, 1.5f);
            }
        }

        private void TryLoadAudio()
        {
            if (menuMusic == null) menuMusic = Resources.Load<AudioClip>("Music/music_main_menu");
            if (clickSfx == null) clickSfx = Resources.Load<AudioClip>("SFX/ui_button_click");
            if (modalOpenSfx == null) modalOpenSfx = Resources.Load<AudioClip>("SFX/ui_modal_open");

#if UNITY_EDITOR
            if (menuMusic == null)
                menuMusic = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Duskborn/Audio/Music/music_main_menu.wav");
            if (clickSfx == null)
                clickSfx = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Duskborn/Art/SFX/ui_button_click.wav");
            if (modalOpenSfx == null)
                modalOpenSfx = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Duskborn/Art/SFX/ui_modal_open.wav");
#endif
        }

        private void PlayButtonSound()
        {
            if (clickSfx == null) TryLoadAudio();
            if (clickSfx != null)
            {
                if (Duskborn.Audio.AudioManager.Instance != null)
                    Duskborn.Audio.AudioManager.Instance.PlayUISfx(clickSfx);
                else
                    AudioSource.PlayClipAtPoint(clickSfx, Camera.main != null ? Camera.main.transform.position : Vector3.zero, _masterVolume);
            }
        }

        private void PlayModalSound()
        {
            if (modalOpenSfx == null) TryLoadAudio();
            if (modalOpenSfx != null)
            {
                if (Duskborn.Audio.AudioManager.Instance != null)
                    Duskborn.Audio.AudioManager.Instance.PlayUISfx(modalOpenSfx);
                else
                    AudioSource.PlayClipAtPoint(modalOpenSfx, Camera.main != null ? Camera.main.transform.position : Vector3.zero, _masterVolume);
            }
        }

        private void InitEmbers()
        {
            _embers = new DuskEmber[36];
            for (int i = 0; i < _embers.Length; i++)
            {
                _embers[i] = new DuskEmber
                {
                    xRatio = UnityEngine.Random.value,
                    yRatio = UnityEngine.Random.value,
                    speed = UnityEngine.Random.Range(0.035f, 0.10f),
                    size = UnityEngine.Random.Range(3f, 7f),
                    seed = UnityEngine.Random.Range(0f, 100f)
                };
            }
        }

        private void Update()
        {
            // Movimentação das brasas
            if (_embers != null)
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
        }

        public void OnPlayClicked()
        {
            PlayButtonSound();
            LoadGame();
        }

        public static bool LoadedFromMainMenu { get; set; } = false;

#if UNITY_EDITOR
        [UnityEditor.InitializeOnEnterPlayMode]
        private static void OnEnterPlayMode(UnityEditor.EnterPlayModeOptions options)
        {
            LoadedFromMainMenu = false;
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticsOnSubsystemRegistration()
        {
            LoadedFromMainMenu = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetStaticsOnBeforeSceneLoad()
        {
            LoadedFromMainMenu = false;
        }

        public void LoadGame()
        {
            Debug.Log($"[MainMenuController] Iniciando jornada na cena: {targetGameScene}");
            LoadedFromMainMenu = true;
            SceneManager.LoadScene(targetGameScene);
        }

        private const float RefHeight = 1080f;

        private void OnGUI()
        {
            InitStyles();

            Matrix4x4 origMatrix = GUI.matrix;
            float scale = Screen.height / RefHeight;
            if (scale <= 0.001f) scale = 1f;
            float virtualW = Screen.width / scale;
            float virtualH = RefHeight;

            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

            // 1. Fundo de Ardósia Gótica Profunda
            GUI.DrawTexture(new Rect(0, 0, virtualW, virtualH), _slateTexture, ScaleMode.StretchToFill);

            // 2. Brasas Místicas Flutuantes
            DrawEmbers(virtualW, virtualH);

            // 3. Moldura de Ferro Forjado e Frisos de Ouro
            DrawMedievalScreenBorders(virtualW, virtualH);

            // Se algum modal estiver aberto, desenha o modal sobreposto
            if (_showLoreModal)
            {
                DrawLoreModal(virtualW, virtualH);
                GUI.matrix = origMatrix;
                return;
            }

            if (_showSettingsModal)
            {
                DrawSettingsModal(virtualW, virtualH);
                GUI.matrix = origMatrix;
                return;
            }

            // 4. Brasão & Título Heráldico
            float centerY = virtualH * 0.28f;
            GUI.Label(new Rect(0, centerY - 80, virtualW, 55), "⚔   D U S K B O R N   ⚔", _titleStyle);
            GUI.Label(new Rect(0, centerY - 20, virtualW, 25), "✦ ROGUELIKE CO-OP SURVIVAL · ERA DOS ERMOS ✦", _subTitleStyle);

            DrawRunicDivider((virtualW - 480f) * 0.5f, centerY + 12f, 480f);
            GUI.Label(new Rect(0, centerY + 24f, virtualW, 22), "\"Onde o aço e as runas decidem o destino dos homens.\"", _mottoStyle);

            // 5. Botões de Laje de Pedra e Ouro
            float btnW = Mathf.Min(virtualW * 0.40f, 340f);
            float btnH = 50f;
            float btnX = (virtualW - btnW) * 0.5f;
            float startY = centerY + 75f;
            float spacing = 62f;

            // Botão 1: Iniciar Expedição
            if (DrawStoneButton(new Rect(btnX, startY, btnW, btnH), "⚔   INICIAR EXPEDIÇÃO", true))
            {
                PlayButtonSound();
                LoadGame();
            }

            // Botão 2: Pergaminho de Saber
            if (DrawStoneButton(new Rect(btnX, startY + spacing, btnW, btnH), "📜   PERGAMINHO DE SABER", false))
            {
                PlayModalSound();
                _showLoreModal = true;
            }

            // Botão 3: Configurações do Reino
            if (DrawStoneButton(new Rect(btnX, startY + spacing * 2, btnW, btnH), "⚙   CONFIGURAÇÕES", false))
            {
                PlayModalSound();
                _showSettingsModal = true;
            }

            // Botão 4: Abandonar o Reino
            if (DrawStoneButton(new Rect(btnX, startY + spacing * 3, btnW, btnH), "✕   ABANDONAR O REINO", false))
            {
                PlayButtonSound();
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }

            // 6. Rodapé do Reino
            GUI.Label(new Rect(30, virtualH - 35, virtualW - 60, 22), "Duskborn v0.1-alpha · Forjado em Unity 6 · Mundo Procedural Sem Engasgos", _versionStyle);

            GUI.matrix = origMatrix;
        }

        private bool DrawStoneButton(Rect rect, string text, bool isPrimary)
        {
            Vector2 mousePos = Event.current != null ? GUI.matrix.inverse.MultiplyPoint(Event.current.mousePosition) : Vector2.zero;
            bool isHover = rect.Contains(mousePos);

            // Borda externa de ferro forjado
            GUI.DrawTexture(new Rect(rect.x - 3, rect.y - 3, rect.width + 6, rect.height + 6), _ironFrameTexture);

            // Moldura de ouro (brilha mais no hover)
            Color oldCol = GUI.color;
            if (isHover)
            {
                GUI.color = new Color(1f, 0.92f, 0.55f, 1f);
            }
            GUI.DrawTexture(new Rect(rect.x - 1, rect.y - 1, rect.width + 2, rect.height + 2), _goldTrimTexture);
            GUI.color = oldCol;

            // Fundo da laje de pedra
            Texture2D bg = isHover ? _btnHoverTex : _btnNormalTex;
            GUI.DrawTexture(rect, bg);

            // Destaque chanfrado no topo da laje
            GUI.DrawTexture(new Rect(rect.x + 2, rect.y + 2, rect.width - 4, 2), _goldTrimTexture);

            // Rebites nos vértices
            GUI.DrawTexture(new Rect(rect.x - 4, rect.y - 4, 5, 5), _goldTrimTexture);
            GUI.DrawTexture(new Rect(rect.x + rect.width - 1, rect.y - 4, 5, 5), _goldTrimTexture);
            GUI.DrawTexture(new Rect(rect.x - 4, rect.y + rect.height - 1, 5, 5), _goldTrimTexture);
            GUI.DrawTexture(new Rect(rect.x + rect.width - 1, rect.y + rect.height - 1, 5, 5), _goldTrimTexture);

            GUIStyle style = isPrimary ? _btnPrimaryStyle : _btnSecondaryStyle;
            return GUI.Button(rect, text, style);
        }

        private void DrawEmbers(float w, float h)
        {
            if (_embers == null || _emberTexture == null) return;

            Color oldColor = GUI.color;
            for (int i = 0; i < _embers.Length; i++)
            {
                float wobble = Mathf.Sin(Time.realtimeSinceStartup * 1.6f + _embers[i].seed) * 25f;
                float ex = _embers[i].xRatio * w + wobble;
                float ey = _embers[i].yRatio * h;

                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.realtimeSinceStartup * 3.5f + _embers[i].seed);
                GUI.color = new Color(1f, 0.72f, 0.28f, 0.35f + 0.55f * pulse);
                GUI.DrawTexture(new Rect(ex, ey, _embers[i].size, _embers[i].size), _emberTexture);
            }
            GUI.color = oldColor;
        }

        private void DrawMedievalScreenBorders(float w, float h)
        {
            GUI.DrawTexture(new Rect(0, 0, w, 6), _ironFrameTexture);
            GUI.DrawTexture(new Rect(0, h - 6, w, 6), _ironFrameTexture);
            GUI.DrawTexture(new Rect(0, 0, 6, h), _ironFrameTexture);
            GUI.DrawTexture(new Rect(w - 6, 0, 6, h), _ironFrameTexture);

            GUI.DrawTexture(new Rect(20, 20, w - 40, 1), _goldTrimTexture);
            GUI.DrawTexture(new Rect(20, h - 21, w - 40, 1), _goldTrimTexture);
            GUI.DrawTexture(new Rect(20, 20, 1, h - 40), _goldTrimTexture);
            GUI.DrawTexture(new Rect(w - 21, 20, 1, h - 40), _goldTrimTexture);

            float cornerSize = 26f;
            DrawCornerBracket(18, 18, cornerSize, cornerSize);
            DrawCornerBracket(w - 18 - cornerSize, 18, cornerSize, cornerSize);
            DrawCornerBracket(18, h - 18 - cornerSize, cornerSize, cornerSize);
            DrawCornerBracket(w - 18 - cornerSize, h - 18 - cornerSize, cornerSize, cornerSize);
        }

        private void DrawCornerBracket(float x, float y, float w, float h)
        {
            GUI.DrawTexture(new Rect(x, y, w, 3), _goldTrimTexture);
            GUI.DrawTexture(new Rect(x, y, 3, h), _goldTrimTexture);
            GUI.DrawTexture(new Rect(x - 1, y - 1, 6, 6), _goldTrimTexture);
        }

        private void DrawRunicDivider(float x, float y, float width)
        {
            float halfW = width * 0.44f;
            GUI.DrawTexture(new Rect(x, y + 4, halfW, 2), _goldTrimTexture);
            GUI.DrawTexture(new Rect(x + width - halfW, y + 4, halfW, 2), _goldTrimTexture);
            GUI.DrawTexture(new Rect(x + halfW + 12f, y + 1, 8, 8), _goldTrimTexture);
        }

        private void DrawLoreModal(float screenW, float screenH)
        {
            float modalW = Mathf.Min(screenW * 0.75f, 680f);
            float modalH = Mathf.Min(screenH * 0.80f, 540f);
            float modalX = (screenW - modalW) * 0.5f;
            float modalY = (screenH - modalH) * 0.5f;

            // Fundo escuro com vinheta
            GUI.DrawTexture(new Rect(0, 0, screenW, screenH), _parchmentModalTex);

            // Moldura externa da placa de pergaminho
            GUI.DrawTexture(new Rect(modalX - 4, modalY - 4, modalW + 8, modalH + 8), _ironFrameTexture);
            GUI.DrawTexture(new Rect(modalX - 2, modalY - 2, modalW + 4, modalH + 4), _goldTrimTexture);
            GUI.DrawTexture(new Rect(modalX, modalY, modalW, modalH), _parchmentInnerTex);

            // Título do Pergaminho
            GUI.Label(new Rect(modalX, modalY + 20, modalW, 36), "📜  PERGAMINHO DE SABER DOS ERMOS  📜", _modalHeaderStyle);
            DrawRunicDivider(modalX + 60, modalY + 60, modalW - 120);

            // Conteúdo
            float contentY = modalY + 80;
            float lineH = 26;

            GUI.Label(new Rect(modalX + 40, contentY, modalW - 80, 26), "❖ COMANDOS DE SOBREVIVÊNCIA & COMBATE:", _modalKeyStyle);
            contentY += 32;

            string[,] controls = new string[,]
            {
                { "[W, A, S, D]", "Navegar pelos vales, platôs e florestas" },
                { "[Espaço]", "Rolamento tático e esquiva contra golpes pesados" },
                { "[Botão Esquerdo / J]", "Golpear com a arma equipada e desferir combos" },
                { "[E / F]", "Interagir com a Bancada, colher recursos e abrir baús" },
                { "[Tab / I]", "Abrir a mochila e organizar o inventário de itens" },
                { "[C]", "Exibir atributos vitais e status do guerreiro" }
            };

            for (int i = 0; i < controls.GetLength(0); i++)
            {
                GUI.Label(new Rect(modalX + 50, contentY + i * lineH, 180, lineH), controls[i, 0], _modalKeyStyle);
                GUI.Label(new Rect(modalX + 240, contentY + i * lineH, modalW - 290, lineH), controls[i, 1], _modalBodyStyle);
            }

            contentY += controls.GetLength(0) * lineH + 16;
            DrawRunicDivider(modalX + 60, contentY, modalW - 120);
            contentY += 16;

            GUI.Label(new Rect(modalX + 40, contentY, modalW - 80, 26), "❖ O DESAFIO DO CREPÚSCULO:", _modalKeyStyle);
            contentY += 28;
            string loreText = "Sob o manto da noite, as névoas ancestrais libertam bestas vorazes. Reúna madeira e ferro, proteja o Santuário Central e resista até os primeiros raios da alvorada.";
            GUI.Label(new Rect(modalX + 50, contentY, modalW - 100, 50), loreText, _modalBodyStyle);

            // Botão Fechar
            float closeBtnW = 220f;
            float closeBtnH = 42f;
            float closeBtnX = modalX + (modalW - closeBtnW) * 0.5f;
            float closeBtnY = modalY + modalH - closeBtnH - 20f;

            if (DrawStoneButton(new Rect(closeBtnX, closeBtnY, closeBtnW, closeBtnH), "✕   FECHAR PERGAMINHO", false))
            {
                PlayButtonSound();
                _showLoreModal = false;
            }
        }

        private void DrawSettingsModal(float screenW, float screenH)
        {
            float modalW = Mathf.Min(screenW * 0.65f, 540f);
            float modalH = Mathf.Min(screenH * 0.60f, 360f);
            float modalX = (screenW - modalW) * 0.5f;
            float modalY = (screenH - modalH) * 0.5f;

            GUI.DrawTexture(new Rect(0, 0, screenW, screenH), _parchmentModalTex);

            GUI.DrawTexture(new Rect(modalX - 4, modalY - 4, modalW + 8, modalH + 8), _ironFrameTexture);
            GUI.DrawTexture(new Rect(modalX - 2, modalY - 2, modalW + 4, modalH + 4), _goldTrimTexture);
            GUI.DrawTexture(new Rect(modalX, modalY, modalW, modalH), _parchmentInnerTex);

            GUI.Label(new Rect(modalX, modalY + 24, modalW, 34), "⚙   CONFIGURAÇÕES DO REINO   ⚙", _modalHeaderStyle);
            DrawRunicDivider(modalX + 50, modalY + 62, modalW - 100);

            float contentY = modalY + 95;

            // Volume Master Slider
            GUI.Label(new Rect(modalX + 50, contentY, 180, 28), "Volume das Trombetas:", _modalKeyStyle);
            float newVol = GUI.HorizontalSlider(new Rect(modalX + 240, contentY + 6, modalW - 350, 20), _masterVolume, 0f, 1f);
            GUI.Label(new Rect(modalX + modalW - 95, contentY, 60, 28), $"{Mathf.RoundToInt(newVol * 100)}%", _modalKeyStyle);

            if (Math.Abs(newVol - _masterVolume) > 0.01f)
            {
                _masterVolume = newVol;
                Duskborn.Core.GameSettings.SetMasterVolume(_masterVolume);
            }

            contentY += 55;

            // Fullscreen Toggle
            GUI.Label(new Rect(modalX + 50, contentY, 180, 28), "Tela Inteira (Imersão):", _modalKeyStyle);
            string fsLabel = _isFullScreen ? "☑  Ativada" : "☐  Desativada";
            if (GUI.Button(new Rect(modalX + 240, contentY - 2, 140, 32), fsLabel, _btnSecondaryStyle))
            {
                PlayButtonSound();
                _isFullScreen = !_isFullScreen;
                Duskborn.Core.GameSettings.SetFullScreen(_isFullScreen);
            }

            // Botão Concluir
            float closeBtnW = 200f;
            float closeBtnH = 42f;
            float closeBtnX = modalX + (modalW - closeBtnW) * 0.5f;
            float closeBtnY = modalY + modalH - closeBtnH - 22f;

            if (DrawStoneButton(new Rect(closeBtnX, closeBtnY, closeBtnW, closeBtnH), "✔   CONCLUIR", false))
            {
                PlayButtonSound();
                _showSettingsModal = false;
            }
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;

            _slateTexture = CreateSlateTexture(32, 32, new Color(0.08f, 0.09f, 0.12f), new Color(0.05f, 0.06f, 0.08f));
            _ironFrameTexture = CreateSolidTexture(new Color(0.14f, 0.16f, 0.20f, 1f));
            _goldTrimTexture = CreateSolidTexture(new Color(0.85f, 0.72f, 0.35f, 0.95f));
            _btnNormalTex = CreateSolidTexture(new Color(0.12f, 0.14f, 0.18f, 0.96f));
            _btnHoverTex = CreateSolidTexture(new Color(0.22f, 0.18f, 0.12f, 0.98f)); // Brilho de brasa/ouro
            _btnActiveTex = CreateSolidTexture(new Color(0.08f, 0.09f, 0.11f, 1f));
            _parchmentModalTex = CreateSolidTexture(new Color(0.02f, 0.02f, 0.04f, 0.75f));
            _parchmentInnerTex = CreateSolidTexture(new Color(0.10f, 0.11f, 0.15f, 0.98f));
            _emberTexture = CreateEmberTexture(8);

            _titleStyle = new GUIStyle
            {
                fontSize = 44,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _titleStyle.normal.textColor = new Color(0.96f, 0.85f, 0.48f);

            _subTitleStyle = new GUIStyle
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _subTitleStyle.normal.textColor = new Color(0.55f, 0.75f, 0.95f);

            _mottoStyle = new GUIStyle
            {
                fontSize = 13,
                fontStyle = FontStyle.Italic,
                alignment = TextAnchor.MiddleCenter
            };
            _mottoStyle.normal.textColor = new Color(0.80f, 0.75f, 0.65f);

            _btnPrimaryStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _btnPrimaryStyle.normal.background = null;
            _btnPrimaryStyle.normal.textColor = new Color(1f, 0.92f, 0.65f);
            _btnPrimaryStyle.hover.textColor = new Color(1f, 1f, 0.85f);
            _btnPrimaryStyle.active.textColor = new Color(0.9f, 0.7f, 0.3f);

            _btnSecondaryStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _btnSecondaryStyle.normal.background = null;
            _btnSecondaryStyle.normal.textColor = new Color(0.85f, 0.88f, 0.92f);
            _btnSecondaryStyle.hover.textColor = Color.white;
            _btnSecondaryStyle.active.textColor = new Color(0.7f, 0.75f, 0.8f);

            _modalHeaderStyle = new GUIStyle
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _modalHeaderStyle.normal.textColor = new Color(0.96f, 0.82f, 0.38f);

            _modalKeyStyle = new GUIStyle
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            _modalKeyStyle.normal.textColor = new Color(0.95f, 0.85f, 0.55f);

            _modalBodyStyle = new GUIStyle
            {
                fontSize = 13,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true
            };
            _modalBodyStyle.normal.textColor = new Color(0.85f, 0.87f, 0.90f);

            _versionStyle = new GUIStyle
            {
                fontSize = 12,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft
            };
            _versionStyle.normal.textColor = new Color(0.55f, 0.60f, 0.70f, 0.75f);

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
                    bool isBorder = (x % 16 == 0) || (y % 16 == 0);
                    float noise = Mathf.PerlinNoise(x * 0.15f, y * 0.15f) * 0.12f;
                    Color c = Color.Lerp(baseCol, darkCol, noise);
                    if (isBorder) c = Color.Lerp(c, Color.black, 0.35f);
                    cols[y * w + x] = c;
                }
            }
            tex.SetPixels(cols);
            tex.Apply();
            return tex;
        }

        private Texture2D CreateEmberTexture(int size)
        {
            Texture2D tex = new Texture2D(size, size);
            Color[] cols = new Color[size * size];
            float center = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / center;
                    float alpha = Mathf.Clamp01(1f - dist * dist);
                    cols[y * size + x] = new Color(1f, 0.85f, 0.45f, alpha);
                }
            }
            tex.SetPixels(cols);
            tex.Apply();
            return tex;
        }

        private void CleanupTextures()
        {
            if (_slateTexture != null) Destroy(_slateTexture);
            if (_ironFrameTexture != null) Destroy(_ironFrameTexture);
            if (_goldTrimTexture != null) Destroy(_goldTrimTexture);
            if (_btnNormalTex != null) Destroy(_btnNormalTex);
            if (_btnHoverTex != null) Destroy(_btnHoverTex);
            if (_btnActiveTex != null) Destroy(_btnActiveTex);
            if (_parchmentModalTex != null) Destroy(_parchmentModalTex);
            if (_parchmentInnerTex != null) Destroy(_parchmentInnerTex);
            if (_emberTexture != null) Destroy(_emberTexture);
        }
    }
}
