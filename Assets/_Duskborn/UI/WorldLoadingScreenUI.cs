using System;
using System.Collections;
using UnityEngine;

namespace Duskborn.UI
{
    /// <summary>
    /// Stylized procedural loading screen with a Medieval / Fantasy Low-Poly theme ("Grimoire & Runic Compass").
    /// Features wrought iron and aged gold frames, a rotating faceted 3D runic compass / star,
    /// floating twilight ember particles, an amber / crystal progress bar with dynamic glow,
    /// epic world-forging titles, and ancestral survival teachings.
    /// Works entirely through OnGUI with dynamically generated textures, without external prefab dependencies.
    /// </summary>
    public class WorldLoadingScreenUI : MonoBehaviour
    {
        public static WorldLoadingScreenUI Instance { get; private set; }
        public static bool IsBlockingGameplay => Instance != null &&
            Instance.gameObject.activeInHierarchy && Instance.isGenerating;
 
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }

        [Header("Loading State")]
        [Range(0f, 1f)] public float targetProgress = 0f;
        public float currentProgress = 0f;
        public string stageTitle = "Summoning the Twilight...";
        public string stageDetail = "Awakening the seeds of the ancestral world...";
        public bool isGenerating = false;

        [Header("Visual Configuration")]
        public bool autoFadeOut = true;
        public float fadeOutSpeed = 2.2f;
        [Tooltip("Seconds used to gently reveal gameplay after the local player camera is ready.")]
        [Min(0.1f)] public float gameplayRevealDuration = 3f;

        private float _currentAlpha = 1f;
        public float CurrentAlpha => _currentAlpha;
        private bool _isFadingOut = false;
        private Coroutine _completionRoutine;
        public bool IsFadingOut => _isFadingOut;
        private float _tipTimer = 0f;
        private int _currentTipIndex = 0;
        private float _starRotation = 0f;
        private float _shimmerOffset = 0f;

