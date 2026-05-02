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

        private ResourceInventory _resourceInventory;
        private PlayerInteractor  _playerInteractor;
        private bool              _dropEventSubscribed;

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
            if (_resourceInventory != null)
                _resourceInventory.ResourceChanged -= OnResourceChanged;
            if (_dropEventSubscribed && installer != null)
                installer.OnItemDroppedOutside -= OnItemDroppedOutside;
        }

        private void TryCacheLocalPlayer()
        {
            if (_resourceInventory != null && _playerInteractor != null) return;

            foreach (var combat in FindObjectsByType<PlayerCombat>(FindObjectsSortMode.None))
            {
                if (!combat.IsOwner) continue;

                if (_resourceInventory == null)
                {
                    var res = combat.GetComponent<ResourceInventory>();
                    if (res != null)
                    {
                        _resourceInventory = res;
                        _resourceInventory.ResourceChanged += OnResourceChanged;
                    }
                    else
                    {
                        Debug.LogWarning($"[InventoryUI] No ResourceInventory on {combat.name}");
                    }
                }

                if (_playerInteractor == null)
                {
                    _playerInteractor = combat.GetComponent<PlayerInteractor>();
                    if (_playerInteractor != null && !_dropEventSubscribed && installer != null)
                    {
                        installer.OnItemDroppedOutside += OnItemDroppedOutside;
                        _dropEventSubscribed = true;
                    }
                }

                break;
            }
        }

        private void OnItemDroppedOutside(IInventoryItem item)
        {
            if (item is not MaterialItem mat || _playerInteractor == null || _resourceInventory == null) return;
            int count = _resourceInventory.GetCount(mat.Id);
            if (count <= 0) return;
            _resourceInventory.TrySpend(mat.Id, count);
            _playerInteractor.DropResource(mat.Id, count);
        }

        private void OnResourceChanged(string resourceId, int newTotal)
        {
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

            var newItem = new MaterialItem(resourceId, displayName, string.Empty, iconId, "resource", newTotal);
            if (installer.Inventory.TryAddItem(newItem, out int newSlot))
                _resourceSlot[resourceId] = newSlot;
        }

        private void Toggle()
        {
            if (inventoryRoot == null) return;
            bool willShow = !inventoryRoot.activeSelf;
            inventoryRoot.SetActive(willShow);
            if (willShow)
                SyncResources();
        }

        private void SyncResources()
        {
            if (!InstallerReady || _resourceInventory == null) return;

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
