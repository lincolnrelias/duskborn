#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Duskborn.Gameplay.Equipment;

namespace Duskborn.Editor
{
    /// <summary>
    /// Draws WeaponActionData as: action settings on top, then one boxed section per clip
    /// (combo step / variant) with its own clip, damage multiplier, event timeline and
    /// event rows. Legacy shared-events data migrates automatically on draw.
    /// </summary>
    [CustomPropertyDrawer(typeof(WeaponActionData))]
    public class WeaponActionDataDrawer : PropertyDrawer
    {
        private const float TimelineH  = 36f;
        private const float DragRadius = 7f;
        private const float BoxPad     = 4f;
        private const float BoxGap     = 6f;

        private static readonly Color[] StepColors =
        {
            new(0.35f, 0.75f, 1f),
            new(1f,    0.75f, 0.3f),
            new(1f,    0.35f, 0.45f),
            new(0.55f, 1f,    0.5f),
        };

        // Only one marker drag active at a time across all drawers.
        private static string s_DragPath = null;
        private static int    s_DragIdx  = -1;

        public override float GetPropertyHeight(SerializedProperty prop, GUIContent label)
        {
            MigrateLegacy(prop);

            float lh  = EditorGUIUtility.singleLineHeight;
            float pad = EditorGUIUtility.standardVerticalSpacing;
            var entries = prop.FindPropertyRelative("Entries");
            bool combo  = prop.FindPropertyRelative("ComboChain").boolValue;

            float h = (lh + pad)                    // Speed
                    + (lh + pad)                    // Preserve Locomotion
                    + (lh + pad)                    // Combo toggle
                    + (combo ? lh + pad : 0f)       // Combo reset time
                    + (lh + pad);                   // Clips header + add button

            for (int i = 0; i < entries.arraySize; i++)
            {
                int ne = entries.GetArrayElementAtIndex(i).FindPropertyRelative("Events").arraySize;
                h += BoxPad * 2f
                   + (lh + pad) * 2f                // header, clip
                   + (TimelineH + pad)
                   + (lh + pad)                     // events header
                   + ne * (lh + pad)
                   + BoxGap;
            }
            return h;
        }

