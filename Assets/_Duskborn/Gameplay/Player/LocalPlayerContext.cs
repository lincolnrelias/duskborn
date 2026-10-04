using System;
using Duskborn.Gameplay.Equipment;
using Duskborn.Gameplay.Loot;
using UnityEngine;

namespace Duskborn.Gameplay.Player
{
    /// <summary>
    /// Central access point for the local player's components.
    /// Eliminates expensive per-frame scene searches (FindObjectsByType/FindAnyObjectByType).
    /// </summary>
    public static class LocalPlayerContext
    {
        public static PlayerController Controller { get; private set; }
        public static PlayerStats Stats { get; private set; }
        public static PlayerCombat Combat { get; private set; }
        public static PlayerEquipmentContainer Equipment { get; private set; }
        public static PlayerBuffContainer Buffs { get; private set; }
        public static PlayerWeaponHandler WeaponHandler { get; private set; }
        public static ResourceInventory Resources { get; private set; }

        public static bool HasLocalPlayer => Controller != null;

        public static event Action OnLocalPlayerRegistered;
        public static event Action OnLocalPlayerUnregistered;

        public static void Register(PlayerController controller)
        {
            if (controller == null) return;
            Controller    = controller;
            Stats         = controller.GetComponent<PlayerStats>();
            Combat        = controller.GetComponent<PlayerCombat>();
            Equipment     = controller.GetComponent<PlayerEquipmentContainer>();
            Buffs         = controller.GetComponent<PlayerBuffContainer>();
            WeaponHandler = controller.GetComponent<PlayerWeaponHandler>();
            Resources     = controller.GetComponent<ResourceInventory>();

            if (Application.isPlaying)
            {
                Duskborn.UI.InGameMenuController.EnsureInstance();
                Duskborn.UI.WorldMapUI.Ensure();
            }

            OnLocalPlayerRegistered?.Invoke();
        }

        public static void Unregister(PlayerController controller)
        {
            if (Controller != controller) return;
            Controller    = null;
            Stats         = null;
            Combat        = null;
            Equipment     = null;
            Buffs         = null;
            WeaponHandler = null;
            Resources     = null;

            OnLocalPlayerUnregistered?.Invoke();
        }
    }
}
