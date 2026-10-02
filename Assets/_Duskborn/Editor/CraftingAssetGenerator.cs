using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Duskborn.Gameplay;
using Duskborn.Gameplay.Equipment;
using Duskborn.Gameplay.Crafting;
using Duskborn.Gameplay.Loot;
using Duskborn.Gameplay.World;
using InventorySystem.Data;

[InitializeOnLoad]
public static class CraftingAssetGenerator
{
    private const string AutoGenKey = "Duskborn_CraftingEcosystem_V5";

    static CraftingAssetGenerator()
    {
        EditorApplication.delayCall += CheckAndAutoGenerate;
    }

    private static void CheckAndAutoGenerate()
    {
        if (!File.Exists(ConsumablesPath + "consumable_vitality_tonic.asset") || !EditorPrefs.GetBool(AutoGenKey, false))
        {
            EditorPrefs.SetBool(AutoGenKey, true);
            Debug.Log("[CraftingAssetGenerator] Running complete crafting ecosystem generation...");
            GenerateAssets();
        }
    }

    private const string MaterialsPath = "Assets/_Duskborn/ScriptableObjects/Resources/";
    private const string GearPath = "Assets/_Duskborn/ScriptableObjects/Gear/";
    private const string RecipesPath = "Assets/_Duskborn/Resources/Crafting/";
    private const string WeaponsPath = "Assets/_Duskborn/ScriptableObjects/Weapons/";
    private const string ConsumablesPath = "Assets/_Duskborn/ScriptableObjects/Consumables/";
    private const string EnemiesLootPath = "Assets/_Duskborn/ScriptableObjects/Enemies/";
    private const string WorldPropsPath = "Assets/_Duskborn/ScriptableObjects/World/Props/";
    private const string WorldPath = "Assets/_Duskborn/ScriptableObjects/World/";

