using System.Collections;
using FishNet.Connection;
using Duskborn.Gameplay.Equipment;
using Duskborn.Gameplay.Projectiles;
using FishNet.Object;
using UnityEngine;

namespace Duskborn.Gameplay.Player
{
    public partial class PlayerCombat
    {
        private WeaponDefinition pendingRangedWeapon;
        private readonly RangedAttackGate rangedGate = new();

        [Header("Ranged Projectile Spawn")]
        [Tooltip("Optional transform for spawn origin. If assigned, its world position and rotation serve as the spawn basis.")]
        [SerializeField] private Transform rangedSpawnPoint;
        [Tooltip("Spawn position offset relative to player (X=Right, Y=Up, Z=Forward). Defaults to (0, 1.3, 0).")]
        [SerializeField] private Vector3 rangedSpawnOffset = new Vector3(0f, 1.3f, 0f);
        [Tooltip("Spawn rotation offset in Euler angles (Pitch, Yaw, Roll) relative to the player/aim direction.")]
        [SerializeField] private Vector3 rangedSpawnRotationOffset = Vector3.zero;

        public Transform RangedSpawnPoint { get => rangedSpawnPoint; set => rangedSpawnPoint = value; }
        public Vector3 RangedSpawnOffset { get => rangedSpawnOffset; set => rangedSpawnOffset = value; }
        public Vector3 RangedSpawnRotationOffset { get => rangedSpawnRotationOffset; set => rangedSpawnRotationOffset = value; }

        public void GetRangedSpawn(RangedWeaponBehaviour ranged, out Vector3 position, out Quaternion rotation, Vector3? targetAimDirection = null)
        {
            var hand = _weaponActionPlayer != null ? _weaponActionPlayer.RangedDrawingHand : null;
            if (ranged?.projectile != null && !ranged.useCustomSpawnOffset && rangedSpawnPoint == null && hand != null)
            {
                rotation = targetAimDirection.HasValue && targetAimDirection.Value.sqrMagnitude > 0.0001f
                    ? Quaternion.LookRotation(targetAimDirection.Value) : _weaponActionPlayer.RangedAimRotation;
                // Bone-driven bows use the aim directly; old body-offset fitting rotations
                // would make the held arrow jump sideways on release.
                position = ranged.projectile.TipPositionFromNock(hand.position, rotation);
                return;
            }
            ResolveRangedSpawn(transform, rangedSpawnPoint, rangedSpawnOffset, rangedSpawnRotationOffset,
                ranged, out position, out rotation, targetAimDirection);
        }

        public static void ResolveRangedSpawn(Transform player, Transform spawnPoint, Vector3 posOffset,
            Vector3 rotOffset, RangedWeaponBehaviour ranged, out Vector3 position, out Quaternion rotation,
            Vector3? targetAimDirection = null)
        {
            bool weaponOverride = ranged != null && ranged.useCustomSpawnOffset;
            if (weaponOverride)
            {
                posOffset = ranged.spawnPositionOffset;
                rotOffset = ranged.spawnRotationOffset;
            }

            if (spawnPoint != null && !weaponOverride)
            {
                position = spawnPoint.position;
                Quaternion baseRot = targetAimDirection.HasValue && targetAimDirection.Value.sqrMagnitude > 0.0001f
                    ? Quaternion.LookRotation(targetAimDirection.Value)
                    : spawnPoint.rotation;
                rotation = baseRot * Quaternion.Euler(rotOffset);
            }
            else
            {
                position = player.TransformPoint(posOffset);
                Quaternion baseRot = targetAimDirection.HasValue && targetAimDirection.Value.sqrMagnitude > 0.0001f
                    ? Quaternion.LookRotation(targetAimDirection.Value)
                    : player.rotation;
                rotation = baseRot * Quaternion.Euler(rotOffset);
            }
        }

        public void GetRangedSpawn(WeaponDefinition weapon, out Vector3 position, out Quaternion rotation, Vector3? targetAimDirection = null)
            => GetRangedSpawn(weapon?.Behaviour as RangedWeaponBehaviour, out position, out rotation, targetAimDirection);

        public void GetRangedSpawn(WeaponItem weapon, out Vector3 position, out Quaternion rotation, Vector3? targetAimDirection = null)
            => GetRangedSpawn(weapon?.Behaviour as RangedWeaponBehaviour, out position, out rotation, targetAimDirection);

        [ServerRpc]
        private void SetRangedAimPitchRpc(float pitch)
        {
            if (!_stats.IsAlive || float.IsNaN(pitch) || float.IsInfinity(pitch)) return;
            ShowRangedAimPitchRpc(Mathf.Clamp(pitch, -70f, 70f));
        }

        [ObserversRpc(ExcludeOwner = true)]
        private void ShowRangedAimPitchRpc(float pitch) => _weaponActionPlayer?.SetRangedAimPitch(pitch);

