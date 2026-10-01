using Duskborn.Audio;
using UnityEngine;

namespace Duskborn.Gameplay.Enemies
{
    /// <summary>Local animation, ground lane and audio; replicated phase is the only attack authority.</summary>
    public sealed class BriarbackPresentation : MonoBehaviour
    {
        [SerializeField] private Briarback enemy;
        [SerializeField] private Animator animator;
        [SerializeField] private Material warningMaterial;
        [SerializeField] private Material impactDustMaterial, impactStoneMaterial;
        [SerializeField] private AudioClip windupClip, chargeClip, hurtClip, deathClip;
        [SerializeField] private AudioClip[] windupClips, chargeClips, headbuttWindupClips, headbuttClips,
            hurtClips, deathClips, hoofClips;
        private Mesh _laneMesh;
        private MeshRenderer _lane;
        private readonly Vector3[] _vertices = new Vector3[18];
        private MaterialPropertyBlock _properties;
        private AudioSource _source;
        private AudioSource _hurtSource, _hoofSource;
        private int _lastWindup = -1, _lastCharge = -1, _lastHeadbuttWindup = -1, _lastHeadbutt = -1,
            _lastHurt = -1, _lastDeath = -1, _lastHoof = -1;
        private float _hoofDistance, _nextHoofTime;
        private int _sequence = -1;
        private string _clip;
        private Vector3 _previousPosition;
        private float _previousHP;
        private float _hurtStart = float.NegativeInfinity;
        private HollowWardenGroundImpact _trample;

