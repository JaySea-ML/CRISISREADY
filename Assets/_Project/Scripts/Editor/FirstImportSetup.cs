using System.IO;
using UnityEditor;
using UnityEngine;

namespace MRCrisisTrainer.EditorTools
{
    [InitializeOnLoad]
    public static class FirstImportSetup
    {
        private const string MarkerPath = "Assets/_Project/.scenes_built.marker";

        static FirstImportSetup()
        {
            EditorApplication.delayCall += TryBuildOnce;
        }

        private static void TryBuildOnce()
        {
            if (File.Exists(MarkerPath)) return;

            var bootstrapPath = "Assets/_Project/Scenes/Bootstrap.unity";
            var trainingPath = "Assets/_Project/Scenes/TrainingRoom.unity";
            if (File.Exists(bootstrapPath) && File.Exists(trainingPath))
            {
                File.WriteAllText(MarkerPath, "scenes already exist");
                return;
            }

            if (Application.isBatchMode)
            {
                File.WriteAllText(MarkerPath, "batch deferred to explicit builder");
                return;
            }

            bool shouldBuild;
            shouldBuild = EditorUtility.DisplayDialog(
                "MR Crisis Trainer - First Import",
                "Wykryto świeży import projektu. Chcesz teraz wygenerować sceny (Bootstrap + MainMenu + TrainingRoom + PostSession)?\n\nMożesz to zrobić później przez menu: MRCrisis -> ★ BUILD EVERYTHING.",
                "Tak, zbuduj teraz",
                "Później");

            if (shouldBuild)
            {
                MasterBuilder.BuildEverything();
            }

            File.WriteAllText(MarkerPath, "ok");
            AssetDatabase.Refresh();
        }
    }
}