        public void BeginRangedAttack(WeaponItem item, bool holdDraw = false)
        {
            if (IsOwner && _stats.IsAlive) BeginRangedRpc(item.Id, holdDraw);
        }

        public void CancelRangedAttack(bool hideWeapon = false)
        {
            if (IsOwner) CancelRangedRpc(hideWeapon);
        }

        [ServerRpc]
        private void CancelRangedRpc(bool hideWeapon)
        {
            pendingRangedWeapon = null;
            rangedGate.Cancel();
            if (hideWeapon) _weaponHandler?.ClearObservedRangedWeapon();
            CancelRangedWindupRpc(hideWeapon);
        }

        [ObserversRpc(ExcludeOwner = true)]
        private void CancelRangedWindupRpc(bool hideWeapon)
        {
            if (_weaponActionPlayer?.CurrentWeapon?.Behaviour is RangedWeaponBehaviour)
                _weaponActionPlayer.CancelAction();
            if (hideWeapon) _weaponHandler?.ClearObservedRangedWeapon();
        }

        [ServerRpc]
        private void BeginRangedRpc(string weaponId, bool holdDraw)
        {
            if (!_stats.IsAlive || string.IsNullOrEmpty(weaponId) || weaponId.Contains("/") || weaponId.Contains("\\")) return;
            // Only server-authored definitions in this catalog can be fired. Inventory
            // selection is currently local in this project; no damage values come from clients.
            var definition = Resources.Load<WeaponDefinition>("Weapons/" + weaponId);
            if (definition == null || !(definition.Behaviour is RangedWeaponBehaviour ranged) || ranged.projectile == null) return;
            if (!RangedWeaponBehaviour.TryGetTiming(definition.Actions, out float release, out float duration)) return;
            if (!rangedGate.TryBegin(Time.timeAsDouble, release, duration, 1f / Mathf.Max(0.01f, _stats.AttackSpeed), holdDraw))
            {
                RejectRangedDrawTarget(Owner, (float)System.Math.Max(0.1, rangedGate.NextAttackAt - Time.timeAsDouble));
                return;
            }
            pendingRangedWeapon = definition;
            _weaponHandler?.ShowObservedWeapon(definition);
            ShowRangedWindupRpc(weaponId, holdDraw);
        }

        [TargetRpc]
        private void RejectRangedDrawTarget(NetworkConnection connection, float retryAfter)
        {
            // Network jitter may put the next local draw slightly ahead of server recovery.
            // Remove the unaccepted pose and retry held aim after the authoritative delay.
            _nextAimedDrawAt = Time.time + retryAfter;
            _weaponActionPlayer?.CancelAction(preserveAimMovement: true);
        }

        [ObserversRpc(ExcludeOwner = true)]
        private void ShowRangedWindupRpc(string weaponId, bool holdDraw)
        {
            var definition = Resources.Load<WeaponDefinition>("Weapons/" + weaponId);
            _weaponHandler?.ShowObservedWeapon(definition);
            if (definition != null && _weaponActionPlayer != null)
                _weaponActionPlayer.PlayAction(0, (WeaponItem)definition.CreateRuntimeItem(), BuildContext(), holdDraw);
        }

        public void ReleaseRangedAttack()
        {
            if (!IsOwner || !_stats.IsAlive) return;
            if (RangedInputBlocked || (_aimDodge != null && _aimDodge.IsRecovering))
            { _weaponActionPlayer.CancelAction(); return; }
            var selected = actionBarInstaller?.Service.GetSelectedItem() as WeaponItem;
            if (selected == null || selected != _weaponActionPlayer.CurrentWeapon) { CancelRangedAttack(); return; }
            _rangedShotAt = Time.unscaledTime;
            if (_weaponActionPlayer.IsRangedAimAction &&
                RangedWeaponBehaviour.TryGetTiming(selected.Actions, out float releaseTime, out float duration))
            {
                _cooldown = Mathf.Max(duration - releaseTime, 1f / Mathf.Max(0.01f, _stats.AttackSpeed));
                _nextAimedDrawAt = Time.time + _cooldown;
            }
            GetRangedSpawn(selected, out Vector3 origin, out _);
            var camera = Camera.main;
            // Send the unadjusted aim. The server applies authored rotation once.
            var ranged = selected.Behaviour as RangedWeaponBehaviour;
            Vector3 aim = rangedSpawnPoint != null && !(ranged != null && ranged.useCustomSpawnOffset)
                ? rangedSpawnPoint.forward : _weaponActionPlayer.RangedAimRotation * Vector3.forward;
            var hand = _weaponActionPlayer.RangedDrawingHand;
            bool handSpawn = hand != null && rangedSpawnPoint == null && ranged != null && !ranged.useCustomSpawnOffset;
            if (handSpawn) origin = hand.position;
            if (camera != null)
            {
                var ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
                Vector3 point = ray.GetPoint(100f);
                float nearest = float.PositiveInfinity;
                foreach (var hit in Physics.RaycastAll(ray, 100f, ~0, QueryTriggerInteraction.Ignore))
                    if (!hit.transform.IsChildOf(transform) && hit.distance < nearest)
                    { nearest = hit.distance; point = hit.point; }
                aim = (point - origin).normalized;
            }
            ReleaseRangedRpc(aim);
        }

