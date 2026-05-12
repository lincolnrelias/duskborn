#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Duskborn.Gameplay.Equipment;

namespace Duskborn.Editor
{
    [CustomPropertyDrawer(typeof(WeaponActionData))]
    public class WeaponActionDataDrawer : PropertyDrawer
    {
        private const float TimelineH  = 36f;
        private const float DragRadius = 7f;

        // Only one marker drag active at a time across all drawers.
        private static string s_DragPath = null;
        private static int    s_DragIdx  = -1;

        public override float GetPropertyHeight(SerializedProperty prop, GUIContent label)
        {
            float lh  = EditorGUIUtility.singleLineHeight;
            float pad = EditorGUIUtility.standardVerticalSpacing;
            var clips  = prop.FindPropertyRelative("Clips");
            var events = prop.FindPropertyRelative("Events");
            int  nc    = clips?.arraySize  ?? 0;
            int  ne    = events?.arraySize ?? 0;

            return (lh + pad)            // Clips header row
                 + nc * (lh + pad)       // per-clip row
                 + (lh + pad)            // Speed slider
                 + (lh + pad)            // Preserve Locomotion toggle
                 + (TimelineH + pad)     // timeline
                 + (lh + pad)            // Events header row
                 + ne * (lh + pad);      // per-event row
        }

        public override void OnGUI(Rect pos, SerializedProperty prop, GUIContent label)
        {
            float lh  = EditorGUIUtility.singleLineHeight;
            float pad = EditorGUIUtility.standardVerticalSpacing;

            EditorGUI.BeginProperty(pos, label, prop);

            var clipsProp   = prop.FindPropertyRelative("Clips");
            var eventsProp  = prop.FindPropertyRelative("Events");
            var useMaskProp = prop.FindPropertyRelative("PreserveLocomotion");
            var speedProp   = prop.FindPropertyRelative("BaseSpeed");

            // ── Clips header: label + array-size field ────────────────────────
            var r  = Row(ref pos, lh, pad);
            float lw = EditorGUIUtility.labelWidth;
            EditorGUI.LabelField(new Rect(r.x, r.y, lw, r.height), "Clips");

            EditorGUI.BeginChangeCheck();
            int newClipSize = EditorGUI.IntField(new Rect(r.x + lw, r.y, 40, r.height),
                                                  clipsProp.arraySize);
            if (EditorGUI.EndChangeCheck() && newClipSize >= 0)
                clipsProp.arraySize = newClipSize;

            // ── Per-clip rows ─────────────────────────────────────────────────
            for (int i = 0; i < clipsProp.arraySize; i++)
            {
                r = Row(ref pos, lh, pad);
                var clipElement = clipsProp.GetArrayElementAtIndex(i);
                EditorGUI.LabelField(new Rect(r.x, r.y, 20f, r.height), i.ToString());
                EditorGUI.PropertyField(new Rect(r.x + 20f, r.y, r.width - 20f, r.height),
                                         clipElement, GUIContent.none);
            }

            // ── Speed slider ──────────────────────────────────────────────────
            r = Row(ref pos, lh, pad);
            EditorGUI.Slider(r, speedProp, 0.1f, 3f,
                new GUIContent("Speed", "Base playback speed. 1 = authored speed. Scaled by RuntimeSpeedMultiplier at runtime."));

            // ── Preserve Locomotion toggle ────────────────────────────────────
            r = Row(ref pos, lh, pad);
            useMaskProp.boolValue = EditorGUI.ToggleLeft(r,
                new GUIContent("Preserve Locomotion / No Root Motion",
                    "ON: upper-body mask, locomotion drives legs and movement.\nOFF: full-body override, animation drives movement via root motion."),
                useMaskProp.boolValue);

            // ── Timeline ──────────────────────────────────────────────────────
            var barRect = Row(ref pos, TimelineH, pad);
            DrawTimeline(barRect, eventsProp, prop.propertyPath);

            // ── Events header: label + array-size field ───────────────────────
            r = Row(ref pos, lh, pad);
            EditorGUI.LabelField(new Rect(r.x, r.y, lw, r.height), "Events");

            EditorGUI.BeginChangeCheck();
            int newEventSize = EditorGUI.IntField(new Rect(r.x + lw, r.y, 40, r.height),
                                                   eventsProp.arraySize);
            if (EditorGUI.EndChangeCheck() && newEventSize >= 0)
                eventsProp.arraySize = newEventSize;

            // ── Per-event rows ────────────────────────────────────────────────
            for (int i = 0; i < eventsProp.arraySize; i++)
            {
                r = Row(ref pos, lh, pad);
                DrawEventRow(r, eventsProp.GetArrayElementAtIndex(i), i);
            }

            EditorGUI.EndProperty();

            if (s_DragPath == prop.propertyPath && Event.current.type == EventType.MouseDrag)
                GUI.changed = true;
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