        public override void OnGUI(Rect pos, SerializedProperty prop, GUIContent label)
        {
            MigrateLegacy(prop);

            float lh  = EditorGUIUtility.singleLineHeight;
            float pad = EditorGUIUtility.standardVerticalSpacing;

            EditorGUI.BeginProperty(pos, label, prop);

            var entriesProp = prop.FindPropertyRelative("Entries");
            var useMaskProp = prop.FindPropertyRelative("PreserveLocomotion");
            var speedProp   = prop.FindPropertyRelative("BaseSpeed");
            var comboProp   = prop.FindPropertyRelative("ComboChain");
            var comboReset  = prop.FindPropertyRelative("ComboResetTime");

            // ── Action settings ───────────────────────────────────────────────
            var r = Row(ref pos, lh, pad);
            EditorGUI.Slider(r, speedProp, 0.1f, 3f,
                new GUIContent("Speed", "Base playback speed. 1 = authored speed. Scaled by RuntimeSpeedMultiplier at runtime."));

            r = Row(ref pos, lh, pad);
            useMaskProp.boolValue = EditorGUI.ToggleLeft(r,
                new GUIContent("Preserve Locomotion / No Root Motion",
                    "ON: upper-body mask, locomotion drives legs and movement.\nOFF: full-body override, animation drives movement via root motion."),
                useMaskProp.boolValue);

            r = Row(ref pos, lh, pad);
            comboProp.boolValue = EditorGUI.ToggleLeft(r,
                new GUIContent("Combo Chain (clips play in order)",
                    "ON: clips are sequential combo steps, chaining while attacks land inside the reset window.\nOFF: a random clip variant is picked per attack."),
                comboProp.boolValue);

            bool combo = comboProp.boolValue;
            if (combo)
            {
                r = Row(ref pos, lh, pad);
                EditorGUI.Slider(r, comboReset, 0.1f, 3f,
                    new GUIContent("Combo Reset Time",
                        "Seconds after an attack ends before the chain resets to step 1."));
            }

            // ── Clips header + add button ─────────────────────────────────────
            r = Row(ref pos, lh, pad);
            EditorGUI.LabelField(r, combo ? $"Combo Steps ({entriesProp.arraySize})"
                                          : $"Clip Variants ({entriesProp.arraySize})",
                                 EditorStyles.boldLabel);
            if (GUI.Button(new Rect(r.xMax - 80f, r.y, 80f, r.height), "+ Add Clip"))
                entriesProp.arraySize++; // duplicates the last entry: keeps events/audio as a starting point

            // ── Per-clip boxes ────────────────────────────────────────────────
            int deleteIndex = -1;
            for (int i = 0; i < entriesProp.arraySize; i++)
            {
                var entry  = entriesProp.GetArrayElementAtIndex(i);
                var events = entry.FindPropertyRelative("Events");
                int ne     = events.arraySize;

                float boxH = BoxPad * 2f + (lh + pad) * 2f + (TimelineH + pad)
                           + (lh + pad) + ne * (lh + pad);
                var box = new Rect(pos.x, pos.y, pos.width, boxH);
                GUI.Box(box, GUIContent.none, EditorStyles.helpBox);

                var inner = new Rect(box.x + BoxPad, box.y + BoxPad, box.width - BoxPad * 2f, boxH);
                Color accent = StepColors[i % StepColors.Length];

                // Header: color chip + name + damage multiplier + delete.
                var hr = Row(ref inner, lh, pad);
                EditorGUI.DrawRect(new Rect(hr.x, hr.y + 3f, 4f, hr.height - 6f), accent);
                EditorGUI.LabelField(new Rect(hr.x + 10f, hr.y, 120f, hr.height),
                    combo ? $"Step {i + 1}" : $"Variant {i + 1}", EditorStyles.boldLabel);

                var multProp = entry.FindPropertyRelative("DamageMultiplier");
                if (multProp.floatValue <= 0f) multProp.floatValue = 1f;
                EditorGUI.LabelField(new Rect(hr.xMax - 110f, hr.y, 46f, hr.height), "× dmg");
                multProp.floatValue = EditorGUI.FloatField(
                    new Rect(hr.xMax - 66f, hr.y, 40f, hr.height), multProp.floatValue);

                if (GUI.Button(new Rect(hr.xMax - 20f, hr.y, 20f, hr.height), "✕",
                               EditorStyles.miniButton))
                    deleteIndex = i;

                var cr = Row(ref inner, lh, pad);
                EditorGUI.PropertyField(cr, entry.FindPropertyRelative("Clip"),
                    new GUIContent("Clip"));

                // Timeline + events for THIS clip.
                var barRect = Row(ref inner, TimelineH, pad);
                DrawTimeline(barRect, events, events.propertyPath);

                var er = Row(ref inner, lh, pad);
                EditorGUI.LabelField(new Rect(er.x, er.y, EditorGUIUtility.labelWidth, er.height), "Events");
                EditorGUI.BeginChangeCheck();
                int newEventSize = EditorGUI.IntField(
                    new Rect(er.x + EditorGUIUtility.labelWidth, er.y, 40, er.height), ne);
                if (EditorGUI.EndChangeCheck() && newEventSize >= 0)
                    events.arraySize = newEventSize;

                for (int j = 0; j < events.arraySize && j < ne; j++)
                {
                    var evr = Row(ref inner, lh, pad);
                    DrawEventRow(evr, events.GetArrayElementAtIndex(j), j);
                }

                pos.y += boxH + BoxGap;
            }

            if (deleteIndex >= 0)
                entriesProp.DeleteArrayElementAtIndex(deleteIndex);

            EditorGUI.EndProperty();

            if (s_DragPath != null && s_DragPath.StartsWith(prop.propertyPath) &&
                Event.current.type == EventType.MouseDrag)
                GUI.changed = true;
        }

        // ── Legacy migration (Clips/Events/ComboDamageMultipliers → Entries) ──

