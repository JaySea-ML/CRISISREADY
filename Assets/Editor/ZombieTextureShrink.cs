using UnityEditor;
using UnityEngine;

/// <summary>Zmniejsza ogromne tekstury zombie (4K-8K) do ≤1024 — mniejszy APK (pewny transfer przez niestabilne
/// WiFi/USB) i mniejsze zużycie VRAM na Queście. Potem buduje APK.</summary>
public static class ZombieTextureShrink
{
    public static void ShrinkAndBuild()
    {
        ShrinkOnly();
        MRCrisisTrainer.EditorTools.BuildScript.BuildAndroidApk();
    }

    public static void ShrinkOnly()
    {
        string dir = "Assets/_Project/External/Act3/ZombieHitman/textures";
        int changed = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { dir }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) continue;
            if (imp.maxTextureSize > 1024)
            {
                imp.maxTextureSize = 1024;
                imp.SaveAndReimport();
                changed++;
            }
        }
        Debug.Log($"[ZombieShrink] downscaled {changed} textures to <=1024");
    }
}
