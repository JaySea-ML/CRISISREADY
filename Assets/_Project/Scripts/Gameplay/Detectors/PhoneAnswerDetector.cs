using System.Collections.Generic;
using UnityEngine;
using TMPro;
using XRNode = UnityEngine.XR.XRNode;
using XRInputDevices = UnityEngine.XR.InputDevices;
using XRCommonUsages = UnityEngine.XR.CommonUsages;
using MRCrisisTrainer.XR;
using MRCrisisTrainer.Logging;

namespace MRCrisisTrainer.Gameplay.Detectors
{
    /// <summary>
    /// NIEZAWODNY odbiór telefonu w SIEDZĄCYM MR. Zalicza krok, gdy zajdzie KTÓREKOLWIEK z:
    ///   • GAZE — gracz PATRZY na dzwoniący telefon przez chwilę (gazeHold). To działa ZAWSZE,
    ///     bo korzysta tylko z kierunku patrzenia kamery (HMD) — bez zależności od śledzenia dłoni/padów.
    ///   • REACH — dłoń blisko telefonu (gdy śledzenie dłoni/kontrolerów działa) — wrażenie „biorę go do ręki".
    ///   • BUTTON — naciśnięcie spustu/grip/przycisku na kontrolerze.
    /// Wcześniej krok zależał WYŁĄCZNIE od precyzyjnej pozycji dłoni (GrabDetector), która na Quest
    /// często nie dochodziła (OpenXR + Input System / hand-tracking) → telefonu „nie dało się złapać",
    /// a krok ruszał dopiero po 38 s timeoutu. Pokazuje wyraźny prompt i pierścień postępu spojrzenia.
    /// </summary>
    public class PhoneAnswerDetector : MicrostepDetector
    {
        [SerializeField] private Transform phone;
        [SerializeField] private MonoBehaviour handProviderBehaviour;   // opcjonalnie (reach)
        [SerializeField] private float reachRadius = 0.85f;
        [SerializeField] private float gazeAngle = 26f;      // stożek spojrzenia (stopnie)
        [SerializeField] private float gazeDistance = 3.0f;  // maks. odległość telefonu
        [SerializeField] private float gazeHold = 1.0f;      // ile s patrzeć, by odebrać
        [SerializeField] private float maxSeconds = 30f;     // awaryjnie i tak przejdź dalej (0 = nigdy)
        [SerializeField] private bool raiseToFaceOnAnswer = true;

        private IHandPoseProvider hands;
        private Camera cam;
        private float elapsed, gaze;
        private bool answered;
        private TextMeshPro prompt;
        private Transform ring;

        void Awake() => hands = handProviderBehaviour as IHandPoseProvider;

        protected override void OnBegin()
        {
            elapsed = 0f; gaze = 0f; answered = false;
            cam = ResolveCam();
            BuildPrompt();
            Log("start", -1f);
        }

        protected override void OnCancel() => Cleanup();

        void Update()
        {
            if (!IsActive || answered || phone == null) return;
            if (cam == null) cam = ResolveCam();
            elapsed += Time.deltaTime;

            // 1) PRZYCISK — gdy pady działają (natychmiast)
            if (AnyButton()) { Answer("button"); return; }

            // 2) DŁOŃ blisko telefonu — gdy śledzenie dłoni/padów działa
            if (hands != null && (Near(HandSide.Left) || Near(HandSide.Right))) { Answer("reach"); return; }

            // 3) SPOJRZENIE — zawsze dostępne (kierunek kamery)
            if (cam != null && Gazing())
            {
                gaze += Time.deltaTime;
                UpdateRing(gaze / Mathf.Max(0.01f, gazeHold));
                if (gaze >= gazeHold) { Answer("gaze"); return; }
            }
            else
            {
                gaze = Mathf.Max(0f, gaze - Time.deltaTime * 1.5f);
                UpdateRing(gaze / Mathf.Max(0.01f, gazeHold));
            }

            if (maxSeconds > 0f && elapsed >= maxSeconds) Answer("timeout");
        }

