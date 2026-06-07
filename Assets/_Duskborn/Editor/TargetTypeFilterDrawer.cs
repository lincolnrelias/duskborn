#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Duskborn.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Duskborn.Editor
{
    // Draws a TargetType flags field as a MaskField restricted to the bits in AllowedMask —
    // same bit-remapping technique as ColliderLayerOverrideTool.LayerMaskField, scoped per-component
    // so e.g. enemies only ever see Humanoid/Beast and resource nodes only ever see Tree/MiningNode.
    [CustomPropertyDrawer(typeof(TargetTypeFilterAttribute))]
    public sealed class TargetTypeFilterDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.Enum)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            int allowedMask = (int)((TargetTypeFilterAttribute)attribute).AllowedMask;

            var names = new List<string>();
            var bits  = new List<int>();
            foreach (TargetType value in Enum.GetValues(typeof(TargetType)))
            {
                int bit = (int)value;
                if (bit == 0 || (allowedMask & bit) == 0) continue;
                names.Add(value.ToString());
                bits.Add(bit);
            }

            int display = 0;
            for (int i = 0; i < bits.Count; i++)
                if ((property.intValue & bits[i]) != 0) display |= (1 << i);

            EditorGUI.BeginProperty(position, label, property);
            int newDisplay = EditorGUI.MaskField(position, label, display, names.ToArray());
            if (newDisplay != display)
            {
                int value = 0;
                for (int i = 0; i < bits.Count; i++)
                    if ((newDisplay & (1 << i)) != 0) value |= bits[i];
                property.intValue = value;
            }
            EditorGUI.EndProperty();
        }
    }
}
#endif
