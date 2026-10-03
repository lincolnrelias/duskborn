using Duskborn.Gameplay.Enchanting;
using Duskborn.Gameplay.Equipment;
using Duskborn.Gameplay.Crafting;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Duskborn.Gameplay.Player
{
    public partial class PlayerCombat
    {
        private static System.Collections.Generic.Dictionary<string, WeaponDefinition> _runeWeaponCatalog;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuneCatalog() => _runeWeaponCatalog = null;
        private readonly SyncVar<int> _equippedRune = new();
        private readonly SyncVar<string> _runeWeaponId = new();
        public WeaponEtching EquippedRune => WeaponEtching.Unpack(_equippedRune.Value);
        public string RuneWeaponId => _runeWeaponId.Value;

        public void PublishRuneEquipment(WeaponItem item)
        {
            if (!IsOwner || !IsClientStarted) return;
            SetRuneEquipmentRpc(item?.Id ?? "", item?.Etching.Packed ?? 0);
        }
        [ServerRpc]
        private void SetRuneEquipmentRpc(string id, int packed)
        {
            // Equipment selection follows the project's local inventory contract. Only
            // catalog weapons and bounded rune levels are accepted; damage stays server-side.
            if (string.IsNullOrEmpty(id)) { _runeWeaponId.Value = ""; _equippedRune.Value = 0; return; }
            if (id.Length > 128 || FindRuneWeapon(id) == null)
            { _runeWeaponId.Value = ""; _equippedRune.Value = 0; return; }
            var rune = WeaponEtching.Unpack(packed);
            if (packed != 0 && !rune.IsValid) return;
            _runeWeaponId.Value = id; _equippedRune.Value = rune.Packed;
        }
        public static WeaponDefinition FindRuneWeapon(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (_runeWeaponCatalog == null)
            {
                _runeWeaponCatalog = new();
                foreach (var definition in Resources.LoadAll<WeaponDefinition>("Weapons"))
                    if (!string.IsNullOrEmpty(definition.Id)) _runeWeaponCatalog[definition.Id] = definition;
                foreach (var recipe in Resources.LoadAll<CraftingRecipe>("Crafting"))
                    if (recipe.OutputItem is WeaponDefinition weapon && !string.IsNullOrEmpty(weapon.Id))
                        _runeWeaponCatalog[weapon.Id] = weapon;
            }
            return _runeWeaponCatalog.TryGetValue(id, out var result) ? result : null;
        }
        private WeaponEtching CurrentHitRune => _weaponHandler?.ActiveWeapon != null && IsOwner
            ? _weaponHandler.ActiveWeapon.Etching : EquippedRune;

        private static void AddProjectileRuneAura(Duskborn.Gameplay.Projectiles.ProjectileFlight flight, WeaponEtching rune)
        {
            if (flight == null || !rune.IsValid) return;
            var aura = flight.gameObject.AddComponent<Duskborn.Effects.RuneAura>();
            aura.Configure(rune.kind, rune.level, Duskborn.Effects.RuneAura.LocalBounds(flight.transform));
        }
    }
}
