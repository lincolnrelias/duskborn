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
        // 1. MATERIAIS COMPLETOS
        // ═════════════════════════════════════════════════════════════════════
        // T1 - Matérias-primas básicas
        var matWood = LoadOrCreateMaterial("material_wood", "Madeira", "raw", 0);
        var matStone = LoadOrCreateMaterial("material_stone", "Pedra", "raw", 0);
        var matFiber = LoadOrCreateMaterial("material_fiber", "Fibra", "raw", 0);

        // T2 - Recursos intermediários e drops de combate
        var matIronOre = LoadOrCreateMaterial("material_iron", "Minério de Ferro", "raw", 1);
        var matLeather = LoadOrCreateMaterial("material_leather", "Couro Cru", "combat_drop", 1);
        var matBone = LoadOrCreateMaterial("material_bone", "Osso Ancestral", "combat_drop", 1);
        var matSap = LoadOrCreateMaterial("material_sap", "Seiva Pegajosa", "organic", 1);
        var matIronBar = LoadOrCreateMaterial("material_iron_bar", "Barra de Ferro", "refined", 1);
        var matTannedLeather = LoadOrCreateMaterial("material_tanned_leather", "Couro Curtido", "refined", 1);

        // T3 - Recursos avançados e raros
        var matArcaneCrystal = LoadOrCreateMaterial("material_arcane_crystal", "Cristal Arcano", "rare", 2);
        var matSteelPlate = LoadOrCreateMaterial("material_steel_plate", "Placa de Aço Reforçado", "component", 2);
        var matCrystalPowder = LoadOrCreateMaterial("material_crystal_powder", "Pó de Cristal Purificado", "refined", 2);

        // T4 - Relíquia de Chefe
        var matThornbarkCore = LoadOrCreateMaterial("material_thornbark_core", "Núcleo do Espinheiro", "boss", 4);

        // ═════════════════════════════════════════════════════════════════════
        // 2. CONSUMÍVEIS & UTILITÁRIOS (Caldeirão Alquímico)
        // ═════════════════════════════════════════════════════════════════════
        var potionIcon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Inventory/Textures/Items/Potion.png");

        var conVitalityTonic = CreateConsumable("consumable_vitality_tonic", "Tônico de Vitalidade",
            "Restaura instantaneamente 70 pontos de vida.",
            potionIcon, ConsumableEffectType.InstantHeal, 70f, 0f, 5,
            "Restaura +70 HP instantaneamente.");

        var conSwiftnessElixir = CreateConsumable("consumable_swiftness_elixir", "Elixir da Rapina",
            "Acelera a circulação garantindo +30% de velocidade de movimento por 20 segundos.",
            potionIcon, ConsumableEffectType.SpeedBuff, 0.30f, 20f, 5,
            "+30% Velocidade de Movimento por 20s.");

        var conFireOil = CreateConsumable("consumable_fire_oil", "Óleo Flamejante",
            "Infunde a arma com essência ardente, aumentando o dano geral em +25% por 40 segundos.",
            potionIcon, ConsumableEffectType.DamageBuff, 0.25f, 40f, 3,
            "+25% Dano de Ataque por 40s.");

        var conThornBomb = CreateConsumable("consumable_thorn_bomb", "Bomba de Espinhos",
            "Bomba alquímica de estilhaços. Aumenta a reflexão de espinhos em +35% por 30 segundos.",
            potionIcon, ConsumableEffectType.ThornsBuff, 0.35f, 30f, 3,
            "+35% Dano de Espinhos refletido por 30s.");

        // ═════════════════════════════════════════════════════════════════════
        // 3. EQUIPAMENTOS COM TRADE-OFFS E QUIRKS
        // ═════════════════════════════════════════════════════════════════════
        // T1 - Primitivo (Bancada)
        var gearFiberChest = CreateGear("gear_fiber_chest", "Armadura de Fibra", 4, new[] { (StatType.HP, 0.10f) });
        var gearFiberHelm = CreateGear("gear_fiber_helm", "Elmo de Fibra", 0, new[] { (StatType.HP, 0.05f) });
        var gearFiberLegs = CreateGear("gear_fiber_legs", "Calças de Fibra", 8, new[] { (StatType.HP, 0.05f) });
        var gearFiberBoots = CreateGear("gear_fiber_boots", "Botas de Fibra", 9, new[] { (StatType.MoveSpeed, 0.05f) });

        // T2 - Especialização: Placas Pesadas (Forja) vs Couro de Caçador (Bancada)
        // Placa Pesada de Ferro: Grande proteção, mas penalidade de velocidade
        var gearHeavyIronChest = CreateGear("gear_heavy_iron_chest", "Placa Pesada de Ferro", 4,
            new[] { (StatType.HP, 0.35f), (StatType.DamageReduction, 0.15f), (StatType.MoveSpeed, -0.10f) });
        var gearHeavyIronHelm = CreateGear("gear_heavy_iron_helm", "Elmo de Ferro Batido", 0,
            new[] { (StatType.HP, 0.15f), (StatType.DamageReduction, 0.08f) });
        var gearHeavyBoots = CreateGear("gear_heavy_boots", "Botas de Aço Pesado", 9,
            new[] { (StatType.DamageReduction, 0.08f), (StatType.MoveSpeed, -0.05f) });

        // Couro de Caçador: Alta mobilidade e agilidade, mas vulnerabilidade a dano
        var gearHunterLeatherChest = CreateGear("gear_hunter_leather_chest", "Gibão do Caçador", 4,
            new[] { (StatType.MoveSpeed, 0.15f), (StatType.AttackSpeed, 0.10f), (StatType.CritChance, 0.08f), (StatType.DamageReduction, -0.05f) });

        // T3 - Reforçado (Forja) & Arcano (Mesa Arcana)
        var gearReinforcedChest = CreateGear("gear_reinforced_chest", "Armadura de Aço Reforçado", 4,
            new[] { (StatType.HP, 0.45f), (StatType.DamageReduction, 0.20f), (StatType.MoveSpeed, -0.08f) });

        // Veste Arcana: Potência ofensiva máxima, fragilidade defensiva
        var gearArcaneRobe = CreateGear("gear_arcane_robe", "Veste de Seda Arcana", 4,
            new[] { (StatType.Damage, 0.25f), (StatType.CritChance, 0.15f), (StatType.DamageReduction, -0.08f) });

        var gearWindBoots = CreateGear("gear_wind_boots", "Botas do Vendaval", 9,
            new[] { (StatType.MoveSpeed, 0.25f), (StatType.AttackSpeed, 0.10f), (StatType.HP, -0.10f) });

        var gearCrystalRing = CreateGear("gear_crystal_ring", "Anel de Cristal Puro", 10,
            new[] { (StatType.CritChance, 0.15f), (StatType.Damage, 0.10f) });

        var gearBoneAmulet = CreateGear("gear_bone_amulet", "Amuleto de Garras", 1,
            new[] { (StatType.AttackSpeed, 0.20f), (StatType.MoveSpeed, 0.10f) });

        // T4 - Relíquia do Espinheiro (Mesa Arcana)
        var gearThornbarkPlate = CreateGear("gear_thornbark_plate", "Couraça do Espinheiro", 4,
            new[] { (StatType.HP, 0.50f), (StatType.DamageReduction, 0.25f), (StatType.ThornsDamage, 0.25f), (StatType.MoveSpeed, -0.12f) });

        // ═════════════════════════════════════════════════════════════════════
        // 4. ARMAS FABRICÁVEIS COM COMPORTAMENTO & GATING
        // ═════════════════════════════════════════════════════════════════════
        var templateAxe = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/_Duskborn/ScriptableObjects/Weapons/Stone Axe/stone_axe.asset");
        var templatePickaxe = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/_Duskborn/ScriptableObjects/Weapons/Stone Pickaxe/stone_pickaxe.asset");
        var swordPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdPartyAssets/Kevin Iglesias/Melee Warrior Animations/Prefabs/Weapons/2HGreatsword.prefab");

        // T1
        var wpnWoodenSword = CreateWeapon("weapon_wooden_sword", "Espada de Madeira",
            "Espada leve de treino entalhada em madeira maciça.",
            templateAxe, swordPrefab,
            new[] { (StatType.Damage, 0.10f), (StatType.AttackSpeed, 0.15f) },
            null);

        // T2 - Ferramentas de Ferro e Armas Forjadas
        var wpnIronAxe = CreateWeapon("weapon_iron_axe", "Machado de Ferro",
            "Machado forjado em ferro. Corta árvores com extrema facilidade (+400%) e golpeia humanoides.",
            templateAxe, null,
            new[] { (StatType.Damage, 0.25f), (StatType.AttackSpeed, 0.10f), (StatType.WoodcuttingResourceBonus, 0.35f) },
            new[] { (TargetType.Tree, 4.0f), (TargetType.Humanoid, 1.0f) });

        var wpnIronPickaxe = CreateWeapon("weapon_iron_pickaxe", "Picareta de Ferro",
            "Picareta de ferro temperado. Capaz de perfurar veios de ferro e estilhaçar cristais arcanos (+400%).",
            templatePickaxe, null,
            new[] { (StatType.Damage, 0.20f), (StatType.GatheringSpeed, 0.25f), (StatType.MiningResourceBonus, 0.35f) },
            new[] { (TargetType.MiningNode, 4.0f) });

        var wpnIronSword = CreateWeapon("weapon_iron_sword", "Espada de Ferro",
            "Espada de ferro forjada na brasa. Dano sólido e balanceado.",
            templateAxe, swordPrefab,
            new[] { (StatType.Damage, 0.35f), (StatType.CritChance, 0.10f) },
            null);

        // Lâmina de Osso: Leve, veloz e focada em críticos
        var wpnBoneBlade = CreateWeapon("weapon_bone_blade", "Lâmina de Osso",
            "Lâmina serrilhada esculpida a partir de fêmures bestiais. Ataques rápidos e críticos frequentes.",
            templateAxe, swordPrefab,
            new[] { (StatType.Damage, 0.25f), (StatType.AttackSpeed, 0.25f), (StatType.CritChance, 0.20f) },
            null);

        // T3 - Machado Pesado (Trade-off: Dano brutal, mas lento)
        var wpnHeavyWaraxe = CreateWeapon("weapon_heavy_waraxe", "Machado de Guerra Pesado",
            "Machado de guerra colossal. Dano maciço e devastador contra árvores e humanoides, porém lento de manusear.",
            templateAxe, null,
            new[] { (StatType.Damage, 0.55f), (StatType.AttackSpeed, -0.20f), (StatType.WoodcuttingResourceBonus, 0.50f) },
            new[] { (TargetType.Tree, 5.0f), (TargetType.Humanoid, 2.0f) });

        // Lâmina Sedenta: Berserker (Dano e Roubo de Vida, penalidade em HP máximo)
        var wpnBloodBlade = CreateWeapon("weapon_blood_blade", "Lâmina Sedenta de Sangue",
            "Arma ritualística forjada com ossos e pó de cristal. Rouba vida a cada golpe, porém drena a vitalidade máxima do usuário.",
            templateAxe, swordPrefab,
            new[] { (StatType.Damage, 0.40f), (StatType.Lifesteal, 0.15f), (StatType.HP, -0.15f) },
            null);

        var wpnReinforcedSword = CreateWeapon("weapon_reinforced_sword", "Espada Reforçada",
            "Lâmina de aço laminado com canais de cristal arcano. Concede roubo de vida consistente.",
            templateAxe, swordPrefab,
            new[] { (StatType.Damage, 0.50f), (StatType.CritChance, 0.15f), (StatType.Lifesteal, 0.10f) },
            null);

        // T4 - Lâmina do Espinheiro
        var wpnThornblade = CreateWeapon("weapon_thornblade", "Lâmina do Espinheiro",
            "Arma viva infundida com o poder corrupto do Espinheiro. Golpes brutais e regeneração vampírica massiva.",
            templateAxe, swordPrefab,
            new[] { (StatType.Damage, 0.75f), (StatType.CritChance, 0.20f), (StatType.Lifesteal, 0.20f) },
            null);

        // ═════════════════════════════════════════════════════════════════════
        // 5. RECEITAS COM PROGRESSÃO, ESTAÇÃO DEDICADA E DESCOBERTA
        // ═════════════════════════════════════════════════════════════════════
        // Estação: BANCADA DE TRABALHO (T1 & Refino Básico)
        CreateOrUpdateRecipe("Recipe_StoneAxe", "Machado de Pedra", "Ferramentas",
            CraftingTier.Primitivo, CraftingStationType.Bancada, true, templateAxe,
            new[] { (matWood, 5), (matStone, 5) });

        CreateOrUpdateRecipe("Recipe_StonePickaxe", "Picareta de Pedra", "Ferramentas",
            CraftingTier.Primitivo, CraftingStationType.Bancada, true, templatePickaxe,
            new[] { (matWood, 5), (matStone, 5) });

        CreateOrUpdateRecipe("Recipe_WoodenSword", "Espada de Madeira", "Armas",
            CraftingTier.Primitivo, CraftingStationType.Bancada, true, wpnWoodenSword,
            new[] { (matWood, 8), (matFiber, 3) });

        CreateOrUpdateRecipe("Recipe_FiberChest", "Armadura de Fibra", "Armadura",
            CraftingTier.Primitivo, CraftingStationType.Bancada, true, gearFiberChest,
            new[] { (matFiber, 10), (matWood, 5) });

        CreateOrUpdateRecipe("Recipe_FiberHelm", "Elmo de Fibra", "Armadura",
            CraftingTier.Primitivo, CraftingStationType.Bancada, true, gearFiberHelm,
            new[] { (matFiber, 6), (matWood, 3) });

        CreateOrUpdateRecipe("Recipe_FiberLegs", "Calças de Fibra", "Armadura",
            CraftingTier.Primitivo, CraftingStationType.Bancada, true, gearFiberLegs,
            new[] { (matFiber, 8), (matWood, 4) });

        CreateOrUpdateRecipe("Recipe_FiberBoots", "Botas de Fibra", "Armadura",
            CraftingTier.Primitivo, CraftingStationType.Bancada, true, gearFiberBoots,
            new[] { (matFiber, 5), (matWood, 3) });

        CreateOrUpdateRecipe("Recipe_TannedLeather", "Couro Curtido", "Materiais",
            CraftingTier.Ferro, CraftingStationType.Bancada, false, matTannedLeather,
            new[] { (matLeather, 2), (matFiber, 2) });

        CreateOrUpdateRecipe("Recipe_HunterLeatherChest", "Gibão do Caçador", "Armadura",
            CraftingTier.Ferro, CraftingStationType.Bancada, false, gearHunterLeatherChest,
            new[] { (matTannedLeather, 8), (matFiber, 4) });

        // Estação: FORJA DE FUNDIÇÃO (Metalurgia, Placas Pesadas, Armas de Ferro)
        CreateOrUpdateRecipe("Recipe_SmeltIronBar", "Fundir Barra de Ferro", "Materiais",
            CraftingTier.Ferro, CraftingStationType.Forja, false, matIronBar,
            new[] { (matIronOre, 2) }, new[] { (matWood, 1) });

        CreateOrUpdateRecipe("Recipe_SteelPlate", "Forjar Placa de Aço", "Materiais",
            CraftingTier.Reforcado, CraftingStationType.Forja, false, matSteelPlate,
            new[] { (matIronBar, 2), (matStone, 1), (matBone, 1) }, new[] { (matWood, 1) });

        CreateOrUpdateRecipe("Recipe_IronAxe", "Machado de Ferro", "Ferramentas",
            CraftingTier.Ferro, CraftingStationType.Forja, false, wpnIronAxe,
            new[] { (matIronBar, 3), (matWood, 3) });

        CreateOrUpdateRecipe("Recipe_IronPickaxe", "Picareta de Ferro", "Ferramentas",
            CraftingTier.Ferro, CraftingStationType.Forja, false, wpnIronPickaxe,
            new[] { (matIronBar, 3), (matWood, 3) });

        CreateOrUpdateRecipe("Recipe_IronSword", "Espada de Ferro", "Armas",
            CraftingTier.Ferro, CraftingStationType.Forja, false, wpnIronSword,
            new[] { (matIronBar, 4), (matWood, 2), (matLeather, 2) });

        CreateOrUpdateRecipe("Recipe_HeavyIronChest", "Placa Pesada de Ferro", "Armadura",
            CraftingTier.Ferro, CraftingStationType.Forja, false, gearHeavyIronChest,
            new[] { (matIronBar, 6), (matLeather, 3) });

        CreateOrUpdateRecipe("Recipe_HeavyIronHelm", "Elmo de Ferro Batido", "Armadura",
            CraftingTier.Ferro, CraftingStationType.Forja, false, gearHeavyIronHelm,
            new[] { (matIronBar, 4), (matLeather, 2) });

        CreateOrUpdateRecipe("Recipe_HeavyBoots", "Botas de Aço Pesado", "Armadura",
            CraftingTier.Ferro, CraftingStationType.Forja, false, gearHeavyBoots,
            new[] { (matIronBar, 3), (matLeather, 2) });

        CreateOrUpdateRecipe("Recipe_HeavyWaraxe", "Machado de Guerra Pesado", "Armas",
            CraftingTier.Reforcado, CraftingStationType.Forja, false, wpnHeavyWaraxe,
            new[] { (matSteelPlate, 3), (matWood, 4) });

        CreateOrUpdateRecipe("Recipe_ReinforcedArmor", "Armadura de Aço Reforçado", "Armadura",
            CraftingTier.Reforcado, CraftingStationType.Forja, false, gearReinforcedChest,
            new[] { (matSteelPlate, 4), (matTannedLeather, 3) });

        // Estação: CALDEIRÃO ALQUÍMICO (Tônicos, Elixires, Bombas, Refino de Cristal)
        CreateOrUpdateRecipe("Recipe_VitalityTonic", "Tônico de Vitalidade", "Consumíveis",
            CraftingTier.Ferro, CraftingStationType.Caldeirao, false, conVitalityTonic,
            new[] { (matFiber, 3), (matSap, 2), (matWood, 1) });

        CreateOrUpdateRecipe("Recipe_SwiftnessElixir", "Elixir da Rapina", "Consumíveis",
            CraftingTier.Ferro, CraftingStationType.Caldeirao, false, conSwiftnessElixir,
            new[] { (matFiber, 2), (matBone, 2), (matSap, 1) });

        CreateOrUpdateRecipe("Recipe_FireOil", "Óleo Flamejante", "Consumíveis",
            CraftingTier.Ferro, CraftingStationType.Caldeirao, false, conFireOil,
            new[] { (matSap, 3), (matStone, 2), (matWood, 1) });

        CreateOrUpdateRecipe("Recipe_ThornBomb", "Bomba de Espinhos", "Consumíveis",
            CraftingTier.Ferro, CraftingStationType.Caldeirao, false, conThornBomb,
            new[] { (matBone, 4), (matStone, 3), (matSap, 2) });

        CreateOrUpdateRecipe("Recipe_GrindCrystalPowder", "Moer Pó de Cristal", "Materiais",
            CraftingTier.Reforcado, CraftingStationType.Caldeirao, false, matCrystalPowder,
            new[] { (matArcaneCrystal, 1) });

        // Estação: MESA ARCANA (Joalheria, Roupas Arcanas, Relíquias do Espinheiro)
        CreateOrUpdateRecipe("Recipe_BoneBlade", "Lâmina de Osso", "Armas",
            CraftingTier.Ferro, CraftingStationType.MesaArcana, false, wpnBoneBlade,
            new[] { (matBone, 6), (matLeather, 3), (matIronBar, 1) });

        CreateOrUpdateRecipe("Recipe_ArcaneRobe", "Veste de Seda Arcana", "Armadura",
            CraftingTier.Reforcado, CraftingStationType.MesaArcana, false, gearArcaneRobe,
            new[] { (matCrystalPowder, 3), (matTannedLeather, 4) });

        CreateOrUpdateRecipe("Recipe_CrystalRing", "Anel de Cristal Puro", "Acessórios",
            CraftingTier.Reforcado, CraftingStationType.MesaArcana, false, gearCrystalRing,
            new[] { (matArcaneCrystal, 2), (matIronBar, 2) });

        CreateOrUpdateRecipe("Recipe_BoneAmulet", "Amuleto de Garras", "Acessórios",
            CraftingTier.Reforcado, CraftingStationType.MesaArcana, false, gearBoneAmulet,
            new[] { (matBone, 4), (matArcaneCrystal, 1) });

        CreateOrUpdateRecipe("Recipe_WindBoots", "Botas do Vendaval", "Armadura",
            CraftingTier.Reforcado, CraftingStationType.MesaArcana, false, gearWindBoots,
            new[] { (matCrystalPowder, 2), (matTannedLeather, 3) });

        CreateOrUpdateRecipe("Recipe_BloodBlade", "Lâmina Sedenta de Sangue", "Armas",
            CraftingTier.Reforcado, CraftingStationType.MesaArcana, false, wpnBloodBlade,
            new[] { (matBone, 5), (matArcaneCrystal, 2), (matLeather, 2) });

        CreateOrUpdateRecipe("Recipe_ReinforcedSword", "Espada Reforçada", "Armas",
            CraftingTier.Reforcado, CraftingStationType.MesaArcana, false, wpnReinforcedSword,
            new[] { (matSteelPlate, 3), (matArcaneCrystal, 2), (matLeather, 2) });

        CreateOrUpdateRecipe("Recipe_Thornblade", "Lâmina do Espinheiro", "Armas",
            CraftingTier.Espinheiro, CraftingStationType.MesaArcana, false, wpnThornblade,
            new[] { (matThornbarkCore, 2), (matSteelPlate, 4), (matArcaneCrystal, 3) });

        CreateOrUpdateRecipe("Recipe_ThornbarkPlate", "Couraça do Espinheiro", "Armadura",
            CraftingTier.Espinheiro, CraftingStationType.MesaArcana, false, gearThornbarkPlate,
            new[] { (matThornbarkCore, 3), (matSteelPlate, 5), (matTannedLeather, 4) });

        // ═════════════════════════════════════════════════════════════════════
        // 6. TABELAS DE LOOT & INTEGRATION
        // ═════════════════════════════════════════════════════════════════════
        UpdateSwarmerLootTable(matLeather, matSap);
        CreateDropTable(EnemiesLootPath + "brute_loot_table.asset",
            new[] { (matBone as ItemDefinitionBase, 0.70f, 0.15f, 1, 3) }, 5, 12);
        CreateDropTable(EnemiesLootPath + "elite_loot_table.asset",
            new[] { (matBone as ItemDefinitionBase, 0.90f, 0.10f, 2, 5), (matArcaneCrystal as ItemDefinitionBase, 0.30f, 0.15f, 1, 2) }, 15, 35);

        // ═════════════════════════════════════════════════════════════════════
        // 7. PROP ARCANO & GATING DE RECURSOS NO MUNDO
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
