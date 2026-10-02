#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Duskborn.Gameplay.Equipment;
using Duskborn.Gameplay.Loot;
using InventorySystem.Bootstrap;
using InventorySystem.Data;
using UnityEditor;
using UnityEngine;

namespace Duskborn.Inventory.Editor
{
    /// <summary>
    /// Utility window for centralized management of Duskborn's initial inventory.
    /// Allows inspection, auditing, quantity configuration, and equipping backpack items,
    /// action bar items, worn equipment, and relics in one intuitive panel.
    /// Functionally similar to SoundSettingsEditorWindow.
    /// </summary>
    public class InitialInventoryEditorWindow : EditorWindow
    {
        [MenuItem("Tools/Duskborn/Initial Inventory Settings", priority = 101)]
        [MenuItem("Window/Duskborn/Initial Inventory Settings", priority = 101)]
        public static void Open()
        {
            var window = GetWindow<InitialInventoryEditorWindow>("Initial Inventory");
            window.minSize = new Vector2(720, 560);
            window.Show();
        }

        private InitialInventoryDatabase _database;
        private SerializedObject _serializedDb;
        private Vector2 _scrollPos;
        private int _currentTab = 0;

        // Item catalog cache.
        private List<ItemDefinitionBase> _allProjectItems = new();
        private List<ItemDefinition> _allProjectRelics = new();
        private string _catalogSearch = "";
        private int _catalogCategoryFilter = 0;
        private Vector2 _catalogScrollPos;
        private bool _catalogInitialized = false;

        private static readonly string[] TabNames = new[]
        {
            "🎒 Backpack",
            "⚔️ Action Bar",
            "🛡️ Equipment",
            "🔮 Relics",
            "📦 Catalog",
            "⚙️ Presets"
        };

        private static readonly string[] CatalogCategoryNames = new[]
        {
            "All",
            "Weapons",
            "Equipment",
            "Materials",
            "Consumables",
            "Relics"
        };

        private void OnEnable()
        {
            EnsureDatabase();
            RefreshCatalog();
        }

        private void EnsureDatabase()
        {
            if (_database == null)
            {
                _database = InitialInventoryDatabase.Instance;
            }

            if (_database != null && (_serializedDb == null || _serializedDb.targetObject != _database))
            {
                _serializedDb = new SerializedObject(_database);
            }
        }

        private void OnGUI()
        {
            EnsureDatabase();

            if (_database == null || _serializedDb == null)
            {
                EditorGUILayout.HelpBox("No InitialInventoryDatabase found. Click the button below to create the central database.", MessageType.Warning);
                if (GUILayout.Button("Create Central InitialInventoryDatabase", GUILayout.Height(35)))
                {
                    _database = InitialInventoryDatabase.CreateDefaultAsset();
                    _serializedDb = new SerializedObject(_database);
                }
                return;
            }

            _serializedDb.Update();

            DrawHeader();
            DrawToolbar();

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            EditorGUILayout.Space(6);

            switch (_currentTab)
            {
                case 0: DrawBackpackTab(); break;
                case 1: DrawActionBarTab(); break;
                case 2: DrawEquipmentTab(); break;
                case 3: DrawRelicsTab(); break;
                case 4: DrawCatalogTab(); break;
                case 5: DrawPresetsTab(); break;
            }

            EditorGUILayout.Space(12);
            EditorGUILayout.EndScrollView();

            DrawFooter();

            if (_serializedDb.hasModifiedProperties)
            {
                _serializedDb.ApplyModifiedProperties();
            }
        }

        // ── Top Header & Utilities ───────────────────────────────────────────

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            GUILayout.Label("🎒 Duskborn — Initial Inventory Settings", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("🔍 Ping Asset", EditorStyles.miniButton, GUILayout.Width(90)))
            {
                EditorGUIUtility.PingObject(_database);
                Selection.activeObject = _database;
            }

            if (GUILayout.Button("📥 Import from Prefabs", EditorStyles.miniButton, GUILayout.Width(130)))
            {
                if (EditorUtility.DisplayDialog("Import Items from Prefabs",
                    "Import the current Player and InventoryUI prefab configuration into this database?", "Yes", "Cancel"))
                {
                    SyncFromPrefabs();
                    _serializedDb.Update();
                }
            }

            if (GUILayout.Button("📤 Apply to Prefabs", EditorStyles.miniButton, GUILayout.Width(130)))
            {
                if (EditorUtility.DisplayDialog("Apply to Prefabs",
                    "Write current settings directly into prefab files (InventoryUI.prefab and Player.prefab)?", "Yes", "Cancel"))
                {
                    _serializedDb.ApplyModifiedProperties();
                    ApplyToPrefabs();
                }
            }

            if (GUILayout.Button("🔄 Auto-Configure", EditorStyles.miniButton, GUILayout.Width(115)))
            {
                if (EditorUtility.DisplayDialog("Auto-Configure Starting Items",
                    "Automatically fill the database with the recommended default starting set (Tools, Resources, and Basic Armor)?", "Yes", "Cancel"))
                {
                    _database.AutoPopulateDefaults();
                    _serializedDb.Update();
                }
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.Space(2);
            _currentTab = GUILayout.Toolbar(_currentTab, TabNames, GUILayout.Height(28));
            EditorGUILayout.Space(4);
        }