    [MenuItem("Duskborn/Generate Complete Crafting Ecosystem")]
    public static void GenerateAssets()
    {
        EnsureDirectory(MaterialsPath);
        EnsureDirectory(GearPath);
        EnsureDirectory(RecipesPath);
        EnsureDirectory(WeaponsPath);
        EnsureDirectory(ConsumablesPath);
        EnsureDirectory(EnemiesLootPath);
        EnsureDirectory(WorldPropsPath);

        // ═════════════════════════════════════════════════════════════════════
        // 1. COMPLETE MATERIALS
        // ═════════════════════════════════════════════════════════════════════
        // T1 - Basic raw materials.
        var matWood = LoadOrCreateMaterial("material_wood", "Wood", "raw", 0);
        var matStone = LoadOrCreateMaterial("material_stone", "Stone", "raw", 0);
        var matFiber = LoadOrCreateMaterial("material_fiber", "Fiber", "raw", 0);

        // T2 - Intermediate resources and combat drops.
        var matIronOre = LoadOrCreateMaterial("material_iron", "Iron Ore", "raw", 1);
        var matLeather = LoadOrCreateMaterial("material_leather", "Raw Leather", "combat_drop", 1);
        var matBone = LoadOrCreateMaterial("material_bone", "Ancestral Bone", "combat_drop", 1);
        var matSap = LoadOrCreateMaterial("material_sap", "Seiva Pegajosa", "organic", 1);
        var matIronBar = LoadOrCreateMaterial("material_iron_bar", "Iron Bar", "refined", 1);
        var matTannedLeather = LoadOrCreateMaterial("material_tanned_leather", "Tanned Leather", "refined", 1);

        // T3 - Advanced and rare resources.
        var matArcaneCrystal = LoadOrCreateMaterial("material_arcane_crystal", "Arcane Crystal", "rare", 2);
        var matSteelPlate = LoadOrCreateMaterial("material_steel_plate", "Reinforced Steel Plate", "component", 2);
        var matCrystalPowder = LoadOrCreateMaterial("material_crystal_powder", "Purified Crystal Powder", "refined", 2);

        // T4 - Boss relic.
        var matThornbarkCore = LoadOrCreateMaterial("material_thornbark_core", "Thornheart Core", "boss", 4);

        // ═════════════════════════════════════════════════════════════════════
        // 2. CONSUMABLES & UTILITIES (Alchemical Cauldron)
        // ═════════════════════════════════════════════════════════════════════
        var potionIcon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Inventory/Textures/Items/Potion.png");

        var conVitalityTonic = CreateConsumable("consumable_vitality_tonic", "Vitality Tonic",
            "Instantly restores 70 health points.",
            potionIcon, ConsumableEffectType.InstantHeal, 70f, 0f, 5,
            "Instantly restores +70 HP.");

        var conSwiftnessElixir = CreateConsumable("consumable_swiftness_elixir", "Predator's Elixir",
            "Accelerates circulation, granting +30% movement speed for 20 seconds.",
            potionIcon, ConsumableEffectType.SpeedBuff, 0.30f, 20f, 5,
            "+30% Movement Speed for 20s.");

        var conFireOil = CreateConsumable("consumable_fire_oil", "Flaming Oil",
            "Infuses the weapon with burning essence, increasing overall damage by +25% for 40 seconds.",
            potionIcon, ConsumableEffectType.DamageBuff, 0.25f, 40f, 3,
            "+25% Attack Damage for 40s.");

        var conThornBomb = CreateConsumable("consumable_thorn_bomb", "Thorn Bomb",
            "Alchemical shrapnel bomb. Increases reflected thorns damage by +35% for 30 seconds.",
            potionIcon, ConsumableEffectType.ThornsBuff, 0.35f, 30f, 3,
            "+35% Reflected Thorns Damage for 30s.");

        // ═════════════════════════════════════════════════════════════════════
        // 3. EQUIPMENT WITH TRADE-OFFS AND QUIRKS
        // ═════════════════════════════════════════════════════════════════════
        // T1 - Primitive (Workbench).
        var gearFiberChest = CreateGear("gear_fiber_chest", "Fiber Armor", 4, new[] { (StatType.HP, 0.10f) });
        var gearFiberHelm = CreateGear("gear_fiber_helm", "Fiber Helm", 0, new[] { (StatType.HP, 0.05f) });
        var gearFiberLegs = CreateGear("gear_fiber_legs", "Fiber Trousers", 8, new[] { (StatType.HP, 0.05f) });
        var gearFiberBoots = CreateGear("gear_fiber_boots", "Fiber Boots", 9, new[] { (StatType.MoveSpeed, 0.05f) });

        // T2 - Specialization: Heavy Plate (Forge) vs Hunter Leather (Workbench).
        // Heavy Iron Plate: great protection, but a speed penalty.
        var gearHeavyIronChest = CreateGear("gear_heavy_iron_chest", "Heavy Iron Plate", 4,
            new[] { (StatType.HP, 0.35f), (StatType.DamageReduction, 0.15f), (StatType.MoveSpeed, -0.10f) });
        var gearHeavyIronHelm = CreateGear("gear_heavy_iron_helm", "Hammered Iron Helm", 0,
            new[] { (StatType.HP, 0.15f), (StatType.DamageReduction, 0.08f) });
        var gearHeavyBoots = CreateGear("gear_heavy_boots", "Heavy Steel Boots", 9,
            new[] { (StatType.DamageReduction, 0.08f), (StatType.MoveSpeed, -0.05f) });

        // Hunter Leather: high mobility and agility, but vulnerable to damage.
        var gearHunterLeatherChest = CreateGear("gear_hunter_leather_chest", "Hunter's Jerkin", 4,
            new[] { (StatType.MoveSpeed, 0.15f), (StatType.AttackSpeed, 0.10f), (StatType.CritChance, 0.08f), (StatType.DamageReduction, -0.05f) });

        // T3 - Reinforced (Forge) & Arcane (Arcane Table).
        var gearReinforcedChest = CreateGear("gear_reinforced_chest", "Reinforced Steel Armor", 4,
            new[] { (StatType.HP, 0.45f), (StatType.DamageReduction, 0.20f), (StatType.MoveSpeed, -0.08f) });

        // Arcane Robe: maximum offensive power, defensive fragility.
        var gearArcaneRobe = CreateGear("gear_arcane_robe", "Arcane Silk Robe", 4,
            new[] { (StatType.Damage, 0.25f), (StatType.CritChance, 0.15f), (StatType.DamageReduction, -0.08f) });

        var gearWindBoots = CreateGear("gear_wind_boots", "Gale Boots", 9,
            new[] { (StatType.MoveSpeed, 0.25f), (StatType.AttackSpeed, 0.10f), (StatType.HP, -0.10f) });

        var gearCrystalRing = CreateGear("gear_crystal_ring", "Pure Crystal Ring", 10,
            new[] { (StatType.CritChance, 0.15f), (StatType.Damage, 0.10f) });

        var gearBoneAmulet = CreateGear("gear_bone_amulet", "Claw Amulet", 1,
            new[] { (StatType.AttackSpeed, 0.20f), (StatType.MoveSpeed, 0.10f) });

        // T4 - Thornheart Relic (Arcane Table).
        var gearThornbarkPlate = CreateGear("gear_thornbark_plate", "Thornheart Cuirass", 4,
            new[] { (StatType.HP, 0.50f), (StatType.DamageReduction, 0.25f), (StatType.ThornsDamage, 0.25f), (StatType.MoveSpeed, -0.12f) });

        // ═════════════════════════════════════════════════════════════════════
        // 4. CRAFTABLE WEAPONS WITH BEHAVIOR & GATING
        // ═════════════════════════════════════════════════════════════════════
        var templateAxe = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/_Duskborn/ScriptableObjects/Weapons/Stone Axe/stone_axe.asset");
        var templatePickaxe = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/_Duskborn/ScriptableObjects/Weapons/Stone Pickaxe/stone_pickaxe.asset");
        var swordPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdPartyAssets/Kevin Iglesias/Melee Warrior Animations/Prefabs/Weapons/2HGreatsword.prefab");

        // T1
        var wpnWoodenSword = CreateWeapon("weapon_wooden_sword", "Wooden Sword",
            "Light training sword carved from solid wood.",
            templateAxe, swordPrefab,
            new[] { (StatType.Damage, 0.10f), (StatType.AttackSpeed, 0.15f) },
            null);

        // T2 - Iron Tools and Forged Weapons.
        var wpnIronAxe = CreateWeapon("weapon_iron_axe", "Iron Axe",
            "Iron-forged axe. Cuts trees with extreme ease (+400%) and strikes humanoids.",
            templateAxe, null,
            new[] { (StatType.Damage, 0.25f), (StatType.AttackSpeed, 0.10f), (StatType.WoodcuttingResourceBonus, 0.35f) },
            new[] { (TargetType.Tree, 4.0f), (TargetType.Humanoid, 1.0f) });

        var wpnIronPickaxe = CreateWeapon("weapon_iron_pickaxe", "Iron Pickaxe",
            "Tempered iron pickaxe. Pierces iron veins and shatters arcane crystals (+400%).",
            templatePickaxe, null,
            new[] { (StatType.Damage, 0.20f), (StatType.GatheringSpeed, 0.25f), (StatType.MiningResourceBonus, 0.35f) },
            new[] { (TargetType.MiningNode, 4.0f) });

        var wpnIronSword = CreateWeapon("weapon_iron_sword", "Iron Sword",
            "Fire-forged iron sword. Solid, balanced damage.",
            templateAxe, swordPrefab,
            new[] { (StatType.Damage, 0.35f), (StatType.CritChance, 0.10f) },
            null);

        // Bone Blade: light, fast, and focused on critical hits.
        var wpnBoneBlade = CreateWeapon("weapon_bone_blade", "Bone Blade",
            "Serrated blade carved from bestial femurs. Fast attacks and frequent critical hits.",
            templateAxe, swordPrefab,
            new[] { (StatType.Damage, 0.25f), (StatType.AttackSpeed, 0.25f), (StatType.CritChance, 0.20f) },
            null);

        // T3 - Heavy Axe (Trade-off: brutal damage, but slow).
        var wpnHeavyWaraxe = CreateWeapon("weapon_heavy_waraxe", "Heavy War Axe",
            "Colossal war axe. Massive, devastating damage against trees and humanoids, but slow to wield.",
            templateAxe, null,
            new[] { (StatType.Damage, 0.55f), (StatType.AttackSpeed, -0.20f), (StatType.WoodcuttingResourceBonus, 0.50f) },
            new[] { (TargetType.Tree, 5.0f), (TargetType.Humanoid, 2.0f) });

        // Bloodthirsty Blade: Berserker (damage and life steal, maximum HP penalty).
        var wpnBloodBlade = CreateWeapon("weapon_blood_blade", "Bloodthirsty Blade",
            "Ritual weapon forged from bones and crystal powder. Steals life with each strike, but drains the user's maximum vitality.",
            templateAxe, swordPrefab,
            new[] { (StatType.Damage, 0.40f), (StatType.Lifesteal, 0.15f), (StatType.HP, -0.15f) },
            null);

        var wpnReinforcedSword = CreateWeapon("weapon_reinforced_sword", "Reinforced Sword",
            "Laminated steel blade with arcane crystal channels. Grants consistent life steal.",
            templateAxe, swordPrefab,
            new[] { (StatType.Damage, 0.50f), (StatType.CritChance, 0.15f), (StatType.Lifesteal, 0.10f) },
            null);

        // T4 - Thornheart Blade.
        var wpnThornblade = CreateWeapon("weapon_thornblade", "Thornheart Blade",
            "Living weapon infused with Thornheart's corrupt power. Brutal strikes and massive vampiric regeneration.",
            templateAxe, swordPrefab,
            new[] { (StatType.Damage, 0.75f), (StatType.CritChance, 0.20f), (StatType.Lifesteal, 0.20f) },
            null);

        // ═════════════════════════════════════════════════════════════════════
        // 5. RECIPES WITH PROGRESSION, DEDICATED STATIONS, AND DISCOVERY
        // ═════════════════════════════════════════════════════════════════════
        // Station: WORKBENCH (T1 & Basic Refining).
        CreateOrUpdateRecipe("Recipe_StoneAxe", "Stone Axe", "Tools",
            CraftingTier.Primitive, CraftingStationType.Workbench, true, templateAxe,
            new[] { (matWood, 5), (matStone, 5) });

        CreateOrUpdateRecipe("Recipe_StonePickaxe", "Stone Pickaxe", "Tools",
            CraftingTier.Primitive, CraftingStationType.Workbench, true, templatePickaxe,
            new[] { (matWood, 5), (matStone, 5) });

        CreateOrUpdateRecipe("Recipe_WoodenSword", "Wooden Sword", "Weapons",
            CraftingTier.Primitive, CraftingStationType.Workbench, true, wpnWoodenSword,
            new[] { (matWood, 8), (matFiber, 3) });

        CreateOrUpdateRecipe("Recipe_FiberChest", "Fiber Armor", "Armor",
            CraftingTier.Primitive, CraftingStationType.Workbench, true, gearFiberChest,
            new[] { (matFiber, 10), (matWood, 5) });

        CreateOrUpdateRecipe("Recipe_FiberHelm", "Fiber Helm", "Armor",
            CraftingTier.Primitive, CraftingStationType.Workbench, true, gearFiberHelm,
            new[] { (matFiber, 6), (matWood, 3) });

        CreateOrUpdateRecipe("Recipe_FiberLegs", "Fiber Trousers", "Armor",
            CraftingTier.Primitive, CraftingStationType.Workbench, true, gearFiberLegs,
            new[] { (matFiber, 8), (matWood, 4) });

        CreateOrUpdateRecipe("Recipe_FiberBoots", "Fiber Boots", "Armor",
            CraftingTier.Primitive, CraftingStationType.Workbench, true, gearFiberBoots,
            new[] { (matFiber, 5), (matWood, 3) });

        CreateOrUpdateRecipe("Recipe_TannedLeather", "Tanned Leather", "Materials",
            CraftingTier.Iron, CraftingStationType.Workbench, false, matTannedLeather,
            new[] { (matLeather, 2), (matFiber, 2) });

        CreateOrUpdateRecipe("Recipe_HunterLeatherChest", "Hunter's Jerkin", "Armor",
            CraftingTier.Iron, CraftingStationType.Workbench, false, gearHunterLeatherChest,
            new[] { (matTannedLeather, 8), (matFiber, 4) });

        // Station: SMELTING FORGE (Metallurgy, Heavy Plate, Iron Weapons).
        CreateOrUpdateRecipe("Recipe_SmeltIronBar", "Smelt Iron Bar", "Materials",
            CraftingTier.Iron, CraftingStationType.Forge, false, matIronBar,
            new[] { (matIronOre, 2) }, new[] { (matWood, 1) });

        CreateOrUpdateRecipe("Recipe_SteelPlate", "Forge Steel Plate", "Materials",
            CraftingTier.Reinforced, CraftingStationType.Forge, false, matSteelPlate,
            new[] { (matIronBar, 2), (matStone, 1), (matBone, 1) }, new[] { (matWood, 1) });

        CreateOrUpdateRecipe("Recipe_IronAxe", "Iron Axe", "Tools",
            CraftingTier.Iron, CraftingStationType.Forge, false, wpnIronAxe,
            new[] { (matIronBar, 3), (matWood, 3) });

        CreateOrUpdateRecipe("Recipe_IronPickaxe", "Iron Pickaxe", "Tools",
            CraftingTier.Iron, CraftingStationType.Forge, false, wpnIronPickaxe,
            new[] { (matIronBar, 3), (matWood, 3) });

        CreateOrUpdateRecipe("Recipe_IronSword", "Iron Sword", "Weapons",
            CraftingTier.Iron, CraftingStationType.Forge, false, wpnIronSword,
            new[] { (matIronBar, 4), (matWood, 2), (matLeather, 2) });

        CreateOrUpdateRecipe("Recipe_HeavyIronChest", "Heavy Iron Plate", "Armor",
            CraftingTier.Iron, CraftingStationType.Forge, false, gearHeavyIronChest,
            new[] { (matIronBar, 6), (matLeather, 3) });

        CreateOrUpdateRecipe("Recipe_HeavyIronHelm", "Hammered Iron Helm", "Armor",
            CraftingTier.Iron, CraftingStationType.Forge, false, gearHeavyIronHelm,
            new[] { (matIronBar, 4), (matLeather, 2) });

        CreateOrUpdateRecipe("Recipe_HeavyBoots", "Heavy Steel Boots", "Armor",
            CraftingTier.Iron, CraftingStationType.Forge, false, gearHeavyBoots,
            new[] { (matIronBar, 3), (matLeather, 2) });

        CreateOrUpdateRecipe("Recipe_HeavyWaraxe", "Heavy War Axe", "Weapons",
            CraftingTier.Reinforced, CraftingStationType.Forge, false, wpnHeavyWaraxe,
            new[] { (matSteelPlate, 3), (matWood, 4) });

        CreateOrUpdateRecipe("Recipe_ReinforcedArmor", "Reinforced Steel Armor", "Armor",
            CraftingTier.Reinforced, CraftingStationType.Forge, false, gearReinforcedChest,
            new[] { (matSteelPlate, 4), (matTannedLeather, 3) });

        // Station: ALCHEMICAL CAULDRON (Tonics, Elixirs, Bombs, Crystal Refining).
        CreateOrUpdateRecipe("Recipe_VitalityTonic", "Vitality Tonic", "Consumables",
            CraftingTier.Iron, CraftingStationType.Cauldron, false, conVitalityTonic,
            new[] { (matFiber, 3), (matSap, 2), (matWood, 1) });

        CreateOrUpdateRecipe("Recipe_SwiftnessElixir", "Predator's Elixir", "Consumables",
            CraftingTier.Iron, CraftingStationType.Cauldron, false, conSwiftnessElixir,
            new[] { (matFiber, 2), (matBone, 2), (matSap, 1) });

        CreateOrUpdateRecipe("Recipe_FireOil", "Flaming Oil", "Consumables",
            CraftingTier.Iron, CraftingStationType.Cauldron, false, conFireOil,
            new[] { (matSap, 3), (matStone, 2), (matWood, 1) });

        CreateOrUpdateRecipe("Recipe_ThornBomb", "Thorn Bomb", "Consumables",
            CraftingTier.Iron, CraftingStationType.Cauldron, false, conThornBomb,
            new[] { (matBone, 4), (matStone, 3), (matSap, 2) });

        CreateOrUpdateRecipe("Recipe_GrindCrystalPowder", "Grind Crystal Powder", "Materials",
            CraftingTier.Reinforced, CraftingStationType.Cauldron, false, matCrystalPowder,
            new[] { (matArcaneCrystal, 1) });

        // Station: ARCANE TABLE (Jewelry, Arcane Clothing, Thornheart Relics).
        CreateOrUpdateRecipe("Recipe_BoneBlade", "Bone Blade", "Weapons",
            CraftingTier.Iron, CraftingStationType.ArcaneTable, false, wpnBoneBlade,
            new[] { (matBone, 6), (matLeather, 3), (matIronBar, 1) });

        CreateOrUpdateRecipe("Recipe_ArcaneRobe", "Arcane Silk Robe", "Armor",
            CraftingTier.Reinforced, CraftingStationType.ArcaneTable, false, gearArcaneRobe,
            new[] { (matCrystalPowder, 3), (matTannedLeather, 4) });

        CreateOrUpdateRecipe("Recipe_CrystalRing", "Pure Crystal Ring", "Accessories",
            CraftingTier.Reinforced, CraftingStationType.ArcaneTable, false, gearCrystalRing,
            new[] { (matArcaneCrystal, 2), (matIronBar, 2) });

        CreateOrUpdateRecipe("Recipe_BoneAmulet", "Claw Amulet", "Accessories",
            CraftingTier.Reinforced, CraftingStationType.ArcaneTable, false, gearBoneAmulet,
            new[] { (matBone, 4), (matArcaneCrystal, 1) });

        CreateOrUpdateRecipe("Recipe_WindBoots", "Gale Boots", "Armor",
            CraftingTier.Reinforced, CraftingStationType.ArcaneTable, false, gearWindBoots,
            new[] { (matCrystalPowder, 2), (matTannedLeather, 3) });

        CreateOrUpdateRecipe("Recipe_BloodBlade", "Bloodthirsty Blade", "Weapons",
            CraftingTier.Reinforced, CraftingStationType.ArcaneTable, false, wpnBloodBlade,
            new[] { (matBone, 5), (matArcaneCrystal, 2), (matLeather, 2) });

        CreateOrUpdateRecipe("Recipe_ReinforcedSword", "Reinforced Sword", "Weapons",
            CraftingTier.Reinforced, CraftingStationType.ArcaneTable, false, wpnReinforcedSword,
            new[] { (matSteelPlate, 3), (matArcaneCrystal, 2), (matLeather, 2) });

        CreateOrUpdateRecipe("Recipe_Thornblade", "Thornheart Blade", "Weapons",
            CraftingTier.Thornheart, CraftingStationType.ArcaneTable, false, wpnThornblade,
            new[] { (matThornbarkCore, 2), (matSteelPlate, 4), (matArcaneCrystal, 3) });

        CreateOrUpdateRecipe("Recipe_ThornbarkPlate", "Thornheart Cuirass", "Armor",
            CraftingTier.Thornheart, CraftingStationType.ArcaneTable, false, gearThornbarkPlate,
            new[] { (matThornbarkCore, 3), (matSteelPlate, 5), (matTannedLeather, 4) });

        // ═════════════════════════════════════════════════════════════════════
        // 6. LOOT TABLES & INTEGRATION
        // ═════════════════════════════════════════════════════════════════════
        UpdateSwarmerLootTable(matLeather, matSap);
        CreateDropTable(EnemiesLootPath + "brute_loot_table.asset",
            new[] { (matBone as ItemDefinitionBase, 0.70f, 0.15f, 1, 3) }, 5, 12);
        CreateDropTable(EnemiesLootPath + "elite_loot_table.asset",
            new[] { (matBone as ItemDefinitionBase, 0.90f, 0.10f, 2, 5), (matArcaneCrystal as ItemDefinitionBase, 0.30f, 0.15f, 1, 2) }, 15, 35);

        // ═════════════════════════════════════════════════════════════════════
        // 7. ARCANE PROP & WORLD RESOURCE GATING
        // ═════════════════════════════════════════════════════════════════════
        CreateCrystalProp();

        // Preserve processing semantics when regenerating the crafting catalog.
        foreach (var id in new[] { "Recipe_SmeltIronBar", "Recipe_SteelPlate", "Recipe_TannedLeather", "Recipe_GrindCrystalPowder" })
        {
            var processing = AssetDatabase.LoadAssetAtPath<CraftingRecipe>(RecipesPath + id + ".asset");
            if (processing == null) continue;
            var serialized = new SerializedObject(processing);
            serialized.FindProperty("processingSeconds").floatValue = 12f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Complete Duskborn Crafting Ecosystem Generated.");
    }

    private static void EnsureDirectory(string path)
    {
        if (!AssetDatabase.IsValidFolder(path.TrimEnd('/')))
        {
            string[] folders = path.TrimEnd('/').Split('/');
            string currentPath = folders[0];
            for (int i = 1; i < folders.Length; i++)
            {
                if (!AssetDatabase.IsValidFolder(currentPath + "/" + folders[i]))
                {
                    AssetDatabase.CreateFolder(currentPath, folders[i]);
                }
                currentPath += "/" + folders[i];
            }
        }
    }

    private static MaterialDefinition LoadOrCreateMaterial(string id, string displayName, string category, int rarity)
    {
        string path = MaterialsPath + id + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<MaterialDefinition>(path);
        if (existing != null) return existing;

        var asset = ScriptableObject.CreateInstance<MaterialDefinition>();
        AssetDatabase.CreateAsset(asset, path);

        var so = new SerializedObject(asset);
        so.FindProperty("id").stringValue = id;
        so.FindProperty("displayName").stringValue = displayName;
        so.FindProperty("description").stringValue = displayName;
        so.FindProperty("category").stringValue = category;
        so.FindProperty("rarity").enumValueIndex = rarity;
        so.ApplyModifiedPropertiesWithoutUndo();
        return asset;
    }

    private static ConsumableDefinition CreateConsumable(string id, string displayName, string desc, Texture2D icon,
        ConsumableEffectType effectType, float effectValue, float duration, int maxStack, string effectDesc)
    {
        string path = ConsumablesPath + id + ".asset";
        var asset = AssetDatabase.LoadAssetAtPath<ConsumableDefinition>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<ConsumableDefinition>();
            AssetDatabase.CreateAsset(asset, path);
        }

        var so = new SerializedObject(asset);
        so.FindProperty("id").stringValue = id;
        so.FindProperty("displayName").stringValue = displayName;
        so.FindProperty("description").stringValue = desc;
        if (icon != null) so.FindProperty("icon").objectReferenceValue = icon;
        so.FindProperty("effectType").enumValueIndex = (int)effectType;
        so.FindProperty("effectValue").floatValue = effectValue;
        so.FindProperty("duration").floatValue = duration;
        so.FindProperty("maxStack").intValue = maxStack;
        so.FindProperty("effectDescription").stringValue = effectDesc;
        so.ApplyModifiedPropertiesWithoutUndo();
        return asset;
    }

