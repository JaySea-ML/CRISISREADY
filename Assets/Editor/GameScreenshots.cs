using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace MRCrisisTrainer.EditorBootstrap
{
    /// <summary>
    /// Renderuje widoki gry do PNG (offscreen, w edytorze) żeby ocenić wygląd bez gogli.
    /// Izoluje akt na zrzucie (chowa lab + drugi akt). Uruchamiać BEZ -nographics.
    /// </summary>
    public static class GameScreenshots
    {
        private const string OutDir = "Builds/Screens";
        private const int W = 1280, H = 720;

        /// <summary>Włącza post-processing (global volume URP) + SMAA na kamerze renderującej GIF/screen.</summary>
        private static void EnablePost(Camera c)
        {
            var d = c.GetUniversalAdditionalCameraData();
            if (d != null)
            {
                d.renderPostProcessing = true;
                d.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                d.antialiasingQuality = AntialiasingQuality.High;
            }
        }

        [MenuItem("MRCrisis/Capture Screenshots")]
        public static void Capture()
        {
            Directory.CreateDirectory(OutDir);
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/TrainingRoom.unity", OpenSceneMode.Single);

            var lab = FindAnyByName("Visuals");
            var act2 = FindAnyByName("Act2_Skid");
            var act3 = FindAnyByName("Act3_Call");
            var hiding = FindAnyByName("HidingHolder");

            var sky = new Color(0.55f, 0.72f, 0.92f);
            var dark = new Color(0.01f, 0.01f, 0.02f);

            // ===== AKT II: chowamy lab + Akt III =====
            if (lab != null) lab.gameObject.SetActive(false);
            if (act3 != null) act3.gameObject.SetActive(false);
            if (act2 != null) act2.gameObject.SetActive(true);

            // SWEEP wysokości oczu — szukamy, gdzie widać szybę+drogę+kierownicę
            Shot("eye_130", new Vector3(0f, 1.30f, 0f), Quaternion.Euler(8, 0, 0), sky, 82);
            Shot("eye_140", new Vector3(0f, 1.40f, 0f), Quaternion.Euler(6, 0, 0), sky, 82);
            Shot("eye_150", new Vector3(0f, 1.50f, 0f), Quaternion.Euler(4, 0, 0), sky, 82);
            Shot("eye_160", new Vector3(0f, 1.60f, 0f), Quaternion.Euler(2, 0, 0), sky, 82);
            Shot("03_act2_thirdperson", new Vector3(-4.5f, 3.2f, -3.5f), Quaternion.Euler(14, 42, 0), sky, 68);
            Shot("07_act2_top", new Vector3(0f, 8f, 0f), Quaternion.Euler(90, 0, 0), sky, 60);

            // ===== AKT III: aktywujemy ukrycie + ciemny ambient =====
            if (act2 != null) act2.gameObject.SetActive(false);
            if (act3 != null) act3.gameObject.SetActive(true);
            if (hiding != null) hiding.gameObject.SetActive(true);

            var savedAmbient = RenderSettings.ambientLight;
            var savedMode = RenderSettings.ambientMode;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.10f, 0.10f, 0.13f);

            Shot("05_act3_gap", new Vector3(0f, 1.55f, 0f), Quaternion.Euler(2, 0, 0), dark, 72);
            Shot("06_act3_intruder", new Vector3(0.2f, 1.6f, 3.0f), Quaternion.Euler(4, 0, 0), dark, 75);

            RenderSettings.ambientLight = savedAmbient;
            RenderSettings.ambientMode = savedMode;

            Debug.Log($"[GameScreenshots] Zapisano PNG do {OutDir} (NIE zapisuję sceny).");
        }

        private static void Shot(string name, Vector3 pos, Quaternion rot, Color bg, float fov)
        {
            var go = new GameObject("ShotCam");
            var cam = go.AddComponent<Camera>();
            cam.transform.SetPositionAndRotation(pos, rot);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = bg;
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.02f;
            cam.farClipPlane = 300f;

            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;

            File.WriteAllBytes(Path.Combine(OutDir, name + ".png"), tex.EncodeToPNG());
            cam.targetTexture = null;
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Debug.Log($"[GameScreenshots] {name}.png");
        }

        [MenuItem("MRCrisis/Jaguar Rotation Test")]
        public static void JaguarRotTest()
        {
            Directory.CreateDirectory(OutDir);
            // rot 270 = front (potwierdzone). Testujemy przesunięcie w bok (driver seat, RHD): X = -0.4 / 0 / +0.4
            float[] xs = { -0.45f, 0f, 0.45f };
            foreach (var x in xs)
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var sunGo = new GameObject("sun"); var sun = sunGo.AddComponent<Light>();
                sun.type = LightType.Directional; sunGo.transform.rotation = Quaternion.Euler(45, 30, 0); sun.intensity = 1.2f;
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.6f);

                var interior = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                if (interior.HasProperty("_BaseColor")) interior.SetColor("_BaseColor", new Color(0.14f, 0.14f, 0.16f));
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/External/CarInterior/Jaguar.fbx");
                var car = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                car.transform.position = new Vector3(x, 1.0f, 0.1f);
                car.transform.rotation = Quaternion.Euler(0, 270, 0);
                car.transform.localScale = Vector3.one * 0.5f;
                foreach (var r in car.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = interior;
                string tag = x < 0 ? "xNeg" : (x > 0 ? "xPos" : "xMid");
                Shot($"seat_{tag}", new Vector3(0f, 1.45f, 0f), Quaternion.Euler(6, 0, 0), new Color(0.55f, 0.72f, 0.92f), 82);
                Object.DestroyImmediate(car); Object.DestroyImmediate(sunGo);
            }
            Debug.Log("[JagRot] gotowe: seat_xNeg/xMid/xPos (rot 270)");
        }

        [MenuItem("MRCrisis/Capture Jaguar")]
        public static void CaptureJaguar()
        {
            Directory.CreateDirectory(OutDir);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/External/CarInterior/Jaguar.fbx");
            var car = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            car.transform.position = Vector3.zero;
            car.transform.rotation = Quaternion.identity;

            var paint = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (paint.HasProperty("_BaseColor")) paint.SetColor("_BaseColor", new Color(0.6f, 0.05f, 0.08f));
            if (paint.HasProperty("_Smoothness")) paint.SetFloat("_Smoothness", 0.75f);
            if (paint.HasProperty("_Metallic")) paint.SetFloat("_Metallic", 0.6f);
            foreach (var r in car.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = paint;

            var sunGo = new GameObject("sun"); var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional; sunGo.transform.rotation = Quaternion.Euler(45, 30, 0); sun.intensity = 1.3f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.45f, 0.5f);

            var bg = new Color(0.7f, 0.75f, 0.82f);
            var target = new Vector3(0f, 1f, 0f);
            ShotAt("jag_A_front", new Vector3(0f, 1.6f, -9f), target, bg, 45);
            ShotAt("jag_B_side", new Vector3(9f, 1.6f, 0f), target, bg, 45);
            ShotAt("jag_C_34", new Vector3(7f, 3f, -7f), target, bg, 50);
            ShotAt("jag_D_top", new Vector3(0.5f, 9f, -0.5f), target, bg, 50);

            Object.DestroyImmediate(car);
            Object.DestroyImmediate(sunGo);
            Debug.Log("[Jag] gotowe.");
        }

        private static void ShotAt(string name, Vector3 pos, Vector3 lookAt, Color bg, float fov)
        {
            var go = new GameObject("ShotCam");
            var cam = go.AddComponent<Camera>();
            cam.transform.position = pos;
            cam.transform.LookAt(lookAt);
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = bg;
            cam.fieldOfView = fov; cam.nearClipPlane = 0.02f; cam.farClipPlane = 300f;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt; cam.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(Path.Combine(OutDir, name + ".png"), tex.EncodeToPNG());
            cam.targetTexture = null;
            Object.DestroyImmediate(go); Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
            Debug.Log($"[Jag] {name}.png");
        }

        [MenuItem("MRCrisis/Capture Drive GIF Frames")]
        public static void CaptureDriveGif()
        {
            var dir = "Builds/gif";
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/TrainingRoom.unity", OpenSceneMode.Single);

            var lab = FindAnyByName("Visuals"); if (lab != null) lab.gameObject.SetActive(false);
            var act3 = FindAnyByName("Act3_Call"); if (act3 != null) act3.gameObject.SetActive(false);
            var act2 = FindAnyByName("Act2_Skid"); if (act2 != null) act2.gameObject.SetActive(true);
            var forest = FindAnyByName("ForestWorld");

            // Trener poślizgu — podłącz teksty, by zademonstrować podpowiedzi w GIF-ie
            TMPro.TMP_Text coachBig = null, coachSub = null;
            var coachGo = FindAnyByName("SkidCoach");
            if (coachGo != null)
                foreach (var t in coachGo.GetComponentsInChildren<TMPro.TMP_Text>(true))
                { if (t.fontSize >= 50f) coachBig = t; else coachSub = t; }

            // Atmosfera jak w grze: niebo gradientowe + mgła dystansu (droga wtapia się w głąb)
            var sky = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Act2/M_DriveSky.mat");
            var bg = new Color(0.55f, 0.72f, 0.92f);
            if (sky != null) { RenderSettings.skybox = sky; RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox; }
            else { RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.55f, 0.57f, 0.62f); }
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = bg; RenderSettings.fogStartDistance = 28f; RenderSettings.fogEndDistance = 150f;
            DynamicGI.UpdateEnvironment();

            const int W = 768, H = 432, N = 54;
            var go = new GameObject("GifCam");
            var cam = go.AddComponent<Camera>();
            // Widok kierowcy hatchbacka (oczy 1,32 m — podniesione): zamknięta kabina (DACH, słupki, boki), droga przez szybę
            cam.transform.position = new Vector3(0f, 1.32f, 0f);
            cam.transform.rotation = Quaternion.Euler(3f, 0, 0);
            cam.clearFlags = sky != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            cam.backgroundColor = bg;
            cam.fieldOfView = 80; cam.nearClipPlane = 0.02f; cam.farClipPlane = 320f;
            EnablePost(cam);
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 2 };
            cam.targetTexture = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);

            // Zwróć panel trenera w stronę kamery (w edytorze FaceCamera nie działa)
            if (coachGo != null) coachGo.rotation = Quaternion.LookRotation(coachGo.position - cam.transform.position, Vector3.up);

            var segs = new List<Transform>();
            if (forest != null) foreach (Transform c in forest) if (c.name.StartsWith("Seg")) segs.Add(c);
            float speed = 13f, dt = 0.13f, segLen = 30f, recycle = 35f;
            float total = Mathf.Max(1, segs.Count) * segLen;

            for (int i = 0; i < N; i++)
            {
                foreach (var s in segs)
                {
                    var p = s.localPosition; p.z -= speed * dt;
                    if (p.z <= -recycle) p.z += total;
                    s.localPosition = p;
                }
                // Poślizg (klatki 20..48): świat ucieka w bok (yaw) + kamera się przechyla (roll) = mocniejszy efekt
                float yaw = 0f, roll = 0f;
                if (i >= 20 && i <= 48) { float s = Mathf.Sin((i - 20) / 28f * Mathf.PI); yaw = s * 42f; roll = s * 13f; }
                if (forest != null) forest.localRotation = Quaternion.Euler(0, yaw, 0);
                cam.transform.rotation = Quaternion.Euler(5f, 0f, roll);

                // Podpowiedzi trenera (demonstracja przepływu: złap → skręć w prawo → odzyskano)
                if (coachBig != null)
                {
                    Color cw = new Color(1f, 0.78f, 0.2f), cr = new Color(1f, 0.4f, 0.32f), cg = new Color(0.3f, 0.95f, 0.4f);
                    string b = "", s = ""; Color col = cw;
                    if (i >= 12 && i < 20) { b = "ZŁAP KIEROWNICĘ"; s = "Sięgnij dłońmi do kierownicy"; col = cw; }
                    else if (i >= 20 && i <= 40) { b = "! SKRĘĆ W PRAWO »"; s = "Kontrasteruj — kręć w stronę poślizgu"; col = cr; }
                    else if (i > 40 && i <= 48) { b = "OK — TRZYMAJ"; s = "Poślizg maleje..."; col = cg; }
                    else if (i > 48) { b = "KONTROLA ODZYSKANA"; s = "Wracaj do spokojnej jazdy"; col = cg; }
                    coachBig.text = b; coachBig.color = col; coachBig.ForceMeshUpdate();
                    if (coachSub != null) { coachSub.text = s; coachSub.color = col; coachSub.ForceMeshUpdate(); }
                }

                cam.Render();
                var prev = RenderTexture.active; RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply();
                RenderTexture.active = prev;
                File.WriteAllBytes($"{dir}/f{i:D3}.png", tex.EncodeToPNG());
            }

            cam.targetTexture = null;
            Object.DestroyImmediate(go); Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
            Debug.Log($"[Gif] {N} klatek zapisanych do {dir} (segmenty: {segs.Count})");
        }

        /// <summary>Sweep ustawień kamery kierowcy (wysokość oczu + pochylenie + cofnięcie),
        /// żeby znaleźć kadr, w którym DROGA jest dobrze widoczna przez szybę nad deską.</summary>
        [MenuItem("MRCrisis/Capture Road Framing")]
        public static void CaptureRoadFraming()
        {
            var dir = OutDir;
            Directory.CreateDirectory(dir);
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/TrainingRoom.unity", OpenSceneMode.Single);
            var lab = FindAnyByName("Visuals"); if (lab != null) lab.gameObject.SetActive(false);
            var act3 = FindAnyByName("Act3_Call"); if (act3 != null) act3.gameObject.SetActive(false);
            var act2 = FindAnyByName("Act2_Skid"); if (act2 != null) act2.gameObject.SetActive(true);
            var forest = FindAnyByName("ForestWorld");

            // Przewiń las tak, by przed graczem był odcinek z linią (nie pień tuż przy masce)
            if (forest != null) foreach (Transform c in forest) if (c.name.StartsWith("Seg")) { var p = c.localPosition; p.z -= 6f; c.localPosition = p; }

            var sky = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Act2/M_DriveSky.mat");
            var bg = new Color(0.55f, 0.72f, 0.92f);
            if (sky != null) { RenderSettings.skybox = sky; RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox; }
            else { RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.55f, 0.57f, 0.62f); }
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogColor = bg;
            RenderSettings.fogStartDistance = 28f; RenderSettings.fogEndDistance = 150f; DynamicGI.UpdateEnvironment();

            const int W = 768, H = 432;
            var go = new GameObject("FrameCam");
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = sky != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            cam.backgroundColor = bg; cam.fieldOfView = 80; cam.nearClipPlane = 0.02f; cam.farClipPlane = 320f;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);

            // Wąskie okno: oczy tuż nad deską (droga w szybie) ale pod górną krawędzią szyby (głowa w aucie)
            cam.transform.rotation = Quaternion.Euler(5f, 0f, 0f);
            float[] eyes = { 1.02f, 1.06f, 1.10f, 1.14f };
            for (int i = 0; i < eyes.Length; i++)
            {
                cam.transform.position = new Vector3(0f, eyes[i], 0f);
                cam.Render();
                var prev = RenderTexture.active; RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply();
                RenderTexture.active = prev;
                string fn = $"{dir}/mclv2_{i}_{eyes[i]:0.00}.png";
                File.WriteAllBytes(fn, tex.EncodeToPNG());
                Debug.Log($"[Framing] {fn}");
            }
            cam.targetTexture = null;
            Object.DestroyImmediate(go); Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
            Debug.Log("[Framing] DONE");
        }

        /// <summary>Inspekcja modelu McLarena: orientacja, wymiary, czy ma dach, gdzie jest kierownica.</summary>
        [MenuItem("MRCrisis/Inspect McLaren")]
        public static void InspectMcLaren()
        {
            var dir = OutDir; Directory.CreateDirectory(dir);
            string glb = "Assets/_Project/External/McLaren/McLaren.glb";
            string fbx = "Assets/_Project/External/McLaren/McLaren.fbx";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(glb) ?? AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            Debug.Log($"[McL] source = {(AssetDatabase.LoadAssetAtPath<GameObject>(glb) != null ? "GLB(glTFast)" : "FBX")}");
            if (prefab == null) { Debug.Log("[McL] NOT IMPORTED YET (glb+fbx null)"); return; }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.6f, 0.6f, 0.65f);
            RenderSettings.fog = false;
            var sunGo = new GameObject("Sun"); var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.2f; sunGo.transform.rotation = Quaternion.Euler(50, 30, 0);

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.position = Vector3.zero; go.transform.rotation = Quaternion.identity;

            // łączny bounding box + środek
            Bounds b = default; bool first = true; int tris = 0;
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
            {
                if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
                var mf = r.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null) tris += mf.sharedMesh.triangles.Length / 3;
            }
            Debug.Log($"[McL] center={b.center} size={b.size} tris={tris} renderers={go.GetComponentsInChildren<MeshRenderer>().Length}");

            const int W = 768, H = 432;
            var camGo = new GameObject("Cam"); var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.5f, 0.55f, 0.62f);
            cam.fieldOfView = 45; cam.nearClipPlane = 0.01f; cam.farClipPlane = 200f;
            var rt = new RenderTexture(W, H, 24) { antiAliasing = 4 }; cam.targetTexture = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            float rad = b.size.magnitude * 0.75f + 1f;

            // (label, kierunek kamery od środka)
            var views = new (string, Vector3)[] {
                ("posX", new Vector3(1, 0.25f, 0)),
                ("negX", new Vector3(-1, 0.25f, 0)),
                ("posZ", new Vector3(0, 0.25f, 1)),
                ("negZ", new Vector3(0, 0.25f, -1)),
                ("top",  new Vector3(0.01f, 1, 0.01f)),
                ("iso",  new Vector3(1, 0.6f, 1)),
            };
            foreach (var (label, vdir) in views)
            {
                cam.transform.position = b.center + vdir.normalized * rad;
                cam.transform.LookAt(b.center);
                cam.Render();
                var prev = RenderTexture.active; RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply(); RenderTexture.active = prev;
                File.WriteAllBytes($"{dir}/mcl_{label}.png", tex.EncodeToPNG());
            }
            cam.targetTexture = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
            Debug.Log("[McL] DONE");
        }

        /// <summary>Sonduje wnętrze McLarena z fotela kierowcy w obu kierunkach + kilku pozycjach,
        /// żeby ustalić PRZÓD (nos), stronę fotela i wysokość oczu.</summary>
        [MenuItem("MRCrisis/Probe McLaren Interior")]
        public static void ProbeMcLarenInterior()
        {
            var dir = OutDir; Directory.CreateDirectory(dir);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/External/McLaren/McLaren.glb")
                      ?? AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/External/McLaren/McLaren.fbx");
            if (prefab == null) { Debug.Log("[McLp] model missing"); return; }

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.62f, 0.62f, 0.66f); RenderSettings.fog = false;
            var sunGo = new GameObject("Sun"); var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.2f; sunGo.transform.rotation = Quaternion.Euler(50, 30, 0);

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            // recenter: środek brył do origin
            Bounds b = default; bool first = true;
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>()) { if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); }
            go.transform.position = -b.center;
            // przelicz nowe bounds (origin-centered)
            Debug.Log($"[McLp] recentered; halfX={b.extents.x:0.00} halfY={b.extents.y:0.00} halfZ={b.extents.z:0.00}");

            const int W = 768, H = 432;
            var camGo = new GameObject("Cam"); var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.55f, 0.72f, 0.92f);
            cam.fieldOfView = 80; cam.nearClipPlane = 0.01f; cam.farClipPlane = 100f;
            var rt = new RenderTexture(W, H, 24) { antiAliasing = 4 }; cam.targetTexture = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);

            // (label, camPos, lookTarget) — szukamy widoku z deską+szybą przed kierowcą
            float ey = b.extents.y * 0.45f; // oczy ~ górna część kabiny
            var probes = new (string, Vector3, Vector3)[] {
                ("pX_c", new Vector3(0f, ey, 0.35f),  new Vector3(3f, ey*0.6f, 0.35f)),
                ("nX_c", new Vector3(0f, ey, 0.35f),  new Vector3(-3f, ey*0.6f, 0.35f)),
                ("pX_back", new Vector3(-0.6f, ey, 0.35f), new Vector3(3f, ey*0.6f, 0.35f)),
                ("nX_back", new Vector3(0.6f, ey, 0.35f),  new Vector3(-3f, ey*0.6f, 0.35f)),
                ("pX_zneg", new Vector3(0f, ey, -0.35f), new Vector3(3f, ey*0.6f, -0.35f)),
                ("nX_zneg", new Vector3(0f, ey, -0.35f), new Vector3(-3f, ey*0.6f, -0.35f)),
            };
            foreach (var (label, pos, look) in probes)
            {
                cam.transform.position = pos; cam.transform.LookAt(look);
                cam.Render();
                var prev = RenderTexture.active; RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply(); RenderTexture.active = prev;
                File.WriteAllBytes($"{dir}/mclp_{label}.png", tex.EncodeToPNG());
            }
            cam.targetTexture = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
            Debug.Log("[McLp] DONE");
        }

        /// <summary>Inspekcja hatchbacka (orientacja, wymiary, dach/wnętrze, materiały) — 6 ujęć + wnętrze.</summary>
        [MenuItem("MRCrisis/Inspect HatchBack")]
        public static void InspectHatchBack()
        {
            var dir = OutDir; Directory.CreateDirectory(dir);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/External/HatchBack/HatchBack.fbx");
            if (prefab == null) { Debug.Log("[HB] FBX not imported"); return; }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.6f, 0.6f, 0.65f); RenderSettings.fog = false;
            var sunGo = new GameObject("Sun"); var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.2f; sunGo.transform.rotation = Quaternion.Euler(50, 30, 0);

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.position = Vector3.zero; go.transform.rotation = Quaternion.identity;
            Bounds b = default; bool first = true; int tris = 0;
            var mats = new HashSet<string>();
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
            {
                if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
                var mf = r.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null) tris += mf.sharedMesh.triangles.Length / 3;
                foreach (var m in r.sharedMaterials) if (m != null) mats.Add(m.name);
            }
            Debug.Log($"[HB] center={b.center} size={b.size} tris={tris} renderers={go.GetComponentsInChildren<MeshRenderer>().Length}");
            Debug.Log($"[HB] mats=[{string.Join(", ", mats)}]");

            const int W = 768, H = 432;
            var camGo = new GameObject("Cam"); var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.5f, 0.55f, 0.62f);
            cam.fieldOfView = 45; cam.nearClipPlane = 0.01f; cam.farClipPlane = 200f;
            var rt = new RenderTexture(W, H, 24) { antiAliasing = 4 }; cam.targetTexture = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            float rad = b.size.magnitude * 0.75f + 1f;
            var views = new (string, Vector3)[] {
                ("posX", new Vector3(1, 0.25f, 0)), ("negX", new Vector3(-1, 0.25f, 0)),
                ("posZ", new Vector3(0, 0.25f, 1)), ("negZ", new Vector3(0, 0.25f, -1)),
                ("top", new Vector3(0.01f, 1, 0.01f)), ("iso", new Vector3(1, 0.6f, 1)),
            };
            foreach (var (label, vdir) in views)
            {
                cam.transform.position = b.center + vdir.normalized * rad; cam.transform.LookAt(b.center);
                cam.Render();
                var prev = RenderTexture.active; RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply(); RenderTexture.active = prev;
                File.WriteAllBytes($"{dir}/hb_{label}.png", tex.EncodeToPNG());
            }
            cam.targetTexture = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
            Debug.Log("[HB] DONE");
        }

        /// <summary>Wypisuje części hatchbacka (nazwa, środek, rozmiar, materiał) — do identyfikacji otwartych drzwi/maski.</summary>
        [MenuItem("MRCrisis/Dump HatchBack Parts")]
        public static void DumpHatchBackParts()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/External/HatchBack/HatchBack.fbx");
            if (prefab == null) { Debug.Log("[HBP] missing"); return; }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.position = Vector3.zero; go.transform.rotation = Quaternion.identity; go.transform.localScale = Vector3.one;
            // łączny środek (do odniesienia)
            Bounds tot = default; bool f0 = true;
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>()) { if (f0) { tot = r.bounds; f0 = false; } else tot.Encapsulate(r.bounds); }
            Debug.Log($"[HBP] TOTAL center={tot.center} size={tot.size}");
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
            {
                var b = r.bounds;
                string m0 = r.sharedMaterials.Length > 0 && r.sharedMaterials[0] != null ? r.sharedMaterials[0].name : "?";
                Debug.Log($"[HBP] {r.name} | c=({b.center.x:0.00},{b.center.y:0.00},{b.center.z:0.00}) sz=({b.size.x:0.00},{b.size.y:0.00},{b.size.z:0.00}) | {m0}");
            }
            Debug.Log("[HBP] DONE");
        }

        /// <summary>Sonduje wnętrze hatchbacka z fotela kierowcy (lewy, x=-0.38) patrząc do przodu (+Z).
        /// Renderuje też wariant z ukrytymi częściami przednimi (chassis/dviga/bonnet), by sprawdzić czy zasłaniają.</summary>
        [MenuItem("MRCrisis/Probe HatchBack Interior")]
        public static void ProbeHatchBackInterior()
        {
            var dir = OutDir; Directory.CreateDirectory(dir);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/External/HatchBack/HatchBack.fbx");
            if (prefab == null) { Debug.Log("[HBI] missing"); return; }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.62f, 0.62f, 0.66f); RenderSettings.fog = false;
            var sunGo = new GameObject("Sun"); var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.2f; sunGo.transform.rotation = Quaternion.Euler(50, 30, 0);

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.position = Vector3.zero; go.transform.rotation = Quaternion.identity;

            const int W = 768, H = 432;
            var camGo = new GameObject("Cam"); var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.55f, 0.72f, 0.92f);
            cam.fieldOfView = 80; cam.nearClipPlane = 0.01f; cam.farClipPlane = 100f;
            var rt = new RenderTexture(W, H, 24) { antiAliasing = 4 }; cam.targetTexture = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);

            void Shot(string fn, Vector3 pos, Vector3 look)
            {
                cam.transform.position = pos; cam.transform.LookAt(look);
                cam.Render();
                var prev = RenderTexture.active; RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply(); RenderTexture.active = prev;
                File.WriteAllBytes($"{dir}/{fn}.png", tex.EncodeToPNG());
            }

            // Pełny model, fotel kierowcy (lewy) patrząc do przodu (+Z) na różnych wysokościach
            Shot("hbi_drv", new Vector3(-0.38f, 1.10f, 0.0f), new Vector3(-0.38f, 1.0f, 3f));
            Shot("hbi_drv_hi", new Vector3(-0.38f, 1.22f, 0.0f), new Vector3(-0.38f, 1.12f, 3f));
            Shot("hbi_drv_lo", new Vector3(-0.38f, 1.00f, 0.0f), new Vector3(-0.38f, 0.92f, 3f));

            // Ukryj przednie/zbędne części i sprawdź czysty widok do przodu
            string[] hideNames = { "chassis", "dviga", "bonnet_ok", "bump_front_ok", "per_fari", "Plane", "stekla_far", "fari_zad", "extra1" };
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
                if (System.Array.IndexOf(hideNames, r.name) >= 0) r.enabled = false;
            Shot("hbi_drv_clean", new Vector3(-0.38f, 1.10f, 0.0f), new Vector3(-0.38f, 1.0f, 3f));
            Shot("hbi_drv_clean_hi", new Vector3(-0.38f, 1.20f, 0.0f), new Vector3(-0.38f, 1.1f, 3f));

            cam.targetTexture = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
            Debug.Log("[HBI] DONE");
        }

        /// <summary>Inspekcja assetów Aktu III (szafa, telefon, złodziej): tris, bounds, animacje, render iso.</summary>
        [MenuItem("MRCrisis/Inspect Act3 Assets")]
        public static void InspectAct3Assets()
        {
            var dir = OutDir; Directory.CreateDirectory(dir);
            string[] names = { "Wardrobe", "Telephone", "Thief" };
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.6f, 0.6f, 0.65f); RenderSettings.fog = false;
            var sunGo = new GameObject("Sun"); var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.2f; sunGo.transform.rotation = Quaternion.Euler(50, 30, 0);

            const int W = 640, H = 640;
            var camGo = new GameObject("Cam"); var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.5f, 0.55f, 0.62f);
            cam.fieldOfView = 40; cam.nearClipPlane = 0.001f; cam.farClipPlane = 500f;
            var rt = new RenderTexture(W, H, 24) { antiAliasing = 4 }; cam.targetTexture = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);

            foreach (var nm in names)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Project/External/Act3/{nm}.glb");
                if (prefab == null) { Debug.Log($"[A3] {nm} MISSING"); continue; }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                go.transform.position = Vector3.zero; go.transform.rotation = Quaternion.identity;
                Bounds b = default; bool first = true; int tris = 0;
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                {
                    if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null) tris += mf.sharedMesh.triangles.Length / 3;
                    var smr = r as SkinnedMeshRenderer;
                    if (smr != null && smr.sharedMesh != null) tris += smr.sharedMesh.triangles.Length / 3;
                }
                int skinned = go.GetComponentsInChildren<SkinnedMeshRenderer>().Length;
                var anim = go.GetComponentInChildren<Animation>();
                var animator = go.GetComponentInChildren<Animator>();
                Debug.Log($"[A3] {nm}: tris={tris} size={b.size} center={b.center} skinned={skinned} animator={(animator != null)} legacyAnim={(anim != null)}");

                float rad = b.size.magnitude * 0.7f + 0.5f;
                cam.transform.position = b.center + new Vector3(1, 0.5f, 1).normalized * rad;
                cam.transform.LookAt(b.center);
                cam.Render();
                var prev = RenderTexture.active; RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply(); RenderTexture.active = prev;
                File.WriteAllBytes($"{dir}/a3_{nm}.png", tex.EncodeToPNG());
                Object.DestroyImmediate(go);
            }
            cam.targetTexture = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
            Debug.Log("[A3] DONE");
        }

        // ---- Akt III: materiały sypialni z wypalonych map VRay (po nazwie mesha) ----
        private static void ApplyBedroomMaterials(GameObject go, float dim)
        {
            const string texDir = "Assets/_Project/External/Bedroom/textures";
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { texDir });
            var texes = new System.Collections.Generic.List<(string name, Texture2D tex)>();
            foreach (var g in guids)
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                var t = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
                if (t != null) texes.Add((System.IO.Path.GetFileNameWithoutExtension(p).ToLower(), t));
            }
            // Mapy są WYPALONE (CompleteMap = z oświetleniem) → Unlit pokazuje je w pełni, bez ponownego cieniowania.
            var sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture");
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                string mn = r.name.ToLower().Replace("_", "");
                if (mn.Contains("wall") || mn.Contains("scerting")) mn = mn.Contains("scerting") ? "scerting" : "room"; // ściany = room.jpg
                Texture2D best = null; int bestScore = -1;
                foreach (var (tn, tex) in texes)
                {
                    string tnc = tn.Replace("vraycompletemap", "").Replace("_", "").Replace("-", "");
                    int score = 0;
                    if (tnc == mn) score = 100;
                    else if (mn.StartsWith(tnc) || tnc.StartsWith(mn)) score = 60;
                    else if (mn.Contains(tnc) || tnc.Contains(mn)) score = 30;
                    if (score > bestScore) { bestScore = score; best = tex; }
                }
                var m = new Material(sh);
                if (best != null && bestScore >= 30 && m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", best);
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(dim, dim, dim, 1f));
                r.sharedMaterial = m;
            }
        }

        // Normalizuje model GLB: skala do docelowej wysokości + spód na y=0, środek w (x,z)=0.
        private static GameObject PlaceNorm(GameObject prefab, float targetH, Vector3 pos, float yaw)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.position = Vector3.zero; go.transform.rotation = Quaternion.identity; go.transform.localScale = Vector3.one;
            Bounds b = default; bool first = true;
            foreach (var r in go.GetComponentsInChildren<Renderer>()) { if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); }
            float h = Mathf.Max(0.01f, b.size.y);
            float s = targetH / h;
            go.transform.localScale = Vector3.one * s;
            // przelicz bounds po skalowaniu
            first = true; Bounds b2 = default;
            foreach (var r in go.GetComponentsInChildren<Renderer>()) { if (first) { b2 = r.bounds; first = false; } else b2.Encapsulate(r.bounds); }
            Vector3 off = new Vector3(b2.center.x, b2.min.y, b2.center.z);
            go.transform.position = pos - off;
            go.transform.rotation = Quaternion.Euler(0, yaw, 0);
            return go;
        }

        /// <summary>Podgląd układu Aktu III: sypialnia + szafa + telefon + intruz. Renderuje rzut + widok ze szpary.</summary>
        [MenuItem("MRCrisis/Preview Act3 Layout")]
        public static void PreviewAct3Layout()
        {
            var dir = OutDir; Directory.CreateDirectory(dir);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.18f, 0.18f, 0.24f); RenderSettings.fog = false;

            var bedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/External/Bedroom/Bedroom.fbx");
            var wardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/External/Act3/Wardrobe.glb");
            var phonePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/External/Act3/Telephone.glb");
            var thiefPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/External/Act3/Thief.glb");
            Debug.Log($"[A3L] bed={bedPrefab!=null} ward={wardPrefab!=null} phone={phonePrefab!=null} thief={thiefPrefab!=null}");

            Bounds room = new Bounds(Vector3.zero, Vector3.one * 4f);
            if (bedPrefab != null)
            {
                var bed = (GameObject)PrefabUtility.InstantiatePrefab(bedPrefab);
                bed.transform.position = Vector3.zero; bed.transform.rotation = Quaternion.identity;
                ApplyBedroomMaterials(bed, 0.75f);
                bool first = true;
                foreach (var r in bed.GetComponentsInChildren<MeshRenderer>()) { if (first) { room = r.bounds; first = false; } else room.Encapsulate(r.bounds); }
                // podłoga na y=0
                bed.transform.position = new Vector3(-room.center.x, -room.min.y, -room.center.z);
                Debug.Log($"[A3L] room size={room.size} center={room.center}");
            }

            // dim ceiling light + thief flashlight
            var lg = new GameObject("Ceil"); var pl = lg.AddComponent<Light>(); pl.type = LightType.Point; pl.intensity = 0.5f; pl.range = 8f; pl.color = new Color(0.7f,0.7f,0.85f); lg.transform.position = new Vector3(0, 2.6f, 0);

            if (wardPrefab != null) PlaceNorm(wardPrefab, 2.0f, new Vector3(-1.8f, 0f, 1.6f), 0f).name = "WARD";
            if (phonePrefab != null) PlaceNorm(phonePrefab, 0.22f, new Vector3(1.4f, 0.55f, -1.2f), -30f).name = "PHONE";
            GameObject thiefGo = null;
            if (thiefPrefab != null) { thiefGo = PlaceNorm(thiefPrefab, 1.8f, new Vector3(0.4f, 0f, -0.5f), 180f); thiefGo.name = "THIEF"; }

            const int W = 900, H = 560;
            var camGo = new GameObject("Cam"); var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.02f,0.02f,0.03f);
            cam.fieldOfView = 70; cam.nearClipPlane = 0.02f; cam.farClipPlane = 100f;
            var rt = new RenderTexture(W, H, 24){antiAliasing=4}; cam.targetTexture = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            void Shot(string fn, Vector3 p, Vector3 look){ cam.transform.position=p; cam.transform.LookAt(look); cam.Render(); var pr=RenderTexture.active; RenderTexture.active=rt; tex.ReadPixels(new Rect(0,0,W,H),0,0); tex.Apply(); RenderTexture.active=pr; File.WriteAllBytes($"{dir}/{fn}.png", tex.EncodeToPNG()); }

            // 1) Rzut pokoju z rogu
            Shot("a3l_overview", new Vector3(room.max.x*0.9f, 2.4f, room.max.z*0.9f), new Vector3(0,1f,0));
            // 2) Widok z miejsca przy telefonie
            Shot("a3l_phone", new Vector3(1.4f, 1.5f, -0.4f), new Vector3(1.4f, 0.6f, -1.2f));
            // 3) Widok "ze szpary szafy" na intruza
            Shot("a3l_gap", new Vector3(-1.8f, 1.45f, 1.0f), new Vector3(0.4f, 1.2f, -0.5f));

            cam.targetTexture = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
            Debug.Log("[A3L] DONE");
        }

        /// <summary>Render sceny ukrycia Aktu III: widok ze szpary szafy na sypialnię + intruza, oraz rzut z boku.</summary>
        [MenuItem("MRCrisis/Capture Act3 Hiding")]
        public static void CaptureAct3Hiding()
        {
            var dir = OutDir; Directory.CreateDirectory(dir);
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/TrainingRoom.unity", OpenSceneMode.Single);
            var lab = FindAnyByName("Visuals"); if (lab != null) lab.gameObject.SetActive(false);
            var act2 = FindAnyByName("Act2_Skid"); if (act2 != null) act2.gameObject.SetActive(false);
            var act3 = FindAnyByName("Act3_Call"); if (act3 != null) act3.gameObject.SetActive(true);
            var holder = FindAnyByName("HidingHolder");
            if (holder != null) holder.gameObject.SetActive(true);
            // pokaż intruza
            var thiefVisual = FindAnyByName("Visual");
            if (thiefVisual != null) thiefVisual.gameObject.SetActive(true);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.28f, 0.28f, 0.32f); RenderSettings.fog = false;  // podbite na podgląd (w grze ciemno + latarka)
            RenderSettings.skybox = null;
            var fillGo = new GameObject("FillLight"); var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional; fill.intensity = 1.2f; fillGo.transform.rotation = Quaternion.Euler(45f, 25f, 0f);

            Vector3 hp = holder != null ? holder.position : Vector3.zero;
            float yaw = holder != null ? holder.eulerAngles.y : 0f;
            Quaternion hr = Quaternion.Euler(0, yaw, 0);

            const int W = 768, H = 432;
            var go = new GameObject("A3Cam"); var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.01f, 0.01f, 0.015f);
            cam.fieldOfView = 75; cam.nearClipPlane = 0.02f; cam.farClipPlane = 60f;
            var rt = new RenderTexture(W, H, 24) { antiAliasing = 4 }; cam.targetTexture = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            void Shot(string fn, Vector3 lp, Vector3 lookLocal)
            {
                cam.transform.position = hp + hr * lp;
                cam.transform.rotation = Quaternion.LookRotation(hr * (lookLocal - lp));
                cam.Render();
                var pr = RenderTexture.active; RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply(); RenderTexture.active = pr;
                File.WriteAllBytes($"{dir}/{fn}.png", tex.EncodeToPNG());
            }
            // 1) Widok gracza ze szpary szafy (oczy 1.4, patrzy +Z na pokój/intruza)
            Shot("a3_hide_gap", new Vector3(0f, 1.4f, 0f), new Vector3(0f, 1.2f, 5f));
            // 2) Szerzej zza gracza (widać szafę + sypialnię + Dana)
            Shot("a3_hide_side", new Vector3(2.2f, 2.3f, -1.2f), new Vector3(0.3f, 0.6f, 4f));

            cam.targetTexture = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
            Debug.Log("[A3H] DONE");
        }

        /// <summary>GIF mechaniki Aktu III: widok z szafy, telefon w ręce, napisy 112, chodzący złodziej z latarką, pasek ciszy.</summary>
        [MenuItem("MRCrisis/Capture Act3 Gif")]
        public static void CaptureAct3Gif()
        {
            var dir = "Builds/gif3";
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/TrainingRoom.unity", OpenSceneMode.Single);
            var lab = FindAnyByName("Visuals"); if (lab != null) lab.gameObject.SetActive(false);
            var act2 = FindAnyByName("Act2_Skid"); if (act2 != null) act2.gameObject.SetActive(false);
            var act3 = FindAnyByName("Act3_Call"); if (act3 != null) act3.gameObject.SetActive(true);
            var holder = FindAnyByName("HidingHolder"); if (holder != null) holder.gameObject.SetActive(true);
            // Spod łóżka kamera patrzy nisko — utnij dalekie tło/niebo pokoju (różowy skybox-dome), zostaw wnętrze
            var leafia = FindAnyByName("LeafiaRoom");
            if (leafia != null)
                foreach (var r in leafia.GetComponentsInChildren<MeshRenderer>(true))
                {
                    float mx = Mathf.Max(r.bounds.size.x, r.bounds.size.y, r.bounds.size.z);
                    string nm = r.gameObject.name.ToLower();
                    if (mx > 9f || nm.Contains("sky") || nm.Contains("land") || nm.Contains("terrain") || nm.Contains("plane") || nm.Contains("env"))
                        r.enabled = false;
                }
            var thiefGo = FindAnyByName("Thief");
            // pobierz poddrzewo złodzieja z samego Thief (nie po globalnej nazwie "Visual", bo takich może być wiele)
            Transform thiefVisual = thiefGo != null ? thiefGo.Find("Visual") : null;
            // aktywuj przodków AŻ do roota sceny...
            if (thiefGo != null) for (var p = thiefGo; p != null; p = p.parent) p.gameObject.SetActive(true);
            // ...oraz CAŁE poddrzewo złodzieja (mesh SkinnedMeshRenderer bywa na nieaktywnym węźle -> inaczej intruz się nie renderuje)
            if (thiefGo != null) foreach (var tr in thiefGo.GetComponentsInChildren<Transform>(true)) tr.gameObject.SetActive(true);
            Transform intruderModel = thiefVisual != null ? thiefVisual.Find("IntruderModel") : FindAnyByName("IntruderModel");
            var flashT = thiefVisual != null ? thiefVisual.Find("Flashlight") : FindAnyByName("Flashlight");
            Light flash = flashT != null ? flashT.GetComponent<Light>() : null;
            if (flash != null) { flash.enabled = true; flash.intensity = 6f; flash.range = 10f; }
            // na GIF: intruz = wyraźna CIEMNA sylwetka (czytelna na tle rozjaśnionego pokoju)
            int intrRenderers = 0; string rinfo = "";
            if (intruderModel != null)
            {
                // NIE nadpisuj materiałów wspólnym overridem — Tower Tenant ma własne per-submesh UV-mapy
                foreach (var r in intruderModel.GetComponentsInChildren<Renderer>(true))
                {
                    r.enabled = true; intrRenderers++;
                    if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
                    rinfo += $"[{r.gameObject.name}]";
                }
            }
            Debug.Log($"[A3Gif] thiefGo={(thiefGo!=null)} visual={(thiefVisual!=null)} intruderModel={(intruderModel!=null)} renderers={intrRenderers} flash={(flash!=null)} {rinfo}");
            // na GIF: schowaj doulapę (kadrujemy „szparę" ciemnymi listwami), żeby był czysty widok na pokój i drzwi
            if (holder != null)
                foreach (var r in holder.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var mn = r.sharedMaterial != null ? r.sharedMaterial.name : "";
                    if (mn.StartsWith("M_ClosetGlass")) r.enabled = false;
                }
            var doorPivot = FindAnyByName("RoomDoorPivot");
            Quaternion doorClosed = doorPivot != null ? doorPivot.localRotation : Quaternion.identity;
            var walkClip = AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/External/Act3/Intruder/IntruderWalk.fbx") is var arr ? System.Array.Find(arr, a => a is AnimationClip c && !c.name.StartsWith("__preview__")) as AnimationClip : null;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.42f, 0.42f); RenderSettings.fog = false; RenderSettings.skybox = null;  // NEUTRALNE (nie niebieskie) ambient
            // światło z PRZODU (świeci ku +Z) — oświetla front drzwi i intruza, żeby nie były ciemnoniebieskie
            var gifFillGo = new GameObject("GifFill"); var gifFill = gifFillGo.AddComponent<Light>();
            gifFill.type = LightType.Directional; gifFill.intensity = 1.35f; gifFill.color = new Color(1f, 0.97f, 0.92f);
            gifFillGo.transform.rotation = Quaternion.Euler(28f, 8f, 0f);
            var gifFill2Go = new GameObject("GifFill2"); var gifFill2 = gifFill2Go.AddComponent<Light>();  // miękkie dopełnienie z góry
            gifFill2.type = LightType.Directional; gifFill2.intensity = 0.5f; gifFill2Go.transform.rotation = Quaternion.Euler(70f, -40f, 0f);

            Vector3 hp = holder != null ? holder.position : Vector3.zero;
            float yaw = holder != null ? holder.eulerAngles.y : 0f;
            Quaternion hr = Quaternion.Euler(0, yaw, 0);

            const int W = 768, H = 432, N = 72;
            var go = new GameObject("A3GifCam"); var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.02f, 0.02f, 0.03f);
            cam.fieldOfView = 52; cam.nearClipPlane = 0.02f; cam.farClipPlane = 30f;
            EnablePost(cam);
            // Widok 3/4 z góry na sypialnię: łóżko (lewy przód), drzwi (w głębi), intruz wchodzi i szuka. Patrzymy W DÓŁ → bez nieba.
            // Kamera WEWNĄTRZ pokoju (z=0.2, koło gracza), na wysokości oczu, patrzy +Z na DRZWI (0,1.4,3.1).
            // Dzięki temu żadna ściana nie zasłania widoku (tylna ściana jest ZA kamerą).
            Vector3 camLocal = new Vector3(-0.8f, 1.62f, 0.2f);
            Vector3 lookLocal = new Vector3(-0.25f, 1.05f, 3.0f);
            cam.transform.position = hp + hr * camLocal;
            cam.transform.rotation = Quaternion.LookRotation(hr * (lookLocal - camLocal), Vector3.up);
            var rt = new RenderTexture(W, H, 24) { antiAliasing = 4 }; cam.targetTexture = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);

            // telefon w ręce (retro słuchawka przy kamerze: uchwyt + górna słuchawka)
            var phone = new GameObject("HeldPhone");
            var pm = SolidURPLocal("M_HeldPhone", new Color(0.24f, 0.22f, 0.22f));
            var phHandle = GameObject.CreatePrimitive(PrimitiveType.Cube); phHandle.name = "Handle";
            Object.DestroyImmediate(phHandle.GetComponent<Collider>());
            phHandle.transform.SetParent(phone.transform, false);
            phHandle.transform.localScale = new Vector3(0.032f, 0.155f, 0.03f);
            phHandle.transform.localRotation = Quaternion.Euler(0, 0, 18f);
            phHandle.GetComponent<Renderer>().sharedMaterial = pm;
            var phEar = GameObject.CreatePrimitive(PrimitiveType.Cube); phEar.name = "Ear";
            Object.DestroyImmediate(phEar.GetComponent<Collider>());
            phEar.transform.SetParent(phone.transform, false);
            phEar.transform.localPosition = new Vector3(0.026f, 0.082f, 0f);
            phEar.transform.localScale = new Vector3(0.062f, 0.052f, 0.052f);
            phEar.GetComponent<Renderer>().sharedMaterial = pm;
            var phMouth = GameObject.CreatePrimitive(PrimitiveType.Cube); phMouth.name = "Mouth";
            Object.DestroyImmediate(phMouth.GetComponent<Collider>());
            phMouth.transform.SetParent(phone.transform, false);
            phMouth.transform.localPosition = new Vector3(-0.026f, -0.082f, 0f);
            phMouth.transform.localScale = new Vector3(0.062f, 0.052f, 0.052f);
            phMouth.GetComponent<Renderer>().sharedMaterial = pm;

            // napisy 112 (TextMeshPro 3D przed kamerą) — mniejszy font + węższy rect, żeby NIE wycinało za frustum
            var subGo = new GameObject("Subtitle"); var sub = subGo.AddComponent<TMPro.TextMeshPro>();
            sub.fontSize = 0.32f; sub.alignment = TMPro.TextAlignmentOptions.Center; sub.color = new Color(1f, 0.95f, 0.7f);
            sub.rectTransform.sizeDelta = new Vector2(1.2f, 0.34f); sub.enableWordWrapping = true;
            sub.fontStyle = TMPro.FontStyles.Bold;
            var subBgT = GameObject.CreatePrimitive(PrimitiveType.Quad); subBgT.name = "SubBg"; Object.DestroyImmediate(subBgT.GetComponent<Collider>());
            // PÓŁPRZEZROCZYSTA plansza napisu (nie pełnoekranowy czarny pas) — czyste „dolne 1/3"
            var subBgMat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
            subBgMat.SetFloat("_Surface", 1f); subBgMat.renderQueue = 3000;
            subBgMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            subBgMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            subBgMat.SetInt("_ZWrite", 0); subBgMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (subBgMat.HasProperty("_BaseColor")) subBgMat.SetColor("_BaseColor", new Color(0f, 0f, 0f, 0.5f)); else subBgMat.color = new Color(0f, 0f, 0f, 0.5f);
            subBgT.GetComponent<Renderer>().sharedMaterial = subBgMat;

            // ciemne listwy = krawędzie drzwi szafy (kadrują „szparę" – patrzysz przez uchyloną doulapę)
            var barMat = SolidURPLocal("M_GapBar", new Color(0.012f, 0.012f, 0.016f));
            var barL = GameObject.CreatePrimitive(PrimitiveType.Quad); barL.name = "GapL"; Object.DestroyImmediate(barL.GetComponent<Collider>()); barL.GetComponent<Renderer>().sharedMaterial = barMat;
            var barR = GameObject.CreatePrimitive(PrimitiveType.Quad); barR.name = "GapR"; Object.DestroyImmediate(barR.GetComponent<Collider>()); barR.GetComponent<Renderer>().sharedMaterial = barMat;
            // Widok 3/4 (diorama) — POV-słuchawka i listwy „spod łóżka" niepotrzebne
            phone.SetActive(false); barL.SetActive(false); barR.SetActive(false);
            // etykieta „TY — pod łóżkiem" nad łóżkiem
            var bedLabGo = new GameObject("BedLabel"); var bedLab = bedLabGo.AddComponent<TMPro.TextMeshPro>();
            bedLab.fontSize = 0.42f; bedLab.alignment = TMPro.TextAlignmentOptions.Center; bedLab.color = new Color(0.4f, 0.9f, 1f);
            bedLab.rectTransform.sizeDelta = new Vector2(3.4f, 0.6f); bedLab.text = "[ TY — pod łóżkiem ]";
            bedLabGo.SetActive(false);   // czysty kadr — kontekst niesie napis na dole

            // własny pasek ciszy/mikrofonu (HUD po prawej) – poprawnie zwrócony do kamery
            var micBg = GameObject.CreatePrimitive(PrimitiveType.Quad); micBg.name = "GifMicBg"; Object.DestroyImmediate(micBg.GetComponent<Collider>());
            micBg.GetComponent<Renderer>().sharedMaterial = SolidURPLocal("M_GifMicBg", new Color(0.04f, 0.04f, 0.05f));
            var micFill = GameObject.CreatePrimitive(PrimitiveType.Quad); micFill.name = "GifMicFill"; Object.DestroyImmediate(micFill.GetComponent<Collider>());
            var micFillMat = SolidURPLocal("M_GifMicFill", new Color(0.30f, 0.90f, 0.45f));
            micFill.GetComponent<Renderer>().sharedMaterial = micFillMat;
            var micLabGo = new GameObject("GifMicLabel"); var micLab = micLabGo.AddComponent<TMPro.TextMeshPro>();
            micLab.fontSize = 0.26f; micLab.alignment = TMPro.TextAlignmentOptions.Center; micLab.color = new Color(0.6f, 1f, 0.7f);
            micLab.rectTransform.sizeDelta = new Vector2(1.6f, 0.4f); micLab.text = "CISZA";

            string[] subs = {
                "Odbierasz dzwoniący telefon...",
                "Wczołgaj się POD ŁÓŻKO",
                "„Halo 112? Włamanie.”",
                "„Jestem pod łóżkiem w sypialni.”",
                "„Jestem sam — proszę o pomoc.”",
                "Dyspozytor: patrol za 2 minuty",
                "LEŻ CICHO — intruz w pokoju",
                "Syreny — intruz ucieka. Bezpiecznie.",
            };

            // ścieżka intruza w pokoju (holder-local): wchodzi od drzwi i SZUKA gracza
            Vector3[] path = {
                hp + hr * new Vector3( 0.00f, 0f, 2.85f),  // próg drzwi (pojawia się gdy drzwi się otworzą)
                hp + hr * new Vector3(-0.85f, 0f, 2.10f),  // przy łóżku
                hp + hr * new Vector3( 0.90f, 0f, 2.20f),  // przy biurku
                hp + hr * new Vector3( 0.15f, 0f, 1.50f),  // blisko gracza/szafy
                hp + hr * new Vector3(-0.60f, 0f, 1.95f),  // dalej szuka
            };
            const int doorStart = 42, doorEnd = 56, thiefShow = 48; // intruz wchodzi DOPIERO po formułkach do 112
            Debug.Log($"[A3Gif] cam={cam.transform.position} fwd={cam.transform.forward} hp={hp} yaw={yaw} doorPivot={(doorPivot!=null)}");
            // ukryj wbudowany MicBar (zwrócony tyłem -> tekst lustrzany); rysuję własny HUD
            var micBarT = FindAnyByName("MicBar"); if (micBarT != null) { var cg = micBarT.GetComponent<CanvasGroup>(); if (cg != null) cg.alpha = 0f; }

            for (int i = 0; i < N; i++)
            {
                float t = (float)i / (N - 1);
                // --- DRZWI: intruz je otwiera (panel obraca się na zawiasie) ---
                if (doorPivot != null)
                {
                    float d = Mathf.Clamp01((i - doorStart) / (float)(doorEnd - doorStart));
                    d = d * d * (3f - 2f * d); // smoothstep
                    doorPivot.localRotation = doorClosed * Quaternion.Euler(0f, 82f * d, 0f);
                }
                // --- INTRUZ: pojawia się gdy drzwi otwarte i SZUKA (chodzi po pokoju) ---
                bool thiefOn = i >= thiefShow;
                if (intruderModel != null)
                    foreach (var r in intruderModel.GetComponentsInChildren<Renderer>(true)) r.enabled = thiefOn;
                if (flash != null) flash.enabled = thiefOn;
                if (thiefOn && thiefGo != null)
                {
                    float wt = Mathf.Clamp01((i - thiefShow) / (float)(N - 1 - thiefShow));
                    float fp = wt * (path.Length - 1);
                    int seg = Mathf.Clamp(Mathf.FloorToInt(fp), 0, path.Length - 2);
                    float segT = fp - seg;
                    Vector3 tpos = Vector3.Lerp(path[seg], path[seg + 1], segT);
                    Vector3 nextp = Vector3.Lerp(path[seg], path[seg + 1], Mathf.Min(1f, segT + 0.05f));
                    Vector3 dirw = nextp - tpos; dirw.y = 0;
                    thiefGo.position = tpos;
                    if (dirw.sqrMagnitude > 1e-5f) thiefGo.rotation = Quaternion.LookRotation(dirw.normalized);
                    if (walkClip != null && intruderModel != null)
                    {
                        walkClip.SampleAnimation(intruderModel.gameObject, ((i - thiefShow) * 0.05f) % Mathf.Max(0.1f, walkClip.length));
                        Bounds wb = default; bool wf = true;
                        foreach (var r in intruderModel.GetComponentsInChildren<Renderer>()) { if (!r.enabled) continue; if (wf) { wb = r.bounds; wf = false; } else wb.Encapsulate(r.bounds); }
                        // wyśrodkuj X/Z na ścieżce ORAZ uziemij stopy (min.y) do podłogi y=0 — żeby nogi nie tonęły/nie wisiały
                        if (!wf) intruderModel.position += new Vector3(tpos.x - wb.center.x, (hp.y - wb.min.y), tpos.z - wb.center.z);
                    }
                }
                // telefon (retro słuchawka) – mały, dolny-lewy róg
                phone.transform.position = cam.transform.TransformPoint(new Vector3(-0.23f, -0.17f, 0.55f));
                phone.transform.rotation = cam.transform.rotation * Quaternion.Euler(6f, 16f, -16f);
                // listwy „spod łóżka": GÓRA (spód materaca) + DÓŁ (podłoga) — zostaje wąska pozioma szpara
                barL.transform.position = cam.transform.TransformPoint(new Vector3(0f, 0.62f, 1.2f));
                barL.transform.rotation = cam.transform.rotation; barL.transform.localScale = new Vector3(3.6f, 0.95f, 1f);
                barR.transform.position = cam.transform.TransformPoint(new Vector3(0f, -0.66f, 1.2f));
                barR.transform.rotation = cam.transform.rotation; barR.transform.localScale = new Vector3(3.6f, 0.95f, 1f);
                // napis (podpowiedzi 112) – mniejszy, niżej, szerokie czarne tło. Skala dopasowana do z=0.85 frustum.
                int si = Mathf.Clamp(Mathf.FloorToInt(t * subs.Length), 0, subs.Length - 1);
                sub.text = subs[si]; sub.ForceMeshUpdate();
                subGo.transform.position = cam.transform.TransformPoint(new Vector3(0f, -0.31f, 0.85f));
                subGo.transform.rotation = cam.transform.rotation;
                subBgT.transform.position = cam.transform.TransformPoint(new Vector3(0f, -0.31f, 0.87f));
                subBgT.transform.rotation = cam.transform.rotation; subBgT.transform.localScale = new Vector3(1.25f, 0.135f, 1f);
                // etykieta łóżka (kryjówka gracza) — nad łóżkiem, zwrócona do kamery
                bedLabGo.transform.position = hp + hr * new Vector3(-1.35f, 1.15f, 1.5f);
                bedLabGo.transform.rotation = Quaternion.LookRotation(bedLabGo.transform.position - cam.transform.position, Vector3.up);
                // HUD pasek mikrofonu/ciszy (prawa krawędź, z=1.0): rozmowa "MÓW", ukrycie "CISZA"
                bool silencePhase = si >= 6;   // LEŻ CICHO + syreny = cisza; formułki (si 2..5) = MÓW
                // mały, dyskretny wskaźnik mikrofonu w prawym dolnym rogu (nie wielki pas)
                micBg.transform.position = cam.transform.TransformPoint(new Vector3(0.82f, -0.14f, 1.0f));
                micBg.transform.rotation = cam.transform.rotation; micBg.transform.localScale = new Vector3(0.055f, 0.34f, 1f);
                float micLevel = silencePhase ? (0.06f + 0.05f * Mathf.Abs(Mathf.Sin(i * 0.7f)))
                                              : (0.35f + 0.45f * Mathf.Abs(Mathf.Sin(i * 0.9f)));
                micFillMat.SetColor("_BaseColor", silencePhase ? new Color(0.30f, 0.90f, 0.45f) : new Color(0.95f, 0.55f, 0.20f));
                const float fullH = 0.30f; float fh = fullH * micLevel;
                micFill.transform.position = cam.transform.TransformPoint(new Vector3(0.82f, -0.14f - (fullH - fh) * 0.5f, 0.99f));
                micFill.transform.rotation = cam.transform.rotation; micFill.transform.localScale = new Vector3(0.04f, fh, 1f);
                micLab.text = silencePhase ? "CISZA" : "MÓW";
                micLab.color = silencePhase ? new Color(0.6f, 1f, 0.7f) : new Color(1f, 0.8f, 0.45f);
                micLabGo.transform.position = cam.transform.TransformPoint(new Vector3(0.82f, -0.34f, 1.0f));
                micLabGo.transform.rotation = cam.transform.rotation;

                cam.Render();
                var prev2 = RenderTexture.active; RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply(); RenderTexture.active = prev2;
                File.WriteAllBytes($"{dir}/f{i:D3}.png", tex.EncodeToPNG());
            }
            cam.targetTexture = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
            Debug.Log("[A3Gif] DONE frames=" + N);
        }

        /// <summary>Diagnostyka: instancjonuje LeafiaRoom i listuje bounds każdego Object_X
        /// żeby zidentyfikować łóżko, biurko, drzwi, ściany itp. (model ma generyczne nazwy).</summary>
        public static void ProbeLeafiaParts()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/External/LeafiaRoom/LeafiaRoom.glb");
            if (prefab == null) { Debug.Log("NO PREFAB"); return; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            // No rotation - glTFast base rotation preserved
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith("Object_")) continue;
                var r = t.GetComponent<MeshRenderer>();
                if (r == null) continue;
                var b = r.bounds;
                string kind = "?";
                float w = b.size.x, h = b.size.y, d = b.size.z;
                float maxd = Mathf.Max(w, Mathf.Max(h, d));
                if (maxd > 14f) kind = "BACKDROP (sky/terrain)";
                else if (h < 0.3f && (w > 2f || d > 2f)) kind = "FLOOR/CEIL/PLANE";
                else if (h > 1.6f && Mathf.Min(w, d) < 0.4f) kind = "WALL/DOOR (thin tall)";
                else if (h < 0.8f && Mathf.Min(w, d) > 1.2f) kind = "BED/DESK (low+wide)";
                else if (h > 1.0f && h < 2.2f) kind = "FURNITURE (medium)";
                Debug.Log($"[Probe] {t.name} center=({b.center.x:F2},{b.center.y:F2},{b.center.z:F2}) size=({w:F2}x{h:F2}x{d:F2}) maxDim={maxd:F2} kind={kind}");
            }
        }

        /// <summary>Podgląd menu głównego (panel MR) do PNG. Passthrough niedostępny w edytorze → ciemne tło.</summary>
        [MenuItem("MRCrisis/Capture Menu")]
        public static void CaptureMenu()
        {
            var dir = OutDir; Directory.CreateDirectory(dir);
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainMenu.unity", OpenSceneMode.Single);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.5f, 0.52f, 0.58f); RenderSettings.skybox = null; RenderSettings.fog = false;
            var Lgo = new GameObject("MenuKey"); var L = Lgo.AddComponent<Light>();
            L.type = LightType.Directional; L.intensity = 1.0f; Lgo.transform.rotation = Quaternion.Euler(35f, 12f, 0f);

            const int W = 768, H = 432;
            var go = new GameObject("MenuCam"); var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.06f, 0.08f, 0.11f);
            cam.fieldOfView = 50; cam.nearClipPlane = 0.02f; cam.farClipPlane = 20f;
            cam.transform.position = new Vector3(0f, 1.5f, 0.05f); cam.transform.rotation = Quaternion.identity;
            EnablePost(cam);
            var rt = new RenderTexture(W, H, 24) { antiAliasing = 4 }; cam.targetTexture = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            cam.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply(); RenderTexture.active = prev;
            File.WriteAllBytes($"{dir}/menu.png", tex.EncodeToPNG());
            cam.targetTexture = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
            Debug.Log("[Menu] saved " + dir + "/menu.png");
        }

        /// <summary>Renderuje OBA showcase'y (Akt II jazda + Akt III pod łóżkiem) w jednym przebiegu batch.</summary>
        public static void CaptureShowcase()
        {
            CaptureDriveGif();
            CaptureAct3Gif();
            Debug.Log("[Showcase] DONE both gifs");
        }

        public static void ProbeLeafiaView()
        {
            var dir = "Builds/leafiaview";
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/External/LeafiaRoom/LeafiaRoom.glb");
            if (prefab == null) { Debug.Log("[LeafiaView] PREFAB NULL"); return; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            // disable the huge environment object so it doesn't clutter the interior view
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
                if (Mathf.Max(r.bounds.size.x, r.bounds.size.y, r.bounds.size.z) > 15f) r.enabled = false;
            float scale = 1.0f;
            go.transform.localScale = Vector3.one * scale;
            var baseRot = go.transform.rotation; // glTFast może mieć obrót bazowy — NIE nadpisuj go
            Debug.Log($"[LeafiaView] baseRot={baseRot.eulerAngles}");

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.8f, 0.8f, 0.85f); RenderSettings.fog = false; RenderSettings.skybox = null;
            var lgo = new GameObject("L"); var L = lgo.AddComponent<Light>(); L.type = LightType.Directional; L.intensity = 1.1f; lgo.transform.rotation = Quaternion.Euler(50, 30, 0);

            const int W = 512, H = 360;
            var camGo = new GameObject("PCam"); var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.05f,0.05f,0.06f);
            cam.fieldOfView = 75; cam.nearClipPlane = 0.02f; cam.farClipPlane = 80f;
            cam.transform.position = new Vector3(0, 1.4f, 0); cam.transform.rotation = Quaternion.identity; // look +Z
            var rt = new RenderTexture(W, H, 24){antiAliasing=2}; cam.targetTexture = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);

            for (int yaw = 0; yaw < 360; yaw += 45)
            {
                go.transform.rotation = Quaternion.Euler(0, yaw, 0) * baseRot;
                // recompute interior bounds and recenter room on player (X,Z) with floor at y=0
                Bounds b = default; bool f = true;
                foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
                { if (!r.enabled) continue; if (f){b=r.bounds;f=false;} else b.Encapsulate(r.bounds); }
                go.transform.position += new Vector3(-b.center.x, -b.min.y, -b.center.z);
                cam.Render();
                var prev = RenderTexture.active; RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0,0,W,H),0,0); tex.Apply(); RenderTexture.active = prev;
                File.WriteAllBytes($"{dir}/yaw{yaw:D3}.png", tex.EncodeToPNG());
                Debug.Log($"[LeafiaView] yaw={yaw} roomCenter={b.center} size={b.size}");
            }
            cam.targetTexture = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
            Debug.Log("[LeafiaView] DONE");
        }

        public static void InspectLeafiaRoom()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/External/LeafiaRoom/LeafiaRoom.glb");
            if (prefab == null) { Debug.Log("[Leafia] PREFAB NULL — glTFast import not ready"); return; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.position = Vector3.zero; go.transform.localScale = Vector3.one;
            // room bounds excluding the huge environment object (>15 m)
            Bounds room = default; bool first = true;
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (Mathf.Max(r.bounds.size.x, r.bounds.size.y, r.bounds.size.z) > 15f) continue;
                if (first) { room = r.bounds; first = false; } else room.Encapsulate(r.bounds);
            }
            Debug.Log($"[Leafia] ROOM bounds center={room.center} size={room.size} (excl. env)");
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                var b = r.bounds; var s = b.size;
                bool huge = Mathf.Max(s.x, s.y, s.z) > 15f;
                float minH = Mathf.Min(s.x, s.z), maxH = Mathf.Max(s.x, s.z);
                bool door = !huge && s.y > 1.7f && s.y < 2.8f && minH < 0.45f && maxH > 0.55f && maxH < 1.8f;
                string tag = huge ? "ENV" : (door ? "DOOR?" : "");
                Debug.Log($"[Leafia] {r.name,-14} size=({s.x:F2},{s.y:F2},{s.z:F2}) center=({b.center.x:F2},{b.center.y:F2},{b.center.z:F2}) {tag}");
            }
        }

        private static Material SolidURPLocal(string name, Color c)
        {
            var sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var m = new Material(sh);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c); else m.color = c;
            return m;
        }

        private static Material GlassURPLocal(string name, Color c)
        {
            var sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var m = new Material(sh) { name = name };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c); else m.color = c;
            // tryb przezroczysty (Surface=Transparent, alpha blending)
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_SURFACE_TYPE_OPAQUE");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return m;
        }

        private static Transform FindAnyByName(string n)
        {
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
                if (t.name == n && t.gameObject.scene.IsValid()) return t;
            return null;
        }
    }
}
