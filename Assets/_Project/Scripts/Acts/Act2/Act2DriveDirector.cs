using System.Collections;
using UnityEngine;
using TMPro;
using MRCrisisTrainer.Core;
using MRCrisisTrainer.Config;
using MRCrisisTrainer.Gameplay;
using MRCrisisTrainer.Logging;

namespace MRCrisisTrainer.Acts.Act2
{
    /// <summary>
    /// Reżyser Aktu II (jazda autem przez las + poślizg). Pełne VR: passthrough off, niebo,
    /// kokpit „nakłada się" wokół gracza (snap do pozycji), las jedzie do gracza od startu.
    /// Po rozpoznaniu poślizgu świat się przechyla; po odzyskaniu kontroli wyjeżdżasz z lasu
    /// na jasną, otwartą przestrzeń.
    /// </summary>
    public class Act2DriveDirector : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private ScenarioRunner runner;
        [SerializeField] private GameObject holder;        // kokpit + las (lokalne wsp.)
        [SerializeField] private ForestScroller forest;
        [SerializeField] private WindshieldView windshield;
        [SerializeField] private Light sunLight;
        [SerializeField] private Material driveSky;   // proceduralne niebo (gradient + słońce)
        [SerializeField] private SteeringWheelController steeringWheel;   // boczne sterowanie: kierownica → realny tor jazdy
        [SerializeField] private MRCrisisTrainer.Acts.Act3.PhoneRingController seatPhone;   // telefon na siedzeniu pasażera (nieużywany — telefon przeniesiony do Aktu III)
        [SerializeField] private GameObject truck;   // ciężarówka do wypadku (krok 'crash' — wjeżdża z naprzeciwka)
        [Header("Tor jazdy")]
        [SerializeField] private float laneBaseX = -1.75f;   // prawy pas (świat przesunięty)
        [SerializeField] private float steerRangeX = 3.5f;   // jak mocno kierownica przesuwa auto w poprzek drogi (wyraźnie)

        [Header("Posadzenie kierowcy")]
        [Tooltip("Docelowa wysokość oczu kierowcy nad drogą (m). Gracz sadzany dynamicznie do tej wysokości NIEZALEŻNIE od wzrostu — kadr jak na screenach (droga wypełnia szybę, deska+kierownica niżej).")]
        [SerializeField] private float targetEyeHeight = 1.32f;   // zgodne z kokpitem Jaguara (EYE=1.32)

        [Header("Skid / atmosfera")]
        [SerializeField] private float skidPreviewAngle = 20f;
        [SerializeField] private Color driveAmbient = new Color(0.42f, 0.46f, 0.5f);
        [SerializeField] private Color driveBackground = new Color(0.55f, 0.72f, 0.92f);
        [SerializeField] private Color exitAmbient = new Color(0.95f, 0.95f, 0.88f);
        [SerializeField] private Color exitBackground = new Color(0.82f, 0.9f, 1f);

        private Camera cam;
        private Transform camOffset;
        private float camOffsetOrigY;
        private bool seated;

        // zapis stanu otoczenia (przywracany po akcie, by nie psuć Aktu III / labu)
        private bool atmoSaved;
        private Material prevSky;
        private bool prevFog;
        private Color prevFogColor;
        private FogMode prevFogMode;
        private float prevFogStart, prevFogEnd;
        private UnityEngine.Rendering.AmbientMode prevAmbMode;
        private Color prevAmb;
        private CameraClearFlags prevClear;
        private Color prevBg;

        private bool driveStarted;
        private GameObject sitPrompt;       // świat-tekst „usiądź" — sprzątany po starcie/wyłączeniu
        private Coroutine onboardingCo;     // korutyna wejścia (sit → auto); wznawiana z OnEnable

