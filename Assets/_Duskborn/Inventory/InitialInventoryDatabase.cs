using System;
using System.Collections.Generic;
using Duskborn.Gameplay.Equipment;
using Duskborn.Gameplay.Loot;
using InventorySystem.Core;
using InventorySystem.Data;
using UnityEngine;

namespace Duskborn.Inventory
{
    /// <summary>
    /// Entrada serializável para um item do inventário inicial com quantidade configurável.
    /// </summary>
    [Serializable]
    public class InitialItemEntry
    {
        [SerializeField] private ItemDefinitionBase item;
        [SerializeField, Min(1)] private int quantity = 1;

        public ItemDefinitionBase Item
        {
            get => item;
            set => item = value;
        }

        public int Quantity
        {
            get => Mathf.Max(1, quantity);
            set => quantity = Mathf.Max(1, value);
        }

        public InitialItemEntry() { }

        public InitialItemEntry(ItemDefinitionBase item, int quantity = 1)
        {
            this.item = item;
            this.quantity = Mathf.Max(1, quantity);
        }

        /// <summary>
        /// Instancia o item em tempo de execução respeitando a quantidade configurada.
        /// </summary>
        public IInventoryItem CreateRuntimeItem()
        {
            if (item == null) return null;
            int qty = Mathf.Max(1, quantity);

            if (item is MaterialDefinition mat)
            {
                return new MaterialItem(mat.Id, mat.DisplayName, mat.Description,
                    mat.Icon != null ? mat.Icon.name : string.Empty, "common", qty);
            }

            if (item is ConsumableDefinition con)
            {
                return new ConsumableItem(con.Id, con.DisplayName, con.Description,
                    con.Icon != null ? con.Icon.name : string.Empty,
                    con.EffectType, con.EffectValue, con.Duration, qty);
            }

            return item.CreateRuntimeItem();
        }
    }

    /// <summary>
    /// Entrada para slot específico de equipamento inicial do jogador.
    /// </summary>
    [Serializable]
    public class InitialGearSlotEntry
    {
        [SerializeField] private EquipmentSlot slot;
        [SerializeField] private GearDefinition gear;

        public EquipmentSlot Slot
        {
            get => slot;
            set => slot = value;
        }

        public GearDefinition Gear
        {
            get => gear;
            set => gear = value;
        }

        public InitialGearSlotEntry() { }

        public InitialGearSlotEntry(EquipmentSlot slot, GearDefinition gear)
        {
            this.slot = slot;
            this.gear = gear;
        }
    }

    /// <summary>
    /// Banco de dados central de itens e configurações de inventário inicial do Duskborn.
    /// Funciona como a única fonte da verdade (Single Source of Truth) para o carregamento inicial
    /// de mochila (backpack), barra de ação rápida (action bar), equipamentos vestidos e relíquias.
    /// </summary>
    [CreateAssetMenu(fileName = "InitialInventoryDatabase", menuName = "Duskborn/Inventory/Initial Inventory Database")]
    public class InitialInventoryDatabase : ScriptableObject
    {
        private static InitialInventoryDatabase _instance;

        public static InitialInventoryDatabase Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<InitialInventoryDatabase>("Inventory/InitialInventoryDatabase");
#if UNITY_EDITOR
                    if (_instance == null)
                    {
                        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:InitialInventoryDatabase");
                        if (guids.Length > 0)
                        {
                            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                            _instance = UnityEditor.AssetDatabase.LoadAssetAtPath<InitialInventoryDatabase>(path);
                        }
                    }

                    if (_instance == null)
                    {
                        _instance = CreateDefaultAsset();
                    }
#endif
                }
                return _instance;
            }
        }

        [Header("Mochila (Backpack / Grid Principal)")]
        [SerializeField] private List<InitialItemEntry> backpackItems = new();

        [Header("Barra de Ação Rápida (Action Bar / Hotbar)")]
        [SerializeField] private List<InitialItemEntry> actionBarItems = new();

        [Header("Equipamento Vestido (Equipped Gear)")]
        [SerializeField] private List<InitialGearSlotEntry> startingGear = new();

        [Header("Relíquias e Buffs Passivos Iniciais")]
        [SerializeField] private List<ItemDefinition> startingRelics = new();

        public List<InitialItemEntry> BackpackItems => backpackItems;
        public List<InitialItemEntry> ActionBarItems => actionBarItems;
        public List<InitialGearSlotEntry> StartingGear => startingGear;
        public List<ItemDefinition> StartingRelics => startingRelics;

        // ── Helpers de Resolução e Criação ───────────────────────────────────

        public ItemDefinitionBase[] GetBackpackDefinitions()
        {
            var list = new List<ItemDefinitionBase>();
            if (backpackItems != null)
            {
                foreach (var entry in backpackItems)
                {
                    if (entry?.Item != null)
                        list.Add(entry.Item);
                }
            }
            return list.ToArray();
        }

        public ItemDefinitionBase[] GetActionBarDefinitions()
        {
            var list = new List<ItemDefinitionBase>();
            if (actionBarItems != null)
            {
                foreach (var entry in actionBarItems)
                {
                    if (entry?.Item != null)
                        list.Add(entry.Item);
                }
            }
            return list.ToArray();
        }

        public GearDefinition[] GetStartingGearDefinitions()
        {
            var list = new List<GearDefinition>();
            if (startingGear != null)
            {
                foreach (var entry in startingGear)
                {
                    if (entry?.Gear != null)
                        list.Add(entry.Gear);
                }
            }
            return list.ToArray();
        }

        public GearDefinition FindGearForSlot(EquipmentSlot slot)
        {
            if (startingGear == null) return null;
            foreach (var entry in startingGear)
            {
                if (entry != null && entry.Slot == slot)
                    return entry.Gear;
            }
            return null;
        }

        public void SetGearForSlot(EquipmentSlot slot, GearDefinition gear)
        {
            if (startingGear == null) startingGear = new List<InitialGearSlotEntry>();

            for (int i = 0; i < startingGear.Count; i++)
            {
                if (startingGear[i] != null && startingGear[i].Slot == slot)
                {
                    if (gear == null)
                    {
                        startingGear.RemoveAt(i);
                    }
                    else
                    {
                        startingGear[i].Gear = gear;
                    }
                    return;
                }
            }

            if (gear != null)
            {
                startingGear.Add(new InitialGearSlotEntry(slot, gear));
            }
        }

        public List<IInventoryItem> CreateRuntimeBackpackItems()
        {
            var list = new List<IInventoryItem>();
            if (backpackItems != null)
            {
                foreach (var entry in backpackItems)
                {
                    var item = entry?.CreateRuntimeItem();
                    if (item != null)
                        list.Add(item);
                }
            }
            return list;
        }

        public List<IInventoryItem> CreateRuntimeActionBarItems()
        {
            var list = new List<IInventoryItem>();
            if (actionBarItems != null)
            {
                foreach (var entry in actionBarItems)
                {
                    var item = entry?.CreateRuntimeItem();
                    if (item != null)
                        list.Add(item);
                }
            }
            return list;
        }

