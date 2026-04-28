using UnityEngine;
using UnityEngine.InputSystem;

namespace Duskborn.Gameplay.Player
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerStats))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Camera")]
        [SerializeField] private float cameraDistance   = 8f;
        [SerializeField] private float cameraHeight     = 10f;
        [SerializeField] private float cameraSmoothing  = 8f;
        [SerializeField] private float cameraLookOffset = 1f;
        [SerializeField] private float cameraSensitivity = 3f;

        [Header("Movement")]
        [SerializeField] private float gravityMultiplier = 2f;
        [SerializeField] private float rotationSpeed     = 720f;

        private CharacterController _cc;
        private PlayerStats         _stats;
        private Camera              _mainCam;

        private Vector2 _moveInput;
        private Vector3 _velocity;
        private float   _cameraYaw;
        private bool    _inputEnabled = true;

        public bool IsMoving => _moveInput.sqrMagnitude > 0.01f;

        private void Awake()
        {
            _cc      = GetComponent<CharacterController>();
            _stats   = GetComponent<PlayerStats>();
            _mainCam = Camera.main;

            // Start camera behind the player
            _cameraYaw = transform.eulerAngles.y;
        }

        public void OnMove(InputValue value) => _moveInput = value.Get<Vector2>();

        private void Update()
        {
            if (!_inputEnabled || !_stats.IsAlive) return;
            HandleCameraRotation();
            HandleMovement();
            HandleGravity();
        }

        private void LateUpdate() => FollowCamera();

        // -------------------------------------------------------------------------

        private void HandleCameraRotation()
        {
            if (Input.GetMouseButton(1))
                _cameraYaw += Input.GetAxis("Mouse X") * cameraSensitivity;
        }

        private void HandleMovement()
        {
            if (_moveInput.sqrMagnitude < 0.01f) return;

            Vector3 camForward = Vector3.ProjectOnPlane(_mainCam.transform.forward, Vector3.up).normalized;
            Vector3 camRight   = Vector3.ProjectOnPlane(_mainCam.transform.right,   Vector3.up).normalized;
            Vector3 moveDir    = (camForward * _moveInput.y + camRight * _moveInput.x).normalized;

            _cc.Move(moveDir * (_stats.MoveSpeed * Time.deltaTime));

            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, Quaternion.LookRotation(moveDir), rotationSpeed * Time.deltaTime);
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

            // Rotate the offset around the player by _cameraYaw, then add height.
            Vector3 offset  = Quaternion.Euler(0f, _cameraYaw, 0f) * new Vector3(0f, 0f, -cameraDistance);
            Vector3 desired = transform.position + offset + Vector3.up * cameraHeight;

            _mainCam.transform.position = Vector3.Lerp(
                _mainCam.transform.position, desired, cameraSmoothing * Time.deltaTime);
            _mainCam.transform.LookAt(transform.position + Vector3.up * cameraLookOffset);
        }

        public void SetInputEnabled(bool enabled) => _inputEnabled = enabled;
    }
}
