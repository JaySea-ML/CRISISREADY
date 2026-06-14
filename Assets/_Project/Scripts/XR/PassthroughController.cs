using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MRCrisisTrainer.XR
{
    public class PassthroughController : MonoBehaviour
    {
        private bool reRegistering;
        private static GameObject ovrRig;   // JEDEN trwały zestaw OVRManager + OVRPassthroughLayer na całą grę
        private static PassthroughController instance;
        public static PassthroughController Instance
        {
            get
            {
                if (instance == null) instance = null;
                return instance;
            }
            private set => instance = value;
        }

        [SerializeField] private bool enableOnStart = true;
        [SerializeField] private Camera xrCamera;

        public bool IsPassthroughActive { get; private set; }

        void Awake()
        {
            if (xrCamera == null) xrCamera = Camera.main;

            if (Instance != null && Instance != this)
            {
                Instance.SetCameraIfAvailable(xrCamera);
                Destroy(this);
                return;
            }
            Instance = this;

            // Only the bootstrap-created manager persists. XR rig copies its camera into
            // that manager and removes this duplicate component, keeping the rig alive.
            if (gameObject.name == "PassthroughController")
            {
                DontDestroyOnLoad(gameObject);
                // Po KAŻDEJ zmianie sceny nowa kamera → ponownie podłącz passthrough i zarejestruj warstwę.
                // To naprawia czerń w menu ORAZ czerń po GRAJ (warstwa rejestruje się w każdej scenie).
                SceneManager.sceneLoaded += OnSceneLoaded;
            }
        }

        void OnDestroy()
        {
            if (instance == this)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                instance = null;
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!IsPassthroughActive) return;
            xrCamera = null;          // wymuś ponowne wyszukanie Camera.main z nowej sceny
            EnablePassthrough();      // ustawia przezroczyste czyszczenie + restart pętli rejestracji warstwy
        }

        void Start()
        {
            if (enableOnStart) EnablePassthrough();
        }

        public void EnablePassthrough()
        {
            ResolveCamera();

            if (xrCamera != null)
            {
                xrCamera.clearFlags = CameraClearFlags.SolidColor;
                xrCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            }

            ApplyMetaPassthrough(true);
            IsPassthroughActive = true;
            // KLUCZ na „czarny ekran": warstwa passthrough musi się zarejestrować PO inicjalizacji OVRManager.
            // Wymuszamy ponowne OnEnable warstwy po kilku klatkach (inaczej numLayers:0 → czerń).
            if (isActiveAndEnabled && !reRegistering) StartCoroutine(ReRegisterLayer());
            Debug.Log("[Passthrough] enabled (transparent clear, Meta layer requested)");
        }

        private static bool layerKicked;   // czy warstwa została już raz „kopnięta" do rejestracji
        private IEnumerator ReRegisterLayer()
        {
            reRegistering = true;
            var ptType = FindTypeInLoadedAssemblies("OVRPassthroughLayer");
            // Pierwsze uruchomienie (Bootstrap): „kopnij" warstwę off→on kilka razy, aż OVRManager zainicjuje passthrough
            // → wtedy OnEnable warstwy rejestruje insight layer (numLayers:1). Warstwa jest trwała, więc zostaje na zawsze.
            // Kolejne sceny: warstwa już zarejestrowana → tylko upewnij się że enabled (BEZ toggla = bez migotania).
            int attempts = layerKicked ? 1 : 8;
            for (int attempt = 0; attempt < attempts && IsPassthroughActive; attempt++)
            {
                yield return new WaitForSeconds(attempt == 0 ? 0.1f : 0.4f);
                if (ptType == null) { ptType = FindTypeInLoadedAssemblies("OVRPassthroughLayer"); if (ptType == null) continue; }
                var layer = FindObjectByType(ptType) as Behaviour;
                if (layer == null) continue;
                ConfigureLayer(ptType, layer);
                SetMember(ptType, layer, "hidden", false);
                if (layer.gameObject != null) layer.gameObject.SetActive(true);
                if (!layerKicked)
                {
                    layer.enabled = false;   // wymuś ponowne OnEnable → CreateInsightPassthroughLayer gdy OVRManager gotowy
                    yield return null;
                }
                layer.enabled = true;
            }
            layerKicked = true;
            Debug.Log("[Passthrough] layer kick/ensure done");
            reRegistering = false;
        }

        public void DisablePassthrough()
        {
            ResolveCamera();
            if (xrCamera != null)
            {
                xrCamera.clearFlags = CameraClearFlags.Skybox;
            }
            ApplyMetaPassthrough(false);
            IsPassthroughActive = false;
            Debug.Log("[Passthrough] disabled (full VR mode)");
        }

        private void SetCameraIfAvailable(Camera camera)
        {
            if (camera != null) xrCamera = camera;
        }

        private Camera ResolveCamera()
        {
            if (xrCamera == null) xrCamera = Camera.main;
            return xrCamera;
        }

        private void ApplyMetaPassthrough(bool enabled)
        {
            // OVRManager is found via reflection so the runtime asmdef compiles
            // even when Meta XR Core SDK is not installed.
            var ovrManagerType = FindTypeInLoadedAssemblies("OVRManager");
            if (ovrManagerType == null)
            {
                Debug.LogWarning("[Passthrough] OVRManager not found - install Meta XR Core SDK package.");
                return;
            }

            // DOKŁADNIE JEDEN OVRManager + warstwa na całą grę (tworzone raz, nigdy nie niszczone).
            // To kończy „wojnę singletonów" (OnDestroy→Awake→AddComponent w pętli → xrDestroyPassthroughFB → numLayers:0).
            EnsureOvrRig(ovrManagerType);
            if (ovrRig == null) return;

            // OVRManager: capability + enable (oba muszą być włączone — wg dokumentacji Meta)
            var mgr = ovrRig.GetComponent(ovrManagerType);
            if (mgr != null)
            {
                SetMember(ovrManagerType, mgr, "isInsightPassthroughCapabilityEnabled", true);
                var insightProp = ovrManagerType.GetProperty("isInsightPassthroughEnabled");
                insightProp?.SetValue(mgr, enabled);
            }

            // Warstwa: konfiguruj + pokaż/ukryj (NIE niszcz — tylko enabled/hidden, zgodnie z zaleceniem Meta dla zmian scen)
            var layerType = FindTypeInLoadedAssemblies("OVRPassthroughLayer");
            if (layerType != null)
            {
                var layer = ovrRig.GetComponent(layerType) as Behaviour;
                if (layer != null)
                {
                    ConfigureLayer(layerType, layer);
                    SetMember(layerType, layer, "hidden", !enabled);
                    layer.enabled = enabled;
                }
            }
            Debug.Log($"[Passthrough] ApplyMetaPassthrough({enabled}) — pojedynczy trwały OVR rig, bez re-create");
        }

        /// <summary>Tworzy LUB adoptuje dokładnie jeden trwały OVRManager + OVRPassthroughLayer (na jednym obiekcie,
        /// DontDestroyOnLoad). Static guard + adopcja istniejącego = brak duplikatów i brak niszczenia między scenami.</summary>
        private static void EnsureOvrRig(System.Type ovrManagerType)
        {
            if (ovrRig != null) return;

            // Adoptuj istniejący OVRManager (gdyby gdzieś był) zamiast tworzyć drugi → brak konfliktu singletonów.
            var existing = FindObjectByType(ovrManagerType) as Component;
            if (existing != null) ovrRig = existing.gameObject;
            else { ovrRig = new GameObject("MR_Passthrough"); ovrRig.AddComponent(ovrManagerType); }
            DontDestroyOnLoad(ovrRig);

            // Warstwa na TYM SAMYM obiekcie (jeśli jej nie ma)
            var layerType = FindTypeInLoadedAssemblies("OVRPassthroughLayer");
            if (layerType != null && ovrRig.GetComponent(layerType) == null)
            {
                var layer = ovrRig.AddComponent(layerType);
                ConfigureLayer(layerType, layer);
            }
            Debug.Log("[Passthrough] OVR rig utworzony/zaadoptowany RAZ (DontDestroyOnLoad) — brak duplikatów");
        }

        private static void ConfigureLayer(System.Type layerType, object comp)
        {
            // overlayType = Underlay (realny świat renderuje się ZA wirtualnymi obiektami)
            SetEnumByName(layerType, comp, "overlayType", "Underlay");
            // projectionSurfaceType = Reconstructed (automatyczna rekonstrukcja otoczenia)
            SetEnumByName(layerType, comp, "projectionSurfaceType", "Reconstructed");
            SetMember(layerType, comp, "textureOpacity", 1f);
        }

        private static void SetMember(System.Type type, object obj, string name, object value)
        {
            try
            {
                var p = type.GetProperty(name);
                if (p != null && p.CanWrite) { p.SetValue(obj, value); return; }
                var f = type.GetField(name);
                if (f != null) f.SetValue(obj, value);
            }
            catch (System.Exception e) { Debug.LogWarning($"[Passthrough] set {name} failed: {e.Message}"); }
        }

        private static void SetEnumByName(System.Type type, object obj, string member, string enumValueName)
        {
            try
            {
                var p = type.GetProperty(member);
                System.Type enumType = p != null ? p.PropertyType : type.GetField(member)?.FieldType;
                if (enumType == null || !enumType.IsEnum) return;
                object val = System.Enum.Parse(enumType, enumValueName);
                SetMember(type, obj, member, val);
            }
            catch (System.Exception e) { Debug.LogWarning($"[Passthrough] set enum {member} failed: {e.Message}"); }
        }

        private static System.Type FindTypeInLoadedAssemblies(string typeName)
        {
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(typeName);
                if (t != null) return t;
            }
            return null;
        }

        private static UnityEngine.Object FindObjectByType(System.Type type)
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType(type);
#else
            return Object.FindObjectOfType(type);
#endif
        }
    }
}
