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
        private static string s_DragPath  = null;
        private static int    s_DragIdx   = -1;

        public override float GetPropertyHeight(SerializedProperty prop, GUIContent label)
        {
            float lh  = EditorGUIUtility.singleLineHeight;
            float pad = EditorGUIUtility.standardVerticalSpacing;
            var events = prop.FindPropertyRelative("Events");
            int  n     = events?.arraySize ?? 0;

            return (lh + pad)           // Clip
                 + (TimelineH + pad)    // timeline
                 + (lh + pad)           // Events header row
                 + n * (lh + pad);      // per-event row
        }

        public override void OnGUI(Rect pos, SerializedProperty prop, GUIContent label)
        {
            float lh  = EditorGUIUtility.singleLineHeight;
            float pad = EditorGUIUtility.standardVerticalSpacing;

            EditorGUI.BeginProperty(pos, label, prop);

            var clipProp   = prop.FindPropertyRelative("Clip");
            var eventsProp = prop.FindPropertyRelative("Events");

            // ── Clip ──────────────────────────────────────────────────────────
            var r = Row(ref pos, lh, pad);
            EditorGUI.PropertyField(r, clipProp);

            // ── Timeline ──────────────────────────────────────────────────────
            var barRect = Row(ref pos, TimelineH, pad);
            DrawTimeline(barRect, eventsProp, prop.propertyPath);

            // ── Events header: label + array-size field ───────────────────────
            r = Row(ref pos, lh, pad);
            float lw = EditorGUIUtility.labelWidth;
            EditorGUI.LabelField(new Rect(r.x, r.y, lw, r.height), "Events");

            EditorGUI.BeginChangeCheck();
            int newSize = EditorGUI.IntField(new Rect(r.x + lw, r.y, 40, r.height),
                                              eventsProp.arraySize);
            if (EditorGUI.EndChangeCheck() && newSize >= 0)
                eventsProp.arraySize = newSize;

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
            // Background
            EditorGUI.DrawRect(bar, new Color(0.13f, 0.13f, 0.13f));

            // Centre rule
            EditorGUI.DrawRect(new Rect(bar.x, bar.y + bar.height * 0.5f, bar.width, 1f),
                                new Color(0.3f, 0.3f, 0.3f));

            // 0 / 1 labels
            var mini = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(bar.x, bar.yMax - 13, 14, 13), "0", mini);
            GUI.Label(new Rect(bar.xMax - 14, bar.yMax - 13, 14, 13), "1", mini);

            if (events == null) return;
            int count = events.arraySize;

            // Draw HitboxOpen → HitboxClose windows as tinted region first.
            float openAt = -1f;
            for (int i = 0; i < count; i++)
            {
                var ep   = events.GetArrayElementAtIndex(i);
                var type = (WeaponEventType)ep.FindPropertyRelative("Type").enumValueIndex;
                float t  = ep.FindPropertyRelative("NormalizedTime").floatValue;

                if (type == WeaponEventType.HitboxOpen)
                {
                    openAt = t;
                }
                else if (type == WeaponEventType.HitboxClose && openAt >= 0f)
                {
                    float x1 = bar.x + openAt * bar.width;
                    float x2 = bar.x + t * bar.width;
                    EditorGUI.DrawRect(new Rect(x1, bar.y + 3f, x2 - x1, bar.height - 6f),
                                       new Color(1f, 0.25f, 0.25f, 0.28f));
                    openAt = -1f;
                }
            }

            // Draw markers and handle drag.
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

                // Marker line
                EditorGUI.DrawRect(new Rect(mx - 1f, bar.y, 2f, bar.height), col);
                // Marker diamond
                EditorGUI.DrawRect(new Rect(mx - 4f, bar.y + bar.height * 0.5f - 4f, 8f, 8f), col);

                // Start drag on click near this marker.
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

            // Apply drag to the currently dragged marker.
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
