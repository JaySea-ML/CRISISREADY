using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MRCrisisTrainer.Config;
using MRCrisisTrainer.Core;
using MRCrisisTrainer.Gameplay;
using MRCrisisTrainer.Gameplay.Detectors;
using MRCrisisTrainer.UI;
using MRCrisisTrainer.XR;
using MRCrisisTrainer.Acts.Act2;
using MRCrisisTrainer.Acts.Act3;

namespace MRCrisisTrainer.EditorTools
{
    /// <summary>
    /// Buduje 2 akty (poślizg, telefon/ukrycie) z mikrokrokami i detektorami
    /// w scenie TrainingRoom oraz SessionFlowManager który je sekwencjonuje.
    /// </summary>
    public static class ActsBuilder
    {
        private const string SO = "Assets/_Project/ScriptableObjects";
        private const string ScenDir = SO + "/Scenarios";

        public static void BuildActs(Transform roomRoot, RoomEnvironment env, MonoBehaviour hands)
        {
            var gameplay = new GameObject("Gameplay");
            gameplay.transform.SetParent(roomRoot, false);

            var flow = gameplay.AddComponent<SessionFlowManager>();
            var acts = new List<SessionFlowManager.Act>();

            // Akt I (reanimacja) usunięty z doświadczenia — zostają tylko II i III.
            acts.Add(BuildAct2(gameplay.transform, env, hands));
            acts.Add(BuildAct3(gameplay.transform, env, hands));

            WriteFlow(flow, acts);
        }

        private const string KenNature = "Assets/_Project/External/Kenney/Nature";
        private const string KenCar = "Assets/_Project/External/Kenney/Car";

        // ============ ACT 2: SKID (jazda przez las + poślizg) ============
        private static SessionFlowManager.Act BuildAct2(Transform parent, RoomEnvironment env, MonoBehaviour hands)
        {
            var root = new GameObject("Act2_Skid");
            root.transform.SetParent(parent, false);

            var vehicle = Load<VehicleConfig>($"{SO}/VehicleConfig.asset");
            var scenario = Load<ScenarioConfig>($"{ScenDir}/act2_skid.asset");

            // Materiały — asfalt JAŚNIEJSZY (czytelna droga), jasne linie z emisją, bogata zieleń lasu
            var asphalt = SolidURP("M_Asphalt", new Color(0.26f, 0.26f, 0.28f), 0.18f);
            var marking = SolidURP("M_LaneMark", new Color(0.96f, 0.93f, 0.72f), 0.1f, new Color(0.55f, 0.52f, 0.36f));
            var edge = SolidURP("M_RoadEdge", new Color(0.92f, 0.92f, 0.9f), 0.1f, new Color(0.4f, 0.4f, 0.4f));
            var ground = SolidURP("M_ForestGround", new Color(0.18f, 0.33f, 0.13f), 0.04f);
            var foliage = SolidURP("M_Foliage", new Color(0.11f, 0.30f, 0.10f), 0.04f);
            var trunk = SolidURP("M_Trunk", new Color(0.21f, 0.13f, 0.07f), 0.12f);
            var rock = SolidURP("M_Rock", new Color(0.33f, 0.33f, 0.31f), 0.08f);
            var driveSky = MakeSkybox();

            // Holder snapowany do gracza w runtime przez Act2DriveDirector (lokalne wsp., +Z = przód)
            var holder = new GameObject("DriveHolder"); holder.transform.SetParent(root.transform, false);

            // ===== KOKPIT = hatchback (Łada Samara) — ZAMKNIĘTE auto z dachem + pełne wnętrze, low-poly (~50k) =====
            // Model: przód=+Z (nie obracamy), fotel kierowcy lewy w (-0.38,1.10,0). Chowamy otwartą maskę/silnik,
            // kolorujemy materiały po nazwie (FBX bez tekstur), szyba przezroczysta. Gracza sadzamy dynamicznie do HB_EYE.
            const float HB_EYE = 1.32f;          // wysokość oczu kierowcy nad drogą (podniesione — gracz siedział za nisko)
            // ===== AUTO: Jaguar (model gracza) z kierownicą; fallback do Łady + proceduralnej obręczy =====
            Transform wheelSpin; GameObject wheelGO; Transform wheelCenterT;
            bool jag = PlaceJaguarCockpit(holder.transform, out wheelSpin, out wheelGO);
            if (jag)
            {
                wheelCenterT = wheelGO.transform;
            }
            else
            {
                PlaceHatchBackInterior(holder.transform, HB_EYE);
                BuildCabinShell(holder.transform, HB_EYE);
                wheelGO = BuildSteeringWheelVisual(holder.transform, out wheelSpin);
                var wc0 = new GameObject("WheelCenter"); wc0.transform.SetParent(holder.transform, false);
                wc0.transform.position = wheelGO.transform.position;
                wheelCenterT = wc0.transform;
            }
            var wheel = wheelGO.AddComponent<SteeringWheelController>();
            var wso = new SerializedObject(wheel);
            wso.FindProperty("config").objectReferenceValue = vehicle;
            wso.FindProperty("handProviderBehaviour").objectReferenceValue = hands;
            wso.FindProperty("wheelTransform").objectReferenceValue = wheelSpin;          // obraca się wizualnie
            wso.FindProperty("wheelCenter").objectReferenceValue = wheelCenterT;
            wso.ApplyModifiedPropertiesWithoutUndo();

            // ===== LAS + DROGA (przewijany świat) =====
            var forestWorld = new GameObject("ForestWorld"); forestWorld.transform.SetParent(holder.transform, false);
            forestWorld.transform.localPosition = new Vector3(-1.75f, 0f, 0f);   // gracz jedzie PRAWYM pasem (prawy pas wypada pod graczem), auta z naprzeciwka po lewej
            string[] trees = { "tree_pineTallA", "tree_pineTallB", "tree_pineTallC", "tree_pineTallD", "tree_pineDefaultA", "tree_pineDefaultB", "tree_pineRoundA", "tree_pineRoundC", "tree_detailed", "tree_oak", "tree_fat", "tree_tall", "tree_default", "tree_simple" };
            string[] bushes = { "plant_bushDetailed", "plant_bushLarge", "grass_large", "rock_smallA", "rock_smallB", "rock_smallC", "stump_round", "tree_pineSmallA", "tree_pineSmallB", "log" };
            string[] cars = { "sedan", "suv", "hatchback-sports", "truck", "police" };
            const int segCount = 6; const float segLen = 30f;
            var segTransforms = new Transform[segCount];
            for (int i = 0; i < segCount; i++)
                segTransforms[i] = BuildForestSegment(forestWorld.transform, $"Seg{i}", i * segLen - segLen, segLen, asphalt, marking, edge, ground, foliage, trunk, rock, trees, bushes, cars);

            var scroller = forestWorld.AddComponent<ForestScroller>();
            var scSo = new SerializedObject(scroller);
            scSo.FindProperty("segmentLength").floatValue = segLen;
            var segArr = scSo.FindProperty("segments"); segArr.arraySize = segCount;
            for (int i = 0; i < segCount; i++) segArr.GetArrayElementAtIndex(i).objectReferenceValue = segTransforms[i];
            scSo.ApplyModifiedPropertiesWithoutUndo();

            // Windshield (obrót świata przy poślizgu)
            var windshieldGo = new GameObject("Windshield"); windshieldGo.transform.SetParent(holder.transform, false);
            var windshield = windshieldGo.AddComponent<WindshieldView>();
            var winSo = new SerializedObject(windshield);
            winSo.FindProperty("worldRoot").objectReferenceValue = forestWorld.transform;
            winSo.ApplyModifiedPropertiesWithoutUndo();

            // Słońce
            var sunGo = new GameObject("DriveSun"); sunGo.transform.SetParent(holder.transform, false);
            sunGo.transform.localRotation = Quaternion.Euler(50, 30, 0);
            var sun = sunGo.AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.1f; sun.color = new Color(1f, 0.97f, 0.9f);

            // ===== Telefon na siedzeniu PASAŻERA (prawo, zasięg ręki) — dzwoni po poślizgu; odebranie = most do Aktu III =====
            // Dziecko holdera (kokpitu), więc jedzie z autem i jest stałe względem kierowcy (NIE rusza się z lasem).
            var seatPhone = new GameObject("SeatPhone"); seatPhone.transform.SetParent(holder.transform, false);
            seatPhone.transform.localPosition = new Vector3(0.38f, 0.30f, 0.18f);   // NIŻEJ — leży na siedzeniu pasażera, w łatwym zasięgu ręki
            seatPhone.transform.localRotation = Quaternion.identity;   // BEZ obrotu rodzica (obrót rodzica psuł pozycjonowanie Act3Glb); kąt modelu daje yaw poniżej
            if (Act3Glb("Assets/_Project/External/Act3/Telephone.glb", seatPhone.transform, 0.20f, seatPhone.transform.position, -25f, out _, true) == null)   // dropFlatBackdrops=TRUE: odrzuć płaskie tła Sketchfaba → pokaż SAM telefon (retro), nie ściany sceny
            {
                var pb = GameObject.CreatePrimitive(PrimitiveType.Cube); pb.transform.SetParent(seatPhone.transform, false);
                pb.transform.localPosition = Vector3.zero; pb.transform.localScale = new Vector3(0.14f, 0.05f, 0.16f);
                Object.DestroyImmediate(pb.GetComponent<Collider>()); pb.GetComponent<Renderer>().sharedMaterial = LabMaterials.Metal;
            }
            var seatReceiver = new GameObject("Receiver"); seatReceiver.transform.SetParent(seatPhone.transform, false);
            seatReceiver.transform.localPosition = new Vector3(0f, 0.12f, 0f);   // punkt chwytu przy słuchawce modelu (telefon ma WŁASNĄ słuchawkę — bez dublowania)
            var seatRingLightGo = new GameObject("RingLight"); seatRingLightGo.transform.SetParent(seatPhone.transform, false);
            seatRingLightGo.transform.localPosition = new Vector3(0f, 0.22f, 0f);
            var seatRingLight = seatRingLightGo.AddComponent<Light>();
            seatRingLight.type = LightType.Point; seatRingLight.color = new Color(0.3f, 0.85f, 1f); seatRingLight.range = 1.3f; seatRingLight.enabled = false;
            var seatPhoneRing = seatPhone.AddComponent<PhoneRingController>();
            var sprSo = new SerializedObject(seatPhoneRing);
            sprSo.FindProperty("ringClip").objectReferenceValue = LoadClip("phone_ring");
            sprSo.FindProperty("indicatorLight").objectReferenceValue = seatRingLight;
            sprSo.ApplyModifiedPropertiesWithoutUndo();
            seatPhone.SetActive(false);   // telefon POJAWIA SIĘ dopiero PO poślizgach (aktywowany na kroku 'resume'), nie od startu

            // ===== Detektory (mikrokroki) =====
            var dRoot = new GameObject("Detectors"); dRoot.transform.SetParent(root.transform, false);
            // 2-MINUTOWY POŚLIZG W OBIE STRONY (timeouty same przesuwają krok → akt nigdy się nie zacina):
            // spokojna jazda → poślizg w PRAWO → recovery → jazda → poślizg w LEWO → recovery → powrót do jazdy.
            AddTimed(dRoot.transform, "drive_calm", 16f);
            AddTimed(dRoot.transform, "recognize_skid", 2.5f);
            var gripDet = AddGrab(dRoot.transform, "grip_wheel", hands, wheelGO.transform, 0.65f);   // hojny zasięg chwytu kierownicy
            var gripSo = new SerializedObject(gripDet);
            gripSo.FindProperty("maxSeconds").floatValue = 6f;   // awaryjnie zalicz chwyt po 6 s → recovery poślizgu ZAWSZE rusza (koniec „auto stoi na ukos")
            gripSo.ApplyModifiedPropertiesWithoutUndo();
            var steeringDet = AddSteering(dRoot.transform, "counter_steer", vehicle, wheel, windshield, +1f);   // poślizg w PRAWO
            AddTimed(dRoot.transform, "stabilize", 3f);
            AddTimed(dRoot.transform, "drive_between", 30f);
            AddTimed(dRoot.transform, "recognize_skid_l", 2.5f);
            AddSteering(dRoot.transform, "counter_steer_l", vehicle, wheel, windshield, -1f);   // poślizg w LEWO
            AddTimed(dRoot.transform, "stabilize_l", 3f);
            AddTimed(dRoot.transform, "resume", 12f);
            // KONIEC AKTU II = WYPADEK: po wyjściach z poślizgu z naprzeciwka wjeżdża CIĘŻARÓWKA (reżyser odgrywa
            // na kroku 'crash'). Krok czasowy ~6 s pozwala odegrać najazd + huk + czerń, po czym scenariusz kończy się.
            AddTimed(dRoot.transform, "crash", 6f);
            // Ciężarówka (model gracza), na razie UKRYTA; reżyser „rzuca" ją na gracza w wypadku (najazd z naprzeciwka).
            GameObject crashTruck = null;
            var truckPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/External/Truck/Renault/RenaultTruck.fbx");
            if (truckPrefab != null)
            {
                crashTruck = (GameObject)PrefabUtility.InstantiatePrefab(truckPrefab);
                crashTruck.name = "CrashTruck"; crashTruck.transform.SetParent(holder.transform, false);
                // Renault = POJEDYNCZA ciężarówka (bez mostu/duplikatu) → nic nie wycinamy. Ujednolicamy shadery na URP/Lit
                // i zachowujemy albedo (FBX bywa importowany jako Standard → różowy w URP).
                var urpLitT = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                const string tdir = "Assets/_Project/External/Truck/Renault/textures/";
                foreach (var r in crashTruck.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = r.sharedMaterials;
                    for (int mi = 0; mi < mats.Length; mi++)
                    {
                        var src = mats[mi]; if (src == null) continue;
                        string mn = (src.name ?? "").Replace(" (Instance)", "").Trim();
                        Texture2D alb = LoadTruckAlbedo(tdir, mn);
                        if (alb == null) alb = AssetDatabase.LoadAssetAtPath<Texture2D>($"{tdir}Carrosserie_albedo.jpeg");   // fallback = karoseria (nie biały)
                        // NOWY materiał przypisany do renderera → ZAPISYWANY ZE SCENĄ, więc tekstura PERSYSTUJE w buildzie.
                        // (Modyfikacja materiału-sub-assetu FBX NIE przetrwała buildu → stąd biały truck.)
                        var nm = new Material(urpLitT) { name = "M_Truck_" + mn };
                        if (alb != null)
                        {
                            if (nm.HasProperty("_BaseMap")) nm.SetTexture("_BaseMap", alb);
                            if (nm.HasProperty("_MainTex")) nm.SetTexture("_MainTex", alb);
                        }
                        mats[mi] = nm;
                        Debug.Log($"[Act2] Truck mat '{src.name}' → {(alb != null ? alb.name : "BRAK")}");
                    }
                    r.sharedMaterials = mats;
                }
                Bounds tb = default; bool tf = true;
                foreach (var r in crashTruck.GetComponentsInChildren<Renderer>(true)) { if (tf) { tb = r.bounds; tf = false; } else tb.Encapsulate(r.bounds); }
                float longest = tf ? 8f : Mathf.Max(tb.size.x, Mathf.Max(tb.size.y, tb.size.z));
                float ts = longest > 0.01f ? 15f / longest : 1.2f;   // ~15 m — duża, ale nie przesadzona; koła na jezdni (offset w CrashSequence)
                crashTruck.transform.localScale = Vector3.one * ts;
                crashTruck.SetActive(false);
                Debug.Log($"[Act2] CrashTruck Renault longest={longest:F2} scale={ts:F3}");
            }
            else Debug.LogWarning("[Act2] Renault truck FBX nie znaleziony — wypadek bez modelu (sam huk + czerń).");

            var runner = root.AddComponent<ScenarioRunner>();
            SetScenario(runner, scenario);
            BuildHUD(holder.transform, runner, new Vector3(0, 0.78f, 1.95f), true);   // attachCamera: HUD przyczepiony do kamery, na dole, pionowo

            // ===== Reżyser jazdy =====
            var director = root.AddComponent<Act2DriveDirector>();
            var dso = new SerializedObject(director);
            dso.FindProperty("runner").objectReferenceValue = runner;
            dso.FindProperty("holder").objectReferenceValue = holder;
            dso.FindProperty("forest").objectReferenceValue = scroller;
            dso.FindProperty("steeringWheel").objectReferenceValue = wheel;   // boczne sterowanie autem kierownicą
            dso.FindProperty("windshield").objectReferenceValue = windshield;
            dso.FindProperty("sunLight").objectReferenceValue = sun;
            dso.FindProperty("driveSky").objectReferenceValue = driveSky;
            dso.FindProperty("targetEyeHeight").floatValue = HB_EYE;
            dso.FindProperty("skidPreviewAngle").floatValue = vehicle != null ? vehicle.initialSkidAngleDeg : 20f;
            dso.FindProperty("seatPhone").objectReferenceValue = seatPhoneRing;   // telefon na siedzeniu — reżyser dzwoni nim po poślizgu
            dso.FindProperty("truck").objectReferenceValue = crashTruck;          // ciężarówka do wypadku (krok 'crash')
            dso.ApplyModifiedPropertiesWithoutUndo();

            // ===== Trener poślizgu (duże podpowiedzi kierunkowe, kolor wg poprawności) =====
            if (vehicle != null) { vehicle.steeringFullLockDeg = vehicle.steeringFullLockDeg > 1f ? vehicle.steeringFullLockDeg : 95f; EditorUtility.SetDirty(vehicle); }
            float skidDir = Mathf.Sign(vehicle != null && vehicle.initialSkidAngleDeg != 0 ? vehicle.initialSkidAngleDeg : 1f);
            var coachPos = new Vector3(0f, 1.08f, 1.8f);   // BLISKO gracza, na dole, bez tła (czytelne w poślizgu)
            var coachCanvas = UIFactory.WorldCanvas("SkidCoach", coachPos, Quaternion.identity, new Vector2(940, 240), 0.0016f);
            coachCanvas.transform.SetParent(holder.transform, false);
            coachCanvas.transform.localPosition = coachPos;
            // BEZ TŁA (życzenie użytkownika) — same duże napisy
            var coachBig = UIFactory.Text(coachCanvas.transform, "", 66, new Vector2(900, 140), new Vector2(0, 40), TextAlignmentOptions.Center, Color.white);
            var coachSub = UIFactory.Text(coachCanvas.transform, "", 30, new Vector2(900, 70), new Vector2(0, -72), TextAlignmentOptions.Center, Color.white);
            coachCanvas.gameObject.AddComponent<MRCrisisTrainer.UI.FaceCamera>();
            var coachHud = coachCanvas.gameObject.AddComponent<MRCrisisTrainer.UI.AttachToCameraHUD>();
            coachHud.localOffset = new Vector3(0f, -0.20f, 1.15f);   // przyczep do kamery: na dole, blisko, PIONOWO (koniec napisów na drodze)
            var coach = coachCanvas.gameObject.AddComponent<Act2SkidCoach>();
            var cso = new SerializedObject(coach);
            cso.FindProperty("runner").objectReferenceValue = runner;
            cso.FindProperty("recovery").objectReferenceValue = steeringDet;
            cso.FindProperty("bigPrompt").objectReferenceValue = coachBig;
            cso.FindProperty("subPrompt").objectReferenceValue = coachSub;
            cso.FindProperty("skidDirection").floatValue = skidDir;
            cso.ApplyModifiedPropertiesWithoutUndo();
            coachCanvas.gameObject.SetActive(false);   // PODPOWIEDZI Aktu II (poślizg) WYŁĄCZONE — życzenie użytkownika

            return Act("Akt II - Poślizg (jazda przez las)", root, runner, 7f, "");   // introDelay 7 s: auto pojawia się ~3 s (szybkie wejście), poślizg ~7 s — czyli gdy już jedziesz, ale nie za długo czekasz
        }

