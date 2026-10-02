using UnityEditor;
using UnityEngine;
using Duskborn.Core;

namespace Duskborn.Editor
{
    [CustomEditor(typeof(DayNightCycle))]
    public class DayNightCycleEditor : UnityEditor.Editor
    {
        private float _editorHour = 12.0f;
        private bool _isScrubbing;

        public override void OnInspectorGUI()
        {
            DayNightCycle cycle = (DayNightCycle)target;

            DrawDefaultInspector();

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Interactive Cycle Control (Editor / Real Time)", EditorStyles.boldLabel);

            // Cycle information in runtime or edit mode.
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField($"Current Period: {cycle.PeriodDisplayName} ({cycle.CurrentPeriod})", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Clock: {cycle.ClockTimeString}  |  Phase: {(cycle.IsDay ? "Dia ☀️" : "Noite 🌙")}");
            EditorGUILayout.LabelField($"Current Night: {cycle.CurrentNight}  |  Phase Progress: {cycle.PhaseProgress * 100f:F1}%");
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("24-Hour Preview (Drag to test shadows and sky):", EditorStyles.miniBoldLabel);

            EditorGUI.BeginChangeCheck();
            _editorHour = EditorGUILayout.Slider("Time of Day (0h–24h)", cycle.ClockHours > 0f && !_isScrubbing ? cycle.ClockHours : _editorHour, 0f, 24f);
            if (EditorGUI.EndChangeCheck())
            {
                _isScrubbing = true;
                ApplyScrubbedHour(cycle, _editorHour);
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Quick Lighting Shortcuts:", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Dawn (06:00)")) { _editorHour = 6.0f; ApplyScrubbedHour(cycle, 6.0f); }
            if (GUILayout.Button("Morning (09:00)"))     { _editorHour = 9.0f; ApplyScrubbedHour(cycle, 9.0f); }
            if (GUILayout.Button("Noon (13:00)"))  { _editorHour = 13.0f; ApplyScrubbedHour(cycle, 13.0f); }
            if (GUILayout.Button("Dusk (19:30)")) { _editorHour = 19.5f; ApplyScrubbedHour(cycle, 19.5f); }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Nightfall (20:30)"))  { _editorHour = 20.5f; ApplyScrubbedHour(cycle, 20.5f); }
            if (GUILayout.Button("Midnight (01:00)")) { _editorHour = 1.0f; ApplyScrubbedHour(cycle, 1.0f); }
            if (GUILayout.Button("Predawn (04:30)")) { _editorHour = 4.5f; ApplyScrubbedHour(cycle, 4.5f); }
            if (GUILayout.Button("Boss Night 7 🩸"))   { _editorHour = 1.0f; ApplyBossNight(cycle); }
            EditorGUILayout.EndHorizontal();

            if (Application.isPlaying)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("Session Debugging (Play Mode):", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Skip to Next Phase"))
                {
                    cycle.ForceEndCurrentPhase();
                }
                if (GUILayout.Button("End Current Night"))
                {
                    cycle.ForceEndNight();
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(6);
            if (GUILayout.Button("Run Cycle Validation Tests"))
            {
                DayNightCycleTests.RunAllTests();
            }
        }

        private void ApplyScrubbedHour(DayNightCycle cycle, float hour)
        {
            // Mapping: day covers 06:00 to 20:00 (14 hours).
            // Night covers 20:00 to 06:00 (10 hours).
            bool isDay = hour >= 6.0f && hour < 20.0f;
            float progress;
            if (isDay)
            {
                progress = Mathf.Clamp01((hour - 6.0f) / 14.0f);
            }
            else
            {
                if (hour >= 20.0f)
                    progress = Mathf.Clamp01((hour - 20.0f) / 10.0f);
                else
                    progress = Mathf.Clamp01((hour + 4.0f) / 10.0f);
            }

            cycle.SetPhaseAndProgressForEditor(isDay, progress, 1);
            EditorUtility.SetDirty(cycle);
            SceneView.RepaintAll();
        }

        private void ApplyBossNight(DayNightCycle cycle)
        {
            cycle.SetPhaseAndProgressForEditor(false, 0.5f, 7);
            EditorUtility.SetDirty(cycle);
            SceneView.RepaintAll();
        }
    }
}
