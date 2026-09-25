using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Duskborn.Editor
{
    /// <summary>
    /// Non-interactive entry points used by Tools/unity.ps1.
    /// Keep these methods synchronous so -quit runs only after the task is complete.
    /// </summary>
    public static class DuskbornCli
    {
        private const string ProjectAssetsRoot = "Assets/_Duskborn";

        private sealed class TestSuite
        {
            public readonly string Name;
            public readonly Action Run;

            public TestSuite(string name, Action run)
            {
                Name = name;
                Run = run;
            }
        }

        [Serializable]
        private sealed class TestResultFile
        {
            public int suitesPassed;
            public int suitesFailed;
            public string[] failures;
        }

        public static void Compile()
        {
            EnsureCompilationSucceeded();
            Debug.Log("[DuskbornCli] Compile check succeeded.");
        }

        public static void Validate()
        {
            EnsureCompilationSucceeded();

            var failures = new List<string>();
            int sceneCount = ValidateBuildScenes(failures);
            int prefabCount = ValidatePrefabs(failures);
            int resourceCount = ValidateResourceAssets(failures);

            if (failures.Count > 0)
                throw new InvalidOperationException(
                    "Project validation failed:\n- " + string.Join("\n- ", failures));

            Debug.Log(
                $"[DuskbornCli] Validation succeeded. " +
                $"Checked {sceneCount} build scenes, {prefabCount} prefabs, and {resourceCount} resource assets.");
        }

        public static void RunTests()
        {
            EnsureCompilationSucceeded();

            var suites = new[]
            {
                new TestSuite(nameof(AudioDatabaseTests), AudioDatabaseTests.RunAllTests),
                new TestSuite(nameof(BuildingTests), BuildingTests.Run),
                new TestSuite(nameof(CharacterPanelTests), CharacterPanelTests.RunAllTests),
                new TestSuite(nameof(CraftingTests), CraftingTests.RunAllTests),
                new TestSuite(nameof(CursorAndMenuFocusTests), CursorAndMenuFocusTests.RunAllTests),
                new TestSuite(nameof(DayNightCycleTests), DayNightCycleTests.RunAllTests),
                new TestSuite(nameof(ItemTierDropTests), ItemTierDropTests.RunAllTests),
                new TestSuite(nameof(ResourceGatheringTests), ResourceGatheringTests.RunAllTests),
                new TestSuite(nameof(SpatialOccupancyMapTests), SpatialOccupancyMapTests.RunAllTests),
                new TestSuite(nameof(UIResolutionScalingTests), UIResolutionScalingTests.RunAllTests),
                new TestSuite(nameof(WorldHealthBarTests), WorldHealthBarTests.RunAllTests)
            };

            var failures = new List<string>();
            int passed = 0;

            foreach (var suite in suites)
            {
                var loggedErrors = new List<string>();
                Application.LogCallback capture = (condition, stackTrace, type) =>
                {
                    if (type == LogType.Error || type == LogType.Assert || type == LogType.Exception)
                        loggedErrors.Add(condition);
                };

                Application.logMessageReceived += capture;
                try
                {
                    Debug.Log($"[DuskbornCli] Running suite: {suite.Name}");
                    suite.Run();
                }
                catch (Exception exception)
                {
                    loggedErrors.Add(exception.ToString());
                }
                finally
                {
                    Application.logMessageReceived -= capture;
                }

                if (loggedErrors.Count == 0)
                {
                    passed++;
                    Debug.Log($"[DuskbornCli] Suite passed: {suite.Name}");
                }
                else
                {
                    failures.Add($"{suite.Name}: {string.Join(" | ", loggedErrors)}");
                }
            }

            WriteTestResults(passed, suites.Length - passed, failures);

            if (failures.Count > 0)
                throw new InvalidOperationException(
                    "One or more project test suites failed:\n- " + string.Join("\n- ", failures));

            Debug.Log($"[DuskbornCli] Test run succeeded. {passed}/{suites.Length} suites passed.");
        }

        public static void BuildWindows()
        {
            EnsureCompilationSucceeded();

            string outputPath = GetCommandLineValue("-buildPath");
            if (string.IsNullOrWhiteSpace(outputPath))
                outputPath = Path.GetFullPath("Builds/Windows/Mugg.exe");

            outputPath = Path.GetFullPath(outputPath);
            string outputDirectory = Path.GetDirectoryName(outputPath);
            if (string.IsNullOrWhiteSpace(outputDirectory))
                throw new InvalidOperationException($"Invalid Windows build path: {outputPath}");
            Directory.CreateDirectory(outputDirectory);

            var scenes = new List<string>();
            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled && !string.IsNullOrWhiteSpace(scene.path))
                    scenes.Add(scene.path);
            }
            if (scenes.Count == 0)
                throw new InvalidOperationException("Cannot build because no enabled build scenes exist.");

            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });

            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException(
                    $"Windows build failed with result {report.summary.result}, " +
                    $"{report.summary.totalErrors} error(s), and {report.summary.totalWarnings} warning(s).");

            Debug.Log(
                $"[DuskbornCli] Windows build succeeded. Output: {outputPath}; " +
                $"Size: {report.summary.totalSize} bytes; Duration: {report.summary.totalTime}.");
        }

        private static void EnsureCompilationSucceeded()
        {
            if (EditorUtility.scriptCompilationFailed)
                throw new InvalidOperationException("Unity reported script compilation errors.");
        }

        private static int ValidateBuildScenes(List<string> failures)
        {
            int enabledSceneCount = 0;
            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (!scene.enabled)
                    continue;

                enabledSceneCount++;
                if (string.IsNullOrWhiteSpace(scene.path) ||
                    AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path) == null)
                    failures.Add($"Enabled build scene is missing or unreadable: {scene.path}");
            }

            if (enabledSceneCount == 0)
                failures.Add("No enabled scenes exist in EditorBuildSettings.");

            return enabledSceneCount;
        }

        private static int ValidatePrefabs(List<string> failures)
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { ProjectAssetsRoot });
            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    failures.Add($"Prefab is unreadable: {path}");
                    continue;
                }

                foreach (var transform in prefab.GetComponentsInChildren<Transform>(true))
                {
                    int missingScripts = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
                    if (missingScripts > 0)
                        failures.Add(
                            $"Prefab has {missingScripts} missing script(s) on '{transform.name}': {path}");
                }
            }

            return prefabGuids.Length;
        }

        private static int ValidateResourceAssets(List<string> failures)
        {
            string resourcesRoot = ProjectAssetsRoot + "/Resources";
            string[] assetGuids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { resourcesRoot });
            foreach (string guid in assetGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.LoadMainAssetAtPath(path) == null)
                    failures.Add($"Resource asset is unreadable: {path}");
            }

            return assetGuids.Length;
        }

        private static void WriteTestResults(int passed, int failed, List<string> failures)
        {
            string logsDirectory = Path.GetFullPath("Logs/UnityCli");
            Directory.CreateDirectory(logsDirectory);
            var result = new TestResultFile
            {
                suitesPassed = passed,
                suitesFailed = failed,
                failures = failures.ToArray()
            };
            File.WriteAllText(
                Path.Combine(logsDirectory, "test-results.json"),
                JsonUtility.ToJson(result, true));
        }

        private static string GetCommandLineValue(string name)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int i = 0; i < arguments.Length - 1; i++)
            {
                if (string.Equals(arguments[i], name, StringComparison.OrdinalIgnoreCase))
                    return arguments[i + 1];
            }
            return null;
        }
    }
}
