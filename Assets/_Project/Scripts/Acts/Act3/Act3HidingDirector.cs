using System.Collections;
using UnityEngine;
using TMPro;
using MRCrisisTrainer.Config;
using MRCrisisTrainer.Core;
using MRCrisisTrainer.Gameplay;
using MRCrisisTrainer.Gameplay.Detectors;
using MRCrisisTrainer.Logging;

namespace MRCrisisTrainer.Acts.Act3
{
    /// <summary>
    /// Reżyser Aktu III (chowanie się pod łóżkiem — schemat „Fears to Fathom").
    /// Sekwencja:
    ///   1. Telefon dzwoni w passthrough (realny pokój). Gracz odbiera słuchawkę.
    ///   2. PŁYNNE przejście (czarna kurtyna) → ciemna sypialnia VR (gracz dalej siedzi).
    ///   3. Cel: „wczołgaj się POD ŁÓŻKO" — gracz podchodzi i kuca/kładzie się nisko (UnderBedDetector).
    ///   4. Pod łóżkiem zakłada się „widok spod łóżka" (wąska szpara) i pojawiają się formułki do 112.
    ///   5. Po przeczytaniu formułek dyspozytor mówi, że patrol jedzie → DRZWI się otwierają, wchodzi intruz.
    ///   6. Gracz leży cicho 2 min (SilenceMicrostepDetector) → syreny → intruz ucieka → koniec.
    /// Zaczepia się o eventy ScenarioRunner i SilenceMicrostepDetector.
    /// </summary>
    public class Act3HidingDirector : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private ScenarioRunner runner;
        [SerializeField] private SilenceMicrostepDetector silenceDetector;
        [SerializeField] private GameObject hidingHolder;     // ciemna sypialnia + łóżko + intruz (lokalne wsp.)
        [SerializeField] private GameObject phoneRoot;        // telefon widoczny w fazie passthrough
        [SerializeField] private PhoneRingController phone;
        [SerializeField] private ThiefWanderAI thief;
        [SerializeField] private Act3MicBarUI micBar;
        [SerializeField] private Transform thiefFleeTarget;
        [SerializeField] private Transform roomDoorPivot;     // prawdziwe drzwi pokoju (obracają się przy wejściu intruza)
        [SerializeField] private Transform hideSpot;          // środek strefy POD ŁÓŻKIEM (cel chowania + cel spojrzenia intruza)
        [SerializeField] private string doorClip = "door_open";    // „Gate 2" — skrzypienie/otwieranie drzwi przez intruza

        [Header("Audio (Resources/Audio)")]
        [SerializeField] private string sirenClip = "police_siren";   // Twoje nagranie POLICE SIRENS SIGNAL
        [SerializeField] private string introClip = "op_112_answer";

        [Header("Atmosfera")]
        // NORMALNE oświetlenie pokoju — pokój ma być widoczny w swoich naturalnych teksturach (życzenie użytkownika),
        // NIE ciemny horror. LeafiaRoom ma zapieczone tekstury (VRay), więc płaskie, jasne ambient pokazuje je wiernie.
        [SerializeField] private Color hidingAmbient = new Color(0.74f, 0.74f, 0.76f);
        [SerializeField] private Color hidingBackground = new Color(0.16f, 0.17f, 0.2f);

        private bool transitioned;
        private bool caught;   // gracz złapany (jumpscare) — zmienia zakończenie (bez syren/ucieczki)
        private Color savedAmbient;
        private CameraClearFlags savedClear;
        private Color savedBg;
        private Camera cam;

        private GameObject hideMarker;     // pierścień „wczołgaj się tutaj"
        private GameObject underBedView;   // czarne pasy zostawiające szparę (widok spod łóżka)
        private TextMeshPro underBedLine;  // formułki do 112 „przyklejone" do głowy (widoczne spod łóżka)

