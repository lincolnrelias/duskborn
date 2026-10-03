using Duskborn.Audio;
using UnityEngine;

namespace Duskborn.Gameplay.Enemies
{
    /// <summary>Procedural living flight with event-driven sound. Corpse motion belongs to physics.</summary>
    public sealed class ThornwingPresentation : MonoBehaviour
    {
        [SerializeField] private Thornwing enemy;
        [SerializeField] private Transform visual, leftWing, rightWing;
        [SerializeField] private Renderer eyes;
        [SerializeField] private Material flightTrailMaterial;
        private TrailRenderer[] flightTrails;
        private Vector3 previousTrailPosition;
        [SerializeField] private AudioClip[] windupClips, spitClips, hurtClips, deathClips, flutterClips, buzzClips;
        private Quaternion leftRest, rightRest;
        private AudioSource actionSource, voiceSource, flutterSource, buzzSource;
        private MaterialPropertyBlock block;
        private EnemyRagdoll ragdoll;
        private bool deathCuePlayed;
        private int sequence = -1, lastWindup = -1, lastSpit = -1, lastHurt = -1, lastDeath = -1, lastFlutter = -1;
        private float previousHP, deathAge = -1, flutterAt, hurtAt = -100, shotAt = -100, offset;
        private static readonly int Emission = Shader.PropertyToID("_EmissionColor");