        // ===== ACT 2 HELPERS =====
        private static Transform BuildForestSegment(Transform parent, string name, float localZ, float len,
            Material asphalt, Material marking, Material edge, Material ground, Material foliage, Material trunk, Material rock,
            string[] trees, string[] bushes, string[] cars)
        {
            var seg = new GameObject(name); seg.transform.SetParent(parent, false);
            seg.transform.localPosition = new Vector3(0, 0, localZ);

            // Jezdnia (jaśniejszy asfalt) + szerokie zielone pobocza
            LBox(seg.transform, "Road", new Vector3(0, 0.0f, len * 0.5f), new Vector3(7f, 0.1f, len), asphalt);
            LBox(seg.transform, "GroundL", new Vector3(-16f, -0.06f, len * 0.5f), new Vector3(26f, 0.08f, len), ground);
            LBox(seg.transform, "GroundR", new Vector3(16f, -0.06f, len * 0.5f), new Vector3(26f, 0.08f, len), ground);

            // Ciągłe linie krawędziowe — czytelny sygnał „to jest droga"
            LBox(seg.transform, "EdgeL", new Vector3(-3.15f, 0.07f, len * 0.5f), new Vector3(0.16f, 0.02f, len), edge);
            LBox(seg.transform, "EdgeR", new Vector3(3.15f, 0.07f, len * 0.5f), new Vector3(0.16f, 0.02f, len), edge);

            // Przerywana linia środkowa — większa i jaśniejsza (czytelny ruch przy przewijaniu)
            int dashes = Mathf.RoundToInt(len / 4f);
            for (int d = 0; d < dashes; d++)
                LBox(seg.transform, $"Dash{d}", new Vector3(0, 0.07f, 2f + d * 4f), new Vector3(0.22f, 0.02f, 2.2f), marking);

            // Gęsty las po obu stronach — ściana drzew (krawędź + głębia)
            for (int s = 0; s < 2; s++)
            {
                float sign = s == 0 ? -1f : 1f;
                for (int t = 0; t < 13; t++)
                {
                    string tn = trees[Random.Range(0, trees.Length)];
                    float dist = Random.Range(4.6f, 18f);
                    float sc = Random.Range(3.0f, 6.5f);
                    Kenney(KenNature, tn, seg.transform,
                        new Vector3(sign * dist, 0, Random.Range(0.5f, len - 0.5f)),
                        sc, Random.Range(0f, 360f), foliage, trunk);
                }
                // Podszycie tuż przy krawędzi jezdni: krzaki, trawa, kamienie, pniaki
                for (int u = 0; u < 4; u++)
                {
                    string bn = bushes[Random.Range(0, bushes.Length)];
                    bool isRock = bn.StartsWith("rock") || bn.StartsWith("stump") || bn.StartsWith("log");
                    Kenney(KenNature, bn, seg.transform,
                        new Vector3(sign * Random.Range(3.7f, 6f), 0, Random.Range(1f, len - 1f)),
                        Random.Range(1.0f, 2.3f), Random.Range(0f, 360f),
                        isRock ? rock : foliage, isRock ? rock : trunk);
                }
            }

            // (USUNIĘTO auta przydrożne i nadjeżdżające — droga ma być PUSTA; jedyny pojazd z naprzeciwka to
            //  CIĘŻARÓWKA, która wjeżdża w gracza w finale Aktu II. Zostaje tylko auto gracza (Jaguar).)
            return seg.transform;
        }

        /// <summary>Wstawia hatchback (FBX): przód +Z, sadza fotel kierowcy (-0.38,1.10,0) tak, by oczy gracza
        /// były w (0,targetEye,0); chowa otwartą maskę/silnik/zderzaki; koloruje materiały; zwraca uchwyt kierownicy.</summary>
        private static Transform PlaceHatchBackInterior(Transform parent, float targetEye)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/External/HatchBack/HatchBack.fbx");
            var carRoot = new GameObject("CarRoot"); carRoot.transform.SetParent(parent, false);
            if (prefab == null) { Debug.LogWarning("[ActsBuilder] HatchBack.fbx missing"); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = "HatchBack"; go.transform.SetParent(carRoot.transform, false);
            go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity; go.transform.localScale = Vector3.one;

            // Pozycja: kierowca modelu (-0.38,1.10,0) -> oczy gracza (0,targetEye,0). Przód już +Z (bez obrotu).
            carRoot.transform.localRotation = Quaternion.identity;
            carRoot.transform.localPosition = new Vector3(0.38f, targetEye - 1.10f, 0f);

            // Ukryj otwartą maskę, silnik, zderzaki, reflektory, podwozie-śmieci (blokowały widok / "rozłożony" model)
            var hide = new HashSet<string> { "bonnet_ok", "dviga", "chassis", "bump_front_ok", "bump_rear_ok",
                "per_fari", "Plane", "stekla_far", "fari_zad", "extra1", "Boot", "brizgov", "podveska", "WheelRide" };
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
                if (hide.Contains(r.name)) r.gameObject.SetActive(false);

            ColorHatchBack(go);

            // Uchwyt kierownicy w środku mesha "WheelRide" (po ustawieniu pozycji auta -> world = poprawne miejsce)
            Transform anchor = null;
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
                if (r.name == "WheelRide")
                {
                    var a = new GameObject("WheelAnchor"); a.transform.SetParent(carRoot.transform, true);
                    a.transform.position = r.bounds.center;
                    anchor = a.transform; break;
                }
            return anchor;
        }

