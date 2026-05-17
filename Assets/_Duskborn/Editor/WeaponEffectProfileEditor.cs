#if UNITY_EDITOR
using Duskborn.Effects;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Duskborn.Editor
{
    [CustomEditor(typeof(WeaponEffectProfile))]
    public sealed class WeaponEffectProfileEditor : UnityEditor.Editor
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
            var profile  = (WeaponEffectProfile)target;
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
                entry.FindPropertyRelative("tag").stringValue    = tag;
                entry.FindPropertyRelative("prefab").objectReferenceValue = null;
                added++;
            }

            serializedObject.ApplyModifiedProperties();

            if (added > 0)
                Debug.Log($"[WeaponEffectProfile] Added {added} tag entr{(added == 1 ? "y" : "ies")} to '{profile.name}'.");
            else
                Debug.Log($"[WeaponEffectProfile] All tags already present in '{profile.name}'.");
        }
    }
}
#endif
