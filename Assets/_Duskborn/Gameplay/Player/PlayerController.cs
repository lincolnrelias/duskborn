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

        [Header("Animation")]
        [SerializeField] private Animator _animator;

        private static readonly int HashVelocityX = Animator.StringToHash("VelocityX");
        private static readonly int HashVelocityY = Animator.StringToHash("VelocityY");

        private CharacterController    _cc;
        private PlayerStats            _stats;
        private PlayerCameraController _camController;
        private PlayerWaterInteraction _waterInteraction;

        private Vector2 _moveInput;
        private Vector2 _smoothedInput;
        private Vector2 _inputSmoothVelocity;
        private Vector3 _velocity;
        private bool    _inputEnabled    = true;
        private bool    _rotationEnabled = true;

        public bool  IsMoving  => _moveInput.sqrMagnitude > 0.01f;
        public float CameraYaw => _camController != null ? _camController.CurrentYaw : transform.eulerAngles.y;

        private void Awake()
        {
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

            if (GetComponent<Duskborn.Audio.FootstepAudio>() == null)
                gameObject.AddComponent<Duskborn.Audio.FootstepAudio>();
            if (GetComponent<Duskborn.Audio.PlayerAudioFeedback>() == null)
                gameObject.AddComponent<Duskborn.Audio.PlayerAudioFeedback>();
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

        private void Update()
        {
            if (_waterInteraction != null)
            {
                bool moving = IsOwner ? IsMoving : (_cc != null && _cc.velocity.sqrMagnitude > 0.05f);
                _waterInteraction.NotifyMovement(moving, _cc != null ? _cc.velocity : Vector3.zero);
            }

            if (!IsOwner) return;
            if (!_inputEnabled || !_stats.IsAlive) return;
            SmoothInput();
            HandleMovement();
            HandleGravity();
            UpdateAnimator();
        }

        // -------------------------------------------------------------------------

        private void SmoothInput()
        {
            float smoothTime = _moveInput.sqrMagnitude > 0.01f ? accelTime : decelTime;
            _smoothedInput = Vector2.SmoothDamp(
                _smoothedInput, _moveInput, ref _inputSmoothVelocity, smoothTime);
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

            // Vetor de movimento relativo à orientação da câmera
            Vector3 moveDir = camForward * _smoothedInput.y + camRight * _smoothedInput.x;
            float waterMod = _waterInteraction != null ? _waterInteraction.SpeedModifier : 1f;
            _cc.Move(moveDir * (_stats.MoveSpeed * waterMod * Time.deltaTime));
        }

        private void HandleGravity()
        {
            if (_cc.isGrounded)
                _velocity.y = -0.5f;
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