        /// <summary>Auto JAGUAR (model gracza, jak w referencji): ładuje Jaguar.fbx, sadza kierowcę przy graczu,
        /// znajduje mesh kierownicy ("weel"), podpina go pod oś obrotu zwróconą do gracza. Zwraca wheelSpin (obraca się
        /// wizualnie) i wheelGO (środek chwytu). false = brak modelu → fallback do proceduralnej kabiny.</summary>
        internal static bool PlaceJaguarCockpit(Transform holder, out Transform wheelSpin, out GameObject wheelGO)
        {
            wheelSpin = null; wheelGO = null;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/External/CarInterior/Jaguar.fbx");
            if (prefab == null) { Debug.LogWarning("[Jag] Jaguar.fbx brak — fallback do proceduralnej kabiny"); return false; }
            const float SCALE = 0.5f, EYE = 1.32f;
            Vector3 driverEyeInCar = new Vector3(-0.47f, 0.95f, -0.15f);   // pozycja oczu kierowcy w modelu
            var root = new GameObject("JaguarRoot"); root.transform.SetParent(holder, false);
            var car = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            car.name = "JaguarInterior"; car.transform.SetParent(root.transform, false);
            // ROZPAKUJ prefab instance — inaczej Unity blokuje reparent mesh'a koła ("weel") na oś obrotu (bug kierownicy)
            PrefabUtility.UnpackPrefabInstance(car, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            car.transform.localPosition = Vector3.zero;
            car.transform.localRotation = Quaternion.Euler(0f, 270f, 0f);   // przód = +Z
            car.transform.localScale = Vector3.one * SCALE;
            Bounds b = CarBounds(car);
            car.transform.localPosition = new Vector3(-b.center.x, -b.min.y, -b.center.z);   // wyśrodkuj XZ, postaw na podłodze
            ApplyJaguarMaterials(car);
            root.transform.localPosition = new Vector3(-driverEyeInCar.x, EYE - driverEyeInCar.y, -driverEyeInCar.z);
            Transform weel = null;
            foreach (var r in car.GetComponentsInChildren<Renderer>())
                if (r.name.ToLower().Contains("weel") || r.name.ToLower().Contains("wheel")) { weel = r.transform; break; }
            if (weel == null) { Debug.LogWarning("[Jag] mesh kierownicy nie znaleziony"); wheelGO = car; return true; }
            Vector3 wheelCenterWorld = weel.GetComponent<Renderer>().bounds.center;
            Vector3 driverWorld = holder.TransformPoint(new Vector3(0f, EYE, 0f));
            Vector3 normal = driverWorld - wheelCenterWorld;
            normal = normal.sqrMagnitude < 0.0001f ? Vector3.back : normal.normalized;
            var spinParent = new GameObject("JagSteeringWheel");
            spinParent.transform.SetParent(holder, true);
            spinParent.transform.position = wheelCenterWorld;
            spinParent.transform.rotation = Quaternion.LookRotation(normal, Vector3.up);   // oś koła zwrócona do gracza
            var spinChild = new GameObject("JagWheelSpin");
            spinChild.transform.SetParent(spinParent.transform, false);
            spinChild.transform.localPosition = Vector3.zero; spinChild.transform.localRotation = Quaternion.identity;
            weel.SetParent(spinChild.transform, true);   // mesh kierownicy obraca się ze spinChild
            wheelSpin = spinChild.transform; wheelGO = spinParent;
            Debug.Log($"[Jag] kokpit ustawiony, wheel@{wheelCenterWorld}");
            return true;
        }

        private static void ApplyJaguarMaterials(GameObject car)
        {
            var body = SolidURP("M_JagBody", new Color(0.16f, 0.13f, 0.11f), 0.32f);    // ciemna skóra/wnętrze
            var wheelM = SolidURP("M_JagWheel", new Color(0.30f, 0.29f, 0.28f), 0.45f); // jaśniejsza — widać obrót koła
            foreach (var r in car.GetComponentsInChildren<Renderer>(true))
            {
                bool w = r.name.ToLower().Contains("weel") || r.name.ToLower().Contains("wheel");
                int n = Mathf.Max(1, r.sharedMaterials.Length);
                var mats = new Material[n];
                for (int i = 0; i < n; i++) mats[i] = w ? wheelM : body;
                r.sharedMaterials = mats;
            }
        }

        private static Bounds CarBounds(GameObject go)
        {
            var rends = go.GetComponentsInChildren<Renderer>(true);
            bool first = true; Bounds b = default;
            foreach (var r in rends) { if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); }
            return b;
        }

        /// <summary>Buduje czystą, wyraźną kierownicę (obręcz=torus + piasta + 3 szprychy) na STAŁEJ wygodnej pozycji
        /// przed graczem (lokalnie w holderze). Niezależna od mesha auta → zawsze widoczna i w zasięgu rąk.
        /// Zwraca root (na nim siada SteeringWheelController), a w out — dziecko „WheelSpin", które się obraca.</summary>
        internal static GameObject BuildSteeringWheelVisual(Transform holder, out Transform wheelSpin)
        {
            var root = new GameObject("SteeringWheel");
            root.transform.SetParent(holder, false);
            root.transform.localPosition = new Vector3(0f, 0.92f, 0.45f);   // przed graczem, na wysokości rąk
            root.transform.localRotation = Quaternion.Euler(22f, 0f, 0f);   // lekki przechył jak prawdziwa kierownica (nie leży płasko!)

            var rim = SolidURP("M_WheelRim", new Color(0.06f, 0.06f, 0.07f), 0.35f);

            var spin = new GameObject("WheelSpin"); spin.transform.SetParent(root.transform, false);
            wheelSpin = spin.transform;

            var rimGo = new GameObject("Rim"); rimGo.transform.SetParent(spin.transform, false);
            rimGo.AddComponent<MeshFilter>().sharedMesh = BuildTorusMesh(0.17f, 0.023f, 32, 12);
            rimGo.AddComponent<MeshRenderer>().sharedMaterial = rim;

            var hub = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            var hc = hub.GetComponent<Collider>(); if (hc != null) Object.DestroyImmediate(hc);
            hub.name = "Hub"; hub.transform.SetParent(spin.transform, false);
            hub.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            hub.transform.localScale = new Vector3(0.075f, 0.012f, 0.075f);
            hub.GetComponent<MeshRenderer>().sharedMaterial = rim;

            for (int i = 0; i < 3; i++)
            {
                var sp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                var sc = sp.GetComponent<Collider>(); if (sc != null) Object.DestroyImmediate(sc);
                sp.name = $"Spoke{i}"; sp.transform.SetParent(spin.transform, false);
                sp.transform.localRotation = Quaternion.Euler(0f, 0f, i * 120f);
                sp.transform.localPosition = sp.transform.localRotation * new Vector3(0.085f, 0f, 0f);
                sp.transform.localScale = new Vector3(0.16f, 0.02f, 0.014f);
                sp.GetComponent<MeshRenderer>().sharedMaterial = rim;
            }
            return root;
        }

        /// <summary>Generuje siatkę torusa w płaszczyźnie XY (oś = Z). majorR = promień obręczy, minorR = grubość.</summary>
        private static Mesh BuildTorusMesh(float majorR, float minorR, int majorSeg, int minorSeg)
        {
            var verts = new List<Vector3>(); var tris = new List<int>();
            for (int i = 0; i <= majorSeg; i++)
            {
                float u = i / (float)majorSeg * Mathf.PI * 2f;
                var center = new Vector3(Mathf.Cos(u) * majorR, Mathf.Sin(u) * majorR, 0f);
                var radial = new Vector3(Mathf.Cos(u), Mathf.Sin(u), 0f);
                for (int j = 0; j <= minorSeg; j++)
                {
                    float v = j / (float)minorSeg * Mathf.PI * 2f;
                    var dir = radial * Mathf.Cos(v) + Vector3.forward * Mathf.Sin(v);
                    verts.Add(center + dir * minorR);
                }
            }
            int strip = minorSeg + 1;
            for (int i = 0; i < majorSeg; i++)
                for (int j = 0; j < minorSeg; j++)
                {
                    int a = i * strip + j, b = a + strip, c = a + 1, d = b + 1;
                    tris.Add(a); tris.Add(b); tris.Add(c);
                    tris.Add(c); tris.Add(b); tris.Add(d);
                }
            var m = new Mesh { name = "WheelTorus" };
            m.SetVertices(verts); m.SetTriangles(tris, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        /// <summary>Buduje ciemną kabinę wokół gracza: DACH nad głową, słupki, boki, tylna ściana i ramka szyby.
        /// Prymitywy (Cube/LBox) mają wszystkie ścianki, więc dach jest widoczny OD ŚRODKA (model bywa jednostronny).
        /// Przednia szyba (przód-góra) zostaje OTWARTA, by widzieć drogę.</summary>
        private static void BuildCabinShell(Transform holder, float eye)
        {
            var darkIn = SolidURP("M_CabinDark", new Color(0.17f, 0.17f, 0.19f), 0.22f);   // czytelne wnętrze (nie czarna dziura)
            // miękkie ciepłe światło wnętrza kabiny — deska i kierownica są widoczne (nie toną w mroku)
            var inLightGo = new GameObject("Cabin_Light"); inLightGo.transform.SetParent(holder, false);
            inLightGo.transform.localPosition = new Vector3(0f, eye + 0.32f, 0.15f);
            var inLight = inLightGo.AddComponent<Light>();
            inLight.type = LightType.Point; inLight.color = new Color(1f, 0.95f, 0.86f);
            inLight.range = 2.4f; inLight.intensity = 1.5f;
            var roofY = eye + 0.46f;
            LBox(holder, "Cabin_Roof",     new Vector3(0f, roofY,        0.05f), new Vector3(1.55f, 0.06f, 2.0f), darkIn);   // dach nad głową
            LBox(holder, "Cabin_Rear",     new Vector3(0f, eye + 0.02f, -0.62f), new Vector3(1.5f, 1.05f, 0.06f), darkIn);   // ściana za plecami
            LBox(holder, "Cabin_SideL",    new Vector3(-0.74f, eye + 0.18f, 0.0f), new Vector3(0.06f, 0.7f, 1.85f), darkIn);  // bok lewy (góra drzwi)
            LBox(holder, "Cabin_SideR",    new Vector3( 0.74f, eye + 0.18f, 0.0f), new Vector3(0.06f, 0.7f, 1.85f), darkIn);  // bok prawy
            LBox(holder, "Cabin_Header",   new Vector3(0f, roofY - 0.06f, 0.92f), new Vector3(1.45f, 0.12f, 0.06f), darkIn);  // ramka nad przednią szybą
            LBox(holder, "Cabin_PillarL",  new Vector3(-0.66f, eye + 0.2f, 0.86f), new Vector3(0.07f, 0.66f, 0.08f), darkIn); // słupek A lewy
            LBox(holder, "Cabin_PillarR",  new Vector3( 0.66f, eye + 0.2f, 0.86f), new Vector3(0.07f, 0.66f, 0.08f), darkIn); // słupek A prawy
        }

        /// <summary>Koloruje hatchback po NAZWACH materiałów (FBX bez tekstur): nadwozie czerwone, wnętrze ciemne,
        /// fotele, opony/kierownica czarne, chrom, a SZYBY przezroczyste (żeby widzieć drogę przez przednią szybę).</summary>
        private static void ColorHatchBack(GameObject go)
        {
            var body = SolidURP("M_HB_Body", new Color(0.52f, 0.06f, 0.07f), 0.5f);
            var dark = SolidURP("M_HB_Dark", new Color(0.07f, 0.07f, 0.08f), 0.25f);
            var seat = SolidURP("M_HB_Seat", new Color(0.14f, 0.14f, 0.16f), 0.15f);
            var black = SolidURP("M_HB_Black", new Color(0.03f, 0.03f, 0.03f), 0.2f);
            var chrome = SolidURP("M_HB_Chrome", new Color(0.6f, 0.6f, 0.62f), 0.85f);
            var glass = GlassURP("M_HB_Glass");

            Material Pick(string raw)
            {
                string n = (raw ?? "").ToLower();
                if (n.Contains("glass") || n.Contains("stekl")) return glass;
                if (n.Contains("chrome") || n.Contains("zerkalo")) return chrome;
                if (n.Contains("sideny")) return seat;
                if (n.Contains("rezina") || n.Contains("wheel") || n.Contains("baraban") || n.Contains("kover") || n.Contains("ruchka")) return black;
                if (n.Contains("carcolor") || n.Contains("extertxt") || n.Contains("number") || n.Contains("lampos") || n.Contains("fari")) return body;
                return dark; // torpeda, priborka, plastik, obshivka, krisha, dno, motor, radiator, music...
            }
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                var src = r.sharedMaterials; var dst = new Material[src.Length];
                for (int i = 0; i < src.Length; i++) dst[i] = Pick(src[i] != null ? src[i].name : "");
                r.sharedMaterials = dst;
            }
        }

        /// <summary>Przezroczysty materiał URP (szyby).</summary>
        private static Material GlassURP(string name)
        {
            const string dir = "Assets/_Project/Materials/Act2"; EnsureFolder(dir);
            string path = $"{dir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                m = new Material(sh); AssetDatabase.CreateAsset(m, path);
            }
            m.SetFloat("_Surface", 1f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = 3000;
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            var c = new Color(0.55f, 0.65f, 0.78f, 0.14f);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c); else m.color = c;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.9f);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Wstawia McLarena (GLB z glTFast, fallback FBX): recenter brył do origin carRoot,
        /// potem obrót + pozycja kokpitu. Zachowuje oryginalne materiały PBR.</summary>
        private static Transform PlaceMcLarenInterior(Transform parent, Vector3 carRootPos, Vector3 carRootEuler)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/External/McLaren/McLaren.glb")
                      ?? AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/External/McLaren/McLaren.fbx");
            var carRoot = new GameObject("CarRoot"); carRoot.transform.SetParent(parent, false);
            if (prefab == null) { Debug.LogWarning("[ActsBuilder] McLaren model missing (glb/fbx)"); return carRoot.transform; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = "McLaren";
            go.transform.SetParent(carRoot.transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            // recenter: środek brył -> origin carRoot (carRoot wciąż bez obrotu)
            Bounds b = default; bool first = true;
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
            { if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); }
            go.transform.localPosition = -carRoot.transform.InverseTransformPoint(b.center);
            // dopiero teraz obrót + pozycja kokpitu (dziecko obraca się razem, zostaje wyśrodkowane)
            carRoot.transform.localRotation = Quaternion.Euler(carRootEuler);
            carRoot.transform.localPosition = carRootPos;
            return carRoot.transform;
        }

