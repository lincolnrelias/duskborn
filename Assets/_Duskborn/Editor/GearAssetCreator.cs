#if UNITY_EDITOR
using System.Text;
using Duskborn.Gameplay.Equipment;
using UnityEditor;
using UnityEngine;

namespace Duskborn.Editor
{
    public static class GearAssetCreator
    {
        private const string OutputFolder = "Assets/_Duskborn/ScriptableObjects/Gear";

        [MenuItem("Duskborn/Create Test Gear Assets")]
        public static void CreateAll()
        {
            EnsureFolder();

            CreateGear("gear_iron_helm",      "Iron Helm",      EquipmentSlot.Head,
                new[] { Bonus(StatType.HP, 0.05f) });

            CreateGear("gear_leather_chest",  "Leather Chest",  EquipmentSlot.Chest,
                new[] { Bonus(StatType.HP, 0.08f), Bonus(StatType.DamageReduction, 0.05f) });

            CreateGear("gear_worn_boots",     "Worn Boots",     EquipmentSlot.Feet,
                new[] { Bonus(StatType.MoveSpeed, 0.10f) });

            CreateGear("gear_copper_ring",    "Copper Ring",    EquipmentSlot.Ring1,
                new[] { Bonus(StatType.CritChance, 0.05f) });

            CreateGear("gear_bone_necklace",  "Bone Necklace",  EquipmentSlot.Neck,
                new[] { Bonus(StatType.Damage, 0.07f) });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[GearAssetCreator] Test gear assets created at " + OutputFolder);
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Duskborn/ScriptableObjects"))
                AssetDatabase.CreateFolder("Assets/_Duskborn", "ScriptableObjects");
            if (!AssetDatabase.IsValidFolder(OutputFolder))
                AssetDatabase.CreateFolder("Assets/_Duskborn/ScriptableObjects", "Gear");
        }

        private static void CreateGear(string fileName, string displayName,
                                        EquipmentSlot slot, StatBonus[] bonuses)
        {
            string path = $"{OutputFolder}/{fileName}.asset";
            if (AssetDatabase.LoadAssetAtPath<GearDefinition>(path) != null)
            {
                Debug.Log($"[GearAssetCreator] '{path}' already exists — skipped.");
                return;
            }

            var asset = ScriptableObject.CreateInstance<GearDefinition>();
            var so    = new SerializedObject(asset);

            so.FindProperty("id").stringValue          = fileName;
            so.FindProperty("displayName").stringValue = displayName;
            so.FindProperty("description").stringValue = BuildDescription(bonuses);
            so.FindProperty("slot").enumValueIndex     = (int)slot;

            var bonusesProp = so.FindProperty("bonuses");
            bonusesProp.arraySize = bonuses.Length;
            for (int i = 0; i < bonuses.Length; i++)
            {
                var elem = bonusesProp.GetArrayElementAtIndex(i);
                elem.FindPropertyRelative("Type").enumValueIndex = (int)bonuses[i].Type;
                elem.FindPropertyRelative("Value").floatValue    = bonuses[i].Value;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(asset, path);
        }

        private static StatBonus Bonus(StatType type, float value) =>
            new StatBonus { Type = type, Value = value };

        private static string BuildDescription(StatBonus[] bonuses)
        {
            var sb = new StringBuilder();
            foreach (var b in bonuses)
            {
                string sign = b.Value >= 0f ? "+" : string.Empty;
                sb.Append($"{b.Type}: {sign}{b.Value * 100f:F0}%  ");
            }
            return sb.ToString().TrimEnd();
        }
    }
}
#endif
