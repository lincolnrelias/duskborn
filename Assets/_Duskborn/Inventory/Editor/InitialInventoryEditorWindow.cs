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
    /// Janela utilitária de gerenciamento centralizado do inventário inicial do Duskborn.
    /// Permite inspecionar, auditar, configurar quantidades e equipar itens de mochila,
    /// barra de ação rápida, equipamentos vestidos e relíquias em um único painel intuitivo.
    /// Funcionalmente semelhante ao SoundSettingsEditorWindow.
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

        // Cache do Catálogo de Itens
        private List<ItemDefinitionBase> _allProjectItems = new();
        private List<ItemDefinition> _allProjectRelics = new();
        private string _catalogSearch = "";
        private int _catalogCategoryFilter = 0;
        private Vector2 _catalogScrollPos;
        private bool _catalogInitialized = false;

        private static readonly string[] TabNames = new[]
        {
            "🎒 Mochila",
            "⚔️ Barra de Ação",
            "🛡️ Equipamento",
            "🔮 Relíquias",
            "📦 Catálogo",
            "⚙️ Presets"
        };

        private static readonly string[] CatalogCategoryNames = new[]
        {
            "Todos",
            "Armas",
            "Equipamento",
            "Materiais",
            "Consumíveis",
            "Relíquias"
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
                EditorGUILayout.HelpBox("Nenhum InitialInventoryDatabase encontrado. Clique no botão abaixo para criar o banco central.", MessageType.Warning);
                if (GUILayout.Button("Criar InitialInventoryDatabase Central", GUILayout.Height(35)))
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

            if (GUILayout.Button("📥 Puxar dos Prefabs", EditorStyles.miniButton, GUILayout.Width(130)))
            {
                if (EditorUtility.DisplayDialog("Importar Itens dos Prefabs",
                    "Deseja importar a configuração atual dos prefabs de Player e InventoryUI para este banco de dados?", "Sim", "Cancelar"))
                {
                    SyncFromPrefabs();
                    _serializedDb.Update();
                }
            }

            if (GUILayout.Button("📤 Aplicar aos Prefabs", EditorStyles.miniButton, GUILayout.Width(130)))
            {
                if (EditorUtility.DisplayDialog("Aplicar aos Prefabs",
                    "Deseja gravar as configurações atuais diretamente nos arquivos de prefab (InventoryUI.prefab e Player.prefab)?", "Sim", "Cancelar"))
                {
                    _serializedDb.ApplyModifiedProperties();
                    ApplyToPrefabs();
                }
            }

            if (GUILayout.Button("🔄 Auto-Configurar", EditorStyles.miniButton, GUILayout.Width(115)))
            {
                if (EditorUtility.DisplayDialog("Auto-Configurar Itens Iniciais",
                    "Deseja preencher automaticamente o banco com o conjunto inicial padrão recomendado (Ferramentas, Recursos e Armadura Básica)?", "Sim", "Cancelar"))
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
                $"Mochila: {backpackCount} | Barra: {actionBarCount} | Equipamento: {gearCount} | Relíquias: {relicsCount}",
                EditorStyles.miniLabel);

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("💾 Salvar Alterações", GUILayout.Width(150), GUILayout.Height(24)))
            {
                _serializedDb.ApplyModifiedProperties();
                EditorUtility.SetDirty(_database);
                AssetDatabase.SaveAssets();
                ShowNotification(new GUIContent("Configurações salvas com sucesso!"));
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        // ── 0. Mochila (Backpack Items) ───────────────────────────────────────

        private void DrawBackpackTab()
        {
            DrawSectionHeader("Mochila (Backpack / Grid Principal)",
                "Configura os itens e quantidades recebidos na bolsa/mochila do jogador ao iniciar a partida.");

            var prop = _serializedDb.FindProperty("backpackItems");
            if (prop == null) return;

            DrawDragAndDropBox("Arraste itens do Project aqui para adicionar à Mochila", (itemDef) =>
            {
                AddItemToEntryList(prop, itemDef, 1);
            });

            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Itens na Mochila ({prop.arraySize})", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("+ Adicionar Novo Item", EditorStyles.miniButton, GUILayout.Width(150)))
            {
                prop.InsertArrayElementAtIndex(prop.arraySize);
                var elem = prop.GetArrayElementAtIndex(prop.arraySize - 1);
                elem.FindPropertyRelative("item").objectReferenceValue = null;
                elem.FindPropertyRelative("quantity").intValue = 1;
            }

            if (prop.arraySize > 0 && GUILayout.Button("Limpar Tudo", EditorStyles.miniButton, GUILayout.Width(90)))
            {
                if (EditorUtility.DisplayDialog("Limpar Mochila", "Deseja remover todos os itens da mochila inicial?", "Sim", "Não"))
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

        // ── 1. Barra de Ação (Action Bar) ─────────────────────────────────────

        private void DrawActionBarTab()
        {
            DrawSectionHeader("Barra de Ação Rápida (Hotbar / Atalhos 1 a 5)",
                "Configura os itens e armas equipados nos atalhos rápidos da interface principal.");

            var prop = _serializedDb.FindProperty("actionBarItems");
            if (prop == null) return;

            DrawDragAndDropBox("Arraste itens do Project aqui para adicionar à Barra de Ação", (itemDef) =>
            {
                AddItemToEntryList(prop, itemDef, 1);
            });

            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Slots da Barra de Ação ({prop.arraySize})", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("+ Adicionar ao Atalho", EditorStyles.miniButton, GUILayout.Width(150)))
            {
                prop.InsertArrayElementAtIndex(prop.arraySize);
                var elem = prop.GetArrayElementAtIndex(prop.arraySize - 1);
                elem.FindPropertyRelative("item").objectReferenceValue = null;
                elem.FindPropertyRelative("quantity").intValue = 1;
            }

            if (prop.arraySize > 0 && GUILayout.Button("Limpar Barra", EditorStyles.miniButton, GUILayout.Width(90)))
            {
                if (EditorUtility.DisplayDialog("Limpar Barra", "Deseja remover todos os itens da barra inicial?", "Sim", "Não"))
                {
                    prop.ClearArray();
                }
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);

            for (int i = 0; i < prop.arraySize; i++)
            {
                var elem = prop.GetArrayElementAtIndex(i);
                DrawItemEntryCard(elem, i, prop, slotPrefix: $"Atalho [{i + 1}]");
            }
        }

        // ── 2. Equipamento Vestido (Equipped Gear) ────────────────────────────

        private void DrawEquipmentTab()
        {
            DrawSectionHeader("Equipamento Vestido Inicial (Starting Gear)",
                "Configura armaduras, anéis e adornos vestidos diretamente nos slots de equipamento do personagem.");

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

            // Ícone do Slot
            Texture2D slotIcon = LoadSlotIcon(slot);
            if (slotIcon != null)
            {
                GUILayout.Label(new GUIContent(slotIcon), GUILayout.Width(34), GUILayout.Height(34));
            }
            else
            {
                GUILayout.Box("🛡️", GUILayout.Width(34), GUILayout.Height(34));
            }

            // Nome do Slot em Português
            EditorGUILayout.BeginVertical(GUILayout.Width(130));
            EditorGUILayout.LabelField(GetSlotDisplayName(slot), EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Slot: {slot}", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            // Campo de Seleção do Gear
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

            // Botão Limpar Slot
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

            // Detalhes do item equipado se presente
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
                    EditorGUILayout.HelpBox($"Atenção: Este item declara o slot '{currentGear.Slot}', mas está alocado em '{slot}'.", MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        // ── 3. Relíquias & Buffs Iniciais ─────────────────────────────────────

        private void DrawRelicsTab()
        {
            DrawSectionHeader("Relíquias & Buffs Roguelite Iniciais",
                "Define os talismãs e elixires de bônus passivo permanente concedidos ao jogador ao iniciar a jornada.");

            var relicsProp = _serializedDb.FindProperty("startingRelics");
            if (relicsProp == null) return;

            DrawDragAndDropBox("Arraste itens de relíquia (ItemDefinition) aqui", (itemDef) =>
            {
                // Handled if relic dragged
            });

            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Relíquias Ativas ({relicsProp.arraySize})", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("+ Adicionar Relíquia", EditorStyles.miniButton, GUILayout.Width(150)))
            {
                relicsProp.InsertArrayElementAtIndex(relicsProp.arraySize);
                relicsProp.GetArrayElementAtIndex(relicsProp.arraySize - 1).objectReferenceValue = null;
            }

            if (relicsProp.arraySize > 0 && GUILayout.Button("Limpar Todas", EditorStyles.miniButton, GUILayout.Width(90)))
            {
                if (EditorUtility.DisplayDialog("Limpar Relíquias", "Deseja remover todas as relíquias iniciais?", "Sim", "Não"))
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

        // ── 4. Catálogo de Itens ──────────────────────────────────────────────

        private void DrawCatalogTab()
        {
            DrawSectionHeader("Catálogo Geral de Itens do Duskborn",
                "Pesquise todos os itens registrados no projeto e adicione-os com um clique à Mochila, Barra de Ação ou Equipamentos.");

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

            if (GUILayout.Button("🔄 Atualizar", EditorStyles.toolbarButton, GUILayout.Width(75)))
            {
                RefreshCatalog();
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(6);

            _catalogScrollPos = EditorGUILayout.BeginScrollView(_catalogScrollPos, GUILayout.MinHeight(300));

            // Desenha itens de ItemDefinitionBase
            int displayedCount = 0;
            string searchLower = _catalogSearch.Trim().ToLowerInvariant();

            if (_catalogCategoryFilter != 5) // Se não for categoria exclusiva de relíquias
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

            // Desenha relíquias (ItemDefinition) se "Todos" ou "Relíquias"
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
                EditorGUILayout.HelpBox("Nenhum item encontrado com os critérios de filtro pesquisados.", MessageType.Info);
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

            string desc = !string.IsNullOrEmpty(item.Description) ? item.Description.Replace("\n", " ") : "Sem descrição";
            if (desc.Length > 90) desc = desc.Substring(0, 87) + "...";
            EditorGUILayout.LabelField(desc, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            // Botões de ação rápida
            if (GUILayout.Button("+ Mochila", EditorStyles.miniButton, GUILayout.Width(75)))
            {
                var prop = _serializedDb.FindProperty("backpackItems");
                AddItemToEntryList(prop, item, 1);
                _serializedDb.ApplyModifiedProperties();
                ShowNotification(new GUIContent($"Adicionado à Mochila: {item.DisplayName}"));
            }

            if (GUILayout.Button("+ Barra", EditorStyles.miniButton, GUILayout.Width(65)))
            {
                var prop = _serializedDb.FindProperty("actionBarItems");
                AddItemToEntryList(prop, item, 1);
                _serializedDb.ApplyModifiedProperties();
                ShowNotification(new GUIContent($"Adicionado à Barra: {item.DisplayName}"));
            }

            if (item is GearDefinition gear)
            {
                if (GUILayout.Button("+ Equipar", EditorStyles.miniButton, GUILayout.Width(70)))
                {
                    _database.SetGearForSlot(gear.Slot, gear);
                    _serializedDb.Update();
                    EditorUtility.SetDirty(_database);
                    ShowNotification(new GUIContent($"Equipado no slot {gear.Slot}: {gear.DisplayName}"));
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
                $"<color={rarityHex}><b>{relic.ItemName}</b></color> <color=#888888>(Relíquia Roguelite)</color>",
                new GUIStyle(EditorStyles.boldLabel) { richText = true });

            string effectText = $"{relic.EffectType} ({relic.EffectMode}): +{relic.EffectValue}";
            EditorGUILayout.LabelField(effectText, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("+ Relíquia", EditorStyles.miniButton, GUILayout.Width(80)))
            {
                var prop = _serializedDb.FindProperty("startingRelics");
                prop.InsertArrayElementAtIndex(prop.arraySize);
                prop.GetArrayElementAtIndex(prop.arraySize - 1).objectReferenceValue = relic;
                _serializedDb.ApplyModifiedProperties();
                ShowNotification(new GUIContent($"Adicionada relíquia: {relic.ItemName}"));
            }

            if (GUILayout.Button("🔍", EditorStyles.miniButton, GUILayout.Width(26)))
            {
                EditorGUIUtility.PingObject(relic);
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        // ── 5. Presets & Operações Globais ────────────────────────────────────

        private void DrawPresetsTab()
        {
            DrawSectionHeader("Presets & Carregamentos Rápidos",
                "Carregue configurações pré-definidas para acelerar testes com arquétipos e diferentes estilos de jogo.");

            DrawSectionBox("⚔️ Preset: Guerreiro Balanceado", () =>
            {
                EditorGUILayout.LabelField("Barra de Ação: Machado de Pedra, Espada de Ferro, 3x Maçã");
                EditorGUILayout.LabelField("Mochila: 15x Madeira, 15x Pedra, 5x Barra de Ferro");
                EditorGUILayout.LabelField("Equipamento: Capacete de Ferro, Armadura de Couro, Botas Gastas, Colar de Osso, Anel de Cobre");
                EditorGUILayout.Space(4);
                if (GUILayout.Button("Aplicar Preset Guerreiro", GUILayout.Height(26)))
                {
                    ApplyWarriorPreset();
                }
            });

            EditorGUILayout.Space(6);
            DrawSectionBox("⛏️ Preset: Coletor / Construtor", () =>
            {
                EditorGUILayout.LabelField("Barra de Ação: Machado de Pedra, Picareta de Pedra, 5x Maçã");
                EditorGUILayout.LabelField("Mochila: 30x Madeira, 30x Pedra, 15x Fibra");
                EditorGUILayout.LabelField("Equipamento: Armadura de Couro, Botas Gastas");
                EditorGUILayout.Space(4);
                if (GUILayout.Button("Aplicar Preset Coletor", GUILayout.Height(26)))
                {
                    ApplyGathererPreset();
                }
            });

            EditorGUILayout.Space(6);
            DrawSectionBox("🧪 Preset: Sobrevivente / Alquimista", () =>
            {
                EditorGUILayout.LabelField("Barra de Ação: Machado de Pedra, 5x Maçã");
                EditorGUILayout.LabelField("Mochila: 10x Cristal Arcano, 10x Pó de Cristal, 20x Madeira");
                EditorGUILayout.LabelField("Equipamento: Botas de Vento, Armadura de Couro");
                EditorGUILayout.Space(4);
                if (GUILayout.Button("Aplicar Preset Alquimista", GUILayout.Height(26)))
                {
                    ApplyAlchemistPreset();
                }
            });

            EditorGUILayout.Space(6);
            DrawSectionBox("🗑️ Limpar Todos os Itens Iniciais", () =>
            {
                EditorGUILayout.LabelField("Remove todos os itens de mochila, barra, equipamentos e relíquias para testar início limpo.");
                EditorGUILayout.Space(4);
                if (GUILayout.Button("Limpar Tudo (Início Vazio)", GUILayout.Height(26)))
                {
                    if (EditorUtility.DisplayDialog("Limpar Inventário Inicial", "Tem certeza que deseja esvaziar todos os itens?", "Sim", "Cancelar"))
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

            // Prefixo opcional
            if (!string.IsNullOrEmpty(slotPrefix))
            {
                EditorGUILayout.LabelField(slotPrefix, EditorStyles.boldLabel, GUILayout.Width(80));
            }

            // ObjectField
            EditorGUILayout.PropertyField(itemProp, GUIContent.none);

            // Quantidade com botões rápidos [-] e [+]
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

            // Controles de Ordenação e Remoção
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

            // Linha de Detalhes do Item
            if (currentItem != null)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(42);
                string rarityHex = GetRarityColorHex(currentItem.Rarity);
                string itemType = GetItemTypeDisplayName(currentItem);
                EditorGUILayout.LabelField(
                    $"<color={rarityHex}><b>{currentItem.DisplayName}</b> ({currentItem.Rarity})</color> — Tipo: {itemType}",
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
                0 => true, // Todos
                1 => item is WeaponDefinition,
                2 => item is GearDefinition,
                3 => item is MaterialDefinition,
                4 => item is ConsumableDefinition || item.GetType().Name.Contains("Apple"),
                _ => true
            };
        }

        private static string GetItemTypeDisplayName(ItemDefinitionBase item)
        {
            if (item is WeaponDefinition) return "Arma";
            if (item is GearDefinition g) return $"Equipamento ({g.Slot})";
            if (item is MaterialDefinition) return "Material / Recurso";
            if (item is ConsumableDefinition) return "Consumível";
            return "Item Geral";
        }

        private static string GetSlotDisplayName(EquipmentSlot slot)
        {
            return slot switch
            {
                EquipmentSlot.Head => "Capacete",
                EquipmentSlot.Neck => "Colar / Amuleto",
                EquipmentSlot.Shoulder => "Ombreiras",
                EquipmentSlot.Back => "Capa",
                EquipmentSlot.Chest => "Peitoral",
                EquipmentSlot.Wrist => "Braçadeiras",
                EquipmentSlot.Hands => "Luvas",
                EquipmentSlot.Waist => "Cinto",
                EquipmentSlot.Legs => "Calças / Perneiras",
                EquipmentSlot.Feet => "Botas",
                EquipmentSlot.Ring1 => "Anel 1",
                EquipmentSlot.Ring2 => "Anel 2",
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
            if (gear == null || gear.Bonuses == null || gear.Bonuses.Count == 0) return "Sem bônus";
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
            // 1. Puxar Mochila do InventoryUI.prefab
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

            // 2. Puxar Equipamentos do Player.prefab
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
            ShowNotification(new GUIContent("Itens importados dos prefabs com sucesso!"));
        }

        private void ApplyToPrefabs()
        {
            int modifiedCount = 0;

            // 1. Grava no InventoryUI.prefab
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

            // 2. Grava nos Player prefabs
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
            ShowNotification(new GUIContent($"Gravado com sucesso em {modifiedCount} prefabs!"));
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
            ShowNotification(new GUIContent("Preset Guerreiro Aplicado!"));
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
            ShowNotification(new GUIContent("Preset Coletor Aplicado!"));
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
            ShowNotification(new GUIContent("Preset Alquimista Aplicado!"));
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
