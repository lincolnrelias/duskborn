using Duskborn.Gameplay.Enchanting;
using UnityEngine;
using UnityEngine.Rendering;

namespace Duskborn.Effects
{
    /// <summary>Continuous, bounded rune feedback. Does not modify authored weapon materials.</summary>
    public sealed class RuneAura : MonoBehaviour
    {
        private ParticleSystem _particles;
        private Material _material;
        private Texture2D _texture;
        private LineRenderer _arc;
        private RuneKind _kind;
        private int _strength;
        private Bounds _bounds;
        private float _nextArc;
        private RuneKind _textureKind;

        public void Configure(RuneKind kind, int strength, Bounds bounds)
        {
            _bounds = bounds;
            if (_kind == kind && _strength == strength) return;
            _kind = kind; _strength = strength;
            if (kind == RuneKind.None || strength <= 0)
            {
                if (_particles != null) _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                if (_arc != null) _arc.enabled = false;
                return;
            }
            if (_particles == null) Build();
            if (_textureKind != kind) PaintParticleTexture(kind);
            var color = RuneCatalog.Color(kind);
            var main = _particles.main;
            main.startColor = color;
            main.startLifetime = kind == RuneKind.Flame ? .55f : .85f;
            main.startSize = kind == RuneKind.Stone ? .055f : .025f + .006f * Mathf.Min(strength, 6);
            main.startSpeed = kind == RuneKind.Flame ? .35f : .08f;
            var shape = _particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.position = bounds.center;
            shape.scale = Vector3.Max(bounds.size, Vector3.one * .04f);
            var emission = _particles.emission; emission.rateOverTime = 10 + 5 * Mathf.Min(strength, 6);
            var velocity = _particles.velocityOverLifetime;
            // Unity requires X/Y/Z velocity curves to share a mode. An equal-value
            // TwoConstants range preserves deterministic vertical motion.
            velocity.enabled = false; velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-.06f, .06f);
            velocity.z = new ParticleSystem.MinMaxCurve(-.06f, .06f);
            float verticalSpeed = kind == RuneKind.Blood || kind == RuneKind.Stone ? -.4f : kind == RuneKind.Flame ? .6f : .1f;
            velocity.y = new ParticleSystem.MinMaxCurve(verticalSpeed, verticalSpeed);
            velocity.enabled = true;
            var rotation = _particles.rotationOverLifetime; rotation.enabled = true;
            rotation.z = kind == RuneKind.Frost || kind == RuneKind.Stone ? 2f : .3f;
            _arc.startColor = _arc.endColor = color;
            _arc.enabled = kind == RuneKind.Storm;
            _particles.Play();
        }
        public static Bounds LocalBounds(Transform root)
        {
            var bounds = new Bounds(Vector3.zero, Vector3.one * .04f);
            bool found = false;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>())
            {
                if (renderer is ParticleSystemRenderer || renderer is LineRenderer) continue;
                var b = renderer.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var p = root.InverseTransformPoint(b.center + Vector3.Scale(b.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                    if (!found) { bounds = new Bounds(p, Vector3.zero); found = true; } else bounds.Encapsulate(p);
                }
            }
            return bounds;
        }
        private void Build()
        {
            _texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            var pixels = new Color[1024];
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
            {
                float alpha = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(x, y), new Vector2(15.5f, 15.5f)) / 15.5f);
                pixels[y * 32 + x] = new Color(1, 1, 1, alpha * alpha);
            }
            _texture.SetPixels(pixels); _texture.Apply();
            var shader = Resources.Load<Shader>("Shaders/RuneAura") ?? Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
            _material = new Material(shader);
            _material.SetTexture("_BaseMap", _texture); _material.mainTexture = _texture;
            if (_material.HasProperty("_Surface")) _material.SetFloat("_Surface", 1);
            if (_material.HasProperty("_ZWrite")) _material.SetFloat("_ZWrite", 0);
            if (_material.HasProperty("_SrcBlend")) _material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (_material.HasProperty("_DstBlend")) _material.SetFloat("_DstBlend", (float)BlendMode.One);
            _material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); _material.renderQueue = 3000;
            var go = new GameObject("Rune particles"); go.transform.SetParent(transform, false);
            _particles = go.AddComponent<ParticleSystem>(); _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _particles.main; main.loop = true; main.maxParticles = 96;
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.playOnAwake = false;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            var overLife = _particles.colorOverLifetime; overLife.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .15f), new GradientAlphaKey(0, 1) });
            overLife.color = gradient;
            _particles.GetComponent<ParticleSystemRenderer>().sharedMaterial = _material;
            var line = new GameObject("Rune lightning"); line.transform.SetParent(transform, false);
            _arc = line.AddComponent<LineRenderer>(); _arc.sharedMaterial = _material;
            _arc.useWorldSpace = false; _arc.positionCount = 7; _arc.widthMultiplier = .014f;
            _arc.enabled = false;
        }
        private void PaintParticleTexture(RuneKind kind)
        {
            _textureKind = kind;
            var pixels = new Color[1024];
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
            {
                float px = (x - 15.5f) / 15.5f, py = (y - 15.5f) / 15.5f;
                float r = Mathf.Sqrt(px * px + py * py);
                float alpha = kind switch
                {
                    RuneKind.Flame => Mathf.Clamp01(1 - Mathf.Abs(px) * 2.4f - Mathf.Abs(py) * .8f),
                    RuneKind.Venom => Mathf.Clamp01(1 - Mathf.Abs(r - .65f) * 7),
                    RuneKind.Stone => Mathf.Abs(px) + Mathf.Abs(py) < .8f ? 1 : 0,
                    RuneKind.Frost => Mathf.Clamp01(1 - Mathf.Min(Mathf.Abs(px), Mathf.Min(Mathf.Abs(px * .5f + py * .866f), Mathf.Abs(px * .5f - py * .866f))) * 12) * Mathf.Clamp01((1-r) * 5),
                    RuneKind.Blood => Mathf.Clamp01(1 - Mathf.Abs(px) * (2f + Mathf.Max(0, py) * 4) - Mathf.Abs(py) * .6f),
                    RuneKind.Hex => Mathf.Clamp01(1 - Mathf.Abs(r - .5f) * 8) * (Mathf.Abs(px) > .1f ? 1 : 0),
                    RuneKind.Radiance => Mathf.Clamp01(1 - Mathf.Min(Mathf.Abs(px), Mathf.Abs(py)) * 9) * Mathf.Clamp01((1-r) * 4),
                    _ => Mathf.Clamp01(1-r)
                };
                pixels[y * 32 + x] = new Color(1, 1, 1, alpha);
            }
            _texture.SetPixels(pixels); _texture.Apply();
        }
        private void Update()
        {
            if (_arc == null || !_arc.enabled || Time.time < _nextArc) return;
            _nextArc = Time.time + .08f;
            for (int i = 0; i < 7; i++)
                _arc.SetPosition(i, _bounds.center + new Vector3(Random.Range(-.08f, .08f),
                    Mathf.Lerp(-_bounds.extents.y, _bounds.extents.y, i / 6f), Random.Range(-.08f, .08f)));
        }
        private void OnDestroy()
        {
            if (Application.isPlaying)
            {
                if (_material != null) Destroy(_material);
                if (_texture != null) Destroy(_texture);
            }
            else
            {
                if (_material != null) DestroyImmediate(_material);
                if (_texture != null) DestroyImmediate(_texture);
            }
        }
    }
}
