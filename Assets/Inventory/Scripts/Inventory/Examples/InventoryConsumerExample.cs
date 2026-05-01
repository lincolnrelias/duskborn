using InventorySystem.Bootstrap;
using InventorySystem.Core;
using UnityEngine;

namespace InventorySystem.Examples
{
    /// <summary>
    /// Minimal host-side consumer that depends only on IInventory.
    /// Attach to any scene object and assign an InventoryInstaller.
    /// </summary>
    public sealed class InventoryConsumerExample : MonoBehaviour
    {
        [SerializeField] private InventoryInstaller installer;

        private IInventory _inventory;

        private void Awake()
        {
            if (installer == null)
            {
                Debug.LogWarning("InventoryConsumerExample: installer is not assigned.");
                enabled = false;
                return;
            }

            _inventory = installer.Inventory;
            if (_inventory == null)
            {
                Debug.LogWarning("InventoryConsumerExample: installer did not provide inventory.");
                enabled = false;
                return;
            }

            _inventory.ItemAdded += OnItemAdded;
            _inventory.ItemRemoved += OnItemRemoved;
        }

        private void OnDestroy()
        {
            if (_inventory == null)
            {
                return;
            }

            _inventory.ItemAdded -= OnItemAdded;
            _inventory.ItemRemoved -= OnItemRemoved;
        }

        [ContextMenu("Example/Add Debug Material")]
        public void AddDebugMaterial()
        {
            if (_inventory == null)
            {
                return;
            }

            var item = new MaterialItem(
                "example_wood",
                "Example Wood",
                "Material added from InventoryConsumerExample",
                string.Empty,
                "example");

            _inventory.TryAddItem(item, out _);
        }

        private static void OnItemAdded(ItemAddedEvent evt)
        {
            Debug.Log($"[InventoryConsumerExample] Added {evt.Item.DisplayName} in slot {evt.SlotIndex}.");
        }

        private static void OnItemRemoved(ItemRemovedEvent evt)
        {
            Debug.Log($"[InventoryConsumerExample] Removed {evt.Item.DisplayName} from slot {evt.SlotIndex}.");
        }
    }
}
