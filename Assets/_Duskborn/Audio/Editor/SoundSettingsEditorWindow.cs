#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Duskborn.Audio.Editor
{
    /// <summary>
    /// Janela de gerenciamento centralizado de áudio do Duskborn.
    /// Permite inspecionar, auditar (preview em tempo real), configurar volumes e substituir
    /// todos os clipes de som do jogo em um único dashboard intuitivo.
    /// </summary>
    public class SoundSettingsEditorWindow : EditorWindow
    {
        [MenuItem("Tools/Duskborn/Sound Settings", priority = 100)]
        [MenuItem("Window/Duskborn/Sound Settings", priority = 100)]
        public static void Open()
        {
            var window = GetWindow<SoundSettingsEditorWindow>("Sound Settings");
            window.minSize = new Vector2(650, 520);
            window.Show();
        }

        private AudioDatabase _database;
        private SerializedObject _serializedDb;
        private Vector2 _scrollPos;
        private int _currentTab = 0;

        private static readonly string[] TabNames = new[]
        {
            "🪓 Recursos",
            "🏃 Jogador",
            "⚔️ Combate",
            "👾 Inimigos",
            "📦 Loot & Baús",
            "🖥️ Interface",
            "🎵 Música",
            "⚙️ Global"
        };

        private void OnEnable()
        {
            EnsureDatabase();
        }

        private void OnDisable()
        {
            AudioPreviewUtility.StopAll();
        }

        private void EnsureDatabase()
        {
            if (_database == null)
            {
                _database = AudioDatabase.Instance;
            }

            if (_database != null)
            {
                _serializedDb = new SerializedObject(_database);
            }
        }

        private void OnGUI()
        {
            EnsureDatabase();

            if (_database == null || _serializedDb == null)
            {
                EditorGUILayout.HelpBox("Nenhum AudioDatabase encontrado. Clique no botão abaixo para criar.", MessageType.Warning);
                if (GUILayout.Button("Criar AudioDatabase Central", GUILayout.Height(35)))
                {
                    _database = AudioDatabase.CreateDefaultAsset();
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
                case 0: DrawResourcesTab(); break;
                case 1: DrawPlayerTab(); break;
                case 2: DrawCombatTab(); break;
                case 3: DrawEnemiesTab(); break;
                case 4: DrawLootTab(); break;
                case 5: DrawUiTab(); break;
                case 6: DrawMusicTab(); break;
                case 7: DrawGlobalTab(); break;
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

            GUILayout.Label("🔊 Duskborn — Central Sound Settings", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("⏹ Parar Áudios", EditorStyles.miniButton, GUILayout.Width(100)))
            {
                AudioPreviewUtility.StopAll();
            }

            if (GUILayout.Button("🔍 Ping Asset", EditorStyles.miniButton, GUILayout.Width(90)))
            {
                EditorGUIUtility.PingObject(_database);
                Selection.activeObject = _database;
            }

            if (GUILayout.Button("🔄 Auto-Vincular", EditorStyles.miniButton, GUILayout.Width(110)))
            {
                if (EditorUtility.DisplayDialog("Auto-Vincular Clipes",
                    "Deseja preencher automaticamente todos os campos vazios com os clipes detectados em Art/SFX e Audio/Music?", "Sim", "Cancelar"))
                {
                    _database.AutoPopulateDefaults();
                    _serializedDb.Update();
                    AudioPreviewUtility.StopAll();
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

            EditorGUILayout.LabelField($"Asset: Assets/_Duskborn/Resources/Audio/{_database.name}.asset", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("💾 Salvar Alterações", GUILayout.Width(140), GUILayout.Height(24)))
            {
                _serializedDb.ApplyModifiedProperties();
                EditorUtility.SetDirty(_database);
                AssetDatabase.SaveAssets();
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        // ── 0. Recursos e Nós de Coleta ───────────────────────────────────────

        private void DrawResourcesTab()
        {
            DrawSectionHeader("Coleta & Destruição de Nós de Recursos",
                "Configura os sons orgânicos de quebra e depleção ao esgotar veios de pedra, minério e árvores.");

            var resProp = _serializedDb.FindProperty("resources");
            if (resProp == null) return;

            DrawClipArray(resProp.FindPropertyRelative("rockShatterClips"), "🪨 Quebra de Pedra (Rock Shatter)");
            DrawClipArray(resProp.FindPropertyRelative("oreShatterClips"), "⛏️ Quebra de Minério (Ore Shatter)");
            DrawClipArray(resProp.FindPropertyRelative("treeFallClips"), "🌲 Queda de Árvore (Tree Fall)");

            EditorGUILayout.Space(6);
            DrawSectionBox("Parâmetros 3D & Volume", () =>
            {
                EditorGUILayout.Slider(resProp.FindPropertyRelative("depletedVolume"), 0f, 1f, "Volume de Depleção");
                EditorGUILayout.PropertyField(resProp.FindPropertyRelative("minDistance"), new GUIContent("Distância Mínima (3D)"));
                EditorGUILayout.PropertyField(resProp.FindPropertyRelative("maxDistance"), new GUIContent("Distância Máxima (3D)"));
            });
        }

        // ── 1. Jogador e Movimentação ─────────────────────────────────────────

        private void DrawPlayerTab()
        {
            DrawSectionHeader("Jogador: Passos, Locomoção & Vitalidade",
                "Configura os passos por superfície, saltos, impactos de dano, morte e a batida cardíaca de perigo crítico.");

            var pProp = _serializedDb.FindProperty("player");
            if (pProp == null) return;

            DrawClipArray(pProp.FindPropertyRelative("grassSteps"), "🌱 Passos na Grama (Grass)");
            DrawClipArray(pProp.FindPropertyRelative("stoneSteps"), "🧱 Passos na Pedra/Metal (Stone/Metal)");

            EditorGUILayout.Space(6);
            DrawSectionBox("Salto e Queda", () =>
            {
                DrawSingleClipWithPreview(pProp.FindPropertyRelative("jumpClip"), "Salto (Takeoff)");
                DrawSingleClipWithPreview(pProp.FindPropertyRelative("landClip"), "Aterrissagem (Land)");
                EditorGUILayout.Slider(pProp.FindPropertyRelative("footstepVolume"), 0f, 1f, "Volume dos Passos");
                EditorGUILayout.Slider(pProp.FindPropertyRelative("jumpVolume"), 0f, 1f, "Volume do Salto");
                EditorGUILayout.Slider(pProp.FindPropertyRelative("landVolume"), 0f, 1f, "Volume da Queda");
            });

            EditorGUILayout.Space(6);
            DrawSectionBox("Vitalidade & Dano", () =>
            {
                DrawClipArray(pProp.FindPropertyRelative("hurtClips"), "🩸 Gemidos de Dano (Hurt Grunts)");
                DrawSingleClipWithPreview(pProp.FindPropertyRelative("deathClip"), "Morte do Jogador");
                DrawSingleClipWithPreview(pProp.FindPropertyRelative("heartbeatLoopClip"), "Batimento Cardíaco (Loop)");

                EditorGUILayout.Slider(pProp.FindPropertyRelative("hurtVolume"), 0f, 1f, "Volume de Dano");
                EditorGUILayout.Slider(pProp.FindPropertyRelative("deathVolume"), 0f, 1f, "Volume de Morte");
                EditorGUILayout.Slider(pProp.FindPropertyRelative("lowHpThreshold"), 0.1f, 0.5f, "Gatilho de Vida Baixa (Heartbeat)");
                EditorGUILayout.Slider(pProp.FindPropertyRelative("pitchVariation"), 0f, 0.2f, "Variação Orgânica de Pitch");
            });
        }

        // ── 2. Combate e Superfícies ─────────────────────────────────────────

        private void DrawCombatTab()
        {
            DrawSectionHeader("Combate: Impactos de Superfície & Swings",
                "Define os sons de impacto padrão por superfície caso uma arma ou skill não possua override específico.");

            var cProp = _serializedDb.FindProperty("combat");
            if (cProp == null) return;

            DrawClipArray(cProp.FindPropertyRelative("woodHitClips"), "🪵 Impacto em Madeira/Árvore");
            DrawClipArray(cProp.FindPropertyRelative("stoneHitClips"), "🪨 Impacto em Pedra");
            DrawClipArray(cProp.FindPropertyRelative("metalHitClips"), "🛡️ Impacto em Metal/Minério");
            DrawClipArray(cProp.FindPropertyRelative("fleshHitClips"), "🥩 Impacto em Carne/Inimigo");
            DrawClipArray(cProp.FindPropertyRelative("defaultHitClips"), "⚪ Impacto Padrão (Fallback)");

            EditorGUILayout.Space(6);
            DrawClipArray(cProp.FindPropertyRelative("lightSwingClips"), "🗡️ Golpes Rápidos no Ar (Light Swings)");
            DrawClipArray(cProp.FindPropertyRelative("heavySwingClips"), "🪓 Golpes Pesados no Ar (Heavy Swings)");

            EditorGUILayout.Space(6);
            DrawSectionBox("Perfis de Armas Registrados", () =>
            {
                var profilesProp = cProp.FindPropertyRelative("registeredProfiles");
                if (profilesProp != null)
                {
                    EditorGUILayout.PropertyField(profilesProp, new GUIContent("Perfis Ativos"), true);
                }
            });
        }

        // ── 3. Inimigos ──────────────────────────────────────────────────────

        private void DrawEnemiesTab()
        {
            DrawSectionHeader("Inimigos: Vocalizações & Feedback",
                "Configura os sons 3D de ataque, dano e morte para arquétipos de monstros.");

            var eProp = _serializedDb.FindProperty("enemies");
            if (eProp == null) return;

            DrawSectionBox("Inimigo: Swarmer", () =>
            {
                DrawClipArray(eProp.FindPropertyRelative("swarmerAttackClips"), "⚔️ Ataques / Mordidas");
                DrawClipArray(eProp.FindPropertyRelative("swarmerHurtClips"), "💥 Guinchos de Dano");
                DrawClipArray(eProp.FindPropertyRelative("swarmerDeathClips"), "☠️ Morte Visceral");

                EditorGUILayout.Space(4);
                EditorGUILayout.Slider(eProp.FindPropertyRelative("attackVolume"), 0f, 1f, "Volume de Ataque");
                EditorGUILayout.Slider(eProp.FindPropertyRelative("hurtVolume"), 0f, 1f, "Volume de Dano");
                EditorGUILayout.Slider(eProp.FindPropertyRelative("deathVolume"), 0f, 1f, "Volume de Morte");
                EditorGUILayout.PropertyField(eProp.FindPropertyRelative("minDistance"), new GUIContent("Distância Mínima (3D)"));
                EditorGUILayout.PropertyField(eProp.FindPropertyRelative("maxDistance"), new GUIContent("Distância Máxima (3D)"));
            });
        }

        // ── 4. Loot e Interações ─────────────────────────────────────────────

        private void DrawLootTab()
        {
            DrawSectionHeader("Loot, Baús & Coletáveis",
                "Configura os sons de abertura de baús e coleta de ouro e recursos no chão.");

            var lProp = _serializedDb.FindProperty("loot");
            if (lProp == null) return;

            DrawSectionBox("Efeitos de Coleta", () =>
            {
                DrawSingleClipWithPreview(lProp.FindPropertyRelative("goldPickupClip"), "🪙 Coleta de Ouro");
                EditorGUILayout.Slider(lProp.FindPropertyRelative("goldVolume"), 0f, 1f, "Volume do Ouro");

                EditorGUILayout.Space(6);
                DrawSingleClipWithPreview(lProp.FindPropertyRelative("itemPickupClip"), "🎒 Coleta de Itens/Recursos");
                EditorGUILayout.Slider(lProp.FindPropertyRelative("itemVolume"), 0f, 1f, "Volume do Item");

                EditorGUILayout.Space(6);
                DrawSingleClipWithPreview(lProp.FindPropertyRelative("chestOpenClip"), "📦 Abertura de Baú");
                EditorGUILayout.Slider(lProp.FindPropertyRelative("chestVolume"), 0f, 1f, "Volume do Baú");
            });
        }

        // ── 5. Interface de Usuário (UI) ──────────────────────────────────────

        private void DrawUiTab()
        {
            DrawSectionHeader("Interface de Usuário (UI)",
                "Configura os efeitos sonoros táteis de cliques de botões, modais, criação de itens e avisos de erro.");

            var uProp = _serializedDb.FindProperty("ui");
            if (uProp == null) return;

            DrawSectionBox("Navegação & Modais", () =>
            {
                DrawSingleClipWithPreview(uProp.FindPropertyRelative("buttonClickClip"), "🖱️ Clique de Botão");
                EditorGUILayout.Slider(uProp.FindPropertyRelative("buttonClickVolume"), 0f, 1f, "Volume do Clique");

                EditorGUILayout.Space(6);
                DrawSingleClipWithPreview(uProp.FindPropertyRelative("modalOpenClip"), "📂 Abertura de Janela/Modal");
                EditorGUILayout.Slider(uProp.FindPropertyRelative("modalOpenVolume"), 0f, 1f, "Volume do Modal");

                EditorGUILayout.Space(6);
                DrawSingleClipWithPreview(uProp.FindPropertyRelative("craftSuccessClip"), "⚒️ Sucesso de Fabricação (Craft)");
                EditorGUILayout.Slider(uProp.FindPropertyRelative("craftSuccessVolume"), 0f, 1f, "Volume de Craft");

                EditorGUILayout.Space(6);
                DrawSingleClipWithPreview(uProp.FindPropertyRelative("errorClip"), "⚠️ Som de Erro / Ação Inválida");
                EditorGUILayout.Slider(uProp.FindPropertyRelative("errorVolume"), 0f, 1f, "Volume de Erro");
            });
        }

        // ── 6. Música e Atmosfera ─────────────────────────────────────────────

        private void DrawMusicTab()
        {
            DrawSectionHeader("Música & Atmosfera Dinâmica",
                "Configura as trilhas sonoras e stingers com suporte a crossfade adaptativo.");

            var mProp = _serializedDb.FindProperty("music");
            if (mProp == null) return;

            DrawSectionBox("Trilhas Musicais", () =>
            {
                DrawSingleClipWithPreview(mProp.FindPropertyRelative("menuMusic"), "Menu Principal (Loop)");
                DrawSingleClipWithPreview(mProp.FindPropertyRelative("dayMusic"), "Exploração Diurna (Loop)");
                DrawSingleClipWithPreview(mProp.FindPropertyRelative("nightMusic"), "Combate Noturno (Loop)");

                EditorGUILayout.Space(4);
                EditorGUILayout.PropertyField(mProp.FindPropertyRelative("musicFadeDuration"), new GUIContent("Duração do Crossfade (s)"));
            });

            EditorGUILayout.Space(6);
            DrawSectionBox("Stingers e Berrantes de Transição", () =>
            {
                DrawSingleClipWithPreview(mProp.FindPropertyRelative("dawnHorn"), "📯 Berrante do Alvorecer (Dawn)");
                DrawSingleClipWithPreview(mProp.FindPropertyRelative("nightHorn"), "📯 Berrante da Noite (Night)");
                EditorGUILayout.Slider(mProp.FindPropertyRelative("hornVolume"), 0f, 1f, "Volume dos Berrantes");
            });
        }

        // ── 7. Global & Volumes ───────────────────────────────────────────────

        private void DrawGlobalTab()
        {
            DrawSectionHeader("Canais de Volume & AudioManager",
                "Visão geral dos barramentos de áudio e configurações persistentes.");

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Barramentos Padrão (AudioManager):", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("• Master: 1.0 (Canal mestre)");
            EditorGUILayout.LabelField("• Music:  0.75 (Trilhas e ambiente sonoro)");
            EditorGUILayout.LabelField("• SFX:    0.90 (Combate, passos, destruição e interações)");
            EditorGUILayout.LabelField("• UI:     0.85 (Feedback tátil de interface)");
            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox("Os volumes são automaticamente persistidos em PlayerPrefs pelo AudioManager durante a execução do jogo.", MessageType.Info);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);
            DrawSectionBox("Operações do Banco de Dados", () =>
            {
                if (GUILayout.Button("Redefinir Tudo para Clipes Padrão do Projeto", GUILayout.Height(30)))
                {
                    if (EditorUtility.DisplayDialog("Redefinir Áudios", "Tem certeza que deseja restaurar as configurações padrão?", "Sim", "Não"))
                    {
                        _database.AutoPopulateDefaults();
                        _serializedDb.Update();
                    }
                }
            });
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

        private void DrawSingleClipWithPreview(SerializedProperty clipProp, string label)
        {
            if (clipProp == null) return;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(clipProp, new GUIContent(label));

            var clip = clipProp.objectReferenceValue as AudioClip;
            GUI.enabled = clip != null;

            if (GUILayout.Button("▶", EditorStyles.miniButtonLeft, GUILayout.Width(26), GUILayout.Height(18)))
            {
                AudioPreviewUtility.PlayClip(clip);
            }

            if (GUILayout.Button("⏹", EditorStyles.miniButtonRight, GUILayout.Width(26), GUILayout.Height(18)))
            {
                AudioPreviewUtility.StopAll();
            }

            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
        }

        private void DrawClipArray(SerializedProperty arrayProp, string title)
        {
            if (arrayProp == null) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            arrayProp.isExpanded = EditorGUILayout.Foldout(arrayProp.isExpanded, $"{title} ({arrayProp.arraySize} variações)", true);
            GUILayout.FlexibleSpace();

            if (arrayProp.arraySize > 0)
            {
                if (GUILayout.Button("🎲 Variação Aleatória", EditorStyles.miniButton, GUILayout.Width(130)))
                {
                    int idx = UnityEngine.Random.Range(0, arrayProp.arraySize);
                    var elem = arrayProp.GetArrayElementAtIndex(idx);
                    if (elem.objectReferenceValue is AudioClip c)
                    {
                        AudioPreviewUtility.PlayClip(c);
                    }
                }
            }

            EditorGUILayout.EndHorizontal();

            if (arrayProp.isExpanded)
            {
                EditorGUI.indentLevel++;
                int newSize = EditorGUILayout.IntField("Tamanho", arrayProp.arraySize);
                if (newSize != arrayProp.arraySize && newSize >= 0)
                {
                    arrayProp.arraySize = newSize;
                }

                for (int i = 0; i < arrayProp.arraySize; i++)
                {
                    var elem = arrayProp.GetArrayElementAtIndex(i);
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PropertyField(elem, new GUIContent($"Variação {i + 1}"));

                    var clip = elem.objectReferenceValue as AudioClip;
                    GUI.enabled = clip != null;

                    if (GUILayout.Button("▶", EditorStyles.miniButtonLeft, GUILayout.Width(26), GUILayout.Height(18)))
                    {
                        AudioPreviewUtility.PlayClip(clip);
                    }

                    if (GUILayout.Button("⏹", EditorStyles.miniButtonRight, GUILayout.Width(26), GUILayout.Height(18)))
                    {
                        AudioPreviewUtility.StopAll();
                    }

                    GUI.enabled = true;
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }
    }
}
#endif
