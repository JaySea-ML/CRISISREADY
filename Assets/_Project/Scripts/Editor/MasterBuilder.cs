using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MRCrisisTrainer.EditorTools
{
    /// <summary>
    /// Główny builder całej gry: materiały z tekstur, dane badawcze (scenariusze + ankiety),
    /// animator ofiary, wszystkie sceny (Bootstrap, MainMenu, TrainingRoom, PostSession),
    /// oraz konfiguracja Build Settings. Wywoływalny z menu i batch mode.
    /// </summary>
    public static class MasterBuilder
    {
        private const string SceneDir = "Assets/_Project/Scenes";

        /// <summary>Pełny rebuild scen + APK w jednym przebiegu (dla batch deployu).</summary>
        public static void BuildEverythingAndApk()
        {
            BuildEverything();
            BuildScript.BuildAndroidApk();
        }

        [MenuItem("MRCrisis/★ BUILD EVERYTHING", priority = -100)]
        public static void BuildEverything()
        {
            Debug.Log("[MasterBuilder] === START full build ===");

            SetupUrpPipeline();

            // 0. Profesjonalny look: post-processing (tonemapping/grading/bloom/vignette) + miękkie cienie + HDR
            ProPolish.SetupPostFX();

            // 0b. Polskie znaki w czcionce TMP (ą/ę/ł/ż/ź/ć/ń/ś dopisane do atlasu — koniec brakujących liter)
            PolishFontSetup.EnsurePolishGlyphs();

            // 1. Passthrough w OVRProjectConfig (manifest Quest 3/3S)
            MetaPassthroughSetup.EnablePassthroughSupport();

            // 1. Materiały z proceduralnych tekstur
            LabMaterials.BuildAll();
            Debug.Log("[MasterBuilder] Materials done.");

            // 2. Dane badawcze (scenariusze + kwestionariusze)
            ResearchDataBuilder.BuildAll();
            AssetDatabase.Refresh();
            Debug.Log("[MasterBuilder] Research data done.");

            // 4. Sceny meta
            MetaSceneBuilder.BuildAll();
            Debug.Log("[MasterBuilder] Meta scenes done.");

            // 5. Główna scena treningowa (pokój + 2 akty)
            LabRoomBuilder.BuildLabRoom();
            Debug.Log("[MasterBuilder] TrainingRoom done.");

            // 6. Build Settings
            ConfigureBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MasterBuilder] === FULL BUILD COMPLETE ===");

            if (!Application.isBatchMode)
                EditorUtility.DisplayDialog("MR Crisis Trainer",
                    "Pełny build zakończony!\n\nSceny:\n- Bootstrap\n- MainMenu (consent + menu)\n- TrainingRoom (2 akty z mikrokrokami)\n- PostSession (ankiety + debrief)\n- GameOver (ekran końcowy)\n\nOtwórz Bootstrap.unity i naciśnij Play.", "OK");
        }

        private static void SetupUrpPipeline()
        {
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = asm.GetType("URPSetup") ?? asm.GetType("MRCrisisTrainer.EditorBootstrap.URPSetup");
                var method = type?.GetMethod("SetupURP", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (method == null) continue;
                method.Invoke(null, null);
                return;
            }

            Debug.LogWarning("[MasterBuilder] URPSetup.SetupURP not found; continuing with current render pipeline settings.");
        }

        private static void ConfigureBuildSettings()
        {
            var order = new[]
            {
                $"{SceneDir}/Bootstrap.unity",
                $"{SceneDir}/MainMenu.unity",
                $"{SceneDir}/Tutorial.unity",
                $"{SceneDir}/TrainingRoom.unity",
                $"{SceneDir}/PostSession.unity",
                $"{SceneDir}/GameOver.unity",
            };
            var list = new List<EditorBuildSettingsScene>();
            foreach (var s in order)
                if (File.Exists(s)) list.Add(new EditorBuildSettingsScene(s, true));
            EditorBuildSettings.scenes = list.ToArray();
            Debug.Log($"[MasterBuilder] Build settings: {list.Count} scenes.");
        }
    }
}
