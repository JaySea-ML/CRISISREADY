using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using MRCrisisTrainer.Core;
using MRCrisisTrainer.XR;

namespace MRCrisisTrainer.EditorTools
{
    /// <summary>
    /// Proceduralnie buduje wirtualne laboratorium VR na podstawie analizy zdjęć referencyjnych.
    /// Pokój ~6m × 4m × 3m, 2 okna, drzwi, biurka, fotele Gamvis, sofa, szafa, TV, drukarka 3D.
    /// Anchory dla aktów: chair (Akt II), wardrobe (Akt III), desk/phone (Akt III).
    /// </summary>
    public static class LabRoomBuilder
    {
        private const string SceneDir = "Assets/_Project/Scenes";
        private const string MaterialsDir = "Assets/_Project/Materials/Lab";

        // Wymiary pokoju (metry)
        private const float RoomWidth = 6.5f;
        private const float RoomDepth = 5f;
        private const float RoomHeight = 3f;

        // Cache materiałów
        private static Material wallMat;
        private static Material floorMat;
        private static Material ceilingMat;
        private static Material doorMat;
        private static Material woodMat;
        private static Material sofaMat;
        private static Material gamvisMat;
        private static Material officeChairMat;
        private static Material plasticChairMat;
        private static Material deskMat;
        private static Material darkDeskMat;
        private static Material tvMat;
        private static Material monitorMat;
        private static Material printerMat;
        private static Material radiatorMat;
        private static Material windowMat;
        private static Material posterMat;

