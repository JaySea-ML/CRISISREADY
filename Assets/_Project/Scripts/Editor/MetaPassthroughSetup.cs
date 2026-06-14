using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace MRCrisisTrainer.EditorTools
{
    /// <summary>
    /// Włącza passthrough w OVRProjectConfig (przez refleksję), żeby APK Quest 3 miał
    /// w manifeście feature passthrough. Bez tego OVRManager.isInsightPassthroughEnabled
    /// nie pokaże realnego obrazu na urządzeniu.
    /// </summary>
    public static class MetaPassthroughSetup
    {
        [MenuItem("MRCrisis/Enable Passthrough In Project Config", priority = 31)]
        public static void EnablePassthroughSupport()
        {
            var cfgType = FindType("OVRProjectConfig");
            if (cfgType == null)
            {
                Debug.LogWarning("[MetaPassthroughSetup] OVRProjectConfig not found (Meta XR SDK?).");
                return;
            }

            object config = GetProjectConfig(cfgType);
            if (config == null) { Debug.LogWarning("[MetaPassthroughSetup] Could not get project config."); return; }

            // insightPassthroughSupport = Supported  (lub starsze: insightPassthroughEnabled = true)
            if (!SetEnumByName(cfgType, config, "insightPassthroughSupport", "Supported"))
                SetBool(cfgType, config, "insightPassthroughEnabled", true);

            // Hand tracking: główny input = RĘCE (kontrolery zostają jako cichy fallback). Max częstotliwość = płynne dłonie.
            SetEnumByName(cfgType, config, "handTrackingSupport", "ControllersAndHands");
            SetEnumByName(cfgType, config, "handTrackingFrequency", "MAX");

            // Scene API + Anchors — żeby gra mogła czytać przeskanowaną przestrzeń (fotel + stolik).
            // sceneSupport = Supported (FeatureSupport), anchorSupport = Enabled (lub bool true w starszych SDK).
            if (!SetEnumByName(cfgType, config, "sceneSupport", "Supported"))
                SetBool(cfgType, config, "sceneSupportEnabled", true);
            if (!SetEnumByName(cfgType, config, "anchorSupport", "Enabled"))
                SetBool(cfgType, config, "anchorSupport", true);
            // spatial anchors / shared anchors — jeśli dana wersja SDK je rozróżnia (best-effort)
            SetEnumByName(cfgType, config, "spatialAnchorsSupport", "Enabled");

            CommitProjectConfig(cfgType, config);
            EditorUtility.SetDirty(config as UnityEngine.Object);
            AssetDatabase.SaveAssets();
            Debug.Log("[MetaPassthroughSetup] Passthrough support enabled in OVRProjectConfig.");
        }

        private static object GetProjectConfig(Type cfgType)
        {
            // Różne wersje SDK: GetProjectConfig() statyczne
            var m = cfgType.GetMethod("GetProjectConfig", BindingFlags.Static | BindingFlags.Public, null, Type.EmptyTypes, null);
            if (m != null) return m.Invoke(null, null);
            var p = cfgType.GetProperty("CachedProjectConfig", BindingFlags.Static | BindingFlags.Public);
            if (p != null) return p.GetValue(null);
            return null;
        }

        private static void CommitProjectConfig(Type cfgType, object config)
        {
            var m = cfgType.GetMethod("CommitProjectConfig", BindingFlags.Static | BindingFlags.Public);
            if (m != null) { m.Invoke(null, new[] { config }); return; }
            // fallback: instancyjne ApplyConfig?
            var im = cfgType.GetMethod("CommitProjectConfig", BindingFlags.Instance | BindingFlags.Public);
            im?.Invoke(config, null);
        }

        private static bool SetEnumByName(Type type, object obj, string member, string valueName)
        {
            try
            {
                var f = type.GetField(member);
                Type et = f != null ? f.FieldType : type.GetProperty(member)?.PropertyType;
                if (et == null || !et.IsEnum) return false;
                if (!Enum.IsDefined(et, valueName)) return false;
                object val = Enum.Parse(et, valueName);
                if (f != null) { f.SetValue(obj, val); return true; }
                var p = type.GetProperty(member);
                if (p != null && p.CanWrite) { p.SetValue(obj, val); return true; }
            }
            catch (Exception e) { Debug.LogWarning($"[MetaPassthroughSetup] {member}: {e.Message}"); }
            return false;
        }

        private static void SetBool(Type type, object obj, string member, bool value)
        {
            var f = type.GetField(member);
            if (f != null && f.FieldType == typeof(bool)) { f.SetValue(obj, value); return; }
            var p = type.GetProperty(member);
            if (p != null && p.PropertyType == typeof(bool) && p.CanWrite) p.SetValue(obj, value);
        }

        /// <summary>PRZY BUILDZIE ustawia tylko przezroczyste czyszczenie kamery (alpha 0), żeby kompozytor mógł
        /// pokazać realny obraz pod sceną. NIE tworzy już OVRManagera per-scena — to powodowało wojnę singletonów
        /// (OnDestroy→Awake w pętli → xrDestroyPassthroughFB → numLayers:0 → czerń). OVRManager + warstwę tworzy
        /// TERAZ jeden raz w runtime PassthroughController.EnsureOvrRig (DontDestroyOnLoad), wspólny dla wszystkich scen.</summary>
        public static void EnsureSceneOVRPassthrough(GameObject rigRoot, Camera cam)
        {
            if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0f, 0f, 0f, 0f); }
            Debug.Log("[Passthrough] build-time: kamera przezroczysta (zestaw OVR budowany RAZ w Bootstrap, nie per-scena).");
        }

        /// <summary>Buduje DOKŁADNIE JEDEN trwały zestaw passthrough — OVRManager + OVRPassthroughLayer na obiekcie
        /// MR_Passthrough z PersistAcrossScenes (DontDestroyOnLoad). Wywoływany TYLKO w scenie Bootstrap. Warstwa jest
        /// serializowanym komponentem sceny (jak w przykładach Meta) → rejestruje się poprawnie (numLayers:1), a że jest
        /// tylko jeden i przeżywa wszystkie sceny — nie ma duplikatów ani wojny singletonów (koniec czerni).</summary>
        public static void BuildPersistentPassthroughRig()
        {
            var mgrType = FindType("OVRManager");
            if (mgrType == null) { Debug.LogWarning("[Passthrough] OVRManager type missing (Meta SDK?) — pomijam Bootstrap rig."); return; }

            var go = new GameObject("MR_Passthrough");
            go.AddComponent<MRCrisisTrainer.XR.PersistAcrossScenes>();

            var mgr = go.AddComponent(mgrType) as UnityEngine.Object;
            if (mgr != null)
            {
                var mso = new SerializedObject(mgr);
                var it = mso.GetIterator();
                while (it.NextVisible(true))
                    if (it.propertyType == SerializedPropertyType.Boolean && it.name.ToLower().Contains("insightpassthrough"))
                        it.boolValue = true;
                mso.ApplyModifiedPropertiesWithoutUndo();
            }

            var layerType = FindType("OVRPassthroughLayer");
            if (layerType != null)
            {
                var layer = go.AddComponent(layerType) as UnityEngine.Object;
                if (layer != null)
                {
                    var lso = new SerializedObject(layer);
                    var lit = lso.GetIterator();
                    while (lit.NextVisible(true))
                    {
                        var n = lit.name.ToLower();
                        if (lit.propertyType == SerializedPropertyType.Enum)
                        {
                            if (n.Contains("overlaytype")) SetEnumProp(lit, "Underlay");
                            else if (n.Contains("projectionsurface")) SetEnumProp(lit, "Reconstructed");
                        }
                        else if (lit.propertyType == SerializedPropertyType.Float && n.Contains("opacity")) lit.floatValue = 1f;
                        else if (lit.propertyType == SerializedPropertyType.Boolean && n.Contains("hidden")) lit.boolValue = false;
                    }
                    lso.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            Debug.Log("[Passthrough] Bootstrap: zbudowano JEDEN trwały MR_Passthrough (OVRManager + warstwa Underlay).");
        }

        private static void SetEnumProp(SerializedProperty p, string enumName)
        {
            var names = p.enumNames;
            for (int i = 0; i < names.Length; i++) if (names[i] == enumName) { p.enumValueIndex = i; return; }
        }

        private static Type FindType(string name)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(name);
                if (t != null) return t;
            }
            return null;
        }
    }
}
