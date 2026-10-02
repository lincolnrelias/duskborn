using System.Collections.Generic;
using UnityEngine;

namespace Duskborn.Gameplay.Player
{
    /// <summary>
    /// Player interaction system for stylized water.
    /// Generates procedural circular ripples (fully circular procedural mesh without textures to avoid square artifacts)
    /// and sends player coordinates to the shader for contact foam and surface wake ripples.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerWaterInteraction : MonoBehaviour
    {
        [Header("Water Settings")]
        [Tooltip("Minimum submersion depth to consider the player in water (meters).")]
        [SerializeField] private float minWaterDepthThreshold = 0.04f;

        [Tooltip("Depth at which water is considered deep (applies subtle physical resistance).")]
        [SerializeField] private float deepWaterThreshold = 0.45f;

        [Tooltip("Speed multiplier when walking in deep water (0.88 = 12% physical resistance).")]
        [Range(0.6f, 1f)]
        [SerializeField] private float deepWaterSpeedMultiplier = 0.88f;

        [Header("Procedural Circular Ripples")]
        [Tooltip("Distance traveled in water between ripple ring emissions (in steps).")]
        [SerializeField] private float rippleStepDistance = 0.46f;

        [Tooltip("Ripple emission interval while standing still in water.")]
        [SerializeField] private float idleRippleInterval = 1.9f;

        [Tooltip("Water ripple ring color.")]
        [SerializeField] private Color rippleColor = new Color(0.88f, 0.96f, 1.0f, 0.46f);

        [Header("Water Movement Audio")]
        [Tooltip("Continuous fluid and water displacement loop when walking / running.")]
        [SerializeField] private AudioClip wadeLoopClip;

        [Tooltip("Impact and dispersion sound when diving / entering water.")]
        [SerializeField] private AudioClip enterSplashClip;

        [Range(0f, 1f)] [SerializeField] private float wadeVolume = 0.70f;
        [Range(0f, 1f)] [SerializeField] private float enterSplashVolume = 0.80f;

        private static readonly int PlayerWaterDataId = Shader.PropertyToID("_PlayerWaterData");
        private static Mesh s_cachedRingMesh;
        private static Material s_cachedRingMaterial;

        private Vector3 _lastRipplePosition;
        private float _idleTimer;
        private float _currentWadingStrength;
        private bool _isInWater;
        private bool _isInDeepWater;
        private bool _wasInWater;
        private float _currentWaterLevel;
        private float _targetWadeVolume;

        private AudioSource _wadeAudioSource;
        private AudioSource _splashAudioSource;
        private PlayerController _playerController;

        // Procedural ripple ring pool.
        private readonly List<RippleInstance> _activeRipples = new List<RippleInstance>();
        private readonly Queue<RippleInstance> _ripplePool = new Queue<RippleInstance>();
        private Transform _rippleContainer;

        public bool IsInWater => _isInWater;
        public bool IsInDeepWater => _isInDeepWater;
        public float SpeedModifier => _isInDeepWater ? deepWaterSpeedMultiplier : 1f;
        public float CurrentWaterSurfaceY => _currentWaterLevel;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private class RippleInstance
        {
            public GameObject gameObject;
            public Transform transform;
            public MeshRenderer renderer;
            public MaterialPropertyBlock propertyBlock;
            public float elapsed;
            public float duration;
            public float startScale;
            public float endScale;
            public Color baseColor;
        }

        private CharacterController _characterController;
        private Collider _mainCollider;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _mainCollider = GetComponent<Collider>();
            _playerController = GetComponent<PlayerController>();
            EnsureResources();
            SetupAudioSources();
        }

        private void Start()
        {
            _lastRipplePosition = transform.position;
            LoadClipsIfNull();
        }

        private void OnDestroy()
        {
            if (_rippleContainer != null)
            {
                Destroy(_rippleContainer.gameObject);
            }
        }

        private void Update()
        {
            UpdateWaterState();
            UpdateWaterAudio();
            UpdateShaderData();
            UpdateActiveRipples();
        }

