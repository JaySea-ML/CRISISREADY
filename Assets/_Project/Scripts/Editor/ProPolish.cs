using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MRCrisisTrainer.EditorTools
{
    /// <summary>
    /// „Profesjonalny" look: tworzy VolumeProfile z post-processingiem (tonemapping, color grading,
    /// bloom, vignette, white balance), przypina go jako domyślny global volume URP oraz włącza
    /// miękkie cienie + HDR. To zamienia płaski, „amatorski" obraz w kinowy bez dużego kosztu na Quest.
    /// </summary>
    public static class ProPolish
    {
        private const string ProfilePath = "Assets/Settings/MRCrisis_PostFX.asset";
        private const string UrpPath = "Assets/Settings/MRCrisis_URP.asset";

        [MenuItem("MRCrisis/Setup Pro PostFX", priority = 32)]
        public static void SetupPostFX()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            // Tonemapping — Neutral (filmowe rolloff, brak prześwietleń)
            var tm = GetOrAdd<Tonemapping>(profile);
            tm.mode.overrideState = true; tm.mode.value = TonemappingMode.Neutral;

            // Color grading — wyraźny kontrast + nasycenie + ciepły filtr (kinowy, nie płaski)
            var ca = GetOrAdd<ColorAdjustments>(profile);
            ca.postExposure.overrideState = true; ca.postExposure.value = 0.1f;
            ca.contrast.overrideState = true; ca.contrast.value = 22f;
            ca.saturation.overrideState = true; ca.saturation.value = 12f;
            ca.colorFilter.overrideState = true; ca.colorFilter.value = new Color(1f, 0.97f, 0.92f);

            // Shadows/Midtones/Highlights — lekko podbite cienie w stronę chłodu, światła w stronę ciepła (filmowo)
            var smh = GetOrAdd<ShadowsMidtonesHighlights>(profile);
            smh.shadows.overrideState = true; smh.shadows.value = new Vector4(0.95f, 0.98f, 1.08f, 0f);
            smh.highlights.overrideState = true; smh.highlights.value = new Vector4(1.06f, 1.02f, 0.94f, 0f);

            // Bloom — poświata na światłach (lampka, ekran, syrena)
            var bloom = GetOrAdd<Bloom>(profile);
            bloom.intensity.overrideState = true; bloom.intensity.value = 0.6f;
            bloom.threshold.overrideState = true; bloom.threshold.value = 1.0f;
            bloom.scatter.overrideState = true; bloom.scatter.value = 0.65f;
            bloom.tint.overrideState = true; bloom.tint.value = new Color(1f, 0.95f, 0.88f);

            // Vignette — ściemnienie brzegów (skupia wzrok, klimat thrillera)
            var vig = GetOrAdd<Vignette>(profile);
            vig.intensity.overrideState = true; vig.intensity.value = 0.28f;
            vig.smoothness.overrideState = true; vig.smoothness.value = 0.5f;

            // White balance — ciut cieplej
            var wb = GetOrAdd<WhiteBalance>(profile);
            wb.temperature.overrideState = true; wb.temperature.value = 6f;

            EditorUtility.SetDirty(profile);

            // Przypnij profil jako domyślny global volume + włącz miękkie cienie + HDR na URP
            var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(UrpPath);
            if (urp != null)
            {
                var so = new SerializedObject(urp);
                SetIfExists(so, "m_VolumeProfile", profile);
                SetBoolIfExists(so, "m_SoftShadowsSupported", true);
                SetBoolIfExists(so, "m_PrefilterSoftShadows", true);
                SetBoolIfExists(so, "m_SupportsHDR", true);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(urp);
            }
            else Debug.LogWarning("[ProPolish] URP asset not found at " + UrpPath);

            AssetDatabase.SaveAssets();
            Debug.Log("[ProPolish] Pro PostFX profile + soft shadows + HDR applied.");
        }

        private static T GetOrAdd<T>(VolumeProfile p) where T : VolumeComponent
        {
            if (p.TryGet<T>(out var existing)) return existing;
            var c = p.Add<T>(true);
            c.hideFlags = HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(c, p);
            return c;
        }

        private static void SetIfExists(SerializedObject so, string prop, Object value)
        {
            var p = so.FindProperty(prop);
            if (p != null) p.objectReferenceValue = value;
        }

        private static void SetBoolIfExists(SerializedObject so, string prop, bool value)
        {
            var p = so.FindProperty(prop);
            if (p != null) p.boolValue = value;
        }
    }
}
