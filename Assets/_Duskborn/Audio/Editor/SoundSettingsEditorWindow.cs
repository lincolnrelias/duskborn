#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Duskborn.Audio.Editor
{
    /// <summary>
    /// Centralized Duskborn audio management window.
    /// Allows inspection, auditing (real-time previews), volume configuration, and replacement of
    /// all game sound clips in one intuitive dashboard.
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
            "🪓 Resources",
            "🏃 Player",
            "⚔️ Combat",
            "👾 Enemies",
            "📦 Loot & Chests",
            "🖥️ Interface",
            "🎵 Music",
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
                EditorGUILayout.HelpBox("No AudioDatabase found. Click the button below to create one.", MessageType.Warning);
                if (GUILayout.Button("Create Central AudioDatabase", GUILayout.Height(35)))
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

            if (GUILayout.Button("⏹ Stop Audio", EditorStyles.miniButton, GUILayout.Width(100)))
            {
                AudioPreviewUtility.StopAll();
            }

            if (GUILayout.Button("🔍 Ping Asset", EditorStyles.miniButton, GUILayout.Width(90)))
            {
                EditorGUIUtility.PingObject(_database);
                Selection.activeObject = _database;
            }

            if (GUILayout.Button("🔄 Auto-Link", EditorStyles.miniButton, GUILayout.Width(110)))
            {
                if (EditorUtility.DisplayDialog("Auto-Link Clips",
                    "Automatically fill all empty fields with clips detected in Art/SFX and Audio/Music?", "Yes", "Cancel"))
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

            if (GUILayout.Button("💾 Save Changes", GUILayout.Width(140), GUILayout.Height(24)))
            {
                _serializedDb.ApplyModifiedProperties();
                EditorUtility.SetDirty(_database);
                AssetDatabase.SaveAssets();
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        // 0. Resources and Gathering Nodes

        private void DrawResourcesTab()
        {
            DrawSectionHeader("Gathering & Resource Node Destruction",
                "Configure organic break and depletion sounds for exhausted stone, ore, and tree nodes.");

            var resProp = _serializedDb.FindProperty("resources");
            if (resProp == null) return;

            DrawClipArray(resProp.FindPropertyRelative("rockShatterClips"), "🪨 Rock Shatter");
            DrawClipArray(resProp.FindPropertyRelative("oreShatterClips"), "⛏️ Ore Shatter");
            DrawClipArray(resProp.FindPropertyRelative("treeFallClips"), "🌲 Tree Fall");

            EditorGUILayout.Space(6);
            DrawSectionBox("3D Parameters & Volume", () =>
            {
                EditorGUILayout.Slider(resProp.FindPropertyRelative("depletedVolume"), 0f, 1f, "Depletion Volume");
                EditorGUILayout.PropertyField(resProp.FindPropertyRelative("minDistance"), new GUIContent("Minimum Distance (3D)"));
                EditorGUILayout.PropertyField(resProp.FindPropertyRelative("maxDistance"), new GUIContent("Maximum Distance (3D)"));
            });
        }

        // 1. Player and Movement

        private void DrawPlayerTab()
        {
            DrawSectionHeader("Player: Footsteps, Locomotion & Vitality",
                "Configure footsteps by surface, jumps, damage impacts, death, and the critical danger heartbeat.");

            var pProp = _serializedDb.FindProperty("player");
            if (pProp == null) return;

            DrawClipArray(pProp.FindPropertyRelative("grassSteps"), "🌱 Grass Footsteps");
            DrawClipArray(pProp.FindPropertyRelative("dirtSteps"),  "🍂 Dirt / Sand Footsteps");
            DrawClipArray(pProp.FindPropertyRelative("stoneSteps"), "🧱 Rock / Stone / Metal Footsteps");
            DrawClipArray(pProp.FindPropertyRelative("waterSteps"), "🌊 Water Footsteps");

            EditorGUILayout.Space(6);
            DrawSectionBox("Jump and Fall", () =>
            {
                DrawSingleClipWithPreview(pProp.FindPropertyRelative("jumpClip"), "Jump (Takeoff)");
                DrawSingleClipWithPreview(pProp.FindPropertyRelative("landClip"), "Landing");
                EditorGUILayout.Slider(pProp.FindPropertyRelative("footstepVolume"), 0f, 1f, "Footstep Volume");
                EditorGUILayout.Slider(pProp.FindPropertyRelative("jumpVolume"), 0f, 1f, "Jump Volume");
                EditorGUILayout.Slider(pProp.FindPropertyRelative("landVolume"), 0f, 1f, "Landing Volume");
            });

            EditorGUILayout.Space(6);
            DrawSectionBox("🌊 Water Interaction (Movement & Splash)", () =>
            {
                DrawSingleClipWithPreview(pProp.FindPropertyRelative("waterWadeLoop"), "Water Movement Loop (Wading)");
                DrawSingleClipWithPreview(pProp.FindPropertyRelative("waterEnterSplashClip"), "Water Entry Splash");
                EditorGUILayout.Slider(pProp.FindPropertyRelative("waterWadeVolume"), 0f, 1f, "Water Movement Volume");
                EditorGUILayout.Slider(pProp.FindPropertyRelative("waterSplashVolume"), 0f, 1f, "Water Splash Volume");
            });

            EditorGUILayout.Space(6);
            DrawSectionBox("Vitality & Damage", () =>
            {
                DrawClipArray(pProp.FindPropertyRelative("hurtClips"), "🩸 Hurt Grunts");
                DrawSingleClipWithPreview(pProp.FindPropertyRelative("deathClip"), "Player Death");
                DrawSingleClipWithPreview(pProp.FindPropertyRelative("heartbeatLoopClip"), "Heartbeat (Loop)");

                EditorGUILayout.Slider(pProp.FindPropertyRelative("hurtVolume"), 0f, 1f, "Damage Volume");
                EditorGUILayout.Slider(pProp.FindPropertyRelative("deathVolume"), 0f, 1f, "Death Volume");
                EditorGUILayout.Slider(pProp.FindPropertyRelative("lowHpThreshold"), 0.1f, 0.5f, "Low Health Threshold (Heartbeat)");
                EditorGUILayout.Slider(pProp.FindPropertyRelative("pitchVariation"), 0f, 0.2f, "Organic Pitch Variation");
            });
        }

        // 2. Combat and Surfaces

        private void DrawCombatTab()
        {
            DrawSectionHeader("Combat: Surface Impacts & Swings",
                "Define default surface impact sounds when a weapon or skill has no specific override.");

            var cProp = _serializedDb.FindProperty("combat");
            if (cProp == null) return;

            DrawClipArray(cProp.FindPropertyRelative("woodHitClips"), "🪵 Wood / Tree Impact");
            DrawClipArray(cProp.FindPropertyRelative("stoneHitClips"), "🪨 Stone Impact");
            DrawClipArray(cProp.FindPropertyRelative("metalHitClips"), "🛡️ Metal / Ore Impact");
            DrawClipArray(cProp.FindPropertyRelative("fleshHitClips"), "🥩 Flesh / Enemy Impact");
            DrawClipArray(cProp.FindPropertyRelative("defaultHitClips"), "⚪ Default Impact (Fallback)");

            EditorGUILayout.Space(6);
            DrawClipArray(cProp.FindPropertyRelative("lightSwingClips"), "🗡️ Light Swings");
            DrawClipArray(cProp.FindPropertyRelative("heavySwingClips"), "🪓 Heavy Swings");

            EditorGUILayout.Space(6);
            DrawSectionBox("Registered Weapon Profiles", () =>
            {
                var profilesProp = cProp.FindPropertyRelative("registeredProfiles");
                if (profilesProp != null)
                {
                    EditorGUILayout.PropertyField(profilesProp, new GUIContent("Active Profiles"), true);
                }
            });
        }

        // ── 3. Enemies ──

        private void DrawEnemiesTab()
        {
            DrawSectionHeader("Enemies: Vocalizations & Feedback",
                "Configure 3D attack, hurt, and death sounds for monster archetypes.");

            var eProp = _serializedDb.FindProperty("enemies");
            if (eProp == null) return;

            DrawSectionBox("Enemy: Swarmer", () =>
            {
                DrawClipArray(eProp.FindPropertyRelative("swarmerAttackClips"), "⚔️ Attacks / Bites");
                DrawClipArray(eProp.FindPropertyRelative("swarmerHurtClips"), "💥 Hurt Screeches");
                DrawClipArray(eProp.FindPropertyRelative("swarmerDeathClips"), "☠️ Visceral Death");

                EditorGUILayout.Space(4);
                EditorGUILayout.Slider(eProp.FindPropertyRelative("attackVolume"), 0f, 1f, "Attack Volume");
                EditorGUILayout.Slider(eProp.FindPropertyRelative("hurtVolume"), 0f, 1f, "Damage Volume");
                EditorGUILayout.Slider(eProp.FindPropertyRelative("deathVolume"), 0f, 1f, "Death Volume");
                EditorGUILayout.PropertyField(eProp.FindPropertyRelative("minDistance"), new GUIContent("Minimum Distance (3D)"));
                EditorGUILayout.PropertyField(eProp.FindPropertyRelative("maxDistance"), new GUIContent("Maximum Distance (3D)"));
            });
        }

        // 4. Loot and Interactions

        private void DrawLootTab()
        {
            DrawSectionHeader("Loot, Chests & Pickups",
                "Configure pickup and drop sounds by rarity (Common to Legendary), gold, and chest opening.");

            var lProp = _serializedDb.FindProperty("loot");
            if (lProp == null) return;

            DrawSectionBox("Item Pickups by Rarity (Pickup SFX)", () =>
            {
                DrawSingleClipWithPreview(lProp.FindPropertyRelative("commonPickupClip"), "⚪ Pickup: Common");
                EditorGUILayout.Slider(lProp.FindPropertyRelative("commonPickupVolume"), 0f, 1f, "Common Volume");

                EditorGUILayout.Space(6);
                DrawSingleClipWithPreview(lProp.FindPropertyRelative("uncommonPickupClip"), "🟢 Pickup: Uncommon");
                EditorGUILayout.Slider(lProp.FindPropertyRelative("uncommonPickupVolume"), 0f, 1f, "Uncommon Volume");

                EditorGUILayout.Space(6);
                DrawSingleClipWithPreview(lProp.FindPropertyRelative("rarePickupClip"), "🔵 Pickup: Rare");
                EditorGUILayout.Slider(lProp.FindPropertyRelative("rarePickupVolume"), 0f, 1f, "Rare Volume");

                EditorGUILayout.Space(6);
                DrawSingleClipWithPreview(lProp.FindPropertyRelative("epicPickupClip"), "🟣 Pickup: Epic");
                EditorGUILayout.Slider(lProp.FindPropertyRelative("epicPickupVolume"), 0f, 1f, "Epic Volume");

                EditorGUILayout.Space(6);
                DrawSingleClipWithPreview(lProp.FindPropertyRelative("legendaryPickupClip"), "🟠 Pickup: Legendary");
                EditorGUILayout.Slider(lProp.FindPropertyRelative("legendaryPickupVolume"), 0f, 1f, "Legendary Volume");
            });

            EditorGUILayout.Space(10);
            DrawSectionBox("Ground Impact / Drop by Rarity (Drop SFX)", () =>
            {
                DrawSingleClipWithPreview(lProp.FindPropertyRelative("commonDropClip"), "⚪ Drop: Common");
                EditorGUILayout.Slider(lProp.FindPropertyRelative("commonDropVolume"), 0f, 1f, "Common Drop Volume");

                EditorGUILayout.Space(6);
                DrawSingleClipWithPreview(lProp.FindPropertyRelative("uncommonDropClip"), "🟢 Drop: Uncommon");
                EditorGUILayout.Slider(lProp.FindPropertyRelative("uncommonDropVolume"), 0f, 1f, "Uncommon Drop Volume");

                EditorGUILayout.Space(6);
                DrawSingleClipWithPreview(lProp.FindPropertyRelative("rareDropClip"), "🔵 Drop: Rare");
                EditorGUILayout.Slider(lProp.FindPropertyRelative("rareDropVolume"), 0f, 1f, "Rare Drop Volume");

                EditorGUILayout.Space(6);
                DrawSingleClipWithPreview(lProp.FindPropertyRelative("epicDropClip"), "🟣 Drop: Epic");
                EditorGUILayout.Slider(lProp.FindPropertyRelative("epicDropVolume"), 0f, 1f, "Epic Drop Volume");

                EditorGUILayout.Space(6);
                DrawSingleClipWithPreview(lProp.FindPropertyRelative("legendaryDropClip"), "🟠 Drop: Legendary");
                EditorGUILayout.Slider(lProp.FindPropertyRelative("legendaryDropVolume"), 0f, 1f, "Legendary Drop Volume");
            });

            EditorGUILayout.Space(10);
            DrawSectionBox("Gold, Chests & General Pickups", () =>
            {
                DrawSingleClipWithPreview(lProp.FindPropertyRelative("goldPickupClip"), "🪙 Gold Pickup");
                EditorGUILayout.Slider(lProp.FindPropertyRelative("goldVolume"), 0f, 1f, "Gold Volume");

                EditorGUILayout.Space(6);
                DrawSingleClipWithPreview(lProp.FindPropertyRelative("itemPickupClip"), "🎒 Default Pickup (Fallback)");
                EditorGUILayout.Slider(lProp.FindPropertyRelative("itemVolume"), 0f, 1f, "Default Volume");

                EditorGUILayout.Space(6);
                DrawSingleClipWithPreview(lProp.FindPropertyRelative("chestOpenClip"), "📦 Chest Opening");
                EditorGUILayout.Slider(lProp.FindPropertyRelative("chestVolume"), 0f, 1f, "Chest Volume");
            });
        }

        // 5. User Interface (UI)

        private void DrawUiTab()
        {
            DrawSectionHeader("User Interface (UI)",
                "Configure tactile sound effects for button clicks, modals, item crafting, and error alerts.");

            var uProp = _serializedDb.FindProperty("ui");
            if (uProp == null) return;

            DrawSectionBox("Navigation & Modals", () =>
            {
                DrawSingleClipWithPreview(uProp.FindPropertyRelative("buttonClickClip"), "🖱️ Button Click");
                EditorGUILayout.Slider(uProp.FindPropertyRelative("buttonClickVolume"), 0f, 1f, "Click Volume");

                EditorGUILayout.Space(6);
                DrawSingleClipWithPreview(uProp.FindPropertyRelative("modalOpenClip"), "📂 Window / Modal Opening");
                EditorGUILayout.Slider(uProp.FindPropertyRelative("modalOpenVolume"), 0f, 1f, "Modal Volume");

                EditorGUILayout.Space(6);
                DrawSingleClipWithPreview(uProp.FindPropertyRelative("craftSuccessClip"), "⚒️ Crafting Success");
                EditorGUILayout.Slider(uProp.FindPropertyRelative("craftSuccessVolume"), 0f, 1f, "Craft Volume");

                EditorGUILayout.Space(6);
                DrawSingleClipWithPreview(uProp.FindPropertyRelative("errorClip"), "⚠️ Error / Invalid Action Sound");
                EditorGUILayout.Slider(uProp.FindPropertyRelative("errorVolume"), 0f, 1f, "Error Volume");
            });
        }

        // 6. Music and Atmosphere

        private void DrawMusicTab()
        {
            DrawSectionHeader("Music & Dynamic Atmosphere",
                "Configure soundtracks and stingers with adaptive crossfade support.");

            var mProp = _serializedDb.FindProperty("music");
            if (mProp == null) return;

            DrawSectionBox("Music Tracks", () =>
            {
                DrawSingleClipWithPreview(mProp.FindPropertyRelative("menuMusic"), "Main Menu (Loop)");
                DrawSingleClipWithPreview(mProp.FindPropertyRelative("dayMusic"), "Daytime Exploration (Loop)");
                DrawSingleClipWithPreview(mProp.FindPropertyRelative("nightMusic"), "Night Combat (Loop)");

                EditorGUILayout.Space(4);
                EditorGUILayout.PropertyField(mProp.FindPropertyRelative("musicFadeDuration"), new GUIContent("Crossfade Duration (s)"));
            });

            EditorGUILayout.Space(6);
            DrawSectionBox("Transition Stingers and Horns", () =>
            {
                DrawSingleClipWithPreview(mProp.FindPropertyRelative("dawnHorn"), "📯 Dawn Horn");
                DrawSingleClipWithPreview(mProp.FindPropertyRelative("nightHorn"), "📯 Night Horn");
                EditorGUILayout.Slider(mProp.FindPropertyRelative("hornVolume"), 0f, 1f, "Horn Volume");
            });
        }

        // ── 7. Global & Volumes ───────────────────────────────────────────────

        private void DrawGlobalTab()
        {
            DrawSectionHeader("Volume Channels & AudioManager",
                "Overview of audio buses and persistent settings.");

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Default Buses (AudioManager):", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("• Master: 1.0 (Master channel)");
            EditorGUILayout.LabelField("• Music:  0.75 (Soundtracks and ambience)");
            EditorGUILayout.LabelField("• SFX:    0.90 (Combat, footsteps, destruction, and interactions)");
            EditorGUILayout.LabelField("• UI:     0.85 (Tactile interface feedback)");
            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox("AudioManager automatically persists volumes in PlayerPrefs while the game runs.", MessageType.Info);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);
            DrawSectionBox("Database Operations", () =>
            {
                if (GUILayout.Button("Reset All to Default Project Clips", GUILayout.Height(30)))
                {
                    if (EditorUtility.DisplayDialog("Reset Audio", "Are you sure you want to restore the default settings?", "Yes", "No"))
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

            arrayProp.isExpanded = EditorGUILayout.Foldout(arrayProp.isExpanded, $"{title} ({arrayProp.arraySize} variations)", true);
            GUILayout.FlexibleSpace();

            if (arrayProp.arraySize > 0)
            {
                if (GUILayout.Button("🎲 Random Variation", EditorStyles.miniButton, GUILayout.Width(130)))
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
                int newSize = EditorGUILayout.IntField("Size", arrayProp.arraySize);
                if (newSize != arrayProp.arraySize && newSize >= 0)
                {
                    arrayProp.arraySize = newSize;
                }

                for (int i = 0; i < arrayProp.arraySize; i++)
                {
                    var elem = arrayProp.GetArrayElementAtIndex(i);
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PropertyField(elem, new GUIContent($"Variation {i + 1}"));

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
