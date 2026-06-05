#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Duskborn.Editor
{
    public sealed class ColliderLayerOverrideTool : EditorWindow
    {
        private GameObject _root;
        private bool _includeInactive = true;
        private bool _include3D = true;
        private bool _include2D = true;
        private LayerMask _includeLayers;
        private LayerMask _excludeLayers;

        [MenuItem("Duskborn/Tools/Collider Layer Override Tool")]
        private static void Open() => GetWindow<ColliderLayerOverrideTool>("Collider Layer Overrides");

        private void OnGUI()
        {
            EditorGUILayout.Space(4);

            _root = (GameObject)EditorGUILayout.ObjectField("Root Object", _root, typeof(GameObject), true);
            _includeInactive = EditorGUILayout.Toggle("Include Inactive", _includeInactive);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Collider Types", EditorStyles.boldLabel);
            _include3D = EditorGUILayout.Toggle("3D Colliders", _include3D);
            _include2D = EditorGUILayout.Toggle("2D Colliders", _include2D);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Layer Overrides", EditorStyles.boldLabel);
            _includeLayers = LayerMaskField(new GUIContent("Include Layers"), _includeLayers);
            _excludeLayers = LayerMaskField(new GUIContent("Exclude Layers"), _excludeLayers);

            if (_root != null)
            {
                int count3D = _include3D ? _root.GetComponentsInChildren<Collider>(_includeInactive).Length : 0;
                int count2D = _include2D ? _root.GetComponentsInChildren<Collider2D>(_includeInactive).Length : 0;
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox($"Found {count3D} 3D and {count2D} 2D collider(s).", MessageType.Info);
            }

            EditorGUILayout.Space(4);
            using (new EditorGUI.DisabledScope(_root == null || (!_include3D && !_include2D)))
            {
                if (GUILayout.Button("Apply Layer Overrides"))
                    Apply();
            }
        }

        private void Apply()
        {
            var cols3D = _include3D
                ? _root.GetComponentsInChildren<Collider>(_includeInactive)
                : new Collider[0];
            var cols2D = _include2D
                ? _root.GetComponentsInChildren<Collider2D>(_includeInactive)
                : new Collider2D[0];

            var all = new List<Object>(cols3D.Length + cols2D.Length);
            foreach (var c in cols3D) all.Add(c);
            foreach (var c in cols2D) all.Add(c);

            Undo.RecordObjects(all.ToArray(), "Set Collider Layer Overrides");

            bool isPrefab = PrefabUtility.IsPartOfPrefabInstance(_root);

            foreach (var c in cols3D)
            {
                c.includeLayers = _includeLayers;
                c.excludeLayers = _excludeLayers;
                EditorUtility.SetDirty(c);
                if (isPrefab) PrefabUtility.RecordPrefabInstancePropertyModifications(c);
            }

            foreach (var c in cols2D)
            {
                c.includeLayers = _includeLayers;
                c.excludeLayers = _excludeLayers;
                EditorUtility.SetDirty(c);
                if (isPrefab) PrefabUtility.RecordPrefabInstancePropertyModifications(c);
            }

            Debug.Log($"[ColliderLayerOverrideTool] Applied layer overrides to {cols3D.Length} 3D and {cols2D.Length} 2D collider(s) on '{_root.name}'.");
        }

        private static LayerMask LayerMaskField(GUIContent label, LayerMask mask)
        {
            var names = new List<string>();
            var indices = new List<int>();
            for (int i = 0; i < 32; i++)
            {
                string n = LayerMask.LayerToName(i);
                if (!string.IsNullOrEmpty(n)) { names.Add(n); indices.Add(i); }
            }

            int display = mask.value == -1 ? -1 : 0;
            if (mask.value != -1)
                for (int i = 0; i < indices.Count; i++)
                    if ((mask.value & (1 << indices[i])) != 0) display |= (1 << i);

            display = EditorGUILayout.MaskField(label, display, names.ToArray());

            if (display == -1) return -1;
            if (display == 0) return 0;

            int value = 0;
            for (int i = 0; i < indices.Count; i++)
                if ((display & (1 << i)) != 0) value |= (1 << indices[i]);

            return value;
        }
    }
}
#endif
