using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Duskborn.Effects;
using Duskborn.UI;

namespace Duskborn.Gameplay.Player
{
    /// <summary>
    /// Professional third-person camera system for Duskborn.
    /// Supports full spherical orbit (Yaw/Pitch), active SphereCast occlusion prevention,
    /// shoulder framing, terrain damping, dynamic FOV, and CameraShake integration.
    /// Runs strictly on the local player instance (IsOwner).
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerCameraController : MonoBehaviour
    {
        public static PlayerCameraController LocalInstance { get; set; }

        [Header("Target and Positioning")]
        [Tooltip("Pivot point relative to the player (chest / eye height).")]
        [SerializeField] private Vector3 pivotOffset = new Vector3(0f, 1.6f, 0f);

        [Tooltip("Horizontal offset to the right (over-the-shoulder view).")]
        [SerializeField] private float shoulderOffset = 0.35f;

        [Tooltip("Pivot position damping time to smooth steps and elevation changes on low-poly terrain.")]
        [SerializeField] private float pivotDampTime = 0.05f;

        [Header("Orbit and Rotation Control")]
        [Tooltip("Horizontal mouse sensitivity.")]
        [SerializeField] private float sensitivityX = 0.15f;

        [Tooltip("Vertical mouse sensitivity.")]
        [SerializeField] private float sensitivityY = 0.15f;

        [Tooltip("Lower vertical angle limit (degrees).")]
        [SerializeField] private float minPitch = -20f;

        [Tooltip("Upper vertical angle limit (degrees).")]
        [SerializeField] private float maxPitch = 70f;

        [Tooltip("Invert the camera's vertical axis.")]
        [SerializeField] private bool invertPitch = false;

        [Tooltip("Camera rotation smoothing time (prevents mouse jolts).")]
        [SerializeField] private float rotationDampTime = 0.02f;

        [Header("Distance and Zoom")]
        [Tooltip("Default camera viewing distance.")]
        [SerializeField] private float defaultDistance = 6.5f;

        [Tooltip("Minimum permitted zoom-in distance.")]
        [SerializeField] private float minDistance = 2.0f;

        [Tooltip("Maximum permitted zoom-out distance.")]
        [SerializeField] private float maxDistance = 11.0f;

        [Tooltip("Distance change per zoom scroll step.")]
        [SerializeField] private float zoomStep = 1.0f;

        [Tooltip("Smooth zoom transition speed.")]
        [SerializeField] private float zoomDampTime = 0.1f;

        [Header("Environment Collision and Occlusion")]
        [Tooltip("Physics layers that block the camera (terrain, rocks, buildings).")]
        [SerializeField] private LayerMask collisionLayers;

        [Tooltip("SphereCast radius to prevent the camera's front plane from penetrating geometry.")]
        [SerializeField] private float collisionRadius = 0.22f;

        [Tooltip("Damping distance from the collided surface.")]
        [SerializeField] private float collisionPadding = 0.2f;

        [Tooltip("Smooth return speed when moving away from an obstacle.")]
        [SerializeField] private float collisionRecoverySpeed = 6.0f;

        [Header("Game Feel")]
        [Tooltip("Base field of view (FOV).")]
        [SerializeField] private float defaultFov = 60f;

        [Tooltip("FOV increase during dodge rolls or running to convey speed.")]
        [SerializeField] private float dynamicFovKick = 4f;

        [Tooltip("Field of view interpolation speed.")]
        [SerializeField] private float fovTransitionSpeed = 8f;

        [Header("Cursor Control")]
        [SerializeField, Range(0.3f, 1f)] private float aimDistanceMultiplier = 0.7f;
        [SerializeField, Range(0.5f, 1f)] private float aimFovMultiplier = 0.85f;

        [Tooltip("Automatically lock the cursor during combat gameplay.")]
        [SerializeField] private bool autoLockCursor = true;

        // Player references.
        private Camera           _mainCam;
        private PlayerController _controller;
        private PlayerDodge      _dodge;
        private PlayerCombat     _combat;

        // Rotation and aiming state.
        private float   _targetYaw;
        private float   _targetPitch = 20f;
        private float   _currentYaw;
        private float   _currentPitch = 20f;
        private float   _yawVelocity;
        private float   _pitchVelocity;
        private Vector2 _lookInput;

        // Position and distance state.
        private Vector3 _smoothedPivotPos;
        private Vector3 _pivotVelocity;
        private float   _targetDistance;
        private float   _currentDistance;
        private float   _distanceVelocity;

        // Control locks and state.
        private bool _isInitialized;
        private bool _isRotationLocked;
        private bool _isCursorLocked;

        private readonly RaycastHit[] _sphereCastHits = new RaycastHit[16];

        // Public orientation and movement properties.
        public bool    IsRotationLocked => _isRotationLocked;
        public float   CurrentYaw    => _currentYaw;
        public float   CurrentPitch  => _currentPitch;
        public Camera  MainCamera    => _mainCam;
        public Vector3 CameraForward => _mainCam != null ? Vector3.ProjectOnPlane(_mainCam.transform.forward, Vector3.up).normalized : transform.forward;
        public Vector3 CameraRight   => _mainCam != null ? Vector3.ProjectOnPlane(_mainCam.transform.right, Vector3.up).normalized : transform.right;

        private void Awake()
        {
            _controller = GetComponent<PlayerController>();
            _dodge      = GetComponent<PlayerDodge>();
            _combat     = GetComponent<PlayerCombat>();
            _mainCam    = Camera.main;

            _targetYaw        = transform.eulerAngles.y;
            _currentYaw       = _targetYaw;
            _targetDistance   = defaultDistance;
            _currentDistance  = defaultDistance;
            _smoothedPivotPos = transform.position + pivotOffset;

            // If no collision layers are assigned in the Inspector, use Default and ResourceNode.
            if (collisionLayers.value == 0)
            {
                collisionLayers = LayerMask.GetMask("Default", "ResourceNode");
                if (collisionLayers.value == 0)
                    collisionLayers = 1 << 0; // Layer 0 (Default)
            }
        }

        private void Start()
        {
            // Initialize if the controller already has confirmed ownership at Start.
            if (_controller != null && _controller.IsOwner)
            {
                InitializeForOwner();
            }
            // Do not set enabled = false; allow LateUpdate to detect IsOwner when the network message arrives.
        }

        /// <summary>
        /// Initialize the camera for the locally owned player instance.
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

            if (_mainCam != null)
            {
                Duskborn.Core.EnvironmentVisualBootstrapper.EnsureCameraPostProcessing(_mainCam);
            }

            _targetYaw        = transform.eulerAngles.y;
            _currentYaw       = _targetYaw;
            _targetPitch      = 20f;
            _currentPitch     = 20f;
            _targetDistance   = defaultDistance;
            _currentDistance  = defaultDistance;
            _smoothedPivotPos = transform.position + pivotOffset;

            SnapCameraToTarget();

            // Apply sensitivity and inversion loaded from GameSettings.
            sensitivityX = Duskborn.Core.GameSettings.MouseSensitivity;
            sensitivityY = Duskborn.Core.GameSettings.MouseSensitivity;
            invertPitch  = Duskborn.Core.GameSettings.InvertPitch;

            if (autoLockCursor)
                SetCursorLocked(true);
        }