        /// <summary>Wstawia wnętrze Jaguara, koloruje (ciemne wnętrze + ciemna kierownica), zwraca transform kierownicy "weel".</summary>
        private static Transform PlaceJaguarInterior(Transform parent, Vector3 pos, Vector3 euler, float scale, Material mat, Material wheelMat)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/External/CarInterior/Jaguar.fbx");
            if (prefab == null) { Debug.LogWarning("[ActsBuilder] Jaguar.fbx missing"); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = "JaguarInterior";
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = Vector3.one * scale;
            Transform wheel = null;
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
            {
                bool isWheel = r.name.ToLower().Contains("weel") || r.name.ToLower().Contains("wheel");
                r.sharedMaterial = isWheel ? wheelMat : mat;
                if (isWheel) wheel = r.transform;
            }
            return wheel;
        }

        private static GameObject Kenney(string dir, string name, Transform parent, Vector3 localPos, float scale, float rotY,
            Material primary = null, Material secondary = null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{dir}/{name}.fbx");
            if (prefab == null) { Debug.LogWarning($"[ActsBuilder] Kenney model missing: {dir}/{name}.fbx"); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = Vector3.one * scale;
            go.transform.localRotation = Quaternion.Euler(0, rotY, 0);
            // Kenney Nature nie ma colormap.png → domyślne puste materiały (brzydkie szare/turkusowe bryły).
            // Nadpisujemy: submesh 0 = liście (primary), reszta = pień (secondary).
            if (primary != null)
                foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
                {
                    int n = Mathf.Max(1, r.sharedMaterials.Length);
                    var arr = new Material[n];
                    for (int k = 0; k < n; k++) arr[k] = (k == 0) ? primary : (secondary != null ? secondary : primary);
                    r.sharedMaterials = arr;
                }
            return go;
        }

        /// <summary>Proceduralne niebo (gradient + słońce) zamiast płaskiego koloru — głębia i klimat trasy.</summary>
        private static Material MakeSkybox()
        {
            const string dir = "Assets/_Project/Materials/Act2";
            EnsureFolder(dir);
            string path = $"{dir}/M_DriveSky.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                var sh = Shader.Find("Skybox/Procedural");
                if (sh == null) { Debug.LogWarning("[ActsBuilder] Skybox/Procedural shader not found"); return null; }
                m = new Material(sh); AssetDatabase.CreateAsset(m, path);
            }
            if (m.HasProperty("_SunSize")) m.SetFloat("_SunSize", 0.05f);
            if (m.HasProperty("_SunSizeConvergence")) m.SetFloat("_SunSizeConvergence", 5f);
            if (m.HasProperty("_AtmosphereThickness")) m.SetFloat("_AtmosphereThickness", 1.15f);
            if (m.HasProperty("_SkyTint")) m.SetColor("_SkyTint", new Color(0.5f, 0.62f, 0.78f));
            if (m.HasProperty("_GroundColor")) m.SetColor("_GroundColor", new Color(0.27f, 0.32f, 0.25f));
            if (m.HasProperty("_Exposure")) m.SetFloat("_Exposure", 1.1f);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material SolidURP(string name, Color c, float smooth, Color? emission = null)
        {
            const string dir = "Assets/_Project/Materials/Act2";
            EnsureFolder(dir);
            string path = $"{dir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                m = new Material(sh); AssetDatabase.CreateAsset(m, path);
            }
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c); else m.color = c;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (emission.HasValue && m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                m.SetColor("_EmissionColor", emission.Value);
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        private static void EnsureFolder(string dir)
        {
            var parts = dir.Split('/'); var path = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = $"{path}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(path, parts[i]);
                path = next;
            }
        }

        private static GameObject LBoxRot(Transform parent, string name, Vector3 pos, Vector3 size, Vector3 euler, Material mat)
        {
            var b = LBox(parent, name, pos, size, mat);
            b.transform.localRotation = Quaternion.Euler(euler);
            return b;
        }

        private static void Disc(Transform parent, string name, Vector3 pos, float radius, Material mat)
        {
            var d = GameObject.CreatePrimitive(PrimitiveType.Cylinder); d.name = name;
            d.transform.SetParent(parent, false);
            d.transform.localPosition = pos; d.transform.localRotation = Quaternion.Euler(90, 0, 0);
            d.transform.localScale = new Vector3(radius * 2, 0.01f, radius * 2);
            Object.DestroyImmediate(d.GetComponent<Collider>());
            d.GetComponent<Renderer>().sharedMaterial = mat;
        }

        // Torus w płaszczyźnie XY (oś przez Z) — obręcz kierownicy
        private static Mesh TorusMesh(float majorR, float minorR, int majorSeg, int minorSeg)
        {
            int ring = minorSeg + 1;
            var verts = new Vector3[(majorSeg + 1) * ring];
            var norms = new Vector3[verts.Length];
            var tris = new int[majorSeg * minorSeg * 6];
            int vi = 0;
            for (int i = 0; i <= majorSeg; i++)
            {
                float u = (float)i / majorSeg * Mathf.PI * 2f;
                float cu = Mathf.Cos(u), su = Mathf.Sin(u);
                for (int j = 0; j <= minorSeg; j++)
                {
                    float v = (float)j / minorSeg * Mathf.PI * 2f;
                    float cv = Mathf.Cos(v), sv = Mathf.Sin(v);
                    float r = majorR + minorR * cv;
                    verts[vi] = new Vector3(r * cu, r * su, minorR * sv);
                    norms[vi] = new Vector3(cv * cu, cv * su, sv);
                    vi++;
                }
            }
            int ti = 0;
            for (int i = 0; i < majorSeg; i++)
                for (int j = 0; j < minorSeg; j++)
                {
                    int a = i * ring + j, b = (i + 1) * ring + j;
                    tris[ti++] = a; tris[ti++] = b; tris[ti++] = a + 1;
                    tris[ti++] = a + 1; tris[ti++] = b; tris[ti++] = b + 1;
                }
            var m = new Mesh { name = "WheelTorus" };
            m.vertices = verts; m.normals = norms; m.triangles = tris;
            m.RecalculateBounds();
            return m;
        }

        // ============ ACT 3: CALL & HIDE (chowanie przed intruzem) ============
        private static SessionFlowManager.Act BuildAct3(Transform parent, RoomEnvironment env, MonoBehaviour hands)
        {
            var root = new GameObject("Act3_Call");
            root.transform.SetParent(parent, false);

            var scenario = Load<ScenarioConfig>($"{ScenDir}/act3_call.asset");
            var act3cfg = Load<Act3Config>($"{SO}/Act3Config.asset");

            // Microphone provider
            var micGo = new GameObject("MicInput");
            micGo.transform.SetParent(root.transform, false);
            var mic = micGo.AddComponent<MicrophoneInputProvider>();

            // --- Telefon (retro GLB) — dzwoni; gracz wstaje i podchodzi (~1.5 m), łapie słuchawkę ---
            Vector3 phonePos = env.Phone != null ? env.Phone.position : new Vector3(0f, 0.78f, 1.5f);
            var phoneBody = new GameObject("Phone"); phoneBody.transform.SetParent(root.transform, false);
            phoneBody.transform.position = phonePos;
            var phoneVis = Act3Glb("Assets/_Project/External/Act3/Telephone.glb", phoneBody.transform, 0.26f, phonePos, 200f, out _, true);
            if (phoneVis == null) // fallback: kostka + kapsuła
            {
                var pb = GameObject.CreatePrimitive(PrimitiveType.Cube); pb.transform.SetParent(phoneBody.transform, false);
                pb.transform.localPosition = Vector3.zero; pb.transform.localScale = new Vector3(0.18f, 0.06f, 0.18f);
                Object.DestroyImmediate(pb.GetComponent<Collider>()); pb.GetComponent<Renderer>().sharedMaterial = LabMaterials.Metal;
            }
            // Uchwyt słuchawki (chwytalny, niewidzialny) — u góry telefonu
            var receiver = new GameObject("Receiver"); receiver.transform.SetParent(phoneBody.transform, false);
            receiver.transform.position = phonePos + new Vector3(0f, 0.16f, 0f);

            var ringLightGo = new GameObject("RingLight"); ringLightGo.transform.SetParent(phoneBody.transform, false);
            ringLightGo.transform.localPosition = new Vector3(0, 0.5f, 0);
            var ringLight = ringLightGo.AddComponent<Light>();
            ringLight.type = LightType.Point; ringLight.color = new Color(0.9f, 0.7f, 0.2f); ringLight.range = 1.5f; ringLight.enabled = false;
            var phoneRing = phoneBody.AddComponent<PhoneRingController>();
            var prSo = new SerializedObject(phoneRing);
            prSo.FindProperty("ringClip").objectReferenceValue = LoadClip("phone_ring");
            prSo.FindProperty("indicatorLight").objectReferenceValue = ringLight;
            prSo.ApplyModifiedPropertiesWithoutUndo();
            phoneBody.SetActive(false);   // telefon Aktu III WYŁĄCZONY — odbierasz go już w aucie (Akt II); tu od razu idziemy pod łóżko

            // --- Środowisko ukrycia (ciemne mieszkanie + szafa z prześwitem) ---
            // Budowane w przestrzeni lokalnej; reżyser ustawia holder w miejscu gracza w runtime.
            var holder = new GameObject("HidingHolder"); holder.transform.SetParent(root.transform, false);
            BuildHidingEnvironment(holder.transform, act3cfg, out var thief, out var bedHideTarget, out var fleeTarget, out var doorPivot);
            var micBar = BuildMicBar(holder.transform, mic);
            holder.SetActive(false);

            // --- Detektory (mikrokroki 0/1/2) ---
            var dRoot = new GameObject("Detectors");
            dRoot.transform.SetParent(root.transform, false);

            AddUnderBed(dRoot.transform, "hide_under_bed", bedHideTarget);   // 1. budzisz się → SCHOWAJ SIĘ POD ŁÓŻKO (schyl głowę)
            AddPhoneAnswer(dRoot.transform, "answer_phone", phoneBody.transform, hands);   // 2. telefon DZWONI POD ŁÓŻKIEM → odbiór SPOJRZENIEM (potem dopiero dyspozytor)
            // PRAWDZIWE rozpoznawanie mowy (Wit.ai /dictation) — sprawdza słowa kluczowe formułki (stemy, odporne na odmianę).
            // Awaryjnie (brak sieci/timeout) krok i tak przechodzi, więc gra nie utyka.
            const string WIT = "5QSQG2RDH7MOLWEK6NMPXO277MTFSOE4";
            // DIALOG 112 ze skryptu: OSOBA czyta swoją kwestię (Wit nasłuchuje), a głos DYSPOZYTORA (Paulina, disp_*)
            // gra Z DETEKTORA na początku danego kroku — czyli pytanie dyspozytora, na które gracz odpowiada formułką.
            AddWit(dRoot.transform, "report_intruder", mic, WIT, new[] { "pomoc", "napastnik", "dom", "dzień" }, "");          // O1 — gracz inicjuje, BEZ głosu dyspozytora przed
            AddWit(dRoot.transform, "give_location", mic, WIT, new[] { "warszaw", "długa", "mieszkan", "dwanaśc", "ulic" }, "disp_address");  // D1 „Jaki jest adres?" → O2 adres
            AddWit(dRoot.transform, "describe_event", mic, WIT, new[] { "nie", "wiem" }, "disp_armed");   // D2 „Czy uzbrojony?" → O3 „Nie wiem"
            AddWit(dRoot.transform, "count_victims", mic, WIT, new[] { "nie" }, "disp_hurt");             // D3 „Czy ranny?" → O4 „Nie"
            AddWit(dRoot.transform, "give_status", mic, WIT, new[] { "słysz", "korytarz", "chodzi", "go" }, "disp_where");  // D4 „Gdzie napastnik?" → O5
            var silence = AddSilence(dRoot.transform, "stay_silent", mic, 45f);  // 45 s ciszy → syreny policyjne → wygrana

            var runner = root.AddComponent<ScenarioRunner>();
            SetScenario(runner, scenario);
            BuildHUD(root.transform, runner, new Vector3(phonePos.x, 0.95f, phonePos.z + 0.6f));   // NA DOLE

            // --- Reżyser sceny ukrycia ---
            var director = root.AddComponent<Act3HidingDirector>();
            var dso = new SerializedObject(director);
            dso.FindProperty("runner").objectReferenceValue = runner;
            dso.FindProperty("silenceDetector").objectReferenceValue = silence;
            dso.FindProperty("hidingHolder").objectReferenceValue = holder;
            dso.FindProperty("phoneRoot").objectReferenceValue = phoneBody;
            dso.FindProperty("phone").objectReferenceValue = phoneRing;   // telefon DZWONI POD ŁÓŻKIEM (krok answer_phone)
            dso.FindProperty("thief").objectReferenceValue = thief;
            dso.FindProperty("micBar").objectReferenceValue = micBar;
            dso.FindProperty("thiefFleeTarget").objectReferenceValue = fleeTarget;
            dso.FindProperty("roomDoorPivot").objectReferenceValue = doorPivot;
            dso.FindProperty("hideSpot").objectReferenceValue = bedHideTarget;
            dso.ApplyModifiedPropertiesWithoutUndo();

            return Act("Akt III - Telefon i ukrycie", root, runner, 0.3f, "");   // BEZ intro na starcie — dyspozytor mówi DOPIERO po odebraniu telefonu pod łóżkiem (give_location)
        }