        // Atmospheric twilight ember particles.
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
            "✦ In the golden hours of Twilight, stockpile wood and iron; darkness hides ravenous horrors.",
            "✦ The Central Sanctuary radiates a sacred barrier. Protect the heart of your refuge at any cost.",
            "✦ Pure steel blades break through heavy armor and pierce the shells of abominations.",
            "✦ Tactical dodging [Space] consumes stamina, but grants temporary immunity to crushing strikes.",
            "✦ Ancient chests scattered across the wilds hold lost relics of fallen kings.",
            "✦ Keep campfires and torches lit: light is the only boundary that creatures of the night hesitate to cross.",
            "✦ The workbench in the clearing lets you forge arcane weapons by combining fine wood and refined ore."
        };

        // Procedural textures with a Medieval Fantasy Low-Poly aesthetic.
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
            if (Application.isPlaying) DontDestroyOnLoad(gameObject);
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

        public void Show(string initialStage = "Forging the Ancient Terrain...", string initialDetail = "Allocating geological data...")
        {
            CancelCompletion();
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
            if (_completionRoutine != null) return;
            targetProgress = 1.0f;
            stageTitle = "The Twilight Reveals Itself!";
            stageDetail = "World consecrated. Entering the wild lands...";
            _completionRoutine = StartCoroutine(FadeOutRoutine(onFinished));
        }

        private void CancelCompletion()
        {
            if (_completionRoutine != null) StopCoroutine(_completionRoutine);
            _completionRoutine = null;
            _isFadingOut = false;
        }

        private void OnDisable()
        {
            CancelCompletion();
            isGenerating = false;
            _currentAlpha = 0f;
        }

        private bool IsReadyToReveal()
        {
            var world = ChunkGridManager.Instance;
            if (world != null && !world.IsWorldReady) return false;
            var network = FishNet.InstanceFinder.NetworkManager;
            if (network == null) return true;
            // A dedicated server has no local player or view to reveal.
            if (Application.isBatchMode && !network.ClientManager.Started) return true;
            var camera = Duskborn.Gameplay.Player.PlayerCameraController.LocalInstance;
            return network.ClientManager.Started && camera != null && camera.IsReadyForWorldReveal;
        }

        private IEnumerator FadeOutRoutine(Action onFinished)
        {
            stageDetail = "Preparing your arrival in the sanctuary...";
            while (!IsReadyToReveal()) yield return null;
            // Let camera LateUpdate and the first rendered world frame settle behind
            // the opaque loading screen before exposing the player view.
            yield return null;
            yield return new WaitForSecondsRealtime(0.4f);
            currentProgress = 1f;

            _isFadingOut = true;
            float elapsed = 0f;
            float duration = Mathf.Max(0.1f, gameplayRevealDuration);
            while (elapsed < duration)
            {
                // Regeneration during the transition must not expose an unfinished world.
                if (!IsReadyToReveal())
                {
                    _currentAlpha = 1f;
                    elapsed = 0f;
                    yield return null;
                    continue;
                }
                elapsed += Time.unscaledDeltaTime;
                _currentAlpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            _currentAlpha = 0f;
            isGenerating = false;
            _isFadingOut = false;
            _completionRoutine = null;
            gameObject.SetActive(false);
            onFinished?.Invoke();

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

            // Smooth progress interpolation.
            currentProgress = Mathf.MoveTowards(currentProgress, targetProgress, Time.unscaledDeltaTime * 0.75f);

            // Smooth medieval runic star rotation.
            _starRotation += Time.unscaledDeltaTime * 45f;
            _shimmerOffset += Time.unscaledDeltaTime * 1.5f;

            // Atmospheric ember movement.
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

            // Rotate survival teachings.
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
            int previousDepth = GUI.depth;
            GUI.depth = -1000;
            float scale = Screen.height / 1080f;
            if (scale <= 0.001f) scale = 1f;
            float virtualW = Screen.width / scale;
            float virtualH = 1080f;

            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

            Color prevGuiColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, _currentAlpha);

            // 1. Gothic Slate / Obsidian Background.
            GUI.DrawTexture(new Rect(0, 0, virtualW, virtualH), _slateTexture, ScaleMode.StretchToFill);

            // 2. Floating Mystical Embers.
            DrawEmbers(virtualW, virtualH);

            // 3. Ornamental Frame and Low-Poly Iron & Gold Corners.
            DrawMedievalScreenBorders(virtualW, virtualH);

            // 4. Main Heraldic Crest & Title.
            float centerY = virtualH * 0.32f;
            GUI.Label(new Rect(0, centerY - 80, virtualW, 42), "❖  D U S K B O R N  ❖", _crestTitleStyle);
            GUI.Label(new Rect(0, centerY - 32, virtualW, 22), "✦ FORGING THE TWILIGHT REALM ✦", _crestSubStyle);

            // 5. Gold divider line with central rune.
            DrawRunicDivider((virtualW - 420f) * 0.5f, centerY - 4f, 420f);

            // 6. Current Phase Title.
            GUI.Label(new Rect(0, centerY + 18, virtualW, 30), stageTitle, _stageStyle);

            // 7. Wrought Iron and Amber Crystal Progress Bar.
            float barW = Mathf.Min(virtualW * 0.65f, 620f);
            float barH = 32f;
            float barX = (virtualW - barW) * 0.5f;
            float barY = centerY + 62f;

            DrawMedievalProgressBar(barX, barY, barW, barH, currentProgress);

            // 8. Highlighted Runic Percentage.
            int percentInt = Mathf.Clamp(Mathf.RoundToInt(currentProgress * 100f), 0, 100);
            GUI.Label(new Rect(0, barY + barH + 10f, virtualW, 28), $"◈  {percentInt}%  ◈", _percentStyle);

            // 9. Geological Detail / Technical Operation.
            GUI.Label(new Rect(0, barY + barH + 42f, virtualW, 22), stageDetail, _detailStyle);

            // 10. Rotating Low-Poly Runic Compass in the lower-right corner.
            DrawLowPolyRunicStar(virtualW - 85f, virtualH - 85f, 54f);

            // 11. Parchment Plaque with Survival Teaching.
            float plaqueW = Mathf.Min(virtualW * 0.85f, 780f);
            float plaqueH = 46f;
            float plaqueX = (virtualW - plaqueW) * 0.5f;
            float plaqueY = virtualH - plaqueH - 22f;

            DrawLorePlaque(plaqueX, plaqueY, plaqueW, plaqueH, SurvivalTips[_currentTipIndex]);

            GUI.color = prevGuiColor;
            GUI.matrix = origMatrix;
            GUI.depth = previousDepth;
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
            // Outer wrought iron border.
            GUI.DrawTexture(new Rect(0, 0, w, 6), _ironFrameTexture);
            GUI.DrawTexture(new Rect(0, h - 6, w, 6), _ironFrameTexture);
            GUI.DrawTexture(new Rect(0, 0, 6, h), _ironFrameTexture);
            GUI.DrawTexture(new Rect(w - 6, 0, 6, h), _ironFrameTexture);

            // Inner gold trim.
            GUI.DrawTexture(new Rect(18, 18, w - 36, 1), _goldTrimTexture);
            GUI.DrawTexture(new Rect(18, h - 19, w - 36, 1), _goldTrimTexture);
            GUI.DrawTexture(new Rect(18, 18, 1, h - 36), _goldTrimTexture);
            GUI.DrawTexture(new Rect(w - 19, 18, 1, h - 36), _goldTrimTexture);

            // Ornamental gold brackets at all 4 corners.
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
            // Golden iron rivet at the corner.
            float dotX = left ? x : x + w - 5;
            float dotY = top ? y : y + h - 5;
            GUI.DrawTexture(new Rect(dotX - 1, dotY - 1, 6, 6), _goldTrimTexture);
        }

        private void DrawRunicDivider(float x, float y, float width)
        {
            float halfW = width * 0.44f;
            GUI.DrawTexture(new Rect(x, y + 5, halfW, 2), _goldTrimTexture);
            GUI.DrawTexture(new Rect(x + width - halfW, y + 5, halfW, 2), _goldTrimTexture);
            // Diamond / Central rune
            GUI.DrawTexture(new Rect(x + halfW + 10f, y + 2, 8, 8), _goldTrimTexture);
        }

        private void DrawMedievalProgressBar(float x, float y, float width, float height, float progress)
        {
            // 1. Outer wrought iron frame.
            GUI.DrawTexture(new Rect(x - 4, y - 4, width + 8, height + 8), _ironFrameTexture);

            // 2. Beveled gold trim.
            GUI.DrawTexture(new Rect(x - 2, y - 2, width + 4, height + 4), _goldTrimTexture);

            // 3. Dark stone track.
            GUI.DrawTexture(new Rect(x, y, width, height), _barTroughTexture);

            // 4. Amber Crystal Fill.
            float fillWidth = Mathf.Clamp(width * progress, 0f, width);
            if (fillWidth > 2f)
            {
                GUI.DrawTexture(new Rect(x, y, fillWidth, height), _amberFillTexture);

                // Pulsing glow at the top.
                GUI.DrawTexture(new Rect(x, y, fillWidth, 3f), _goldTrimTexture);

                // Shimmer glint traveling along the bar.
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

            // Rivets at the bar's four corners.
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

            // Faceted runic star with simulated normal lighting.
            float pulse = 0.88f + 0.12f * Mathf.Sin(Time.realtimeSinceStartup * 4f);
            Color prev = GUI.color;
            GUI.color = new Color(1f, 0.9f, 0.65f, pulse * _currentAlpha);
            GUI.DrawTexture(new Rect(cx, cy, size, size), _runicStarTexture);
            GUI.color = prev;

            GUI.matrix = savedMatrix;
        }

        private void DrawLorePlaque(float x, float y, float w, float h, string text)
        {
            // Dark parchment / slate background.
            GUI.DrawTexture(new Rect(x, y, w, h), _parchmentPlaqueTexture);

            // Subtle gold trim on the plaque.
            GUI.DrawTexture(new Rect(x, y, w, 1), _goldTrimTexture);
            GUI.DrawTexture(new Rect(x, y + h - 1, w, 1), _goldTrimTexture);

            // Teaching text.
            GUI.Label(new Rect(x + 16, y + 6, w - 32, h - 12), text, _loreStyle);
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;

            // 1. Procedural Textures
            _slateTexture = CreateSlateTexture(32, 32, new Color(0.08f, 0.09f, 0.12f), new Color(0.05f, 0.06f, 0.08f));
            _ironFrameTexture = CreateSolidTexture(new Color(0.14f, 0.16f, 0.20f, 1f));
            _goldTrimTexture = CreateSolidTexture(new Color(0.85f, 0.72f, 0.35f, 0.95f));
            _amberFillTexture = CreateAmberGradientTexture(48, 8);
            _barTroughTexture = CreateSolidTexture(new Color(0.04f, 0.05f, 0.07f, 1f));
            _emberTexture = CreateEmberTexture(8);
            _parchmentPlaqueTexture = CreateSolidTexture(new Color(0.09f, 0.10f, 0.14f, 0.90f));
            _runicStarTexture = CreateLowPolyRunicStarTexture(64);

            // 2. Medieval / Fantasy Typography Styles.
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
                    // Stone / slate tile pattern with subtle veins.
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
            Color cLeft = new Color(0.85f, 0.45f, 0.12f);   // Warm amber.
            Color cMid = new Color(0.98f, 0.75f, 0.22f);    // Radiant gold.
            Color cRight = new Color(0.92f, 0.88f, 0.45f);  // Crystal light.

            Color[] cols = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float t = (float)x / (w - 1);
                    Color c = t < 0.6f ? Color.Lerp(cLeft, cMid, t / 0.6f) : Color.Lerp(cMid, cRight, (t - 0.6f) / 0.4f);
                    // Upper edge highlight for a beveled feel.
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

            Color cLit = new Color(1f, 0.88f, 0.45f, 1f);     // Illuminated gold.
            Color cMid = new Color(0.85f, 0.65f, 0.25f, 1f);    // Base amber.
            Color cShadow = new Color(0.55f, 0.38f, 0.12f, 1f); // Occlusion shadow.
            Color cCore = new Color(0.98f, 0.95f, 0.85f, 1f);   // Crystal core.

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

                    // 8 points (45 degrees per point).
                    float segment = angle % 45f;
                    float radiusAtAngle = maxR * (0.35f + 0.65f * Mathf.Cos((segment - 22.5f) * Mathf.Deg2Rad * 4f));
                    if (dist > radiusAtAngle)
                    {
                        cols[y * size + x] = Color.clear;
                        continue;
                    }

                    // Faceted low-poly shading: half the tip lit, half shaded.
                    bool isLitFacet = (segment < 22.5f);
                    Color facetCol = isLitFacet ? cLit : cShadow;

                    // Radiant core.
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