        private bool Near(HandSide s)
        {
            if (!hands.TryGetHandPosition(s, out var p)) return false;
            return (p - phone.position).sqrMagnitude <= reachRadius * reachRadius;
        }

        private bool Gazing()
        {
            Vector3 to = phone.position - cam.transform.position;
            if (to.sqrMagnitude > gazeDistance * gazeDistance) return false;
            return Vector3.Angle(cam.transform.forward, to) <= gazeAngle;
        }

        private static bool AnyButton() => Btn(XRNode.RightHand) || Btn(XRNode.LeftHand);

        private static bool Btn(XRNode node)
        {
            var d = XRInputDevices.GetDeviceAtXRNode(node);
            if (!d.isValid) return false;
            if (d.TryGetFeatureValue(XRCommonUsages.triggerButton, out bool t) && t) return true;
            if (d.TryGetFeatureValue(XRCommonUsages.gripButton, out bool g) && g) return true;
            if (d.TryGetFeatureValue(XRCommonUsages.primaryButton, out bool p) && p) return true;
            return false;
        }

        private void Answer(string how)
        {
            if (answered) return;
            answered = true;
            Log("answer", -1f, how);
            if (raiseToFaceOnAnswer && cam != null && phone != null)
                phone.position = cam.transform.position + cam.transform.forward * 0.32f - cam.transform.up * 0.08f;
            Cleanup();
            Complete();
        }

        // ===== Prompt + pierścień postępu (świat) =====
        private void BuildPrompt()
        {
            if (phone == null) return;
            var go = new GameObject("PhoneAnswerPrompt");
            go.transform.SetParent(phone, false);
            go.transform.localPosition = new Vector3(0f, 0.34f, 0f);   // nad telefonem
            prompt = go.AddComponent<TextMeshPro>();
            prompt.alignment = TextAlignmentOptions.Center;
            prompt.fontSize = 0.6f;
            prompt.fontStyle = FontStyles.Bold;
            prompt.color = new Color(0.45f, 0.9f, 1f);
            prompt.rectTransform.sizeDelta = new Vector2(2.2f, 0.9f);
            prompt.text = "ODBIERZ TELEFON\nspójrz na niego (lub naciśnij spust)";
            go.AddComponent<MRCrisisTrainer.UI.FaceCamera>();

            // pierścień postępu spojrzenia — cienki dysk, skaluje się 0→1
            var r = GameObject.CreatePrimitive(PrimitiveType.Cylinder); r.name = "GazeRing";
            var col = r.GetComponent<Collider>(); if (col != null) Destroy(col);
            r.transform.SetParent(phone, false);
            r.transform.localPosition = new Vector3(0f, 0.16f, 0f);
            r.transform.localScale = new Vector3(0.001f, 0.006f, 0.001f);
            var sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var m = new Material(sh); var c = new Color(0.3f, 0.9f, 1f);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c); else m.color = c;
            r.GetComponent<Renderer>().sharedMaterial = m;
            ring = r.transform;
        }

        private void UpdateRing(float p)
        {
            if (ring == null) return;
            float s = Mathf.Clamp01(p) * 0.22f;
            ring.localScale = new Vector3(s, 0.006f, s);
        }

        private void Cleanup()
        {
            if (prompt != null) { Destroy(prompt.gameObject); prompt = null; }
            if (ring != null) { Destroy(ring.gameObject); ring = null; }
        }

        private static Camera ResolveCam()
        {
            var c = Camera.main;
#if UNITY_2023_1_OR_NEWER
            if (c == null) c = UnityEngine.Object.FindFirstObjectByType<Camera>();
#else
            if (c == null) c = UnityEngine.Object.FindObjectOfType<Camera>();
#endif
            return c;
        }

        private void Log(string phase, float ignored, string how = null)
        {
            bool lt = hands != null && hands.IsTracked(HandSide.Left);
            bool rt = hands != null && hands.IsTracked(HandSide.Right);
            JSONLLogger.Instance?.LogEvent("phone_answer", new Dictionary<string, object>
            {
                { "phase", phase }, { "how", how ?? "" },
                { "left_tracked", lt }, { "right_tracked", rt }, { "elapsed_s", elapsed }
            });
        }
    }
}