        // ============ ACT 3 ENVIRONMENT ============
        private static void BuildHidingEnvironment(Transform holder, Act3Config cfg,
            out ThiefWanderAI thief, out Transform bedHideTarget, out Transform fleeTarget, out Transform doorPivotOut)
        {
            var wood = FirstMat(LabMaterials.Wood, LabMaterials.WoodLight, LabMaterials.Metal);
            var floorMat = FirstMat(LabMaterials.Floor, wood);
            var ceilMat = FirstMat(LabMaterials.Ceiling, wood);
            var darkMat = FirstMat(LabMaterials.FabricDark, wood);
            var paleMat = FirstMat(LabMaterials.Wall, LabMaterials.WoodLight, LabMaterials.Metal);

            // KRYJÓWKA = POD ŁÓŻKIEM z LeafiaRoom (Object_13). NIE budujemy duplikatu — pokój już ma łóżko.
            // bedHideTarget zostanie ustawione PO pozycjonowaniu pokoju (gdy znamy world-pos Object_13).
            bedHideTarget = LPoint(holder, "BedHideTarget", new Vector3(-1.35f, 0.08f, 1.5f)); // tymczasowo, nadpisane niżej

            // ----- POKÓJ = leafias_room (sypialnia). Gracz w szafie patrzy na DRZWI naprzeciwko -----
            const float DOOR_X = 0f;            // środek otworu drzwiowego przed graczem
            const float DOOR_Z = 3.1f;          // odległość drzwi od gracza
            const float ROOM_DOOR_OFFX = 1.53f; // pozycja drzwi względem środka pokoju (ProbeLeafiaView, yaw=180)
            const float ROOM_DOOR_OFFZ = 3.2f;  // pół-głębokość pokoju (ściana z drzwiami)
            var roomPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/External/LeafiaRoom/LeafiaRoom.glb");
            Vector3 hpW = holder.position;
            Transform doorLeaf = null;   // Object_25 (skrzydło drzwi LeafiaRoom) — podepniemy pod zawias, żeby się otwierały
            if (roomPrefab != null)   // UŻYWAMY LeafiaRoom (model gracza). Proceduralny pokój tylko jako fallback (else).
            {
                var room = (GameObject)PrefabUtility.InstantiatePrefab(roomPrefab);
                room.name = "LeafiaRoom"; room.transform.SetParent(holder, false);
                var baseLocalRot = room.transform.localRotation;   // glTFast nadaje ~(270,0,0) – ZACHOWAJ
                room.transform.localPosition = Vector3.zero;
                room.transform.localRotation = Quaternion.Euler(0f, 180f, 0f) * baseLocalRot; // ściana z drzwiami do przodu (+Z)
                room.transform.localScale = Vector3.one;
                // Ukryj WYŁĄCZNIE gigantyczny skybox/teren (Object_20 ≈ 60×21×25 m) — NIE ściany pokoju (próg 30 m, nie 15 m,
                // bo przy 15 m znikały ściany i drzwi „wisiały w pustce").
                foreach (var r in room.GetComponentsInChildren<MeshRenderer>(true))
                    if (Mathf.Max(r.bounds.size.x, r.bounds.size.y, r.bounds.size.z) > 30f) r.enabled = false;
                // Object_25 = skrzydło drzwi pokoju; Object_13 = łóżko.
                Transform leafiaBed = null;
                foreach (var t in room.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "Object_25") doorLeaf = t;
                    else if (t.name == "Object_27") leafiaBed = t;   // Object_27 = ŁÓŻKO (zweryfikowane renderem); Object_13 to BIURKO (monitor+fotel)
                }
                // POZYCJONOWANIE: ustaw pokój tak, by DRZWI (Object_25) wypadły dokładnie w (DOOR_X,_,DOOR_Z)
                // PRZED graczem, a podłoga na y=0. Dzięki temu intruz/kamera/zawias zgadzają się z resztą kodu.
                float floorMinW = float.MaxValue;
                foreach (var r in room.GetComponentsInChildren<MeshRenderer>(true))
                    if (r.enabled) floorMinW = Mathf.Min(floorMinW, r.bounds.min.y);
                if (floorMinW == float.MaxValue) floorMinW = 0f;
                var doorR0 = doorLeaf != null ? doorLeaf.GetComponent<Renderer>() : null;
                if (doorR0 != null)
                {
                    Vector3 dW = doorR0.bounds.center;     // świat == lokalne holdera (holder w origin)
                    Vector3 cur = room.transform.localPosition;
                    room.transform.localPosition = new Vector3(
                        cur.x + (DOOR_X - dW.x),
                        cur.y + (0f - floorMinW),
                        cur.z + (DOOR_Z - dW.z));
                }
                else // fallback: stare wyśrodkowanie po bounds
                {
                    Bounds bb = default; bool f = true;
                    foreach (var r in room.GetComponentsInChildren<MeshRenderer>(true))
                    { if (!r.enabled) continue; if (f) { bb = r.bounds; f = false; } else bb.Encapsulate(r.bounds); }
                    Vector3 cdiff = bb.center - hpW; float half = bb.size.y * 0.5f;
                    room.transform.localPosition = new Vector3((DOOR_X - ROOM_DOOR_OFFX) - cdiff.x, half - cdiff.y, (DOOR_Z - ROOM_DOOR_OFFZ) - cdiff.z);
                }

                // Cel chowania = POD ŁÓŻKIEM. Łóżko = Object_27 (leafiaBed) — ZWERYFIKOWANE renderem pokoju:
                // world bounds ~2.50×1.23×2.00 m, biała pościel z przodu pokoju. Object_13 (1.33×0.96×2.43) to BIURKO
                // (monitor + fotel) — wcześniej strzałka błędnie lądowała na nim. Model STAŁY → celujemy w Object_27 wprost.
                foreach (var r in room.GetComponentsInChildren<MeshRenderer>(true))
                    if (r.enabled) Debug.Log($"[Act3] MESH '{r.name}' c=({r.bounds.center.x:F2},{r.bounds.center.y:F2},{r.bounds.center.z:F2}) s=({r.bounds.size.x:F2},{r.bounds.size.y:F2},{r.bounds.size.z:F2})");

                Renderer bedR = leafiaBed != null ? leafiaBed.GetComponent<Renderer>() : null;   // PRIORYTET: Object_27 (łóżko)
                if (bedR == null)   // fallback gdyby Object_13 zniknął: największa niska, pozioma płyta wielkości łóżka
                {
                    float bestArea = 0f;
                    foreach (var r in room.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        if (!r.enabled) continue;
                        var s = r.bounds.size;
                        if (s.y < 0.25f || s.y > 1.3f) continue;   // łóżko z zagłówkiem bywa ~1 m wysokie
                        if (r.bounds.center.y > 1.0f) continue;
                        if (s.x < 1.1f || s.z < 1.1f) continue;
                        if (Mathf.Max(s.x, s.z) > 6f) continue;
                        float area = s.x * s.z;
                        if (area > bestArea) { bestArea = area; bedR = r; }
                    }
                }
                if (bedR != null)
                {
                    Vector3 bw = bedR.bounds.center;
                    bedHideTarget.position = new Vector3(bw.x, 0.10f, bw.z);
                    Debug.Log($"[Act3] ŁÓŻKO = '{bedR.name}' @ {bedHideTarget.position} (size {bedR.bounds.size})");
                }
                else Debug.LogWarning("[Act3] NIE wykryto łóżka — strzałka zostaje na pozycji tymczasowej.");
            }
            else // PROCEDURALNA SYPIALNIA — pełna kontrola: ściany + drzwi w PRAWDZIWEJ framudze + łóżko do schowania
            {
                const float ROOM_HW = 2.4f, BACK_Z = -1.6f, WALL_H = 2.6f, DOOR_W = 1.1f;
                var rr = new GameObject("Bedroom"); rr.transform.SetParent(holder, false);
                float depth = DOOR_Z - BACK_Z, cz = (DOOR_Z + BACK_Z) * 0.5f;
                LBox(rr.transform, "Floor",   new Vector3(0f, -0.02f, cz), new Vector3(ROOM_HW * 2f, 0.04f, depth), floorMat);
                LBox(rr.transform, "Ceiling", new Vector3(0f, WALL_H, cz),  new Vector3(ROOM_HW * 2f, 0.06f, depth), ceilMat);
                LBox(rr.transform, "Wall_Back",  new Vector3(0f, WALL_H * 0.5f, BACK_Z), new Vector3(ROOM_HW * 2f, WALL_H, 0.1f), paleMat);
                LBox(rr.transform, "Wall_Left",  new Vector3(-ROOM_HW, WALL_H * 0.5f, cz), new Vector3(0.1f, WALL_H, depth), paleMat);
                LBox(rr.transform, "Wall_Right", new Vector3( ROOM_HW, WALL_H * 0.5f, cz), new Vector3(0.1f, WALL_H, depth), paleMat);
                // przednia ściana z OTWOREM na drzwi (dwa segmenty + nadproże)
                float segW = (ROOM_HW * 2f - DOOR_W) * 0.5f, segCx = (DOOR_W + segW) * 0.5f;
                LBox(rr.transform, "Wall_F_L",   new Vector3(-segCx, WALL_H * 0.5f, DOOR_Z), new Vector3(segW, WALL_H, 0.1f), paleMat);
                LBox(rr.transform, "Wall_F_R",   new Vector3( segCx, WALL_H * 0.5f, DOOR_Z), new Vector3(segW, WALL_H, 0.1f), paleMat);
                LBox(rr.transform, "Wall_F_Top", new Vector3(0f, 2.18f, DOOR_Z), new Vector3(DOOR_W, WALL_H - 2.04f, 0.1f), paleMat);
                // ŁÓŻKO do schowania (rama+materac+nogi); gracz wczołguje się POD nie, twarzą do drzwi (+Z)
                Vector3 bp = new Vector3(0f, 0f, 0.7f);
                var bed = new GameObject("Bed"); bed.transform.SetParent(rr.transform, false);
                LBox(bed.transform, "BedFrame",    bp + new Vector3(0f, 0.42f, 0f), new Vector3(1.5f, 0.12f, 2.0f), darkMat);
                LBox(bed.transform, "BedMattress", bp + new Vector3(0f, 0.52f, 0f), new Vector3(1.42f, 0.16f, 1.92f), paleMat);
                for (int lx = -1; lx <= 1; lx += 2) for (int lz = -1; lz <= 1; lz += 2)
                    LBox(bed.transform, "BedLeg", bp + new Vector3(lx * 0.66f, 0.18f, lz * 0.88f), new Vector3(0.1f, 0.36f, 0.1f), darkMat);
                bedHideTarget.position = new Vector3(bp.x, 0.10f, bp.z);
            }

            // DRZWI: Object_25 to NIE skrzydło drzwi — to DUŻY element (~3×2.3×6 m). Wcześniej był chowany (kasowało
            // to ścianę), a własne skrzydło lądowało w jego ŚRODKU = na ŚRODKU POKOJU (zgłoszony bug). Teraz NIE chowamy
            // Object_25, a skrzydło stawiamy W TYLNEJ ŚCIANIE bryły pokoju, wyrównane w X z łóżkiem (→ „tyłem do drzwi,
            // twarzą do łóżka" w jednej linii, drzwi płasko w ścianie, nie w powietrzu).
            Bounds roomBB = default; bool rfirst = true;
            foreach (var r in holder.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!r.enabled) continue;
                if (rfirst) { roomBB = r.bounds; rfirst = false; } else roomBB.Encapsulate(r.bounds);
            }
            float backZ = rfirst ? DOOR_Z : (roomBB.max.z - 0.12f);
            Vector3 doorC = new Vector3(0.86f, 1.02f, backZ);   // DOKŁADNIE na drzwiach modelu (zweryfikowane renderem z markerem: tylna ściana, x=0.86)
            Debug.Log($"[Act3] Drzwi w OTWORZE modelu @ {doorC} (roomBB.max.z={(rfirst ? 0f : roomBB.max.z):F2})");