        private void DrawFooter()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            int backpackCount = _database.BackpackItems != null ? _database.BackpackItems.Count : 0;
            int actionBarCount = _database.ActionBarItems != null ? _database.ActionBarItems.Count : 0;
            int gearCount = _database.StartingGear != null ? _database.StartingGear.Count : 0;
            int relicsCount = _database.StartingRelics != null ? _database.StartingRelics.Count : 0;

            EditorGUILayout.LabelField(
                $"Backpack: {backpackCount} | Action Bar: {actionBarCount} | Equipment: {gearCount} | Relics: {relicsCount}",
                EditorStyles.miniLabel);

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("💾 Save Changes", GUILayout.Width(150), GUILayout.Height(24)))
            {
                _serializedDb.ApplyModifiedProperties();
                EditorUtility.SetDirty(_database);
                AssetDatabase.SaveAssets();
                ShowNotification(new GUIContent("Settings saved successfully!"));
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        // 0. Backpack Items

        private void DrawBackpackTab()
        {
            DrawSectionHeader("Backpack / Main Grid",
                "Configure items and quantities received in the player's backpack at session start.");

            var prop = _serializedDb.FindProperty("backpackItems");
            if (prop == null) return;

            DrawDragAndDropBox("Drag items from Project here to add to the Backpack", (itemDef) =>
            {
                AddItemToEntryList(prop, itemDef, 1);
            });

            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Backpack Items ({prop.arraySize})", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("+ Add New Item", EditorStyles.miniButton, GUILayout.Width(150)))
            {
                prop.InsertArrayElementAtIndex(prop.arraySize);
                var elem = prop.GetArrayElementAtIndex(prop.arraySize - 1);
                elem.FindPropertyRelative("item").objectReferenceValue = null;
                elem.FindPropertyRelative("quantity").intValue = 1;
            }

            if (prop.arraySize > 0 && GUILayout.Button("Clear All", EditorStyles.miniButton, GUILayout.Width(90)))
            {
                if (EditorUtility.DisplayDialog("Clear Backpack", "Remove all starting backpack items?", "Yes", "No"))
                {
                    prop.ClearArray();
                }
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);

            for (int i = 0; i < prop.arraySize; i++)
            {
                var elem = prop.GetArrayElementAtIndex(i);
                DrawItemEntryCard(elem, i, prop);
            }
        }

        // 1. Action Bar

        private void DrawActionBarTab()
        {
            DrawSectionHeader("Quick Action Bar (Hotbar / Shortcuts 1 to 5)",
                "Configure items and weapons equipped in the main interface's quick shortcuts.");

            var prop = _serializedDb.FindProperty("actionBarItems");
            if (prop == null) return;

            DrawDragAndDropBox("Drag items from Project here to add to the Action Bar", (itemDef) =>
            {
                AddItemToEntryList(prop, itemDef, 1);
            });

            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Action Bar Slots ({prop.arraySize})", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("+ Add to Shortcut", EditorStyles.miniButton, GUILayout.Width(150)))
            {
                prop.InsertArrayElementAtIndex(prop.arraySize);
                var elem = prop.GetArrayElementAtIndex(prop.arraySize - 1);
                elem.FindPropertyRelative("item").objectReferenceValue = null;
                elem.FindPropertyRelative("quantity").intValue = 1;
            }

            if (prop.arraySize > 0 && GUILayout.Button("Clear Action Bar", EditorStyles.miniButton, GUILayout.Width(90)))
            {
                if (EditorUtility.DisplayDialog("Clear Action Bar", "Remove all starting action bar items?", "Yes", "No"))
                {
                    prop.ClearArray();
                }
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);

            for (int i = 0; i < prop.arraySize; i++)
            {
                var elem = prop.GetArrayElementAtIndex(i);
                DrawItemEntryCard(elem, i, prop, slotPrefix: $"Shortcut [{i + 1}]");
            }
        }

        // 2. Equipped Gear

        private void DrawEquipmentTab()
        {
            DrawSectionHeader("Starting Equipped Gear",
                "Configure armor, rings, and accessories worn directly in character equipment slots.");

            var gearProp = _serializedDb.FindProperty("startingGear");
            if (gearProp == null) return;

            Array slots = Enum.GetValues(typeof(EquipmentSlot));

            foreach (EquipmentSlot slot in slots)
            {
                int existingIdx = -1;
                for (int i = 0; i < gearProp.arraySize; i++)
                {
                    var elem = gearProp.GetArrayElementAtIndex(i);
                    var sProp = elem.FindPropertyRelative("slot");
                    if (sProp != null && (EquipmentSlot)sProp.enumValueIndex == slot)
                    {
                        existingIdx = i;
                        break;
                    }
                }

                DrawEquipmentSlotRow(slot, existingIdx, gearProp);
            }
        }

        private void DrawEquipmentSlotRow(EquipmentSlot slot, int existingIndex, SerializedProperty gearProp)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            // Slot icon.
            Texture2D slotIcon = LoadSlotIcon(slot);
            if (slotIcon != null)
            {
                GUILayout.Label(new GUIContent(slotIcon), GUILayout.Width(34), GUILayout.Height(34));
            }
            else
            {
                GUILayout.Box("🛡️", GUILayout.Width(34), GUILayout.Height(34));
            }

            // English slot name.
            EditorGUILayout.BeginVertical(GUILayout.Width(130));
            EditorGUILayout.LabelField(GetSlotDisplayName(slot), EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Slot: {slot}", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            // Gear selection field.
            GearDefinition currentGear = null;
            SerializedProperty entryProp = null;
            SerializedProperty itemProp = null;

            if (existingIndex >= 0)
            {
                entryProp = gearProp.GetArrayElementAtIndex(existingIndex);
                itemProp = entryProp.FindPropertyRelative("gear");
                currentGear = itemProp.objectReferenceValue as GearDefinition;
            }

            var newGear = EditorGUILayout.ObjectField(currentGear, typeof(GearDefinition), false) as GearDefinition;

            if (newGear != currentGear)
            {
                if (newGear == null)
                {
                    if (existingIndex >= 0)
                    {
                        gearProp.DeleteArrayElementAtIndex(existingIndex);
                    }
                }
                else
                {
                    if (existingIndex >= 0)
                    {
                        itemProp.objectReferenceValue = newGear;
                    }
                    else
                    {
                        gearProp.InsertArrayElementAtIndex(gearProp.arraySize);
                        var newElem = gearProp.GetArrayElementAtIndex(gearProp.arraySize - 1);
                        newElem.FindPropertyRelative("slot").enumValueIndex = (int)slot;
                        newElem.FindPropertyRelative("gear").objectReferenceValue = newGear;
                    }
                }
            }

            // Clear slot button.
            GUI.enabled = currentGear != null;
            if (GUILayout.Button("✖", EditorStyles.miniButton, GUILayout.Width(26), GUILayout.Height(20)))
            {
                if (existingIndex >= 0)
                {
                    gearProp.DeleteArrayElementAtIndex(existingIndex);
                }
            }
            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();

            // Equipped item details, if present.
            if (currentGear != null)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(42);

                string rarityHex = GetRarityColorHex(currentGear.Rarity);
                string statsSummary = GetGearStatsSummary(currentGear);

                EditorGUILayout.LabelField(
                    $"<color={rarityHex}><b>{currentGear.DisplayName}</b> ({currentGear.Rarity})</color> — {statsSummary}",
                    new GUIStyle(EditorStyles.miniLabel) { richText = true });

                if (currentGear.Slot != slot)
                {
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.HelpBox($"Warning: This item declares slot '{currentGear.Slot}', but is assigned to '{slot}'.", MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        // 3. Starting Relics & Buffs

        private void DrawRelicsTab()
        {
            DrawSectionHeader("Starting Roguelite Relics & Buffs",
                "Define talismans and elixirs granting permanent passive bonuses at the start of the journey.");

            var relicsProp = _serializedDb.FindProperty("startingRelics");
            if (relicsProp == null) return;

            DrawDragAndDropBox("Drag relic items (ItemDefinition) here", (itemDef) =>
            {
                // Handled if relic dragged
            });

            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Active Relics ({relicsProp.arraySize})", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("+ Add Relic", EditorStyles.miniButton, GUILayout.Width(150)))
            {
                relicsProp.InsertArrayElementAtIndex(relicsProp.arraySize);
                relicsProp.GetArrayElementAtIndex(relicsProp.arraySize - 1).objectReferenceValue = null;
            }

            if (relicsProp.arraySize > 0 && GUILayout.Button("Clear All", EditorStyles.miniButton, GUILayout.Width(90)))
            {
                if (EditorUtility.DisplayDialog("Clear Relics", "Remove all starting relics?", "Yes", "No"))
                {
                    relicsProp.ClearArray();
                }
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);

            for (int i = 0; i < relicsProp.arraySize; i++)
            {
                var elem = relicsProp.GetArrayElementAtIndex(i);
                var relic = elem.objectReferenceValue as ItemDefinition;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();

                if (relic != null && relic.Icon != null)
                {
                    GUILayout.Label(new GUIContent(relic.Icon), GUILayout.Width(34), GUILayout.Height(34));
                }
                else
                {
                    GUILayout.Box("🔮", GUILayout.Width(34), GUILayout.Height(34));
                }

                EditorGUILayout.PropertyField(elem, GUIContent.none);

                if (GUILayout.Button("✖", EditorStyles.miniButton, GUILayout.Width(26), GUILayout.Height(20)))
                {
                    relicsProp.DeleteArrayElementAtIndex(i);
                    break;
                }

                EditorGUILayout.EndHorizontal();

                if (relic != null)
                {
                    string rarityHex = GetRarityColorHex(relic.Rarity);
                    string effectText = $"{relic.EffectType} ({relic.EffectMode}): +{relic.EffectValue}";
                    EditorGUILayout.LabelField($"<color={rarityHex}><b>{relic.ItemName}</b></color> — {effectText}",
                        new GUIStyle(EditorStyles.miniLabel) { richText = true });
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }
        }

        // 4. Item Catalog

        private void DrawCatalogTab()
        {
            DrawSectionHeader("Duskborn General Item Catalog",
                "Search all registered project items and add them to Backpack, Action Bar, or Equipment with one click.");

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            _catalogSearch = EditorGUILayout.TextField(_catalogSearch, EditorStyles.toolbarSearchField, GUILayout.Width(250));
            if (!string.IsNullOrEmpty(_catalogSearch) && GUILayout.Button("✖", EditorStyles.toolbarButton, GUILayout.Width(22)))
            {
                _catalogSearch = "";
                GUI.FocusControl(null);
            }

            EditorGUILayout.Space(8);
            _catalogCategoryFilter = GUILayout.Toolbar(_catalogCategoryFilter, CatalogCategoryNames, EditorStyles.toolbarButton);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("🔄 Refresh", EditorStyles.toolbarButton, GUILayout.Width(75)))
            {
                RefreshCatalog();
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(6);

            _catalogScrollPos = EditorGUILayout.BeginScrollView(_catalogScrollPos, GUILayout.MinHeight(300));

            // Draw ItemDefinitionBase items.
            int displayedCount = 0;
            string searchLower = _catalogSearch.Trim().ToLowerInvariant();

            if (_catalogCategoryFilter != 5) // If not the exclusive relic category.
            {
                foreach (var item in _allProjectItems)
                {
                    if (item == null) continue;

                    if (!MatchesCategoryFilter(item, _catalogCategoryFilter)) continue;
                    if (!string.IsNullOrEmpty(searchLower) &&
                        !item.name.ToLowerInvariant().Contains(searchLower) &&
                        !item.DisplayName.ToLowerInvariant().Contains(searchLower) &&
                        !item.Id.ToLowerInvariant().Contains(searchLower))
                    {
                        continue;
                    }

                    DrawCatalogItemCard(item);
                    displayedCount++;
                }
            }

            // Draw relics (ItemDefinition) if "All" or "Relics" is selected.
            if (_catalogCategoryFilter == 0 || _catalogCategoryFilter == 5)
            {
                foreach (var relic in _allProjectRelics)
                {
                    if (relic == null) continue;
                    if (!string.IsNullOrEmpty(searchLower) &&
                        !relic.name.ToLowerInvariant().Contains(searchLower) &&
                        !relic.ItemName.ToLowerInvariant().Contains(searchLower))
                    {
                        continue;
                    }

                    DrawCatalogRelicCard(relic);
                    displayedCount++;
                }
            }

            if (displayedCount == 0)
            {
                EditorGUILayout.HelpBox("No items found matching the search filters.", MessageType.Info);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawCatalogItemCard(ItemDefinitionBase item)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            // Thumbnail
            if (item.Icon != null)
            {
                GUILayout.Label(new GUIContent(item.Icon), GUILayout.Width(38), GUILayout.Height(38));
            }
            else
            {
                GUILayout.Box("📦", GUILayout.Width(38), GUILayout.Height(38));
            }

            // Info
            EditorGUILayout.BeginVertical();
            string rarityHex = GetRarityColorHex(item.Rarity);
            string itemType = GetItemTypeDisplayName(item);
            EditorGUILayout.LabelField(
                $"<color={rarityHex}><b>{item.DisplayName}</b></color> <color=#888888>({itemType})</color>",
                new GUIStyle(EditorStyles.boldLabel) { richText = true });

            string desc = !string.IsNullOrEmpty(item.Description) ? item.Description.Replace("\n", " ") : "No description";
            if (desc.Length > 90) desc = desc.Substring(0, 87) + "...";
            EditorGUILayout.LabelField(desc, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            // Quick action buttons.
            if (GUILayout.Button("+ Backpack", EditorStyles.miniButton, GUILayout.Width(75)))
            {
                var prop = _serializedDb.FindProperty("backpackItems");
                AddItemToEntryList(prop, item, 1);
                _serializedDb.ApplyModifiedProperties();
                ShowNotification(new GUIContent($"Added to Backpack: {item.DisplayName}"));
            }

            if (GUILayout.Button("+ Action Bar", EditorStyles.miniButton, GUILayout.Width(65)))
            {
                var prop = _serializedDb.FindProperty("actionBarItems");
                AddItemToEntryList(prop, item, 1);
                _serializedDb.ApplyModifiedProperties();
                ShowNotification(new GUIContent($"Added to Action Bar: {item.DisplayName}"));
            }

            if (item is GearDefinition gear)
            {
                if (GUILayout.Button("+ Equip", EditorStyles.miniButton, GUILayout.Width(70)))
                {
                    _database.SetGearForSlot(gear.Slot, gear);
                    _serializedDb.Update();
                    EditorUtility.SetDirty(_database);
                    ShowNotification(new GUIContent($"Equipped in slot {gear.Slot}: {gear.DisplayName}"));
                }
            }

            if (GUILayout.Button("🔍", EditorStyles.miniButton, GUILayout.Width(26)))
            {
                EditorGUIUtility.PingObject(item);
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        private void DrawCatalogRelicCard(ItemDefinition relic)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            if (relic.Icon != null)
            {
                GUILayout.Label(new GUIContent(relic.Icon), GUILayout.Width(38), GUILayout.Height(38));
            }
            else
            {
                GUILayout.Box("🔮", GUILayout.Width(38), GUILayout.Height(38));
            }

            EditorGUILayout.BeginVertical();
            string rarityHex = GetRarityColorHex(relic.Rarity);
            EditorGUILayout.LabelField(
                $"<color={rarityHex}><b>{relic.ItemName}</b></color> <color=#888888>(Roguelite Relic)</color>",
                new GUIStyle(EditorStyles.boldLabel) { richText = true });

            string effectText = $"{relic.EffectType} ({relic.EffectMode}): +{relic.EffectValue}";
            EditorGUILayout.LabelField(effectText, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("+ Relic", EditorStyles.miniButton, GUILayout.Width(80)))
            {
                var prop = _serializedDb.FindProperty("startingRelics");
                prop.InsertArrayElementAtIndex(prop.arraySize);
                prop.GetArrayElementAtIndex(prop.arraySize - 1).objectReferenceValue = relic;
                _serializedDb.ApplyModifiedProperties();
                ShowNotification(new GUIContent($"Added relic: {relic.ItemName}"));
            }

            if (GUILayout.Button("🔍", EditorStyles.miniButton, GUILayout.Width(26)))
            {
                EditorGUIUtility.PingObject(relic);
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        // 5. Presets & Global Operations

        private void DrawPresetsTab()
        {
            DrawSectionHeader("Presets & Quick Loadouts",
                "Load presets to speed up testing with archetypes and different play styles.");

            DrawSectionBox("⚔️ Preset: Balanced Warrior", () =>
            {
                EditorGUILayout.LabelField("Action Bar: Stone Axe, Iron Sword, 3x Apple");
                EditorGUILayout.LabelField("Backpack: 15x Wood, 15x Stone, 5x Iron Bar");
                EditorGUILayout.LabelField("Equipment: Iron Helmet, Leather Armor, Worn Boots, Bone Necklace, Copper Ring");
                EditorGUILayout.Space(4);
                if (GUILayout.Button("Apply Warrior Preset", GUILayout.Height(26)))
                {
                    ApplyWarriorPreset();
                }
            });

            EditorGUILayout.Space(6);
            DrawSectionBox("⛏️ Preset: Gatherer / Builder", () =>
            {
                EditorGUILayout.LabelField("Action Bar: Stone Axe, Stone Pickaxe, 5x Apple");
                EditorGUILayout.LabelField("Backpack: 30x Wood, 30x Stone, 15x Fiber");
                EditorGUILayout.LabelField("Equipment: Leather Armor, Worn Boots");
                EditorGUILayout.Space(4);
                if (GUILayout.Button("Apply Gatherer Preset", GUILayout.Height(26)))
                {
                    ApplyGathererPreset();
                }
            });

            EditorGUILayout.Space(6);
            DrawSectionBox("🧪 Preset: Survivor / Alchemist", () =>
            {
                EditorGUILayout.LabelField("Action Bar: Stone Axe, 5x Apple");
                EditorGUILayout.LabelField("Backpack: 10x Arcane Crystal, 10x Crystal Powder, 20x Wood");
                EditorGUILayout.LabelField("Equipment: Wind Boots, Leather Armor");
                EditorGUILayout.Space(4);
                if (GUILayout.Button("Apply Alchemist Preset", GUILayout.Height(26)))
                {
                    ApplyAlchemistPreset();
                }
            });

            EditorGUILayout.Space(6);
            DrawSectionBox("🗑️ Clear All Starting Items", () =>
            {
                EditorGUILayout.LabelField("Remove all backpack, action bar, equipment, and relic items to test an empty start.");
                EditorGUILayout.Space(4);
                if (GUILayout.Button("Clear All (Empty Start)", GUILayout.Height(26)))
                {
                    if (EditorUtility.DisplayDialog("Clear Initial Inventory", "Are you sure you want to remove all items?", "Yes", "Cancel"))
                    {
                        _database.BackpackItems.Clear();
                        _database.ActionBarItems.Clear();
                        _database.StartingGear.Clear();
                        _database.StartingRelics.Clear();
                        _serializedDb.Update();
                        EditorUtility.SetDirty(_database);
                    }
                }
            });
        }

        // ── UI Card Helpers ───────────────────────────────────────────────────

        private void DrawItemEntryCard(SerializedProperty elem, int index, SerializedProperty parentList, string slotPrefix = null)
        {
            var itemProp = elem.FindPropertyRelative("item");
            var qtyProp = elem.FindPropertyRelative("quantity");

            var currentItem = itemProp.objectReferenceValue as ItemDefinitionBase;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            // Thumbnail
            if (currentItem != null && currentItem.Icon != null)
            {
                GUILayout.Label(new GUIContent(currentItem.Icon), GUILayout.Width(36), GUILayout.Height(36));
            }
            else
            {
                GUILayout.Box("📦", GUILayout.Width(36), GUILayout.Height(36));
            }

            // Optional prefix
            if (!string.IsNullOrEmpty(slotPrefix))
            {
                EditorGUILayout.LabelField(slotPrefix, EditorStyles.boldLabel, GUILayout.Width(80));
            }

            // ObjectField
            EditorGUILayout.PropertyField(itemProp, GUIContent.none);

            // Quantity with quick [-] and [+] buttons.
            EditorGUILayout.LabelField("Qtd:", GUILayout.Width(30));
            if (GUILayout.Button("-", EditorStyles.miniButtonLeft, GUILayout.Width(20), GUILayout.Height(18)))
            {
                qtyProp.intValue = Mathf.Max(1, qtyProp.intValue - 1);
            }
            qtyProp.intValue = Mathf.Max(1, EditorGUILayout.IntField(qtyProp.intValue, GUILayout.Width(42)));
            if (GUILayout.Button("+", EditorStyles.miniButtonRight, GUILayout.Width(20), GUILayout.Height(18)))
            {
                qtyProp.intValue += 1;
            }

            // Ordering and removal controls.
            GUI.enabled = index > 0;
            if (GUILayout.Button("▲", EditorStyles.miniButtonLeft, GUILayout.Width(24), GUILayout.Height(18)))
            {
                parentList.MoveArrayElement(index, index - 1);
                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }

            GUI.enabled = index < parentList.arraySize - 1;
            if (GUILayout.Button("▼", EditorStyles.miniButtonMid, GUILayout.Width(24), GUILayout.Height(18)))
            {
                parentList.MoveArrayElement(index, index + 1);
                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }
            GUI.enabled = true;

            if (GUILayout.Button("✖", EditorStyles.miniButtonRight, GUILayout.Width(24), GUILayout.Height(18)))
            {
                parentList.DeleteArrayElementAtIndex(index);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.EndHorizontal();

            // Item details row.
            if (currentItem != null)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(42);
                string rarityHex = GetRarityColorHex(currentItem.Rarity);
                string itemType = GetItemTypeDisplayName(currentItem);
                EditorGUILayout.LabelField(
                    $"<color={rarityHex}><b>{currentItem.DisplayName}</b> ({currentItem.Rarity})</color> — Type: {itemType}",
                    new GUIStyle(EditorStyles.miniLabel) { richText = true });
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        private void DrawDragAndDropBox(string label, Action<ItemDefinitionBase> onItemDropped)
        {
            var dropRect = GUILayoutUtility.GetRect(0f, 32f, GUILayout.ExpandWidth(true));
            GUI.Box(dropRect, label, EditorStyles.helpBox);

            var evt = Event.current;
            if (dropRect.Contains(evt.mousePosition))
            {
                if (evt.type == EventType.DragUpdated)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    evt.Use();
                }
                else if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    foreach (var dragged in DragAndDrop.objectReferences)
                    {
                        if (dragged is ItemDefinitionBase itemDef)
                        {
                            onItemDropped?.Invoke(itemDef);
                        }
                    }
                    _serializedDb.Update();
                    evt.Use();
                }
            }
        }

        private void AddItemToEntryList(SerializedProperty listProp, ItemDefinitionBase item, int quantity)
        {
            if (listProp == null || item == null) return;
            listProp.InsertArrayElementAtIndex(listProp.arraySize);
            var elem = listProp.GetArrayElementAtIndex(listProp.arraySize - 1);
            elem.FindPropertyRelative("item").objectReferenceValue = item;
            elem.FindPropertyRelative("quantity").intValue = Mathf.Max(1, quantity);
        }

        // ── UI Helpers ────────────────────────────────────────────────────────

        private void DrawSectionHeader(string title, string subtitle)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(subtitle, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        private void DrawSectionBox(string header, Action content)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            if (!string.IsNullOrEmpty(header))
            {
                EditorGUILayout.LabelField(header, EditorStyles.boldLabel);
                EditorGUILayout.Space(2);
            }
            content();
            EditorGUILayout.EndVertical();
        }

        // ── Catalog Helpers ───────────────────────────────────────────────────

        private void RefreshCatalog()
        {
            _allProjectItems.Clear();
            _allProjectRelics.Clear();

            string[] baseGuids = AssetDatabase.FindAssets("t:ItemDefinitionBase");
            foreach (var guid in baseGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var item = AssetDatabase.LoadAssetAtPath<ItemDefinitionBase>(path);
                if (item != null)
                {
                    _allProjectItems.Add(item);
                }
            }

            string[] relicGuids = AssetDatabase.FindAssets("t:ItemDefinition");
            foreach (var guid in relicGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var relic = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
                if (relic != null)
                {
                    _allProjectRelics.Add(relic);
                }
            }

            _catalogInitialized = true;
        }

        private bool MatchesCategoryFilter(ItemDefinitionBase item, int categoryIndex)
        {
            return categoryIndex switch
            {
                0 => true, // All.
                1 => item is WeaponDefinition,
                2 => item is GearDefinition,
                3 => item is MaterialDefinition,
                4 => item is ConsumableDefinition || item.GetType().Name.Contains("Apple"),
                _ => true
            };
        }

        private static string GetItemTypeDisplayName(ItemDefinitionBase item)
        {
            if (item is WeaponDefinition) return "Weapon";
            if (item is GearDefinition g) return $"Equipment ({g.Slot})";
            if (item is MaterialDefinition) return "Material / Resource";
            if (item is ConsumableDefinition) return "Consumable";
            return "General Item";
        }

        private static string GetSlotDisplayName(EquipmentSlot slot)
        {
            return slot switch
            {
                EquipmentSlot.Head => "Helmet",
                EquipmentSlot.Neck => "Necklace / Amulet",
                EquipmentSlot.Shoulder => "Shoulders",
                EquipmentSlot.Back => "Capa",
                EquipmentSlot.Chest => "Chest",
                EquipmentSlot.Wrist => "Bracers",
                EquipmentSlot.Hands => "Gloves",
                EquipmentSlot.Waist => "Belt",
                EquipmentSlot.Legs => "Trousers / Leggings",
                EquipmentSlot.Feet => "Boots",
                EquipmentSlot.Ring1 => "Ring 1",
                EquipmentSlot.Ring2 => "Ring 2",
                _ => slot.ToString()
            };
        }

        private static string GetRarityColorHex(ItemRarity rarity)
        {
            return rarity switch
            {
                ItemRarity.Common => "#DCDCDC",
                ItemRarity.Uncommon => "#55FF55",
                ItemRarity.Rare => "#4499FF",
                ItemRarity.Epic => "#BB44FF",
                ItemRarity.Legendary => "#FFAA00",
                ItemRarity.Cursed => "#FF4444",
                _ => "#FFFFFF"
            };
        }

        private static string GetGearStatsSummary(GearDefinition gear)
        {
            if (gear == null || gear.Bonuses == null || gear.Bonuses.Count == 0) return "No bonuses";
            var parts = new List<string>();
            foreach (var b in gear.Bonuses)
            {
                parts.Add(b.FormatLine());
            }
            return string.Join(", ", parts);
        }

        private static Texture2D LoadSlotIcon(EquipmentSlot slot)
        {
            string slotFileName = slot switch
            {
                EquipmentSlot.Head => "slot_head",
                EquipmentSlot.Neck => "slot_neck",
                EquipmentSlot.Shoulder => "slot_shoulder",
                EquipmentSlot.Back => "slot_back",
                EquipmentSlot.Chest => "slot_chest",
                EquipmentSlot.Wrist => "slot_wrist",
                EquipmentSlot.Hands => "slot_hands",
                EquipmentSlot.Waist => "slot_waist",
                EquipmentSlot.Legs => "slot_legs",
                EquipmentSlot.Feet => "slot_feet",
                EquipmentSlot.Ring1 => "slot_ring",
                EquipmentSlot.Ring2 => "slot_ring",
                _ => null
            };

            if (slotFileName == null) return null;
            return Resources.Load<Texture2D>($"Textures/Slots/{slotFileName}");
        }

        // ── Prefab Sync & Presets ─────────────────────────────────────────────

        private void SyncFromPrefabs()
        {
            // 1. Import Backpack from InventoryUI.prefab.
            string invPath = "Assets/Inventory/Prefabs/InventoryUI.prefab";
            var invPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(invPath);
            if (invPrefab != null)
            {
                var installer = invPrefab.GetComponent<InventoryInstaller>();
                if (installer != null)
                {
                    var so = new SerializedObject(installer);
                    var startupProp = so.FindProperty("startupItems");
                    if (startupProp != null)
                    {
                        _database.BackpackItems.Clear();
                        for (int i = 0; i < startupProp.arraySize; i++)
                        {
                            var def = startupProp.GetArrayElementAtIndex(i).objectReferenceValue as ItemDefinitionBase;
                            if (def != null)
                            {
                                _database.BackpackItems.Add(new InitialItemEntry(def, 1));
                            }
                        }
                    }
                }
            }

            // 2. Import Equipment from Player.prefab.
            string playerPath = "Assets/_Duskborn/Prefabs/Player/Player.prefab";
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(playerPath);
            if (playerPrefab != null)
            {
                var equipContainer = playerPrefab.GetComponent<PlayerEquipmentContainer>();
                if (equipContainer != null)
                {
                    var so = new SerializedObject(equipContainer);
                    var gearProp = so.FindProperty("startingGear");
                    if (gearProp != null)
                    {
                        _database.StartingGear.Clear();
                        for (int i = 0; i < gearProp.arraySize; i++)
                        {
                            var g = gearProp.GetArrayElementAtIndex(i).objectReferenceValue as GearDefinition;
                            if (g != null)
                            {
                                _database.SetGearForSlot(g.Slot, g);
                            }
                        }
                    }
                }
            }

            EditorUtility.SetDirty(_database);
            AssetDatabase.SaveAssets();
            ShowNotification(new GUIContent("Items imported from prefabs successfully!"));
        }

        private void ApplyToPrefabs()
        {
            int modifiedCount = 0;

            // 1. Write to InventoryUI.prefab.
            string invPath = "Assets/Inventory/Prefabs/InventoryUI.prefab";
            var invPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(invPath);
            if (invPrefab != null)
            {
                var installer = invPrefab.GetComponent<InventoryInstaller>();
                if (installer != null)
                {
                    var so = new SerializedObject(installer);
                    var startupProp = so.FindProperty("startupItems");
                    if (startupProp != null)
                    {
                        startupProp.ClearArray();
                        for (int i = 0; i < _database.BackpackItems.Count; i++)
                        {
                            var entry = _database.BackpackItems[i];
                            if (entry?.Item != null)
                            {
                                startupProp.InsertArrayElementAtIndex(startupProp.arraySize);
                                startupProp.GetArrayElementAtIndex(startupProp.arraySize - 1).objectReferenceValue = entry.Item;
                            }
                        }
                        so.ApplyModifiedProperties();
                        EditorUtility.SetDirty(invPrefab);
                        PrefabUtility.SavePrefabAsset(invPrefab);
                        modifiedCount++;
                    }
                }
            }

            // 2. Write to Player prefabs.
            string[] playerPaths = new[]
            {
                "Assets/_Duskborn/Prefabs/Player/Player.prefab",
                "Assets/_Duskborn/Prefabs/Player/Player 1.prefab"
            };

            foreach (var path in playerPaths)
            {
                var pPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (pPrefab == null) continue;
                var equipContainer = pPrefab.GetComponent<PlayerEquipmentContainer>();
                if (equipContainer != null)
                {
                    var so = new SerializedObject(equipContainer);
                    var gearProp = so.FindProperty("startingGear");
                    if (gearProp != null)
                    {
                        gearProp.ClearArray();
                        for (int i = 0; i < _database.StartingGear.Count; i++)
                        {
                            var entry = _database.StartingGear[i];
                            if (entry?.Gear != null)
                            {
                                gearProp.InsertArrayElementAtIndex(gearProp.arraySize);
                                gearProp.GetArrayElementAtIndex(gearProp.arraySize - 1).objectReferenceValue = entry.Gear;
                            }
                        }
                        so.ApplyModifiedProperties();
                        EditorUtility.SetDirty(pPrefab);
                        PrefabUtility.SavePrefabAsset(pPrefab);
                        modifiedCount++;
                    }
                }
            }

            AssetDatabase.SaveAssets();
            ShowNotification(new GUIContent($"Successfully written to {modifiedCount} prefabs!"));
        }

        private void ApplyWarriorPreset()
        {
            _database.BackpackItems.Clear();
            _database.ActionBarItems.Clear();
            _database.StartingGear.Clear();

            // ActionBar
            LoadAndAddActionBar("Assets/_Duskborn/ScriptableObjects/Weapons/Stone Axe/stone_axe.asset", 1);
            LoadAndAddActionBar("Assets/_Duskborn/ScriptableObjects/Weapons/weapon_iron_sword.asset", 1);
            LoadAndAddActionBar("Assets/Inventory/Examples/ItemDefinitions/Apple.asset", 3);

            // Backpack
            LoadAndAddBackpack("Assets/_Duskborn/ScriptableObjects/Resources/material_wood.asset", 15);
            LoadAndAddBackpack("Assets/_Duskborn/ScriptableObjects/Resources/material_stone.asset", 15);
            LoadAndAddBackpack("Assets/_Duskborn/ScriptableObjects/Resources/material_iron.asset", 5);

            // Gear
            LoadAndSetGear(EquipmentSlot.Head, "Assets/_Duskborn/ScriptableObjects/Gear/gear_iron_helm.asset");
            LoadAndSetGear(EquipmentSlot.Chest, "Assets/_Duskborn/ScriptableObjects/Gear/gear_leather_chest.asset");
            LoadAndSetGear(EquipmentSlot.Feet, "Assets/_Duskborn/ScriptableObjects/Gear/gear_worn_boots.asset");
            LoadAndSetGear(EquipmentSlot.Neck, "Assets/_Duskborn/ScriptableObjects/Gear/gear_bone_necklace.asset");
            LoadAndSetGear(EquipmentSlot.Ring1, "Assets/_Duskborn/ScriptableObjects/Gear/gear_copper_ring.asset");

            _serializedDb.Update();
            EditorUtility.SetDirty(_database);
            ShowNotification(new GUIContent("Warrior Preset Applied!"));
        }

        private void ApplyGathererPreset()
        {
            _database.BackpackItems.Clear();
            _database.ActionBarItems.Clear();
            _database.StartingGear.Clear();

            // ActionBar
            LoadAndAddActionBar("Assets/_Duskborn/ScriptableObjects/Weapons/Stone Axe/stone_axe.asset", 1);
            LoadAndAddActionBar("Assets/_Duskborn/ScriptableObjects/Weapons/Stone Pickaxe/stone_pickaxe.asset", 1);
            LoadAndAddActionBar("Assets/Inventory/Examples/ItemDefinitions/Apple.asset", 5);

            // Backpack
            LoadAndAddBackpack("Assets/_Duskborn/ScriptableObjects/Resources/material_wood.asset", 30);
            LoadAndAddBackpack("Assets/_Duskborn/ScriptableObjects/Resources/material_stone.asset", 30);
            LoadAndAddBackpack("Assets/_Duskborn/ScriptableObjects/Resources/material_fiber.asset", 15);

            // Gear
            LoadAndSetGear(EquipmentSlot.Chest, "Assets/_Duskborn/ScriptableObjects/Gear/gear_leather_chest.asset");
            LoadAndSetGear(EquipmentSlot.Feet, "Assets/_Duskborn/ScriptableObjects/Gear/gear_worn_boots.asset");

            _serializedDb.Update();
            EditorUtility.SetDirty(_database);
            ShowNotification(new GUIContent("Gatherer Preset Applied!"));
        }

        private void ApplyAlchemistPreset()
        {
            _database.BackpackItems.Clear();
            _database.ActionBarItems.Clear();
            _database.StartingGear.Clear();

            // ActionBar
            LoadAndAddActionBar("Assets/_Duskborn/ScriptableObjects/Weapons/Stone Axe/stone_axe.asset", 1);
            LoadAndAddActionBar("Assets/Inventory/Examples/ItemDefinitions/Apple.asset", 5);

            // Backpack
            LoadAndAddBackpack("Assets/_Duskborn/ScriptableObjects/Resources/material_arcane_crystal.asset", 10);
            LoadAndAddBackpack("Assets/_Duskborn/ScriptableObjects/Resources/material_crystal_powder.asset", 10);
            LoadAndAddBackpack("Assets/_Duskborn/ScriptableObjects/Resources/material_wood.asset", 20);

            // Gear
            LoadAndSetGear(EquipmentSlot.Chest, "Assets/_Duskborn/ScriptableObjects/Gear/gear_leather_chest.asset");
            LoadAndSetGear(EquipmentSlot.Feet, "Assets/_Duskborn/ScriptableObjects/Gear/gear_wind_boots.asset");

            _serializedDb.Update();
            EditorUtility.SetDirty(_database);
            ShowNotification(new GUIContent("Alchemist Preset Applied!"));
        }

        private void LoadAndAddActionBar(string assetPath, int qty)
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinitionBase>(assetPath);
            if (item != null) _database.ActionBarItems.Add(new InitialItemEntry(item, qty));
        }

        private void LoadAndAddBackpack(string assetPath, int qty)
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinitionBase>(assetPath);
            if (item != null) _database.BackpackItems.Add(new InitialItemEntry(item, qty));
        }

        private void LoadAndSetGear(EquipmentSlot slot, string assetPath)
        {
            var gear = AssetDatabase.LoadAssetAtPath<GearDefinition>(assetPath);
            if (gear != null) _database.SetGearForSlot(slot, gear);
        }
    }
}
#endif