        void OnEnable()
        {
            // Faza startowa: passthrough ON, sypialnia ukryta.
            if (hidingHolder != null) hidingHolder.SetActive(false);
            if (micBar != null) micBar.Hide();
            if (thief != null) thief.HideImmediate();

            if (runner != null)
            {
                runner.OnStepActivated += HandleStepActivated;
                runner.OnScenarioFinished += HandleFinished;
            }
            if (silenceDetector != null)
            {
                silenceDetector.OnStrike += HandleNoiseStrike;
                silenceDetector.OnProgress += HandleSilenceProgress;
            }

            XR.PassthroughController.Instance?.EnablePassthrough();
            JSONLLogger.Instance?.LogEvent("act3_hiding_ready", null);
        }

        void Start()
        {
            // NOWY przepływ: budzisz się po wypadku → chowasz się pod łóżko → DOPIERO TAM dzwoni telefon (krok answer_phone).
            // Telefon NIE dzwoni na starcie aktu (nie ma już telefonu w aucie).
            StartCoroutine(AssertPassthrough());
        }

        /// <summary>Stawia dzwoniący telefon na PRAWDZIWYM stoliku z przeskanowanej przestrzeni (Scene API);
        /// gdy skanu/stolika brak — fallback: tuż przed graczem na wysokości ręki, żeby dało się go odebrać.</summary>
        private void SnapPhoneToPlayer()
        {
            var c = GetCamera(); if (c == null || phoneRoot == null) return;

            // SIEDZĄCE MR: telefon ZAWSZE tuż przed graczem na wysokości ręki (skanowany stolik bywa za daleko/poza guardianem).
            // 0.45 m w przód, 0.42 m poniżej oczu = naturalny zasięg, żeby dało się go odebrać bez wstawania i chodzenia.
            Vector3 fwd = c.transform.forward; fwd.y = 0f; fwd = fwd.sqrMagnitude > 0.01f ? fwd.normalized : Vector3.forward;
            phoneRoot.transform.position = c.transform.position + fwd * 0.45f + Vector3.up * (-0.42f);
            phoneRoot.transform.rotation = Quaternion.LookRotation(-fwd, Vector3.up);
        }

        /// <summary>Telefon DZWONI POD ŁÓŻKIEM: pojawia się na podłodze tuż przed schowanym graczem (nisko,
        /// w zasięgu spojrzenia) i dzwoni. Gracz odbiera go SPOJRZENIEM (PhoneAnswerDetector).</summary>
        private void ShowPhoneUnderBed()
        {
            if (phoneRoot != null && hideSpot != null)
            {
                phoneRoot.SetActive(true);
                // STAŁA pozycja POD ŁÓŻKIEM (przy hideSpot, na podłodze) — NIE zależna od spojrzenia gracza
                // (wcześniej szedł „przed kamerę" → lądował na biurku, gdy gracz patrzył na biurko).
                Vector3 p = hideSpot.position; p.y = 0.10f;
                phoneRoot.transform.position = p;
                var c = GetCamera();
                if (c != null)
                {
                    Vector3 toCam = c.transform.position - p; toCam.y = 0f;
                    if (toCam.sqrMagnitude > 0.01f) phoneRoot.transform.rotation = Quaternion.LookRotation(toCam.normalized, Vector3.up);
                }
            }
            if (phone != null) phone.StartRinging();
        }

        private IEnumerator AssertPassthrough()
        {
            yield return null; yield return null;
            if (!transitioned) XR.PassthroughController.Instance?.EnablePassthrough();
        }

        void OnDisable()
        {
            if (runner != null)
            {
                runner.OnStepActivated -= HandleStepActivated;
                runner.OnScenarioFinished -= HandleFinished;
            }
            if (silenceDetector != null)
            {
                silenceDetector.OnStrike -= HandleNoiseStrike;
                silenceDetector.OnProgress -= HandleSilenceProgress;
            }
        }