    private static GearDefinition CreateGear(string id, string displayName, int slot, (StatType type, float val)[] bonuses)
    {
        string path = GearPath + id + ".asset";
        var asset = AssetDatabase.LoadAssetAtPath<GearDefinition>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<GearDefinition>();
            AssetDatabase.CreateAsset(asset, path);
        }

        var so = new SerializedObject(asset);
        so.FindProperty("id").stringValue = id;
        so.FindProperty("displayName").stringValue = displayName;
        so.FindProperty("description").stringValue = displayName;
        so.FindProperty("slot").enumValueIndex = slot;

        var bonusesProp = so.FindProperty("bonuses");
        bonusesProp.arraySize = bonuses.Length;
        for (int i = 0; i < bonuses.Length; i++)
        {
            var elem = bonusesProp.GetArrayElementAtIndex(i);
            elem.FindPropertyRelative("Type").enumValueIndex = (int)bonuses[i].type;
            elem.FindPropertyRelative("Value").floatValue = bonuses[i].val;
            elem.FindPropertyRelative("Mode").enumValueIndex = 0;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        return asset;
    }

    private static WeaponDefinition CreateWeapon(
        string id, string displayName, string desc,
        WeaponDefinition template,
        GameObject customPrefab,
        (StatType type, float val)[] bonuses,
        (TargetType type, float bonus)[] typeModifiers)
    {
        string path = WeaponsPath + id + ".asset";
        var asset = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<WeaponDefinition>();
            AssetDatabase.CreateAsset(asset, path);
        }

