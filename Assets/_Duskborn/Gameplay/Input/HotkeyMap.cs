using System;
using UnityEngine;

namespace Duskborn.Gameplay.Hotkeys
{
    [CreateAssetMenu(fileName = "HotkeyMap", menuName = "Duskborn/Hotkey Map")]
    public class HotkeyMap : ScriptableObject
    {
        public const string Skill1 = "Skill1";
        public const string Skill2 = "Skill2";
        public const string Skill3 = "Skill3";

        [Serializable]
        public struct Binding
        {
            public string  actionId;
            public KeyCode key;
        }

        [SerializeField] private Binding[] bindings =
        {
            new() { actionId = Skill1, key = KeyCode.Q },
            new() { actionId = Skill2, key = KeyCode.E },
            new() { actionId = Skill3, key = KeyCode.R },
        };

        public bool WasPressedThisFrame(string actionId)
        {
            foreach (var b in bindings)
                if (b.actionId == actionId) return UnityEngine.Input.GetKeyDown(b.key);
            return false;
        }

        public KeyCode GetKey(string actionId)
        {
            foreach (var b in bindings)
                if (b.actionId == actionId) return b.key;
            return KeyCode.None;
        }
    }
}
