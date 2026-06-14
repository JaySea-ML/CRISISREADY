using UnityEditor;
using UnityEngine;

namespace MRCrisisTrainer.EditorTools
{
    /// <summary>
    /// Fabryka materiałów URP używających proceduralnie wygenerowanych tekstur.
    /// Współdzielona przez LabRoomBuilder i TrainingRoomBuilder.
    /// </summary>
    public static class LabMaterials
    {
        private const string MatDir = "Assets/_Project/Materials/Lab";
        private const string TexDir = "Assets/_Project/Textures";

        public static Material Wall, Floor, Ceiling, Wood, WoodLight, FabricDark, FabricSofa,
            Window, WindowView, PosterBci, PosterVr, PosterEeg, Screen, Metal, Radiator, LED, Plastic;

        public static void BuildAll()
        {
            EnsureDir(MatDir);
            Floor = TexMat("M_Floor", "floor_albedo", new Color(0.84f, 0.78f, 0.62f), 0.15f, 4);
            Wall = TexMat("M_Wall", "wall_albedo", new Color(0.96f, 0.96f, 0.94f), 0.05f, 3);
            Ceiling = SolidMat("M_Ceiling", new Color(0.98f, 0.98f, 0.96f), 0.05f);
            Wood = TexMat("M_Wood", "wood_albedo", new Color(0.55f, 0.4f, 0.25f), 0.3f, 1);
            WoodLight = TexMat("M_WoodLight", "wood_light_albedo", new Color(0.7f, 0.55f, 0.35f), 0.25f, 2);
            FabricDark = TexMat("M_FabricDark", "fabric_dark_albedo", new Color(0.1f, 0.1f, 0.12f), 0.2f, 2);
            FabricSofa = TexMat("M_FabricSofa", "fabric_sofa_albedo", new Color(0.3f, 0.2f, 0.16f), 0.2f, 2);
            Window = GlassMat("M_Window");
            WindowView = TexMat("M_WindowView", "window_view", new Color(0.6f, 0.75f, 0.85f), 0.05f, 1, emissive: true);
            PosterBci = TexMat("M_PosterBci", "poster_bci", Color.white, 0.1f, 1);
            PosterVr = TexMat("M_PosterVr", "poster_vr", Color.white, 0.1f, 1);
            PosterEeg = TexMat("M_PosterEeg", "poster_eeg", Color.white, 0.1f, 1);
            Screen = TexMat("M_Screen", "screen_off", new Color(0.04f, 0.04f, 0.05f), 0.85f, 1);
            Metal = SolidMat("M_Metal", new Color(0.22f, 0.22f, 0.24f), 0.6f, 0.7f);
            Radiator = SolidMat("M_Radiator", new Color(0.94f, 0.94f, 0.92f), 0.3f);
            LED = SolidMat("M_LED", new Color(0.95f, 0.97f, 1f), 0.5f, emission: new Color(1f, 0.98f, 0.95f) * 2.2f);
            Plastic = SolidMat("M_Plastic", new Color(0.78f, 0.78f, 0.78f), 0.5f);
            AssetDatabase.SaveAssets();
        }

        private static Shader URP() => Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        private static Texture2D LoadTex(string name)
        {
            var path = $"{TexDir}/{name}.png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Material TexMat(string name, string tex, Color tint, float smooth, float tile, bool emissive = false)
        {
            var mat = LoadOrCreate(name);
            var t = LoadTex(tex);
            if (t != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", t);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", t);
                mat.mainTextureScale = new Vector2(tile, tile);
            }
            SetColor(mat, tint);
            SetSmooth(mat, smooth);
            if (emissive && mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                mat.SetColor("_EmissionColor", new Color(0.3f, 0.35f, 0.4f));
                if (t != null) mat.SetTexture("_EmissionMap", t);
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material SolidMat(string name, Color c, float smooth, float metallic = 0f, Color? emission = null)
        {
            var mat = LoadOrCreate(name);
            SetColor(mat, c);
            SetSmooth(mat, smooth);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (emission.HasValue && mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                mat.SetColor("_EmissionColor", emission.Value);
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material GlassMat(string name)
        {
            var mat = LoadOrCreate(name);
            // URP transparent surface
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f); // transparent
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = 3000;
            SetColor(mat, new Color(0.7f, 0.85f, 0.95f, 0.25f));
            SetSmooth(mat, 0.95f);
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material LoadOrCreate(string name)
        {
            var path = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(URP()); AssetDatabase.CreateAsset(mat, path); }
            return mat;
        }

        private static void SetColor(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            else m.color = c;
        }

        private static void SetSmooth(Material m, float s)
        {
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", s);
        }

        private static void EnsureDir(string dir)
        {
            var parts = dir.Split('/');
            var path = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = $"{path}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(path, parts[i]);
                path = next;
            }
        }
    }
}