        private void HandleStepActivated(Microstep step)
        {
            if (step == null) return;
            switch (step.id)
            {
                case "hide_under_bed":   // budzisz się po wypadku → cel: wczołgaj się POD ŁÓŻKO (schyl głowę)
                    if (!transitioned) StartCoroutine(EnterBedroomThenGuide());
                    break;
                case "answer_phone":     // schowany pod łóżkiem → telefon DZWONI przy Tobie → odbierz SPOJRZENIEM
                    DestroyHideMarker();
                    EnableUnderBedView();
                    ShowPhoneUnderBed();
                    SetUnderBedLine("Telefon dzwoni — ODBIERZ\nspójrz na niego");
                    break;
                case "report_intruder":  // O1 — gracz dzwoni i zgłasza (BEZ głosu dyspozytora przed; on odpowiada dopiero potem)
                    if (phone != null) phone.StopRinging();
                    EnableUnderBedView();
                    DestroyHideMarker();
                    SetPlayerLine(step.label);   // formułka GRACZA — DUŻA CZERWONA
                    break;
                // O2–O5: gracz czyta swoją kwestię (czerwony tekst). Głos DYSPOZYTORA (Paulina) gra Z DETEKTORA Wit
                // (operatorVoiceClip), więc TUTAJ NIE odtwarzamy nic dodatkowego — to było źródło „podwójnego lektora".
                case "give_location":    // O2 — adres
                case "describe_event":   // O3 — „Nie wiem"
                case "count_victims":    // O4 — „Nie"
                case "give_status":      // O5 — „Słyszę go. Chodzi po korytarzu"
                    EnableUnderBedView();
                    DestroyHideMarker();
                    SetPlayerLine(step.label);
                    break;
                case "stay_silent":      // formułki przeczytane → dyspozytor: patrol jedzie → wchodzi intruz → leż cicho
                    SetUnderBedLine("LEŻ CICHO i nie ruszaj się.\nPatrol jest w drodze — czekaj na syreny.");
                    StartCoroutine(DispatcherThenIntruder());
                    break;
            }
        }

        /// <summary>Telefon odebrany: płynne przejście do ciemnej sypialni, potem pokaż cel „pod łóżko".</summary>
        private IEnumerator EnterBedroomThenGuide()
        {
            yield return StartCoroutine(TransitionToHiding());
            // PRZEBUDZENIE po wypadku: zdejmij czerń z Aktu II ("CrashBlackout"), jeśli była — teraz BUDZISZ SIĘ w sypialni.
            var crashBlack = GameObject.Find("CrashBlackout");
            if (crashBlack != null) Destroy(crashBlack);
            SpawnHideMarker();
            EnableUnderBedView();
            SetUnderBedLine("Wczołgaj się POD ŁÓŻKO\ni schowaj się nisko przy podłodze");   // instrukcja na DOLE ekranu (nie w teksturach)
        }

        /// <summary>Ustawia pokój tak, by gracz stał TYŁEM do drzwi i patrzył na łóżko (kierunek drzwi→łóżko = wzrok gracza),
        /// a drzwi były ~0.5 m ZA graczem. Dzięki temu intruz wchodzi za plecami — dokładnie jak w referencji.</summary>
        private void PlaceRoomBackToDoor()
        {
            Camera c = cam != null ? cam : GetCamera();
            if (hidingHolder == null || c == null) return;
            Vector3 camPos = c.transform.position; camPos.y = 0f;
            Vector3 camFwd = c.transform.forward; camFwd.y = 0f;
            if (camFwd.sqrMagnitude < 0.0001f) camFwd = Vector3.forward;
            camFwd.Normalize();
            if (hideSpot == null || roomDoorPivot == null)
            {
                hidingHolder.transform.SetPositionAndRotation(camPos, Quaternion.Euler(0f, c.transform.eulerAngles.y, 0f));
                return;
            }
            hidingHolder.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Physics.SyncTransforms();
            Vector3 bedL = hideSpot.position;
            Vector3 doorL = roomDoorPivot.position;
            Vector3 dir = bedL - doorL; dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
            dir.Normalize();
            Quaternion R = Quaternion.FromToRotation(dir, camFwd);   // door→bed wyrównane z kierunkiem wzroku gracza
            // STOISZ PRZED DRZWIAMI, TYŁEM DO NICH (drzwi ~0.5 m za plecami), twarzą w głąb pokoju (łóżko przed Tobą).
            // Intruz wchodzi drzwiami za plecami. Chowasz się SCHYLAJĄC GŁOWĘ (hojny zasięg — to gest „pod łóżko", nie fizyczne wejście).
            const float doorBehind = 0.5f;
            Vector3 P = camPos - camFwd * doorBehind - R * doorL;
            P.y = 0f;
            hidingHolder.transform.SetPositionAndRotation(P, R);
            Physics.SyncTransforms();
            JSONLLogger.Instance?.LogEvent("act3_room_placed_backtodoor", null);
        }

