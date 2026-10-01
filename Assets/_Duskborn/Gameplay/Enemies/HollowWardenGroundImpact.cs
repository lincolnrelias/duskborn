using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Duskborn.Gameplay.Enemies
{
    /// <summary>Bounded, reusable cosmetic dust and ballistic stones; no colliders or network objects.</summary>
    internal sealed class HollowWardenGroundImpact : IDisposable
    {
        private const int Sites = 12;
        private readonly GameObject _root;
        private readonly Mesh _mesh;
        private readonly Piece[] _pieces = new Piece[Sites * 5];
        private readonly MaterialPropertyBlock _properties = new();
        private int _sequence = -1;
        private bool _emitted;
        private float _trailTime;
        private int _trailSite, _trailStamp;
        private int _headbuttSequence = -1;
        private bool _trailReady;
        private Vector3 _trailPreviousPoint;
        private readonly System.Random _trailRandom = new(401);

        private sealed class Piece
        {
            public Transform Transform;
            public MeshRenderer Renderer;
            public Vector3 Origin, Velocity, Spin;
            public float Delay, Size;
            public bool Dust;
            public bool TrailActive;
            public float TrailStarted;
        }

        public HollowWardenGroundImpact(Material dustMaterial, Material stoneMaterial, string label = "Warden ground impacts")
        {
            _root = new GameObject(label);
            // Flat-shaded, irregular octahedra match the Warden's faceted art style.
            Vector3[] corners = { Vector3.up, Vector3.down * .7f, Vector3.left,
                Vector3.right * .8f, Vector3.forward, Vector3.back * .8f };
            int[] faces = { 0,4,3, 0,3,5, 0,5,2, 0,2,4, 1,3,4, 1,5,3, 1,2,5, 1,4,2 };
            var vertices = new Vector3[faces.Length];
            var indices = new int[faces.Length];
            for (int i = 0; i < faces.Length; i++) { vertices[i] = corners[faces[i]]; indices[i] = i; }
            _mesh = new Mesh { name = "Warden dust and stone facets", vertices = vertices, triangles = indices };
            _mesh.RecalculateNormals(); _mesh.RecalculateBounds();
            for (int i = 0; i < _pieces.Length; i++)
            {
                bool dust = i % 5 < 3;
                var go = new GameObject(dust ? "Dust puff" : "Thrown stone", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(_root.transform, false);
                go.GetComponent<MeshFilter>().sharedMesh = _mesh;
                var renderer = go.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = dust ? dustMaterial : stoneMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = !dust;
                renderer.enabled = false;
                _pieces[i] = new Piece { Transform = go.transform, Renderer = renderer, Dust = dust };
            }
        }

        public void Update(HollowWardenBoss boss)
        {
            var view = boss.ActionView;
            bool sweep = view.State == WardenState.HarvestSweep;
            if (!sweep && view.State != WardenState.Rootbreaker) { Hide(); return; }
            if (_sequence != view.Sequence)
            {
                Hide(); _sequence = view.Sequence; _emitted = false;
            }
            float age = (float)boss.ActionAge - (sweep ? .9f : 1f) / Mathf.Max(1, view.Rate);
            if (age < 0) return;
            if (!_emitted)
            {
                _emitted = true;
                // Do not replay an old impact when joining or resuming a hidden encounter.
                if (age > .25f) return;
                Seed(boss, view, sweep);
                _root.SetActive(true);
            }
            if (!_root.activeSelf) return;
            foreach (var piece in _pieces)
            {
                float t = age - piece.Delay;
                Animate(piece, t);
            }
        }

        // The same Warden puffs, stone mesh, ballistics and fade, distributed at
        // hoof-sized impact sites along a moving enemy's actual path.
        public void UpdateTrail(Vector3 position, Vector3 forward, float delta, bool emitting)
        {
            _trailTime += Mathf.Max(0, delta);
            if (!emitting) _trailReady = false;
            else if (!_trailReady)
            {
                _trailReady = true;
                _trailPreviousPoint = position;
                EmitTrail(position, forward);
            }
            else
            {
                Vector3 travel = position - _trailPreviousPoint;
                travel.y = 0;
                // Teleports/pool reuse must not draw a bridge across the map.
                if (travel.sqrMagnitude > 16) _trailPreviousPoint = position;
                else
                {
                    const float spacing = .65f;
                    int count = Mathf.Min(4, Mathf.FloorToInt(travel.magnitude / spacing));
                    Vector3 direction = travel.sqrMagnitude > .001f ? travel.normalized : forward;
                    for (int site = 0; site < count; site++)
                    {
                        _trailPreviousPoint += direction * spacing;
                        _trailPreviousPoint.y = position.y;
                        EmitTrail(_trailPreviousPoint, forward);
                    }
                }
            }
            bool active = false;
            foreach (var piece in _pieces)
            {
                if (!piece.TrailActive) { piece.Renderer.enabled = false; continue; }
                Animate(piece, _trailTime - piece.TrailStarted);
                piece.TrailActive = piece.Renderer.enabled;
                active |= piece.TrailActive;
            }
            _root.SetActive(active);
        }

        public bool EmitHeadbutt(Vector3 position, Vector3 forward, int sequence, float age)
        {
            // Match the upward strike pose; emit once and never replay stale attacks.
            if (sequence == _headbuttSequence || age < .06f || age >= BriarbackCharge.HeadbuttSeconds) return false;
            _headbuttSequence = sequence;
            for (int site = 0; site < 3; site++)
                EmitTrail(position + forward * (.9f + site * .3f), forward);
            return true;
        }

        private void EmitTrail(Vector3 point, Vector3 forward)
        {
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            point += right * ((_trailStamp++ % 2 == 0 ? -1 : 1) * .40f);
            point.y = HollowWardenPresentation.SampleGround(point, point.y, out var normal);
            for (int j = 0; j < 5; j++)
            {
                var piece = _pieces[_trailSite * 5 + j];
                float jitter = (float)_trailRandom.NextDouble();
                Vector3 sideways = right * ((j - 2) * .09f);
                piece.Origin = point + normal * .04f + sideways;
                piece.Velocity = -forward * (.25f + jitter * .65f) + sideways * 2 +
                    normal * (piece.Dust ? .35f + jitter * .35f : 1.6f + jitter);
                piece.Size = piece.Dust ? .13f + jitter * .10f : .045f + jitter * .065f;
                piece.Spin = new Vector3(170 + jitter * 200, _trailSite * 29, 220 - jitter * 300);
                piece.TrailStarted = _trailTime;
                piece.TrailActive = true;
            }
            _trailSite = (_trailSite + 1) % Sites;
        }

        private void Animate(Piece piece, float t)
        {
            float lifetime = piece.Dust ? 1.05f : .8f;
            piece.Renderer.enabled = t >= 0 && t < lifetime;
            if (!piece.Renderer.enabled) return;
            float progress = t / lifetime;
            Vector3 displacement = piece.Velocity * t;
            if (!piece.Dust) displacement += Vector3.down * (4.9f * t * t);
            displacement.y = Mathf.Max(0, displacement.y);
            piece.Transform.position = piece.Origin + displacement;
            piece.Transform.rotation = Quaternion.Euler(piece.Spin * t);
            float size = piece.Size * (piece.Dust ? Mathf.Lerp(.5f, 2.5f, progress)
                : 1 - Mathf.SmoothStep(0, 1, (progress - .65f) / .35f));
            piece.Transform.localScale = new Vector3(1, piece.Dust ? .65f : .7f, 1.15f) * size;
            Color color = piece.Dust ? new Color(.48f, .39f, .27f, .38f * (1 - progress) * Mathf.Clamp01(t / .035f))
                : new Color(.31f, .28f, .23f, 1);
            _properties.SetColor("_BaseColor", color);
            piece.Renderer.SetPropertyBlock(_properties);
        }

        public void ResetTrail()
        {
            Hide();
            _trailReady = false; _trailSite = _trailStamp = 0; _trailTime = 0;
            _headbuttSequence = -1;
            foreach (var piece in _pieces) { piece.TrailActive = false; piece.Renderer.enabled = false; }
        }

        private void Seed(HollowWardenBoss boss, WardenActionView view, bool sweep)
        {
            var random = new System.Random(view.Sequence);
            Quaternion facing = Quaternion.LookRotation(view.Forward);
            for (int site = 0; site < Sites; site++)
            {
                float progress = site / (float)(Sites - 1);
                float angle = Mathf.Lerp(75, -75, progress) * Mathf.Deg2Rad;
                Vector3 direction = sweep ? new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) : Vector3.forward;
                Vector3 local = sweep ? direction * (boss.SweepRadius * .65f)
                    : new Vector3(0, 0, Mathf.Lerp(.9f, boss.RootbreakerLength, progress));
                Vector3 point = view.Origin + facing * local;
                point.y = HollowWardenPresentation.SampleGround(point, view.Origin.y, out var normal);
                for (int j = 0; j < 5; j++)
                {
                    var piece = _pieces[site * 5 + j];
                    float jitter = (float)random.NextDouble();
                    Vector3 sideways = facing * Vector3.right * ((j - 2) * .16f);
                    piece.Origin = point + normal * .08f + sideways;
                    piece.Velocity = facing * direction * (.5f + jitter * 1.8f) +
                        sideways * 2 + normal * (piece.Dust ? .5f + jitter * .5f : 2.5f + jitter * 1.4f);
                    piece.Delay = progress * .14f / Mathf.Max(1, view.Rate);
                    piece.Size = piece.Dust ? .3f + jitter * .2f : .08f + jitter * .12f;
                    piece.Spin = new Vector3(170 + jitter * 200, site * 29, 220 - jitter * 300);
                }
            }
        }

        public void Hide() => _root.SetActive(false);
        public void Dispose()
        {
            if (Application.isPlaying)
            { UnityEngine.Object.Destroy(_root); UnityEngine.Object.Destroy(_mesh); }
            else
            { UnityEngine.Object.DestroyImmediate(_root); UnityEngine.Object.DestroyImmediate(_mesh); }
        }
    }
}
