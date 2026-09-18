using FishNet.Object;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Duskborn.Gameplay.Player
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerStats))]
    public class PlayerController : NetworkBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float gravityMultiplier = 2f;
        [SerializeField] private float rotationSpeed     = 720f;
        [SerializeField] private float accelTime         = 0.15f;
        [SerializeField] private float decelTime         = 0.08f;
        [SerializeField] private float runMultiplier     = 1.0f;
        [SerializeField] private float sprintMultiplier  = 1.5f;

        [Header("Jump")]
        [SerializeField] private float jumpHeight        = 1.6f;
        [SerializeField] private float coyoteTime        = 0.15f;
        [SerializeField] private float jumpBufferTime    = 0.15f;

        [Header("Animation")]
        [SerializeField] private Animator _animator;

        private static readonly int HashVelocityX  = Animator.StringToHash("VelocityX");
        private static readonly int HashVelocityY  = Animator.StringToHash("VelocityY");
        private static readonly int HashJump       = Animator.StringToHash("Jump");
        private static readonly int HashIsGrounded = Animator.StringToHash("IsGrounded");

        private CharacterController             _cc;
        private PlayerStats                     _stats;
        private PlayerCameraController          _camController;
        private PlayerWaterInteraction          _waterInteraction;
        private Duskborn.Audio.FootstepAudio    _footstepAudio;

        private Vector2 _moveInput;
        private Vector2 _smoothedInput;
        private Vector2 _inputSmoothVelocity;
        private Vector3 _velocity;
        private bool    _inputEnabled    = true;
        private bool    _rotationEnabled = true;
        private bool    _isSprinting;
        private int     _lastToggleFrame = -1;

        private float   _lastGroundedTimer;
        private float   _jumpBufferTimer;
        private bool    _hasJumpedInAir;

        public bool  IsMoving    => _moveInput.sqrMagnitude > 0.01f;
        public float CameraYaw   => _camController != null ? _camController.CurrentYaw : transform.eulerAngles.y;
        public bool  IsSprinting => _isSprinting;

        private void Awake()
        {
            _isSprinting   = false;
            _cc            = GetComponent<CharacterController>();
            _stats         = GetComponent<PlayerStats>();
            _camController = GetComponent<PlayerCameraController>();
            if (_camController == null)
            {
                _camController = gameObject.AddComponent<PlayerCameraController>();
            }

            _waterInteraction = GetComponent<PlayerWaterInteraction>();
            if (_waterInteraction == null)
            {
                _waterInteraction = gameObject.AddComponent<PlayerWaterInteraction>();
            }

            _footstepAudio = GetComponent<Duskborn.Audio.FootstepAudio>();
            if (_footstepAudio == null)
                _footstepAudio = gameObject.AddComponent<Duskborn.Audio.FootstepAudio>();

            if (GetComponent<Duskborn.Audio.PlayerAudioFeedback>() == null)
                gameObject.AddComponent<Duskborn.Audio.PlayerAudioFeedback>();

            if (_animator == null)
                _animator = GetComponentInChildren<Animator>();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (IsOwner)
            {
                if (_camController == null)
                    _camController = GetComponent<PlayerCameraController>();
                _camController?.InitializeForOwner();
            }
        }

        public override void OnOwnershipClient(FishNet.Connection.NetworkConnection prevOwner)
        {
            base.OnOwnershipClient(prevOwner);
            if (IsOwner)
            {
                if (_camController == null)
                    _camController = GetComponent<PlayerCameraController>();
                _camController?.InitializeForOwner();
            }
        }

        public void OnMove(InputValue value)
        {
            if (!IsOwner) return;
            _moveInput = value.Get<Vector2>();
        }

        public void OnSprint(InputValue value)
        {
            if (!IsOwner) return;
            if (value.isPressed)
                ToggleSprint();
        }

        public void ToggleSprint()
        {
            if (Time.frameCount == _lastToggleFrame) return;
            _lastToggleFrame = Time.frameCount;
            _isSprinting = !_isSprinting;
        }

        public void OnJump(InputValue value)
        {
            if (!IsOwner) return;
            if (value.isPressed)
                QueueJump();
        }

        public void QueueJump()
        {
            if (!_inputEnabled || !_stats.IsAlive) return;

            var dodge = GetComponent<PlayerDodge>();
            if (dodge != null && dodge.IsRolling) return;

            _jumpBufferTimer = jumpBufferTime;
        }

        private void Update()
        {
            if (_waterInteraction != null)
            {
                bool moving = IsOwner ? IsMoving : (_cc != null && _cc.velocity.sqrMagnitude > 0.05f);
                _waterInteraction.NotifyMovement(moving, _cc != null ? _cc.velocity : Vector3.zero);
            }

            if (!IsOwner) return;
            if (!_inputEnabled || !_stats.IsAlive) return;

            if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift))
                ToggleSprint();

            if (Input.GetKeyDown(KeyCode.Space))
                QueueJump();

            HandleJumpTimers();
            SmoothInput();
            HandleMovement();
            HandleGravity();
            UpdateAnimator();
        }

        // -------------------------------------------------------------------------

        private void HandleJumpTimers()
        {
            if (_cc.isGrounded)
            {
                _lastGroundedTimer = coyoteTime;
                if (_velocity.y <= 0f)
                    _hasJumpedInAir = false;
            }
            else
            {
                _lastGroundedTimer -= Time.deltaTime;
            }

            if (_jumpBufferTimer > 0f)
            {
                _jumpBufferTimer -= Time.deltaTime;
                if (_lastGroundedTimer > 0f && !_hasJumpedInAir)
                {
                    ExecuteJump();
                }
            }
        }

        private void ExecuteJump()
        {
            _jumpBufferTimer   = 0f;
            _lastGroundedTimer = 0f;
            _hasJumpedInAir    = true;

            float effectiveGravity = Mathf.Abs(Physics.gravity.y * gravityMultiplier);
            _velocity.y = Mathf.Sqrt(2f * jumpHeight * effectiveGravity);

            if (_animator != null)
            {
                _animator.SetBool(HashIsGrounded, false);
                _animator.ResetTrigger(HashJump);
                _animator.SetTrigger(HashJump);
                _animator.Play("BasicMotions@Jump01", 0, 0f);
            }

            if (_footstepAudio != null)
                _footstepAudio.PlayJump();
        }

        private void SmoothInput()
        {
            float speedFactor = _isSprinting ? sprintMultiplier : runMultiplier;
            Vector2 targetInput = _moveInput * speedFactor;

            float smoothTime = _moveInput.sqrMagnitude > 0.01f ? accelTime : decelTime;
            _smoothedInput = Vector2.SmoothDamp(
                _smoothedInput, targetInput, ref _inputSmoothVelocity, smoothTime);
        }

        private void HandleMovement()
        {
            float targetYaw = CameraYaw;

            // O personagem acompanha o ângulo horizontal da câmera ao girar
            if (_rotationEnabled)
            {
                Quaternion targetRot = Quaternion.Euler(0f, targetYaw, 0f);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
            }

            if (_smoothedInput.sqrMagnitude < 0.001f) return;

            Vector3 camForward = _camController != null ? _camController.CameraForward : transform.forward;
            Vector3 camRight   = _camController != null ? _camController.CameraRight   : transform.right;

            // Vetor de movimento relativo à orientação da câmera (já escalonado pelo modo de caminhada/corrida)
            Vector3 moveDir = camForward * _smoothedInput.y + camRight * _smoothedInput.x;
            float waterMod = _waterInteraction != null ? _waterInteraction.SpeedModifier : 1f;
            _cc.Move(moveDir * (_stats.MoveSpeed * waterMod * Time.deltaTime));
        }

        private void HandleGravity()
        {
            if (_cc.isGrounded && _velocity.y < 0f)
                _velocity.y = -2f;
            else
                _velocity.y += Physics.gravity.y * gravityMultiplier * Time.deltaTime;

            _cc.Move(_velocity * Time.deltaTime);
        }

        private void UpdateAnimator()
        {
            if (_animator == null) return;

            // O personagem alinha-se à câmera, então a entrada suavizada representa velocidade no espaço local:
            // Y = frente/trás, X = strafe lateral.
            _animator.SetFloat(HashVelocityX, _smoothedInput.x);
            _animator.SetFloat(HashVelocityY, _smoothedInput.y);

            bool groundedForAnim = _cc.isGrounded && !_hasJumpedInAir;
            _animator.SetBool(HashIsGrounded, groundedForAnim);
        }

        public void SetInputEnabled(bool enabled) => _inputEnabled = enabled;

        // Mantém o corpo voltado para a direção atual (usado pelo PlayerDodge para manter o rolamento alinhado).
        public void SetRotationEnabled(bool enabled) => _rotationEnabled = enabled;

        // Direção de movimento no mundo relativa à câmera
        public Vector3 GetMoveDirectionWorld()
        {
            if (_moveInput.sqrMagnitude < 0.01f)
                return transform.forward;

            Vector3 f = _camController != null ? _camController.CameraForward : transform.forward;
            Vector3 r = _camController != null ? _camController.CameraRight   : transform.right;
            return (f * _moveInput.y + r * _moveInput.x).normalized;
        }
    }
}