        var so = new SerializedObject(asset);
        so.FindProperty("id").stringValue = id;
        so.FindProperty("displayName").stringValue = displayName;
        so.FindProperty("description").stringValue = desc;

        if (template != null)
        {
            var tempSo = new SerializedObject(template);
            so.FindProperty("icon").objectReferenceValue = tempSo.FindProperty("icon").objectReferenceValue;
            so.FindProperty("behaviour").objectReferenceValue = tempSo.FindProperty("behaviour").objectReferenceValue;
            so.FindProperty("audioProfile").objectReferenceValue = tempSo.FindProperty("audioProfile").objectReferenceValue;
            so.FindProperty("effectProfile").objectReferenceValue = tempSo.FindProperty("effectProfile").objectReferenceValue;

            var srcActions = tempSo.FindProperty("actions");
            var dstActions = so.FindProperty("actions");
            dstActions.arraySize = srcActions.arraySize;
            for (int i = 0; i < srcActions.arraySize; i++)
            {
                var sElem = srcActions.GetArrayElementAtIndex(i);
                var dElem = dstActions.GetArrayElementAtIndex(i);
                dElem.FindPropertyRelative("BaseSpeed").floatValue = sElem.FindPropertyRelative("BaseSpeed").floatValue;
                dElem.FindPropertyRelative("PreserveLocomotion").boolValue = sElem.FindPropertyRelative("PreserveLocomotion").boolValue;
                dElem.FindPropertyRelative("ComboChain").boolValue = sElem.FindPropertyRelative("ComboChain").boolValue;
                dElem.FindPropertyRelative("ComboResetTime").floatValue = sElem.FindPropertyRelative("ComboResetTime").floatValue;

                var sEntries = sElem.FindPropertyRelative("Entries");
                var dEntries = dElem.FindPropertyRelative("Entries");
                dEntries.arraySize = sEntries.arraySize;
                for (int j = 0; j < sEntries.arraySize; j++)
                {
                    var sEntry = sEntries.GetArrayElementAtIndex(j);
                    var dEntry = dEntries.GetArrayElementAtIndex(j);
                    dEntry.FindPropertyRelative("Clip").objectReferenceValue = sEntry.FindPropertyRelative("Clip").objectReferenceValue;
                    dEntry.FindPropertyRelative("DamageMultiplier").floatValue = sEntry.FindPropertyRelative("DamageMultiplier").floatValue;

                    var sEvts = sEntry.FindPropertyRelative("Events");
                    var dEvts = dEntry.FindPropertyRelative("Events");
                    dEvts.arraySize = sEvts.arraySize;
                    for (int k = 0; k < sEvts.arraySize; k++)
                    {
                        var sEvt = sEvts.GetArrayElementAtIndex(k);
                        var dEvt = dEvts.GetArrayElementAtIndex(k);
                        dEvt.FindPropertyRelative("Type").enumValueIndex = sEvt.FindPropertyRelative("Type").enumValueIndex;
                        dEvt.FindPropertyRelative("NormalizedTime").floatValue = sEvt.FindPropertyRelative("NormalizedTime").floatValue;
                    }
                }
            }

            var srcSkills = tempSo.FindProperty("skills");
            var dstSkills = so.FindProperty("skills");
            dstSkills.arraySize = srcSkills.arraySize;
            for (int i = 0; i < srcSkills.arraySize; i++)
                dstSkills.GetArrayElementAtIndex(i).objectReferenceValue = srcSkills.GetArrayElementAtIndex(i).objectReferenceValue;

            so.FindProperty("prefab").objectReferenceValue = customPrefab != null ? customPrefab : tempSo.FindProperty("prefab").objectReferenceValue;
        }