        private void SetupAudioSources()
        {
            bool isLocalOwner = _playerController == null || _playerController.IsOwner;

            _wadeAudioSource = gameObject.AddComponent<AudioSource>();
            _wadeAudioSource.playOnAwake = false;
            _wadeAudioSource.loop = true;
            _wadeAudioSource.spatialBlend = isLocalOwner ? 0.15f : 1.0f;
            _wadeAudioSource.minDistance = isLocalOwner ? 4.0f : 2.0f;
            _wadeAudioSource.maxDistance = 25.0f;
            _wadeAudioSource.volume = 0f;

            _splashAudioSource = gameObject.AddComponent<AudioSource>();
            _splashAudioSource.playOnAwake = false;
            _splashAudioSource.loop = false;
            _splashAudioSource.spatialBlend = isLocalOwner ? 0.20f : 1.0f;
            _splashAudioSource.minDistance = isLocalOwner ? 3.0f : 2.0f;
            _splashAudioSource.maxDistance = 25.0f;
        }

        public void LoadClipsIfNull()
        {
            var db = Duskborn.Audio.AudioDatabase.Instance?.Player;
            if (db != null)
            {
                if (wadeLoopClip == null) wadeLoopClip = db.waterWadeLoop;
                if (enterSplashClip == null) enterSplashClip = db.waterEnterSplashClip;
                wadeVolume = db.waterWadeVolume;
                enterSplashVolume = db.waterSplashVolume;
            }
            else
            {
                if (wadeLoopClip == null) wadeLoopClip = Resources.Load<AudioClip>("SFX/water_wade_loop");
                if (enterSplashClip == null) enterSplashClip = Resources.Load<AudioClip>("SFX/water_splash_enter");
            }

            if (wadeLoopClip != null && _wadeAudioSource != null)
            {
                _wadeAudioSource.clip = wadeLoopClip;
            }
        }

        private void UpdateWaterAudio()
        {
            if (_wadeAudioSource == null) return;

            // Water entry transition (splash).
            if (!_wasInWater && _isInWater)
            {
                PlayEnterSplash();
            }
            _wasInWater = _isInWater;

            if (!_isInWater)
            {
                _targetWadeVolume = 0f;
            }
            else
            {
                // Player's actual horizontal speed.
                float speed = 0f;
                if (_characterController != null)
                {
                    speed = new Vector2(_characterController.velocity.x, _characterController.velocity.z).magnitude;
                }

                if (_playerController != null && _playerController.IsMoving && speed < 0.5f)
                {
                    speed = _playerController.IsSprinting ? 6.5f : 4.5f;
                }

                if (speed > 0.35f)
                {
                    float normalizedSpeed = Mathf.Clamp01(speed / 6.0f);
                    float deepBonus = _isInDeepWater ? 1.25f : 1.0f;
                    float masterSfx = Duskborn.Audio.AudioManager.Instance != null
                        ? Duskborn.Audio.AudioManager.Instance.MasterVolume * Duskborn.Audio.AudioManager.Instance.SfxVolume
                        : 1f;

                    _targetWadeVolume = Mathf.Clamp01(wadeVolume * Mathf.Lerp(0.35f, 1.0f, normalizedSpeed) * deepBonus) * masterSfx;

                    // Fuller pitch in deep water, faster pitch when running.
                    float basePitch = _isInDeepWater ? 0.90f : 1.0f;
                    _wadeAudioSource.pitch = basePitch * Mathf.Lerp(0.95f, 1.18f, normalizedSpeed);

                    if (!_wadeAudioSource.isPlaying && wadeLoopClip != null)
                    {
                        if (_wadeAudioSource.clip == null) _wadeAudioSource.clip = wadeLoopClip;
                        _wadeAudioSource.Play();
                    }
                }
                else
                {
                    _targetWadeVolume = 0f;
                }
            }

            // Smooth volume interpolation for organic fluid displacement.
            _wadeAudioSource.volume = Mathf.MoveTowards(_wadeAudioSource.volume, _targetWadeVolume, Time.deltaTime * 3.5f);

            if (_wadeAudioSource.volume <= 0.001f && _wadeAudioSource.isPlaying)
            {
                _wadeAudioSource.Stop();
            }
        }

