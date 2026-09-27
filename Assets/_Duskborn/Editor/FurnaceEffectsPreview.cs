using Duskborn.Effects;
using Duskborn.Gameplay.Building;
using UnityEditor;
using UnityEngine;

namespace Duskborn.Editor
{
    // An isolated preview scene: no game inventory, save, or network state is touched.
    public sealed class FurnaceEffectsPreview : EditorWindow
    {
        private PreviewRenderUtility preview;
        private GameObject model;
        private FurnaceEffects effect;
        private double previousTime;
        private bool running, dark;
        private float angle = 20, distance = 4.6f;

        [MenuItem("Duskborn/Art/Furnace Effects Preview")]
        private static void Open() => GetWindow<FurnaceEffectsPreview>("Furnace effects");

        private void OnEnable()
        {
            var definition = AssetDatabase.LoadAssetAtPath<BuildableDefinition>("Assets/_Duskborn/Resources/Building/Build_forge.asset");
            if (definition == null || definition.operatingEffect == null) return;
            preview = new PreviewRenderUtility();
            preview.camera.fieldOfView = 40;
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            model = BuildingWorld.CreateVisual(definition);
            var placed = model.AddComponent<PlacedBuilding>();
            placed.Initialize(definition, new BuildingState());
            effect = Instantiate(definition.operatingEffect, model.transform, false);
            effect.Bind(placed);
            preview.AddSingleGO(model);
            previousTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += Animate;
        }

        private void Animate()
        {
            if (effect == null) return;
            double now = EditorApplication.timeSinceStartup;
            float dt = Mathf.Min(.05f, (float)(now - previousTime)); previousTime = now;
            effect.SetOperating(running);
            effect.AdvanceVisuals(dt, preview.camera);
            foreach (var ps in effect.GetComponentsInChildren<ParticleSystem>())
                ps.Simulate(dt, false, false, false);
            Repaint();
        }

        private void OnGUI()
        {
            if (preview == null) { EditorGUILayout.HelpBox("Import the furnace effect assets first.", MessageType.Info); return; }
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Start")) running = true;
            if (GUILayout.Button("Stop / cool")) running = false;
            dark = GUILayout.Toggle(dark, "Dark lighting");
            EditorGUILayout.EndHorizontal();
            angle = EditorGUILayout.Slider("Orbit", angle, -180, 180);
            distance = EditorGUILayout.Slider("Distance", distance, 2.5f, 10);
            EditorGUILayout.LabelField("Isolated visual preview • no bloom • no crafting simulation");
            var rect = GUILayoutUtility.GetRect(1, 10000, 1, 10000);
            if (Event.current.type != EventType.Repaint) return;
            preview.camera.transform.position = Quaternion.Euler(0, angle, 0) * new Vector3(0, 2.6f, distance);
            preview.camera.transform.LookAt(new Vector3(0, 1.5f, 0));
            preview.camera.backgroundColor = dark ? new Color(.018f,.024f,.035f) : new Color(.23f,.28f,.32f);
            preview.lights[0].intensity = dark ? .12f : 1.1f;
            preview.lights[0].transform.rotation = Quaternion.Euler(40, -30, 0);
            preview.lights[1].intensity = dark ? .035f : .35f;
            preview.ambientColor = dark ? new Color(.04f,.05f,.07f) : new Color(.3f,.32f,.35f);
            preview.BeginPreview(rect, GUIStyle.none);
            preview.Render(true);
            GUI.DrawTexture(rect, preview.EndPreview(), ScaleMode.StretchToFill, false);
        }

        private void OnDisable()
        {
            EditorApplication.update -= Animate;
            if (model != null) DestroyImmediate(model);
            preview?.Cleanup(); preview = null;
        }
    }
}
