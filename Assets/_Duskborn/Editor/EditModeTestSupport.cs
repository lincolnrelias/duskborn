using System;
using System.Reflection;
using Duskborn.UI;
using UnityEngine;

namespace Duskborn.Editor
{
    internal static class EditModeTestSupport
    {
        // Ordinary MonoBehaviours do not receive Awake in the static edit-mode runner.
        internal static T AddInitialized<T>(GameObject owner) where T : MonoBehaviour
        {
            var component = owner.AddComponent<T>();
            if (!Application.isPlaying)
                typeof(T).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(component, null);
            return component;
        }

        internal static InventoryUIManager AddInventory(GameObject owner)
        {
            var manager = AddInitialized<InventoryUIManager>(owner);
            var root = new GameObject("InventoryFrame", typeof(RectTransform));
            root.transform.SetParent(owner.transform, false);
            root.SetActive(false);
            typeof(InventoryUIManager).GetField("inventoryRoot", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(manager, root);
            return manager;
        }

        internal static void AssertCursorRequested(Duskborn.Gameplay.Player.PlayerCameraController camera, bool locked)
        {
            var field = typeof(Duskborn.Gameplay.Player.PlayerCameraController).GetField("_isCursorLocked", BindingFlags.Instance | BindingFlags.NonPublic);
            if ((bool)field.GetValue(camera) != locked)
                throw new Exception($"Expected cursor lock request: {locked}.");
            // A batch-mode Editor has no game window and cannot apply an OS cursor lock.
            if (Application.isPlaying && (Cursor.lockState != (locked ? CursorLockMode.Locked : CursorLockMode.None) || Cursor.visible == locked))
                throw new Exception("Native cursor state did not match the lock request.");
        }
    }
}
