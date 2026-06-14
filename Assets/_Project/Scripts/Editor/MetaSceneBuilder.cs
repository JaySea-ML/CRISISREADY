using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;
using TMPro;
using MRCrisisTrainer.Core;
using MRCrisisTrainer.XR;
using MRCrisisTrainer.UI;
using MRCrisisTrainer.Research;

namespace MRCrisisTrainer.EditorTools
{
    /// <summary>Buduje sceny meta: Bootstrap, MainMenu (consent+menu), PostSession (kwestionariusze+debrief).</summary>
    public static class MetaSceneBuilder
    {
        private const string SceneDir = "Assets/_Project/Scenes";
        private const string QDir = "Assets/_Project/ScriptableObjects/Questionnaires";
        private const string InterfaceDir = "Assets/_Project/External/Interface";

        /// <summary>Wymusza import PNG jako Sprite (2D/UI) i zwraca załadowany Sprite. Bez tego LoadAssetAtPath&lt;Sprite&gt; zwraca null (domyślnie tekstury importują się jako Default).</summary>
        private static Sprite LoadInterfaceSprite(string fileName)
        {
            string path = $"{InterfaceDir}/{fileName}";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[Interface] Brak tekstury (lub nie-tekstura): {path}");
                return null;
            }
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.SaveAndReimport();
            }
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) Debug.LogWarning($"[Interface] Nie udało się załadować Sprite: {path}");
            return sprite;
        }

        /// <summary>Podmienia grafikę przycisku na podany Sprite (zachowuje Button/raycast → gaze i kontroler działają). Ukrywa tekstowy label.</summary>
        private static void ApplyButtonSprite(Button btn, Sprite sprite)
        {
            if (btn == null || sprite == null) return;
            var img = btn.GetComponent<Image>();
            if (img != null)
            {
                img.sprite = sprite;
                img.color = Color.white;   // pełny kolor grafiki (bez przyciemnienia tłem przycisku)
                img.type = Image.Type.Simple;
                img.preserveAspect = true;
            }
            var lbl = btn.GetComponentInChildren<TMP_Text>();
            if (lbl != null) lbl.gameObject.SetActive(false);   // grafika zawiera już napis (GRAJ / TRENING)
        }

        public static void BuildAll()
        {
            BuildBootstrap();
            BuildMainMenu();
            BuildTutorial();
            BuildPostSession();
            BuildGameOver();
        }

        // ---------------- BOOTSTRAP ----------------
        public static void BuildBootstrap()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AddManagers();
            var rig = SimpleRig(false);
            var cam = rig.GetComponentInChildren<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.05f, 0.08f);

            // JEDEN trwały zestaw passthrough na całą grę (OVRManager + warstwa), budowany tylko tutaj.
            // Przeżywa wszystkie sceny (DontDestroyOnLoad) → brak duplikatów, warstwa rejestruje się raz (numLayers:1).
            MetaPassthroughSetup.BuildPersistentPassthroughRig();

            var boot = new GameObject("Bootstrap");
            var init = boot.AddComponent<BootstrapInitializer>();
            var so = new SerializedObject(init);
            so.FindProperty("firstActSceneName").stringValue = "MainMenu";
            so.FindProperty("waitForEnterKey").boolValue = false;
            so.FindProperty("autoStartDelay").floatValue = 1.4f;
            so.ApplyModifiedPropertiesWithoutUndo();

            var canvas = UIFactory.WorldCanvas("Splash", new Vector3(0, 1.5f, 1.8f), Quaternion.identity, new Vector2(700, 220), 0.0022f);
            canvas.gameObject.AddComponent<MRCrisisTrainer.UI.PlaceInFrontOfUser>();
            UIFactory.Text(canvas.transform, "MR CRISIS TRAINER", 54, new Vector2(680, 120), new Vector2(0, 20),
                TextAlignmentOptions.Center, new Color(0.30f, 0.78f, 1f));
            UIFactory.Text(canvas.transform, "Ładowanie...", 26, new Vector2(680, 60), new Vector2(0, -70),
                TextAlignmentOptions.Center, new Color(0.8f, 0.85f, 0.95f));

            var light = new GameObject("Light").AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1f;
            light.transform.rotation = Quaternion.Euler(50, -30, 0);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, $"{SceneDir}/Bootstrap.unity");
        }

        // ---------------- MAIN MENU ----------------
        public static void BuildMainMenu()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AddManagers();
            UIFactory.EnsureEventSystem();
            var rig = SimpleRig(true);
            var cam = rig.GetComponentInChildren<Camera>();
            // MR: menu pływa w REALNYM pokoju (passthrough). Kamera czyści na przezroczysto.
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            EnablePostOnCamera(cam);
            var ptGo = new GameObject("PassthroughController");
            ptGo.AddComponent<MRCrisisTrainer.XR.PassthroughController>();
            Skybox();

            var accent = new Color(0.30f, 0.78f, 1f);
            var canvas = UIFactory.WorldCanvas("MenuCanvas", new Vector3(0, 1.5f, 1.85f), Quaternion.identity, new Vector2(1000, 720), 0.0017f);
            canvas.gameObject.AddComponent<MRCrisisTrainer.UI.PlaceInFrontOfUser>();   // menu zawsze PRZED graczem (niezależnie gdzie patrzy)

            var menu = new GameObject("MenuPanel", typeof(RectTransform));
            menu.transform.SetParent(canvas.transform, false);
            // ramka akcentu + ciemne tło (TYLKO fallback, gdy ui_start.png się nie wczyta — w pełni zasłonięte grafiką)
            UIFactory.Panel(menu.transform, new Vector2(1012, 732), new Color(accent.r, accent.g, accent.b, 0.5f));
            UIFactory.Panel(menu.transform, new Vector2(1000, 720), new Color(0.04f, 0.06f, 0.09f, 0.95f));
            // TŁO CRISISREADY (ui_start.png) — JEDYNA grafika tytułowa, wypełnia CAŁY panel menu (bez własnych napisów TMP).
            var bgSprite = LoadInterfaceSprite("ui_start.png");
            if (bgSprite != null)
            {
                var bg = UIFactory.Panel(menu.transform, new Vector2(1000, 720), Color.white);
                bg.sprite = bgSprite;
                bg.type = Image.Type.Simple;
                bg.preserveAspect = false;   // rozciągnij na cały panel = pełnoekranowe tło
                bg.raycastTarget = false;    // tło nie przechwytuje gaze/pointer (przyciski działają)
            }

            // === Pulsujący przycisk GRAJ (grafika ui_btn_graj) — DOLNA, pusta część panelu, wyśrodkowany ===
            // ui_start.png ma tytuł/logo w GÓRNO-ŚRODKOWEJ części; przyciski idą NIŻEJ, by nie zasłaniać grafiki.
            // Box 360×200 (3:2 = aspekt sprite'a) + preserveAspect → grafika wypełnia box, cel kliknięcia = widoczna grafika.
            var startBtn = UIFactory.Button(menu.transform, "GRAJ", new Vector2(360, 200), new Vector2(0, -50),
                new Color(0.16f, 0.62f, 0.34f));
            ApplyButtonSprite(startBtn, LoadInterfaceSprite("ui_btn_graj.png"));   // grafika CRISISREADY "GRAJ" (ukrywa label TMP)
            startBtn.gameObject.AddComponent<MRCrisisTrainer.UI.UIPulse>();

            var controller = canvas.gameObject.AddComponent<MainMenuController>();
            var cso = new SerializedObject(controller);
            cso.FindProperty("consentPanel").objectReferenceValue = null;   // bez panelu RODO → od razu GRAJ
            cso.FindProperty("menuPanel").objectReferenceValue = menu;
            cso.FindProperty("startButton").objectReferenceValue = startBtn;
            cso.FindProperty("trainingScene").stringValue = "TrainingRoom";   // GRAJ → OD RAZU gra (akt poślizgu), z pominięciem samouczka
            cso.ApplyModifiedPropertiesWithoutUndo();

            // DRUGI PRZYCISK: TRENING → samouczek. Ten sam box 3:2 (360×200), POD GRAJ, wyśrodkowany.
            // GRAJ: Y∈[-150,+50], TRENING: Y∈[-340,-140] → ~10px odstępu, bez nakładania i bez wchodzenia w tytuł.
            var trainBtn = UIFactory.Button(menu.transform, "TRENING", new Vector2(360, 200), new Vector2(0, -240), new Color(0.20f, 0.46f, 0.78f));
            ApplyButtonSprite(trainBtn, LoadInterfaceSprite("ui_btn_trening.png"));   // grafika CRISISREADY "TRENING" (ukrywa label TMP)
            trainBtn.gameObject.AddComponent<MRCrisisTrainer.UI.UIPulse>();
            var trainLoader = trainBtn.gameObject.AddComponent<MRCrisisTrainer.Core.SceneLoadButton>();
            trainLoader.sceneName = "Tutorial";
            UnityEditor.Events.UnityEventTools.AddPersistentListener(trainBtn.onClick, trainLoader.Load);

            DirectionalLight();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, $"{SceneDir}/MainMenu.unity");
        }

        // ---------------- TUTORIAL (samouczek przed grą) ----------------
        public static void BuildTutorial()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AddManagers();
            UIFactory.EnsureEventSystem();
            var rig = SimpleRig(true);
            var cam = rig.GetComponentInChildren<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);   // passthrough — realny pokój w tle
            EnablePostOnCamera(cam);
            var ptGo = new GameObject("PassthroughController");
            ptGo.AddComponent<MRCrisisTrainer.XR.PassthroughController>();
            DirectionalLight();

            var hands = rig.GetComponent<CompositeHandPoseProvider>();
            var vehicle = AssetDatabase.LoadAssetAtPath<MRCrisisTrainer.Config.VehicleConfig>("Assets/_Project/ScriptableObjects/VehicleConfig.asset");

            // Kierownica do ćwiczenia (ta sama co w grze) przed graczem
            var holder = new GameObject("TutorialHolder");
            holder.transform.position = Vector3.zero;
            holder.AddComponent<MRCrisisTrainer.UI.PlaceWheelInFront>();   // kierownica w zasięgu ręki przed graczem (fix „za daleko")
            // Kierownica JAGUARA (ta sama co w grze) — obracalna; fallback na proceduralną gdyby model się nie wczytał
            Transform wheelSpin; GameObject wheelGO;
            bool jagOk = ActsBuilder.PlaceJaguarCockpit(holder.transform, out wheelSpin, out wheelGO);
            // SAMOUCZEK: pokaż TYLKO kierownicę (kierownica jest osobnym obiektem JagSteeringWheel) — usuń korpus auta,
            // żeby gracz ćwiczył sam obrót koła, a nie „siedział w aucie".
            if (jagOk) { var jagBody = holder.transform.Find("JaguarRoot"); if (jagBody != null) Object.DestroyImmediate(jagBody.gameObject); }
            if (!jagOk || wheelSpin == null) wheelGO = ActsBuilder.BuildSteeringWheelVisual(holder.transform, out wheelSpin);
            var wc = new GameObject("WheelCenter"); wc.transform.SetParent(holder.transform, false);
            wc.transform.position = wheelSpin != null ? wheelSpin.position : wheelGO.transform.position;
            var wheel = wheelGO.AddComponent<MRCrisisTrainer.Acts.Act2.SteeringWheelController>();
            var wso = new SerializedObject(wheel);
            wso.FindProperty("config").objectReferenceValue = vehicle;
            wso.FindProperty("handProviderBehaviour").objectReferenceValue = hands;
            wso.FindProperty("wheelTransform").objectReferenceValue = wheelSpin;
            wso.FindProperty("wheelCenter").objectReferenceValue = wc.transform;
            wso.ApplyModifiedPropertiesWithoutUndo();

            // Mikrofon (ćwiczenie mowy + ciszy)
            var micGo = new GameObject("Mic");
            var mic = micGo.AddComponent<MRCrisisTrainer.Acts.Act3.MicrophoneInputProvider>();

            // Panel z instrukcją + GRAJ (ukryty do zaliczenia treningu)
            var accent = new Color(0.30f, 0.78f, 1f);
            var canvas = UIFactory.WorldCanvas("TutorialCanvas", new Vector3(0, 1.55f, 1.7f), Quaternion.identity, new Vector2(1000, 600), 0.0017f);
            canvas.gameObject.AddComponent<MRCrisisTrainer.UI.PlaceInFrontOfUser>();
            var panel = new GameObject("Panel", typeof(RectTransform));
            panel.transform.SetParent(canvas.transform, false);
            UIFactory.Panel(panel.transform, new Vector2(1012, 612), new Color(accent.r, accent.g, accent.b, 0.5f));
            UIFactory.Panel(panel.transform, new Vector2(1000, 600), new Color(0.04f, 0.06f, 0.09f, 0.93f));
            UIFactory.Panel(panel.transform, new Vector2(1000, 12), accent, new Vector2(0, 294));
            var prompt = UIFactory.Text(panel.transform, "...", 34, new Vector2(920, 380), new Vector2(0, 30),
                TextAlignmentOptions.Center, new Color(0.95f, 0.97f, 1f));
            var playBtn = UIFactory.Button(panel.transform, "GRAJ", new Vector2(380, 130), new Vector2(0, -220), new Color(0.16f, 0.62f, 0.34f));
            var playLbl = playBtn.GetComponentInChildren<TMP_Text>(); if (playLbl != null) playLbl.fontSize = 48;
            playBtn.gameObject.AddComponent<MRCrisisTrainer.UI.UIPulse>();
            playBtn.gameObject.SetActive(false);

            var tcGo = new GameObject("TutorialController");
            var tc = tcGo.AddComponent<MRCrisisTrainer.Gameplay.TutorialController>();
            var tso = new SerializedObject(tc);
            tso.FindProperty("wheel").objectReferenceValue = wheel;
            tso.FindProperty("mic").objectReferenceValue = mic;
            tso.FindProperty("prompt").objectReferenceValue = prompt;
            tso.FindProperty("playButton").objectReferenceValue = playBtn.gameObject;
            tso.FindProperty("nextScene").stringValue = "TrainingRoom";
            tso.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, $"{SceneDir}/Tutorial.unity");
        }

        // ---------------- POST SESSION ----------------
        public static void BuildPostSession()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AddManagers();
            UIFactory.EnsureEventSystem();
            var rig = SimpleRig(true);
            var cam = rig.GetComponentInChildren<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.07f, 0.1f);
            Skybox();

            var canvas = UIFactory.WorldCanvas("PostCanvas", new Vector3(0, 1.6f, 2.2f), Quaternion.identity, new Vector2(1100, 900), 0.0022f);
            canvas.gameObject.AddComponent<MRCrisisTrainer.UI.PlaceInFrontOfUser>();   // ankieta zawsze przed graczem

            // Questionnaire panel
            var qPanel = new GameObject("QuestionnairePanel", typeof(RectTransform));
            qPanel.transform.SetParent(canvas.transform, false);
            UIFactory.Panel(qPanel.transform, new Vector2(1100, 900), new Color(0.05f, 0.06f, 0.1f, 0.97f));
            var title = UIFactory.Text(qPanel.transform, "Ankieta", 38, new Vector2(1040, 80), new Vector2(0, 380));
            var progress = UIFactory.Text(qPanel.transform, "1 / 10", 26, new Vector2(300, 50), new Vector2(380, 380));
            var question = UIFactory.Text(qPanel.transform, "Pytanie...", 30, new Vector2(1000, 220), new Vector2(0, 120));
            var leftA = UIFactory.Text(qPanel.transform, "", 22, new Vector2(300, 50), new Vector2(-380, -60), TextAlignmentOptions.Left, new Color(0.7f, 0.75f, 0.85f));
            var rightA = UIFactory.Text(qPanel.transform, "", 22, new Vector2(300, 50), new Vector2(380, -60), TextAlignmentOptions.Right, new Color(0.7f, 0.75f, 0.85f));

            var btnContainer = new GameObject("ScaleButtons", typeof(RectTransform));
            btnContainer.transform.SetParent(qPanel.transform, false);
            var bcRt = btnContainer.GetComponent<RectTransform>();
            bcRt.sizeDelta = new Vector2(1000, 100);
            bcRt.anchoredPosition = new Vector2(0, -160);
            var hlg = btnContainer.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 12; hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

            // Scale button template (inactive)
            var template = new GameObject("ScaleBtnTemplate", typeof(RectTransform));
            template.transform.SetParent(canvas.transform, false);
            template.SetActive(false);
            var tImg = template.AddComponent<Image>();
            tImg.color = new Color(0.2f, 0.4f, 0.8f); tImg.sprite = UIFactory.WhiteSprite();
            template.GetComponent<RectTransform>().sizeDelta = new Vector2(74, 74);
            template.AddComponent<Button>();
            var tTxt = UIFactory.Text(template.transform, "0", 30, new Vector2(74, 74), Vector2.zero);

            var qController = qPanel.AddComponent<QuestionnaireController>();
            var qso = new SerializedObject(qController);
            qso.FindProperty("titleLabel").objectReferenceValue = title;
            qso.FindProperty("questionLabel").objectReferenceValue = question;
            qso.FindProperty("progressLabel").objectReferenceValue = progress;
            qso.FindProperty("leftAnchorLabel").objectReferenceValue = leftA;
            qso.FindProperty("rightAnchorLabel").objectReferenceValue = rightA;
            qso.FindProperty("buttonContainer").objectReferenceValue = btnContainer.transform;
            qso.FindProperty("scaleButtonPrefab").objectReferenceValue = template;
            qso.ApplyModifiedPropertiesWithoutUndo();

            // Debrief panel
            var debrief = new GameObject("DebriefPanel", typeof(RectTransform));
            debrief.transform.SetParent(canvas.transform, false);
            debrief.SetActive(false);
            UIFactory.Panel(debrief.transform, new Vector2(1100, 900), new Color(0.05f, 0.06f, 0.1f, 0.97f));
            UIFactory.Text(debrief.transform, "Podsumowanie sesji", 44, new Vector2(1040, 100), new Vector2(0, 350));
            var debriefText = UIFactory.Text(debrief.transform, "...", 28, new Vector2(1000, 500), new Vector2(0, -20),
                TextAlignmentOptions.Center, new Color(0.85f, 0.9f, 0.95f));
            var menuBtn = UIFactory.Button(debrief.transform, "Powrót do menu", new Vector2(420, 90), new Vector2(0, -360), new Color(0.25f, 0.4f, 0.7f));
            HookSceneLoad(menuBtn, "MainMenu");

            var post = canvas.gameObject.AddComponent<PostSessionController>();
            var pso = new SerializedObject(post);
            pso.FindProperty("questionnaireUI").objectReferenceValue = qController;
            pso.FindProperty("questionnairePanel").objectReferenceValue = qPanel;
            pso.FindProperty("debriefPanel").objectReferenceValue = debrief;
            pso.FindProperty("debriefText").objectReferenceValue = debriefText;
            var qArr = pso.FindProperty("questionnaires");
            string[] qids = { "nasa_tlx", "sus", "ueq_s", "ssq", "ipq", "sam" };
            qArr.arraySize = qids.Length;
            for (int i = 0; i < qids.Length; i++)
                qArr.GetArrayElementAtIndex(i).objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<QuestionnaireDefinition>($"{QDir}/{qids[i]}.asset");
            pso.ApplyModifiedPropertiesWithoutUndo();

            DirectionalLight();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, $"{SceneDir}/PostSession.unity");
        }

        // ---------------- GAME OVER (koniec gry: osobna CZARNA scena) ----------------
        /// <summary>Buduje scenę „GameOver": w pełni czarne tło (clearFlags=SolidColor, czarny kolor, BEZ skyboxa,
        /// BEZ passthrough), rig VR (headset renderuje + śledzi), oraz world-space napis z wynikiem.
        /// GameOverController czyta PlayerPrefs (end_message / end_is_win) i restartuje na SPUST.
        /// Dorzuca scenę do Build Settings, żeby SceneManager.LoadScene("GameOver") działało w buildzie.</summary>
        public static void BuildGameOver()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AddManagers();
            UIFactory.EnsureEventSystem();
            var rig = SimpleRig(true);   // hands-only laser z palca + retikuła (VRUIPointer) — klikalny przycisk
            var cam = rig.GetComponentInChildren<Camera>();
            // CAŁA scena czarna: brak skyboxa, brak passthrough — tylko czarny solid color.
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;

            // World-space napis PRZED graczem (czarne tło daje sama kamera — bez panelu).
            // WorldCanvas dodaje GraphicRaycaster → VRUIPointer trafia przycisk; PlaceInFrontOfUser ustawia panel prosto na gracza.
            var canvas = UIFactory.WorldCanvas("GameOverCanvas", new Vector3(0, 1.5f, 1.8f), Quaternion.identity,
                new Vector2(1100, 600), 0.0022f);
            canvas.gameObject.AddComponent<MRCrisisTrainer.UI.PlaceInFrontOfUser>();   // zawsze przed graczem

            // Duży, pogrubiony, wyśrodkowany napis z wynikiem (kolor ustawia GameOverController wg wygranej).
            var message = UIFactory.Text(canvas.transform, "KONIEC", 72, new Vector2(1060, 360), new Vector2(0, 130),
                TextAlignmentOptions.Center, new Color(0.30f, 1f, 0.45f));
            message.fontStyle = FontStyles.Bold;
            // Mniejsza podpowiedź: wskaż przycisk dłonią i ściśnij palce (tekst ustawia też GameOverController).
            var hint = UIFactory.Text(canvas.transform, "Wskaż przycisk dłonią i ściśnij palce", 30,
                new Vector2(1060, 80), new Vector2(0, -30), TextAlignmentOptions.Center, new Color(0.85f, 0.88f, 0.95f));

            var controller = canvas.gameObject.AddComponent<GameOverController>();
            var cso = new SerializedObject(controller);
            cso.FindProperty("messageLabel").objectReferenceValue = message;
            cso.FindProperty("hintLabel").objectReferenceValue = hint;
            cso.ApplyModifiedPropertiesWithoutUndo();

            // KLIKALNY przycisk „SPRÓBUJ PONOWNIE" (jak w menu) — celuj dłonią + pinch. Duży, wyśrodkowany, pod podpowiedzią.
            var retryBtn = UIFactory.Button(canvas.transform, "SPRÓBUJ PONOWNIE", new Vector2(560, 150), new Vector2(0, -200),
                new Color(0.16f, 0.62f, 0.34f));
            var retryLbl = retryBtn.GetComponentInChildren<TMP_Text>(); if (retryLbl != null) retryLbl.fontSize = 44;
            retryBtn.gameObject.AddComponent<MRCrisisTrainer.UI.UIPulse>();   // pulsuje jak przyciski menu
            UnityEditor.Events.UnityEventTools.AddPersistentListener(retryBtn.onClick, controller.Retry);   // klik → GameOverController.Retry()

            string path = $"{SceneDir}/GameOver.unity";
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, path);
            EnsureSceneInBuildSettings(path);
        }

        // ---------------- HELPERS ----------------
        /// <summary>Dodaje scenę do EditorBuildSettings.scenes (włączoną), jeśli jeszcze jej tam nie ma —
        /// bez tego SceneManager.LoadScene("GameOver") nie zadziała w zbudowanej grze.</summary>
        private static void EnsureSceneInBuildSettings(string scenePath)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == scenePath)) return;
            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"[MetaSceneBuilder] Dodano do Build Settings: {scenePath}");
        }
        private static void AddManagers()
        {
            var m = new GameObject("[Persistent]");
            m.AddComponent<GameStateManager>();
            m.AddComponent<SceneLoader>();
            m.AddComponent<MRCrisisTrainer.Logging.JSONLLogger>();
            m.AddComponent<SessionManager>();
            m.AddComponent<AudioManager>();
            m.AddComponent<PerformanceMonitor>();
            m.AddComponent<SafeModeController>();
            m.AddComponent<PermissionRequester>();
        }

        private static GameObject SimpleRig(bool withPointer)
        {
            var rig = new GameObject("XR Rig");
            var offset = new GameObject("Camera Offset");
            offset.transform.SetParent(rig.transform, false);
            offset.transform.localPosition = Vector3.zero;
            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(offset.transform, false);
            camGo.GetComponent<Camera>().nearClipPlane = 0.05f;
            XRRigHelper.AddHeadTracking(camGo);
            XRRigHelper.ConfigureXROrigin(rig, offset, camGo.GetComponent<Camera>());
            EnablePostOnCamera(camGo.GetComponent<Camera>());   // kinowy look także w grze na goglach
            MetaPassthroughSetup.EnsureSceneOVRPassthrough(rig, camGo.GetComponent<Camera>());   // MR: realny pokój (build-time OVR layer)

            var xrHands = rig.AddComponent<XRHandsPoseProvider>();
            var ctrls = rig.AddComponent<ControllerPoseProvider>();
            var xhSo2 = new SerializedObject(xrHands);
            xhSo2.FindProperty("trackingOrigin").objectReferenceValue = offset.transform;
            xhSo2.ApplyModifiedPropertiesWithoutUndo();
            var cpSo2 = new SerializedObject(ctrls);
            cpSo2.FindProperty("trackingOrigin").objectReferenceValue = offset.transform;
            cpSo2.ApplyModifiedPropertiesWithoutUndo();
            var comp = rig.AddComponent<CompositeHandPoseProvider>();
            var compSo = new SerializedObject(comp);
            compSo.FindProperty("hands").objectReferenceValue = xrHands;
            compSo.FindProperty("controllers").objectReferenceValue = ctrls;
            compSo.ApplyModifiedPropertiesWithoutUndo();
            var vis = rig.AddComponent<HandVisualizer>();
            var visSo = new SerializedObject(vis);
            visSo.FindProperty("handProviderBehaviour").objectReferenceValue = comp;
            visSo.FindProperty("trackingOrigin").objectReferenceValue = offset;   // ten sam origin co kamera → dłonie dokładnie przy realnych
            visSo.ApplyModifiedPropertiesWithoutUndo();
            if (withPointer)
            {
                var ptr = rig.AddComponent<VRUIPointer>();
                // reticle
                var ret = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                ret.name = "Reticle";
                Object.DestroyImmediate(ret.GetComponent<Collider>());
                ret.transform.localScale = Vector3.one * 0.04f;
                var rmat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
                if (rmat.HasProperty("_BaseColor")) rmat.SetColor("_BaseColor", new Color(1f, 0.9f, 0.2f));
                ret.GetComponent<Renderer>().sharedMaterial = rmat;
                ret.SetActive(false);
                var pso = new SerializedObject(ptr);
                pso.FindProperty("reticlePrefab").objectReferenceValue = ret;
                pso.FindProperty("hands").objectReferenceValue = xrHands;   // laser z palca (hands-only)
                pso.ApplyModifiedPropertiesWithoutUndo();
            }
            AddFirstPersonBody(offset.transform, camGo.transform, comp);   // pierwszoosobowe ciało (widzisz ręce/ramiona/nogi)
            return rig;
        }

        /// <summary>Wstawia pierwszoosobowe ciało (model PlayerAvatar.glb) podążające za głową, z IK ramion do dłoni.</summary>
        internal static void AddFirstPersonBody(Transform camOffset, Transform cam, MonoBehaviour handProvider)
        {
            // WYŁĄCZONE: proceduralne ciało (IK ramion + zgadywana poza siedząca) wygląda źle na goglach
            // (połamane nogi/tułów). Pełne, poprawne ciało zrobimy przez Meta Movement SDK (retargeting).
            // Do tego czasu: tylko śledzone dłonie + passthrough.
            return;
#pragma warning disable 0162
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/External/Avatar/PlayerAvatar.glb");
            if (prefab == null) { Debug.LogWarning("[Body] PlayerAvatar.glb brak — pomijam ciało"); return; }
            var avatar = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            avatar.name = "PlayerBody";   // niezagnieżdżone — FirstPersonAvatar sam ustawia pozycję co klatkę
            var fpa = avatar.AddComponent<MRCrisisTrainer.XR.FirstPersonAvatar>();
            var so = new SerializedObject(fpa);
            so.FindProperty("head").objectReferenceValue = cam;
            so.FindProperty("trackingOrigin").objectReferenceValue = camOffset;
            so.FindProperty("handProviderBehaviour").objectReferenceValue = handProvider;
            so.FindProperty("avatarRoot").objectReferenceValue = avatar.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("[Body] PlayerBody (first-person avatar) dodany");
        }

        private static void DirectionalLight()
        {
            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1f;
            light.transform.rotation = Quaternion.Euler(50, -30, 0);
        }

        /// <summary>Włącza post-processing (global volume URP) + SMAA na kamerze — kinowy look także w grze (nie tylko GIF).</summary>
        private static void EnablePostOnCamera(Camera c)
        {
            if (c == null) return;
            var d = c.GetUniversalAdditionalCameraData();
            if (d != null)
            {
                d.renderPostProcessing = true;
                d.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                d.antialiasingQuality = AntialiasingQuality.High;
            }
        }

        private static void Skybox()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.4f, 0.45f, 0.55f);
        }

        private static void HookButton(Button btn, MonoBehaviour target, string method)
        {
            var m = target.GetType().GetMethod(method);
            if (m == null) return;
            UnityEditor.Events.UnityEventTools.AddPersistentListener(btn.onClick,
                (UnityEngine.Events.UnityAction)System.Delegate.CreateDelegate(typeof(UnityEngine.Events.UnityAction), target, m));
        }

        private static void HookSceneLoad(Button btn, string sceneName)
        {
            var loaderGo = new GameObject($"Load_{sceneName}");
            var helper = loaderGo.AddComponent<SceneLoadButton>();
            helper.sceneName = sceneName;
            UnityEditor.Events.UnityEventTools.AddPersistentListener(btn.onClick, helper.Load);
        }
    }
}