        private void PlayEnterSplash()
        {
            if (enterSplashClip == null || _splashAudioSource == null) return;

            float masterSfx = Duskborn.Audio.AudioManager.Instance != null
                ? Duskborn.Audio.AudioManager.Instance.MasterVolume * Duskborn.Audio.AudioManager.Instance.SfxVolume
                : 1f;

            _splashAudioSource.pitch = Random.Range(0.95f, 1.05f);
            _splashAudioSource.PlayOneShot(enterSplashClip, enterSplashVolume * masterSfx);
        }

        public void NotifyMovement(bool isMoving, Vector3 currentVelocity)
        {
            if (!_isInWater) return;

            if (isMoving)
            {
                _idleTimer = 0f;
                float distMoved = Vector3.Distance(new Vector3(transform.position.x, 0f, transform.position.z),
                                                   new Vector3(_lastRipplePosition.x, 0f, _lastRipplePosition.z));

                if (distMoved >= rippleStepDistance)
                {
                    // Balanced footstep ripple (0.25m -> 1.35m in 1.05s).
                    SpawnProceduralRipple(0.25f, 1.35f, 1.05f);
                    _lastRipplePosition = transform.position;
                }
            }
            else
            {
                _idleTimer += Time.deltaTime;
                if (_idleTimer >= idleRippleInterval)
                {
                    // Gentle idle pulse.
                    SpawnProceduralRipple(0.20f, 0.95f, 1.4f);
                    _idleTimer = 0f;
                }
            }
        }

        private float GetPlayerFootY()
        {
            if (_characterController != null)
            {
                return transform.position.y + _characterController.center.y - (_characterController.height * 0.5f) + _characterController.skinWidth;
            }
            if (_mainCollider != null)
            {
                return _mainCollider.bounds.min.y;
            }
            return transform.position.y;
        }

        private void UpdateWaterState()
        {
            if (ChunkGridManager.Instance != null && ChunkGridManager.Instance.generateWaterPlane)
            {
                _currentWaterLevel = ChunkGridManager.Instance.EffectiveWaterLevel;
            }
            else
            {
                _currentWaterLevel = -999f;
            }

            float playerFootY = GetPlayerFootY();
            float submergence = _currentWaterLevel - playerFootY;

            _isInWater = submergence > minWaterDepthThreshold;
            _isInDeepWater = submergence > deepWaterThreshold;

            float targetWading = _isInWater ? 1f : 0f;
            _currentWadingStrength = Mathf.MoveTowards(_currentWadingStrength, targetWading, Time.deltaTime * 5.0f);
        }

        private void UpdateShaderData()
        {
            Vector3 pos = transform.position;
            Shader.SetGlobalVector(PlayerWaterDataId, new Vector4(pos.x, _currentWaterLevel, pos.z, _currentWadingStrength));
        }

        private void SpawnProceduralRipple(float startScale, float endScale, float duration)
        {
            RippleInstance ripple = GetOrCreateRippleInstance();
            if (ripple == null) return;

            ripple.transform.position = new Vector3(transform.position.x, _currentWaterLevel + 0.005f, transform.position.z);
            ripple.transform.localScale = new Vector3(startScale, 1f, startScale);
            ripple.elapsed = 0f;
            ripple.duration = duration;
            ripple.startScale = startScale;
            ripple.endScale = endScale;
            ripple.baseColor = rippleColor;
            ripple.propertyBlock.SetColor(BaseColorId, rippleColor);
            ripple.renderer.SetPropertyBlock(ripple.propertyBlock);
            ripple.gameObject.SetActive(true);

            _activeRipples.Add(ripple);
        }

        private void UpdateActiveRipples()
        {
            for (int i = _activeRipples.Count - 1; i >= 0; i--)
            {
                RippleInstance r = _activeRipples[i];
                r.elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(r.elapsed / r.duration);

                // Smooth circular expansion.
                float currentScale = Mathf.Lerp(r.startScale, r.endScale, Mathf.SmoothStep(0f, 1f, t));
                r.transform.localScale = new Vector3(currentScale, 1f, currentScale);

                // Gradual opacity fade-out with smooth decay.
                float alpha = r.baseColor.a * (1f - t) * (1f - t);
                r.propertyBlock.SetColor(BaseColorId, new Color(r.baseColor.r, r.baseColor.g, r.baseColor.b, alpha));
                r.renderer.SetPropertyBlock(r.propertyBlock);

                if (t >= 1f)
                {
                    r.gameObject.SetActive(false);
                    _activeRipples.RemoveAt(i);
                    _ripplePool.Enqueue(r);
                }
            }
        }