        void OnEnable()
        {
            if (holder != null) holder.SetActive(false);   // auto: las/kokpit ukryte; pokazujemy je dopiero po usiądnięciu
            // Onboarding w MR: realny pokój widoczny (passthrough ON), dopóki gracz nie usiądzie.
            XR.PassthroughController.Instance?.EnablePassthrough();
            if (runner != null)
            {
                runner.OnStepActivated += OnStep;
                runner.OnScenarioFinished += OnFinished;
            }
            // Onboarding z OnEnable (NIE ze Start): SessionFlowManager wyłącza/włącza akt na starcie,
            // a Start() odpala się tylko raz — jego korutyna ginęła przy wyłączeniu i AUTO NIGDY SIĘ NIE POJAWIAŁO.
            // Z OnEnable wznawia się przy każdym włączeniu aktu.
            if (!driveStarted)
            {
                if (onboardingCo != null) StopCoroutine(onboardingCo);
                onboardingCo = StartCoroutine(Onboarding());
            }
        }

        void OnDisable()
        {
            // Po jeździe wracamy do RZECZYWISTOŚCI (realny pokój) – passthrough znów ON
            XR.PassthroughController.Instance?.EnablePassthrough();
            AudioManager.Instance?.StopAmbient();   // WYCISZ silnik auta — nie wlec dźwięku jazdy do Aktu III (pokój)
            seatPhone?.StopRinging();
            if (runner != null)
            {
                runner.OnStepActivated -= OnStep;
                runner.OnScenarioFinished -= OnFinished;
            }
            if (onboardingCo != null) { StopCoroutine(onboardingCo); onboardingCo = null; }
            CleanupPrompt();
            // Przywróć wysokość kamery po akcie
            if (seated && camOffset != null)
            {
                var lp = camOffset.localPosition; lp.y = camOffsetOrigY; camOffset.localPosition = lp;
                seated = false;
            }
            // Przywróć otoczenie (niebo / mgła / ambient / kamera) — Akt III ma swój klimat
            if (atmoSaved)
            {
                RenderSettings.skybox = prevSky; RenderSettings.fog = prevFog; RenderSettings.fogColor = prevFogColor;
                RenderSettings.fogMode = prevFogMode; RenderSettings.fogStartDistance = prevFogStart; RenderSettings.fogEndDistance = prevFogEnd;
                RenderSettings.ambientMode = prevAmbMode; RenderSettings.ambientLight = prevAmb;
                if (cam != null) { cam.clearFlags = prevClear; cam.backgroundColor = prevBg; }
                atmoSaved = false;
            }
        }

        void Start() { }   // onboarding przeniesiony do OnEnable (przeżywa disable/enable z SessionFlowManagera)

        void Update()
        {
            // REALNE STEROWANIE: kierownica przesuwa auto w poprzek drogi — MOCNO i czytelnie.
            // Tor jazdy oddajemy WindshieldView (JEDYNY pisarz pozycji świata) → koniec walki o transform.
            // Wcześniej Act2DriveDirector i WindshieldView pisały ten sam localPosition i kasowały się nawzajem,
            // dlatego „ruszało się autem, ale nic to nie dawało". Teraz reakcja na kierownicę jest realna.
            if (!driveStarted || steeringWheel == null || windshield == null) return;
            float steer = steeringWheel.NormalizedSteering;                 // -1..+1
            // ZNAK ODWRÓCONY (potwierdzone na goglach): skręt kołem w lewo → auto jedzie w LEWO (było odwrotnie).
            windshield.LateralOffset = laneBaseX - (steer * steerRangeX);   // prawy pas + wychylenie kołem zgodne z kierunkiem
        }

