#if UNITY_EDITOR
using Duskborn.Audio;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Duskborn.Editor
{
    [CustomEditor(typeof(WeaponAudioProfile))]
    public sealed class WeaponAudioProfileEditor : UnityEditor.Editor
    {
        private static readonly System.Collections.Generic.HashSet<string> BuiltInTags = new()
        {
            "Untagged", "Respawn", "Finish", "EditorOnly",
            "MainCamera", "Player", "GameController",
        };

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            if (GUILayout.Button("Generate Entries for All Tags"))
                GenerateMissingEntries();
        }

        private void GenerateMissingEntries()
        {
            var profile  = (WeaponAudioProfile)target;
            var surfaces = serializedObject.FindProperty("surfaces");

            var existing = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < surfaces.arraySize; i++)
                existing.Add(surfaces.GetArrayElementAtIndex(i).FindPropertyRelative("tag").stringValue);

            int added = 0;
            foreach (string tag in InternalEditorUtility.tags)
            {
                if (BuiltInTags.Contains(tag)) continue;
                if (existing.Contains(tag)) continue;
                surfaces.arraySize++;
                var entry = surfaces.GetArrayElementAtIndex(surfaces.arraySize - 1);
                entry.FindPropertyRelative("tag").stringValue   = tag;
                entry.FindPropertyRelative("clips").arraySize   = 0;
                added++;
            }

            serializedObject.ApplyModifiedProperties();

            if (added > 0)
                Debug.Log($"[WeaponAudioProfile] Added {added} tag entr{(added == 1 ? "y" : "ies")} to '{profile.name}'.");
            else
                Debug.Log($"[WeaponAudioProfile] All tags already present in '{profile.name}'.");
        }
    }
}
#endif