            // materiały drzwi przez SolidURP (sprawdzona ścieżka — poprawne kolory jak reszta sceny)
            var doorWhite = SolidURP("M_DoorLeaf3", new Color(0.90f, 0.88f, 0.83f), 0.2f);
            var panelMat  = SolidURP("M_DoorPanel3", new Color(0.78f, 0.76f, 0.70f), 0.2f);

            var doorPivot = (new GameObject("RoomDoorPivot")).transform; doorPivot.SetParent(holder, false);
            doorPivot.position = new Vector3(doorC.x - 0.65f, 0f, doorC.z);   // zawias = lewa krawędź POWIĘKSZONEGO skrzydła
            doorPivot.rotation = holder.rotation;

            var leaf = GameObject.CreatePrimitive(PrimitiveType.Cube); leaf.name = "DoorLeaf";
            Object.DestroyImmediate(leaf.GetComponent<Collider>());
            leaf.transform.SetParent(doorPivot, false);
            // POWIĘKSZONE do rozmiaru drzwi modelu + framugi (1.3×2.5 m) → ZAKRYWA namalowane drzwi pokoju, żeby nie było ich widać
            leaf.transform.localPosition = new Vector3(0.65f, 1.25f, 0f);     // środek skrzydła 0.65 m od zawiasu
            leaf.transform.localScale = new Vector3(1.3f, 2.5f, 0.06f);
            leaf.GetComponent<Renderer>().sharedMaterial = doorWhite;
            for (int pi = 0; pi < 2; pi++)   // dwie wpuszczone płyciny (front = -Z lokalne)
            {
                var pnl = GameObject.CreatePrimitive(PrimitiveType.Cube); pnl.name = "DoorPanelInset";
                Object.DestroyImmediate(pnl.GetComponent<Collider>());
                pnl.transform.SetParent(leaf.transform, false);
                pnl.transform.localPosition = new Vector3(0f, pi == 0 ? 0.23f : -0.23f, -0.55f);
                pnl.transform.localScale = new Vector3(0.64f, 0.40f, 0.7f);
                pnl.GetComponent<Renderer>().sharedMaterial = panelMat;
            }
            var knob = GameObject.CreatePrimitive(PrimitiveType.Sphere); knob.name = "DoorKnob";
            Object.DestroyImmediate(knob.GetComponent<Collider>());
            knob.transform.SetParent(leaf.transform, false);
            // kontr-skalowanie (skrzydło ma skalę 1.3×2.5×0.06) → klamka OKRĄGŁA, na froncie, przy prawej krawędzi
            knob.transform.localPosition = new Vector3(0.38f, 0f, -0.7f);
            knob.transform.localScale = new Vector3(0.062f, 0.032f, 1.33f);
            knob.GetComponent<Renderer>().sharedMaterial = SolidURP("M_DoorKnob3", new Color(0.72f, 0.6f, 0.22f), 0.7f);

            // ciemne tło za otworem (po otwarciu widać mrok korytarza, nie dziurę do skyboxa)
            var backdrop = GameObject.CreatePrimitive(PrimitiveType.Cube); backdrop.name = "DoorBackdrop";
            Object.DestroyImmediate(backdrop.GetComponent<Collider>());
            backdrop.transform.SetParent(holder, false);
            backdrop.transform.position = new Vector3(doorC.x, 1.20f, doorC.z + 0.06f);
            backdrop.transform.rotation = holder.rotation;
            backdrop.transform.localScale = new Vector3(2.3f, 2.7f, 0.04f);   // DUŻO szersze/wyższe — w pełni zakrywa namalowane drzwi modelu (koniec „podwójnych drzwi")
            backdrop.GetComponent<Renderer>().sharedMaterial = SolidURP("M_DoorBack3", new Color(0.05f, 0.05f, 0.06f), 0f);

            doorPivotOut = doorPivot;

            // Ciepła lampka (klimat referencji Fears to Fathom)
            var lampGo = new GameObject("Lamp"); lampGo.transform.SetParent(holder, false);
            lampGo.transform.localPosition = new Vector3(-1.5f, 0.6f, 1.4f);
            var lamp = lampGo.AddComponent<Light>();
            lamp.type = LightType.Point; lamp.color = new Color(1f, 0.62f, 0.28f); lamp.range = 4.2f; lamp.intensity = 1.6f;

