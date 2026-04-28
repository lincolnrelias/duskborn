using UnityEngine;
using UnityEngine.InputSystem;
using Duskborn.Gameplay.Player;

namespace Duskborn.Gameplay.Player
{
    /// <summary>
    /// Handles player movement and camera follow. Input via Unity Input System.
    /// Combat input routed to PlayerCombat (added in Phase 2).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerStats))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Camera")]
        [SerializeField] private float cameraDistance  = 8f;
        [SerializeField] private float cameraHeight    = 10f;
        [SerializeField] private float cameraSmoothing = 8f;
        [SerializeField] private float cameraLookOffset = 1f; // height above player to look at

        [Header("Movement")]
        [SerializeField] private float gravityMultiplier = 2f;
        [SerializeField] private float rotationSpeed = 720f; // deg/s

        private CharacterController _cc;
        private PlayerStats _stats;
        private Camera _mainCam;

        private Vector2 _moveInput;
        private Vector3 _velocity;
        private bool _inputEnabled = true;

        public bool IsMoving => _moveInput.sqrMagnitude > 0.01f;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _stats = GetComponent<PlayerStats>();
            _mainCam = Camera.main;
        }

        // Called by Unity Input System PlayerInput component
        public void OnMove(InputValue value) => _moveInput = value.Get<Vector2>();

        private void Update()
        {
            if (!_inputEnabled || !_stats.IsAlive) return;
            HandleMovement();
            HandleGravity();
        }

        private void LateUpdate() => FollowCamera();

        private void HandleMovement()
        {
            if (_moveInput.sqrMagnitude < 0.01f) return;

            // Camera-relative movement
            Vector3 camForward = Vector3.ProjectOnPlane(_mainCam.transform.forward, Vector3.up).normalized;
            Vector3 camRight = Vector3.ProjectOnPlane(_mainCam.transform.right, Vector3.up).normalized;
            Vector3 moveDir = (camForward * _moveInput.y + camRight * _moveInput.x).normalized;

            _cc.Move(moveDir * (_stats.MoveSpeed * Time.deltaTime));

            // Rotate to face movement direction
            Quaternion targetRot = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot,
                rotationSpeed * Time.deltaTime);
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
            // Fixed-angle isometric offset — no feedback loop.
            Vector3 desired = transform.position + new Vector3(0f, cameraHeight, -cameraDistance);
            _mainCam.transform.position = Vector3.Lerp(
                _mainCam.transform.position, desired, cameraSmoothing * Time.deltaTime);
            _mainCam.transform.LookAt(transform.position + Vector3.up * cameraLookOffset);
        }

        public void SetInputEnabled(bool enabled) => _inputEnabled = enabled;
    }
}
