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

        private Mesh _lineMesh, _arcMesh;
        private MeshRenderer _line, _arc;
        private int _sequence = -1;
        private string _clip;
        private int _groundSequence = -1;
        private bool _wasLocked;
        private float _groundAt;
        private MaterialPropertyBlock _properties;
        private readonly Vector3[] _linePoints = new Vector3[4];
        private readonly Vector3[] _arcPoints = new Vector3[26];
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

        private void Awake()
        {
            _properties = new MaterialPropertyBlock();
            _line = CreateTelegraph("Rootbreaker warning", out _lineMesh);
            _arc = CreateTelegraph("Sweep warning", out _arcMesh);
            _lineMesh.vertices = new Vector3[4];
            _lineMesh.triangles = new[] {0,2,1,0,3,2};
            _arcMesh.vertices = new Vector3[26];
            int[] triangles = new int[24*3];
            for (int i=0; i<24; i++) { triangles[i*3]=0; triangles[i*3+1]=i+1; triangles[i*3+2]=i+2; }
            _arcMesh.triangles = triangles;
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
            float width = boss.RootbreakerWidth*.5f;
            _linePoints[0]=new Vector3(-width,0,0);_linePoints[1]=new Vector3(width,0,0);
            _linePoints[2]=new Vector3(width,0,boss.RootbreakerLength);_linePoints[3]=new Vector3(-width,0,boss.RootbreakerLength);
            _arcPoints[0]=Vector3.zero;
            for (int i=0;i<=24;i++)
            {
                float angle=(-90+i*180f/24)*Mathf.Deg2Rad;
                _arcPoints[i+1]=new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle))*boss.SweepRadius;
            }
            Ground(_linePoints,origin,rotation);
            Ground(_arcPoints,origin,rotation);
            _lineMesh.vertices=_linePoints;_lineMesh.RecalculateBounds();_lineMesh.RecalculateNormals();
            _arcMesh.vertices=_arcPoints;_arcMesh.RecalculateBounds();_arcMesh.RecalculateNormals();
        }

        private void Ground(Vector3[] vertices, Vector3 origin, Quaternion rotation)
        {
            int mask=~((1<<LayerMask.NameToLayer("Enemy"))|(1<<LayerMask.NameToLayer("Player"))|(1<<2));
            for (int i=0;i<vertices.Length;i++)
            {
                Vector3 world=origin+rotation*vertices[i];
                if (Physics.Raycast(world+Vector3.up*3,Vector3.down,out var hit,7,mask,QueryTriggerInteraction.Ignore)) world.y=hit.point.y;
                vertices[i]=world+Vector3.up*.065f;
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
                visual.Root.transform.position = spike.Position + Vector3.up * .06f;
                if (visual.Wave != spike.Wave || visual.Tick != spike.StartTick || visual.Position != spike.Position)
                {
                    // Ground plane slope is replicated by the server. Preserve the horizontal hit radius.
                    SetCircle(visual.RingMesh, .91f, spike.Normal);
                    SetCircle(visual.FillMesh, 0, spike.Normal);
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

        private static void SetCircle(Mesh mesh, float inner, Vector3 normal)
        {
            const int segments = 32;
            var vertices = new Vector3[segments * 2];
            var triangles = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                float x = Mathf.Cos(angle) * HollowWardenEncounter.SpikeRadius;
                float z = Mathf.Sin(angle) * HollowWardenEncounter.SpikeRadius;
                float y = -(normal.x * x + normal.z * z) / Mathf.Max(.3f, normal.y);
                vertices[i * 2] = new Vector3(x, y, z);
                vertices[i * 2 + 1] = vertices[i * 2] * inner;
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