        var bonusesProp = so.FindProperty("bonuses");
        bonusesProp.arraySize = bonuses != null ? bonuses.Length : 0;
        if (bonuses != null)
        {
            for (int i = 0; i < bonuses.Length; i++)
            {
                var elem = bonusesProp.GetArrayElementAtIndex(i);
                elem.FindPropertyRelative("Type").enumValueIndex = (int)bonuses[i].type;
                elem.FindPropertyRelative("Value").floatValue = bonuses[i].val;
                elem.FindPropertyRelative("Mode").enumValueIndex = 0;
            }
        }

        var typeModsProp = so.FindProperty("typeModifiers");
        typeModsProp.arraySize = typeModifiers != null ? typeModifiers.Length : 0;
        if (typeModifiers != null)
        {
            for (int i = 0; i < typeModifiers.Length; i++)
            {
                var elem = typeModsProp.GetArrayElementAtIndex(i);
                elem.FindPropertyRelative("Type").intValue = (int)typeModifiers[i].type;
                elem.FindPropertyRelative("Bonus").floatValue = typeModifiers[i].bonus;
            }
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        return asset;
    }

    private static void CreateOrUpdateRecipe(string id, string recipeName, string category,
        CraftingTier tier, CraftingStationType station, bool isAlwaysDiscovered,
        ItemDefinitionBase outputItem, (MaterialDefinition mat, int amount)[] ingredients,
        (MaterialDefinition mat, int amount)[] fuel = null, int outputAmount = 1)
    {
        string path = RecipesPath + id + ".asset";
        var asset = AssetDatabase.LoadAssetAtPath<CraftingRecipe>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<CraftingRecipe>();
            AssetDatabase.CreateAsset(asset, path);
        }

