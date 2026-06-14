using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace MRCrisisTrainer.XR
{
    /// <summary>
    /// Czyta przestrzeń przeskanowaną przez gracza (Meta Space Setup / Scene API) i udostępnia
    /// najlepszy „fotel" (COUCH / siedzisko) oraz „stolik" (TABLE), żeby:
    ///   • Akt II ustawił pierścień „usiądź tutaj" na PRAWDZIWEJ kanapie/fotelu,
    ///   • Akt III postawił dzwoniący telefon na PRAWDZIWYM stoliku.
    ///
    /// Działa przez refleksję na typach z Meta XR Core SDK (OVRSceneManager, OVRSceneVolume,
    /// OVRSemanticClassification…), które są już w projekcie — dzięki temu assembly kompiluje się
    /// nawet bez SDK, a brak skanu / brak zgody / Editor => HasSeat/HasTable = false i gra używa
    /// dotychczasowego zachowania (telefon przy ręce, pierścień przed graczem). Zero regresji.
    ///
    /// Pivot kotwicy wolumenu Meta leży na GÓRNEJ ściance bryły (a dla kanapy — na siedzisku),
    /// więc transform.position kotwicy to dokładnie blat stolika / punkt siedzenia. Bez offsetów.
    /// </summary>
    public class ScannedSpaceAnchors : MonoBehaviour
    {
        public static ScannedSpaceAnchors Instance { get; private set; }

        // --- stan publiczny (czytany przez Akt II / Akt III) ---
        public bool Ready { get; private set; }      // próba skanu zakończona (sukces lub nie)
        public bool HasSeat { get; private set; }     // znaleziono kanapę/fotel
        public bool HasTable { get; private set; }    // znaleziono stolik

        public float FloorY { get; private set; } = 0f;

        private const string ScenePermission = "com.oculus.permission.USE_SCENE";

        // etykiety klasyfikacji Meta (OVRSceneManager.Classification)
        private const string L_COUCH = "COUCH";
        private const string L_TABLE = "TABLE";
        private const string L_DESK  = "DESK";
        private const string L_STORAGE = "STORAGE";
        private const string L_OTHER = "OTHER";
        private const string L_FLOOR = "FLOOR";
        private const string L_BED   = "BED";

        // żywe transformy kotwic (czytane na bieżąco, odporne na recenter)
        private Transform _seatT, _tableT, _floorT;
        private Vector3 _seatSnap, _tableSnap;     // zapas, gdy kotwica zniknie

        // uchwyty refleksji
        private Type _smType, _anchorType, _volumeType, _planeType, _classType;
        private MethodInfo _containsMethod;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("ScannedSpaceAnchors");
            go.AddComponent<ScannedSpaceAnchors>();
            // DontDestroyOnLoad ustawiamy w Awake
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            // W Editorze (PlayMode / nagrywanie GIF-a) Scene API działa tylko po Linku i nic nie psuje,
            // ale żeby nie zaśmiecać i nie zmieniać zachowania renderów — od razu „gotowe, brak skanu".
            if (Application.isEditor)
            {
                Ready = true;
                return;
            }
            StartCoroutine(SetupAndScan());
        }

        /// <summary>Czeka aż skan się zakończy (lub timeout). Akt II woła to zanim postawi pierścień.</summary>
        public IEnumerator WaitUntilReady(float timeout)
        {
            float t = 0f;
            while (!Ready && t < timeout) { t += Time.deltaTime; yield return null; }
        }

        /// <summary>Punkt na podłodze pod siedziskiem (x/z fotela, y = podłoga) — cel pierścienia „usiądź".</summary>
        public bool TryGetSeatFloor(out Vector3 pos)
        {
            pos = Vector3.zero;
            if (!HasSeat) return false;
            Vector3 s = _seatT != null ? _seatT.position : _seatSnap;
            pos = new Vector3(s.x, FloorY + 0.02f, s.z);
            return true;
        }

        /// <summary>Blat stolika (środek górnej ścianki) — miejsce na dzwoniący telefon.</summary>
        public bool TryGetTableTop(out Vector3 pos)
        {
            pos = Vector3.zero;
            if (!HasTable) return false;
            pos = _tableT != null ? _tableT.position : _tableSnap;
            return true;
        }

        private IEnumerator SetupAndScan()
        {
            // Wszystko w try/catch-bezpiecznych krokach; każdy błąd => Ready=true, Has*=false (fallback).
            if (!ResolveTypes())
            {
                Debug.LogWarning("[ScannedSpace] Meta Scene API nieobecne — używam pozycji zastępczych.");
                Ready = true;
                yield break;
            }

            // 1) zgoda na dane przestrzenne
            yield return StartCoroutine(EnsureScenePermission());

            // 2) zbuduj szablony „prefabów" (bez nich OVRSceneManager nie instancjonuje mebli)
            object volumePrefab = BuildAnchorTemplate("ScanVolTemplate", _volumeType);
            object planePrefab  = BuildAnchorTemplate("ScanPlaneTemplate", _planeType);

            // 3) zapewnij OVRSceneManager + podłącz szablony
            object sm = EnsureSceneManager(volumePrefab, planePrefab);
            if (sm == null) { Ready = true; yield break; }

            // 4) wczytaj model przestrzeni
            TryInvoke(_smType, sm, "LoadSceneModel");

            // 5) odpytuj instancjonowane kotwice aż znajdziemy meble (max ~10 s)
            float t = 0f;
            while (t < 10f)
            {
                t += 0.25f;
                if (ScanForFurniture()) break;
                yield return new WaitForSeconds(0.25f);
            }

            Ready = true;
            Debug.Log($"[ScannedSpace] gotowe. seat={HasSeat} table={HasTable} floorY={FloorY:0.00}");
        }

        private bool ResolveTypes()
        {
            _smType     = FindType("OVRSceneManager");
            _anchorType = FindType("OVRSceneAnchor");
            _volumeType = FindType("OVRSceneVolume");
            _planeType  = FindType("OVRScenePlane");
            _classType  = FindType("OVRSemanticClassification");
            if (_smType == null || _anchorType == null || _volumeType == null || _classType == null)
                return false;
            _containsMethod = _classType.GetMethod("Contains", new[] { typeof(string) });
            return _containsMethod != null;
        }

        private IEnumerator EnsureScenePermission()
        {
            // Uwaga: nie wolno mieszać yield z try/catch — stąd osobne (statyczne) helpery.
            if (HasScenePermission()) yield break;
            RequestScenePermission();

            // poczekaj aż użytkownik przyzna (max 12 s)
            float t = 0f;
            while (t < 12f)
            {
                if (HasScenePermission()) yield break;
                t += 0.3f;
                yield return new WaitForSeconds(0.3f);
            }
        }

        private static bool HasScenePermission()
        {
            try { return UnityEngine.Android.Permission.HasUserAuthorizedPermission(ScenePermission); }
            catch { return false; }
        }

        private static void RequestScenePermission()
        {
            try { UnityEngine.Android.Permission.RequestUserPermission(ScenePermission); }
            catch (Exception e) { Debug.LogWarning("[ScannedSpace] permission: " + e.Message); }
        }

        /// <summary>Tworzy nieaktywny obiekt-szablon z OVRSceneAnchor + (volume|plane) + klasyfikacją.</summary>
        private object BuildAnchorTemplate(string name, Type geometryType)
        {
            try
            {
                var go = new GameObject(name);
                go.SetActive(false);
                DontDestroyOnLoad(go);
                var anchorComp = go.AddComponent(_anchorType);   // OVRSceneAnchor
                if (geometryType != null) go.AddComponent(geometryType);
                go.AddComponent(_classType);                      // OVRSemanticClassification
                return anchorComp;                                // pole *Prefab oczekuje OVRSceneAnchor
            }
            catch (Exception e) { Debug.LogWarning("[ScannedSpace] template: " + e.Message); return null; }
        }

        private object EnsureSceneManager(object volumePrefab, object planePrefab)
        {
            try
            {
                var existing = FindObjectByType(_smType);
                object sm = existing;
                if (sm == null)
                {
                    var go = new GameObject("OVRSceneManager");
                    sm = go.AddComponent(_smType);
                    DontDestroyOnLoad(go);
                }
                SetMember(_smType, sm, "VolumePrefab", volumePrefab);
                SetMember(_smType, sm, "PlanePrefab", planePrefab);
                SetMember(_smType, sm, "ActiveRoomsOnly", true);
                // meble/pokoje pod naszym (DontDestroyOnLoad) obiektem — przetrwają zmianę sceny
                SetMember(_smType, sm, "InitialAnchorParent", transform);
                return sm;
            }
            catch (Exception e) { Debug.LogWarning("[ScannedSpace] sceneManager: " + e.Message); return null; }
        }

        /// <summary>Przegląda zinstancjonowane kotwice, wybiera fotel + stolik. Zwraca true gdy mamy komplet.</summary>
        private bool ScanForFurniture()
        {
            UnityEngine.Object[] classifications;
            try { classifications = UnityEngine.Object.FindObjectsByType(_classType, FindObjectsSortMode.None); }
            catch { return false; }
            if (classifications == null || classifications.Length == 0) return false;

            float bestSeatScore = float.MaxValue;   // preferuj niskie siedzisko (~0.45 m)
            float bestTableScore = float.MaxValue;   // preferuj blat ~0.4–1.1 m

            foreach (var obj in classifications)
            {
                var comp = obj as Component;
                if (comp == null) continue;
                var t = comp.transform;

                // podłoga — do wysokości pierścienia
                if (_floorT == null && HasLabel(comp, L_FLOOR)) { _floorT = t; FloorY = t.position.y; }

                bool isVolume = comp.GetComponent(_volumeType) != null;
                if (!isVolume) continue;   // fotel i stolik to bryły 3D

                float height = ReadVolumeHeight(comp);   // wysokość bryły (m); 0 gdy brak

                // --- FOTEL / KANAPA ---
                bool seatLabel = HasLabel(comp, L_COUCH);
                bool seatFallback = !seatLabel && (HasLabel(comp, L_OTHER) || HasLabel(comp, L_BED)) && height > 0.25f && height < 0.75f;
                if (seatLabel || seatFallback)
                {
                    float score = seatLabel ? 0f : Mathf.Abs(height - 0.45f) + 1f; // realna kanapa wygrywa
                    if (score < bestSeatScore)
                    {
                        bestSeatScore = score; _seatT = t; _seatSnap = t.position; HasSeat = true;
                    }
                }

                // --- STOLIK ---
                bool tableLabel = HasLabel(comp, L_TABLE) || HasLabel(comp, L_DESK);
                bool tableFallback = !tableLabel && HasLabel(comp, L_STORAGE) && height > 0.35f;
                if (tableLabel || tableFallback)
                {
                    float score = tableLabel ? 0f : 1f;
                    if (score < bestTableScore)
                    {
                        bestTableScore = score; _tableT = t; _tableSnap = t.position; HasTable = true;
                    }
                }
            }

            // mamy komplet (lub przynajmniej jedno) — można skończyć wcześnie gdy oba pewne
            return HasSeat && HasTable && bestSeatScore == 0f && bestTableScore == 0f;
        }

        private float ReadVolumeHeight(Component anchorGoComp)
        {
            try
            {
                var vol = anchorGoComp.GetComponent(_volumeType);
                if (vol == null) return 0f;
                var p = _volumeType.GetProperty("Height");
                if (p != null) return (float)p.GetValue(vol);
            }
            catch { }
            return 0f;
        }

        private bool HasLabel(Component classificationComp, string label)
        {
            try
            {
                var cls = classificationComp.GetComponent(_classType);
                if (cls == null) return false;
                return (bool)_containsMethod.Invoke(cls, new object[] { label });
            }
            catch { return false; }
        }

        // ---------- helpers refleksji ----------
        private static void TryInvoke(Type type, object obj, string method)
        {
            try { type.GetMethod(method, Type.EmptyTypes)?.Invoke(obj, null); }
            catch (Exception e) { Debug.LogWarning($"[ScannedSpace] {method}: {e.Message}"); }
        }

        private static void SetMember(Type type, object obj, string name, object value)
        {
            try
            {
                var p = type.GetProperty(name);
                if (p != null && p.CanWrite) { p.SetValue(obj, value); return; }
                var f = type.GetField(name);
                if (f != null) { f.SetValue(obj, value); return; }
            }
            catch (Exception e) { Debug.LogWarning($"[ScannedSpace] set {name}: {e.Message}"); }
        }

        private static Type FindType(string typeName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(typeName);
                if (t != null) return t;
            }
            return null;
        }

        private static UnityEngine.Object FindObjectByType(Type type)
        {
#if UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindFirstObjectByType(type);
#else
            return UnityEngine.Object.FindObjectOfType(type);
#endif
        }
    }
}
