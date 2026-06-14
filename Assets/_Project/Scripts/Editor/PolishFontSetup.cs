using UnityEditor;
using UnityEngine;
using TMPro;

namespace MRCrisisTrainer.EditorTools
{
    /// <summary>
    /// Gwarantuje polskie znaki w domyślnej czcionce TMP (LiberationSans SDF). Ustawia atlas na Dynamic
    /// i WPISUJE polskie glify do atlasu przy buildzie (rasteryzacja z TTF). Dzięki temu ą/ę/ł/ż/ź/ć/ń/ś/ó
    /// renderują się na urządzeniu, a nie jako puste kwadraty/braki.
    /// </summary>
    public static class PolishFontSetup
    {
        private const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        // małe + wielkie polskie znaki + typografia (cudzysłowy, myślniki, wielokropek)
        private const string Polish = "ąćęłńóśźżĄĆĘŁŃÓŚŹŻ„”“–—…•°";

        [MenuItem("MRCrisis/Ensure Polish Font Glyphs", priority = 32)]
        public static void EnsurePolishGlyphs()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null) { Debug.LogWarning("[PolishFont] LiberationSans SDF nie znaleziono — pomijam."); return; }

            // Atlas dynamiczny → TMP dorysowuje brakujące znaki z TTF (w edytorze i w runtime).
            if (font.atlasPopulationMode != AtlasPopulationMode.Dynamic)
                font.atlasPopulationMode = AtlasPopulationMode.Dynamic;

            bool ok = font.TryAddCharacters(Polish, out string missing);
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
            Debug.Log($"[PolishFont] Polskie glify dopisane do atlasu (ok={ok}, brakujące='{missing}').");
        }
    }
}
