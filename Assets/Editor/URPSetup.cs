using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MRCrisisTrainer.EditorBootstrap
{
    /// <summary>
    /// Tworzy i przypisuje UniversalRenderPipelineAsset. Bez aktywnego URP pipeline
    /// wszystkie materiały URP/Lit renderują się na MAGENTA (Built-in nie zna shaderów URP).
    /// W projekcie był tylko UniversalRenderPipelineGlobalSettings, brakowało samego pipeline assetu.
    /// </summary>
    public static class URPSetup
    {
        private const string Dir = "Assets/Settings";
        private const string RendererPath = Dir + "/MRCrisis_Renderer.asset";
        private const string UrpPath = Dir + "/MRCrisis_URP.asset";

        [MenuItem("MRCrisis/Setup URP Pipeline", priority = 1)]
        public static void SetupURP()
        {
            if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets", "Settings");

            // 1. Renderer data (Universal Renderer)
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (rendererData == null)
            {
                rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, RendererPath);
                Debug.Log("[URPSetup] Created UniversalRendererData.");
            }

            // 2. URP asset
            var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(UrpPath);
            if (urp == null)
            {
                urp = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(urp, UrpPath);
                Debug.Log("[URPSetup] Created UniversalRenderPipelineAsset.");
            }

            // 3. Ustawienia pod Quest 3 (mobile VR)
            urp.msaaSampleCount = 4;
            urp.supportsHDR = false;
            urp.renderScale = 1.0f;
            urp.shadowDistance = 25f;
            EditorUtility.SetDirty(urp);
            EditorUtility.SetDirty(rendererData);
            AssetDatabase.SaveAssets();

            // 4. Przypisz jako domyślny pipeline (GraphicsSettings) + wszystkie poziomy quality
            GraphicsSettings.defaultRenderPipeline = urp;

            int startLevel = QualitySettings.GetQualityLevel();
            int levels = QualitySettings.names.Length;
            for (int i = 0; i < levels; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = urp;
            }
            QualitySettings.SetQualityLevel(startLevel, false);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[URPSetup] URP przypisany: GraphicsSettings.defaultRenderPipeline + {levels} poziomów quality. Asset: {UrpPath}");
        }
    }
}
