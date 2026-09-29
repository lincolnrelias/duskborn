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

        private sealed class Piece
        {
            public Transform Transform;
            public MeshRenderer Renderer;
            public Vector3 Origin, Velocity, Spin;
            public float Delay, Size;
            public bool Dust;
        }

        public HollowWardenGroundImpact(Material dustMaterial, Material stoneMaterial)
        {
            _root = new GameObject("Warden ground impacts");
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
                float lifetime = piece.Dust ? 1.05f : .8f;
                piece.Renderer.enabled = t >= 0 && t < lifetime;
                if (!piece.Renderer.enabled) continue;
                float progress = t / lifetime;
                Vector3 displacement = piece.Velocity * t;
                if (!piece.Dust) displacement += Vector3.down * (4.9f * t * t);
                // End below-ground travel at the impact surface and shrink settled fragments away.
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
            UnityEngine.Object.Destroy(_root);
            UnityEngine.Object.Destroy(_mesh);
        }
    }
}
