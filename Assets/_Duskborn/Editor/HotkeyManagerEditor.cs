#if UNITY_EDITOR
using System.Collections.Generic;
using Duskborn.Gameplay.Hotkeys;
using UnityEditor;
using UnityEngine;

namespace Duskborn.Editor
{
    /// <summary>
    /// Shows one fixed row per action from HotkeyManager.DefaultBindings (label + key dropdown).
    /// Missing actions are added automatically, stale ids removed — no manual array editing.
    /// </summary>
    [CustomEditor(typeof(HotkeyManager))]
    public sealed class HotkeyManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var bindings = serializedObject.FindProperty("bindings");
            SyncActions(bindings);

            EditorGUILayout.LabelField("Bindings", EditorStyles.boldLabel);
            for (int i = 0; i < bindings.arraySize; i++)
            {
                var el  = bindings.GetArrayElementAtIndex(i);
                var key = el.FindPropertyRelative("key");
                string id = el.FindPropertyRelative("actionId").stringValue;
                key.intValue = (int)(KeyCode)EditorGUILayout.EnumPopup(id, (KeyCode)key.intValue);
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Reset to Defaults"))
                for (int i = 0; i < bindings.arraySize; i++)
                    bindings.GetArrayElementAtIndex(i).FindPropertyRelative("key").intValue =
                        (int)HotkeyManager.DefaultBindings[i].key;

            serializedObject.ApplyModifiedProperties();
        }

        // Rewrites the serialized array to match DefaultBindings order, preserving any
        // keys the user already remapped. No-op when already in sync.
        private static void SyncActions(SerializedProperty bindings)
        {
            var defaults = HotkeyManager.DefaultBindings;

            bool inSync = bindings.arraySize == defaults.Length;
            for (int i = 0; inSync && i < defaults.Length; i++)
                inSync = bindings.GetArrayElementAtIndex(i)
                    .FindPropertyRelative("actionId").stringValue == defaults[i].actionId;
            if (inSync) return;

            var existing = new Dictionary<string, KeyCode>();
            for (int i = 0; i < bindings.arraySize; i++)
            {
                var el = bindings.GetArrayElementAtIndex(i);
                existing[el.FindPropertyRelative("actionId").stringValue] =
                    (KeyCode)el.FindPropertyRelative("key").intValue;
            }

            bindings.arraySize = defaults.Length;
            for (int i = 0; i < defaults.Length; i++)
            {
                var el = bindings.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("actionId").stringValue = defaults[i].actionId;
                el.FindPropertyRelative("key").intValue = (int)(existing.TryGetValue(
                    defaults[i].actionId, out var k) ? k : defaults[i].key);
            }
        }
    }
}
#endif