        [MenuItem("MRCrisis/Build Lab Room Scene", priority = 4)]
        public static void BuildLabRoom()
        {
            EnsureDirectories();
            CreateMaterials();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var roomRoot = new GameObject("LabRoom");
            var visualRoot = new GameObject("Visuals");
            visualRoot.transform.SetParent(roomRoot.transform, false);

            BuildShell(visualRoot.transform);
            BuildWindows(visualRoot.transform);
            BuildDoor(visualRoot.transform);
            BuildPosters(visualRoot.transform);
            BuildCeilingLights(visualRoot.transform);
            var desks = BuildDesks(visualRoot.transform);
            var furniture = BuildFurniture(visualRoot.transform);

            // RoomEnvironment + Anchors
            var env = roomRoot.AddComponent<RoomEnvironment>();
            var envSo = new SerializedObject(env);
            // Start w trybie PASSTHROUGH: widać realny pokój, wirtualny lab ukryty,
            // a Remy / obiekty aktów są nałożone na realne otoczenie (Mixed Reality).
            envSo.FindProperty("startMode").enumValueIndex = 1; // Passthrough
            envSo.FindProperty("roomVisualRoot").objectReferenceValue = visualRoot;
            envSo.FindProperty("victimSpawnAnchor").objectReferenceValue = furniture.victimSpawn;
            envSo.FindProperty("chairAnchor").objectReferenceValue = furniture.gamvisChair.transform;
            envSo.FindProperty("deskAnchor").objectReferenceValue = desks.leftDesk.transform;
            envSo.FindProperty("phoneAnchor").objectReferenceValue = furniture.phoneAnchor;
            envSo.FindProperty("wardrobeAnchor").objectReferenceValue = furniture.wardrobe.transform;
            envSo.FindProperty("playerStartAnchor").objectReferenceValue = furniture.playerStart;
            envSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(env);

            // XR Rig na PlayerStart
            var handProvider = BuildXRRig(furniture.playerStart.position);

            // Persistent managers (gdy scena odpalana bezpośrednio do testów)
            var managers = new GameObject("[Persistent]");
            managers.AddComponent<GameStateManager>();
            managers.AddComponent<SceneLoader>();
            managers.AddComponent<MRCrisisTrainer.Logging.JSONLLogger>();
            // PassthroughController jest na XR Rig (z ustawioną kamerą) - nie dublujemy tu.
            managers.AddComponent<SessionManager>();
            managers.AddComponent<AudioManager>();
            managers.AddComponent<PerformanceMonitor>();
            managers.AddComponent<SafeModeController>();
            managers.AddComponent<PermissionRequester>();
            // Bezpiecznik: przycisk B/Y kontrolera (lub klawisz M) przełącza MR (passthrough) ↔ wirtualny pokój
            managers.AddComponent<EnvironmentModeToggle>();

            // 2 akty z mikrokrokami + SessionFlowManager
            ActsBuilder.BuildActs(roomRoot.transform, env, handProvider);

            // Lights & skybox
            BuildEnvironmentLighting();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, $"{SceneDir}/TrainingRoom.unity");
            AssetDatabase.SaveAssets();

            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("MR Crisis Trainer",
                    "TrainingRoom.unity zbudowany!\n\nAnchory dostępne w RoomEnvironment:\n- VictimSpawn (środek podłogi)\n- Chair (fotel Gamvis)\n- Desk + Phone (lewe biurko)\n- Wardrobe (drewniana szafa)\n- PlayerStart\n\nMożesz otworzyć scenę i nacisnąć Play.", "OK");
            }
            Debug.Log("[LabRoomBuilder] TrainingRoom scene built.");
        }

        // ============== HELPERS ==============

        private static void EnsureDirectories()
        {
            foreach (var dir in new[] { SceneDir, MaterialsDir })
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

        private static Material CreateMat(string name, Color color, float smoothness = 0.3f, float metallic = 0f, Color? emission = null)
        {
            var path = $"{MaterialsDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            else mat.color = color;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (emission.HasValue && mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission.Value);
            }
            return mat;
        }

        private static void CreateMaterials()
        {
            // Buduje materiały URP z proceduralnych tekstur (LabMaterials).
            LabMaterials.BuildAll();
            wallMat = LabMaterials.Wall;
            floorMat = LabMaterials.Floor;
            ceilingMat = LabMaterials.Ceiling;
            doorMat = LabMaterials.Plastic;
            woodMat = LabMaterials.Wood;
            sofaMat = LabMaterials.FabricSofa;
            gamvisMat = LabMaterials.FabricDark;
            officeChairMat = LabMaterials.FabricDark;
            plasticChairMat = LabMaterials.Plastic;
            deskMat = LabMaterials.WoodLight;
            darkDeskMat = LabMaterials.Metal;
            tvMat = LabMaterials.Screen;
            monitorMat = LabMaterials.Screen;
            printerMat = LabMaterials.Metal;
            radiatorMat = LabMaterials.Radiator;
            windowMat = LabMaterials.Window;
            posterMat = LabMaterials.PosterBci;
        }

        // Budowanie skorupy pokoju (4 ściany, podłoga, sufit)
        private static void BuildShell(Transform parent)
        {
            float w = RoomWidth, d = RoomDepth, h = RoomHeight, t = 0.05f;

            CreateBox(parent, "Floor", new Vector3(0, -t / 2f, 0), new Vector3(w, t, d), floorMat);
            CreateBox(parent, "Ceiling", new Vector3(0, h + t / 2f, 0), new Vector3(w, t, d), ceilingMat);

            // 4 ściany: tylna (drzwi), przednia (okna), lewa, prawa
            CreateBox(parent, "WallNorth", new Vector3(0, h / 2f, d / 2f), new Vector3(w, h, t), wallMat); // okna
            CreateBox(parent, "WallSouth", new Vector3(0, h / 2f, -d / 2f), new Vector3(w, h, t), wallMat); // drzwi
            CreateBox(parent, "WallEast", new Vector3(w / 2f, h / 2f, 0), new Vector3(t, h, d), wallMat);
            CreateBox(parent, "WallWest", new Vector3(-w / 2f, h / 2f, 0), new Vector3(t, h, d), wallMat);
        }

        // 2 okna w ścianie północnej, kaloryfery pod każdym
        private static void BuildWindows(Transform parent)
        {
            float wallZ = RoomDepth / 2f - 0.03f;
            float windowY = 1.55f;
            float windowW = 1.4f, windowH = 1.6f;

            // Okno 1
            var win1 = CreateBox(parent, "Window1", new Vector3(-1.5f, windowY, wallZ), new Vector3(windowW, windowH, 0.02f), windowMat);
            // Okno 2
            var win2 = CreateBox(parent, "Window2", new Vector3(1.5f, windowY, wallZ), new Vector3(windowW, windowH, 0.02f), windowMat);

            // Widok za oknami - niebo + drzewa (tekstura window_view)
            var viewMat = LabMaterials.WindowView;
            CreateBox(parent, "WindowView1", new Vector3(-1.5f, windowY, wallZ + 0.4f), new Vector3(windowW * 1.6f, windowH * 1.6f, 0.01f), viewMat);
            CreateBox(parent, "WindowView2", new Vector3(1.5f, windowY, wallZ + 0.4f), new Vector3(windowW * 1.6f, windowH * 1.6f, 0.01f), viewMat);

            // Kaloryfer pod każdym oknem
            CreateBox(parent, "Radiator1", new Vector3(-1.5f, 0.35f, wallZ - 0.1f), new Vector3(1.3f, 0.55f, 0.12f), radiatorMat);
            CreateBox(parent, "Radiator2", new Vector3(1.5f, 0.35f, wallZ - 0.1f), new Vector3(1.3f, 0.55f, 0.12f), radiatorMat);

            // Parapet niski pod oknem (cały wzdłuż ściany)
            CreateBox(parent, "Sill", new Vector3(0, 0.75f, wallZ - 0.2f), new Vector3(RoomWidth - 0.4f, 0.1f, 0.25f), wallMat);
        }

        // Drzwi w ścianie południowej (lewa strona)
        private static void BuildDoor(Transform parent)
        {
            float wallZ = -RoomDepth / 2f + 0.03f;
            CreateBox(parent, "DoorFrame", new Vector3(0, 1.05f, wallZ - 0.01f), new Vector3(1.0f, 2.1f, 0.04f), doorMat);
            CreateBox(parent, "DoorHandle", new Vector3(0.35f, 1.0f, wallZ - 0.04f), new Vector3(0.05f, 0.05f, 0.05f), darkDeskMat);
        }

        // Plakaty naukowe na ścianach (BCI/EEG/VR jak na zdjęciach)
        private static void BuildPosters(Transform parent)
        {
            float wallE = RoomWidth / 2f - 0.04f;
            var posterMats = new[] { LabMaterials.PosterBci, LabMaterials.PosterVr, LabMaterials.PosterEeg, LabMaterials.PosterBci };
            for (int i = 0; i < 4; i++)
            {
                float z = -RoomDepth / 2f + 1.2f + i * 1.1f;
                CreateBox(parent, $"Poster_E_{i}", new Vector3(wallE, 1.9f, z), new Vector3(0.03f, 0.85f, 0.6f), posterMats[i]);
            }
            // Kalendarz / mała ramka na wschodniej ścianie (między plakatami)
            CreateBox(parent, "Calendar", new Vector3(wallE, 1.4f, 0.5f), new Vector3(0.03f, 0.35f, 0.25f), posterMat);
        }

        // Sufitowe lampy LED płaskie (2 sztuki)
        private static void BuildCeilingLights(Transform parent)
        {
            var ledMat = CreateMat("LEDPanel", new Color(0.95f, 0.97f, 1f), smoothness: 0.5f, emission: new Color(1f, 0.98f, 0.95f) * 2f);
            CreateBox(parent, "LED_1", new Vector3(-1.5f, RoomHeight - 0.06f, 1.0f), new Vector3(0.6f, 0.04f, 0.6f), ledMat);
            CreateBox(parent, "LED_2", new Vector3(1.5f, RoomHeight - 0.06f, -0.5f), new Vector3(0.6f, 0.04f, 0.6f), ledMat);

            // Faktyczne źródła światła
            var l1 = new GameObject("Light_LED_1");
            l1.transform.SetParent(parent, false);
            l1.transform.position = new Vector3(-1.5f, RoomHeight - 0.1f, 1.0f);
            var pl1 = l1.AddComponent<Light>();
            pl1.type = LightType.Point;
            pl1.color = new Color(1f, 0.97f, 0.92f);
            pl1.intensity = 8f;
            pl1.range = 6f;

            var l2 = new GameObject("Light_LED_2");
            l2.transform.SetParent(parent, false);
            l2.transform.position = new Vector3(1.5f, RoomHeight - 0.1f, -0.5f);
            var pl2 = l2.AddComponent<Light>();
            pl2.type = LightType.Point;
            pl2.color = new Color(1f, 0.97f, 0.92f);
            pl2.intensity = 8f;
            pl2.range = 6f;
        }

        private class DesksResult { public GameObject leftDesk; public GameObject rightDesk; }

        // Biurka po lewej i prawej stronie (długie wzdłuż ścian)
        private static DesksResult BuildDesks(Transform parent)
        {
            float deskTopY = 0.74f;
            float deskThickness = 0.04f;
            float legsThickness = 0.04f;

            // Lewe długie biurko (drewniane jasne - widoczne na zdjęciu 1)
            var leftDesk = new GameObject("LeftDesk");
            leftDesk.transform.SetParent(parent, false);
            leftDesk.transform.position = new Vector3(-RoomWidth / 2f + 0.4f, 0, 0.5f);

            CreateBox(leftDesk.transform, "LeftDesk_Top",
                new Vector3(0, deskTopY, 0), new Vector3(0.8f, deskThickness, 3.5f), deskMat);
            CreateBox(leftDesk.transform, "LeftDesk_LegFront",
                new Vector3(0.3f, deskTopY / 2f, 1.7f), new Vector3(legsThickness, deskTopY, legsThickness), darkDeskMat);
            CreateBox(leftDesk.transform, "LeftDesk_LegBack",
                new Vector3(0.3f, deskTopY / 2f, -1.7f), new Vector3(legsThickness, deskTopY, legsThickness), darkDeskMat);
            CreateBox(leftDesk.transform, "LeftDesk_LegFrontInner",
                new Vector3(-0.3f, deskTopY / 2f, 1.7f), new Vector3(legsThickness, deskTopY, legsThickness), darkDeskMat);
            CreateBox(leftDesk.transform, "LeftDesk_LegBackInner",
                new Vector3(-0.3f, deskTopY / 2f, -1.7f), new Vector3(legsThickness, deskTopY, legsThickness), darkDeskMat);

            // Czajnik na lewym biurku
            CreateBox(leftDesk.transform, "Kettle",
                new Vector3(0, deskTopY + 0.13f, -1.5f), new Vector3(0.18f, 0.25f, 0.18f), monitorMat);

            // Słuchawki (placeholder)
            CreateBox(leftDesk.transform, "Headphones",
                new Vector3(0, deskTopY + 0.06f, -1.0f), new Vector3(0.22f, 0.1f, 0.15f), darkDeskMat);

            // Drukarka 3D w głębi
            var printer = new GameObject("Printer3D");
            printer.transform.SetParent(leftDesk.transform, false);
            printer.transform.localPosition = new Vector3(0, deskTopY + 0.25f, 1.4f);
            CreateBox(printer.transform, "PrinterFrame",
                new Vector3(0, 0, 0), new Vector3(0.35f, 0.5f, 0.35f), printerMat);
            CreateBox(printer.transform, "PrinterBed",
                new Vector3(0, -0.2f, 0), new Vector3(0.28f, 0.05f, 0.28f), darkDeskMat);

            // Monitor + komputer na lewym biurku (z przodu, gdzie laptop na zdjęciu 5)
            CreateBox(leftDesk.transform, "LeftMonitor",
                new Vector3(0, deskTopY + 0.3f, -1.6f), new Vector3(0.55f, 0.35f, 0.04f), monitorMat);

            // Prawe biurko (ciemne, długie - widoczne na zdjęciach 1, 3, 4)
            var rightDesk = new GameObject("RightDesk");
            rightDesk.transform.SetParent(parent, false);
            rightDesk.transform.position = new Vector3(RoomWidth / 2f - 0.4f, 0, 0.5f);

            CreateBox(rightDesk.transform, "RightDesk_Top",
                new Vector3(0, deskTopY, 0), new Vector3(0.65f, deskThickness, 3.5f), darkDeskMat);
            CreateBox(rightDesk.transform, "RightDesk_LegFront",
                new Vector3(0.25f, deskTopY / 2f, 1.7f), new Vector3(legsThickness, deskTopY, legsThickness), darkDeskMat);
            CreateBox(rightDesk.transform, "RightDesk_LegBack",
                new Vector3(0.25f, deskTopY / 2f, -1.7f), new Vector3(legsThickness, deskTopY, legsThickness), darkDeskMat);
            CreateBox(rightDesk.transform, "RightDesk_LegFrontInner",
                new Vector3(-0.25f, deskTopY / 2f, 1.7f), new Vector3(legsThickness, deskTopY, legsThickness), darkDeskMat);
            CreateBox(rightDesk.transform, "RightDesk_LegBackInner",
                new Vector3(-0.25f, deskTopY / 2f, -1.7f), new Vector3(legsThickness, deskTopY, legsThickness), darkDeskMat);

            // 2 monitory na prawym biurku
            CreateBox(rightDesk.transform, "RightMonitor_1",
                new Vector3(0, deskTopY + 0.3f, 0.5f), new Vector3(0.55f, 0.35f, 0.04f), monitorMat);
            CreateBox(rightDesk.transform, "RightMonitor_2",
                new Vector3(0, deskTopY + 0.3f, -0.5f), new Vector3(0.55f, 0.35f, 0.04f), monitorMat);

            // Komputer pod biurkiem
            CreateBox(rightDesk.transform, "PCTower",
                new Vector3(0.1f, 0.3f, 1.4f), new Vector3(0.22f, 0.45f, 0.45f), tvMat);

            return new DesksResult { leftDesk = leftDesk, rightDesk = rightDesk };
        }

        private class FurnitureResult
        {
            public Transform victimSpawn;
            public GameObject gamvisChair;
            public Transform phoneAnchor;
            public GameObject wardrobe;
            public Transform playerStart;
        }

        // Sofa, szafa, fotele Gamvis, krzesła biurowe, TV na statywie
        private static FurnitureResult BuildFurniture(Transform parent)
        {
            var result = new FurnitureResult();

            // TV na statywie w środku pokoju (między oknami)
            var tvStand = new GameObject("TVStand");
            tvStand.transform.SetParent(parent, false);
            tvStand.transform.position = new Vector3(0, 0, RoomDepth / 2f - 1.2f);

            CreateBox(tvStand.transform, "TVStand_Pole",
                new Vector3(0, 0.55f, 0), new Vector3(0.08f, 1.1f, 0.08f), darkDeskMat);
            CreateBox(tvStand.transform, "TVStand_Base",
                new Vector3(0, 0.05f, 0), new Vector3(0.7f, 0.05f, 0.5f), darkDeskMat);
            CreateBox(tvStand.transform, "TVScreen",
                new Vector3(0, 1.4f, 0.05f), new Vector3(1.6f, 0.9f, 0.06f), tvMat);

            // Sofa pod ścianą zachodnią
            var sofa = new GameObject("Sofa");
            sofa.transform.SetParent(parent, false);
            sofa.transform.position = new Vector3(-RoomWidth / 2f + 1.2f, 0, -RoomDepth / 2f + 1.2f);
            CreateBox(sofa.transform, "Sofa_Base",
                new Vector3(0, 0.25f, 0), new Vector3(1.8f, 0.5f, 0.9f), sofaMat);
            CreateBox(sofa.transform, "Sofa_Back",
                new Vector3(0, 0.65f, -0.35f), new Vector3(1.8f, 0.6f, 0.2f), sofaMat);
            CreateBox(sofa.transform, "Sofa_ArmL",
                new Vector3(-0.85f, 0.45f, 0), new Vector3(0.15f, 0.45f, 0.9f), sofaMat);
            CreateBox(sofa.transform, "Sofa_ArmR",
                new Vector3(0.85f, 0.45f, 0), new Vector3(0.15f, 0.45f, 0.9f), sofaMat);

            // Drewniana szafa pod ścianą południową (obok drzwi, prawa strona)
            var wardrobe = new GameObject("Wardrobe");
            wardrobe.transform.SetParent(parent, false);
            wardrobe.transform.position = new Vector3(1.6f, 0, -RoomDepth / 2f + 0.4f);
            CreateBox(wardrobe.transform, "Wardrobe_Body",
                new Vector3(0, 1.0f, 0), new Vector3(1.2f, 2.0f, 0.55f), woodMat);
            // Naklejki na drzwiczkach (place holdery - małe kolorowe kwadraty)
            for (int i = 0; i < 6; i++)
            {
                float x = -0.35f + (i % 3) * 0.35f;
                float y = 0.6f + (i / 3) * 0.5f;
                var stickerColor = new Color(Random.value, Random.value, Random.value);
                var stickerMat = CreateMat($"Sticker_{i}", stickerColor, smoothness: 0.2f);
                CreateBox(wardrobe.transform, $"Sticker_{i}",
                    new Vector3(x, y, 0.3f), new Vector3(0.12f, 0.12f, 0.005f), stickerMat);
            }
            // Pudełka na szafie
            for (int i = 0; i < 3; i++)
            {
                CreateBox(wardrobe.transform, $"Box_{i}",
                    new Vector3(-0.2f + i * 0.2f, 2.18f, 0), new Vector3(0.4f, 0.3f, 0.4f), printerMat);
            }
            result.wardrobe = wardrobe;

            // Wieszak na ubrania (przed szafą)
            var coatRack = new GameObject("CoatRack");
            coatRack.transform.SetParent(parent, false);
            coatRack.transform.position = new Vector3(0.4f, 0, -RoomDepth / 2f + 0.8f);
            CreateBox(coatRack.transform, "CoatRack_Pole",
                new Vector3(0, 0.95f, 0), new Vector3(0.06f, 1.9f, 0.06f), darkDeskMat);
            CreateBox(coatRack.transform, "CoatRack_Base",
                new Vector3(0, 0.04f, 0), new Vector3(0.4f, 0.04f, 0.4f), darkDeskMat);
            CreateBox(coatRack.transform, "CoatRack_Top",
                new Vector3(0, 1.95f, 0), new Vector3(0.4f, 0.04f, 0.4f), darkDeskMat);

            // 4 fotele Gamvis (czarne) - 1 przy lewym biurku (gracz), 2 na środku, 1 na prawo
            var gamvisMain = BuildGamvisChair(parent, "GamvisChair_Main",
                new Vector3(-1.3f, 0, -0.5f), Quaternion.Euler(0, 60, 0));
            result.gamvisChair = gamvisMain;

            BuildGamvisChair(parent, "GamvisChair_Right",
                new Vector3(RoomWidth / 2f - 1.5f, 0, -1.5f), Quaternion.Euler(0, -90, 0));
            BuildGamvisChair(parent, "GamvisChair_Center",
                new Vector3(-0.5f, 0, -1.2f), Quaternion.Euler(0, 0, 0));

            // 3 krzesła biurowe na kółkach (środek pokoju)
            BuildOfficeChair(parent, "OfficeChair_1", new Vector3(1.4f, 0, -0.5f), Quaternion.Euler(0, -90, 0));
            BuildOfficeChair(parent, "OfficeChair_2", new Vector3(2.0f, 0, 0.5f), Quaternion.Euler(0, -90, 0));
            BuildOfficeChair(parent, "OfficeChair_3", new Vector3(1.2f, 0, 1.5f), Quaternion.Euler(0, -90, 0));

            // Białe plastikowe krzesło (widoczne na zdjęciach 2, 3)
            BuildPlasticChair(parent, "PlasticChair", new Vector3(0.8f, 0, 1.6f), Quaternion.Euler(0, 180, 0));

            // Anchory dla scenariuszy
            // Victim spawn = środek wolnej przestrzeni
            var victimAnchor = new GameObject("VictimSpawnAnchor");
            victimAnchor.transform.SetParent(parent, false);
            victimAnchor.transform.position = new Vector3(0, 0, 0.3f);
            result.victimSpawn = victimAnchor.transform;

            // Phone anchor - na lewym biurku
            var phoneAnchor = new GameObject("PhoneAnchor");
            phoneAnchor.transform.SetParent(parent, false);
            phoneAnchor.transform.position = new Vector3(-RoomWidth / 2f + 0.7f, 0.85f, -1.0f);
            result.phoneAnchor = phoneAnchor.transform;

            // Player start - naprzeciw Gamvis chair (gracz wchodzi do pokoju)
            var playerStart = new GameObject("PlayerStartAnchor");
            playerStart.transform.SetParent(parent, false);
            playerStart.transform.position = new Vector3(0, 0, -1.5f);
            playerStart.transform.rotation = Quaternion.Euler(0, 0, 0); // patrzy w stronę okien
            result.playerStart = playerStart.transform;

            return result;
        }

        private static GameObject BuildGamvisChair(Transform parent, string name, Vector3 pos, Quaternion rot)
        {
            var chair = new GameObject(name);
            chair.transform.SetParent(parent, false);
            chair.transform.position = pos;
            chair.transform.rotation = rot;

            // Wysokie oparcie (charakterystyczne dla Gamvis racing)
            CreateBox(chair.transform, "Seat",
                new Vector3(0, 0.5f, 0), new Vector3(0.55f, 0.1f, 0.55f), gamvisMat);
            CreateBox(chair.transform, "Backrest",
                new Vector3(0, 1.15f, -0.27f), new Vector3(0.55f, 1.3f, 0.1f), gamvisMat);
            // Boczne osłony pleców (wysokie)
            CreateBox(chair.transform, "BackrestSideL",
                new Vector3(-0.25f, 0.95f, -0.15f), new Vector3(0.08f, 0.9f, 0.4f), gamvisMat);
            CreateBox(chair.transform, "BackrestSideR",
                new Vector3(0.25f, 0.95f, -0.15f), new Vector3(0.08f, 0.9f, 0.4f), gamvisMat);
            // Logo (białe na czarnym) - mały detal
            var logoMat = CreateMat("GamvisLogo", new Color(0.9f, 0.85f, 0.4f), smoothness: 0.5f);
            CreateBox(chair.transform, "Logo",
                new Vector3(0, 1.5f, -0.21f), new Vector3(0.15f, 0.12f, 0.02f), logoMat);
            // Pięcioramienna podstawa (uproszczona)
            CreateBox(chair.transform, "Base",
                new Vector3(0, 0.05f, 0), new Vector3(0.55f, 0.08f, 0.55f), darkDeskMat);
            CreateBox(chair.transform, "Column",
                new Vector3(0, 0.3f, 0), new Vector3(0.08f, 0.4f, 0.08f), darkDeskMat);

            return chair;
        }

        private static GameObject BuildOfficeChair(Transform parent, string name, Vector3 pos, Quaternion rot)
        {
            var chair = new GameObject(name);
            chair.transform.SetParent(parent, false);
            chair.transform.position = pos;
            chair.transform.rotation = rot;

            CreateBox(chair.transform, "Seat",
                new Vector3(0, 0.5f, 0), new Vector3(0.5f, 0.08f, 0.5f), officeChairMat);
            CreateBox(chair.transform, "Backrest",
                new Vector3(0, 0.95f, -0.2f), new Vector3(0.5f, 0.7f, 0.08f), officeChairMat);
            CreateBox(chair.transform, "Base",
                new Vector3(0, 0.05f, 0), new Vector3(0.55f, 0.06f, 0.55f), darkDeskMat);
            CreateBox(chair.transform, "Column",
                new Vector3(0, 0.3f, 0), new Vector3(0.06f, 0.45f, 0.06f), darkDeskMat);
            return chair;
        }

        private static GameObject BuildPlasticChair(Transform parent, string name, Vector3 pos, Quaternion rot)
        {
            var chair = new GameObject(name);
            chair.transform.SetParent(parent, false);
            chair.transform.position = pos;
            chair.transform.rotation = rot;

            CreateBox(chair.transform, "Seat",
                new Vector3(0, 0.45f, 0), new Vector3(0.45f, 0.05f, 0.45f), plasticChairMat);
            CreateBox(chair.transform, "Backrest",
                new Vector3(0, 0.78f, -0.22f), new Vector3(0.45f, 0.6f, 0.04f), plasticChairMat);
            // 4 metalowe nogi
            float legY = 0.22f;
            foreach (var (xs, zs) in new[] { (-1, -1), (-1, 1), (1, -1), (1, 1) })
            {
                CreateBox(chair.transform, $"Leg_{xs}_{zs}",
                    new Vector3(xs * 0.2f, legY, zs * 0.2f), new Vector3(0.025f, 0.45f, 0.025f), darkDeskMat);
            }
            return chair;
        }

        private static void BuildEnvironmentLighting()
        {
            // Główne światło słoneczne od okien
            var sun = new GameObject("SunLight");
            var sunLight = sun.AddComponent<Light>();
            sunLight.type = LightType.Directional;
            sunLight.color = new Color(1f, 0.97f, 0.88f);
            sunLight.intensity = 1.3f;
            sunLight.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(25f, -40f, 0); // wpada przez okna

            // Ambient
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.7f, 0.78f, 0.85f);
            RenderSettings.ambientEquatorColor = new Color(0.65f, 0.6f, 0.55f);
            RenderSettings.ambientGroundColor = new Color(0.4f, 0.38f, 0.32f);
        }

        private static MonoBehaviour BuildXRRig(Vector3 startPosition)
        {
            var rig = new GameObject("XR Rig");
            rig.transform.position = startPosition;
            var camOffset = new GameObject("Camera Offset");
            camOffset.transform.SetParent(rig.transform, false);
            // Offset 0: TrackedPoseDriver z floor-tracking ustawia kamerę na wysokości oczu.
            camOffset.transform.localPosition = Vector3.zero;

            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(camOffset.transform, false);
            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 50f;
            XRRigHelper.AddHeadTracking(camGo);
            XRRigHelper.ConfigureXROrigin(rig, camOffset, cam);
            // MR: warstwa passthrough build-time (jak Building Block) — realny pokój w onboardingu/Akcie III
            MetaPassthroughSetup.EnsureSceneOVRPassthrough(rig, cam);

            // Hand placeholders
            var leftHand = new GameObject("LeftHandPlaceholder");
            leftHand.transform.SetParent(camOffset.transform, false);
            leftHand.transform.localPosition = new Vector3(-0.2f, -0.3f, 0.4f);
            var rightHand = new GameObject("RightHandPlaceholder");
            rightHand.transform.SetParent(camOffset.transform, false);
            rightHand.transform.localPosition = new Vector3(0.2f, -0.3f, 0.4f);

            // Hand tracking + kontrolery + composite (działa z rękami LUB padami)
            var xrHands = rig.AddComponent<XRHandsPoseProvider>();
            var controllers = rig.AddComponent<ControllerPoseProvider>();
            // trackingOrigin = Camera Offset: pozy rąk transformowane tak samo jak kamera (poprawna wysokość)
            var xhSo = new SerializedObject(xrHands);
            xhSo.FindProperty("trackingOrigin").objectReferenceValue = camOffset.transform;
            xhSo.ApplyModifiedPropertiesWithoutUndo();
            var cpSo = new SerializedObject(controllers);
            cpSo.FindProperty("trackingOrigin").objectReferenceValue = camOffset.transform;
            cpSo.ApplyModifiedPropertiesWithoutUndo();
            var composite = rig.AddComponent<CompositeHandPoseProvider>();
            var compSo = new SerializedObject(composite);
            compSo.FindProperty("hands").objectReferenceValue = xrHands;
            compSo.FindProperty("controllers").objectReferenceValue = controllers;
            compSo.ApplyModifiedPropertiesWithoutUndo();

            // Wizualizacja dłoni/kontrolerów
            var vis = rig.AddComponent<HandVisualizer>();
            var visSo = new SerializedObject(vis);
            visSo.FindProperty("handProviderBehaviour").objectReferenceValue = composite;
            visSo.FindProperty("trackingOrigin").objectReferenceValue = camOffset.transform;   // dłonie dokładnie przy realnych
            visSo.ApplyModifiedPropertiesWithoutUndo();

            var passthrough = rig.AddComponent<PassthroughController>();
            var ptSo = new SerializedObject(passthrough);
            ptSo.FindProperty("xrCamera").objectReferenceValue = cam;
            // Default: passthrough OFF (full VR with virtual room)
            ptSo.FindProperty("enableOnStart").boolValue = false;
            ptSo.ApplyModifiedPropertiesWithoutUndo();
            MetaSceneBuilder.AddFirstPersonBody(camOffset.transform, cam.transform, composite);   // pierwszoosobowe ciało (ręce/ramiona/nogi)
            return composite;
        }

        private static GameObject CreateBox(Transform parent, string name, Vector3 localPos, Vector3 localScale, Material mat)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = localPos;
            box.transform.localScale = localScale;
            UnityEngine.Object.DestroyImmediate(box.GetComponent<Collider>());
            box.GetComponent<Renderer>().sharedMaterial = mat;
            return box;
        }
    }
}
