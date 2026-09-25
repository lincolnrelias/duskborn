using System;
using System.Collections.Generic;
using UnityEngine;
using Duskborn.Core;

namespace Duskborn.Gameplay.Hotkeys
{
    /// <summary>
    /// Scene singleton. All remappable key presses (excluding WASD and mouse buttons)
    /// fire through here. Systems call Register/Unregister with stable delegate references.
    /// </summary>
    public class HotkeyManager : MonoBehaviour
    {
        public static HotkeyManager Instance { get; private set; }

        public const string Skill1   = "Skill1";
        public const string Skill2   = "Skill2";
        public const string Skill3   = "Skill3";
        public const string Interact = "Interact";
        public const string Build = "Build";
        public const string BuildRotate = "BuildRotate";
        public const string Dodge    = "Dodge";

        [Serializable]
        public struct Binding
        {
            public string  actionId;
            public KeyCode key;
        }

        // Canonical action list + default keys. The custom editor syncs scene components
        // against this, so new actions only need a const + an entry here.
        public static readonly Binding[] DefaultBindings =
        {
            new() { actionId = Build, key = KeyCode.B },
            new() { actionId = BuildRotate, key = KeyCode.R },
            new() { actionId = Skill1,   key = KeyCode.Q },
            new() { actionId = Skill2,   key = KeyCode.E },
            new() { actionId = Skill3,   key = KeyCode.R },
            new() { actionId = Interact, key = KeyCode.F },
            new() { actionId = Dodge,    key = KeyCode.LeftAlt },
        };

        [SerializeField] private Binding[] bindings = (Binding[])DefaultBindings.Clone();

        private readonly Dictionary<string, Action> _handlers = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            EnsureDefaultBindings();
        }

        private void EnsureDefaultBindings()
        {
            var dict = new Dictionary<string, KeyCode>();
            if (bindings != null)
            {
                foreach (var b in bindings)
                {
                    // If Dodge was previously saved as Space, override it to LeftAlt so it doesn't conflict with Jump
                    if (b.actionId == Dodge && b.key == KeyCode.Space)
                        dict[b.actionId] = KeyCode.LeftAlt;
                    else
                        dict[b.actionId] = b.key;
                }
            }

            var merged = new List<Binding>();
            foreach (var def in DefaultBindings)
            {
                KeyCode key = dict.TryGetValue(def.actionId, out var existingKey) ? existingKey : def.key;
                merged.Add(new Binding { actionId = def.actionId, key = key });
            }
            bindings = merged.ToArray();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Register(string actionId, Action handler)
        {
            if (_handlers.TryGetValue(actionId, out var existing))
                _handlers[actionId] = existing + handler;
            else
                _handlers[actionId] = handler;
        }

        public void Unregister(string actionId, Action handler)
        {
            if (_handlers.TryGetValue(actionId, out var existing))
                _handlers[actionId] = existing - handler;
        }

        public KeyCode GetKey(string actionId)
        {
            foreach (var b in bindings)
                if (b.actionId == actionId) return b.key;
            return KeyCode.None;
        }

        private void Update()
        {
            foreach (var b in bindings)
            {
                if (UnityEngine.Input.GetKeyDown(b.key) &&
                    _handlers.TryGetValue(b.actionId, out var handler))
                    handler?.Invoke();
            }
        }
    }
}