        private RippleInstance GetOrCreateRippleInstance()
        {
            while (_ripplePool.Count > 0)
            {
                RippleInstance pooled = _ripplePool.Dequeue();
                if (pooled != null && pooled.gameObject != null)
                {
                    return pooled;
                }
            }

            // Create a new procedural ring instance.
            if (_rippleContainer == null)
            {
                GameObject container = new GameObject("PlayerWaterRipples_Container");
                _rippleContainer = container.transform;
            }

            GameObject rippleObj = new GameObject($"RippleRing_{_activeRipples.Count + _ripplePool.Count}");
            rippleObj.transform.parent = _rippleContainer;

            MeshFilter mf = rippleObj.AddComponent<MeshFilter>();
            mf.sharedMesh = GetOrCreateRingMesh();

            MeshRenderer mr = rippleObj.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            mr.sharedMaterial = s_cachedRingMaterial;

            RippleInstance instance = new RippleInstance
            {
                gameObject = rippleObj,
                transform = rippleObj.transform,
                renderer = mr,
                propertyBlock = new MaterialPropertyBlock()
            };

            return instance;
        }

        private void EnsureResources()
        {
            if (s_cachedRingMaterial == null)
            {
                Shader rippleShader = Shader.Find("Duskborn/WaterRippleRing");
                if (rippleShader == null) rippleShader = Shader.Find("Universal Render Pipeline/Unlit");
                if (rippleShader != null)
                {
                    s_cachedRingMaterial = new Material(rippleShader);
                    s_cachedRingMaterial.name = "M_WaterRippleRing_Shared";
                }
            }

            GetOrCreateRingMesh();
        }

        private static Mesh GetOrCreateRingMesh()
        {
            if (s_cachedRingMesh != null) return s_cachedRingMesh;

            const int segments = 40;
            const int rings = 4;
            const int vertCount = segments * rings;
            Vector3[] verts = new Vector3[vertCount];
            Color[] colors = new Color[vertCount];
            int[] tris = new int[segments * (rings - 1) * 6];

            // Balanced profile with a crisp crest and organic decay.
            float[] radii = new float[] { 0.52f, 0.74f, 0.88f, 1.00f };
            float[] alphas = new float[] { 0f, 0.85f, 0.45f, 0f };

            for (int r = 0; r < rings; r++)
            {
                float radius = radii[r];
                float alpha = alphas[r];

                for (int s = 0; s < segments; s++)
                {
                    float angle = (s / (float)segments) * Mathf.PI * 2f;
                    int idx = r * segments + s;
                    verts[idx] = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                    colors[idx] = new Color(1f, 1f, 1f, alpha);
                }
            }

            int t = 0;
            for (int r = 0; r < rings - 1; r++)
            {
                int ringCurrent = r * segments;
                int ringNext = (r + 1) * segments;

                for (int s = 0; s < segments; s++)
                {
                    int nextS = (s + 1) % segments;

                    int i0 = ringCurrent + s;
                    int i1 = ringNext + s;
                    int i2 = ringNext + nextS;
                    int i3 = ringCurrent + nextS;

                    tris[t++] = i0; tris[t++] = i3; tris[t++] = i2;
                    tris[t++] = i0; tris[t++] = i2; tris[t++] = i1;
                }
            }

            s_cachedRingMesh = new Mesh();
            s_cachedRingMesh.name = "Mesh_ProceduralWaterRippleRing_HighRes";
            s_cachedRingMesh.vertices = verts;
            s_cachedRingMesh.colors = colors;
            s_cachedRingMesh.triangles = tris;
            s_cachedRingMesh.RecalculateNormals();
            s_cachedRingMesh.RecalculateBounds();

            return s_cachedRingMesh;
        }
    }
}
