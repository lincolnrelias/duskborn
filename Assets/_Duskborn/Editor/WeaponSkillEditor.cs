#if UNITY_EDITOR
using Duskborn.Gameplay.Equipment;
using UnityEditor;

namespace Duskborn.Editor
{
    [CustomEditor(typeof(WeaponSkill), true)]
    public sealed class WeaponSkillEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