        /// <summary>MR onboarding: realny pokój (passthrough), gracz PODCHODZI do fotela i SIADA; auto „nakłada się" dopiero po usiądnięciu.</summary>
        private IEnumerator Onboarding()
        {
            if (driveStarted) yield break;
            yield return null; // kamera zdąży się ustawić
            XR.PassthroughController.Instance?.EnablePassthrough();   // realny pokój widoczny
            if (holder != null) holder.SetActive(false);

            var c = GetCamera();
            // Gracz SIADA → auto się „nakłada". Pokazujemy wyraźny cel; czekamy aż usiądzie (z bezpiecznym timeoutem,
            // żeby nigdy nie zablokować). To było życzenie użytkownika: „muszę usiąść i wtedy pojawia się auto".
            CleanupPrompt();
            sitPrompt = MakeSitPrompt(c);
            yield return StartCoroutine(WaitForSit(c));
            CleanupPrompt();

            driveStarted = true;
            yield return StartCoroutine(BeginDrive());   // snap auta do gracza, passthrough off, kokpit+las
        }

        private void CleanupPrompt()
        {
            if (sitPrompt != null) { Destroy(sitPrompt); sitPrompt = null; }
        }

        /// <summary>Czeka aż gracz usiądzie (HMD opada o ≥22 cm wzgl. pozycji stojącej). Timeout 30 s = i tak startuj.</summary>
        private IEnumerator WaitForSit(Camera c)
        {
            if (c == null) { yield return new WaitForSeconds(1.5f); yield break; }
            // baseline = najwyższa pozycja głowy w 1. sek (pozycja stojąca/wyjściowa)
            float baseline = c.transform.position.y, ts = 0f;
            while (ts < 1.0f) { baseline = Mathf.Max(baseline, c.transform.position.y); ts += Time.deltaTime; yield return null; }
            float sitY = baseline - 0.18f;   // spadek 18 cm = usiadł
            float stable = 0f, timeout = 0f;
            while (stable < 0.5f)
            {
                timeout += Time.deltaTime;
                if (timeout > 15f) { JSONLLogger.Instance?.LogEvent("act2_sit_timeout", null); yield break; }  // i tak startuj — NIGDY nie blokuj wejścia
                if (c.transform.position.y <= sitY) stable += Time.deltaTime; else stable = 0f;
                yield return null;
            }
            JSONLLogger.Instance?.LogEvent("act2_seated", null);
        }

        /// <summary>Świat-tekst „usiądź tutaj" + zielony pierścień na podłodze, zwrócone do gracza.
        /// Jeśli skan przestrzeni znalazł kanapę/fotel — pierścień ląduje DOKŁADNIE na nim.</summary>
        private GameObject MakeSitPrompt(Camera c)
        {
            if (c == null) return null;
            Vector3 fwd = c.transform.forward; fwd.y = 0f; fwd = fwd.sqrMagnitude > 0.01f ? fwd.normalized : Vector3.forward;

            // SIEDZĄCE MR: NIE wysyłamy gracza do dalekiego fotela ze skanu (bywa poza guardianem → gracz wychodził z granic).
            // Gracz siada TAM GDZIE JEST; pierścień ląduje dokładnie pod nim, a po usiądnięciu kokpit „nakłada się" wokół niego.
            Vector3 ringPos = new Vector3(c.transform.position.x, 0.02f, c.transform.position.z);
            string label = "USIĄDŹ — tu, gdzie jesteś\nAuto pojawi się, gdy usiądziesz";

            var root = new GameObject("SitPrompt");
            // tekst 3D — nad pierścieniem, zwrócony do gracza
            var txtGo = new GameObject("SitText"); txtGo.transform.SetParent(root.transform, false);
            var tmp = txtGo.AddComponent<TextMeshPro>();
            tmp.text = label;
            tmp.fontSize = 0.5f; tmp.alignment = TextAlignmentOptions.Center; tmp.color = new Color(0.95f, 0.97f, 1f);
            tmp.fontStyle = FontStyles.Bold;
            tmp.rectTransform.sizeDelta = new Vector2(2.6f, 1.0f);
            // napis PRZED graczem na wysokości oczu, mały i czytelny (nie gigant pod nogami)
            Vector3 txtPos = c.transform.position + fwd * 1.3f; txtPos.y = c.transform.position.y - 0.05f;
            txtGo.transform.position = txtPos;
            txtGo.transform.rotation = Quaternion.LookRotation(txtPos - c.transform.position, Vector3.up);

            // „latarnia" celu — akcent cyjan jak w menu: pulsujący pierścień + cienki słup światła (bardzo czytelne w passthrough)
            var accent = new Color(0.3f, 0.78f, 1f);
            var sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            Material AccentMat() { var mm = new Material(sh); if (mm.HasProperty("_BaseColor")) mm.SetColor("_BaseColor", accent); else mm.color = accent; return mm; }

            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder); ring.name = "SeatRing";
            var col = ring.GetComponent<Collider>(); if (col != null) Destroy(col);
            ring.transform.SetParent(root.transform, false);
            ring.transform.position = new Vector3(ringPos.x, 0.02f, ringPos.z);
            ring.transform.localScale = new Vector3(0.6f, 0.012f, 0.6f);
            ring.GetComponent<Renderer>().sharedMaterial = AccentMat();
            ring.AddComponent<MRCrisisTrainer.UI.UIPulse>();   // pulsuje jak beacon „tutaj"

