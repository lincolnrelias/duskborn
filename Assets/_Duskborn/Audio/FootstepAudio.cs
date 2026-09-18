using UnityEngine;

namespace Duskborn.Audio
{
    /// <summary>
    /// Sistema de áudio de locomoção e passos para o jogador.
    /// Detecta a velocidade de deslocamento do CharacterController, identifica a superfície do solo
    /// via Raycast e reproduz passos com cadência orgânica e variação de pitch, além de salto e aterrissagem.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FootstepAudio : MonoBehaviour
    {
        [Header("Clipes de Passos")]
        [SerializeField] private AudioClip[] grassSteps;
        [SerializeField] private AudioClip[] stoneSteps;

        [Header("Salto e Queda")]
        [SerializeField] private AudioClip jumpClip;
        [SerializeField] private AudioClip landClip;

        [Header("Cadência de Passos")]
        [SerializeField] private float walkStepInterval = 0.45f;
        [SerializeField] private float sprintStepInterval = 0.32f;
        [SerializeField] private float velocityThreshold = 1.0f;
        [SerializeField] private float sprintSpeedThreshold = 6.0f;

        [Header("Áudio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] [Range(0f, 1f)] private float footstepVolume = 0.65f;
        [SerializeField] private float pitchVariation = 0.08f;

        private CharacterController _cc;
        private float _stepTimer;
        private bool _wasGrounded;
        private float _airborneTimer;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
            audioSource.minDistance = 1.5f;
            audioSource.maxDistance = 25f;
        }

        private void Start()
        {
            LoadClipsIfNull();
            _wasGrounded = _cc.isGrounded;
        }

        private void LoadClipsIfNull()
        {
            var db = AudioDatabase.Instance?.Player;
            if (db != null)
            {
                if (grassSteps == null || grassSteps.Length == 0) grassSteps = db.grassSteps;
                if (stoneSteps == null || stoneSteps.Length == 0) stoneSteps = db.stoneSteps;
                if (jumpClip == null) jumpClip = db.jumpClip;
                if (landClip == null) landClip = db.landClip;

                footstepVolume = db.footstepVolume;
                pitchVariation = db.pitchVariation;
            }
            else
            {
                if (grassSteps == null || grassSteps.Length == 0)
                {
                    grassSteps = new AudioClip[]
                    {
                        Resources.Load<AudioClip>("SFX/footstep_grass_01"),
                        Resources.Load<AudioClip>("SFX/footstep_grass_02"),
                        Resources.Load<AudioClip>("SFX/footstep_grass_03"),
                        Resources.Load<AudioClip>("SFX/footstep_grass_04")
                    };
                }

                if (stoneSteps == null || stoneSteps.Length == 0)
                {
                    stoneSteps = new AudioClip[]
                    {
                        Resources.Load<AudioClip>("SFX/footstep_stone_01"),
                        Resources.Load<AudioClip>("SFX/footstep_stone_02"),
                        Resources.Load<AudioClip>("SFX/footstep_stone_03")
                    };
                }

                if (jumpClip == null) jumpClip = Resources.Load<AudioClip>("SFX/jump_takeoff");
                if (landClip == null) landClip = Resources.Load<AudioClip>("SFX/jump_land");
            }
        }

        private void Update()
        {
            bool isGrounded = _cc.isGrounded;

            // Transição de aterrissagem
            if (!_wasGrounded && isGrounded)
            {
                if (_airborneTimer > 0.18f && landClip != null)
                {
                    PlayClipWithPitch(landClip, footstepVolume * 1.1f);
                }
                _airborneTimer = 0f;
            }
            else if (!isGrounded)
            {
                _airborneTimer += Time.deltaTime;
            }

            _wasGrounded = isGrounded;

            if (!isGrounded) return;

            // Cálculo da velocidade horizontal
            Vector3 horizontalVel = new Vector3(_cc.velocity.x, 0f, _cc.velocity.z);
            float speed = horizontalVel.magnitude;

            if (speed > velocityThreshold)
            {
                float targetInterval = speed > sprintSpeedThreshold ? sprintStepInterval : walkStepInterval;
                _stepTimer += Time.deltaTime;

                if (_stepTimer >= targetInterval)
                {
                    _stepTimer = 0f;
                    PlayFootstep();
                }
            }
            else
            {
                _stepTimer = targetIntervalOrDefault();
            }
        }

        private float targetIntervalOrDefault() => walkStepInterval * 0.85f;

        private void PlayFootstep()
        {
            string surfaceTag = DetectSurfaceTag();
            AudioClip clip = (surfaceTag == "Stone" || surfaceTag == "Metal")
                ? stoneSteps.RandomOrNull()
                : grassSteps.RandomOrNull();

            if (clip != null)
            {
                PlayClipWithPitch(clip, footstepVolume);
            }
        }

        private string DetectSurfaceTag()
        {
            if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 1.5f))
            {
                return hit.collider.tag;
            }
            return "Grass";
        }

        public void PlayJump()
        {
            if (jumpClip != null)
            {
                PlayClipWithPitch(jumpClip, footstepVolume * 0.9f);
            }
        }

        private void PlayClipWithPitch(AudioClip clip, float volume)
        {
            if (clip == null) return;
            audioSource.pitch = 1.0f + Random.Range(-pitchVariation, pitchVariation);
            audioSource.PlayOneShot(clip, volume * (AudioManager.Instance != null ? AudioManager.Instance.MasterVolume * AudioManager.Instance.SfxVolume : 1f));
        }
    }
}