            // ----- Intruz = CHODZĄCY humanoid (Mixamo) + zapętlona animacja chodu + ciemna sylwetka + latarka -----
            var thiefGo = new GameObject("Thief"); thiefGo.transform.SetParent(holder, false);
            thiefGo.transform.localPosition = new Vector3(DOOR_X, 0f, DOOR_Z - 0.3f); // tuż w progu drzwi
            thiefGo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);          // twarzą do gracza (-Z)
            var visual = new GameObject("Visual"); visual.transform.SetParent(thiefGo.transform, false);
            const string scaryPath = "Assets/_Project/External/Act3/WarzombieWalk/WarzombieWalk.fbx";   // World War Zombie z WBUDOWANYMI teksturami (diffuse/normal/specular) + własny chód
            const string walkPath  = "Assets/_Project/External/Act3/WarzombieWalk/WarzombieWalk.fbx";   // chód = WŁASNA animacja modelu (ten sam rig → BEZ retargetu, zapętlony). RootMotion OFF, ThiefWanderAI przesuwa transform.
            SetupHumanoid(walkPath);                          // upewnij Humanoid + zapętl
            Avatar scaryAvatar = SetupHumanoid(scaryPath);    // NOWY intruz: model Humanoid + jego Avatar
            var scaryPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(scaryPath);
            bool usingScary = scaryPrefab != null;
            var intruderPrefab = usingScary ? scaryPrefab : AssetDatabase.LoadAssetAtPath<GameObject>(walkPath);
            if (intruderPrefab != null)
            {
                var dan = (GameObject)PrefabUtility.InstantiatePrefab(intruderPrefab);
                dan.name = "IntruderModel"; dan.transform.SetParent(visual.transform, false);
                dan.transform.localPosition = Vector3.zero; dan.transform.localRotation = Quaternion.identity; dan.transform.localScale = Vector3.one;
                Bounds db = default; bool df = true;
                foreach (var r in dan.GetComponentsInChildren<Renderer>()) { if (df) { db = r.bounds; df = false; } else db.Encapsulate(r.bounds); }
                float ds = db.size.y > 0.01f ? 1.78f / db.size.y : 1f;
                ds = Mathf.Clamp(ds, 0.3f, 4f);   // zabezpieczenie: nigdy gigantyczny/mikroskopijny (Mixamo bywa kapryśny ze skalą)
                dan.transform.localScale = Vector3.one * ds;
                Debug.Log($"[Act3] Intruz auto-skala ds={ds:F3} bounds={db.size}");
                df = true; Bounds db2 = default;
                foreach (var r in dan.GetComponentsInChildren<Renderer>()) { if (df) { db2 = r.bounds; df = false; } else db2.Encapsulate(r.bounds); }
                Vector3 vp = visual.transform.position;
                dan.transform.localPosition = new Vector3(-(db2.center.x - vp.x), -(db2.min.y - vp.y), -(db2.center.z - vp.z));
                // ZombieHitman ma WŁASNE realistyczne materiały+tekstury (skóra/ubranie/oczy/zęby) z importu FBX.
                // NIE nadpisujemy ich (żadnej „bladej skóry", która wcześniej psuła przezroczystości → znikała połowa ciała).
                // Tylko ujednolicamy shader na URP/Lit i, jeśli po zmianie shadera albedo zostało pod _MainTex, przepisujemy do _BaseMap.
                var urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                // FBX ma WBUDOWANE tekstury (diffuse/normal/specular), ale Unity nie zawsze podpina je do materiału → BIAŁY model.
                // Wyciągamy DIFFUSE i NORMAL z sub-assetów FBX i wymuszamy podpięcie.
                Texture2D embDiffuse = null, embNormal = null;
                foreach (var a in AssetDatabase.LoadAllAssetsAtPath(scaryPath))
                {
                    if (!(a is Texture2D tx)) continue;
                    var ln = (tx.name ?? "").ToLowerInvariant();
                    if (ln.Contains("normal")) { if (embNormal == null) embNormal = tx; }
                    else if (ln.Contains("diffuse") || ln.Contains("albedo") || ln.Contains("basecolor") || ln.Contains("_d")) { embDiffuse = tx; }
                    else if (embDiffuse == null && !ln.Contains("specular") && !ln.Contains("metallic") && !ln.Contains("rough")) embDiffuse = tx;
                }
                // FALLBACK: wyciągnięte ręcznie z binarki FBX PNG-i obok pliku (Unity nie robi z nich sub-assetów)
                if (embDiffuse == null) embDiffuse = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/External/Act3/WarzombieWalk/world_war_zombie_diffuse.png");
                Debug.Log($"[Act3] Intruz tekstury wbudowane: diffuse={(embDiffuse != null ? embDiffuse.name : "BRAK")} normal={(embNormal != null ? embNormal.name : "brak")}");
                foreach (var r in dan.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = r.sharedMaterials;
                    for (int mi = 0; mi < mats.Length; mi++)
                    {
                        var src = mats[mi]; if (src == null) continue;
                        Texture keepAlbedo = src.HasProperty("_BaseMap") ? src.GetTexture("_BaseMap") : null;
                        if (keepAlbedo == null && src.HasProperty("_MainTex")) keepAlbedo = src.GetTexture("_MainTex");
                        if (keepAlbedo == null) keepAlbedo = embDiffuse;   // WYMUSZENIE wbudowanej tekstury (inaczej BIAŁY)
                        if (src.shader == null || src.shader.name == null || !src.shader.name.Contains("Universal"))
                            src.shader = urpLit;
                        if (keepAlbedo != null)
                        {
                            if (src.HasProperty("_BaseMap")) src.SetTexture("_BaseMap", keepAlbedo);
                            if (src.HasProperty("_MainTex")) src.SetTexture("_MainTex", keepAlbedo);
                            if (src.HasProperty("_BaseColor")) src.SetColor("_BaseColor", Color.white);   // pełna jasność tekstury
                        }
                        if (embNormal != null && src.HasProperty("_BumpMap")) { src.SetTexture("_BumpMap", embNormal); src.EnableKeyword("_NORMALMAP"); }
                    }
                }
                // Animator z zapętlonym chodem; bez root motion (ThiefWanderAI przesuwa transform -> chodzi)
                var anim = dan.GetComponentInChildren<Animator>() ?? dan.AddComponent<Animator>();
                anim.applyRootMotion = false;
                if (usingScary && scaryAvatar != null) anim.avatar = scaryAvatar;   // RETARGET chodu Humanoid na szkielet nowego intruza
                EnsureFbxClipsLoop(walkPath);   // chód MUSI się zapętlać (inaczej „chodzi chwilę i zamarza")
                var walkClip = LoadFirstClip(walkPath);
                if (walkClip != null) anim.runtimeAnimatorController = MakeWalkController(walkClip);

                // FOOT-GROUNDING (runtime): retarget chodu (Mixamo → CC zombie) ZATAPIAŁ stopy („chodzi w ziemi").
                // Komponent mierzy zatopienie w pierwszym cyklu chodu i podnosi model RAZ (niezawodne, bez kruchego
                // AnimationMode w batch; wyłącza się po cyklu → nie walczy z jumpscarem).
                dan.AddComponent<MRCrisisTrainer.Acts.Act3.IntruderFootGrounder>();
                Debug.Log($"[Act3] Intruz: model='{intruderPrefab.name}' scary={usingScary} avatarOK={(scaryAvatar != null)} walkClip={(walkClip != null)}");
            }
            else // fallback: blada sylwetka (kapsuła)
            {
                var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                body.name = "Body"; body.transform.SetParent(visual.transform, false);
                body.transform.localPosition = new Vector3(0, 0.95f, 0);
                body.transform.localScale = new Vector3(0.55f, 0.95f, 0.40f);
                Object.DestroyImmediate(body.GetComponent<Collider>());
                body.GetComponent<Renderer>().sharedMaterial = paleMat;
            }
            var flashGo = new GameObject("Flashlight"); flashGo.transform.SetParent(visual.transform, false);
            flashGo.transform.localPosition = new Vector3(0.0f, 1.3f, 0.25f);
            flashGo.transform.localRotation = Quaternion.Euler(10f, 0f, 0f);
            var flash = flashGo.AddComponent<Light>();
            flash.type = LightType.Spot; flash.color = new Color(1f, 0.95f, 0.8f); flash.range = 8f; flash.spotAngle = 46f; flash.intensity = 4f;

            thief = thiefGo.AddComponent<ThiefWanderAI>();
            var wps = new[]
            {
                LPoint(holder, "WP0", new Vector3( 0.0f, 0f, 2.6f)),   // wchodzi od drzwi w głąb pokoju
                LPoint(holder, "WP1", new Vector3(-1.3f, 0f, 1.9f)),   // szuka przy łóżku
                LPoint(holder, "WP2", new Vector3( 1.3f, 0f, 2.1f)),   // szuka przy biurku
                LPoint(holder, "WP3", new Vector3( 0.0f, 0f, 1.2f)),   // podchodzi blisko gracza/szafy
            };
            fleeTarget = LPoint(holder, "FleeTarget", new Vector3(DOOR_X, 0f, DOOR_Z + 0.3f)); // ucieka przez drzwi
            var tso = new SerializedObject(thief);
            tso.FindProperty("config").objectReferenceValue = cfg;
            var wpArr = tso.FindProperty("waypoints");
            wpArr.arraySize = wps.Length;
            for (int i = 0; i < wps.Length; i++) wpArr.GetArrayElementAtIndex(i).objectReferenceValue = wps[i];
            tso.FindProperty("closetTarget").objectReferenceValue = bedHideTarget;  // intruz spogląda w stronę łóżka (kryjówki)
            tso.FindProperty("visualRoot").objectReferenceValue = visual;
            tso.FindProperty("flashlight").objectReferenceValue = flash;
            tso.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Act3MicBarUI BuildMicBar(Transform holder, MicrophoneInputProvider mic)
        {
            var canvas = UIFactory.WorldCanvas("MicBar", Vector3.zero, Quaternion.identity, new Vector2(150, 380), 0.0022f);
            canvas.transform.SetParent(holder, false);
            canvas.transform.localPosition = new Vector3(0.62f, 1.3f, 0.5f);
            canvas.transform.localRotation = Quaternion.Euler(0, 180f, 0);
            var group = canvas.gameObject.AddComponent<CanvasGroup>();

            // MINIMALISTYCZNY HUD mikrofonu (jak na zdjęciu): cienka ścieżka + wypełnienie, BEZ wielkiego tła
            UIFactory.Panel(canvas.transform, new Vector2(30, 300), new Color(0.1f, 0.1f, 0.12f, 0.40f), new Vector2(0, 25));
            var fill = UIFactory.FilledBar(canvas.transform, new Vector2(30, 300), new Vector2(0, 25), new Color(0.3f, 0.9f, 0.4f));
            fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Vertical;
            fill.fillOrigin = (int)Image.OriginVertical.Bottom; fill.fillAmount = 0f;
            // prosta ikonka mikrofonu (kropla) pod paskiem
            UIFactory.Panel(canvas.transform, new Vector2(22, 22), new Color(0.85f, 0.85f, 0.9f, 0.9f), new Vector2(0, -120));
            var label = UIFactory.Text(canvas.transform, "", 20, new Vector2(160, 40), new Vector2(0, -150),
                TextAlignmentOptions.Center, new Color(0.85f, 0.9f, 1f));

            var ui = canvas.gameObject.AddComponent<Act3MicBarUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("mic").objectReferenceValue = mic;
            so.FindProperty("fill").objectReferenceValue = fill;
            so.FindProperty("label").objectReferenceValue = label;
            so.FindProperty("group").objectReferenceValue = group;
            so.ApplyModifiedPropertiesWithoutUndo();
            canvas.gameObject.AddComponent<MRCrisisTrainer.UI.FaceCamera>();
            // Pasek mikrofonu PRZYCZEPIONY DO KAMERY, BLIŻEJ ŚRODKA (dół, lekko z lewej) i BLISKO (z=0.42) → dobrze widoczny,
            // nie wchodzi w tekstury (bliżej niż geometria pokoju). Render na wierzchu: wysoki sortingOrder + mała skala.
            var attach = canvas.gameObject.AddComponent<MRCrisisTrainer.UI.AttachToCameraHUD>();
            attach.localOffset = new Vector3(-0.14f, -0.22f, 0.42f);
            var cv = canvas.GetComponent<Canvas>();
            if (cv != null) { cv.overrideSorting = true; cv.sortingOrder = 32760; }
            foreach (var g in canvas.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                if (g.material != null) { g.material.SetFloat("unity_GUIZTestMode", (float)UnityEngine.Rendering.CompareFunction.Always); }
            return ui;
        }

        private static GameObject LBox(Transform parent, string name, Vector3 localCenter, Vector3 localSize, Material mat)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
            b.name = name; b.transform.SetParent(parent, false);
            b.transform.localPosition = localCenter;
            b.transform.localScale = localSize;
            Object.DestroyImmediate(b.GetComponent<Collider>());
            if (mat != null) b.GetComponent<Renderer>().sharedMaterial = mat;
            return b;
        }

        private static Transform LPoint(Transform parent, string name, Vector3 localPos)
        {
            var g = new GameObject(name); g.transform.SetParent(parent, false);
            g.transform.localPosition = localPos; return g.transform;
        }

        /// <summary>Świat-AABB obejmujące wszystkie renderery pod transformem.</summary>
        private static Bounds GetRendererBounds(Transform t)
        {
            Bounds b = default; bool first = true;
            foreach (var r in t.GetComponentsInChildren<Renderer>(true))
            {
                if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
            }
            return b;
        }

        /// <summary>Ciasny świat-AABB drzwi: z wierzchołków siatki, z odrzuceniem błędnych (oddalonych) wierzchołków.</summary>
        private static Bounds RobustDoorBounds(Transform t)
        {
            var pts = new System.Collections.Generic.List<Vector3>();
            foreach (var mf in t.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null && mf.sharedMesh.isReadable) { var m = mf.transform.localToWorldMatrix; foreach (var v in mf.sharedMesh.vertices) pts.Add(m.MultiplyPoint3x4(v)); }
            foreach (var smr in t.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (smr.sharedMesh != null && smr.sharedMesh.isReadable) { var m = smr.transform.localToWorldMatrix; foreach (var v in smr.sharedMesh.vertices) pts.Add(m.MultiplyPoint3x4(v)); }
            if (pts.Count == 0) return GetRendererBounds(t);
            float mx = MedianAxis(pts, 0), my = MedianAxis(pts, 1), mz = MedianAxis(pts, 2);
            Bounds b = default; bool any = false;
            foreach (var p in pts)
                if (Mathf.Abs(p.x - mx) < 2.5f && Mathf.Abs(p.y - my) < 2.5f && Mathf.Abs(p.z - mz) < 2.5f)
                { if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p); }
            return any ? b : GetRendererBounds(t);
        }

        private static float MedianAxis(System.Collections.Generic.List<Vector3> pts, int axis)
        {
            var a = new float[pts.Count];
            for (int i = 0; i < pts.Count; i++) a[i] = pts[i][axis];
            System.Array.Sort(a);
            return a[a.Length / 2];
        }

        private static Material FirstMat(params Material[] mats)
        {
            foreach (var m in mats) if (m != null) return m;
            return null;
        }

        // ============ ACT 3 ASSET HELPERS ============
        /// <summary>Wstawia GLB (glTFast) znormalizowany: skala do docelowej WYSOKOŚCI, spód na pos.y, środek xz na pos.</summary>
        private static GameObject Act3Glb(string path, Transform parent, float targetH, Vector3 pos, float yaw, out Bounds worldBounds, bool dropFlatBackdrops = false)
        {
            worldBounds = default;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { Debug.LogWarning($"[ActsBuilder] GLB missing: {path}"); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetParent(parent, false);
            var baseRot = go.transform.localRotation;   // glTFast nadaje obrót bazowy (~270° X) – ZACHOWAJ, inaczej model leży na boku
            go.transform.localPosition = Vector3.zero; go.transform.localRotation = baseRot; go.transform.localScale = Vector3.one;
            // niektóre modele mają WIELKIE płaskie tło (podłoga/ściana sceny renderu) -> psuje auto-skalowanie i wygląda jak ściana
            if (dropFlatBackdrops)
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                {
                    var sz = r.bounds.size;
                    float mn = Mathf.Min(sz.x, Mathf.Min(sz.y, sz.z));
                    float mx = Mathf.Max(sz.x, Mathf.Max(sz.y, sz.z));
                    if (mn < 0.03f && mx > 2.0f) r.enabled = false;   // płaski + duży = tło, wyłącz
                }
            Bounds b = default; bool first = true;
            foreach (var r in go.GetComponentsInChildren<Renderer>()) { if (!r.enabled) continue; if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); }
            if (!first && b.size.y > 0.001f)
            {
                float s = targetH / b.size.y;
                go.transform.localScale = Vector3.one * s;
                first = true; Bounds b2 = default;
                foreach (var r in go.GetComponentsInChildren<Renderer>()) { if (!r.enabled) continue; if (first) { b2 = r.bounds; first = false; } else b2.Encapsulate(r.bounds); }
                Vector3 parentPos = parent != null ? parent.position : Vector3.zero;
                Vector3 localCenter = b2.center - parentPos; // parent bez obrotu w czasie budowy
                go.transform.localPosition = new Vector3(pos.x - localCenter.x, pos.y - (b2.min.y - parentPos.y), pos.z - localCenter.z);
                worldBounds = b2;
            }
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0) * baseRot;   // yaw + zachowana orientacja modelu
            return go;
        }

        /// <summary>Materiały sypialni z wypalonych map VRay (Unlit), przyciemnione do klimatu nocy.</summary>
        private static void ApplyBedroomMats(GameObject go, float dim)
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
            var sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture");
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                string mn = r.name.ToLower().Replace("_", "");
                if (mn.Contains("wall")) mn = "room";
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

        /// <summary>Materiały pokoju per-SUBMESH wg nazwy materiału (np. living room: jeden mesh, materiały Sofa/TV/Carpet).
        /// lit=true -> URP/Lit (latarka/lampa oświetlają), false -> Unlit (wypalone mapy).</summary>
        private static void ApplyRoomMatsBySubmesh(GameObject go, string texDir, float dim, bool lit)
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { texDir });
            var texes = new System.Collections.Generic.List<(string name, Texture2D tex)>();
            foreach (var g in guids)
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                var t = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
                if (t != null) texes.Add((System.IO.Path.GetFileNameWithoutExtension(p).ToLower(), t));
            }
            var sh = Shader.Find(lit ? "Universal Render Pipeline/Lit" : "Universal Render Pipeline/Unlit") ?? Shader.Find("Standard");
            Texture2D Match(string raw)
            {
                string n = (raw ?? "").ToLower().Replace("_", "").Replace(" ", "").Replace(".", "");
                if (n.Contains("wall") || n.Contains("structure") || n.Contains("backdrop")) { /* zostaw do dopasowania */ }
                Texture2D best = null; int bs = -1;
                foreach (var (tn, tex) in texes)
                {
                    string tnc = tn.Replace("vraycompletemap", "").Replace("_t", "").Replace("_", "").Replace("-", "");
                    int sc = 0;
                    if (tnc == n) sc = 100;
                    else if (n.StartsWith(tnc) || tnc.StartsWith(n)) sc = 60;
                    else if (n.Contains(tnc) || tnc.Contains(n)) sc = 30;
                    if (sc > bs) { bs = sc; best = tex; }
                }
                return bs >= 30 ? best : null;
            }
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                var src = r.sharedMaterials; var dst = new Material[src.Length];
                for (int i = 0; i < src.Length; i++)
                {
                    string nm = src[i] != null ? src[i].name : r.name;
                    var tex = Match(nm) ?? Match(r.name);
                    var m = new Material(sh);
                    if (tex != null && m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(dim, dim, dim, 1f));
                    if (lit && m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.1f);
                    dst[i] = m;
                }
                r.sharedMaterials = dst;
            }
        }

        /// <summary>Pierwsza animacja z FBX (np. chód intruza).</summary>
        private static AnimationClip LoadFirstClip(string fbxPath)
        {
            foreach (var a in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
                if (a is AnimationClip c && !c.name.StartsWith("__preview__")) return c;
            return null;
        }

        /// <summary>AnimatorController z jednym, zapętlonym stanem chodu.</summary>
        private static UnityEditor.Animations.AnimatorController MakeWalkController(AnimationClip clip)
        {
            string path = "Assets/_Project/External/Act3/Intruder/IntruderWalk.controller";
            var ctrl = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(path);
            var sm = ctrl.layers[0].stateMachine;
            var st = sm.AddState("Walk");
            st.motion = clip;
            sm.defaultState = st;
            return ctrl;
        }

        /// <summary>Wymusza zapętlenie klipów chodu w FBX (loopTime=true), żeby intruz chodził w kółko,
        /// a nie „chodził chwilę i zamarł/leciał" po jednym przejściu animacji.</summary>
        private static void EnsureFbxClipsLoop(string fbxPath)
        {
            var importer = UnityEditor.AssetImporter.GetAtPath(fbxPath) as UnityEditor.ModelImporter;
            if (importer == null) return;
            var clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0) return;
            bool changed = false;
            for (int i = 0; i < clips.Length; i++) { if (!clips[i].loopTime) { clips[i].loopTime = true; changed = true; } }
            if (changed) { importer.clipAnimations = clips; importer.SaveAndReimport(); }
        }

        /// <summary>Ustawia FBX jako Humanoid (tworzy Avatar) + zapętla klipy. Zwraca Avatar (do retargetu animacji).</summary>
        private static Avatar SetupHumanoid(string fbxPath)
        {
            var imp = UnityEditor.AssetImporter.GetAtPath(fbxPath) as UnityEditor.ModelImporter;
            if (imp == null) return null;
            bool changed = false;
            if (imp.animationType != UnityEditor.ModelImporterAnimationType.Human)
            {
                imp.animationType = UnityEditor.ModelImporterAnimationType.Human;
                imp.avatarSetup = UnityEditor.ModelImporterAvatarSetup.CreateFromThisModel;
                changed = true;
            }
            var clips = imp.defaultClipAnimations;
            if (clips != null && clips.Length > 0)
            {
                bool lc = false;
                for (int i = 0; i < clips.Length; i++) if (!clips[i].loopTime) { clips[i].loopTime = true; lc = true; }
                if (lc) { imp.clipAnimations = clips; changed = true; }
            }
            if (changed) imp.SaveAndReimport();
            foreach (var a in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(fbxPath))
                if (a is Avatar av) return av;
            return null;
        }

        // ============ DETECTOR HELPERS ============
        private static void AddConfirm(Transform p, string id)
        {
            var go = new GameObject($"D_{id}"); go.transform.SetParent(p, false);
            var d = go.AddComponent<ConfirmActionDetector>(); d.stepId = id;
        }
        private static void AddProximity(Transform p, string id, Transform target, float radius)
        {
            var go = new GameObject($"D_{id}"); go.transform.SetParent(p, false);
            var d = go.AddComponent<ProximityDetector>(); d.stepId = id;
            var so = new SerializedObject(d);
            so.FindProperty("target").objectReferenceValue = target;
            so.FindProperty("radius").floatValue = radius;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void AddHandsOnTarget(Transform p, string id, MonoBehaviour hands, Transform target, float radius)
        {
            var go = new GameObject($"D_{id}"); go.transform.SetParent(p, false);
            var d = go.AddComponent<HandsOnTargetDetector>(); d.stepId = id;
            var so = new SerializedObject(d);
            so.FindProperty("handProviderBehaviour").objectReferenceValue = hands;
            so.FindProperty("target").objectReferenceValue = target;
            so.FindProperty("radius").floatValue = radius;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        private static GrabDetector AddGrab(Transform p, string id, MonoBehaviour hands, Transform grabbable, float radius)
        {
            var go = new GameObject($"D_{id}"); go.transform.SetParent(p, false);
            var d = go.AddComponent<GrabDetector>(); d.stepId = id;
            var so = new SerializedObject(d);
            so.FindProperty("handProviderBehaviour").objectReferenceValue = hands;
            so.FindProperty("grabbable").objectReferenceValue = grabbable;
            so.FindProperty("radius").floatValue = radius;
            so.ApplyModifiedPropertiesWithoutUndo();
            return d;
        }
        private static void AddPhoneAnswer(Transform p, string id, Transform phone, MonoBehaviour hands)
        {
            var go = new GameObject($"D_{id}"); go.transform.SetParent(p, false);
            var d = go.AddComponent<PhoneAnswerDetector>(); d.stepId = id;
            var so = new SerializedObject(d);
            so.FindProperty("phone").objectReferenceValue = phone;
            so.FindProperty("handProviderBehaviour").objectReferenceValue = hands;   // zapas (reach), gdy śledzenie działa
            so.FindProperty("maxSeconds").floatValue = 30f;
            so.FindProperty("raiseToFaceOnAnswer").boolValue = false;   // telefon zostaje POD ŁÓŻKIEM (nie skacze do twarzy/biurka)
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void AddVoice(Transform p, string id, MicrophoneInputProvider mic, string opClip, float secs)
        {
            var go = new GameObject($"D_{id}"); go.transform.SetParent(p, false);
            var d = go.AddComponent<VoiceSpeakDetector>(); d.stepId = id;
            var so = new SerializedObject(d);
            so.FindProperty("mic").objectReferenceValue = mic;
            so.FindProperty("operatorVoiceClip").stringValue = opClip;
            so.FindProperty("requiredSpeechSeconds").floatValue = secs;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void AddWit(Transform p, string id, MicrophoneInputProvider mic, string token, string[] keywords, string opClip)
        {
            var go = new GameObject($"D_{id}"); go.transform.SetParent(p, false);
            var d = go.AddComponent<WitDictationDetector>(); d.stepId = id;
            var so = new SerializedObject(d);
            so.FindProperty("mic").objectReferenceValue = mic;
            so.FindProperty("witToken").stringValue = token;
            so.FindProperty("operatorVoiceClip").stringValue = opClip;
            var arr = so.FindProperty("keywords"); arr.arraySize = keywords.Length;
            for (int i = 0; i < keywords.Length; i++) arr.GetArrayElementAtIndex(i).stringValue = keywords[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        private static SilenceMicrostepDetector AddSilence(Transform p, string id, MicrophoneInputProvider mic, float secs)
        {
            var go = new GameObject($"D_{id}"); go.transform.SetParent(p, false);
            var d = go.AddComponent<SilenceMicrostepDetector>(); d.stepId = id;
            var so = new SerializedObject(d);
            so.FindProperty("mic").objectReferenceValue = mic;
            so.FindProperty("requiredSilenceSeconds").floatValue = secs;
            so.ApplyModifiedPropertiesWithoutUndo();
            return d;
        }
        private static void AddUnderBed(Transform p, string id, Transform hideZone)
        {
            var go = new GameObject($"D_{id}"); go.transform.SetParent(p, false);
            var d = go.AddComponent<UnderBedDetector>(); d.stepId = id;
            var so = new SerializedObject(d);
            so.FindProperty("hideZone").objectReferenceValue = hideZone;
            // SIEDZĄCE MR: stoisz przy drzwiach (łóżko w głębi). Chowasz się SCHYLAJĄC GŁOWĘ (gest „pod łóżko"). Hojny zasięg
            // poziomy, żeby zaliczyć od drzwi; liczy się SCHYLENIE (duckDelta). Rozmowa rusza dopiero po schyleniu (answer_phone).
            so.FindProperty("horizontalRadius").floatValue = 2.5f;
            so.FindProperty("maxHeadHeight").floatValue = 0.5f;   // ABS tylko dla REALNEGO wczołgania (głowa <0.6 m nad podłogą) — siedzący NIE zalicza bez schylenia
            so.FindProperty("duckDelta").floatValue = 0.30f;      // MUSISZ schylić głowę ~30 cm = „wchodzę pod łóżko" (inaczej telefon się nie pokaże)
            so.FindProperty("baselineWindow").floatValue = 0.6f;
            so.FindProperty("requiredHold").floatValue = 0.9f;   // utrzymaj schylenie ~1 s = wyraźny sygnał „schowałem się"
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        private static SteeringRecoveryDetector AddSteering(Transform p, string id, VehicleConfig cfg, SteeringWheelController wheel, WindshieldView windshield, float dir = 1f)
        {
            var go = new GameObject($"D_{id}"); go.transform.SetParent(p, false);
            var d = go.AddComponent<SteeringRecoveryDetector>(); d.stepId = id;
            var so = new SerializedObject(d);
            so.FindProperty("config").objectReferenceValue = cfg;
            so.FindProperty("wheel").objectReferenceValue = wheel;
            so.FindProperty("windshield").objectReferenceValue = windshield;
            so.FindProperty("initialDirection").floatValue = dir >= 0f ? 1f : -1f;   // +1 = poślizg w prawo, -1 = w lewo
            so.ApplyModifiedPropertiesWithoutUndo();
            return d;
        }

        /// <summary>Ładuje albedo ciężarówki Renault po nazwie materiału (obsługuje spacje → podkreślenia w nazwach plików).</summary>
        private static Texture2D LoadTruckAlbedo(string tdir, string mn)
        {
            foreach (var name in new[] { mn, mn.Replace(" ", "_") })
                foreach (var ext in new[] { ".jpeg", ".jpg", ".png" })
                {
                    var t = AssetDatabase.LoadAssetAtPath<Texture2D>($"{tdir}{name}_albedo{ext}");
                    if (t != null) return t;
                }
            return null;
        }

        private static void AddGaze(Transform p, string id, Transform target, float maxAngle, float hold)
        {
            var go = new GameObject($"D_{id}"); go.transform.SetParent(p, false);
            var d = go.AddComponent<GazeDetector>(); d.stepId = id;
            var so = new SerializedObject(d);
            so.FindProperty("target").objectReferenceValue = target;
            so.FindProperty("maxAngle").floatValue = maxAngle;
            so.FindProperty("holdSeconds").floatValue = hold;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void AddTimed(Transform p, string id, float duration)
        {
            var go = new GameObject($"D_{id}"); go.transform.SetParent(p, false);
            var d = go.AddComponent<TimedAutoDetector>(); d.stepId = id;
            var so = new SerializedObject(d);
            so.FindProperty("duration").floatValue = duration;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ============ HUD ============
        private static MicrostepHUD BuildHUD(Transform parent, ScenarioRunner runner, Vector3 pos, bool attachCamera = false)
        {
            var cam = Camera.main;
            Quaternion rot = Quaternion.identity;
            var canvas = UIFactory.WorldCanvas("HUD", pos, rot, new Vector2(720, 320), 0.0013f);
            canvas.transform.SetParent(parent, false);
            canvas.transform.position = pos;
            canvas.gameObject.SetActive(false);   // HUD mikrokroków WYŁĄCZONY (życzenie: bez napisów „o krokach wykonanych")

            // BEZ TŁA (życzenie użytkownika) — same napisy, czytelne na dole ekranu
            var step = UIFactory.Text(canvas.transform, "...", 34, new Vector2(680, 90), new Vector2(0, 110));
            var hint = UIFactory.Text(canvas.transform, "", 24, new Vector2(680, 110), new Vector2(0, 20),
                TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.3f));
            var progress = UIFactory.Text(canvas.transform, "", 24, new Vector2(360, 50), new Vector2(0, -70));
            var bar = UIFactory.FilledBar(canvas.transform, new Vector2(560, 18), new Vector2(0, -110), new Color(0.2f, 0.9f, 0.3f));
            var score = UIFactory.Text(canvas.transform, "Punkty: 0", 22, new Vector2(360, 44), new Vector2(0, -150));

            var billboard = canvas.gameObject.AddComponent<MRCrisisTrainer.UI.FaceCamera>();
            if (attachCamera)
            {
                var h = canvas.gameObject.AddComponent<MRCrisisTrainer.UI.AttachToCameraHUD>();
                h.localOffset = new Vector3(0f, -0.40f, 1.25f);   // HUD na samym DOLE ekranu, blisko, pionowo
            }

            var hud = canvas.gameObject.AddComponent<MicrostepHUD>();
            var so = new SerializedObject(hud);
            so.FindProperty("runner").objectReferenceValue = runner;
            so.FindProperty("stepLabel").objectReferenceValue = step;
            so.FindProperty("hintLabel").objectReferenceValue = hint;
            so.FindProperty("progressLabel").objectReferenceValue = progress;
            so.FindProperty("progressBar").objectReferenceValue = bar;
            so.FindProperty("scoreLabel").objectReferenceValue = score;
            so.ApplyModifiedPropertiesWithoutUndo();
            return hud;
        }

        // ============ UTIL ============
        private static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);
        private static AudioClip LoadClip(string name) =>
            AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/_Project/Resources/Audio/{name}.wav");

        private static void SetScenario(ScenarioRunner runner, ScenarioConfig scenario)
        {
            var so = new SerializedObject(runner);
            so.FindProperty("scenario").objectReferenceValue = scenario;
            so.FindProperty("autoStart").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static SessionFlowManager.Act Act(string name, GameObject root, ScenarioRunner runner, float introDelay, string voice)
        {
            return new SessionFlowManager.Act { actName = name, actRoot = root, runner = runner, introDelay = introDelay, introVoice = voice };
        }

        private static void WriteFlow(SessionFlowManager flow, List<SessionFlowManager.Act> acts)
        {
            var so = new SerializedObject(flow);
            var arr = so.FindProperty("acts");
            arr.arraySize = acts.Count;
            for (int i = 0; i < acts.Count; i++)
            {
                var e = arr.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("actName").stringValue = acts[i].actName;
                e.FindPropertyRelative("actRoot").objectReferenceValue = acts[i].actRoot;
                e.FindPropertyRelative("runner").objectReferenceValue = acts[i].runner;
                e.FindPropertyRelative("introDelay").floatValue = acts[i].introDelay;
                e.FindPropertyRelative("introVoice").stringValue = acts[i].introVoice;
            }
            so.FindProperty("autoStart").boolValue = true;
            so.FindProperty("betweenActsDelay").floatValue = 0.6f;   // szybkie przejście Akt II→III (po odebraniu telefonu)
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