        private void Awake()
        {
            leftRest = leftWing.localRotation; rightRest = rightWing.localRotation;
            block = new MaterialPropertyBlock(); offset = Random.value * 6.28f;
            actionSource = Source(); voiceSource = Source(); flutterSource = Source();
            actionSource.priority = voiceSource.priority = 64; flutterSource.priority = 160;
            buzzSource = Source(); buzzSource.loop = true; buzzSource.priority = 128;
            buzzSource.minDistance = 3; buzzSource.maxDistance = 24;
            ragdoll = GetComponent<EnemyRagdoll>();
            flightTrails = new[] { CreateFlightTrail(leftWing), CreateFlightTrail(rightWing) };
            enemy.OnHealthChanged += HealthChanged;
        }
        private void OnDestroy() { if (enemy != null) enemy.OnHealthChanged -= HealthChanged; }
        private AudioSource Source()
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false; source.spatialBlend = 1; source.minDistance = 6;
            source.maxDistance = 28; source.dopplerLevel = 0; source.rolloffMode = AudioRolloffMode.Linear;
            return source;
        }
        private void OnEnable() => ResetPresentation();
        private void OnDisable()
        {
            ClearFlightTrails();
            actionSource?.Stop(); voiceSource?.Stop(); flutterSource?.Stop();
            buzzSource?.Stop();
        }
        public void ResetPresentation()
        {
            ClearFlightTrails();
            previousTrailPosition = transform.position;
            sequence = -1; previousHP = enemy != null ? enemy.CurrentHP : 0;
            deathAge = -1; flutterAt = Time.time + .3f; shotAt = hurtAt = -100;
            deathCuePlayed = false;
            lastWindup = lastSpit = lastHurt = lastDeath = lastFlutter = -1;
            actionSource?.Stop(); voiceSource?.Stop(); flutterSource?.Stop();
            buzzSource?.Stop(); if (buzzSource != null) buzzSource.clip = null;
            // A joining observer may already be a corpse. Never overwrite physics poses.
            if (ragdoll == null || !ragdoll.IsRagdoll)
            {
                if (visual != null) { visual.localPosition = Vector3.up * Thornwing.HoverHeight; visual.localRotation = Quaternion.identity; }
                if (leftWing != null) leftWing.localRotation = leftRest;
                if (rightWing != null) rightWing.localRotation = rightRest;
            }
            if (eyes != null && block != null) { block.Clear(); eyes.SetPropertyBlock(block); }
        }
        private static AudioClip Pick(AudioClip[] clips, ref int last)
        {
            if (clips == null || clips.Length == 0) return null;
            int i = Random.Range(0, clips.Length > 1 && last >= 0 ? clips.Length - 1 : clips.Length);
            if (clips.Length > 1 && last >= 0 && i >= last) i++;
            last = i; return clips[i];
        }
        private static void Play(AudioSource source, AudioClip clip, float volume, float age = 0)
        {
            if (source == null || clip == null || age >= clip.length) return;
            source.Stop(); source.clip = clip;
            var manager = AudioManager.Instance;
            // Master volume already applies through AudioListener; don't attenuate it twice.
            source.volume = volume * (manager != null ? manager.SfxVolume : 1);
            source.time = Mathf.Clamp(age, 0, clip.length - .001f); source.Play();
        }
        public void PlayShot()
        {
            if (enemy == null || !enemy.IsAlive || !enemy.IsClientStarted) return;
            shotAt = Time.time; Play(actionSource, Pick(spitClips, ref lastSpit), .9f);
        }
        private void HealthChanged(float current, float maximum)
        {
            float before = previousHP; previousHP = current;
            if (current > 0 && before <= 0)
            {
                deathCuePlayed = false;
                // Remote pooled revival can arrive after OnStartClient saw stale dead HP.
                if (ragdoll != null && ragdoll.IsRagdoll) ragdoll.DisableRagdoll();
            }
            if (enemy == null || !enemy.IsSpawned || !enemy.IsClientStarted || current >= before) return;
            if (current > 0) { hurtAt = Time.time; Play(voiceSource, Pick(hurtClips, ref lastHurt), .8f); }
            else if (!deathCuePlayed)
            {
                deathCuePlayed = true;
                actionSource.Stop(); flutterSource.Stop(); voiceSource.Stop();
                buzzSource.Stop();
                Play(voiceSource, Pick(deathClips, ref lastDeath), .95f);
            }
        }
        private void LateUpdate()
        {
            if (enemy == null || !enemy.IsSpawned || !enemy.IsClientStarted) return;
            float age = (float)enemy.ActionAge;
            if (enemy.CurrentHP != previousHP) HealthChanged(enemy.CurrentHP, enemy.MaxHP);
            // Death impulse RPC may beat health replication. Stop pose writes in either order.
            if (!enemy.IsAlive || (ragdoll != null && ragdoll.IsRagdoll))
            {
                if (flightTrails != null) foreach (var trail in flightTrails) trail.emitting = false;
                buzzSource.Stop();
                if (deathAge < 0)
                {
                    actionSource.Stop(); flutterSource.Stop();
                    deathAge = 0;
                }
                block.SetColor(Emission, Color.black); eyes.SetPropertyBlock(block);
                return;
            }
            if (deathAge >= 0 || previousHP <= 0) ResetPresentation();
            previousHP = enemy.CurrentHP;
            UpdateBuzz();
            if (sequence != enemy.View.Sequence)
            {
                sequence = enemy.View.Sequence;
                if (enemy.View.Phase == ThornwingPhase.Windup)
                    Play(actionSource, Pick(windupClips, ref lastWindup), .85f, age);
                else if (enemy.View.Phase == ThornwingPhase.Recover && Time.time - shotAt > .2f)
                    actionSource.Stop();
            }
            bool warning = enemy.View.Phase == ThornwingPhase.Windup;
            float t = Time.time * (warning ? 30 : 42) + offset;
            float flap = Mathf.Sin(t) * (warning ? 18 : 35);
            leftWing.localRotation = Quaternion.Euler(0, 0, flap) * leftRest;
            rightWing.localRotation = Quaternion.Euler(0, 0, -flap) * rightRest;
            // One stable flight anchor for the visible body, hitbox and server shot socket.
            // Wing rotation supplies flight motion without moving the body off that anchor.
            visual.localPosition = Vector3.up * Thornwing.HoverHeight;
            visual.localRotation = Quaternion.identity;
            float glow = warning ? 2 + 4 * Mathf.Clamp01(age / ThornwingClock.WindupSeconds) : 2f;
            block.SetColor(Emission, new Color(1, .3f, .015f) * glow); eyes.SetPropertyBlock(block);
            UpdateFlightTrails();
            if (!warning && Time.time >= flutterAt)
            { flutterAt = Time.time + 1.1f + Random.value * .6f; Play(flutterSource, Pick(flutterClips, ref lastFlutter), .3f); }
        }
        private TrailRenderer CreateFlightTrail(Transform wing)
        {
            var tip = new GameObject("FlightTrail"); tip.layer = gameObject.layer;
            tip.transform.SetParent(wing, false);
            var bounds = wing.GetComponent<MeshFilter>().sharedMesh.bounds;
            // Choose the outer edge of each imported wing in its own mesh units.
            Vector3 localTip = bounds.center;
            localTip.x = Mathf.Abs(bounds.min.x) > Mathf.Abs(bounds.max.x) ? bounds.min.x : bounds.max.x;
            tip.transform.localPosition = localTip;
            var trail = tip.AddComponent<TrailRenderer>();
            trail.sharedMaterial = flightTrailMaterial; trail.time = .28f;
            trail.minVertexDistance = .07f; trail.startWidth = .075f; trail.endWidth = 0;
            trail.startColor = new Color(1f, .5f, .08f, .65f);
            trail.endColor = new Color(1f, .25f, .015f, 0);
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false; trail.autodestruct = false; trail.emitting = false;
            return trail;
        }
        private void ClearFlightTrails()
        {
            if (flightTrails == null) return;
            foreach (var trail in flightTrails) { trail.emitting = false; trail.Clear(); }
        }
        private void UpdateFlightTrails()
        {
            Vector3 position = transform.position;
            float distance = Vector3.Distance(previousTrailPosition, position);
            if (distance > 3f) ClearFlightTrails(); // Network teleports/reuse must not draw long ribbons.
            bool moving = distance > .002f && distance <= 3f;
            foreach (var trail in flightTrails) trail.emitting = moving;
            previousTrailPosition = position;
        }
        private void UpdateBuzz()
        {
            if (buzzSource.clip == null && buzzClips != null && buzzClips.Length > 0)
            {
                buzzSource.clip = buzzClips[Random.Range(0,buzzClips.Length)];
                buzzSource.pitch = Random.Range(.96f,1.04f);
                buzzSource.time = Random.value * buzzSource.clip.length;
            }
            var manager = AudioManager.Instance;
            buzzSource.volume = .35f * (manager != null ? manager.SfxVolume : 1);
            if (buzzSource.clip != null && !buzzSource.isPlaying) buzzSource.Play();
        }
    }
}
