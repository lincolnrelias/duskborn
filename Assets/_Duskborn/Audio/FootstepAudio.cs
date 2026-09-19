using UnityEngine;
using Duskborn.Gameplay.Player;
using Duskborn.Gameplay.World;

namespace Duskborn.Audio
{
    /// <summary>
    /// Sistema de áudio de locomoção e passos para o jogador.
    /// Detecta a velocidade de deslocamento, identifica a superfície do solo via Raycast
    /// (Grama, Terra/Areia, Rocha/Pedra e Água) e reproduz passos com cadência orgânica,
    /// variação de pitch, além de sons de salto e aterrissagem.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FootstepAudio : MonoBehaviour
    {
        [Header("Clipes de Passos por Superfície")]
        [SerializeField] private AudioClip[] grassSteps;
        [SerializeField] private AudioClip[] dirtSteps;
        [SerializeField] private AudioClip[] stoneSteps;
        [SerializeField] private AudioClip[] waterSteps;

        [Header("Salto e Queda")]
        [SerializeField] private AudioClip jumpClip;
        [SerializeField] private AudioClip landClip;

        [Header("Cadência de Passos")]
        [SerializeField] private float walkStepInterval = 0.44f;
        [SerializeField] private float sprintStepInterval = 0.30f;
        [SerializeField] private float velocityThreshold = 0.7f;
        [SerializeField] private float sprintSpeedThreshold = 5.8f;

        [Header("Áudio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] [Range(0f, 1f)] private float footstepVolume = 0.65f;
        [SerializeField] private float pitchVariation = 0.08f;

        private CharacterController _cc;
        private PlayerController _playerController;
        private PlayerWaterInteraction _waterInteraction;

        private float _stepTimer;
        private bool _wasGrounded;
        private float _airborneTimer;
        private Vector3 _lastPosition;
        private float _coyoteGroundedTimer;

        public SurfaceType CurrentSurface { get; private set; } = SurfaceType.Grass;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _playerController = GetComponent<PlayerController>();
            _waterInteraction = GetComponent<PlayerWaterInteraction>();

            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

            audioSource.playOnAwake = false;

            // Configuração espacial: o jogador local precisa ouvir passos com clareza
            // independente do raio de órbita da câmera em terceira pessoa
            bool isLocalOwner = _playerController == null || _playerController.IsOwner;
            audioSource.spatialBlend = isLocalOwner ? 0.15f : 1.0f;
            audioSource.minDistance = isLocalOwner ? 5.0f : 2.0f;
            audioSource.maxDistance = 30f;

            _lastPosition = transform.position;
        }

        private void Start()
        {
            LoadClipsIfNull();
            _wasGrounded = _cc != null && _cc.isGrounded;
        }

        public void LoadClipsIfNull()
        {
            var db = AudioDatabase.Instance?.Player;
            if (db != null)
            {
                if (grassSteps == null || grassSteps.Length == 0) grassSteps = db.grassSteps;
                if (dirtSteps  == null || dirtSteps.Length == 0)  dirtSteps  = db.dirtSteps;
                if (stoneSteps == null || stoneSteps.Length == 0) stoneSteps = db.stoneSteps;
                if (waterSteps == null || waterSteps.Length == 0) waterSteps = db.waterSteps;
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

                if (dirtSteps == null || dirtSteps.Length == 0)
                {
                    dirtSteps = new AudioClip[]
                    {
                        Resources.Load<AudioClip>("SFX/footstep_dirt_01"),
                        Resources.Load<AudioClip>("SFX/footstep_dirt_02"),
                        Resources.Load<AudioClip>("SFX/footstep_dirt_03"),
                        Resources.Load<AudioClip>("SFX/footstep_dirt_04")
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

                if (waterSteps == null || waterSteps.Length == 0)
                {
                    waterSteps = new AudioClip[]
                    {
                        Resources.Load<AudioClip>("SFX/footstep_water_01"),
                        Resources.Load<AudioClip>("SFX/footstep_water_02"),
                        Resources.Load<AudioClip>("SFX/footstep_water_03"),
                        Resources.Load<AudioClip>("SFX/footstep_water_04")
                    };
                }

                if (jumpClip == null) jumpClip = Resources.Load<AudioClip>("SFX/jump_takeoff");
                if (landClip == null) landClip = Resources.Load<AudioClip>("SFX/jump_land");
            }
        }

        private void Update()
        {
            if (_cc == null) return;

            // Suavização do estado de grounded para evitar interrupções em declives low-poly
            bool rawGrounded = _cc.isGrounded;
            if (rawGrounded)
            {
                _coyoteGroundedTimer = 0.15f;
            }
            else
            {
                _coyoteGroundedTimer -= Time.deltaTime;
            }

            bool isGrounded = rawGrounded || _coyoteGroundedTimer > 0f;

            // Transição de aterrissagem
            if (!_wasGrounded && rawGrounded)
            {
                if (_airborneTimer > 0.18f && landClip != null)
                {
                    PlayClipWithPitch(landClip, footstepVolume * 1.15f);
                }
                _airborneTimer = 0f;
            }
            else if (!rawGrounded)
            {
                _airborneTimer += Time.deltaTime;
            }

            _wasGrounded = rawGrounded;

            if (!isGrounded)
            {
                _lastPosition = transform.position;
                return;
            }

            // Cálculo robusto da velocidade horizontal
            Vector3 horizontalVel = new Vector3(_cc.velocity.x, 0f, _cc.velocity.z);
            float ccSpeed = horizontalVel.magnitude;

            // Cálculo por delta de posição para proteção contra sobreposições de física
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            Vector3 posDelta = (transform.position - _lastPosition) / dt;
            float deltaSpeed = new Vector2(posDelta.x, posDelta.z).magnitude;
            _lastPosition = transform.position;

            float speed = Mathf.Max(ccSpeed, deltaSpeed);

            // Se o PlayerController local estiver movendo, assegura velocidade mínima
            if (_playerController != null && _playerController.IsMoving && speed < velocityThreshold)
            {
                speed = _playerController.IsSprinting ? 7.5f : 5.0f;
            }

            if (speed > velocityThreshold)
            {
                bool isSprinting = (_playerController != null && _playerController.IsSprinting) || speed > sprintSpeedThreshold;
                float targetInterval = isSprinting ? sprintStepInterval : walkStepInterval;

                // Na água a passada é ligeiramente mais cadenciada
                if (CurrentSurface == SurfaceType.Water)
                {
                    targetInterval *= 1.12f;
                }

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
            CurrentSurface = DetectSurface();
            AudioClip clip = CurrentSurface switch
            {
                SurfaceType.Water => waterSteps.RandomOrNull() ?? grassSteps.RandomOrNull(),
                SurfaceType.Dirt  => dirtSteps.RandomOrNull()  ?? grassSteps.RandomOrNull(),
                SurfaceType.Rock  => stoneSteps.RandomOrNull() ?? grassSteps.RandomOrNull(),
                _                 => grassSteps.RandomOrNull()
            };

            if (clip != null)
            {
                float volume = footstepVolume;
                if (CurrentSurface == SurfaceType.Water) volume *= 1.1f;
                PlayClipWithPitch(clip, volume);
            }
        }

        public SurfaceType DetectSurface()
        {
            // 1. Prioridade Água: PlayerWaterInteraction
            if (_waterInteraction != null && _waterInteraction.IsInWater)
            {
                return SurfaceType.Water;
            }

            // 2. Prioridade Água: Posição vertical abaixo da lâmina d'água
            if (ChunkGridManager.Instance != null && ChunkGridManager.Instance.generateWaterPlane)
            {
                float waterLevel = ChunkGridManager.Instance.EffectiveWaterLevel;
                if (transform.position.y <= waterLevel + 0.15f)
                {
                    return SurfaceType.Water;
                }
            }

            // 3. Raycast descendente ignorando colisor do próprio jogador e gatilhos
            int layerMask = ~LayerMask.GetMask("Player", "Ignore Raycast", "TransparentFX");
            Vector3 rayOrigin = transform.position + Vector3.up * 0.4f;

            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 2.0f, layerMask, QueryTriggerInteraction.Ignore))
            {
                // 3a. Componente GroundSurface explícito
                var explicitSurface = hit.collider.GetComponentInParent<GroundSurface>();
                if (explicitSurface != null)
                {
                    return explicitSurface.SurfaceType;
                }

                // 3b. Identificação por Tag
                string tag = hit.collider.tag;
                if (!string.IsNullOrEmpty(tag) && tag != "Untagged")
                {
                    if (tag.Equals("Stone", System.StringComparison.OrdinalIgnoreCase) ||
                        tag.Equals("Rock", System.StringComparison.OrdinalIgnoreCase) ||
                        tag.Equals("Metal", System.StringComparison.OrdinalIgnoreCase))
                        return SurfaceType.Rock;

                    if (tag.Equals("Dirt", System.StringComparison.OrdinalIgnoreCase) ||
                        tag.Equals("Sand", System.StringComparison.OrdinalIgnoreCase) ||
                        tag.Equals("Mud", System.StringComparison.OrdinalIgnoreCase) ||
                        tag.Equals("Gravel", System.StringComparison.OrdinalIgnoreCase))
                        return SurfaceType.Dirt;

                    if (tag.Equals("Water", System.StringComparison.OrdinalIgnoreCase))
                        return SurfaceType.Water;

                    if (tag.Equals("Grass", System.StringComparison.OrdinalIgnoreCase))
                        return SurfaceType.Grass;
                }

                // 3c. Identificação por PhysicMaterial
                var pMat = hit.collider.sharedMaterial;
                if (pMat != null && !string.IsNullOrEmpty(pMat.name))
                {
                    string matName = pMat.name.ToLowerInvariant();
                    if (matName.Contains("stone") || matName.Contains("rock") || matName.Contains("metal") || matName.Contains("cliff"))
                        return SurfaceType.Rock;
                    if (matName.Contains("dirt") || matName.Contains("sand") || matName.Contains("gravel") || matName.Contains("mud"))
                        return SurfaceType.Dirt;
                    if (matName.Contains("water"))
                        return SurfaceType.Water;
                    if (matName.Contains("grass"))
                        return SurfaceType.Grass;
                }

                // 3d. Detecção por Bioma / Relevo no Terreno Low-Poly (TerrainChunk)
                var chunk = hit.collider.GetComponentInParent<TerrainChunk>();
                if (chunk != null || hit.collider.gameObject.name.StartsWith("Chunk_"))
                {
                    return EvaluateTerrainSurface(hit);
                }

                // 3e. Identificação por Nome de GameObject / Mesh / Prefab
                string goName = hit.collider.gameObject.name.ToLowerInvariant();
                if (goName.Contains("rock") || goName.Contains("stone") || goName.Contains("boulder") ||
                    goName.Contains("cliff") || goName.Contains("monolith") || goName.Contains("ore") ||
                    goName.Contains("ridge") || goName.Contains("outcrop"))
                {
                    return SurfaceType.Rock;
                }

                if (goName.Contains("dirt") || goName.Contains("sand") || goName.Contains("path") ||
                    goName.Contains("gravel") || goName.Contains("mud") || goName.Contains("earth"))
                {
                    return SurfaceType.Dirt;
                }

                if (goName.Contains("water") || goName.Contains("river") || goName.Contains("pond"))
                {
                    return SurfaceType.Water;
                }

                // 3f. Material do Renderer
                var mr = hit.collider.GetComponent<Renderer>();
                if (mr != null && mr.sharedMaterial != null)
                {
                    string mName = mr.sharedMaterial.name.ToLowerInvariant();
                    if (mName.Contains("water")) return SurfaceType.Water;
                    if (mName.Contains("rock") || mName.Contains("stone") || mName.Contains("cliff")) return SurfaceType.Rock;
                    if (mName.Contains("dirt") || mName.Contains("sand")) return SurfaceType.Dirt;
                }
            }

            return SurfaceType.Grass;
        }

        private SurfaceType EvaluateTerrainSurface(RaycastHit hit)
        {
            float slopeAngle = Vector3.Angle(hit.normal, Vector3.up);
            float hitY = hit.point.y;

            float waterLevel = 2.0f;
            float steepSlopeThreshold = 38.0f;
            float heightMultiplier = 12.0f;

            if (ChunkGridManager.Instance != null && ChunkGridManager.Instance.config != null)
            {
                var cfg = ChunkGridManager.Instance.config;
                waterLevel = cfg.waterLevel;
                steepSlopeThreshold = cfg.steepSlopeThreshold;
                heightMultiplier = cfg.heightMultiplier;
            }

            // Submerso ou tocando o lençol freático
            if (hitY <= waterLevel + 0.15f)
            {
                return SurfaceType.Water;
            }

            // Paredões íngremes e escarpas
            if (slopeAngle >= steepSlopeThreshold)
            {
                return SurfaceType.Rock;
            }

            // Picos altos e platôs montanhosos (acima de 62% da altitude)
            if (hitY >= heightMultiplier * 0.62f)
            {
                return SurfaceType.Rock;
            }

            // Faixa de praia / terra costeira logo acima da água
            if (hitY <= waterLevel + 1.8f)
            {
                return SurfaceType.Dirt;
            }

            // Planícies e vales
            return SurfaceType.Grass;
        }

        public void PlayJump()
        {
            if (jumpClip != null)
            {
                PlayClipWithPitch(jumpClip, footstepVolume * 0.95f);
            }
        }

        private void PlayClipWithPitch(AudioClip clip, float volume)
        {
            if (clip == null) return;
            audioSource.pitch = 1.0f + Random.Range(-pitchVariation, pitchVariation);
            float masterSfx = (AudioManager.Instance != null ? AudioManager.Instance.MasterVolume * AudioManager.Instance.SfxVolume : 1f);
            audioSource.PlayOneShot(clip, volume * masterSfx);
        }
    }
}