        private void Awake()
        {
            _previousPosition = transform.position;
            _properties = new MaterialPropertyBlock();
            _source = CreateSource(); _hurtSource = CreateSource(); _hoofSource = CreateSource();
            var lane = new GameObject("Briarback charge warning");
            lane.transform.SetParent(transform, false);
            _laneMesh = new Mesh { name = "Briarback warning lane" };
            _laneMesh.MarkDynamic();
            lane.AddComponent<MeshFilter>().sharedMesh = _laneMesh;
            _lane = lane.AddComponent<MeshRenderer>();
            _lane.sharedMaterial = warningMaterial;
            _lane.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _lane.receiveShadows = false;
            _lane.enabled = false;
            var indices = new int[48];
            for (int i = 0; i < 8; i++)
            { int v = i * 2, t = i * 6;
                indices[t] = v; indices[t + 1] = v + 2; indices[t + 2] = v + 1;
                indices[t + 3] = v + 1; indices[t + 4] = v + 2; indices[t + 5] = v + 3; }
            _laneMesh.vertices = _vertices; _laneMesh.triangles = indices;
        }
        private void OnEnable()
        {
            _sequence = -1; _clip = null; _previousPosition = transform.position;
            _hurtStart = float.NegativeInfinity;
            _hoofDistance = _nextHoofTime = 0;
            _lastWindup = _lastCharge = _lastHeadbuttWindup = _lastHeadbutt =
                _lastHurt = _lastDeath = _lastHoof = -1;
            _previousHP = enemy != null ? enemy.CurrentHP : 0;
            _trample?.ResetTrail();
        }
        private void OnDisable()
        { if (_lane != null) _lane.enabled = false; StopAudio(); _trample?.ResetTrail(); }
        private void OnDestroy() { if (_laneMesh != null) Destroy(_laneMesh); _trample?.Dispose(); }
        private AudioSource CreateSource()
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false; source.spatialBlend = 1;
            source.minDistance = 3; source.maxDistance = 32; source.dopplerLevel = 0;
            return source;
        }
        private void StopAudio()
        { _source?.Stop(); _hurtSource?.Stop(); _hoofSource?.Stop(); }
        private static AudioClip Pick(AudioClip[] clips, AudioClip fallback, ref int last)
        {
            if (clips == null || clips.Length == 0) return fallback;
            int index = Random.Range(0, clips.Length > 1 && last >= 0 ? clips.Length - 1 : clips.Length);
            if (clips.Length > 1 && last >= 0 && index >= last) index++;
            last = index;
            return clips[index] != null ? clips[index] : fallback;
        }
        private void Play(AudioClip clip, AudioSource source, float volume = .8f)
        {
            if (clip == null) return;
            source.volume = volume * (AudioManager.Instance != null ? AudioManager.Instance.SfxVolume : 1);
            source.clip = clip; source.Play();
        }
        private void PlayPhase(BriarbackPhase phase, float age)
        {
            switch (phase)
            {
                case BriarbackPhase.Windup:
                    Play(Pick(windupClips, windupClip, ref _lastWindup), _source); break;
                case BriarbackPhase.Charge:
                    Play(Pick(chargeClips, chargeClip, ref _lastCharge), _source); break;
                case BriarbackPhase.HeadbuttWindup:
                    Play(Pick(headbuttWindupClips, windupClip, ref _lastHeadbuttWindup), _source); break;
                case BriarbackPhase.Headbutt:
                    // The file contains its impact at +0.06 s; do not also emit on damage.
                    Play(Pick(headbuttClips, chargeClip, ref _lastHeadbutt), _source); break;
                case BriarbackPhase.Dead:
                    var death = Pick(deathClips, deathClip, ref _lastDeath);
                    if (AudioManager.Instance != null)
                        AudioManager.Instance.PlayAtPoint(death, transform.position, .8f, 3, 32, 0);
                    else Play(death, _source);
                    break;
                default: return;
            }
            // Keep the recorded +0.06 s headbutt contact aligned to replicated time.
            if (phase != BriarbackPhase.Dead && _source.isPlaying && _source.clip != null)
                _source.time = Mathf.Clamp(age, 0, Mathf.Max(0, _source.clip.length - .001f));
        }
        private void LateUpdate()
        {
            if (enemy == null || !enemy.IsSpawned || !enemy.IsClientStarted)
            { _lane.enabled = false; StopAudio(); _hoofDistance = 0; _trample?.ResetTrail(); return; }
            var view = enemy.View;
            float age = (float)enemy.ActionAge;
            if (_sequence != view.Sequence)
            {
                _sequence = view.Sequence; _clip = null; _hoofDistance = 0;
                // Recovery preserves the short headbutt tail. A new committed action
                // cancels its preceding warning; death cancels all living cues.
                if (view.Phase == BriarbackPhase.Dead) StopAudio();
                else if (view.Phase != BriarbackPhase.HeadbuttRecover) _source.Stop();
                if (view.Phase != BriarbackPhase.Hunt) _hoofSource.Stop();
                float latestStart = view.Phase == BriarbackPhase.Headbutt ? .06f : .3f;
                if (age < latestStart) PlayPhase(view.Phase, age);
            }
            if (enemy.CurrentHP < _previousHP && enemy.IsAlive)
            { Play(Pick(hurtClips, hurtClip, ref _lastHurt), _hurtSource, .65f); _hurtStart = Time.time; }
            _previousHP = enemy.CurrentHP;
            Vector3 displacement = transform.position - _previousPosition;
            float speed = displacement.magnitude / Mathf.Max(.001f, Time.deltaTime);
            _previousPosition = transform.position;
            // Charge already contains hoof foley. Only hunting adds distance-driven
            // steps; ignore teleports and emit at most one step per frame.
            displacement.y = 0;
            if (enemy.IsAlive && view.Phase == BriarbackPhase.Hunt && speed > .2f && displacement.magnitude < 2f)
            {
                _hoofDistance += displacement.magnitude;
                if (_hoofDistance >= .75f && Time.time >= _nextHoofTime)
                {
                    _hoofDistance %= .75f; _nextHoofTime = Time.time + .18f;
                    Play(Pick(hoofClips, null, ref _lastHoof), _hoofSource, .45f);
                }
            }
            else _hoofDistance = 0;
            bool trampling = enemy.IsAlive && view.Phase == BriarbackPhase.Charge && speed > .2f;
            bool headbuttImpact = enemy.IsAlive && view.Phase == BriarbackPhase.Headbutt &&
                age >= .06f && age < BriarbackCharge.HeadbuttSeconds;
            if (_trample == null && (trampling || headbuttImpact) && impactDustMaterial != null && impactStoneMaterial != null)
                _trample = new HollowWardenGroundImpact(impactDustMaterial, impactStoneMaterial, "Briarback trample trail");
            if (headbuttImpact) _trample?.EmitHeadbutt(transform.position, view.Forward, view.Sequence, age);
            _trample?.UpdateTrail(transform.position, view.Forward, Time.deltaTime, trampling);
            // EnemyBase switches the skeleton to physics on replicated death. Never
            // play/sample a death clip over ragdoll transforms, even on late observers.
            if (!enemy.IsAlive || !animator.enabled) { _lane.enabled = false; return; }
            string clip = view.Phase switch { BriarbackPhase.Windup => "BB_Windup", BriarbackPhase.Charge => "BB_Charge",
                BriarbackPhase.HeadbuttWindup => "BB_HeadbuttWindup", BriarbackPhase.Headbutt => "BB_Headbutt",
                BriarbackPhase.HeadbuttRecover => "BB_HeadbuttRecover",
                BriarbackPhase.Recover => "BB_Recover", BriarbackPhase.Dead => "BB_Death",
                _ => Time.time - _hurtStart < .3f ? "BB_Hurt" : speed > .1f ? "BB_Walk" : "BB_Idle" };
            if (_clip != clip)
            {
                _clip = clip;
                float duration = view.Phase switch { BriarbackPhase.Windup => BriarbackCharge.WindupSeconds,
                    BriarbackPhase.HeadbuttWindup => BriarbackCharge.HeadbuttWindupSeconds,
                    BriarbackPhase.Headbutt => BriarbackCharge.HeadbuttSeconds,
                    BriarbackPhase.HeadbuttRecover => BriarbackCharge.HeadbuttRecoverSeconds,
                    BriarbackPhase.Recover => BriarbackCharge.RecoverSeconds, BriarbackPhase.Dead => 2f, _ => 1f };
                bool loop = clip == "BB_Idle" || clip == "BB_Walk" || clip == "BB_Charge";
                animator.Play(clip, 0, loop ? 0 : clip == "BB_Hurt" ?
                    Mathf.Clamp01((Time.time - _hurtStart) / .3f) : Mathf.Clamp01(age / duration));
            }
            animator.speed = clip == "BB_Walk" ? Mathf.Clamp(speed / 3.2f, .5f, 1.8f) : 1;
            bool headbutt = view.Phase == BriarbackPhase.HeadbuttWindup;
            float warningDuration = headbutt ? BriarbackCharge.HeadbuttWindupSeconds : BriarbackCharge.WindupSeconds;
            _lane.enabled = enemy.IsAlive && (view.Phase == BriarbackPhase.Windup || headbutt) && age <= warningDuration;
            if (!_lane.enabled) return;
            Vector3 right = Vector3.Cross(Vector3.up, view.Forward);
            for (int i = 0; i < 9; i++)
                for (int side = 0; side < 2; side++)
                {
                    Vector3 point = headbutt ? transform.position +
                        Quaternion.AngleAxis(Mathf.Lerp(-60, 60, i / 8f), Vector3.up) * transform.forward *
                        (side == 0 ? 0 : BriarbackCharge.HeadbuttRange) :
                        view.Origin + view.Forward * (-BriarbackCharge.HitRadius +
                        i * (BriarbackCharge.ChargeLength + 2 * BriarbackCharge.HitRadius) / 8f) +
                        right * ((side * 2 - 1) * BriarbackCharge.HitRadius);
                    point.y = HollowWardenPresentation.SampleGround(point, view.Origin.y, out _) + .045f;
                    _vertices[i * 2 + side] = transform.InverseTransformPoint(point);
                }
            _laneMesh.vertices = _vertices; _laneMesh.RecalculateBounds();
            _properties.SetColor("_BaseColor", new Color(1, .36f, .07f, .28f + .28f * Mathf.Clamp01(age / warningDuration)));
            _lane.SetPropertyBlock(_properties);
        }
    }
}
