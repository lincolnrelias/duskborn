using System.Collections.Generic;
using UnityEngine;

namespace Duskborn.Core
{
    /// <summary>
    /// Press H to toggle hitbox flash visualization.
    /// All combat code calls HitboxDebugger.Flash() — works in both Scene and Game view.
    /// Add this component to any scene GameObject (e.g. the debug object with GameDebugController).
    /// </summary>
    public class HitboxDebugger : MonoBehaviour
    {
        public static HitboxDebugger Instance { get; private set; }
        public static bool IsEnabled => Instance != null && Instance._enabled;

        [SerializeField] private KeyCode toggleKey  = KeyCode.H;
        [SerializeField] private float   lineWidth  = 0.05f;

        private bool _enabled;

        private readonly List<ActiveFlash>    _activeFlashes = new();
        private readonly Queue<LineRenderer>  _pool          = new();
        private Material _mat;

        private const int Segments = 24;
        private const int PoolSize = 30; // 3 circles × 10 max concurrent flashes

        private struct ActiveFlash
        {
            public LineRenderer[] Circles; // [0]=XZ, [1]=XY, [2]=YZ
            public float          EndTime;
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            // Sprites/Default supports LineRenderer vertex colors in both built-in and URP.
            _mat = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave };

            for (int i = 0; i < PoolSize; i++)
                _pool.Enqueue(MakeLineRenderer());
        }

        private void OnDestroy()
        {
            if (_mat != null) Destroy(_mat);
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                _enabled = !_enabled;
                Debug.Log($"[HitboxDebugger] {(_enabled ? "ON" : "OFF")}  ({toggleKey} to toggle)");
                if (!_enabled) ReturnAll();
            }

            if (!_enabled) return;

            float now = Time.time;
            for (int i = _activeFlashes.Count - 1; i >= 0; i--)
            {
                if (_activeFlashes[i].EndTime > now) continue;
                ReturnFlash(_activeFlashes[i]);
                _activeFlashes.RemoveAt(i);
            }
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Flash a wireframe sphere at <paramref name="center"/> for <paramref name="duration"/> seconds.
        /// Call this from any combat code. No-ops when the debugger is off.
        /// </summary>
        public static void Flash(Vector3 center, float radius, Color color, float duration = 0.15f)
        {
            if (Instance == null || !Instance._enabled) return;
            Instance.DoFlash(center, radius, color, duration);
        }

        // ── Internal ──────────────────────────────────────────────────────────

        private void DoFlash(Vector3 center, float radius, Color color, float duration)
        {
            if (_pool.Count < 3) return; // pool exhausted — skip silently

            var flash = new ActiveFlash
            {
                Circles = new[] { _pool.Dequeue(), _pool.Dequeue(), _pool.Dequeue() },
                EndTime = Time.time + duration,
            };

            ConfigureCircle(flash.Circles[0], center, radius, color, 0); // XZ (horizontal)
            ConfigureCircle(flash.Circles[1], center, radius, color, 1); // XY (front-facing)
            ConfigureCircle(flash.Circles[2], center, radius, color, 2); // YZ (side-facing)

            _activeFlashes.Add(flash);
        }

        private static void ConfigureCircle(LineRenderer lr, Vector3 center, float radius, Color color, int axis)
        {
            lr.startColor = color;
            lr.endColor   = color;

            float step = 2f * Mathf.PI / Segments;
            var pts = new Vector3[Segments];
            for (int i = 0; i < Segments; i++)
            {
                float c = Mathf.Cos(i * step), s = Mathf.Sin(i * step);
                pts[i] = center + axis switch
                {
                    0 => new Vector3(c, 0, s),  // XZ
                    1 => new Vector3(c, s, 0),  // XY
                    _ => new Vector3(0, c, s),  // YZ
                } * radius;
            }

            lr.SetPositions(pts);
            lr.gameObject.SetActive(true);
        }

        private LineRenderer MakeLineRenderer()
        {
            var go = new GameObject("_HitboxCircle") { hideFlags = HideFlags.HideAndDontSave };
            go.transform.SetParent(transform);

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace         = true;
            lr.loop                  = true;
            lr.positionCount         = Segments;
            lr.startWidth            = lineWidth;
            lr.endWidth              = lineWidth;
            lr.shadowCastingMode     = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows        = false;
            lr.material              = _mat;

            go.SetActive(false);
            return lr;
        }

        private void ReturnFlash(ActiveFlash flash)
        {
            foreach (var lr in flash.Circles)
            {
                lr.gameObject.SetActive(false);
                _pool.Enqueue(lr);
            }
        }

        private void ReturnAll()
        {
            foreach (var f in _activeFlashes)
                ReturnFlash(f);
            _activeFlashes.Clear();
        }
    }
}
