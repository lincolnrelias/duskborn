using UnityEngine;
using Duskborn.Gameplay.Enemies;

namespace Duskborn.Audio
{
    /// <summary>
    /// Feedback sonoro 3D para inimigos (grunhidos de ataque, guinchos de dor e ruído visceral de morte).
    /// </summary>
    [RequireComponent(typeof(EnemyBase))]
    public class EnemyAudioFeedback : MonoBehaviour
    {
        [Header("Vocalizações do Inimigo")]
        [SerializeField] private AudioClip[] attackClips;
        [SerializeField] private AudioClip[] hurtClips;
        [SerializeField] private AudioClip[] deathClips;

        [Header("Configurações")]
        [SerializeField] [Range(0f, 1f)] private float attackVolume = 0.8f;
        [SerializeField] [Range(0f, 1f)] private float hurtVolume   = 0.75f;
        [SerializeField] [Range(0f, 1f)] private float deathVolume  = 0.9f;
        [SerializeField] private float minDistance = 2f;
        [SerializeField] private float maxDistance = 30f;

        private EnemyBase   _enemy;
        private AudioSource _audioSource;
        private float       _lastHP;

        private void Awake()
        {
            _enemy = GetComponent<EnemyBase>();

            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();

            _audioSource.playOnAwake  = false;
            _audioSource.spatialBlend = 1f; // 3D espacial
            _audioSource.rolloffMode  = AudioRolloffMode.Logarithmic;
            _audioSource.minDistance  = minDistance;
            _audioSource.maxDistance  = maxDistance;

            LoadClipsIfNull();
        }

        private void LoadClipsIfNull()
        {
            var db = AudioDatabase.Instance?.Enemies;
            if (db != null)
            {
                if (attackClips == null || attackClips.Length == 0) attackClips = db.swarmerAttackClips;
                if (hurtClips == null || hurtClips.Length == 0) hurtClips = db.swarmerHurtClips;
                if (deathClips == null || deathClips.Length == 0) deathClips = db.swarmerDeathClips;

                attackVolume = db.attackVolume;
                hurtVolume = db.hurtVolume;
                deathVolume = db.deathVolume;
                minDistance = db.minDistance;
                maxDistance = db.maxDistance;
            }
            else
            {
                if (attackClips == null || attackClips.Length == 0)
                {
                    attackClips = new AudioClip[]
                    {
                        Resources.Load<AudioClip>("SFX/enemy_swarmer_attack_01"),
                        Resources.Load<AudioClip>("SFX/enemy_swarmer_attack_02")
                    };
                }

                if (hurtClips == null || hurtClips.Length == 0)
                {
                    hurtClips = new AudioClip[]
                    {
                        Resources.Load<AudioClip>("SFX/enemy_swarmer_hurt_01"),
                        Resources.Load<AudioClip>("SFX/enemy_swarmer_hurt_02")
                    };
                }

                if (deathClips == null || deathClips.Length == 0)
                {
                    deathClips = new AudioClip[]
                    {
                        Resources.Load<AudioClip>("SFX/enemy_swarmer_death_01"),
                        Resources.Load<AudioClip>("SFX/enemy_swarmer_death_02")
                    };
                }
            }
        }

        private void Start()
        {
            _lastHP = _enemy.CurrentHP;
            _enemy.OnHealthChanged += HandleHealthChanged;
            _enemy.OnDied += HandleDied;
        }

        private void OnDestroy()
        {
            if (_enemy != null)
            {
                _enemy.OnHealthChanged -= HandleHealthChanged;
                _enemy.OnDied -= HandleDied;
            }
        }

        private void HandleHealthChanged(float current, float max)
        {
            if (current < _lastHP && current > 0f)
            {
                PlayClip(hurtClips.RandomOrNull(), hurtVolume);
            }
            _lastHP = current;
        }

        private void HandleDied(EnemyBase enemy)
        {
            // Reproduz via AudioManager 3D para o som não ser cortado se o GameObject for desativado
            var clip = deathClips.RandomOrNull();
            if (clip != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayAtPoint(clip, transform.position, deathVolume, minDistance, maxDistance, 0.06f);
            }
            else if (clip != null)
            {
                PlayClip(clip, deathVolume);
            }
        }

        public void PlayAttackVoice()
        {
            PlayClip(attackClips.RandomOrNull(), attackVolume);
        }

        private void PlayClip(AudioClip clip, float volume)
        {
            if (clip == null) return;
            _audioSource.pitch = 1.0f + Random.Range(-0.08f, 0.08f);
            _audioSource.PlayOneShot(clip, volume * (AudioManager.Instance != null ? AudioManager.Instance.MasterVolume * AudioManager.Instance.SfxVolume : 1f));
        }
    }
}
