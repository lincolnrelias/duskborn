using UnityEngine;
using Duskborn.Gameplay.Player;

namespace Duskborn.Audio
{
    /// <summary>
    /// Feedback sonoro de vitalidade e combate para o jogador.
    /// Gerencia gemidos de dano (hurt grunts), som de morte e a batida cardíaca imersiva (heartbeat)
    /// quando a vida cai abaixo de 30%.
    /// </summary>
    [RequireComponent(typeof(PlayerStats))]
    public class PlayerAudioFeedback : MonoBehaviour
    {
        [Header("Clipes de Dano e Morte")]
        [SerializeField] private AudioClip[] hurtClips;
        [SerializeField] private AudioClip deathClip;
        [SerializeField] private AudioClip heartbeatLoopClip;

        [Header("Configurações")]
        [SerializeField] [Range(0f, 1f)] private float hurtVolume = 0.85f;
        [SerializeField] [Range(0f, 1f)] private float deathVolume = 1.0f;
        [SerializeField] private float lowHpThreshold = 0.30f;

        private PlayerStats _stats;
        private AudioSource _heartbeatSource;
        private AudioSource _voiceSource;
        private float _lastHP;

        private void Awake()
        {
            _stats = GetComponent<PlayerStats>();

            _voiceSource = gameObject.AddComponent<AudioSource>();
            _voiceSource.playOnAwake = false;
            _voiceSource.spatialBlend = 0f; // 2D para feedback do jogador local

            _heartbeatSource = gameObject.AddComponent<AudioSource>();
            _heartbeatSource.playOnAwake = false;
            _heartbeatSource.loop = true;
            _heartbeatSource.spatialBlend = 0f;

            LoadClipsIfNull();
        }

        private void LoadClipsIfNull()
        {
            if (hurtClips == null || hurtClips.Length == 0)
            {
                hurtClips = new AudioClip[]
                {
                    Resources.Load<AudioClip>("SFX/player_hurt_01"),
                    Resources.Load<AudioClip>("SFX/player_hurt_02")
                };
            }

            if (deathClip == null) deathClip = Resources.Load<AudioClip>("SFX/player_death");
            if (heartbeatLoopClip == null) heartbeatLoopClip = Resources.Load<AudioClip>("SFX/heartbeat_loop");
        }

        private void Start()
        {
            if (heartbeatLoopClip != null)
            {
                _heartbeatSource.clip = heartbeatLoopClip;
            }

            _lastHP = _stats.CurrentHP;
            _stats.OnHealthChanged += HandleHealthChanged;
            _stats.OnDied += HandleDied;
        }

        private void OnDestroy()
        {
            if (_stats != null)
            {
                _stats.OnHealthChanged -= HandleHealthChanged;
                _stats.OnDied -= HandleDied;
            }
        }

        private void HandleHealthChanged(float current, float max)
        {
            // Dano recebido
            if (current < _lastHP && current > 0f)
            {
                PlayHurt();
            }

            _lastHP = current;

            // Heartbeat em perigo crítico
            float hpRatio = max > 0f ? (current / max) : 1f;
            if (hpRatio < lowHpThreshold && current > 0f)
            {
                if (!_heartbeatSource.isPlaying && heartbeatLoopClip != null)
                {
                    _heartbeatSource.Play();
                }

                // Volume aumenta quanto mais perto da morte
                float danger = 1f - (hpRatio / lowHpThreshold);
                _heartbeatSource.volume = Mathf.Lerp(0.3f, 0.9f, danger) *
                    (AudioManager.Instance != null ? AudioManager.Instance.MasterVolume * AudioManager.Instance.SfxVolume : 1f);
            }
            else
            {
                if (_heartbeatSource.isPlaying)
                {
                    _heartbeatSource.Stop();
                }
            }
        }

        private void HandleDied()
        {
            if (_heartbeatSource.isPlaying) _heartbeatSource.Stop();

            if (deathClip != null)
            {
                _voiceSource.pitch = 1.0f;
                _voiceSource.PlayOneShot(deathClip, deathVolume * (AudioManager.Instance != null ? AudioManager.Instance.MasterVolume * AudioManager.Instance.SfxVolume : 1f));
            }
        }

        private void PlayHurt()
        {
            var clip = hurtClips.RandomOrNull();
            if (clip != null)
            {
                _voiceSource.pitch = 1.0f + Random.Range(-0.06f, 0.06f);
                _voiceSource.PlayOneShot(clip, hurtVolume * (AudioManager.Instance != null ? AudioManager.Instance.MasterVolume * AudioManager.Instance.SfxVolume : 1f));
            }
        }
    }
}
