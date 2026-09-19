using System;
using System.Collections;
using UnityEngine;

namespace Duskborn.UI
{
    /// <summary>
    /// Tela de carregamento procedural estilizada em tema Medieval/Fantasy Low-Poly ("Grimório & Bússola Rúnica").
    /// Apresenta molduras de ferro forjado e ouro envelhecido, bússola/estrela rúnica facetada giratória em 3D,
    /// partículas de brasas do crepúsculo flutuantes, barra de progresso em âmbar/cristal com brilho dinâmico,
    /// títulos épicos de forja do mundo e ensinamentos ancestrais de sobrevivência.
    /// Funciona 100% via OnGUI com texturas geradas dinamicamente, sem dependências de prefabs externos.
    /// </summary>
    public class WorldLoadingScreenUI : MonoBehaviour
    {
        public static WorldLoadingScreenUI Instance { get; private set; }
 
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }

        [Header("Estado de Carregamento")]
        [Range(0f, 1f)] public float targetProgress = 0f;
        public float currentProgress = 0f;
        public string stageTitle = "Evocando o Crepúsculo...";
        public string stageDetail = "Despertando as sementes do mundo ancestral...";
        public bool isGenerating = false;

        [Header("Configuração Visual")]
        public bool autoFadeOut = true;
        public float fadeOutSpeed = 2.2f;

        private float _currentAlpha = 1f;
        public float CurrentAlpha => _currentAlpha;
        private bool _isFadingOut = false;
        public bool IsFadingOut => _isFadingOut;
        private float _tipTimer = 0f;
        private int _currentTipIndex = 0;
        private float _starRotation = 0f;
        private float _shimmerOffset = 0f;

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

        private static readonly string[] SurvivalTips = new string[]
        {
            "✦ Nas horas douradas do Crepúsculo, acumule madeira e ferro; a escuridão oculta horrores vorazes.",
            "✦ O Santuário Central emana uma barreira sagrada. Proteja o coração do seu refúgio a qualquer custo.",
            "✦ Lâminas de aço puro quebram a guarda de armaduras pesadas e rompem a carapaça de abominações.",
            "✦ A esquiva tática [Espaço] consome fôlego, mas garante imunidade temporária contra golpes esmagadores.",
            "✦ Baús antigos espalhados nos confins do ermo guardam relíquias perdidas dos reis caídos.",
            "✦ Mantenha fogueiras e tochas acesas: a luz é a única fronteira que as criaturas da noite hesitam cruzar.",
            "✦ A bancada de trabalho na clareira permite forjar armas arcanas combinando madeira nobre e minério refinado."
        };

        // Texturas procedurais com estética Medieval Fantasy Low-Poly
        private Texture2D _slateTexture;
        private Texture2D _ironFrameTexture;
        private Texture2D _goldTrimTexture;
        private Texture2D _amberFillTexture;
        private Texture2D _barTroughTexture;
        private Texture2D _runicStarTexture;
        private Texture2D _emberTexture;
        private Texture2D _parchmentPlaqueTexture;

