using InventorySystem.UI;
using UnityEditor;
using UnityEngine;

namespace InventorySystem.Editor
{
    [CustomEditor(typeof(InventoryGridLayoutController))]
    public sealed class InventoryGridLayoutControllerEditor : UnityEditor.Editor
    {
        private SerializedProperty _gridLayoutGroupProperty;
        private SerializedProperty _slotTemplateProperty;
        private SerializedProperty _columnsProperty;
        private SerializedProperty _rowsProperty;
        private SerializedProperty _spacingProperty;
        private SerializedProperty _holdThresholdSecondsProperty;

        private void OnEnable()
        {
            _gridLayoutGroupProperty = serializedObject.FindProperty("gridLayoutGroup");
            _slotTemplateProperty = serializedObject.FindProperty("slotTemplate");
            _columnsProperty = serializedObject.FindProperty("columns");
            _rowsProperty = serializedObject.FindProperty("rows");
            _spacingProperty = serializedObject.FindProperty("spacing");
            _holdThresholdSecondsProperty = serializedObject.FindProperty("holdThresholdSeconds");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_gridLayoutGroupProperty);
            EditorGUILayout.PropertyField(_slotTemplateProperty);
            EditorGUILayout.PropertyField(_columnsProperty);
            EditorGUILayout.PropertyField(_rowsProperty);
            EditorGUILayout.PropertyField(_spacingProperty);

            EditorGUILayout.Space(4f);
            if (GUILayout.Button("Populate Slots"))
            {
                foreach (var targetObject in targets)
                {
                    if (targetObject is not InventoryGridLayoutController controller)
                    {
                        continue;
                    }

                    Undo.RecordObject(controller, "Populate Slots");
                    controller.PopulateSlots();
                    EditorUtility.SetDirty(controller);
                }
            }

            EditorGUILayout.PropertyField(
                _holdThresholdSecondsProperty,
                new GUIContent("Hold Threshold (s)"));
            serializedObject.ApplyModifiedProperties();
        }
    }
}