#if UNITY_EDITOR
        public static InitialInventoryDatabase CreateDefaultAsset()
        {
            var db = CreateInstance<InitialInventoryDatabase>();
            string dir = "Assets/_Duskborn/Resources/Inventory";
            if (!System.IO.Directory.Exists(dir))
            {
                System.IO.Directory.CreateDirectory(dir);
            }
            string assetPath = $"{dir}/InitialInventoryDatabase.asset";
            UnityEditor.AssetDatabase.CreateAsset(db, assetPath);
            db.AutoPopulateDefaults();
            UnityEditor.EditorUtility.SetDirty(db);
            UnityEditor.AssetDatabase.SaveAssets();
            return db;
        }

        public void AutoPopulateDefaults()
        {
            backpackItems.Clear();
            actionBarItems.Clear();
            startingGear.Clear();
            startingRelics.Clear();

            // 1. Barra de Ação Inicial (Ferramentas e Consumíveis)
            var stoneAxe = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemDefinitionBase>(
                "Assets/_Duskborn/ScriptableObjects/Weapons/Stone Axe/stone_axe.asset");
            if (stoneAxe != null)
                actionBarItems.Add(new InitialItemEntry(stoneAxe, 1));

            var stonePickaxe = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemDefinitionBase>(
                "Assets/_Duskborn/ScriptableObjects/Weapons/Stone Pickaxe/stone_pickaxe.asset");
            if (stonePickaxe != null)
                actionBarItems.Add(new InitialItemEntry(stonePickaxe, 1));

            var appleDef = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemDefinitionBase>(
                "Assets/_Duskborn/ScriptableObjects/Items/Apple.asset")
                ?? UnityEditor.AssetDatabase.LoadAssetAtPath<ItemDefinitionBase>("Assets/Inventory/Examples/ItemDefinitions/Apple.asset");
            if (appleDef != null)
                actionBarItems.Add(new InitialItemEntry(appleDef, 3));

            // 2. Mochila Inicial (Recursos Iniciais)
            var wood = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemDefinitionBase>(
                "Assets/_Duskborn/ScriptableObjects/Resources/material_wood.asset");
            if (wood != null)
                backpackItems.Add(new InitialItemEntry(wood, 15));

            var stone = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemDefinitionBase>(
                "Assets/_Duskborn/ScriptableObjects/Resources/material_stone.asset");
            if (stone != null)
                backpackItems.Add(new InitialItemEntry(stone, 15));

            // 3. Equipamento Inicial (Matching Player.prefab starting gear)
            AddGearIfFound(EquipmentSlot.Head, "Assets/_Duskborn/ScriptableObjects/Gear/gear_iron_helm.asset");
            AddGearIfFound(EquipmentSlot.Chest, "Assets/_Duskborn/ScriptableObjects/Gear/gear_leather_chest.asset");
            AddGearIfFound(EquipmentSlot.Feet, "Assets/_Duskborn/ScriptableObjects/Gear/gear_worn_boots.asset");
            AddGearIfFound(EquipmentSlot.Neck, "Assets/_Duskborn/ScriptableObjects/Gear/gear_bone_necklace.asset");
            AddGearIfFound(EquipmentSlot.Ring1, "Assets/_Duskborn/ScriptableObjects/Gear/gear_copper_ring.asset");

            UnityEditor.EditorUtility.SetDirty(this);
        }

        private void AddGearIfFound(EquipmentSlot slot, string assetPath)
        {
            var gear = UnityEditor.AssetDatabase.LoadAssetAtPath<GearDefinition>(assetPath);
            if (gear != null)
            {
                startingGear.Add(new InitialGearSlotEntry(slot, gear));
            }
        }
#endif
    }
}