        var so = new SerializedObject(asset);
        so.FindProperty("recipeId").stringValue = id;
        so.FindProperty("recipeName").stringValue = recipeName;
        so.FindProperty("description").stringValue = recipeName;
        so.FindProperty("category").stringValue = category;
        so.FindProperty("tier").enumValueIndex = (int)tier;
        so.FindProperty("requiredStation").enumValueIndex = (int)station;
        so.FindProperty("isAlwaysDiscovered").boolValue = isAlwaysDiscovered;
        so.FindProperty("outputItem").objectReferenceValue = outputItem;
        so.FindProperty("outputAmount").intValue = outputAmount;

        var ingProp = so.FindProperty("ingredients");
        ingProp.arraySize = ingredients.Length;
        for (int i = 0; i < ingredients.Length; i++)
        {
            var elem = ingProp.GetArrayElementAtIndex(i);
            elem.FindPropertyRelative("material").objectReferenceValue = ingredients[i].mat;
            elem.FindPropertyRelative("amount").intValue = ingredients[i].amount;
        }

        var fuelProp = so.FindProperty("fuelIngredients");
        fuelProp.arraySize = fuel?.Length ?? 0;
        for (int i = 0; i < fuelProp.arraySize; i++)
        {
            var elem = fuelProp.GetArrayElementAtIndex(i);
            elem.FindPropertyRelative("material").objectReferenceValue = fuel[i].mat;
            elem.FindPropertyRelative("amount").intValue = fuel[i].amount;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void UpdateSwarmerLootTable(MaterialDefinition leather, MaterialDefinition sap)
    {
        string path = EnemiesLootPath + "swarmer_loot_table.asset";
        var table = AssetDatabase.LoadAssetAtPath<DropLootTable>(path);
        if (table != null)
        {
            var list = new List<DropEntry>(table.entries ?? System.Array.Empty<DropEntry>());
            bool hasLeather = false;
            bool hasSap = false;
            foreach (var e in list)
            {
                if (e != null && e.itemDefinition == leather) hasLeather = true;
                if (e != null && e.itemDefinition == sap) hasSap = true;
            }

            if (!hasLeather && leather != null)
            {
                list.Add(new DropEntry { itemDefinition = leather, baseChance = 0.70f, scalingBonus = 0.15f, minAmount = 1, maxAmount = 2 });
            }
            if (!hasSap && sap != null)
            {
                list.Add(new DropEntry { itemDefinition = sap, baseChance = 0.40f, scalingBonus = 0.10f, minAmount = 1, maxAmount = 2 });
            }

            table.entries = list.ToArray();
            EditorUtility.SetDirty(table);
        }
    }

    private static DropLootTable CreateDropTable(string path, (ItemDefinitionBase item, float baseChance, float scalingBonus, int min, int max)[] entries, int goldMin, int goldMax)
    {
        var table = AssetDatabase.LoadAssetAtPath<DropLootTable>(path);
        if (table == null)
        {
            table = ScriptableObject.CreateInstance<DropLootTable>();
            AssetDatabase.CreateAsset(table, path);
        }
        table.goldMin = goldMin;
        table.goldMax = goldMax;
        table.rarityScaleRate = 0.5f;

        var list = new List<DropEntry>();
        foreach (var e in entries)
        {
            if (e.item != null)
            {
                list.Add(new DropEntry
                {
                    itemDefinition = e.item,
                    baseChance = e.baseChance,
                    scalingBonus = e.scalingBonus,
                    minAmount = e.min,
                    maxAmount = e.max
                });
            }
        }
        table.entries = list.ToArray();
        EditorUtility.SetDirty(table);
        return table;
    }

    private static void CreateCrystalProp()
    {
        string path = WorldPropsPath + "Prop_ArcaneCrystal.asset";
        var prop = AssetDatabase.LoadAssetAtPath<PropDefinition>(path);
        if (prop == null)
        {
            prop = ScriptableObject.CreateInstance<PropDefinition>();
            AssetDatabase.CreateAsset(prop, path);
        }

        var ironProp = AssetDatabase.LoadAssetAtPath<PropDefinition>(WorldPropsPath + "Prop_Iron.asset");

        prop.propName = "Arcane Crystal";
        prop.prefab = ironProp != null ? ironProp.prefab : null;
        prop.minRadialDistance = 50f;
        prop.maxRadialDistance = 0f;
        prop.minPerChunk = 0;
        prop.maxPerChunk = 2;
        prop.clusterSettings = new ResourceClusterSettings
        {
            enableClustering = true,
            clustersPerChunk = 1,
            nodesPerCluster = new Vector2Int(1, 3),
            clusterRadius = 4f,
            intraClusterSpacing = 2.5f,
            interClusterSpacing = 6f
        };
        prop.solidRadius = 1.3f;
        prop.minHeight = 1f;
        prop.maxHeight = 120f;
        prop.maxSlopeAngle = 40f;
        prop.scaleRange = new Vector2(0.9f, 1.25f);
        prop.heightScaleMultiplier = new Vector2(1f, 1f);
        prop.randomYRotation = true;
        prop.alignToNormal = true;
        prop.exclusionRadius = 3.5f;

        EditorUtility.SetDirty(prop);

        var propsConfig = AssetDatabase.LoadAssetAtPath<WorldPropsConfig>(WorldPath + "PropsConfig_Default.asset");
        if (propsConfig != null)
        {
            bool hasProp = false;
            if (propsConfig.extraProps != null)
            {
                foreach (var p in propsConfig.extraProps)
                    if (p == prop) { hasProp = true; break; }
            }
            if (!hasProp)
            {
                var list = new List<PropDefinition>(propsConfig.extraProps ?? System.Array.Empty<PropDefinition>());
                list.Add(prop);
                propsConfig.extraProps = list.ToArray();
                EditorUtility.SetDirty(propsConfig);
            }
        }
    }
}
