using FishNet.Object;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Duskborn.Gameplay.Player
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerStats))]
    public class PlayerController : NetworkBehaviour
    {
        [Header("Camera")]
        [SerializeField] private float cameraDistance    = 8f;
        [SerializeField] private float cameraHeight      = 10f;
        [SerializeField] private float cameraSmoothing   = 8f;
        [SerializeField] private float cameraLookOffset  = 1f;
        [SerializeField] private float cameraSensitivity = 3f;

        [Header("Movement")]
        [SerializeField] private float gravityMultiplier = 2f;
        [SerializeField] private float rotationSpeed     = 720f;
        [SerializeField] private float accelTime         = 0.15f;
        [SerializeField] private float decelTime         = 0.08f;

        [Header("Animation")]
        [SerializeField] private Animator _animator;

        private static readonly int HashVelocityX = Animator.StringToHash("VelocityX");
        private static readonly int HashVelocityY = Animator.StringToHash("VelocityY");

        private CharacterController _cc;
        private PlayerStats         _stats;
        private Camera              _mainCam;

        private Vector2 _moveInput;
        private Vector2 _smoothedInput;
        private Vector2 _inputSmoothVelocity;
        private Vector3 _velocity;
        private float   _cameraYaw;
        private bool    _inputEnabled = true;

        public bool IsMoving => _moveInput.sqrMagnitude > 0.01f;

        private void Awake()
        {
            _cc      = GetComponent<CharacterController>();
            _stats   = GetComponent<PlayerStats>();
            _mainCam = Camera.main;

            _cameraYaw = transform.eulerAngles.y;
        }

        public void OnMove(InputValue value)
        {
            if (!IsOwner) return;
            _moveInput = value.Get<Vector2>();
        }

        private void Update()
        {
            if (!IsOwner) return;
            if (!_inputEnabled || !_stats.IsAlive) return;
            HandleCameraRotation();
            SmoothInput();
            HandleMovement();
            HandleGravity();
            UpdateAnimator();
        }

        private void LateUpdate()
        {
            if (!IsOwner) return;
            FollowCamera();
        }

        // -------------------------------------------------------------------------

        private void HandleCameraRotation()
        {
            if (Input.GetMouseButton(1))
                _cameraYaw += Input.GetAxis("Mouse X") * cameraSensitivity;
        }

        private void SmoothInput()
        {
            float smoothTime = _moveInput.sqrMagnitude > 0.01f ? accelTime : decelTime;
            _smoothedInput = Vector2.SmoothDamp(
                _smoothedInput, _moveInput, ref _inputSmoothVelocity, smoothTime);
        }

        private void HandleMovement()
        {
            // Character always faces camera yaw — WASD never rotates the body.
            Quaternion targetRot = Quaternion.Euler(0f, _cameraYaw, 0f);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, targetRot, rotationSpeed * Time.deltaTime);

            if (_smoothedInput.sqrMagnitude < 0.001f) return;

            Vector3 camForward = Vector3.ProjectOnPlane(_mainCam.transform.forward, Vector3.up).normalized;
            Vector3 camRight   = Vector3.ProjectOnPlane(_mainCam.transform.right,   Vector3.up).normalized;

            // Not normalized — magnitude encodes current speed fraction (0–1).
            Vector3 moveDir = camForward * _smoothedInput.y + camRight * _smoothedInput.x;
            _cc.Move(moveDir * (_stats.MoveSpeed * Time.deltaTime));
        }

        private void HandleGravity()
        {
            if (_cc.isGrounded)
                _velocity.y = -0.5f;
            else
                _velocity.y += Physics.gravity.y * gravityMultiplier * Time.deltaTime;

            _cc.Move(_velocity * Time.deltaTime);
        }

        private void FollowCamera()
        {
            if (_mainCam == null) return;

            Vector3 offset  = Quaternion.Euler(0f, _cameraYaw, 0f) * new Vector3(0f, 0f, -cameraDistance);
            Vector3 desired = transform.position + offset + Vector3.up * cameraHeight;

            _mainCam.transform.position = Vector3.Lerp(
                _mainCam.transform.position, desired, cameraSmoothing * Time.deltaTime);
            _mainCam.transform.LookAt(transform.position + Vector3.up * cameraLookOffset);
        }

        private void UpdateAnimator()
        {
            if (_animator == null) return;

            // Character faces camera, so smoothed input IS local-space velocity:
            // Y = forward/back, X = strafe. No projection needed.
            _animator.SetFloat(HashVelocityX, _smoothedInput.x);
            _animator.SetFloat(HashVelocityY, _smoothedInput.y);
        }

        public void SetInputEnabled(bool enabled) => _inputEnabled = enabled;
    }
}
