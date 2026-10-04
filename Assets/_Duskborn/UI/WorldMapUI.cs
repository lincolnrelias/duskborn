using System.Collections;
using System.Collections.Generic;
using Duskborn.Core;
using Duskborn.Gameplay.Building;
using Duskborn.Gameplay.Hotkeys;
using Duskborn.Gameplay.Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Duskborn.UI
{
    public sealed class WorldMapUI : MonoBehaviour
    {
        public static WorldMapUI Instance { get; private set; }
        public bool IsExpanded { get; private set; }
        public int LastClosedFrame { get; private set; } = -1;
        public float MinimapRadius { get; private set; } = 55;
        public float MapZoom { get; private set; } = 1;
        private Canvas canvas;
        private CanvasScaler mapScaler;
        private RectTransform mini, window, shade, tooltip;
        private WorldMapGraphic miniTerrain, miniMarkers, mapTerrain, mapMarkers;
        private Text coordinates, clock, status, tooltipText;
        private HotkeyManager hotkeys;
        private Texture2D atlas, parchment;
        private Coroutine baking;
        private ChunkGridManager source;
        private LowPolyTerrainConfig config;
        private int seed;
        private float waterLevel, nextRefresh;
        private Rect bounds;
        private Vector2 mapCenter, screenSize;
        private bool showPlayers = true, showBuildings = true;
        private bool hasWaypoint;
        private Vector3 waypoint;
        private readonly Color brass = new Color(.73f, .57f, .30f);
        private sealed class MapControl
        {
            public RectTransform Rect;
            public UnityEngine.Events.UnityAction Action;
            public int LastClickFrame = -1;
        }
        private readonly List<MapControl> minimapControls = new();
        private readonly Vector3[] controlCorners = new Vector3[4];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap() => Ensure();
        public static WorldMapUI Ensure()
        {
            if (Instance != null) return Instance;
            var root = new GameObject("World Map HUD", typeof(RectTransform));
            var map = root.AddComponent<WorldMapUI>();
            if (Application.isPlaying) DontDestroyOnLoad(root);
            return map;
        }
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (GetComponent<RectTransform>() == null) gameObject.AddComponent<RectTransform>();
            canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 95;
            var scaler = mapScaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 1f;
            gameObject.AddComponent<GraphicRaycaster>();
            BuildMinimap(); BuildWorldMap();
            tooltip = Box("Marker tooltip", transform, new Vector2(260, 54), new Color(.035f, .028f, .02f, .96f));
            tooltipText = Label("", tooltip, Vector2.zero, new Vector2(248, 48), 15);
            tooltip.gameObject.SetActive(false);
            Resize(); canvas.enabled = false;
        }
        private void BuildMinimap()
        {
            mini = Rect("Minimap", transform, new Vector2(280, 350));
            mini.anchorMin = mini.anchorMax = mini.pivot = Vector2.one;
            mini.anchoredPosition = new Vector2(-22, -18);
            var title = Box("Region plaque", mini, new Vector2(250, 34), new Color(.06f, .045f, .025f, .96f));
            title.anchoredPosition = new Vector2(0, 145);
            Label("THE WILDS", title, Vector2.zero, new Vector2(244, 32), 18);
            var frame = Graphic("Engraved compass bezel", mini, new Vector2(280, 280)); frame.Frame = true;
            miniTerrain = Graphic("Minimap terrain", mini, new Vector2(244, 244));
            miniMarkers = Graphic("Minimap markers", mini, new Vector2(244, 244)); miniMarkers.Markers = true;
            Surface(miniTerrain, false);
            Label("N", mini, new Vector2(0, 124), new Vector2(28, 24), 18);
            clock = Label("", mini, new Vector2(0, -157), new Vector2(210, 24), 14);
            Button("+", mini, new Vector2(99, -102), new Vector2(30, 30), () => ZoomMinimap(true));
            Button("−", mini, new Vector2(63, -126), new Vector2(30, 30), () => ZoomMinimap(false));
            Button("M", mini, new Vector2(-92, -110), new Vector2(36, 32), Toggle);
            coordinates = Label("", mini, new Vector2(0, -185), new Vector2(280, 24), 13);
        }
        private void BuildWorldMap()
        {
            shade = Box("Map backdrop", transform, Vector2.zero, new Color(0, 0, 0, .64f));
            shade.anchorMin = Vector2.zero; shade.anchorMax = Vector2.one; shade.sizeDelta = Vector2.zero;
            shade.GetComponent<Image>().raycastTarget = true;
            window = Box("Atlas frame", transform, new Vector2(1020, 750), new Color(.10f, .073f, .045f));
            var trim = Box("Gold edging", window, new Vector2(1012, 742), brass);
            var inset = Box("Leather binding", trim, new Vector2(1006, 736), new Color(.12f, .085f, .046f));
            Label("D U S K B O R N   •   W O R L D   M A P", inset, new Vector2(0, 340), new Vector2(910, 42), 24);
            Label("THE WILDS  /  Your shared realm", inset, new Vector2(0, 300), new Vector2(880, 30), 16);
            var mat = Box("Chart border", window, new Vector2(952, 578), new Color(.35f, .24f, .12f));
            mat.anchoredPosition = new Vector2(0, -2);
            mapTerrain = Graphic("Parchment chart", mat, new Vector2(940, 566)); mapTerrain.Square = true;
            mapMarkers = Graphic("World markers", mat, new Vector2(940, 566)); mapMarkers.Square = mapMarkers.Markers = true;
            Surface(mapTerrain, true);
            Label("N", mat, new Vector2(436, 245), new Vector2(30, 32), 24).color = new Color(.25f, .17f, .08f);
            Button("X", window, new Vector2(477, 337), new Vector2(34, 34), CloseExpanded);
            Button("+", window, new Vector2(459, -249), new Vector2(32, 32), () => ZoomMap(true));
            Button("−", window, new Vector2(421, -249), new Vector2(32, 32), () => ZoomMap(false));
            Button("Reset", window, new Vector2(389, -322), new Vector2(76, 30), ResetMap);
            Button("Players", window, new Vector2(-396, -322), new Vector2(108, 30), () => { showPlayers = !showPlayers; RefreshMaps(); });
            Button("Structures", window, new Vector2(-270, -322), new Vector2(128, 30), () => { showBuildings = !showBuildings; RefreshMaps(); });
            status = Label("", window, new Vector2(0, -318), new Vector2(380, 30), 14);
            Label("Drag to pan • Wheel to zoom • Shift-click to mark • Right-click to clear", window,
                new Vector2(0, -351), new Vector2(900, 28), 14);
            shade.gameObject.SetActive(false); window.gameObject.SetActive(false);
        }
        private void Surface(WorldMapGraphic surface, bool world)
        {
            surface.raycastTarget = true;
            var input = surface.gameObject.AddComponent<WorldMapSurface>(); input.Owner = this; input.World = world;
        }
        private void Update()
        {
            EnsureCanvasScale();
            bool visible = LocalPlayerContext.HasLocalPlayer && ChunkGridManager.Instance != null;
            canvas.enabled = visible;
            if (!visible) { if (IsExpanded) CloseExpanded(); return; }
            if (EventSystem.current == null && Application.isPlaying)
                new GameObject("Map EventSystem", typeof(EventSystem), typeof(StandaloneInputModule)).transform.SetParent(transform, false);
            if (screenSize != new Vector2(Screen.width, Screen.height)) Resize();
            if (hotkeys != HotkeyManager.Instance)
            {
                if (hotkeys != null) hotkeys.Unregister(HotkeyManager.Map, Toggle);
                hotkeys = HotkeyManager.Instance;
                if (hotkeys != null) hotkeys.Register(HotkeyManager.Map, Toggle);
            }
            if (hotkeys == null && Input.GetKeyDown(KeyCode.M)) Toggle();
            var manager = ChunkGridManager.Instance;
            if (source != manager || config != manager.config || seed != manager.ActiveSeed || !Mathf.Approximately(waterLevel, manager.EffectiveWaterLevel))
            {
                source = manager; SetSource(manager.config, manager.ActiveSeed, manager.EffectiveWaterLevel);
                if (baking != null) StopCoroutine(baking);
                if (config != null) baking = StartCoroutine(BakeAtlas());
            }
            if (Time.unscaledTime >= nextRefresh)
            {
                nextRefresh = Time.unscaledTime + .05f; RefreshMaps();
            }
            RefreshTooltip();
        }
        private void SetSource(LowPolyTerrainConfig value, int worldSeed, float water)
        {
            config = value; seed = worldSeed; waterLevel = water;
            if (config != null) bounds = WorldMapProjection.TerrainBounds(config);
            ResetMap(); hasWaypoint = false;
        }
        public void Toggle()
        {
            if (IsExpanded) { CloseExpanded(); return; }
            if (!canvas.enabled) return;
            var pause = InGameMenuController.Instance;
            if (pause != null && pause.IsOpen) pause.CloseMenu(false);
            if (PlayerCameraController.IsAnyMenuOpen()) return;
            IsExpanded = true; shade.gameObject.SetActive(true); window.gameObject.SetActive(true);
            PlayerCameraController.LocalInstance?.SetRotationLocked(true);
            PlayerCameraController.LocalInstance?.SetCursorLocked(false);
            if (Application.isPlaying) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            RefreshMaps();
        }
        public void CloseExpanded()
        {
            if (!IsExpanded) return;
            IsExpanded = false; LastClosedFrame = Time.frameCount;
            shade.gameObject.SetActive(false); window.gameObject.SetActive(false); tooltip.gameObject.SetActive(false);
            if (!PlayerCameraController.IsAnyMenuOpen())
            {
                PlayerCameraController.LocalInstance?.SetRotationLocked(false);
                PlayerCameraController.LocalInstance?.SetCursorLocked(true);
            }
            RefreshMaps();
        }
        public void ZoomMinimap(bool closer) { MinimapRadius = Mathf.Clamp(MinimapRadius * (closer ? .75f : 1.333333f), 20, 160); RefreshMaps(); }
        // Camera input runs before EventSystem on some frames. Test actual canvas
        // geometry rather than its cached pointer-over result before reclaiming focus.
        public bool ContainsScreenPoint(Vector2 screenPoint)
        {
            if (canvas == null || !canvas.isActiveAndEnabled) return false;
            return IsExpanded || (mini != null && mini.gameObject.activeInHierarchy &&
                RectTransformUtility.RectangleContainsScreenPoint(mini, screenPoint, null));
        }
        public void ZoomMap(bool closer) { MapZoom = Mathf.Clamp(MapZoom * (closer ? 1.3f : 1 / 1.3f), 1, 8); RefreshMaps(); }
        public void ResetMap() { MapZoom = 1; mapCenter = bounds.center; RefreshMaps(); }
        public void Pan(Vector2 pixels)
        {
            float ratio = mapTerrain.WorldRadius / (mapTerrain.rectTransform.rect.height * .5f);
            mapCenter -= pixels * ratio / Mathf.Max(.01f, mapTerrain.rectTransform.lossyScale.x);
            mapCenter = new Vector2(Mathf.Clamp(mapCenter.x, bounds.xMin, bounds.xMax), Mathf.Clamp(mapCenter.y, bounds.yMin, bounds.yMax));
            RefreshMaps();
        }
        public void Mark(Vector2 screenPoint, bool world)
        {
            var graphic = world ? mapTerrain : miniTerrain;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(graphic.rectTransform, screenPoint, null, out var point);
            Vector2 position = graphic.Center + point / (graphic.rectTransform.rect.height * .5f) * graphic.WorldRadius;
            waypoint = new Vector3(position.x, 0, position.y); hasWaypoint = true; RefreshMaps();
        }
        public void ClearWaypoint() { hasWaypoint = false; RefreshMaps(); }
        private void RefreshMaps()
        {
            if (miniTerrain == null || mapTerrain == null) return;
            Vector3 position = LocalPlayerContext.HasLocalPlayer ? LocalPlayerContext.Controller.transform.position : Vector3.zero;
            miniTerrain.Center = miniMarkers.Center = new Vector2(position.x, position.z);
            miniTerrain.WorldRadius = miniMarkers.WorldRadius = MinimapRadius;
            float aspect = mapTerrain.rectTransform.rect.width / mapTerrain.rectTransform.rect.height;
            float radius = Mathf.Max(bounds.height, bounds.width / aspect) * .55f / MapZoom;
            mapTerrain.Center = mapMarkers.Center = mapCenter;
            mapTerrain.WorldRadius = mapMarkers.WorldRadius = Mathf.Max(1, radius);
            foreach (var graphic in new[] { miniMarkers, mapMarkers })
            {
                graphic.ShowBuildings = showBuildings; graphic.ShowPlayers = showPlayers;
                graphic.HasWaypoint = hasWaypoint; graphic.Waypoint = waypoint;
            }
            miniTerrain.SetVerticesDirty(); miniMarkers.SetVerticesDirty(); mapTerrain.SetVerticesDirty(); mapMarkers.SetVerticesDirty();
            coordinates.text = $"{position.x:0}, {position.z:0}   •   {MinimapRadius:0}m radius";
            clock.text = DayNightCycle.Instance != null ? DayNightCycle.Instance.ClockTimeString : "The Wilds";
            status.text = hasWaypoint ? $"Waypoint  •  {Vector2.Distance(new Vector2(position.x, position.z), new Vector2(waypoint.x, waypoint.z)):0}m" :
                $"Players {(showPlayers ? "on" : "off")}   •   Structures {(showBuildings ? "on" : "off")}   •   {MapZoom:0.0}×";
        }
        private void RefreshTooltip()
        {
            if (!Cursor.visible) { tooltip.gameObject.SetActive(false); return; }
            var graphic = IsExpanded ? mapMarkers : miniMarkers;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(graphic.rectTransform, Input.mousePosition, null, out var cursor);
            float radius = graphic.rectTransform.rect.height * .5f;
            string title = null;
            if (graphic.Contains(cursor / radius, radius, .98f))
            {
                foreach (var player in PlayerRegistry.All)
                    if (player != null && (showPlayers || player == LocalPlayerContext.Stats) &&
                        (graphic.MarkerPosition(player.transform.position) - cursor).sqrMagnitude < 225)
                        title = player == LocalPlayerContext.Stats ? "You" : $"Player {player.OwnerId} {(player.IsAlive ? "" : "(downed)")}";
                if (showBuildings && BuildingWorld.Instance != null)
                    foreach (var building in BuildingWorld.Instance.Buildings.Values)
                        if (building != null && building.Definition != null && building.State != null &&
                            (WorldMapProjection.Project(building.State.position, graphic.Center, graphic.WorldRadius) * radius - cursor).sqrMagnitude < 225)
                            title = building.Definition.displayName;
            }
            tooltip.gameObject.SetActive(title != null);
            if (title == null) return;
            tooltipText.text = title;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, Input.mousePosition, null, out var point);
            tooltip.anchoredPosition = point + new Vector2(-140, -38);
        }
        private void Resize()
        {
            screenSize = new Vector2(Screen.width, Screen.height);
            float scale = Mathf.Max(1, Screen.height) / 1080f;
            float fit = Mathf.Min(1, Mathf.Min(Screen.width / scale / 1080f, Screen.height / scale / 810f));
            window.localScale = Vector3.one * fit;
        }
        public static bool OwnsCanvas(Canvas value) => value != null && value.GetComponent<WorldMapUI>() != null;
        private void EnsureCanvasScale()
        {
            if (mapScaler == null) mapScaler = GetComponent<CanvasScaler>();
            if (mapScaler == null) return;
            if (mapScaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize &&
                mapScaler.referenceResolution == new Vector2(1920, 1080) && mapScaler.matchWidthOrHeight == 1f) return;
            mapScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            mapScaler.referenceResolution = new Vector2(1920, 1080);
            mapScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            mapScaler.matchWidthOrHeight = 1f;
            Resize();
        }
        private IEnumerator BakeAtlas()
        {
            const int n = 1024;
            float extent = Mathf.Max(bounds.width, bounds.height) + 240;
            Rect uvBounds = new Rect(bounds.center - Vector2.one * extent * .5f, Vector2.one * extent);
            var terrainPixels = new Color32[n * n]; var chartPixels = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float wx = uvBounds.xMin + (x + .5f) / n * extent, wz = uvBounds.yMin + (y + .5f) / n * extent;
                    float h = TerrainNoise.SampleNoise(wx, wz, config, seed);
                    bool inside = bounds.Contains(new Vector2(wx, wz));
                    float dx = inside ? TerrainNoise.SampleNoise(wx + 1, wz, config, seed) - h : 0;
                    float dz = inside ? TerrainNoise.SampleNoise(wx, wz + 1, config, seed) - h : 0;
                    float light = Mathf.Clamp(.94f + (dz - dx) * .14f, .58f, 1.14f);
                    bool water = !inside || h <= waterLevel;
                    float noise = Mathf.PerlinNoise(wx * .8f, wz * .8f) * .08f;
                    Color land = h < waterLevel + 1 ? new Color(.69f, .62f, .38f) : Color.Lerp(new Color(.21f, .39f, .18f), new Color(.49f, .46f, .33f), Mathf.InverseLerp(waterLevel + 2, config.heightMultiplier, h));
                    Color miniColor = water ? new Color(.13f, .29f, .33f) : land * light;
                    Color chart = water ? new Color(.47f, .59f, .59f) : new Color(.77f + noise, .66f + noise, .43f + noise) * light;
                    if (inside && Mathf.Abs(h - waterLevel) < .45f) { miniColor *= .65f; chart *= .73f; }
                    if (!water && Mathf.Repeat(h, 3) < .12f) chart *= .87f;
                    miniColor.a = chart.a = 1; terrainPixels[y * n + x] = miniColor; chartPixels[y * n + x] = chart;
                }
                if (y % 8 == 7) yield return null;
            }
            Release(atlas); Release(parchment);
            atlas = Texture(terrainPixels, n, "Minimap terrain"); parchment = Texture(chartPixels, n, "Parchment world chart");
            miniTerrain.Atlas = atlas; mapTerrain.Atlas = parchment;
            miniTerrain.AtlasBounds = mapTerrain.AtlasBounds = uvBounds;
            miniTerrain.SetAllDirty(); mapTerrain.SetAllDirty(); baking = null;
        }
        private static Texture2D Texture(Color32[] pixels, int size, string name)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels32(pixels); texture.Apply(false, true); return texture;
        }
        private static RectTransform Rect(string name, Transform parent, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer)); var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false); rect.sizeDelta = size; return rect;
        }
        private static RectTransform Box(string name, Transform parent, Vector2 size, Color tint)
        {
            var rect = Rect(name, parent, size); var image = rect.gameObject.AddComponent<Image>(); image.color = tint; image.raycastTarget = false; return rect;
        }
        private static WorldMapGraphic Graphic(string name, Transform parent, Vector2 size)
        {
            var graphic = Rect(name, parent, size).gameObject.AddComponent<WorldMapGraphic>(); graphic.raycastTarget = false; return graphic;
        }
        private static Text Label(string text, Transform parent, Vector2 position, Vector2 size, int fontSize)
        {
            var label = Rect(text, parent, size).gameObject.AddComponent<Text>(); label.rectTransform.anchoredPosition = position;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); label.text = text; label.fontSize = fontSize;
            label.alignment = TextAnchor.MiddleCenter; label.color = new Color(.94f, .82f, .57f); label.raycastTarget = false;
            label.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, .65f); return label;
        }
        private void Button(string text, Transform parent, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action)
        {
            var rect = Box(text, parent, size, brass); rect.anchoredPosition = position;
            var inset = Box("Button face", rect, size - Vector2.one * 3, new Color(.16f, .12f, .075f));
            rect.GetComponent<Image>().raycastTarget = true;
            var control = new MapControl { Rect = rect, Action = action };
            if (parent == mini) minimapControls.Add(control);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = inset.GetComponent<Image>();
            button.onClick.AddListener(() => ActivateControl(control));
            Label(text, rect, Vector2.zero, size, text.Length > 2 ? 14 : 20);
        }
        private static void ActivateControl(MapControl control)
        {
            if (control.LastClickFrame == Time.frameCount) return;
            control.LastClickFrame = Time.frameCount;
            control.Action();
        }
        // Pause is an IMGUI interface. Route its pointer events to the existing
        // minimap buttons too, without relying on a gameplay UI action map being active.
        // Both input paths share the frame guard so a click never applies zoom twice.
        private void OnGUI()
        {
            if (canvas == null || !canvas.enabled || InGameMenuController.Instance == null || !InGameMenuController.Instance.IsOpen) return;
            Matrix4x4 previousMatrix = GUI.matrix;
            int previousDepth = GUI.depth;
            GUI.matrix = Matrix4x4.identity; GUI.depth = -10;
            foreach (var control in minimapControls)
            {
                if (!control.Rect.gameObject.activeInHierarchy) continue;
                control.Rect.GetWorldCorners(controlCorners);
                // Overlay-canvas corners are screen pixels, with bottom-left origin.
                var hit = new Rect(controlCorners[0].x, Screen.height - controlCorners[2].y,
                    controlCorners[2].x - controlCorners[0].x, controlCorners[2].y - controlCorners[0].y);
                if (GUI.Button(hit, GUIContent.none, GUIStyle.none)) ActivateControl(control);
            }
            GUI.matrix = previousMatrix; GUI.depth = previousDepth;
        }
        private void OnDestroy()
        {
            if (IsExpanded) CloseExpanded();
            if (hotkeys != null) hotkeys.Unregister(HotkeyManager.Map, Toggle);
            Release(atlas); Release(parchment);
            if (Instance == this) Instance = null;
        }
        private static void Release(Texture2D texture)
        {
            if (texture == null) return;
            if (Application.isPlaying) Destroy(texture); else DestroyImmediate(texture);
        }
    }
}