        [ServerRpc]
        private void ReleaseRangedRpc(Vector3 direction)
        {
            if (pendingRangedWeapon == null || !_stats.IsAlive ||
                !ProjectileFlight.IsFinite(direction) || direction.sqrMagnitude < 0.001f || direction.sqrMagnitude > 4f ||
                !rangedGate.TryRequestRelease(Time.timeAsDouble)) return;
            StartCoroutine(ReleaseAfterWindup(rangedGate.Generation, direction.normalized));
        }

        private IEnumerator ReleaseAfterWindup(int generation, Vector3 direction)
        {
            // Never accept a client release before the authored windup has elapsed.
            while (Time.timeAsDouble < rangedGate.ReleaseAt && generation == rangedGate.Generation) yield return null;
            if (pendingRangedWeapon == null || !_stats.IsAlive || !IsServerStarted ||
                !rangedGate.TryConsume(generation, Time.timeAsDouble)) yield break;
            var weapon = pendingRangedWeapon;
            pendingRangedWeapon = null;
            ReleaseObservedDrawRpc();
            var definition = ((RangedWeaponBehaviour)weapon.Behaviour).projectile;
            var item = (WeaponItem)weapon.CreateRuntimeItem();
            bool crit = Random.value < _stats.CritChance;
            float amount = _stats.Damage * definition.damageMultiplier * (crit ? _stats.CritMultiplier : 1f);
            int shot = ProjectileFlight.NextId();

            GetRangedSpawn(weapon, out Vector3 origin, out Quaternion spawnRotation, direction);
            direction = spawnRotation * Vector3.forward;

            ShowPlayerProjectileRpc(shot, weapon.Id, origin, direction, spawnRotation);
            var ranged = (RangedWeaponBehaviour)weapon.Behaviour;
            var hand = _weaponActionPlayer != null ? _weaponActionPlayer.RangedDrawingHand : null;
            Vector3? clearanceOrigin = hand != null && rangedSpawnPoint == null && !ranged.useCustomSpawnOffset
                ? hand.position : (Vector3?)null;
            ProjectileFlight.Launch(shot, definition, transform, true, origin, direction, true,
                (col, point, forward) => ProjectileDamage.Apply(col, point, forward, amount, crit, this, item),
                BroadcastProjectileImpact, spawnRotation, clearanceOrigin);
        }

        [ObserversRpc]
        private void ShowPlayerProjectileRpc(int shot, string weaponId, Vector3 origin, Vector3 direction, Quaternion rotation)
        {
            if (IsServerStarted) return;
            var weapon = Resources.Load<WeaponDefinition>("Weapons/" + weaponId);
            if (weapon?.Behaviour is RangedWeaponBehaviour ranged)
                ProjectileFlight.Launch(shot, ranged.projectile, transform, true, origin, direction, false, null, null, rotation);
        }

        // Route impacts through each connected player's object, so arrows remain
        // authoritative even after the firing enemy has died and returned to its pool.
        public static void BroadcastProjectileImpact(int shot, Vector3 point, Vector3 direction, NetworkObject target, string path,
            bool flesh, string surfaceTag)
        {
            foreach (var stats in PlayerRegistry.All)
            {
                if (stats == null) continue;
                var combat = stats.GetComponent<PlayerCombat>();
                if (combat != null && combat.IsServerStarted && combat.IsSpawned && combat.Owner.IsActive)
                    combat.PlayerProjectileImpactTarget(combat.Owner, shot, point, direction, target, path, flesh, surfaceTag);
            }
        }

        [ObserversRpc(ExcludeOwner = true)]
        private void ReleaseObservedDrawRpc() => _weaponActionPlayer?.RequestAimedShot();

        public void ConfirmRangedHit(bool critical)
        {
            if (IsServerStarted && IsSpawned && Owner.IsActive) ConfirmRangedHitTarget(Owner, critical);
        }

        [TargetRpc]
        private void ConfirmRangedHitTarget(NetworkConnection connection, bool critical)
        {
            _rangedHitAt = Time.unscaledTime;
            _rangedHitCritical = critical;
        }

        [TargetRpc]
        private void PlayerProjectileImpactTarget(NetworkConnection connection, int shot, Vector3 point, Vector3 direction,
            NetworkObject target, string path, bool flesh, string surfaceTag)
        {
            if (!IsServerStarted) ProjectileFlight.ReceiveImpact(shot, point, direction, target, path, flesh, surfaceTag);
        }
    }
}