            // (usunięto wielki cyjanowy słup — to on robił plamę, gdy gracz stał w jego środku; wystarczy pulsujący pierścień)
            return root;
        }

        private IEnumerator BeginDrive()
        {
            yield return null; // kamera zdąży się ustawić
            cam = GetCamera();
            if (holder != null)
            {
                if (cam != null)
                {
                    Vector3 p = cam.transform.position; p.y = 0f;
                    holder.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, cam.transform.eulerAngles.y, 0f));
                }
                holder.SetActive(true);
            }

            // Posadź gracza DYNAMICZNIE: oczy dokładnie na targetEyeHeight nad drogą, niezależnie od wzrostu.
            // (holder snapowany do y=0 = poziom drogi; cam.position.y = aktualna wysokość oczu nad drogą)
            if (cam != null && cam.transform.parent != null)
            {
                camOffset = cam.transform.parent;
                camOffsetOrigY = camOffset.localPosition.y;
                float roadY = holder != null ? holder.transform.position.y : 0f;
                float eyeNow = cam.transform.position.y - roadY;
                float drop = Mathf.Clamp(eyeNow - targetEyeHeight, -0.6f, 0.9f);
                var lp = camOffset.localPosition; lp.y = camOffsetOrigY - drop; camOffset.localPosition = lp;
                seated = true;
            }

            // Pełne VR: passthrough off + niebo (gradient) + mgła głębi + słońce
            // Zapisz poprzedni stan otoczenia, by przywrócić po akcie
            prevSky = RenderSettings.skybox; prevFog = RenderSettings.fog; prevFogColor = RenderSettings.fogColor;
            prevFogMode = RenderSettings.fogMode; prevFogStart = RenderSettings.fogStartDistance; prevFogEnd = RenderSettings.fogEndDistance;
            prevAmbMode = RenderSettings.ambientMode; prevAmb = RenderSettings.ambientLight;
            if (cam != null) { prevClear = cam.clearFlags; prevBg = cam.backgroundColor; }
            atmoSaved = true;

            if (driveSky != null)
            {
                RenderSettings.skybox = driveSky;
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
                if (cam != null) cam.clearFlags = CameraClearFlags.Skybox;
            }
            else
            {
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = driveAmbient;
                if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = driveBackground; }
            }
            // Mgła dystansu — droga i las wtapiają się w głąb (głębia + ukrycie końca segmentów)
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = driveBackground;
            RenderSettings.fogStartDistance = 28f;
            RenderSettings.fogEndDistance = 150f;
            DynamicGI.UpdateEnvironment();
            if (sunLight != null) sunLight.enabled = true;
            XR.PassthroughController.Instance?.DisablePassthrough();
            // Ukryj wirtualny lab — ma być widać las, nie ściany pokoju
            RoomEnvironment.Instance?.SetRoomVisible(false);

            // Jazda od pierwszej klatki
            windshield?.SetActive(true);
            windshield?.SetSkidYaw(0f);
            forest?.Begin();
            AudioManager.Instance?.PlayAmbient("engine_hum", 0.5f);
            JSONLLogger.Instance?.LogEvent("act2_drive_start", null);
        }

        private void OnStep(Microstep step)
        {
            if (step == null) return;
            // Poślizg widoczny już przy rozpoznaniu; podczas counter_steer przejmuje SteeringRecoveryDetector.
            switch (step.id)
            {
                case "recognize_skid":
                    windshield?.SetSkidYaw(skidPreviewAngle);          // poślizg w PRAWO (ten sam kąt co start symulacji = brak skoku)
                    windshield?.AddShake(1f);                          // mocny wstrząs w momencie utraty przyczepności
                    AudioManager.Instance?.PlaySfx("tire_screech", 1f);
                    JSONLLogger.Instance?.LogEvent("act2_skid_begin", null);
                    break;
                case "recognize_skid_l":
                    windshield?.SetSkidYaw(-skidPreviewAngle);         // poślizg w LEWO
                    windshield?.AddShake(1f);
                    AudioManager.Instance?.PlaySfx("tire_screech", 1f);
                    JSONLLogger.Instance?.LogEvent("act2_skid_begin_l", null);
                    break;
                case "grip_wheel":
                    AudioManager.Instance?.PlaySfx("ui_click", 0.7f);   // potwierdzenie chwytu
                    break;
                case "stabilize":
                case "stabilize_l":
                    windshield?.SetSkidYaw(0f);                         // GWARANTOWANE wyprostowanie auta po opanowaniu poślizgu (koniec „auto na ukos")
                    AudioManager.Instance?.PlaySfx("success_chime", 0.8f); // poślizg opanowany
                    JSONLLogger.Instance?.LogEvent("act2_recovered", null);
                    break;
                case "resume":
                    JSONLLogger.Instance?.LogEvent("act2_resume", null);   // wracasz do jazdy — za chwilę WYPADEK (telefon przeniesiony do Aktu III)
                    break;
                case "crash":
                    StartCoroutine(CrashSequence());   // z naprzeciwka wjeżdża ciężarówka → huk → czerń
                    break;
            }
        }

        private void OnFinished(int total, int max) => StartCoroutine(ExitForest());

        private IEnumerator ExitForest()
        {
            JSONLLogger.Instance?.LogEvent("act2_exit_forest", null);
            seatPhone?.StopRinging();   // odebrane / koniec aktu — telefon przestaje dzwonić
            windshield?.SetSkidYaw(0f);
            float t = 0f;
            const float dur = 1.2f;   // szybkie zakończenie po odebraniu telefonu → niemal natychmiastowy teleport do Aktu III
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / dur);
                forest?.SetSpeedMultiplier(Mathf.Lerp(1f, 0.1f, k));   // zwalniamy
                // las → jasna otwarta przestrzeń: mgła cofa się i jaśnieje
                RenderSettings.fogColor = Color.Lerp(driveBackground, exitBackground, k);
                RenderSettings.fogEndDistance = Mathf.Lerp(150f, 320f, k);
                if (RenderSettings.ambientMode == UnityEngine.Rendering.AmbientMode.Flat)
                    RenderSettings.ambientLight = Color.Lerp(driveAmbient, exitAmbient, k);
                if (cam != null && cam.clearFlags == CameraClearFlags.SolidColor)
                    cam.backgroundColor = Color.Lerp(driveBackground, exitBackground, k);
                yield return null;
            }
            forest?.Stop();
        }

        /// <summary>WYPADEK: z naprzeciwka pędzi ciężarówka, wjeżdża w gracza → HUK → CZERŃ (utrata przytomności).
        /// Czerń ZOSTAJE (quad na kamerze, „CrashBlackout") — Akt III zdejmie ją przy przebudzeniu w sypialni.</summary>
        private IEnumerator CrashSequence()
        {
            var c = GetCamera();
            windshield?.SetSkidYaw(0f);
            JSONLLogger.Instance?.LogEvent("act2_crash", null);
            AudioManager.Instance?.PlaySfx("truck_sound", 1f);   // NOWY dźwięk nadjeżdżającej ciężarówki (od użytkownika)

            if (truck != null && c != null)
            {
                truck.SetActive(true);
                // KIERUNEK DROGI (nie spojrzenia!) — ciężarówka wjeżdża Z NAPRZECIWKA W DÓŁ DROGI, a nie z lasu, na który patrzysz.
                // Droga biegnie wzdłuż „forward" rodzica ciężarówki (holder środowiska jazdy). Fallback: spojrzenie.
                Transform roadRef = truck.transform.parent != null ? truck.transform.parent : c.transform;
                Vector3 fwd = roadRef.forward; fwd.y = 0f; fwd = fwd.sqrMagnitude > 0.01f ? fwd.normalized : Vector3.forward;
                Quaternion face = Quaternion.LookRotation(-fwd, Vector3.up);   // przód ciężarówki zwrócony do gracza
                // KOŁA NA JEZDNI: ustaw obrót, zmierz DÓŁ modelu i podnieś o tyle, by spód (koła) był na poziomie jezdni (y=0),
                // a nie zatapiał się do połowy (pivot modelu jest w środku, nie przy kołach).
                truck.transform.SetPositionAndRotation(new Vector3(c.transform.position.x, 0f, c.transform.position.z), face);
                Bounds tbb = default; bool tff = true;
                foreach (var rr in truck.GetComponentsInChildren<Renderer>()) { if (tff) { tbb = rr.bounds; tff = false; } else tbb.Encapsulate(rr.bounds); }
                float yWheels = tff ? 0f : -tbb.min.y;   // o tyle podnieś, by spód = jezdnia
                Vector3 startPos = c.transform.position + fwd * 42f; startPos.y = yWheels;
                Vector3 endPos = c.transform.position + fwd * 1.0f; endPos.y = yWheels;
                float t = 0f; const float dur = 1.0f;
                while (t < dur)
                {
                    t += Time.deltaTime;
                    float k = t / dur; k *= k;   // przyspiesza — pędzi na gracza
                    truck.transform.SetPositionAndRotation(Vector3.Lerp(startPos, endPos, k), face);
                    yield return null;
                }
            }
            else yield return new WaitForSeconds(1.0f);

            // HUK + natychmiastowa CZERŃ
            AudioManager.Instance?.PlaySfx("jumpscare_boom", 1f);
            AudioManager.Instance?.PlaySfx("jumpscare_hit", 1f);
            MakeBlackout(c);
            if (truck != null) truck.SetActive(false);
        }

        private void MakeBlackout(Camera c)
        {
            if (c == null || GameObject.Find("CrashBlackout") != null) return;
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad); q.name = "CrashBlackout";
            var col = q.GetComponent<Collider>(); if (col != null) Destroy(col);
            q.transform.SetParent(c.transform, false);   // na KAMERZE (przeżywa przełączenie aktów)
            q.transform.localPosition = new Vector3(0f, 0f, 0.25f);
            q.transform.localRotation = Quaternion.identity;
            q.transform.localScale = new Vector3(5f, 4f, 1f);
            var sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var m = new Material(sh); if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.black); else m.color = Color.black;
            q.GetComponent<Renderer>().sharedMaterial = m;
        }

        private Camera GetCamera()
        {
            var c = Camera.main;
            if (c == null) c = FindFirstObjectByType<Camera>();
            return c;
        }
    }
}
