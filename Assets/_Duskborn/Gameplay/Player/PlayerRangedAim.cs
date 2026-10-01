using Duskborn.Gameplay.Equipment;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Duskborn.Gameplay.Player
{
    public partial class PlayerCombat
    {
        private PlayerDodge _aimDodge;
        private WeaponItem _aimWeapon;
        private float _nextAimedDrawAt;
        private bool _aiming;
        private bool _aimHasFocus = true;
        private float _nextAimPitchSendAt;
        private float _lastAimPitch;
        private float _rangedShotAt = -10f;
        private float _rangedHitAt = -10f;
        private bool _rangedHitCritical;
        private bool _isAimingAtEnemy;
        private bool _leftBowHeld;
        private bool _bowReleaseRequested;

        public bool HasRangedWeaponEquipped => _weaponHandler?.ActiveWeapon?.Behaviour is RangedWeaponBehaviour;

        public bool IsAiming => IsOwner && _aiming && _stats.IsAlive &&
            (_aimDodge == null || !_aimDodge.IsRecovering) && !RangedInputBlocked;

        private bool RangedInputBlocked => !_aimHasFocus || Time.timeScale <= 0f ||
            Cursor.lockState != CursorLockMode.Locked || PlayerCameraController.IsAnyMenuOpen() ||
            Duskborn.Gameplay.Building.BuildingController.BlocksGameplay;

        private void UpdateRangedAim()
        {
            if (_aimDodge == null) _aimDodge = GetComponent<PlayerDodge>();
            var weapon = actionBarInstaller?.Service.GetSelectedItem() as WeaponItem;
            bool allowed = _stats.IsAlive && !RangedInputBlocked &&
                (_aimDodge == null || !_aimDodge.IsRecovering);
            bool hasRanged = weapon?.Behaviour is RangedWeaponBehaviour && _weaponHandler?.ActiveWeapon == weapon;
            bool leftHeld = Mouse.current != null && Mouse.current.leftButton.isPressed;
            bool rightHeld = Mouse.current != null && Mouse.current.rightButton.isPressed;
            if (!allowed || !hasRanged || (_aimWeapon != null && _aimWeapon != weapon))
                StopRangedAim();
            // Remember the edge ourselves: Input System callbacks and Update can both
            // visit this method in one frame. A held button must never request release.
            if (allowed && hasRanged && _leftBowHeld && !leftHeld && _weaponActionPlayer?.IsDrawingBow == true)
            {
                _weaponActionPlayer.RequestAimedShot();
                _bowReleaseRequested = true;
            }
            _leftBowHeld = allowed && hasRanged && leftHeld;
            if (_weaponActionPlayer?.IsPlaying != true) _bowReleaseRequested = false;
            bool wantsAim = allowed && (rightHeld || leftHeld || _bowReleaseRequested) &&
                hasRanged && !(PlayerCameraController.LocalInstance?.JustLockedCursorThisFrame ?? false);

            if (allowed && hasRanged)
                UpdateTargetDetection();
            else
                _isAimingAtEnemy = false;

            if (!wantsAim || _aimWeapon != weapon)
                StopRangedAim();

            // Also interrupt ordinary bow shots when dodging, opening UI or dying.
            if (!allowed && _weaponActionPlayer?.CurrentWeapon?.Behaviour is RangedWeaponBehaviour)
                _weaponActionPlayer.CancelAction();

            if (!wantsAim) return;
            _aiming = true;
            _aimWeapon = weapon;
            if (_weaponActionPlayer != null && !_weaponActionPlayer.IsPlaying &&
                (rightHeld || leftHeld) && Time.time >= _nextAimedDrawAt && _cooldown <= 0f)
                _weaponActionPlayer.PlayAction(0, weapon, BuildContext(), true);
            float pitch = PlayerCameraController.LocalInstance?.CurrentPitch ?? 0f;
            _weaponActionPlayer?.SetRangedAimPitch(pitch);
            if (Time.time >= _nextAimPitchSendAt && Mathf.Abs(pitch - _lastAimPitch) > 0.5f)
            {
                _nextAimPitchSendAt = Time.time + 0.1f;
                _lastAimPitch = pitch;
                SetRangedAimPitchRpc(pitch);
            }
        }

        private void UpdateTargetDetection()
        {
            var camera = PlayerCameraController.LocalInstance?.MainCamera;
            if (camera == null)
            {
                _isAimingAtEnemy = false;
                return;
            }

            Ray ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!hit.transform.IsChildOf(transform))
                {
                    var enemy = hit.collider.GetComponentInParent<Duskborn.Gameplay.Enemies.EnemyBase>();
                    if (enemy != null && enemy.IsAlive)
                    {
                        _isAimingAtEnemy = true;
                        return;
                    }
                }
            }

            // Forgiving sphere tolerance (0.4m radius) for smaller or agile units
            if (Physics.SphereCast(ray, 0.4f, out RaycastHit sHit, 100f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!sHit.transform.IsChildOf(transform))
                {
                    var enemy = sHit.collider.GetComponentInParent<Duskborn.Gameplay.Enemies.EnemyBase>();
                    if (enemy != null && enemy.IsAlive)
                    {
                        _isAimingAtEnemy = true;
                        return;
                    }
                }
            }

            _isAimingAtEnemy = false;
        }

        public void InterruptRangedAimForDodge()
        {
            _bufferedAction = null;
            StopRangedAim();
            if (_weaponActionPlayer?.CurrentWeapon?.Behaviour is RangedWeaponBehaviour)
                _weaponActionPlayer.CancelAction();
        }

        private void StopRangedAim()
        {
            _leftBowHeld = false;
            _bowReleaseRequested = false;
            _aiming = false;
            _aimWeapon = null;
            if (_weaponActionPlayer != null && _weaponActionPlayer.IsRangedAimAction)
                _weaponActionPlayer.CancelAction(blendOut: _stats.IsAlive && !RangedInputBlocked && !(_aimDodge?.IsRecovering ?? false));
        }

        private void OnApplicationFocus(bool focused)
        {
            _aimHasFocus = focused;
            if (!focused && IsOwner) InterruptRangedAimForDodge();
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || !IsOwner) return;

            bool isAiming = IsAiming;
            float hitProgress = Mathf.Clamp01(1f - (Time.unscaledTime - _rangedHitAt) / 0.32f);
            bool hasRanged = HasRangedWeaponEquipped && _stats != null && _stats.IsAlive && !RangedInputBlocked;

            if (!isAiming && !hasRanged && hitProgress <= 0f) return;

            // Exactly the viewport center used by ReleaseRangedAttack's camera ray.
            var camera = PlayerCameraController.LocalInstance?.MainCamera;
            if (camera == null) return;
            Rect viewport = camera.pixelRect;
            float x = viewport.center.x;
            float y = Screen.height - viewport.center.y;
            float scale = Mathf.Max(0.75f, Screen.height / 1080f);

            float drawProgress = (isAiming && _weaponActionPlayer != null) ? _weaponActionPlayer.RangedDrawProgress : 0f;
            float shotProgress = Mathf.Clamp01(1f - (Time.unscaledTime - _rangedShotAt) / 0.18f);

            Duskborn.UI.RangedReticle.Draw(
                new Vector2(x, y),
                scale,
                drawProgress,
                shotProgress,
                hitProgress,
                _rangedHitCritical,
                isAiming,
                _isAimingAtEnemy);
        }
    }
}
