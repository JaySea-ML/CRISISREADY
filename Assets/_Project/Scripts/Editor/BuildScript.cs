using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MRCrisisTrainer.EditorTools
{
    public static class BuildScript
    {
        private const string OutputDir = "Builds";
        private const string ApkName = "MRCrisisTrainer.apk";

        [MenuItem("MRCrisis/Build APK (Quest 3)", priority = 20)]
        public static void BuildAndroidApk()
        {
            var scenes = EditorBuildSettings.scenes;
            var enabledScenes = new System.Collections.Generic.List<string>();
            foreach (var s in scenes)
            {
                if (s.enabled) enabledScenes.Add(s.path);
            }

            if (enabledScenes.Count == 0)
            {
                EditorUtility.DisplayDialog("MR Crisis Trainer",
                    "Brak scen w Build Settings. Uruchom najpierw MRCrisis -> Build All Scenes.",
                    "OK");
                return;
            }

            string outputPath = Path.Combine(Application.dataPath, "..", OutputDir, ApkName);
            outputPath = Path.GetFullPath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

            // Delete existing APK to ensure fresh build
            if (File.Exists(outputPath)) File.Delete(outputPath);

            var buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = enabledScenes.ToArray(),
                locationPathName = outputPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            };

            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            ConfigureQuestPlayerSettings();
            EditorUserBuildSettings.buildAppBundle = false; // Build APK, not AAB

            Debug.Log($"[BuildScript] Starting Android APK build to {outputPath}");
            Debug.Log($"[BuildScript] Scenes: {string.Join(", ", enabledScenes)}");

            BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[BuildScript] Build SUCCESS - {summary.totalSize / 1024 / 1024} MB in {summary.totalTime.TotalSeconds:F0}s");
                Debug.Log($"[BuildScript] APK at: {outputPath}");
                if (!Application.isBatchMode)
                {
                    EditorUtility.DisplayDialog("MR Crisis Trainer",
                        $"Build SUCCESS!\n\nAPK: {outputPath}\nSize: {summary.totalSize / 1024 / 1024} MB\nTime: {summary.totalTime.TotalSeconds:F0}s\n\nMożesz teraz sideloadować przez adb install lub SideQuest.",
                        "OK");
                    EditorUtility.RevealInFinder(outputPath);
                }
            }
            else
            {
                Debug.LogError($"[BuildScript] Build FAILED - {summary.totalErrors} errors");
                if (!Application.isBatchMode)
                {
                    EditorUtility.DisplayDialog("MR Crisis Trainer",
                        $"Build FAILED.\n\nErrors: {summary.totalErrors}\nWarnings: {summary.totalWarnings}\n\nZobacz Console żeby znaleźć szczegóły.",
                        "OK");
                }
            }
        }

        private static void ConfigureQuestPlayerSettings()
        {
            PlayerSettings.companyName = "Grupa1_Temat3";
            PlayerSettings.productName = "MRCrisisTrainer";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.grupa1.mrcrisistrainer");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.preserveFramebufferAlpha = true;
            SetInputSystemOnly();
            EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
        }

        private static void SetInputSystemOnly()
        {
            var property = typeof(PlayerSettings).GetProperty("activeInputHandling");
            if (property == null || !property.CanWrite)
            {
                Debug.LogWarning("[BuildScript] PlayerSettings.activeInputHandling unavailable; ProjectSettings.asset should keep Input System only.");
                return;
            }

            try
            {
                var value = Enum.Parse(property.PropertyType, "InputSystemPackage");
                property.SetValue(null, value);
                Debug.Log("[BuildScript] Active Input Handling set to Input System Package.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BuildScript] Could not set Active Input Handling via API: {ex.Message}");
            }
        }
    }
}
