using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Duskborn.Gameplay.Enemies
{
    /// <summary>Local presentation only. The server's encounter clock is the sole hit authority.</summary>
    public sealed class HollowWardenPresentation : MonoBehaviour
    {
        [SerializeField] private HollowWardenBoss boss;
        [SerializeField] private Animator animator;
        [SerializeField] private Material telegraphMaterial;
        [SerializeField] private Canvas hud;
        [SerializeField] private Image healthFill;
        [SerializeField] private TextMeshProUGUI statusLabel;
        [SerializeField] private Transform rightPlate;
        [SerializeField] private Transform leftPlate;
        [SerializeField] private Quaternion openRight;
        [SerializeField] private Quaternion openLeft;

        private const int LineSegmentsZ = 16;
        private const int LineSegmentsX = 2;
        private const int LineVertexCount = (LineSegmentsZ + 1) * (LineSegmentsX + 1); // 51
        private readonly Vector3[] _linePoints = new Vector3[LineVertexCount];

        private const int ArcRings = 4;
        private const int ArcSlices = 18;
        private const int ArcVertexCount = 1 + ArcRings * (ArcSlices + 1); // 77
        private readonly Vector3[] _arcPoints = new Vector3[ArcVertexCount];

        private Mesh _lineMesh, _arcMesh;
        private MeshRenderer _line, _arc;
        private int _sequence = -1;
        private string _clip;
        private int _groundSequence = -1;
        private bool _wasLocked;
        private float _groundAt;
        private MaterialPropertyBlock _properties;
        private readonly List<SpikeVisual> _spikePool = new();
        private Mesh _spikeMesh;
        private Material _spikeMaterial;
        private HollowWardenChannelVisual _channelVisual;

        private sealed class SpikeVisual
        {
            public GameObject Root;
            public MeshRenderer Ring, Fill, Spikes;
            public Mesh RingMesh, FillMesh;
            public int Wave = -1;
            public uint Tick;
            public Vector3 Position;
        }

        private static int _cachedGroundMask;
        private static int GroundMask
        {
            get
            {
                if (_cachedGroundMask == 0)
                {
                    int mask = ~0;
                    string[] exclude = { "Enemy", "Player", "Ignore Raycast", "TransparentFX", "Resource", "ResourceNode", "Water" };
                    foreach (var name in exclude)
                    {
                        int layer = LayerMask.NameToLayer(name);
                        if (layer >= 0) mask &= ~(1 << layer);
                    }
                    mask &= ~(1 << 2);
                    _cachedGroundMask = mask;
                }
                return _cachedGroundMask;
            }
        }

        private static float SampleGround(Vector3 worldPos, float referenceY, out Vector3 normal)
        {
            int mask = GroundMask;
            float rayStartY = referenceY + 30f;
            if (Physics.Raycast(new Vector3(worldPos.x, rayStartY, worldPos.z), Vector3.down,
                out var hit, 60f, mask, QueryTriggerInteraction.Ignore))
            {
                normal = hit.normal;
                return hit.point.y;
            }
            if (Physics.Raycast(new Vector3(worldPos.x, worldPos.y + 40f, worldPos.z), Vector3.down,
                out hit, 80f, mask, QueryTriggerInteraction.Ignore))
            {
                normal = hit.normal;
                return hit.point.y;
            }
            normal = Vector3.up;
            return referenceY;
        }

        private void Awake()
        {
            _properties = new MaterialPropertyBlock();
            _line = CreateTelegraph("Rootbreaker warning", out _lineMesh);
            _arc = CreateTelegraph("Sweep warning", out _arcMesh);

            _lineMesh.vertices = new Vector3[LineVertexCount];
            int[] lineTriangles = new int[LineSegmentsZ * LineSegmentsX * 6];
            int ltIdx = 0;
            for (int z = 0; z < LineSegmentsZ; z++)
            {
                for (int x = 0; x < LineSegmentsX; x++)
                {
                    int r0 = z * (LineSegmentsX + 1) + x;
                    int r0Next = r0 + 1;
                    int r1 = (z + 1) * (LineSegmentsX + 1) + x;
                    int r1Next = r1 + 1;

                    lineTriangles[ltIdx++] = r0;
                    lineTriangles[ltIdx++] = r1;
                    lineTriangles[ltIdx++] = r1Next;

                    lineTriangles[ltIdx++] = r0;
                    lineTriangles[ltIdx++] = r1Next;
                    lineTriangles[ltIdx++] = r0Next;
                }
            }
            _lineMesh.triangles = lineTriangles;

            _arcMesh.vertices = new Vector3[ArcVertexCount];
            int[] arcTriangles = new int[(ArcSlices + (ArcRings - 1) * ArcSlices * 2) * 3];
            int atIdx = 0;
            for (int s = 0; s < ArcSlices; s++)
            {
                arcTriangles[atIdx++] = 0;
                arcTriangles[atIdx++] = 1 + s + 1;
                arcTriangles[atIdx++] = 1 + s;
            }
            for (int r = 1; r < ArcRings; r++)
            {
                int rStart = 1 + (r - 1) * (ArcSlices + 1);
                int nextRStart = 1 + r * (ArcSlices + 1);
                for (int s = 0; s < ArcSlices; s++)
                {
                    int r0 = rStart + s;
                    int r0Next = r0 + 1;
                    int r1 = nextRStart + s;
                    int r1Next = r1 + 1;

                    arcTriangles[atIdx++] = r0;
                    arcTriangles[atIdx++] = r1Next;
                    arcTriangles[atIdx++] = r1;

                    arcTriangles[atIdx++] = r0;
                    arcTriangles[atIdx++] = r0Next;
                    arcTriangles[atIdx++] = r1Next;
                }
            }
            _arcMesh.triangles = arcTriangles;

            if (hud != null) hud.enabled = false;
            _spikeMaterial = boss != null ? boss.SpikeMaterial : null;
            _spikeMesh = BuildSpikeMesh();
            _channelVisual = new HollowWardenChannelVisual(transform, animator, rightPlate, leftPlate, openRight, openLeft, telegraphMaterial);
        }

        private MeshRenderer CreateTelegraph(string label, out Mesh mesh)
        {
            var go = new GameObject(label, typeof(MeshFilter), typeof(MeshRenderer));
            // World-space warnings must not drift with interpolated network transforms after lock.
            mesh = new Mesh {name=label};
            mesh.MarkDynamic();
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = telegraphMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;
            return renderer;
        }

        private void Update()
        {
            if (boss == null || !boss.IsSpawned || !boss.IsClientStarted)
            {
                _line.enabled = _arc.enabled = false;
                if (hud != null) hud.enabled = false;
                HideSpikes();
                _channelVisual?.Hide();
                return;
            }
            var view = boss.ActionView;
            UpdateSpikeVisuals();
            if (hud != null) hud.enabled = view.State != WardenState.Dormant && view.State != WardenState.Dead;
            if (healthFill != null) healthFill.fillAmount = Mathf.Clamp01(boss.CurrentHP / Mathf.Max(1,boss.DisplayMaxHP));
            if (statusLabel != null)
                statusLabel.text = view.State == WardenState.Rooted ? "Core exposed — attack while dodging the spikes"
                    : view.State == WardenState.RootPlant ? "Preparing spike channel"
                    : view.PhaseTwo ? "The heart awakens" : "Hollow Warden";

            string clip = view.State == WardenState.Ready ? (boss.Moving ? "HW_Walk" : "HW_Idle")
                : "HW_" + (view.State == WardenState.Dead ? "Death" : view.State.ToString());
            if (animator != null && view.State != WardenState.Dormant && (_sequence != view.Sequence || _clip != clip))
            {
                animator.speed = view.Rate > 0 ? view.Rate : 1;
                float age = (float)boss.ActionAge * animator.speed;
                float normalized = age / ClipLength(clip);
                if (view.State == WardenState.Ready) normalized = 0;
                else if (view.State == WardenState.Rooted || view.State == WardenState.Exposed) normalized %= 1;
                else normalized = Mathf.Clamp01(normalized);
                animator.Play(clip, 0, normalized);
                _sequence = view.Sequence;
                _clip = clip;
            }

            float impact = (view.State == WardenState.Rootbreaker ? 1f : .9f) / Mathf.Max(1,view.Rate);
            bool warning = boss.ActionAge <= impact && boss.IsAlive;
            _line.enabled = warning && view.State == WardenState.Rootbreaker;
            _arc.enabled = warning && view.State == WardenState.HarvestSweep;
            if (!_line.enabled && !_arc.enabled) return;
            if (_groundSequence != view.Sequence || _wasLocked != view.FacingLocked || (!view.FacingLocked && Time.time >= _groundAt))
            {
                ProjectWarning(view);
                _groundSequence = view.Sequence;
                _wasLocked = view.FacingLocked;
                _groundAt = Time.time + .05f;
            }
            Color color = Color.Lerp(new Color(1,.65f,.12f,.30f), new Color(1,.22f,.05f,.65f), Mathf.Clamp01((float)boss.ActionAge/impact));
            _properties.SetColor("_BaseColor",color);
            _line.SetPropertyBlock(_properties);
            _arc.SetPropertyBlock(_properties);
        }

        private void ProjectWarning(WardenActionView view)
        {
            Vector3 origin = view.FacingLocked ? view.Origin : boss.transform.position;
            Vector3 forward = view.FacingLocked ? view.Forward : boss.transform.forward;
            Quaternion rotation = Quaternion.LookRotation(forward);
            float targetY = boss.Target != null ? boss.Target.position.y : origin.y;
            float highestY = Mathf.Max(origin.y, targetY);

            if (_line.enabled)
            {
                float length = boss.RootbreakerLength;
                float halfWidth = boss.RootbreakerWidth * 0.5f;
                int idx = 0;
                for (int zi = 0; zi <= LineSegmentsZ; zi++)
                {
                    float z = ((float)zi / LineSegmentsZ) * length;
                    for (int xi = 0; xi <= LineSegmentsX; xi++)
                    {
                        float x = Mathf.Lerp(-halfWidth, halfWidth, (float)xi / LineSegmentsX);
                        Vector3 worldPos = origin + rotation * new Vector3(x, 0, z);
                        float groundY = SampleGround(worldPos, highestY, out var normal);
                        _linePoints[idx++] = new Vector3(worldPos.x, groundY, worldPos.z) + normal * 0.08f + Vector3.up * 0.04f;
                    }
                }
                _lineMesh.vertices = _linePoints;
                _lineMesh.RecalculateBounds();
                _lineMesh.RecalculateNormals();
            }

            if (_arc.enabled)
            {
                float radius = boss.SweepRadius;
                int idx = 0;
                float centerGroundY = SampleGround(origin, highestY, out var centerNormal);
                _arcPoints[idx++] = new Vector3(origin.x, centerGroundY, origin.z) + centerNormal * 0.08f + Vector3.up * 0.04f;

                for (int ring = 1; ring <= ArcRings; ring++)
                {
                    float r = ((float)ring / ArcRings) * radius;
                    for (int s = 0; s <= ArcSlices; s++)
                    {
                        float angle = (-90f + s * (180f / ArcSlices)) * Mathf.Deg2Rad;
                        Vector3 local = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * r;
                        Vector3 worldPos = origin + rotation * local;
                        float groundY = SampleGround(worldPos, highestY, out var normal);
                        _arcPoints[idx++] = new Vector3(worldPos.x, groundY, worldPos.z) + normal * 0.08f + Vector3.up * 0.04f;
                    }
                }
                _arcMesh.vertices = _arcPoints;
                _arcMesh.RecalculateBounds();
                _arcMesh.RecalculateNormals();
            }
        }

        private void LateUpdate()
        {
            if (boss == null || !boss.IsSpawned || !boss.IsClientStarted) return;
            _channelVisual?.Apply(boss.ActionView.State, (float)boss.ActionAge);
        }
        private static float ClipLength(string clip)
        {
            switch (clip)
            {
                case "HW_Walk": return 1.2f;
                case "HW_Rootbreaker": return 2.4f;
                case "HW_HarvestSweep": return 2.2f;
                case "HW_RootPlant": return 1.4f;
                case "HW_Stagger": case "HW_Recover": return 1;
                case "HW_Death": return 3;
                default: return 2;
            }
        }

        private void OnDestroy()
        {
            _channelVisual?.Dispose();
            foreach (var visual in _spikePool)
            {
                if (visual.Root != null) Destroy(visual.Root);
                if (visual.RingMesh != null) Destroy(visual.RingMesh);
                if (visual.FillMesh != null) Destroy(visual.FillMesh);
            }
            if (_spikeMesh != null) Destroy(_spikeMesh);
            if (_line != null) Destroy(_line.gameObject);
            if (_arc != null) Destroy(_arc.gameObject);
            if (_lineMesh != null) Destroy(_lineMesh);
            if (_arcMesh != null) Destroy(_arcMesh);
        }

        private void OnDisable()
        {
            _channelVisual?.Hide();
            HideSpikes();
            if (_line != null) _line.enabled=false;
            if (_arc != null) _arc.enabled=false;
            if (hud != null) hud.enabled=false;
        }

        private void HideSpikes()
        {
            foreach (var visual in _spikePool) visual.Root.SetActive(false);
        }

        private void UpdateSpikeVisuals()
        {
            if (!boss.IsAlive) { HideSpikes(); return; }
            var spikes = boss.Spikes;
            for (int i = 0; i < spikes.Count; i++)
            {
                if (i == _spikePool.Count) _spikePool.Add(CreateSpikeVisual());
                var visual = _spikePool[i];
                var spike = spikes[i];
                float age = Mathf.Max(0, (float)boss.SpikeAge(spike));
                float delay = (float)HollowWardenEncounter.SpikeDelay;
                float lifetime = (float)HollowWardenEncounter.SpikeVisibleSeconds;
                visual.Root.SetActive(age < delay + lifetime);
                if (!visual.Root.activeSelf) continue;
                visual.Root.transform.position = spike.Position + Vector3.up * .08f;
                if (visual.Wave != spike.Wave || visual.Tick != spike.StartTick || visual.Position != spike.Position)
                {
                    // Ground plane slope adapts to local terrain. Preserve the horizontal hit radius.
                    SetCircle(visual.RingMesh, .91f, spike.Position, spike.Normal);
                    SetCircle(visual.FillMesh, 0, spike.Position, spike.Normal);
                    visual.Wave = spike.Wave; visual.Tick = spike.StartTick; visual.Position = spike.Position;
                }
                bool warning = age < delay;
                visual.Ring.enabled = warning;
                visual.Fill.enabled = warning;
                visual.Spikes.enabled = !warning;
                if (warning)
                {
                    float progress = Mathf.Clamp01(age / delay);
                    visual.Fill.transform.localScale = Vector3.one * Mathf.Lerp(.08f, 1, progress);
                    var color = Color.Lerp(new Color(1, .65f, .08f, .65f), new Color(1, .12f, .025f, .9f), progress);
                    _properties.SetColor("_BaseColor", color);
                    visual.Ring.SetPropertyBlock(_properties);
                    color.a = .16f + .3f * progress;
                    _properties.SetColor("_BaseColor", color);
                    visual.Fill.SetPropertyBlock(_properties);
                }
                else
                {
                    float erupted = age - delay;
                    float growth = Mathf.Clamp01(erupted / .08f);
                    float retreat = 1 - Mathf.Clamp01((erupted - lifetime + .22f) / .22f);
                    visual.Spikes.transform.localScale = new Vector3(1, Mathf.Max(.01f, growth * retreat), 1);
                }
            }
            for (int i = spikes.Count; i < _spikePool.Count; i++) _spikePool[i].Root.SetActive(false);
        }

        private SpikeVisual CreateSpikeVisual()
        {
            var visual = new SpikeVisual { Root = new GameObject("Warden delayed spikes") };
            visual.Ring = CreateTelegraph("Spike danger boundary", out visual.RingMesh);
            visual.Fill = CreateTelegraph("Spike countdown", out visual.FillMesh);
            visual.Ring.transform.SetParent(visual.Root.transform, false);
            visual.Fill.transform.SetParent(visual.Root.transform, false);
            var body = new GameObject("Erupting roots", typeof(MeshFilter), typeof(MeshRenderer));
            body.transform.SetParent(visual.Root.transform, false);
            body.GetComponent<MeshFilter>().sharedMesh = _spikeMesh;
            visual.Spikes = body.GetComponent<MeshRenderer>();
            visual.Spikes.sharedMaterial = _spikeMaterial;
            // These are presentation only: no enemy component, collider, loot or network spawn.
            return visual;
        }

        private static void SetCircle(Mesh mesh, float inner, Vector3 spikePos, Vector3 fallbackNormal)
        {
            const int segments = 32;
            var vertices = new Vector3[segments * 2];
            var triangles = new int[segments * 6];
            float highestY = spikePos.y + 15f;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                float x = Mathf.Cos(angle) * HollowWardenEncounter.SpikeRadius;
                float z = Mathf.Sin(angle) * HollowWardenEncounter.SpikeRadius;

                Vector3 worldOuter = spikePos + new Vector3(x, 0, z);
                float outerGroundY = SampleGround(worldOuter, highestY, out _);
                float localOuterY = outerGroundY - spikePos.y;

                float localInnerY;
                if (inner > 0.01f)
                {
                    Vector3 worldInner = spikePos + new Vector3(x * inner, 0, z * inner);
                    float innerGroundY = SampleGround(worldInner, highestY, out _);
                    localInnerY = innerGroundY - spikePos.y;
                }
                else
                {
                    localInnerY = localOuterY * inner;
                }

                vertices[i * 2] = new Vector3(x, localOuterY + 0.04f, z);
                vertices[i * 2 + 1] = new Vector3(x * inner, localInnerY + 0.04f, z * inner);
                int next = ((i + 1) % segments) * 2;
                int t = i * 6;
                triangles[t] = i * 2; triangles[t + 1] = next; triangles[t + 2] = i * 2 + 1;
                triangles[t + 3] = next; triangles[t + 4] = next + 1; triangles[t + 5] = i * 2 + 1;
            }
            mesh.vertices = vertices; mesh.triangles = triangles; mesh.RecalculateBounds();
        }

        private static Mesh BuildSpikeMesh()
        {
            var vertices = new List<Vector3>();
            for (int root = 0; root < 7; root++)
            {
                float angle = root * Mathf.PI / 3;
                Vector3 center = root == 0 ? Vector3.zero
                    : new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 1.65f;
                float height = root == 0 ? 2.4f : 1.5f + (root % 3) * .3f;
                for (int side = 0; side < 5; side++)
                {
                    float a = side * Mathf.PI * 2 / 5;
                    float b = (side + 1) * Mathf.PI * 2 / 5;
                    vertices.Add(center + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * .55f);
                    vertices.Add(center + Vector3.up * height);
                    vertices.Add(center + new Vector3(Mathf.Cos(b), 0, Mathf.Sin(b)) * .55f);
                }
            }
            var indices = new int[vertices.Count];
            for (int i = 0; i < indices.Length; i++) indices[i] = i;
            var mesh = new Mesh { name = "Warden spike eruption" };
            mesh.SetVertices(vertices); mesh.triangles = indices;
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }
    }
}


