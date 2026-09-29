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

        public void BeginRangedAttack(WeaponItem item)
        {
            if (IsOwner && _stats.IsAlive) BeginRangedRpc(item.Id);
        }

        public void CancelRangedAttack()
        {
            if (IsOwner) CancelRangedRpc();
        }

        [ServerRpc]
        private void CancelRangedRpc()
        {
            pendingRangedWeapon = null;
            rangedGate.Cancel();
            _weaponHandler?.ClearObservedRangedWeapon();
            CancelRangedWindupRpc();
        }

        [ObserversRpc(ExcludeOwner = true)]
        private void CancelRangedWindupRpc()
        {
            if (_weaponActionPlayer?.CurrentWeapon?.Behaviour is RangedWeaponBehaviour)
                _weaponActionPlayer.CancelAction();
            _weaponHandler?.ClearObservedRangedWeapon();
        }

        [ServerRpc]
        private void BeginRangedRpc(string weaponId)
        {
            if (!_stats.IsAlive || string.IsNullOrEmpty(weaponId) || weaponId.Contains("/") || weaponId.Contains("\\")) return;
            // Only server-authored definitions in this catalog can be fired. Inventory
            // selection is currently local in this project; no damage values come from clients.
            var definition = Resources.Load<WeaponDefinition>("Weapons/" + weaponId);
            if (definition == null || !(definition.Behaviour is RangedWeaponBehaviour ranged) || ranged.projectile == null) return;
            if (!RangedWeaponBehaviour.TryGetTiming(definition.Actions, out float release, out float duration)) return;
            if (!rangedGate.TryBegin(Time.timeAsDouble, release, duration, 1f / Mathf.Max(0.01f, _stats.AttackSpeed))) return;
            pendingRangedWeapon = definition;
            _weaponHandler?.ShowObservedWeapon(definition);
            ShowRangedWindupRpc(weaponId);
        }

        [ObserversRpc(ExcludeOwner = true)]
        private void ShowRangedWindupRpc(string weaponId)
        {
            var definition = Resources.Load<WeaponDefinition>("Weapons/" + weaponId);
            _weaponHandler?.ShowObservedWeapon(definition);
            if (definition != null && _weaponActionPlayer != null)
                _weaponActionPlayer.PlayAction(0, (WeaponItem)definition.CreateRuntimeItem(), BuildContext());
        }

        public void ReleaseRangedAttack()
        {
            if (!IsOwner || !_stats.IsAlive) return;
            var selected = actionBarInstaller?.Service.GetSelectedItem() as WeaponItem;
            if (selected == null || selected != _weaponActionPlayer.CurrentWeapon) { CancelRangedAttack(); return; }
            Vector3 origin = transform.position + Vector3.up * 1.3f;
            var camera = Camera.main;
            Vector3 aim = camera != null ? camera.transform.forward : transform.forward;
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
            var definition = ((RangedWeaponBehaviour)weapon.Behaviour).projectile;
            var item = (WeaponItem)weapon.CreateRuntimeItem();
            bool crit = Random.value < _stats.CritChance;
            float amount = _stats.Damage * definition.damageMultiplier * (crit ? _stats.CritMultiplier : 1f);
            int shot = ProjectileFlight.NextId();
            Vector3 origin = transform.position + Vector3.up * 1.3f;
            ShowPlayerProjectileRpc(shot, weapon.Id, origin, direction);
            ProjectileFlight.Launch(shot, definition, transform, true, origin, direction, true,
                (col, point, forward) => ProjectileDamage.Apply(col, point, forward, amount, crit, this, item),
                BroadcastProjectileImpact);
        }

        [ObserversRpc]
        private void ShowPlayerProjectileRpc(int shot, string weaponId, Vector3 origin, Vector3 direction)
        {
            if (IsServerStarted) return;
            var weapon = Resources.Load<WeaponDefinition>("Weapons/" + weaponId);
            if (weapon?.Behaviour is RangedWeaponBehaviour ranged)
                ProjectileFlight.Launch(shot, ranged.projectile, transform, true, origin, direction, false);
        }

        // Route impacts through each connected player's object, so arrows remain
        // authoritative even after the firing enemy has died and returned to its pool.
        public static void BroadcastProjectileImpact(int shot, Vector3 point, Vector3 direction, NetworkObject target, string path)
        {
            foreach (var stats in PlayerRegistry.All)
            {
                if (stats == null) continue;
                var combat = stats.GetComponent<PlayerCombat>();
                if (combat != null && combat.IsServerStarted && combat.IsSpawned && combat.Owner.IsActive)
                    combat.PlayerProjectileImpactTarget(combat.Owner, shot, point, direction, target, path);
            }
        }

        [TargetRpc]
        private void PlayerProjectileImpactTarget(NetworkConnection connection, int shot, Vector3 point, Vector3 direction, NetworkObject target, string path)
        {
            if (!IsServerStarted) ProjectileFlight.ReceiveImpact(shot, point, direction, target, path);
        }
    }
}