        private static void MigrateLegacy(SerializedProperty prop)
        {
            var entries = prop.FindPropertyRelative("Entries");
            var clips   = prop.FindPropertyRelative("Clips");
            if (entries.arraySize > 0 || clips.arraySize == 0) return;

            var events = prop.FindPropertyRelative("Events");
            var mults  = prop.FindPropertyRelative("ComboDamageMultipliers");

            entries.arraySize = clips.arraySize;
            for (int i = 0; i < clips.arraySize; i++)
            {
                var e = entries.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("Clip").objectReferenceValue =
                    clips.GetArrayElementAtIndex(i).objectReferenceValue;

                float m = i < mults.arraySize ? mults.GetArrayElementAtIndex(i).floatValue : 1f;
                e.FindPropertyRelative("DamageMultiplier").floatValue = m > 0f ? m : 1f;

                var dst = e.FindPropertyRelative("Events");
                dst.arraySize = events.arraySize;
                for (int j = 0; j < events.arraySize; j++)
                {
                    var src = events.GetArrayElementAtIndex(j);
                    var d   = dst.GetArrayElementAtIndex(j);
                    d.FindPropertyRelative("Type").enumValueIndex =
                        src.FindPropertyRelative("Type").enumValueIndex;
                    d.FindPropertyRelative("NormalizedTime").floatValue =
                        src.FindPropertyRelative("NormalizedTime").floatValue;
                }
            }

            clips.arraySize  = 0;
            events.arraySize = 0;
            mults.arraySize  = 0;
            prop.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        // ── Timeline ──────────────────────────────────────────────────────────

        private void DrawTimeline(Rect bar, SerializedProperty events, string path)
        {
            EditorGUI.DrawRect(bar, new Color(0.13f, 0.13f, 0.13f));
            EditorGUI.DrawRect(new Rect(bar.x, bar.y + bar.height * 0.5f, bar.width, 1f),
                                new Color(0.3f, 0.3f, 0.3f));

            var mini = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(bar.x, bar.yMax - 13, 14, 13), "0", mini);
            GUI.Label(new Rect(bar.xMax - 14, bar.yMax - 13, 14, 13), "1", mini);

            if (events == null) return;
            int count = events.arraySize;

            float openAt = -1f;
            for (int i = 0; i < count; i++)
            {
                var ep   = events.GetArrayElementAtIndex(i);
                var type = (WeaponEventType)ep.FindPropertyRelative("Type").enumValueIndex;
                float t  = ep.FindPropertyRelative("NormalizedTime").floatValue;

                if (type == WeaponEventType.HitboxOpen)
                    openAt = t;
                else if (type == WeaponEventType.HitboxClose && openAt >= 0f)
                {
                    float x1 = bar.x + openAt * bar.width;
                    float x2 = bar.x + t * bar.width;
                    EditorGUI.DrawRect(new Rect(x1, bar.y + 3f, x2 - x1, bar.height - 6f),
                                       new Color(1f, 0.25f, 0.25f, 0.28f));
                    openAt = -1f;
                }
            }

            Event ev        = Event.current;
            bool  isDragger = s_DragPath == path;

            for (int i = 0; i < count; i++)
            {
                var ep       = events.GetArrayElementAtIndex(i);
                var typeProp = ep.FindPropertyRelative("Type");
                var timeProp = ep.FindPropertyRelative("NormalizedTime");
                var type     = (WeaponEventType)typeProp.enumValueIndex;
                float t      = timeProp.floatValue;
                float mx     = bar.x + t * bar.width;
                Color col    = MarkerColor(type);

                EditorGUI.DrawRect(new Rect(mx - 1f, bar.y, 2f, bar.height), col);
                EditorGUI.DrawRect(new Rect(mx - 4f, bar.y + bar.height * 0.5f - 4f, 8f, 8f), col);

                if (ev.type == EventType.MouseDown && bar.Contains(ev.mousePosition))
                {
                    if (Mathf.Abs(ev.mousePosition.x - mx) < DragRadius)
                    {
                        s_DragPath = path;
                        s_DragIdx  = i;
                        ev.Use();
                    }
                }
            }

            if (isDragger && s_DragIdx >= 0 && s_DragIdx < count)
            {
                if (ev.type == EventType.MouseDrag)
                {
                    float newT = Mathf.Clamp01((ev.mousePosition.x - bar.x) / bar.width);
                    events.GetArrayElementAtIndex(s_DragIdx)
                          .FindPropertyRelative("NormalizedTime").floatValue = newT;
                    ev.Use();
                }
                else if (ev.type == EventType.MouseUp)
                {
                    s_DragPath = null;
                    s_DragIdx  = -1;
                    ev.Use();
                }
            }
        }

        // ── Event row ─────────────────────────────────────────────────────────

        private static void DrawEventRow(Rect r, SerializedProperty ep, int index)
        {
            var typeProp = ep.FindPropertyRelative("Type");
            var timeProp = ep.FindPropertyRelative("NormalizedTime");

            float idxW    = 20f;
            float typeW   = 148f;
            float sliderW = r.width - idxW - typeW - 6f;

            EditorGUI.LabelField(new Rect(r.x, r.y, idxW, r.height), index.ToString());
            EditorGUI.PropertyField(new Rect(r.x + idxW, r.y, typeW, r.height),
                                     typeProp, GUIContent.none);
            EditorGUI.Slider(new Rect(r.x + idxW + typeW + 4f, r.y, sliderW, r.height),
                              timeProp, 0f, 1f, GUIContent.none);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static Rect Row(ref Rect pos, float h, float pad)
        {
            var r = new Rect(pos.x, pos.y, pos.width, h);
            pos.y += h + pad;
            return r;
        }

        private static Color MarkerColor(WeaponEventType t) => t switch
        {
            WeaponEventType.HitboxOpen      => new Color(1f,    0.25f, 0.25f),
            WeaponEventType.HitboxClose     => new Color(1f,    0.6f,  0.15f),
            WeaponEventType.SpawnProjectile => new Color(0.3f,  0.65f, 1f),
            _                               => Color.yellow,
        };
    }
}
#endif
