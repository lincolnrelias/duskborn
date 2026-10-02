#if UNITY_EDITOR
using Duskborn.Gameplay.Equipment;
using UnityEditor;
using UnityEngine;

namespace Duskborn.Editor
{
    // Hides the inherited "Drop Prefab" field from ItemDefinitionBase — weapons use "Prefab" instead.
    [CustomEditor(typeof(WeaponDefinition))]
    public sealed class WeaponDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "dropPrefab");
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(8);
            if (GUILayout.Button("Open in Item Fitting Studio", GUILayout.Height(28)))
            {
                ItemFittingStudioWindow.OpenWithWeapon((WeaponDefinition)target);
            }
        }
    }
}
#endif
