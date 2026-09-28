using System.Collections.Generic;
using Duskborn.Core;
using FishNet;
using UnityEngine;
using Duskborn.Audio;

namespace Duskborn.Gameplay.Enemies
{
    /// <summary>Bounded local fragments from the rigidly weighted boss mesh. No gameplay colliders.</summary>
    public sealed class HollowWardenDebris : MonoBehaviour
    {
        public const float Lifetime = 18f;
        private sealed class Piece
        {
            public Transform Transform;
            public Mesh Mesh;
            public Vector3[] Vertices;
            public Vector3 Velocity, Spin;
            public float Radius;
            public bool Settled;
        }
        private readonly List<Piece> _pieces = new();
        private float _age;
        private int _groundMask;
        private AudioSource _wood, _stone;

        public void PlayBreakup(AudioDatabase database)
        {
            if (database == null) return;
            _wood = BreakupSource(database.ResourcesSettings.treeFallClips.RandomOrNull(), .65f);
            _stone = BreakupSource(database.ResourcesSettings.rockShatterClips.RandomOrNull(), .75f);
        }

        private AudioSource BreakupSource(AudioClip clip, float pitch)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1;
            source.minDistance = 4;
            source.maxDistance = 45;
            source.dopplerLevel = 0;
            source.volume = AudioManager.Instance.SfxVolume * .7f;
            source.clip = clip;
            source.pitch = pitch;
            if (clip != null) source.Play();
            return source;
        }

        public static HollowWardenDebris Create(SkinnedMeshRenderer[] bodies, int seed, float age)
        {
            if (age >= Lifetime) return null;
            var root = new GameObject("Hollow Warden remains");
            var debris = root.AddComponent<HollowWardenDebris>();
            if (bodies.Length > 0 && bodies[0] != null) root.transform.position = bodies[0].bounds.center;
            debris._age = age;
            debris._groundMask = ~((1 << LayerMask.NameToLayer("Enemy")) |
                (1 << LayerMask.NameToLayer("Player")) | (1 << LayerMask.NameToLayer("Resource")) | (1 << 2));
            var random = new System.Random(seed);
            foreach (var body in bodies)
            {
                if (body == null || body.sharedMesh == null || !body.sharedMesh.isReadable) continue;
                debris.AddBody(body, random);
            }
            if (debris._pieces.Count == 0) { Destroy(root); return null; }
            // Late observers get the same lifetime and an approximately progressed local trajectory.
            for (float elapsed = 0; elapsed < Mathf.Min(age, 4f); elapsed += .02f) debris.Simulate(.02f);
            return debris;
        }

        private void AddBody(SkinnedMeshRenderer body, System.Random random)
        {
            var baked = new Mesh();
            body.BakeMesh(baked);
            var vertices = baked.vertices;
            var uv = baked.uv;
            var weights = body.sharedMesh.boneWeights;
            // Model has rigid weights. Group whole triangles by the strongest bone;
            // material submeshes remain intact so shoulders, wood and glowing core retain their look.
            var groups = new Dictionary<int, List<int>[]>();
            for (int sub = 0; sub < baked.subMeshCount; sub++)
            {
                var triangles = baked.GetTriangles(sub);
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    int bone = weights.Length == vertices.Length ? weights[triangles[i]].boneIndex0 : 0;
                    if (!groups.TryGetValue(bone, out var lists))
                    {
                        lists = new List<int>[baked.subMeshCount];
                        for (int j = 0; j < lists.Length; j++) lists[j] = new List<int>();
                        groups.Add(bone, lists);
                    }
                    lists[sub].Add(triangles[i]); lists[sub].Add(triangles[i + 1]); lists[sub].Add(triangles[i + 2]);
                }
            }
            foreach (var group in groups.Values)
            {
                if (_pieces.Count >= 24) break;
                var points = new List<Vector3>();
                var uvs = new List<Vector2>();
                var map = new Dictionary<int, int>();
                var indices = new List<int>[group.Length];
                for (int sub = 0; sub < group.Length; sub++)
                {
                    indices[sub] = new List<int>();
                    foreach (int original in group[sub])
                    {
                        if (!map.TryGetValue(original, out int index))
                        {
                            index = points.Count;
                            map.Add(original, index);
                            points.Add(body.transform.TransformPoint(vertices[original]));
                            uvs.Add(uv.Length == vertices.Length ? uv[original] : Vector2.zero);
                        }
                        indices[sub].Add(index);
                    }
                }
                var bounds = new Bounds(points[0], Vector3.zero);
                foreach (var point in points) bounds.Encapsulate(point);
                Vector3 center = bounds.center;
                for (int i = 0; i < points.Count; i++) points[i] -= center;
                var mesh = new Mesh { name = "Warden fragment", subMeshCount = group.Length };
                mesh.SetVertices(points); mesh.SetUVs(0, uvs);
                for (int sub = 0; sub < indices.Length; sub++) mesh.SetTriangles(indices[sub], sub);
                mesh.RecalculateNormals(); mesh.RecalculateBounds();
                var go = new GameObject("Warden fragment", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(transform, false);
                go.transform.position = center;
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                go.GetComponent<MeshRenderer>().sharedMaterials = body.sharedMaterials;
                float angle = (float)random.NextDouble() * Mathf.PI * 2;
                _pieces.Add(new Piece {
                    Transform = go.transform, Mesh = mesh, Vertices = points.ToArray(),
                    Radius = Mathf.Clamp(bounds.extents.magnitude * .45f, .08f, .5f),
                    Velocity = new Vector3(Mathf.Cos(angle) * 3, 3 + (float)random.NextDouble() * 3, Mathf.Sin(angle) * 3),
                    Spin = new Vector3((float)random.NextDouble() * 240, 170, (float)random.NextDouble() * 240)
                });
            }
            Destroy(baked);
        }

        private void Update()
        {
            if (!InstanceFinder.IsClientStarted ||
                (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState != GameState.Running))
            { Destroy(gameObject); return; }
            _age += Time.deltaTime;
            if (_wood != null) _wood.volume = AudioManager.Instance.SfxVolume * .7f;
            if (_stone != null) _stone.volume = AudioManager.Instance.SfxVolume * .7f;
            if (_age >= Lifetime) { Destroy(gameObject); return; }
            Simulate(Mathf.Min(Time.deltaTime, .05f));
            float scale = Mathf.Clamp01((Lifetime - _age) / 2f);
            foreach (var piece in _pieces) piece.Transform.localScale = Vector3.one * scale;
        }

        private void Simulate(float dt)
        {
            foreach (var piece in _pieces)
            {
                if (piece.Settled) continue;
                piece.Velocity += Physics.gravity * dt;
                Vector3 delta = piece.Velocity * dt;
                if (Physics.SphereCast(piece.Transform.position, piece.Radius, delta.normalized,
                    out var hit, delta.magnitude, _groundMask, QueryTriggerInteraction.Ignore))
                {
                    // Rest the actual lowest vertex on the surface, rather than floating
                    // every differently sized limb on its conservative cast sphere.
                    float support = float.MaxValue;
                    foreach (var vertex in piece.Vertices)
                        support = Mathf.Min(support, Vector3.Dot(piece.Transform.rotation * vertex, hit.normal));
                    piece.Transform.position = hit.point - hit.normal * support;
                    piece.Settled = true;
                }
                else
                {
                    piece.Transform.position += delta;
                    piece.Transform.Rotate(piece.Spin * dt, Space.World);
                }
            }
        }

        private void OnDestroy()
        {
            foreach (var piece in _pieces) if (piece.Mesh != null) Destroy(piece.Mesh);
        }
    }
}
