using Duskborn.Audio;
using Duskborn.Core;
using UnityEngine;

namespace Duskborn.Gameplay.Enemies
{
    /// <summary>Client-only audio and breakup, driven by replicated encounter time.</summary>
    [DefaultExecutionOrder(200)]
    public sealed class HollowWardenFeedback : MonoBehaviour
    {
        private HollowWardenBoss _boss;
        private AudioDatabase _database;
        private AudioSource _action, _channel;
        private readonly AudioSource[] _spikes = new AudioSource[4];
        private int _sequence = -1, _lastWave = -1, _voice;
        private bool _impact, _broken;
        private SkinnedMeshRenderer[] _bodies;
        private HollowWardenDebris _debris;

        private void Awake()
        {
            _boss = GetComponent<HollowWardenBoss>();
            _bodies = GetComponentsInChildren<SkinnedMeshRenderer>();
            // These are local sources, never NetworkObjects; dedicated servers create no voices.
        }

        private AudioSource Source(string label, bool loop = false)
        {
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 1;
            source.minDistance = 4;
            source.maxDistance = 45;
            source.dopplerLevel = 0;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            return source;
        }

        private void InitializeAudio()
        {
            if (_action != null) return;
            _database = AudioDatabase.Instance;
            _action = Source("Warden actions");
            _channel = Source("Warden channel", true);
            for (int i = 0; i < _spikes.Length; i++) _spikes[i] = Source("Warden eruption " + i);
        }

        private static void Play(AudioSource source, AudioClip clip, float pitch = 1)
        {
            if (source == null || clip == null) return;
            source.clip = clip;
            source.pitch = pitch;
            source.Play(); // One voice per source; no unbounded PlayOneShot overlap.
        }

        private void LateUpdate()
        {
            if (!_boss.IsSpawned || !_boss.IsClientStarted ||
                (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState != GameState.Running))
            { StopAudio(); return; }
            InitializeAudio();
            var view = _boss.ActionView;
            float age = (float)_boss.ActionAge;
            // Master is applied by AudioListener through AudioManager; do not multiply it twice.
            float volume = AudioManager.Instance.SfxVolume;
            _action.volume = volume * .85f;
            _channel.volume = volume * .32f;
            foreach (var source in _spikes) source.volume = volume * .6f;

            if (view.State != WardenState.Dead && _broken)
            {
                if (_debris != null) Destroy(_debris.gameObject);
                foreach (var body in _bodies) if (body != null) body.enabled = true;
                _broken = false;
                _lastWave = -1;
            }
            if (_sequence != view.Sequence)
            {
                _sequence = view.Sequence;
                _impact = false;
                _action.Stop();
                // Late observers do not replay old one-shot windups.
                if (age < .25f && _database != null)
                {
                    switch (view.State)
                    {
                        case WardenState.RootPlant:
                            Play(_action, _database.ResourcesSettings.treeFallClips.RandomOrNull(), .7f); break;
                        case WardenState.Rootbreaker:
                        case WardenState.HarvestSweep:
                            Play(_action, _database.Combat.heavySwingClips.RandomOrNull(), .7f * view.Rate); break;
                        case WardenState.PhaseBreak:
                            Play(_action, _database.ResourcesSettings.rockShatterClips.RandomOrNull(), .65f); break;
                    }
                }
            }
            if (view.State == WardenState.Rooted && _database != null)
            {
                if (!_channel.isPlaying) Play(_channel, _database.Player.heartbeatLoopClip, .65f);
            }
            else _channel.Stop();

            if (view.State == WardenState.Rootbreaker || view.State == WardenState.HarvestSweep)
            {
                float impactAt = (view.State == WardenState.Rootbreaker ? 1f : .9f) / Mathf.Max(1, view.Rate);
                if (!_impact && age >= impactAt)
                {
                    _impact = true;
                    if (age < impactAt + .25f && _database != null)
                        Play(_action, view.State == WardenState.Rootbreaker
                            ? _database.ResourcesSettings.rockShatterClips.RandomOrNull()
                            : _database.Combat.woodHitClips.RandomOrNull(), .75f);
                }
            }
            if (view.State == WardenState.Rooted)
            {
                // One nearest marker per wave, regardless of player count or marker overlap.
                int newest = _lastWave;
                Vector3 point = transform.position;
                float nearest = float.MaxValue;
                Vector3 listener = Camera.main != null ? Camera.main.transform.position : transform.position;
                foreach (var spike in _boss.Spikes)
                {
                    double spikeAge = _boss.SpikeAge(spike);
                    if (spike.Wave <= _lastWave || spikeAge < HollowWardenEncounter.SpikeDelay ||
                        spikeAge > HollowWardenEncounter.SpikeDelay + .25) continue;
                    float distance = (listener - spike.Position).sqrMagnitude;
                    if (spike.Wave > newest) { newest = spike.Wave; nearest = float.MaxValue; }
                    if (spike.Wave == newest && distance < nearest) { nearest = distance; point = spike.Position; }
                }
                if (newest > _lastWave)
                {
                    _lastWave = newest;
                    var source = _spikes[_voice++ % _spikes.Length];
                    source.transform.position = point;
                    if (_database != null) Play(source, _database.Combat.woodHitClips.RandomOrNull(), .65f);
                }
            }
            else foreach (var source in _spikes) source.Stop();

            if (view.State == WardenState.Dead && !_broken)
            {
                _broken = true;
                _debris = HollowWardenDebris.Create(_bodies, view.Sequence, age);
                if (_debris != null)
                {
                    foreach (var body in _bodies) if (body != null) body.enabled = false;
                    if (age < .25f) _debris.PlayBreakup(_database);
                }
            }
        }

        private void StopAudio()
        {
            if (_action != null) _action.Stop();
            if (_channel != null) _channel.Stop();
            foreach (var source in _spikes) if (source != null) source.Stop();
        }

        private void OnDisable()
        {
            StopAudio();
            _sequence = -1;
            _lastWave = -1;
        }
    }
}