        /// <summary>
        /// Instantly position the camera behind the player (avoids long interpolation on spawn).
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
        /// Capture Look rotation input dispatched by Unity PlayerInput.
        /// </summary>
        public void OnLook(InputValue value)
        {
            if (_controller != null && !_controller.IsOwner) return;
            if (_isRotationLocked || IsAnyMenuOpen()) return;
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

            // Instantly reposition if the player teleported or spawned far from the previous pivot.
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

        // ── Input and Controls ──

        private void HandleInput()
        {
            if (_isRotationLocked || IsAnyMenuOpen())
            {
                _lookInput = Vector2.zero;
                return;
            }

            // Do not rotate the camera when the cursor is unlocked (e.g. free mouse for UI).
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
            if (_isRotationLocked || IsAnyMenuOpen()) return;

            // Support zoom while holding Alt or using dedicated keys ([ and ]).
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            float scroll = 0f;

            if (kb != null)
            {
                if (kb.leftBracketKey.wasPressedThisFrame) scroll = 1f;
                else if (kb.rightBracketKey.wasPressedThisFrame) scroll = -1f;
                else if (kb.altKey.isPressed && mouse != null && !Duskborn.Gameplay.Building.BuildingController.IsPlacing)
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

        private int  _justLockedCursorFrame = -1;
        private bool _wasAnyMenuOpen;
        private bool _isAltUnlocked;
        public bool JustLockedCursorThisFrame => _justLockedCursorFrame == Time.frameCount;

        /// <summary>
        /// Check whether any interface menu is currently open in the game.
        /// </summary>
        public static bool IsAnyMenuOpen()
        {
            if (Duskborn.Gameplay.Building.BuildingController.MenuOpen) return true;
            if (InGameMenuController.Instance != null && InGameMenuController.Instance.IsOpen)
                return true;

            if (CraftingUIManager.Instance != null && CraftingUIManager.Instance.IsOpen)
                return true;

            if (InventoryUIManager.Instance != null && InventoryUIManager.Instance.IsOpen)
                return true;

            if (CharacterUIManager.Instance != null && CharacterUIManager.Instance.IsOpen)
                return true;

            var hud = GameHUD.Instance ?? UnityEngine.Object.FindAnyObjectByType<GameHUD>();
            if (hud != null && hud.ShowStats)
                return true;

            if (WorldLoadingScreenUI.Instance != null &&
                (WorldLoadingScreenUI.Instance.isGenerating ||
                 (WorldLoadingScreenUI.Instance.gameObject.activeInHierarchy && WorldLoadingScreenUI.Instance.CurrentAlpha > 0.01f)))
                return true;

            return false;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) return;
            if (_controller != null && !_controller.IsOwner) return;

            // When the game window regains focus, hide and lock the cursor if no menu is open.
            if (!IsAnyMenuOpen())
            {
                _isAltUnlocked = false;
                SetRotationLocked(false);
                SetCursorLocked(true);
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                _justLockedCursorFrame = Time.frameCount;
            }
        }

        private void UpdateCursorLock()
        {
            // Record the state before automatic cursor recovery below. An ordinary
            // gameplay click must not be tagged as a click used to restore focus.
            bool cursorWasLocked = _isCursorLocked && Cursor.lockState == CursorLockMode.Locked && !Cursor.visible;
            // Convenience shortcut: Left Alt temporarily toggles the cursor during testing.
            var kb = Keyboard.current;
            // Alt is also the default dodge key. Never unlock the cursor during a
            // ranged dodge, otherwise holding RMB cannot resume aim afterwards.
            if (kb != null && kb.leftAltKey.wasPressedThisFrame && !_isRotationLocked &&
                !(_combat != null && _combat.HasRangedWeaponEquipped))
            {
                _isAltUnlocked = !_isCursorLocked;
                SetCursorLocked(!_isCursorLocked);
                return;
            }

            bool anyMenuOpen = IsAnyMenuOpen();

            // When an open / visible element closes / hides, ensure the cursor disappears immediately.
            if (_wasAnyMenuOpen && !anyMenuOpen)
            {
                _isAltUnlocked = false;
                SetRotationLocked(false);
                SetCursorLocked(true);
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                _justLockedCursorFrame = Time.frameCount;
            }
            _wasAnyMenuOpen = anyMenuOpen;

            // If no menu is open and the player is not intentionally using Alt,
            // ensure the cursor is NEVER visible during gameplay.
            if (!anyMenuOpen && !_isAltUnlocked && (!_isCursorLocked || Cursor.visible || Cursor.lockState != CursorLockMode.Locked))
            {
                SetRotationLocked(false);
                SetCursorLocked(true);
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            // Detect a mouse click in this frame.
            bool mouseClicked = false;
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                mouseClicked = Mouse.current.leftButton.wasPressedThisFrame ||
                               Mouse.current.rightButton.wasPressedThisFrame;
            }
#endif
            if (!mouseClicked)
            {
                mouseClicked = Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1);
            }

            if (!mouseClicked) return;
            if (Duskborn.Gameplay.Building.BuildingController.MenuOpen) return;

            // If the pause menu is open, its OnGUI manages the cursor.
            if (InGameMenuController.Instance != null && InGameMenuController.Instance.IsOpen)
            {
                return;
            }

            // Do not close or interfere when the player is dragging an inventory item.
            if (InventoryUIManager.Instance != null && InventoryUIManager.Instance.Installer != null &&
                InventoryUIManager.Instance.Installer.IsDraggingItem)
            {
                return;
            }

            bool isOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

            // Case 1: NO menu is open and the cursor was visible / unlocked.
            // Clicking anywhere focuses the game, hides the cursor, and locks aim.
            if (!anyMenuOpen)
            {
                if (cursorWasLocked) return;
                _isAltUnlocked = false;
                SetRotationLocked(false);
                SetCursorLocked(true);
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                _justLockedCursorFrame = Time.frameCount;
                return;
            }

            // Case 2: a menu is open (Inventory, Crafting, or Stats).
            // If the player clicks an empty area (outside any UI frame or button), close the menu and focus the game.
            if (!isOverUI)
            {
                if (CraftingUIManager.Instance != null && CraftingUIManager.Instance.IsOpen)
                {
                    CraftingUIManager.Instance.Close();
                }

                if (InventoryUIManager.Instance != null && InventoryUIManager.Instance.IsOpen)
                {
                    InventoryUIManager.Instance.Close();
                }

                if (CharacterUIManager.Instance != null && CharacterUIManager.Instance.IsOpen)
                {
                    CharacterUIManager.Instance.Close();
                }

                var hud = GameHUD.Instance ?? UnityEngine.Object.FindAnyObjectByType<GameHUD>();
                if (hud != null && hud.ShowStats)
                {
                    hud.CloseStats();
                }

                _isAltUnlocked = false;
                SetRotationLocked(false);
                SetCursorLocked(true);
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                _justLockedCursorFrame = Time.frameCount;
            }
        }

        // Camera Updates

        private void UpdatePivotPosition()
        {
            Vector3 targetPivot = transform.position + pivotOffset;

            // Damped interpolation to smooth vertical jitter on procedural terrain.
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

            // Shoulder offset calculated from current camera rotation.
            Vector3 shoulderVector = orbitRot * (Vector3.right * shoulderOffset);
            Vector3 rayOrigin      = _smoothedPivotPos + shoulderVector;

            // Vector to the desired collision-free position.
            Vector3 backDir = -(orbitRot * Vector3.forward);

            // Desired distance with zoom smoothing.
            float desiredDistance = Mathf.SmoothDamp(
                _currentDistance,
                _combat != null && _combat.IsAiming ? Mathf.Max(minDistance, _targetDistance * aimDistanceMultiplier) : _targetDistance,
                ref _distanceVelocity,
                zoomDampTime);

            // Spherical obstruction detection (SphereCastNonAlloc) ignoring the player.
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
                // Ignore the player and all objects in its hierarchy.
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
                // Immediate push to prevent the camera from entering geometry.
                float safeHitDist = Mathf.Max(closestHitDist - collisionPadding, minDistance);
                resolvedDistance  = Mathf.Min(safeHitDist, desiredDistance);
                _currentDistance  = resolvedDistance;
            }
            else
            {
                // Smooth return when moving away from obstacles.
                _currentDistance = Mathf.Lerp(
                    _currentDistance,
                    desiredDistance,
                    collisionRecoverySpeed * Time.deltaTime);
                resolvedDistance = _currentDistance;
            }

            // Final camera position + independent shake (CameraShake).
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
            if (_combat != null && _combat.IsAiming)
                targetFov *= aimFovMultiplier;
            else if (isDodgeRolling)
                targetFov += dynamicFovKick;
            else if (isMovingFast)
                targetFov += dynamicFovKick * 0.5f;

            _mainCam.fieldOfView = Mathf.Lerp(
                _mainCam.fieldOfView,
                targetFov,
                fovTransitionSpeed * Time.deltaTime);
        }

        // Public State Management

        /// <summary>
        /// Lock or unlock the mouse cursor and set its visibility.
        /// </summary>
        public void SetCursorLocked(bool locked)
        {
            _isCursorLocked  = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible   = !locked;
        }

        /// <summary>
        /// Prevent camera rotation from receiving mouse input (used when opening inventories / menus).
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
                SetCursorLocked(true);
            }
        }

        public float SensitivityX
        {
            get => sensitivityX;
            set => sensitivityX = value;
        }

        public float SensitivityY
        {
            get => sensitivityY;
            set => sensitivityY = value;
        }

        public bool InvertPitch
        {
            get => invertPitch;
            set => invertPitch = value;
        }

        public void SetSensitivity(float sensitivity)
        {
            sensitivityX = sensitivity;
            sensitivityY = sensitivity;
        }
    }
}