        private IEnumerator TransitionToHiding()
        {
            transitioned = true;
            if (phone != null) phone.StopRinging();
            JSONLLogger.Instance?.LogEvent("act3_enter_hiding", null);
            cam = GetCamera();

            yield return StartCoroutine(WipeTeleport(() =>
            {
                if (hidingHolder != null)
                {
                    hidingHolder.SetActive(true);
                    PlaceRoomBackToDoor();   // pokój ustawiony tak, że stoisz TYŁEM do drzwi, twarzą do łóżka (intruz wchodzi za plecami)
                }
                ApplyHidingAtmosphere();
                XR.PassthroughController.Instance?.DisablePassthrough();
                if (phoneRoot != null) phoneRoot.SetActive(false);
            }));
        }

        /// <summary>Czarna kurtyna: wjeżdża z GÓRY na DÓŁ (zakrywa), w czerni wykonuje swap, potem zjeżdża dalej (odsłania).</summary>
        private IEnumerator WipeTeleport(System.Action onCovered)
        {
            var c = cam != null ? cam : GetCamera();
            if (c == null) { onCovered?.Invoke(); yield break; }
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad); quad.name = "TeleportWipe";
            var col = quad.GetComponent<Collider>(); if (col != null) Destroy(col);
            var sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var m = new Material(sh); if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.black); else m.color = Color.black;
            quad.GetComponent<Renderer>().sharedMaterial = m;
            quad.transform.SetParent(c.transform, false);
            const float dist = 0.32f, H = 3.2f, W = 4f;
            quad.transform.localScale = new Vector3(W, H, 1f);
            quad.transform.localRotation = Quaternion.identity;
            float t = 0f;
            while (t < 1f) { t += Time.deltaTime / 0.55f; float s = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)); quad.transform.localPosition = new Vector3(0f, Mathf.Lerp(H, 0f, s), dist); yield return null; }
            quad.transform.localPosition = new Vector3(0f, 0f, dist);
            onCovered?.Invoke();
            yield return new WaitForSeconds(0.15f);
            t = 0f;
            while (t < 1f) { t += Time.deltaTime / 0.55f; float s = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)); quad.transform.localPosition = new Vector3(0f, Mathf.Lerp(0f, -H, s), dist); yield return null; }
            Destroy(quad);
        }

        /// <summary>Pierścień + tekst „wczołgaj się pod łóżko" przy łóżku, zwrócony do gracza.</summary>
        private void SpawnHideMarker()
        {
            DestroyHideMarker();
            var c = GetCamera();
            Vector3 pos = hideSpot != null ? hideSpot.position
                : (c != null ? c.transform.position + Vector3.ProjectOnPlane(c.transform.forward, Vector3.up).normalized * 1.6f : Vector3.zero);

            hideMarker = new GameObject("HideMarker");
            // pierścień na podłodze
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder); ring.name = "HideRing";
            var col = ring.GetComponent<Collider>(); if (col != null) Destroy(col);
            ring.transform.SetParent(hideMarker.transform, false);
            ring.transform.position = new Vector3(pos.x, 0.02f, pos.z);
            ring.transform.localScale = new Vector3(0.7f, 0.012f, 0.7f);
            var sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var rm = new Material(sh); var rc = new Color(0.3f, 0.85f, 1f);
            if (rm.HasProperty("_BaseColor")) rm.SetColor("_BaseColor", rc); else rm.color = rc;
            ring.GetComponent<Renderer>().sharedMaterial = rm;
            // BELKA-znacznik nad łóżkiem — wyraźny słup „TU IDŹ", widoczny z drugiego końca pokoju.
            // (Tekst instrukcji jest na DOLE ekranu przy graczu, NIE w teksturach łóżka.)
            var beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder); beam.name = "HideBeam";
            var bcol = beam.GetComponent<Collider>(); if (bcol != null) Destroy(bcol);
            beam.transform.SetParent(hideMarker.transform, false);
            beam.transform.position = new Vector3(pos.x, 0.62f, pos.z);
            beam.transform.localScale = new Vector3(0.12f, 0.62f, 0.12f);   // słup od podłogi do BLATU łóżka (~1.24 m), nie pod sufit
            var bm = new Material(sh); var bcl = new Color(0.3f, 0.85f, 1f);
            if (bm.HasProperty("_BaseColor")) bm.SetColor("_BaseColor", bcl); else bm.color = bcl;
            beam.GetComponent<Renderer>().sharedMaterial = bm;
            beam.AddComponent<MRCrisisTrainer.UI.UIPulse>();

            // STRZAŁKA „W DÓŁ" na szczycie belki — wyraźnie pokazuje, GDZIE się wczołgać (geometryczna, niezależna od fontu)
            var arrowMat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
            var aCol = new Color(0.3f, 0.85f, 1f);
            if (arrowMat.HasProperty("_BaseColor")) arrowMat.SetColor("_BaseColor", aCol); else arrowMat.color = aCol;
            var arrow = new GameObject("HideArrow"); arrow.transform.SetParent(hideMarker.transform, false);
            arrow.transform.position = new Vector3(pos.x, 1.5f, pos.z);   // TUŻ nad blatem łóżka, wskazuje w dół DOKŁADNIE na łóżko (nie wisi pod sufitem)
            MakeArrowBar(arrow.transform, new Vector3(0f, 0.28f, 0f), new Vector3(0.11f, 0.78f, 0.11f), 0f, arrowMat);    // trzon (DUŻY, dobrze widoczny)
            MakeArrowBar(arrow.transform, new Vector3(-0.19f, -0.19f, 0f), new Vector3(0.11f, 0.52f, 0.11f), 45f, arrowMat);  // lewe ramię grota
            MakeArrowBar(arrow.transform, new Vector3(0.19f, -0.19f, 0f), new Vector3(0.11f, 0.52f, 0.11f), -45f, arrowMat); // prawe ramię grota
            arrow.AddComponent<MRCrisisTrainer.UI.UIPulse>();   // pulsuje → przyciąga wzrok
            arrow.AddComponent<MRCrisisTrainer.UI.FaceCamera>();   // zwrócona do gracza → czyta się jako ↓ z każdej strony
        }

        private void MakeArrowBar(Transform parent, Vector3 localPos, Vector3 localScale, float zRotDeg, Material mat)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Cube); b.name = "ArrowBar";
            var col = b.GetComponent<Collider>(); if (col != null) Destroy(col);
            b.transform.SetParent(parent, false);
            b.transform.localPosition = localPos;
            b.transform.localRotation = Quaternion.Euler(0f, 0f, zRotDeg);
            b.transform.localScale = localScale;
            b.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private void DestroyHideMarker()
        {
            if (hideMarker != null) { Destroy(hideMarker); hideMarker = null; }
        }

        /// <summary>Widok „spod łóżka": dwa czarne pasy (góra/dół) przyklejone do kamery, zostawiające poziomą szparę,
        /// plus „przyklejona" do głowy linijka z formułkami (bliżej niż pasy, więc zawsze czytelna w szparze).</summary>
        private void EnableUnderBedView()
        {
            if (underBedView != null) return;
            var c = GetCamera(); if (c == null) return;
            underBedView = new GameObject("UnderBedView");
            underBedView.transform.SetParent(c.transform, false);
            // BEZ czarnych pasów — NIE zwężamy pola widzenia (życzenie użytkownika). Sama formułka na DOLE ekranu, blisko, czytelna.
            var lineGo = new GameObject("UnderBedLine"); lineGo.transform.SetParent(c.transform, false);
            lineGo.transform.localPosition = new Vector3(0f, -0.13f, 0.34f);   // DÓŁ ekranu, BARDZO BLISKO kamery (bliżej niż geometria pokoju)
            lineGo.transform.localRotation = Quaternion.identity;
            underBedLine = lineGo.AddComponent<TextMeshPro>();
            underBedLine.alignment = TextAlignmentOptions.Center;
            underBedLine.fontSize = 0.07f;   // WIĘKSZE w kadrze (bo blisko), dobrze widoczne w goglach
            underBedLine.fontStyle = FontStyles.Bold;
            underBedLine.color = new Color(0.96f, 0.98f, 1f);
            underBedLine.outlineWidth = 0.22f; underBedLine.outlineColor = new Color32(0, 0, 0, 220);   // czarny kontur = czytelne na każdym tle
            underBedLine.rectTransform.sizeDelta = new Vector2(1.2f, 0.8f);
            underBedLine.text = "";
            ForceOnTop(underBedLine);
            JSONLLogger.Instance?.LogEvent("act3_under_bed_view_on", null);
        }

        /// <summary>Wymusza render NA WIERZCHU (ZTest Always + kolejka Overlay) → napis NIGDY nie wchodzi w tekstury.</summary>
        private void ForceOnTop(TextMeshPro t)
        {
            if (t == null) return;
            var m = t.fontMaterial;   // instancja materiału tego napisu
            if (m != null)
            {
                m.SetFloat("_ZTestMode", (float)UnityEngine.Rendering.CompareFunction.Always);   // pełny shader SDF
                m.SetFloat("unity_GUIZTestMode", (float)UnityEngine.Rendering.CompareFunction.Always);   // wariant mobilny
                m.renderQueue = 4000;   // Overlay — rysowane na końcu
            }
            var rend = t.GetComponent<Renderer>();
            if (rend != null) rend.sortingOrder = 32760;
        }

        private void SetUnderBedLine(string text)
        {
            if (underBedLine == null) return;
            underBedLine.text = text;
            underBedLine.color = new Color(0.96f, 0.98f, 1f);   // instrukcje = białe
            underBedLine.fontSize = 0.07f;
            ForceOnTop(underBedLine);
        }

        /// <summary>Formułka GRACZA (co mówić do 112) — DUŻA CZERWONA czcionka, na wierzchu, na dole ekranu.</summary>
        private void SetPlayerLine(string text)
        {
            if (underBedLine == null) return;
            underBedLine.text = text;
            underBedLine.color = new Color(1f, 0.16f, 0.12f);   // CZERWONA — co MÓWIĆ
            underBedLine.fontSize = 0.092f;                      // DUŻA
            ForceOnTop(underBedLine);
        }

        private void MakeBar(Transform parent, Vector3 localPos, Vector3 localScale)
        {
            var bar = GameObject.CreatePrimitive(PrimitiveType.Quad); bar.name = "UBBar";
            var col = bar.GetComponent<Collider>(); if (col != null) Destroy(col);
            bar.transform.SetParent(parent, false);
            bar.transform.localPosition = localPos;
            bar.transform.localRotation = Quaternion.identity;
            bar.transform.localScale = localScale;
            var sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var m = new Material(sh); if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.black); else m.color = Color.black;
            bar.GetComponent<Renderer>().sharedMaterial = m;
        }

        private void DisableUnderBedView()
        {
            if (underBedView != null) { Destroy(underBedView); underBedView = null; }
            if (underBedLine != null) { Destroy(underBedLine.gameObject); underBedLine = null; }   // napis to OSOBNY obiekt — kasuj, by nie został na ekranie końcowym
        }

        /// <summary>Po formułkach: dyspozytor mówi, że patrol jedzie; potem DRZWI się otwierają i wchodzi intruz.</summary>
        private IEnumerator DispatcherThenIntruder()
        {
            JSONLLogger.Instance?.LogEvent("act3_dispatcher_eta", null);
            micBar?.Show();
            // NAPISY DYSPOZYTORA WYŁĄCZONE (życzenie użytkownika) — na ekranie zostaje tylko instrukcja gracza „LEŻ CICHO".
            yield return new WaitForSeconds(4f);

            JSONLLogger.Instance?.LogEvent("act3_thief_appears", null);
            yield return StartCoroutine(OpenDoorThenIntruder());
        }

        /// <summary>Otwórz PRAWDZIWE drzwi pokoju (na zawiasie), potem intruz wchodzi i przeszukuje.</summary>
        private IEnumerator OpenDoorThenIntruder()
        {
            if (roomDoorPivot != null)
            {
                if (!string.IsNullOrEmpty(doorClip)) AudioManager.Instance?.PlaySfx(doorClip);
                Quaternion closed = Quaternion.identity, open = Quaternion.Euler(0f, 82f, 0f); // do środka pokoju (w stronę gracza)
                float t = 0f;
                while (t < 1f)
                {
                    t += Time.deltaTime / 1.1f;
                    float s = t * t * (3f - 2f * t);
                    roomDoorPivot.localRotation = Quaternion.Slerp(closed, open, s);
                    yield return null;
                }
                roomDoorPivot.localRotation = open;
                yield return new WaitForSeconds(0.2f);
            }
            if (thief != null)
            {
                if (roomDoorPivot != null) thief.transform.position = new Vector3(roomDoorPivot.position.x, 0f, roomDoorPivot.position.z);   // intruz wchodzi PRZEZ drzwi
                thief.Appear();
            }
        }

        private TextMeshPro ShowDispatcherSubtitle(string text)
        {
            var c = GetCamera(); if (c == null) return null;
            var go = new GameObject("DispatcherSubtitle");
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = 0.9f; tmp.alignment = TextAlignmentOptions.Center; tmp.color = new Color(0.85f, 0.95f, 1f);
            tmp.rectTransform.sizeDelta = new Vector2(4.5f, 1.6f);
            Vector3 fwd = c.transform.forward;
            go.transform.position = c.transform.position + fwd * 1.3f - c.transform.up * 0.45f;   // NA DOLE ekranu
            go.transform.rotation = Quaternion.LookRotation(go.transform.position - c.transform.position);
            return tmp;
        }

        private void ApplyHidingAtmosphere()
        {
            savedAmbient = RenderSettings.ambientLight;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = hidingAmbient;

            if (cam == null) cam = GetCamera();
            if (cam != null)
            {
                savedClear = cam.clearFlags;
                savedBg = cam.backgroundColor;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = hidingBackground;
            }
        }

        private void HandleNoiseStrike(int strikes)
        {
            JSONLLogger.Instance?.LogEvent("act3_noise_strike", new System.Collections.Generic.Dictionary<string, object> { { "strike", strikes } });
            micBar?.FlashAlarm();
            if (strikes >= 2)   // 2. strike = detektor i tak faila („too_loud") → wtedy intruz ZNAJDUJE i skacze przed twarz (horror)
            {
                caught = true;
                var c = GetCamera();
                if (c != null) thief?.Jumpscare(c.transform);
                JSONLLogger.Instance?.LogEvent("act3_jumpscare", null);
            }
            else thief?.OnNoiseDetected();
        }

        private void HandleSilenceProgress(float elapsed, float required)
        {
            micBar?.SetRemaining(Mathf.Max(0f, required - elapsed), elapsed / Mathf.Max(0.01f, required));
        }

        private void HandleFinished(int total, int max)
        {
            StartCoroutine(EndScene());
        }

        private IEnumerator EndScene()
        {
            micBar?.Hide();
            if (caught)
            {
                // ZŁAPANY — jumpscare gra chwilę, potem CZARNY EKRAN: „byłeś za głośny, przegrałeś" + SPRÓBUJ PONOWNIE.
                JSONLLogger.Instance?.LogEvent("act3_caught", null);
                yield return new WaitForSeconds(2.0f);   // jumpscare zdąży zagrać
                DisableUnderBedView();
                RenderSettings.ambientLight = savedAmbient;
                LoadGameOver("BYŁEŚ ZA GŁOŚNY\nPRZEGRAŁEŚ", false);
                yield break;
            }
            JSONLLogger.Instance?.LogEvent("act3_police_arrival", null);
            if (!string.IsNullOrEmpty(sirenClip)) AudioManager.Instance?.PlaySfx(sirenClip);
            if (thief != null) thief.Flee(thiefFleeTarget);
            yield return new WaitForSeconds(4f);
            DisableUnderBedView();
            RenderSettings.ambientLight = savedAmbient;
            LoadGameOver("UDAŁO SIĘ PRZEJŚĆ GRĘ", true);   // KONIEC GRY → osobna czarna scena GameOver (bez testów)
        }

        /// <summary>KONIEC GRY → ładuje OSOBNĄ czarną scenę „GameOver" (czarne wnętrze + napis + restart na SPUST).
        /// Wiadomość i wynik przekazywane przez PlayerPrefs. GameOverController w tej scenie je czyta.</summary>
        private void LoadGameOver(string message, bool isWin)
        {
            JSONLLogger.Instance?.LogEvent(isWin ? "game_complete" : "game_over_caught", null);
            PlayerPrefs.SetString("end_message", message);
            PlayerPrefs.SetInt("end_is_win", isWin ? 1 : 0);
            PlayerPrefs.Save();
            UnityEngine.SceneManagement.SceneManager.LoadScene("GameOver");
        }

        /// <summary>(NIEUŻYWANE — zastąpione sceną GameOver) Pełny czarny ekran na kamerze + napis.</summary>
        private void ShowEndScreen(string message, Color color)
        {
            var c = GetCamera(); if (c == null) return;
            var sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var black = GameObject.CreatePrimitive(PrimitiveType.Quad); black.name = "EndBlackout";
            var col = black.GetComponent<Collider>(); if (col != null) Destroy(col);
            black.transform.SetParent(c.transform, false);
            black.transform.localPosition = new Vector3(0f, 0f, 0.18f);   // BARDZO blisko oczu → NIC nie jest bliżej, więc zakrywa CAŁY kadr (koniec prześwitów łóżka)
            black.transform.localRotation = Quaternion.identity;
            black.transform.localScale = new Vector3(2.4f, 1.8f, 1f);
            var bm = new Material(sh); if (bm.HasProperty("_BaseColor")) bm.SetColor("_BaseColor", Color.black); else bm.color = Color.black;
            bm.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
            bm.renderQueue = 3999;
            black.GetComponent<Renderer>().sharedMaterial = bm;
            var txtGo = new GameObject("EndText"); txtGo.transform.SetParent(c.transform, false);
            txtGo.transform.localPosition = new Vector3(0f, 0f, 0.16f);
            txtGo.transform.localRotation = Quaternion.identity;
            var tmp = txtGo.AddComponent<TextMeshPro>();
            tmp.text = message;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 0.05f; tmp.fontStyle = FontStyles.Bold;
            tmp.color = color;
            tmp.rectTransform.sizeDelta = new Vector2(0.42f, 0.32f);
            ForceOnTop(tmp);
            JSONLLogger.Instance?.LogEvent("game_complete", null);
            StartCoroutine(RetryOnButton());   // SPRÓBUJ PONOWNIE — po naciśnięciu spustu
        }

        /// <summary>Czeka na naciśnięcie dowolnego przycisku kontrolera, potem restartuje grę (przeładowanie sceny).</summary>
        private IEnumerator RetryOnButton()
        {
            yield return new WaitForSeconds(1.5f);   // krótka zwłoka — nie restartuj od razu (palec na spuście)
            while (true)
            {
                if (AnyControllerButton())
                {
                    JSONLLogger.Instance?.LogEvent("retry_pressed", null);
                    PlayerPrefs.SetInt("retry_room_act", 1); PlayerPrefs.Save();   // restart NIE od początku gry, tylko od AKTU Z POKOJEM
                    var sc = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                    UnityEngine.SceneManagement.SceneManager.LoadScene(sc.name);
                    yield break;
                }
                yield return null;
            }
        }

        private bool AnyControllerButton()
        {
            foreach (var node in new[] { UnityEngine.XR.XRNode.RightHand, UnityEngine.XR.XRNode.LeftHand })
            {
                var dev = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(node);
                if (!dev.isValid) continue;
                if (dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out bool tr) && tr) return true;
                if (dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool pr) && pr) return true;
                if (dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryButton, out bool se) && se) return true;
                if (dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.gripButton, out bool gr) && gr) return true;
            }
            return false;
        }

        private Camera GetCamera()
        {
            var c = Camera.main;
            if (c == null) c = FindFirstObjectByType<Camera>();
            return c;
        }
    }
}
