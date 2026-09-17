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
            EditorGUILayout.LabelField("Controle Interativo de Ciclo (Editor / Tempo Real)", EditorStyles.boldLabel);

            // Informações do ciclo em tempo de execução ou modo de edição
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField($"Período Atual: {cycle.PeriodDisplayName} ({cycle.CurrentPeriod})", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Relógio: {cycle.ClockTimeString}  |  Fase: {(cycle.IsDay ? "Dia ☀️" : "Noite 🌙")}");
            EditorGUILayout.LabelField($"Noite Atual: {cycle.CurrentNight}  |  Progresso da Fase: {cycle.PhaseProgress * 100f:F1}%");
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Pré-visualização 24 Horas (Arraste para testar sombras e céu):", EditorStyles.miniBoldLabel);

            EditorGUI.BeginChangeCheck();
            _editorHour = EditorGUILayout.Slider("Hora do Dia (0h–24h)", cycle.ClockHours > 0f && !_isScrubbing ? cycle.ClockHours : _editorHour, 0f, 24f);
            if (EditorGUI.EndChangeCheck())
            {
                _isScrubbing = true;
                ApplyScrubbedHour(cycle, _editorHour);
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Atalhos Rápidos de Iluminação:", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Alvorecer (06:00)")) { _editorHour = 6.0f; ApplyScrubbedHour(cycle, 6.0f); }
            if (GUILayout.Button("Manhã (09:00)"))     { _editorHour = 9.0f; ApplyScrubbedHour(cycle, 9.0f); }
            if (GUILayout.Button("Meio-dia (13:00)"))  { _editorHour = 13.0f; ApplyScrubbedHour(cycle, 13.0f); }
            if (GUILayout.Button("Crepúsculo (19:30)")) { _editorHour = 19.5f; ApplyScrubbedHour(cycle, 19.5f); }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Anoitecer (20:30)"))  { _editorHour = 20.5f; ApplyScrubbedHour(cycle, 20.5f); }
            if (GUILayout.Button("Meia-noite (01:00)")) { _editorHour = 1.0f; ApplyScrubbedHour(cycle, 1.0f); }
            if (GUILayout.Button("Madrugada (04:30)")) { _editorHour = 4.5f; ApplyScrubbedHour(cycle, 4.5f); }
            if (GUILayout.Button("Chefe Noite 7 🩸"))   { _editorHour = 1.0f; ApplyBossNight(cycle); }
            EditorGUILayout.EndHorizontal();

            if (Application.isPlaying)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("Depuração de Partida (PlayMode):", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Pular para Próxima Fase"))
                {
                    cycle.ForceEndCurrentPhase();
                }
                if (GUILayout.Button("Encerrar Noite Atual"))
                {
                    cycle.ForceEndNight();
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(6);
            if (GUILayout.Button("Executar Testes de Validação do Ciclo"))
            {
                DayNightCycleTests.RunAllTests();
            }
        }

        private void ApplyScrubbedHour(DayNightCycle cycle, float hour)
        {
            // Mapeamento: Dia cobre 06:00 às 20:00 (14 horas)
            // Noite cobre 20:00 às 06:00 (10 horas)
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