        private GUIStyle _crestTitleStyle;
        private GUIStyle _crestSubStyle;
        private GUIStyle _stageStyle;
        private GUIStyle _detailStyle;
        private GUIStyle _percentStyle;
        private GUIStyle _loreStyle;
        private bool _stylesInitialized = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitEmbers();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            CleanupTextures();
        }

        public static WorldLoadingScreenUI EnsureInstance()
        {
            if (Instance != null) return Instance;

            Instance = FindAnyObjectByType<WorldLoadingScreenUI>();
            if (Instance != null) return Instance;

            GameObject go = new GameObject("[WorldLoadingScreenUI]");
            Instance = go.AddComponent<WorldLoadingScreenUI>();
            return Instance;
        }

        public void Show(string initialStage = "Forjando o Relevo Ancestral...", string initialDetail = "Alocando dados geológicos...")
        {
            gameObject.SetActive(true);
            isGenerating = true;
            _isFadingOut = false;
            _currentAlpha = 1f;
            targetProgress = 0f;
            currentProgress = 0f;
            stageTitle = initialStage;
            stageDetail = initialDetail;
        }

        public void UpdateProgress(float progress, string stage, string detail)
        {
            targetProgress = Mathf.Clamp01(progress);
            if (!string.IsNullOrEmpty(stage)) stageTitle = stage;
            if (!string.IsNullOrEmpty(detail)) stageDetail = detail;
        }

        public void CompleteAndFadeOut(Action onFinished = null)
        {
            targetProgress = 1.0f;
            stageTitle = "O Crepúsculo se Revela!";
            stageDetail = "Mundo consagrado. Entrando na terra dos ermos...";
            StartCoroutine(FadeOutRoutine(onFinished));
        }

        private IEnumerator FadeOutRoutine(Action onFinished)
        {
            yield return new WaitForSecondsRealtime(0.4f);

            _isFadingOut = true;
            while (_currentAlpha > 0.01f)
            {
                _currentAlpha = Mathf.MoveTowards(_currentAlpha, 0f, Time.unscaledDeltaTime * fadeOutSpeed);
                yield return null;
            }

            _currentAlpha = 0f;
            isGenerating = false;
            _isFadingOut = false;
            onFinished?.Invoke();
            gameObject.SetActive(false);

            if (!Duskborn.Gameplay.Player.PlayerCameraController.IsAnyMenuOpen())
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                if (Duskborn.Gameplay.Player.PlayerCameraController.LocalInstance != null)
                {
                    Duskborn.Gameplay.Player.PlayerCameraController.LocalInstance.SetRotationLocked(false);
                    Duskborn.Gameplay.Player.PlayerCameraController.LocalInstance.SetCursorLocked(true);
                }
            }
        }

        private void InitEmbers()
        {
            _embers = new DuskEmber[32];
            for (int i = 0; i < _embers.Length; i++)
            {
                _embers[i] = new DuskEmber
                {
                    xRatio = UnityEngine.Random.value,
                    yRatio = UnityEngine.Random.value,
                    speed = UnityEngine.Random.Range(0.04f, 0.12f),
                    size = UnityEngine.Random.Range(3f, 7f),
                    seed = UnityEngine.Random.Range(0f, 100f)
                };
            }
        }

        private void Update()
        {
            if (!isGenerating && _currentAlpha <= 0.001f) return;

            // Interpolação suave do progresso
            currentProgress = Mathf.MoveTowards(currentProgress, targetProgress, Time.unscaledDeltaTime * 0.75f);

            // Rotação suave da estrela rúnica medieval
            _starRotation += Time.unscaledDeltaTime * 45f;
            _shimmerOffset += Time.unscaledDeltaTime * 1.5f;

            // Movimento das brasas atmosféricas
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

            // Rotação dos ensinamentos de sobrevivência
            _tipTimer += Time.unscaledDeltaTime;
            if (_tipTimer >= 5f)
            {
                _tipTimer = 0f;
                _currentTipIndex = (_currentTipIndex + 1) % SurvivalTips.Length;
            }
        }

        private void OnGUI()
        {
            if (!isGenerating && _currentAlpha <= 0.001f) return;

            InitStyles();

            Matrix4x4 origMatrix = GUI.matrix;
            float scale = Screen.height / 1080f;
            if (scale <= 0.001f) scale = 1f;
            float virtualW = Screen.width / scale;
            float virtualH = 1080f;

            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

            Color prevGuiColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, _currentAlpha);

            // 1. Fundo de Ardósia/Obsidiana Gótica
            GUI.DrawTexture(new Rect(0, 0, virtualW, virtualH), _slateTexture, ScaleMode.StretchToFill);

            // 2. Brasas Místicas Flutuantes
            DrawEmbers(virtualW, virtualH);

            // 3. Moldura Ornamental e Cantoneiras de Ferro & Ouro Low-Poly
            DrawMedievalScreenBorders(virtualW, virtualH);

            // 4. Brasão & Título Heraldico Principal
            float centerY = virtualH * 0.32f;
            GUI.Label(new Rect(0, centerY - 80, virtualW, 42), "❖  D U S K B O R N  ❖", _crestTitleStyle);
            GUI.Label(new Rect(0, centerY - 32, virtualW, 22), "✦ FORJANDO O REINO DO CREPÚSCULO ✦", _crestSubStyle);

            // 5. Linha divisória de ouro com runa central
            DrawRunicDivider((virtualW - 420f) * 0.5f, centerY - 4f, 420f);

            // 6. Título da Fase Atual
            GUI.Label(new Rect(0, centerY + 18, virtualW, 30), stageTitle, _stageStyle);

            // 7. Barra de Progresso em Ferro Forjado e Cristal de Âmbar
            float barW = Mathf.Min(virtualW * 0.65f, 620f);
            float barH = 32f;
            float barX = (virtualW - barW) * 0.5f;
            float barY = centerY + 62f;

            DrawMedievalProgressBar(barX, barY, barW, barH, currentProgress);

            // 8. Percentual Rúnico Destacado
            int percentInt = Mathf.Clamp(Mathf.RoundToInt(currentProgress * 100f), 0, 100);
            GUI.Label(new Rect(0, barY + barH + 10f, virtualW, 28), $"◈  {percentInt}%  ◈", _percentStyle);

            // 9. Detalhe Geológico / Operação Técnica
            GUI.Label(new Rect(0, barY + barH + 42f, virtualW, 22), stageDetail, _detailStyle);

            // 10. Bússola Rúnica Low-Poly Giratória no canto inferior direito
            DrawLowPolyRunicStar(virtualW - 85f, virtualH - 85f, 54f);

            // 11. Placa de Pergaminho com Ensinamento de Sobrevivência
            float plaqueW = Mathf.Min(virtualW * 0.85f, 780f);
            float plaqueH = 46f;
            float plaqueX = (virtualW - plaqueW) * 0.5f;
            float plaqueY = virtualH - plaqueH - 22f;

            DrawLorePlaque(plaqueX, plaqueY, plaqueW, plaqueH, SurvivalTips[_currentTipIndex]);

            GUI.color = prevGuiColor;
            GUI.matrix = origMatrix;
        }

        private void DrawEmbers(float w, float h)
        {
            if (_embers == null || _emberTexture == null) return;

            Color oldColor = GUI.color;
            for (int i = 0; i < _embers.Length; i++)
            {
                float wobble = Mathf.Sin(Time.realtimeSinceStartup * 1.5f + _embers[i].seed) * 22f;
                float ex = _embers[i].xRatio * w + wobble;
                float ey = _embers[i].yRatio * h;

                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.realtimeSinceStartup * 3f + _embers[i].seed);
                GUI.color = new Color(1f, 0.72f, 0.28f, (0.35f + 0.55f * pulse) * _currentAlpha);
                GUI.DrawTexture(new Rect(ex, ey, _embers[i].size, _embers[i].size), _emberTexture);
            }
            GUI.color = oldColor;
        }

        private void DrawMedievalScreenBorders(float w, float h)
        {
            // Borda externa de ferro forjado
            GUI.DrawTexture(new Rect(0, 0, w, 6), _ironFrameTexture);
            GUI.DrawTexture(new Rect(0, h - 6, w, 6), _ironFrameTexture);
            GUI.DrawTexture(new Rect(0, 0, 6, h), _ironFrameTexture);
            GUI.DrawTexture(new Rect(w - 6, 0, 6, h), _ironFrameTexture);

            // Friso de ouro interior
            GUI.DrawTexture(new Rect(18, 18, w - 36, 1), _goldTrimTexture);
            GUI.DrawTexture(new Rect(18, h - 19, w - 36, 1), _goldTrimTexture);
            GUI.DrawTexture(new Rect(18, 18, 1, h - 36), _goldTrimTexture);
            GUI.DrawTexture(new Rect(w - 19, 18, 1, h - 36), _goldTrimTexture);

            // Cantoneiras ornamentadas de ouro nos 4 cantos
            float cornerSize = 24f;
            DrawCornerBracket(16, 16, cornerSize, cornerSize, true, true);
            DrawCornerBracket(w - 16 - cornerSize, 16, cornerSize, cornerSize, false, true);
            DrawCornerBracket(16, h - 16 - cornerSize, cornerSize, cornerSize, true, false);
            DrawCornerBracket(w - 16 - cornerSize, h - 16 - cornerSize, cornerSize, cornerSize, false, false);
        }

        private void DrawCornerBracket(float x, float y, float w, float h, bool left, bool top)
        {
            GUI.DrawTexture(new Rect(x, y, w, 3), _goldTrimTexture);
            GUI.DrawTexture(new Rect(x, y, 3, h), _goldTrimTexture);
            // Rebite de ferro dourado no vértice
            float dotX = left ? x : x + w - 5;
            float dotY = top ? y : y + h - 5;
            GUI.DrawTexture(new Rect(dotX - 1, dotY - 1, 6, 6), _goldTrimTexture);
        }

        private void DrawRunicDivider(float x, float y, float width)
        {
            float halfW = width * 0.44f;
            GUI.DrawTexture(new Rect(x, y + 5, halfW, 2), _goldTrimTexture);
            GUI.DrawTexture(new Rect(x + width - halfW, y + 5, halfW, 2), _goldTrimTexture);
            // Losango / Runa central
            GUI.DrawTexture(new Rect(x + halfW + 10f, y + 2, 8, 8), _goldTrimTexture);
        }

        private void DrawMedievalProgressBar(float x, float y, float width, float height, float progress)
        {
            // 1. Moldura externa de ferro forjado
            GUI.DrawTexture(new Rect(x - 4, y - 4, width + 8, height + 8), _ironFrameTexture);

            // 2. Friso biselado de ouro
            GUI.DrawTexture(new Rect(x - 2, y - 2, width + 4, height + 4), _goldTrimTexture);

            // 3. Calha em pedra escura
            GUI.DrawTexture(new Rect(x, y, width, height), _barTroughTexture);

            // 4. Preenchimento em Cristal de Âmbar
            float fillWidth = Mathf.Clamp(width * progress, 0f, width);
            if (fillWidth > 2f)
            {
                GUI.DrawTexture(new Rect(x, y, fillWidth, height), _amberFillTexture);

                // Brilho pulsante no topo
                GUI.DrawTexture(new Rect(x, y, fillWidth, 3f), _goldTrimTexture);

                // Shimmer glint que viaja pela barra
                float shimmerX = x + Mathf.Repeat(_shimmerOffset * width, width);
                if (shimmerX < x + fillWidth)
                {
                    float glintW = Mathf.Min(35f, x + fillWidth - shimmerX);
                    Color prev = GUI.color;
                    GUI.color = new Color(1f, 1f, 0.8f, 0.4f * _currentAlpha);
                    GUI.DrawTexture(new Rect(shimmerX, y, glintW, height), _goldTrimTexture);
                    GUI.color = prev;
                }
            }

            // Rebites nos quatro cantos da barra
            GUI.DrawTexture(new Rect(x - 6, y - 6, 5, 5), _goldTrimTexture);
            GUI.DrawTexture(new Rect(x + width + 1, y - 6, 5, 5), _goldTrimTexture);
            GUI.DrawTexture(new Rect(x - 6, y + height + 1, 5, 5), _goldTrimTexture);
            GUI.DrawTexture(new Rect(x + width + 1, y + height + 1, 5, 5), _goldTrimTexture);
        }

        private void DrawLowPolyRunicStar(float cx, float cy, float size)
        {
            if (_runicStarTexture == null) return;

            Matrix4x4 savedMatrix = GUI.matrix;
            Vector2 pivot = new Vector2(cx + size * 0.5f, cy + size * 0.5f);
            GUIUtility.RotateAroundPivot(_starRotation, pivot);

            // Estrela rúnica facetada com iluminação de normais simulada
            float pulse = 0.88f + 0.12f * Mathf.Sin(Time.realtimeSinceStartup * 4f);
            Color prev = GUI.color;
            GUI.color = new Color(1f, 0.9f, 0.65f, pulse * _currentAlpha);
            GUI.DrawTexture(new Rect(cx, cy, size, size), _runicStarTexture);
            GUI.color = prev;

            GUI.matrix = savedMatrix;
        }

        private void DrawLorePlaque(float x, float y, float w, float h, string text)
        {
            // Fundo escuro de pergaminho/ardósia
            GUI.DrawTexture(new Rect(x, y, w, h), _parchmentPlaqueTexture);

            // Friso sutil de ouro na placa
            GUI.DrawTexture(new Rect(x, y, w, 1), _goldTrimTexture);
            GUI.DrawTexture(new Rect(x, y + h - 1, w, 1), _goldTrimTexture);

            // Texto do ensinamento
            GUI.Label(new Rect(x + 16, y + 6, w - 32, h - 12), text, _loreStyle);
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;

            // 1. Texturas Procedurais
            _slateTexture = CreateSlateTexture(32, 32, new Color(0.08f, 0.09f, 0.12f), new Color(0.05f, 0.06f, 0.08f));
            _ironFrameTexture = CreateSolidTexture(new Color(0.14f, 0.16f, 0.20f, 1f));
            _goldTrimTexture = CreateSolidTexture(new Color(0.85f, 0.72f, 0.35f, 0.95f));
            _amberFillTexture = CreateAmberGradientTexture(48, 8);
            _barTroughTexture = CreateSolidTexture(new Color(0.04f, 0.05f, 0.07f, 1f));
            _emberTexture = CreateEmberTexture(8);
            _parchmentPlaqueTexture = CreateSolidTexture(new Color(0.09f, 0.10f, 0.14f, 0.90f));
            _runicStarTexture = CreateLowPolyRunicStarTexture(64);

            // 2. Estilos Tipográficos Medieval/Fantasy
            _crestTitleStyle = new GUIStyle
            {
                fontSize = 32,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _crestTitleStyle.normal.textColor = new Color(0.95f, 0.85f, 0.50f);

            _crestSubStyle = new GUIStyle
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _crestSubStyle.normal.textColor = new Color(0.55f, 0.75f, 0.95f);

            _stageStyle = new GUIStyle
            {
                fontSize = 19,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _stageStyle.normal.textColor = new Color(0.95f, 0.96f, 0.98f);

            _percentStyle = new GUIStyle
            {
                fontSize = 21,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _percentStyle.normal.textColor = new Color(0.95f, 0.78f, 0.32f);

            _detailStyle = new GUIStyle
            {
                fontSize = 13,
                fontStyle = FontStyle.Italic,
                alignment = TextAnchor.MiddleCenter
            };
            _detailStyle.normal.textColor = new Color(0.65f, 0.72f, 0.82f);

            _loreStyle = new GUIStyle
            {
                fontSize = 13,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            _loreStyle.normal.textColor = new Color(0.90f, 0.88f, 0.82f);

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
                    // Padrão de lajotas de pedra/ardósia com veios suaves
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

        private Texture2D CreateAmberGradientTexture(int w, int h)
        {
            Texture2D tex = new Texture2D(w, h);
            Color cLeft = new Color(0.85f, 0.45f, 0.12f);   // Âmbar quente
            Color cMid = new Color(0.98f, 0.75f, 0.22f);    // Ouro radiante
            Color cRight = new Color(0.92f, 0.88f, 0.45f);  // Cristal luz

            Color[] cols = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float t = (float)x / (w - 1);
                    Color c = t < 0.6f ? Color.Lerp(cLeft, cMid, t / 0.6f) : Color.Lerp(cMid, cRight, (t - 0.6f) / 0.4f);
                    // Destaque na borda superior para sensação chanfrada
                    if (y >= h - 2) c = Color.Lerp(c, Color.white, 0.45f);
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

        private Texture2D CreateLowPolyRunicStarTexture(int size)
        {
            Texture2D tex = new Texture2D(size, size);
            Color[] cols = new Color[size * size];
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float maxR = (size - 1) * 0.48f;

            Color cLit = new Color(1f, 0.88f, 0.45f, 1f);     // Ouro iluminado
            Color cMid = new Color(0.85f, 0.65f, 0.25f, 1f);    // Âmbar base
            Color cShadow = new Color(0.55f, 0.38f, 0.12f, 1f); // Sombra de oclusão
            Color cCore = new Color(0.98f, 0.95f, 0.85f, 1f);   // Núcleo cristalino

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x, y) - center;
                    float dist = p.magnitude;
                    if (dist > maxR)
                    {
                        cols[y * size + x] = Color.clear;
                        continue;
                    }

                    float angle = Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg;
                    if (angle < 0) angle += 360f;

                    // 8 pontas (45 graus cada ponta)
                    float segment = angle % 45f;
                    float radiusAtAngle = maxR * (0.35f + 0.65f * Mathf.Cos((segment - 22.5f) * Mathf.Deg2Rad * 4f));
                    if (dist > radiusAtAngle)
                    {
                        cols[y * size + x] = Color.clear;
                        continue;
                    }

                    // Shading facetado Low-Poly: metade da ponta iluminada, metade sombreada
                    bool isLitFacet = (segment < 22.5f);
                    Color facetCol = isLitFacet ? cLit : cShadow;

                    // Núcleo radiante
                    if (dist < maxR * 0.28f)
                    {
                        facetCol = Color.Lerp(cCore, cMid, dist / (maxR * 0.28f));
                    }

                    cols[y * size + x] = facetCol;
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
            if (_amberFillTexture != null) Destroy(_amberFillTexture);
            if (_barTroughTexture != null) Destroy(_barTroughTexture);
            if (_runicStarTexture != null) Destroy(_runicStarTexture);
            if (_emberTexture != null) Destroy(_emberTexture);
            if (_parchmentPlaqueTexture != null) Destroy(_parchmentPlaqueTexture);
        }
    }
}
