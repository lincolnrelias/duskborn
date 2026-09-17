using UnityEngine;
using UnityEngine.InputSystem;
using Duskborn.Effects;

namespace Duskborn.Gameplay.Player
{
    /// <summary>
    /// Sistema profissional de câmera em terceira pessoa para o Duskborn.
    /// Suporta órbita esférica completa (Yaw/Pitch), prevenção ativa de oclusão via SphereCast,
    /// enquadramento no ombro (shoulder framing), amortecimento de terreno, FOV dinâmico e integração com CameraShake.
    /// Executado estritamente na instância do jogador local (IsOwner).
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerCameraController : MonoBehaviour
    {
        public static PlayerCameraController LocalInstance { get; private set; }

        [Header("Alvo e Posicionamento")]
        [Tooltip("Ponto pivô relativo ao jogador (altura do peito/olhos).")]
        [SerializeField] private Vector3 pivotOffset = new Vector3(0f, 1.6f, 0f);

        [Tooltip("Deslocamento horizontal para a direita (visão sobre o ombro).")]
        [SerializeField] private float shoulderOffset = 0.35f;

        [Tooltip("Tempo de amortecimento da posição do pivô para suavizar degraus e desníveis do terreno low-poly.")]
        [SerializeField] private float pivotDampTime = 0.05f;

        [Header("Órbita e Controle de Rotação")]
        [Tooltip("Sensibilidade horizontal do mouse.")]
        [SerializeField] private float sensitivityX = 0.15f;

        [Tooltip("Sensibilidade vertical do mouse.")]
        [SerializeField] private float sensitivityY = 0.15f;

        [Tooltip("Limite inferior do ângulo vertical (em graus).")]
        [SerializeField] private float minPitch = -20f;

        [Tooltip("Limite superior do ângulo vertical (em graus).")]
        [SerializeField] private float maxPitch = 70f;

        [Tooltip("Inverter eixo vertical da câmera.")]
        [SerializeField] private bool invertPitch = false;

        [Tooltip("Tempo de suavização da rotação da câmera (evita solavancos no mouse).")]
        [SerializeField] private float rotationDampTime = 0.02f;

        [Header("Distância e Zoom")]
        [Tooltip("Distância padrão de visualização da câmera.")]
        [SerializeField] private float defaultDistance = 6.5f;

        [Tooltip("Distância mínima permitida ao aproximar o zoom.")]
        [SerializeField] private float minDistance = 2.0f;

        [Tooltip("Distância máxima permitida ao afastar o zoom.")]
        [SerializeField] private float maxDistance = 11.0f;

        [Tooltip("Passo de alteração de distância por scroll de zoom.")]
        [SerializeField] private float zoomStep = 1.0f;

        [Tooltip("Velocidade de transição suave do zoom.")]
        [SerializeField] private float zoomDampTime = 0.1f;

        [Header("Colisão com Cenário e Oclusão")]
        [Tooltip("Camadas de física que devem bloquear a câmera (terreno, rochas, construções).")]
        [SerializeField] private LayerMask collisionLayers;

        [Tooltip("Raio do SphereCast para evitar que o plano frontal da câmera atravesse geometrias.")]
        [SerializeField] private float collisionRadius = 0.22f;

        [Tooltip("Distância de amortecimento em relação à superfície colidida.")]
        [SerializeField] private float collisionPadding = 0.2f;

        [Tooltip("Velocidade de retorno suave ao se afastar de um obstáculo.")]
        [SerializeField] private float collisionRecoverySpeed = 6.0f;

        [Header("Sensação de Jogo (Game Feel)")]
        [Tooltip("Campo de visão (FOV) base.")]
        [SerializeField] private float defaultFov = 60f;

        [Tooltip("Acréscimo de FOV durante esquiva (dodge roll) ou corridas para sensação de velocidade.")]
        [SerializeField] private float dynamicFovKick = 4f;

        [Tooltip("Velocidade de interpolação do campo de visão.")]
        [SerializeField] private float fovTransitionSpeed = 8f;

        [Header("Controle do Cursor")]
        [Tooltip("Travar o cursor automaticamente durante a gameplay de combate.")]
        [SerializeField] private bool autoLockCursor = true;

        // Referências do jogador
        private Camera           _mainCam;
        private PlayerController _controller;
        private PlayerDodge      _dodge;

        // Estado de rotação e mira
        private float   _targetYaw;
        private float   _targetPitch = 20f;
        private float   _currentYaw;
        private float   _currentPitch = 20f;
        private float   _yawVelocity;
        private float   _pitchVelocity;
        private Vector2 _lookInput;

        // Estado de posicionamento e distância
        private Vector3 _smoothedPivotPos;
        private Vector3 _pivotVelocity;
        private float   _targetDistance;
        private float   _currentDistance;
        private float   _distanceVelocity;

        // Travas de controle e estado
        private bool _isInitialized;
        private bool _isRotationLocked;
        private bool _isCursorLocked;

        private readonly RaycastHit[] _sphereCastHits = new RaycastHit[16];

        // Propriedades públicas para orientação e movimentação
        public float   CurrentYaw    => _currentYaw;
        public float   CurrentPitch  => _currentPitch;
        public Camera  MainCamera    => _mainCam;
        public Vector3 CameraForward => _mainCam != null ? Vector3.ProjectOnPlane(_mainCam.transform.forward, Vector3.up).normalized : transform.forward;
        public Vector3 CameraRight   => _mainCam != null ? Vector3.ProjectOnPlane(_mainCam.transform.right, Vector3.up).normalized : transform.right;

        private void Awake()
        {
            _controller = GetComponent<PlayerController>();
            _dodge      = GetComponent<PlayerDodge>();
            _mainCam    = Camera.main;

            _targetYaw        = transform.eulerAngles.y;
            _currentYaw       = _targetYaw;
            _targetDistance   = defaultDistance;
            _currentDistance  = defaultDistance;
            _smoothedPivotPos = transform.position + pivotOffset;

            // Se nenhuma camada de colisão foi atribuída no inspetor, usa Default e ResourceNode
            if (collisionLayers.value == 0)
            {
                collisionLayers = LayerMask.GetMask("Default", "ResourceNode");
                if (collisionLayers.value == 0)
                    collisionLayers = 1 << 0; // Layer 0 (Default)
            }
        }

        private void Start()
        {
            // Se o controller já tiver posse confirmada no Start, inicializa
            if (_controller != null && _controller.IsOwner)
            {
                InitializeForOwner();
            }
            // Não desativa com enabled = false para permitir que LateUpdate detecte o IsOwner quando chegar a mensagem de rede
        }

        /// <summary>
        /// Inicializa a câmera para a instância do jogador proprietário local.
        /// </summary>
        public void InitializeForOwner()
        {
            if (_isInitialized) return;
            _isInitialized = true;

            LocalInstance = this;

            if (_mainCam == null)
                _mainCam = Camera.main;

            if (_mainCam == null)
            {
                var camGo = GameObject.FindWithTag("MainCamera");
                if (camGo != null) _mainCam = camGo.GetComponent<Camera>();
            }

            if (_mainCam == null)
            {
                _mainCam = FindAnyObjectByType<Camera>();
            }

            _targetYaw        = transform.eulerAngles.y;
            _currentYaw       = _targetYaw;
            _targetPitch      = 20f;
            _currentPitch     = 20f;
            _targetDistance   = defaultDistance;
            _currentDistance  = defaultDistance;
            _smoothedPivotPos = transform.position + pivotOffset;

            SnapCameraToTarget();

            if (autoLockCursor)
                SetCursorLocked(true);
        }

        /// <summary>
        /// Posiciona instantaneamente a câmera atrás do jogador (evita interpolações longas ao spawnar).
        /// </summary>
        public void SnapCameraToTarget()
        {
            _smoothedPivotPos = transform.position + pivotOffset;
            _currentYaw       = _targetYaw;
            _currentPitch     = _targetPitch;
            _currentDistance  = _targetDistance;

            if (_mainCam != null)
            {
                Quaternion orbitRot    = Quaternion.Euler(_currentPitch, _currentYaw, 0f);
                Vector3 shoulderVector = orbitRot * (Vector3.right * shoulderOffset);
                Vector3 rayOrigin      = _smoothedPivotPos + shoulderVector;
                Vector3 backDir        = -(orbitRot * Vector3.forward);

                _mainCam.transform.position = rayOrigin + backDir * _currentDistance;
                _mainCam.transform.rotation = orbitRot;
            }
        }

        private void OnDestroy()
        {
            if (LocalInstance == this)
            {
                LocalInstance = null;
                SetCursorLocked(false);
            }
        }

        /// <summary>
        /// Captura a entrada de rotação (Look) disparada pelo PlayerInput da Unity.
        /// </summary>
        public void OnLook(InputValue value)
        {
            if (_controller != null && !_controller.IsOwner) return;
            if (_isRotationLocked) return;
            _lookInput = value.Get<Vector2>();
        }

        private void Update()
        {
            if (_controller == null)
                _controller = GetComponent<PlayerController>();

            if (_controller == null || !_controller.IsOwner) return;

            if (!_isInitialized)
                InitializeForOwner();

            HandleInput();
            HandleZoomInput();
            UpdateCursorLock();
        }

        private void LateUpdate()
        {
            if (_controller == null)
                _controller = GetComponent<PlayerController>();

            if (_controller == null || !_controller.IsOwner) return;

            if (!_isInitialized)
                InitializeForOwner();

            if (_mainCam == null)
            {
                _mainCam = Camera.main;
                if (_mainCam == null)
                    _mainCam = FindAnyObjectByType<Camera>();
                if (_mainCam == null) return;
            }

            // Se o jogador foi teletransportado ou spawnou longe do pivô anterior, reposiciona instantaneamente
            Vector3 targetPivot = transform.position + pivotOffset;
            if (Vector3.Distance(_smoothedPivotPos, targetPivot) > 10f)
            {
                SnapCameraToTarget();
            }

            UpdatePivotPosition();
            UpdateOrbitAngles();
            ResolveCollisionAndPosition();
            UpdateDynamicFov();
        }

        // ── Entrada e Controles ───────────────────────────────────────────────

        private void HandleInput()
        {
            if (_isRotationLocked)
            {
                _lookInput = Vector2.zero;
                return;
            }

            // Se o cursor estiver destravado (ex: mouse livre para UI), não rotaciona a câmera
            if (!_isCursorLocked)
            {
                _lookInput = Vector2.zero;
                return;
            }

            _targetYaw += _lookInput.x * sensitivityX;

            float pitchDelta = _lookInput.y * sensitivityY * (invertPitch ? 1f : -1f);
            _targetPitch = Mathf.Clamp(_targetPitch + pitchDelta, minPitch, maxPitch);

            _lookInput = Vector2.zero;
        }

        private void HandleZoomInput()
        {
            if (_isRotationLocked) return;

            // Suporte a zoom segurando a tecla Alt ou via teclas específicas ([ e ])
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            float scroll = 0f;

            if (kb != null)
            {
                if (kb.leftBracketKey.wasPressedThisFrame) scroll = 1f;
                else if (kb.rightBracketKey.wasPressedThisFrame) scroll = -1f;
                else if (kb.altKey.isPressed && mouse != null)
                    scroll = mouse.scroll.ReadValue().y;
            }

            if (Mathf.Abs(scroll) > 0.01f)
            {
                _targetDistance = Mathf.Clamp(
                    _targetDistance - Mathf.Sign(scroll) * zoomStep,
                    minDistance,
                    maxDistance);
            }
        }

        private void UpdateCursorLock()
        {
            // Atalho de conveniência: pressionar Alt Esquerdo alterna temporariamente o cursor durante testes
            var kb = Keyboard.current;
            if (kb != null && kb.leftAltKey.wasPressedThisFrame && !_isRotationLocked)
            {
                SetCursorLocked(!_isCursorLocked);
            }
        }

        // ── Atualizações de Câmera ───────────────────────────────────────────

        private void UpdatePivotPosition()
        {
            Vector3 targetPivot = transform.position + pivotOffset;

            // Interpolação amortecida para suavizar trepidações verticais do relevo procedural
            _smoothedPivotPos = Vector3.SmoothDamp(
                _smoothedPivotPos,
                targetPivot,
                ref _pivotVelocity,
                pivotDampTime);
        }

        private void UpdateOrbitAngles()
        {
            _currentYaw = Mathf.SmoothDampAngle(
                _currentYaw,
                _targetYaw,
                ref _yawVelocity,
                rotationDampTime);

            _currentPitch = Mathf.SmoothDamp(
                _currentPitch,
                _targetPitch,
                ref _pitchVelocity,
                rotationDampTime);
        }

        private void ResolveCollisionAndPosition()
        {
            Quaternion orbitRot = Quaternion.Euler(_currentPitch, _currentYaw, 0f);

            // Deslocamento de ombro calculado a partir da rotação atual da câmera
            Vector3 shoulderVector = orbitRot * (Vector3.right * shoulderOffset);
            Vector3 rayOrigin      = _smoothedPivotPos + shoulderVector;

            // Vetor para a posição desejada sem colisões
            Vector3 backDir = -(orbitRot * Vector3.forward);

            // Distância desejada com suavização de zoom
            float desiredDistance = Mathf.SmoothDamp(
                _currentDistance,
                _targetDistance,
                ref _distanceVelocity,
                zoomDampTime);

            // Detecção esférica de obstrução (SphereCastNonAlloc) ignorando o próprio jogador
            int hitCount = Physics.SphereCastNonAlloc(
                rayOrigin,
                collisionRadius,
                backDir,
                _sphereCastHits,
                desiredDistance,
                collisionLayers,
                QueryTriggerInteraction.Ignore);

            float closestHitDist = desiredDistance;
            bool hasObstacle = false;

            for (int i = 0; i < hitCount; i++)
            {
                var h = _sphereCastHits[i];
                if (h.collider == null || h.collider.isTrigger) continue;
                // Ignora o próprio jogador e qualquer objeto sob sua hierarquia
                if (h.collider.transform.root == transform.root) continue;

                if (h.distance < closestHitDist)
                {
                    closestHitDist = h.distance;
                    hasObstacle = true;
                }
            }

            float resolvedDistance = desiredDistance;
            if (hasObstacle)
            {
                // Empurrão imediato para evitar que a câmera entre na geometria
                float safeHitDist = Mathf.Max(closestHitDist - collisionPadding, minDistance);
                resolvedDistance  = Mathf.Min(safeHitDist, desiredDistance);
                _currentDistance  = resolvedDistance;
            }
            else
            {
                // Retorno suave ao afastar-se de obstáculos
                _currentDistance = Mathf.Lerp(
                    _currentDistance,
                    desiredDistance,
                    collisionRecoverySpeed * Time.deltaTime);
                resolvedDistance = _currentDistance;
            }

            // Posição final da câmera + tremor desvinculado (CameraShake)
            Vector3 finalPosition = rayOrigin + backDir * resolvedDistance;
            _mainCam.transform.position = finalPosition + CameraShake.Offset;
            _mainCam.transform.rotation = orbitRot;
        }

        private void UpdateDynamicFov()
        {
            if (_mainCam == null) return;

            bool isDodgeRolling = _dodge != null && _dodge.IsRolling;
            bool isMovingFast   = _controller != null && _controller.IsMoving;

            float targetFov = defaultFov;
            if (isDodgeRolling)
                targetFov += dynamicFovKick;
            else if (isMovingFast)
                targetFov += dynamicFovKick * 0.5f;

            _mainCam.fieldOfView = Mathf.Lerp(
                _mainCam.fieldOfView,
                targetFov,
                fovTransitionSpeed * Time.deltaTime);
        }

        // ── Gerenciamento Público de Estado ───────────────────────────────────

        /// <summary>
        /// Bloqueia ou libera o cursor do mouse e define sua visibilidade.
        /// </summary>
        public void SetCursorLocked(bool locked)
        {
            _isCursorLocked  = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible   = !locked;
        }

        /// <summary>
        /// Impede que a rotação da câmera receba comandos do mouse (usado ao abrir inventários/menus).
        /// </summary>
        public void SetRotationLocked(bool locked)
        {
            _isRotationLocked = locked;
            if (locked)
            {
                _lookInput = Vector2.zero;
                SetCursorLocked(false);
            }
            else
            {
                if (autoLockCursor)
                    SetCursorLocked(true);
            }
        }
    }
}
