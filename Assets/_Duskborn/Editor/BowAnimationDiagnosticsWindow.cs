using System;
using System.Collections.Generic;
using System.IO;
using Duskborn.Gameplay.Equipment;
using UnityEditor;
using UnityEngine;

namespace Duskborn.Editor
{
    public sealed class BowAnimationDiagnosticsWindow : EditorWindow
    {
        [Serializable]
        private sealed class Capture
        {
            public int version = 5;
            public string utc, label, player, controller, avatar, mask;
            public string sampling = "WeaponActionPlayer.LateUpdate; quaternion rotations; positive lean = left; positions in Animator space";
            public List<string> humanoidMask = new List<string>();
            public List<string> transformMask = new List<string>();
            public List<string> controllerClips = new List<string>();
            public List<string> actionClips = new List<string>();
            public List<WeaponActionPlayer.AnimationDiagnosticFrame> frames = new List<WeaponActionPlayer.AnimationDiagnosticFrame>();
        }

        private WeaponActionPlayer target, recordingTarget;
        private Capture capture;
        private string label = "baseline";
        private string lastPath;
        private bool recording;
        private float startedAt;
        private Vector2 scroll;

        [MenuItem("Duskborn/Diagnostics/Bow Animation Capture")]
        public static void Open() => GetWindow<BowAnimationDiagnosticsWindow>("Bow Animation Capture");

        private void OnEnable() => EditorApplication.playModeStateChanged += OnPlayMode;
        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayMode;
            StopCapture();
        }
        private void OnPlayMode(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode) StopCapture();
        }
        private void OnInspectorUpdate()
        {
            if (recording && (recordingTarget == null || !recordingTarget.isActiveAndEnabled ||
                Time.unscaledTime - startedAt >= 60f)) StopCapture();
            Repaint();
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Read-only capture during a session you start yourself. Records the real playable controller, bow overlay, mask and final body pose. Stops after 60 seconds or 3,600 frames.", MessageType.Info);
            using (new EditorGUI.DisabledScope(recording))
            {
                target = (WeaponActionPlayer)EditorGUILayout.ObjectField("Player action component", target, typeof(WeaponActionPlayer), true);
                label = EditorGUILayout.TextField("Scenario label", label);
                if (GUILayout.Button("Use selected player's action component") && Selection.activeGameObject != null)
                    target = Selection.activeGameObject.GetComponentInParent<WeaponActionPlayer>() ??
                        Selection.activeGameObject.GetComponentInChildren<WeaponActionPlayer>();
            }
            using (new EditorGUI.DisabledScope(!Application.isPlaying || target == null ||
                target.DiagnosticAnimator == null || !target.gameObject.scene.IsValid() || recording))
                if (GUILayout.Button("Start capture")) StartCapture();
            using (new EditorGUI.DisabledScope(!recording))
                if (GUILayout.Button("Stop and save")) StopCapture();
            using (new EditorGUI.DisabledScope(capture == null || capture.frames.Count == 0 || recording))
                if (GUILayout.Button("Save capture again")) SaveCapture();
            if (!string.IsNullOrEmpty(lastPath)) EditorGUILayout.SelectableLabel(lastPath, GUILayout.Height(38));
            if (capture == null) return;
            EditorGUILayout.LabelField(recording ? "Recording" : "Stopped", capture.frames.Count + " frames");
            if (capture.frames.Count == 0) return;
            var frame = capture.frames[capture.frames.Count - 1];
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Phase / clip", frame.phase + " / " + frame.actionClip);
            EditorGUILayout.LabelField("Action weight / time", frame.actionWeight.ToString("F3") + " / " + frame.actionTime.ToString("F3"));
            EditorGUILayout.LabelField("Torso lean (+ left)", frame.torsoLeftLean.ToString("F2") + " degrees");
            EditorGUILayout.LabelField("Local velocity", frame.localVelocity.ToString("F2"));
            EditorGUILayout.LabelField("Bow anchor / tilt error", frame.bowAnchorWeight.ToString("F3") + " / " + frame.bowSpineTiltError.ToString("F2") + " degrees");
            foreach (var layer in frame.layers)
            {
                EditorGUILayout.LabelField(layer.name, "weight " + layer.weight.ToString("F3"));
                EditorGUILayout.LabelField("Controller X / Y", layer.velocityX.ToString("F3") + " / " + layer.velocityY.ToString("F3"));
                foreach (var clip in layer.currentClips)
                    EditorGUILayout.LabelField(clip.name, clip.weight.ToString("F3"));
                foreach (var clip in layer.nextClips)
                    EditorGUILayout.LabelField("Next: " + clip.name, clip.weight.ToString("F3"));
            }
            EditorGUILayout.EndScrollView();
        }

        private void StartCapture()
        {
            recordingTarget = target;
            var animator = target.DiagnosticAnimator;
            var mask = target.DiagnosticMask;
            capture = new Capture
            {
                utc = DateTime.UtcNow.ToString("O"), label = label,
                player = target.name + "#" + target.GetInstanceID(),
                controller = AssetDatabase.GetAssetPath(animator.runtimeAnimatorController),
                avatar = AssetDatabase.GetAssetPath(animator.avatar),
                mask = AssetDatabase.GetAssetPath(mask)
            };
            if (mask != null)
            {
                for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
                    capture.humanoidMask.Add(((AvatarMaskBodyPart)i) + "=" + mask.GetHumanoidBodyPartActive((AvatarMaskBodyPart)i));
                for (int i = 0; i < mask.transformCount; i++)
                    capture.transformMask.Add(mask.GetTransformPath(i) + "=" + mask.GetTransformActive(i));
            }
            if (animator.runtimeAnimatorController != null)
                foreach (var clip in animator.runtimeAnimatorController.animationClips)
                {
                    string entry = clip.name + " | " + AssetDatabase.GetAssetPath(clip);
                    if (!capture.controllerClips.Contains(entry)) capture.controllerClips.Add(entry);
                }
            // Include source asset paths, since ranged playback uses transient clip clones.
            var weapon = target.CurrentWeapon;
            if (weapon?.Actions != null)
                foreach (var action in weapon.Actions)
                    if (action?.Entries != null)
                        foreach (var entry in action.Entries)
                            if (entry?.Clip != null)
                                capture.actionClips.Add(entry.Clip.name + " | " + AssetDatabase.GetAssetPath(entry.Clip));
            recording = true;
            startedAt = Time.unscaledTime;
            target.AnimationDiagnosticSample += OnSample;
        }

        private void OnSample(WeaponActionPlayer.AnimationDiagnosticFrame frame)
        {
            capture.frames.Add(frame);
            if (capture.frames.Count >= 3600 || Time.unscaledTime - startedAt >= 60f) StopCapture();
        }

        private void StopCapture()
        {
            if (!recording) return;
            recording = false;
            if (recordingTarget != null) recordingTarget.AnimationDiagnosticSample -= OnSample;
            recordingTarget = null;
            SaveCapture();
        }

        private void SaveCapture()
        {
            if (capture == null || capture.frames.Count == 0) return;
            try
            {
                string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/BowAnimation"));
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, "bow-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".json");
                File.WriteAllText(path, JsonUtility.ToJson(capture, false));
                lastPath = path;
                Debug.Log("[BowAnimationCapture] Saved " + capture.frames.Count + " frames to " + path);
            }
            catch (Exception ex)
            {
                Debug.LogError("[BowAnimationCapture] Save failed; capture remains available for retry: " + ex.Message);
            }
        }
    }
}
