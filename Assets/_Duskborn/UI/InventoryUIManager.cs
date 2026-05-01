using System.Collections.Generic;
using InventorySystem.Bootstrap;
using InventorySystem.Core;
using InventorySystem.Data;
using Duskborn.Gameplay.Loot;
using Duskborn.Gameplay.Player;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Duskborn.UI
{
    public class InventoryUIManager : MonoBehaviour
    {
        [SerializeField] private InventoryInstaller installer;
        [SerializeField] private GameObject inventoryRoot;
        [SerializeField] private MaterialDefinition[] resourceDefinitions;

        private PlayerInventory   _playerInventory;
        private ResourceInventory _resourceInventory;

        private readonly Dictionary<string, MaterialDefinition> _defById      = new();
        private readonly Dictionary<string, int>                _resourceSlot = new();

        private bool InstallerReady => installer != null && installer.Inventory != null;

        private void Awake()
        {
            if (resourceDefinitions != null)
            {
                foreach (var def in resourceDefinitions)
                {
                    if (def == null || string.IsNullOrWhiteSpace(def.Id)) continue;
                    _defById[def.Id] = def;
                }
            }

            Debug.Log($"[InventoryUI] Awake — installer={(installer != null ? installer.name : "NULL")}, InstallerReady={InstallerReady}, defs={_defById.Count}");
        }

        private void Start()
        {
            if (installer != null)
            {
                foreach (var def in _defById.Values)
                {
                    if (def.Icon != null)
                        installer.RegisterIcon(def.Id, def.Icon);
                }
            }

            Debug.Log($"[InventoryUI] Start — InstallerReady={InstallerReady}");

            if (inventoryRoot != null)
                inventoryRoot.SetActive(false);
        }

        private void Update()
        {
            TryCacheLocalPlayer();

            if (IsInventoryKeyDown())
                Toggle();
        }

        private void OnDestroy()
        {
            if (_playerInventory != null)
                _playerInventory.ItemAdded -= OnItemAdded;
            if (_resourceInventory != null)
                _resourceInventory.ResourceChanged -= OnResourceChanged;
        }

        private void TryCacheLocalPlayer()
        {
            if (_playerInventory != null && _resourceInventory != null) return;

            foreach (var combat in FindObjectsByType<PlayerCombat>(FindObjectsSortMode.None))
            {
                if (!combat.IsOwner) continue;

                if (_playerInventory == null)
                {
                    var inv = combat.GetComponent<PlayerInventory>();
                    if (inv != null)
                    {
                        _playerInventory = inv;
                        _playerInventory.ItemAdded += OnItemAdded;
                        Debug.Log($"[InventoryUI] Subscribed to PlayerInventory on {combat.name}");
                    }
                }

                if (_resourceInventory == null)
                {
                    var res = combat.GetComponent<ResourceInventory>();
                    if (res != null)
                    {
                        _resourceInventory = res;
                        _resourceInventory.ResourceChanged += OnResourceChanged;
                        Debug.Log($"[InventoryUI] Subscribed to ResourceInventory on {combat.name}");
                    }
                    else
                    {
                        Debug.LogWarning($"[InventoryUI] PlayerCombat '{combat.name}' has no ResourceInventory component");
                    }
                }

                break;
            }
        }

        private void OnItemAdded(ItemDefinition def)
        {
            if (!InstallerReady) return;
            installer.Inventory.TryAddItem(new DuskbornInventoryItem(def), out _);
        }

        private void OnResourceChanged(string resourceId, int newTotal)
        {
            Debug.Log($"[InventoryUI] OnResourceChanged: '{resourceId}' x{newTotal} | InstallerReady={InstallerReady}");

            if (!InstallerReady) return;

            if (_resourceSlot.TryGetValue(resourceId, out int oldSlot))
            {
                installer.Inventory.RemoveItem(oldSlot);
                _resourceSlot.Remove(resourceId);
            }

            if (newTotal <= 0) return;

            _defById.TryGetValue(resourceId, out var def);
            string displayName = def != null ? def.DisplayName : resourceId;
            string iconId      = def?.Icon != null ? def.Icon.name : string.Empty;

            var item = new MaterialItem(resourceId, displayName, $"x{newTotal}", iconId, "resource");
            bool placed = installer.Inventory.TryAddItem(item, out int newSlot);
            Debug.Log($"[InventoryUI] TryAddItem '{resourceId}': placed={placed}, slot={newSlot}, totalSlots={installer.Service.Grid.SlotCount}");
            if (placed)
                _resourceSlot[resourceId] = newSlot;
        }

        private void Toggle()
        {
            if (inventoryRoot == null) return;
            bool willShow = !inventoryRoot.activeSelf;
            inventoryRoot.SetActive(willShow);
            Debug.Log($"[InventoryUI] Toggle show={willShow} InstallerReady={InstallerReady} resInv={(_resourceInventory != null ? "set" : "NULL")}");
            if (willShow)
                SyncResources();
        }

        private void SyncResources()
        {
            if (!InstallerReady || _resourceInventory == null)
            {
                Debug.LogWarning($"[InventoryUI] SyncResources bailed — InstallerReady={InstallerReady}, resInv={(_resourceInventory != null ? "set" : "NULL")}");
                return;
            }

            Debug.Log($"[InventoryUI] SyncResources — {_resourceInventory.Counts.Count} resource types");
            foreach (var kv in _resourceSlot)
                installer.Inventory.RemoveItem(kv.Value);
            _resourceSlot.Clear();

            foreach (var kv in _resourceInventory.Counts)
                if (kv.Value > 0)
                    OnResourceChanged(kv.Key, kv.Value);
        }

        private static bool IsInventoryKeyDown()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.iKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.I);
#endif
        }
    }
}
