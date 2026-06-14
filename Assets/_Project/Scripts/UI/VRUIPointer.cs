using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Management;
using MRCrisisTrainer.XR;
using XRInputDevice = UnityEngine.XR.InputDevice;
using XRInputDevices = UnityEngine.XR.InputDevices;
using XRInputDeviceCharacteristics = UnityEngine.XR.InputDeviceCharacteristics;
using XRCommonUsages = UnityEngine.XR.CommonUsages;

namespace MRCrisisTrainer.UI
{
    /// <summary>
    /// Wskaźnik UI „tylko ręce": LASER Z PALCA (gdy dłoń śledzona) + retikuła; klik = pinch.
    /// Fallback gdy brak rąk: promień ze wzroku (gaze) + dwell ~1.8 s. Zawsze działa, bez kontrolerów.
    /// </summary>
    public class VRUIPointer : MonoBehaviour
    {
        [SerializeField] private XRHandsPoseProvider hands;
        [SerializeField] private float maxDistance = 8f;
        [SerializeField] private float pinchThreshold = 0.05f;   // łatwiejszy pinch (było 0.035 — za precyzyjnie)
        [SerializeField] private float dwellTime = 1.3f;
        [SerializeField] private GameObject reticlePrefab;
        [SerializeField] private Color rayColor = new Color(0.3f, 0.78f, 1f, 0.9f);

        private Transform cam;
        private Selectable hovered, lastHovered;
        private float dwell;
        private readonly List<XRInputDevice> devices = new List<XRInputDevice>();
        private XRHandSubsystem handSubsystem;
        private GameObject reticle;
        private LineRenderer ray;
        private bool confirmLatch;
        private Vector3 reticleBaseScale;
        private Renderer reticleRenderer;
        private MaterialPropertyBlock reticleMpb;
        private Selectable lastValidHovered;
        private float lastValidHoverTime = -1f;
        private Graphic hiGraphic;       // podświetlony przycisk pod kursorem
        private Color hiOrig;

        // --- wygładzanie promienia z ręki (śledzenie dłoni drży; krótka baza palca skacze) ---
        private bool raySmoothInit;
        private Vector3 smoothOrigin, smoothDir;
        [SerializeField] private float raySmoothing = 18f;   // wyżej = szybciej podąża, niżej = stabilniej
        // --- histereza pinch: łatwiej ZACZĄĆ klik, trudniej go przypadkiem zgubić (drżenie) ---
        private bool pinchLatched;

        void Start()
        {
            if (Camera.main != null) cam = Camera.main.transform;
            reticle = reticlePrefab != null ? Instantiate(reticlePrefab) : BuildReticle();   // ZAWSZE jest celownik
            reticle.SetActive(true);   // prefab bywa nieaktywny — instancja MUSI być widoczna (inaczej brak celownika!)
            reticleBaseScale = reticle.transform.localScale;
            reticleMpb = new MaterialPropertyBlock();
            reticleRenderer = reticle.GetComponentInChildren<Renderer>();
            BuildRay();
        }

        /// <summary>Proceduralny celownik (jasna kropka) — żeby ZAWSZE było widać gdzie celujemy, bez prefab.</summary>
        private GameObject BuildReticle()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var col = go.GetComponent<Collider>(); if (col != null) Destroy(col);
            go.name = "AimReticle";
            go.transform.localScale = Vector3.one * 0.03f;
            var sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            var m = new Material(sh);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", rayColor); else m.color = rayColor;
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }

        private void SetReticleColor(Color c)
        {
            if (reticleRenderer == null || reticleMpb == null) return;
            reticleRenderer.GetPropertyBlock(reticleMpb);
            reticleMpb.SetColor("_BaseColor", c);
            reticleMpb.SetColor("_Color", c);
            reticleRenderer.SetPropertyBlock(reticleMpb);
        }

        /// <summary>Podświetla przycisk pod kursorem (tint w stronę cyjanu), przywraca kolor po zejściu.</summary>
        private void UpdateHoverHighlight(Selectable s)
        {
            Graphic g = s != null ? s.targetGraphic as Graphic : null;
            if (g == hiGraphic) return;
            if (hiGraphic != null) hiGraphic.color = hiOrig;
            hiGraphic = g;
            if (hiGraphic != null) { hiOrig = hiGraphic.color; hiGraphic.color = Color.Lerp(hiOrig, new Color(0.3f, 0.85f, 1f), 0.55f); }
        }

        private void BuildRay()
        {
            var go = new GameObject("HandRay");
            ray = go.AddComponent<LineRenderer>();
            ray.useWorldSpace = true;
            ray.positionCount = 2;
            ray.widthMultiplier = 0.006f;
            ray.numCapVertices = 4;
            var sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            var m = new Material(sh);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", rayColor); else m.color = rayColor;
            ray.material = m;
            ray.enabled = false;
        }

        void Update()
        {
            if (cam == null && Camera.main != null) cam = Camera.main.transform;
            if (cam == null) return;

            // 1) wyznacz promień: PALEC (gdy dłoń śledzona) albo WZROK
            bool fromHand = false;
            Vector3 ro = cam.position, rd = cam.forward;
            if (hands != null)
            {
                if (hands.TryGetPointer(HandSide.Right, out var o, out var d)) { ro = o; rd = d; fromHand = true; }
                else if (hands.TryGetPointer(HandSide.Left, out o, out d)) { ro = o; rd = d; fromHand = true; }
            }

            // WYGŁADZANIE: surowy promień z palca drży (krótka baza prox→tip + szum trackingu).
            // Wykładnicze wygładzanie origin+kierunku → stabilny laser, dużo łatwiej utrzymać celownik na przycisku.
            if (fromHand)
            {
                if (!raySmoothInit) { smoothOrigin = ro; smoothDir = rd; raySmoothInit = true; }
                float k = 1f - Mathf.Exp(-raySmoothing * Time.unscaledDeltaTime);
                smoothOrigin = Vector3.Lerp(smoothOrigin, ro, k);
                smoothDir = Vector3.Slerp(smoothDir, rd, k).normalized;
                ro = smoothOrigin; rd = smoothDir;
            }
            else raySmoothInit = false;

            hovered = Raycast(ro, rd, out Vector3 hitPoint, out float hitDist);

            // dwell tylko dla wzroku (z palca klikamy pinchem)
            if (hovered != null && !fromHand)
            {
                if (hovered == lastHovered) dwell += Time.unscaledDeltaTime;
                else { dwell = 0f; lastHovered = hovered; }
            }
            else { dwell = 0f; lastHovered = hovered; }

            // zapamiętaj ostatni trafiony przycisk (okno tolerancji dla drżącej ręki)
            if (hovered != null) { lastValidHovered = hovered; lastValidHoverTime = Time.unscaledTime; }

            // retikuła — ZAWSZE widoczna; zielona nad klikalnym, cyjan w powietrzu
            if (reticle != null)
            {
                Vector3 pos = hovered != null ? hitPoint : ro + rd * 2.5f;
                reticle.transform.position = pos;
                reticle.transform.rotation = Quaternion.LookRotation(pos - cam.position);
                float grow = (hovered != null && !fromHand) ? 1f + 2.2f * Mathf.Clamp01(dwell / dwellTime) : (hovered != null ? 1.8f : 1f);
                reticle.transform.localScale = reticleBaseScale * grow;
                SetReticleColor(hovered != null ? new Color(0.2f, 1f, 0.45f) : rayColor);
            }
            UpdateHoverHighlight(hovered);

            // laser z palca
            if (ray != null)
            {
                if (fromHand)
                {
                    ray.enabled = true;
                    Vector3 end = hovered != null ? hitPoint : ro + rd * 2.5f;
                    ray.SetPosition(0, ro);
                    ray.SetPosition(1, end);
                }
                else ray.enabled = false;
            }

            // aktywacja
            bool confirm = ConfirmDown();
            bool instant = confirm && !confirmLatch && hovered != null;
            // OKNO TOLERANCJI: pinch chwilę po zejściu z przycisku i tak klika ostatni (ręka drży / kursor ucieka)
            bool grace = confirm && !confirmLatch && hovered == null && lastValidHovered != null
                         && (Time.unscaledTime - lastValidHoverTime) < 0.45f;
            bool dwellDone = hovered != null && !fromHand && dwell >= dwellTime;
            if (instant || dwellDone || grace)
            {
                Activate(hovered != null ? hovered : lastValidHovered);
                dwell = 0f; lastHovered = null; lastValidHovered = null; lastValidHoverTime = -1f;
            }
            confirmLatch = confirm;
        }

        private Selectable Raycast(Vector3 origin, Vector3 dir, out Vector3 hitPoint, out float hitDist)
        {
            hitPoint = Vector3.zero; hitDist = maxDistance;
            Selectable best = null;
            Ray r = new Ray(origin, dir);
#if UNITY_2023_1_OR_NEWER
            var selectables = Object.FindObjectsByType<Selectable>(FindObjectsSortMode.None);
#else
            var selectables = Object.FindObjectsOfType<Selectable>();
#endif
            foreach (var s in selectables)
            {
                if (!s.interactable || !s.gameObject.activeInHierarchy) continue;
                var rt = s.transform as RectTransform;
                if (rt == null) continue;
                if (RayHitsRect(r, rt, out Vector3 hp, out float dist) && dist < hitDist)
                { hitDist = dist; best = s; hitPoint = hp; }
            }
            return best;
        }

        private static readonly Vector3[] corners = new Vector3[4];
        private bool RayHitsRect(Ray ray, RectTransform rt, out Vector3 hitPoint, out float dist)
        {
            hitPoint = Vector3.zero; dist = 0;
            rt.GetWorldCorners(corners);
            Vector3 normal = Vector3.Cross(corners[1] - corners[0], corners[3] - corners[0]).normalized;
            var plane = new Plane(normal, corners[0]);
            if (!plane.Raycast(ray, out float enter) || enter > maxDistance) return false;
            Vector3 p = ray.GetPoint(enter);
            Vector3 u = corners[3] - corners[0], v = corners[1] - corners[0], w = p - corners[0];
            float uu = Vector3.Dot(u, u), vv = Vector3.Dot(v, v);
            float pu = Vector3.Dot(w, u), pv = Vector3.Dot(w, v);
            // 4 cm zapasu wokół przycisku → DUŻO łatwiej trafić małe przyciski (ankiety na końcu!)
            const float margin = 0.04f;
            float mu = margin * Mathf.Sqrt(uu), mv = margin * Mathf.Sqrt(vv);
            if (pu < -mu || pu > uu + mu || pv < -mv || pv > vv + mv) return false;
            hitPoint = p; dist = enter; return true;
        }

        private void Activate(Selectable s)
        {
            if (s == null) return;
            if (s is Button b) b.onClick.Invoke();
            else if (s is Toggle t) t.isOn = !t.isOn;
            Core.AudioManager.Instance?.PlaySfx("ui_click");
        }

        private bool ConfirmDown()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && (kb.enterKey.isPressed || kb.spaceKey.isPressed)) return true;

            XRInputDevices.GetDevices(devices);
            foreach (var d in devices)
            {
                if (!d.isValid || (d.characteristics & XRInputDeviceCharacteristics.Controller) == 0) continue;
                if (d.TryGetFeatureValue(XRCommonUsages.triggerButton, out bool tr) && tr) return true;
                if (d.TryGetFeatureValue(XRCommonUsages.primaryButton, out bool pr) && pr) return true;
                if (d.TryGetFeatureValue(XRCommonUsages.gripButton, out bool gr) && gr) return true;
            }

            if (handSubsystem == null)
            {
                var loader = XRGeneralSettings.Instance?.Manager?.activeLoader;
                if (loader != null) handSubsystem = loader.GetLoadedSubsystem<XRHandSubsystem>();
            }
            if (handSubsystem != null)
            {
                // HISTEREZA: próg domknięcia = pinchThreshold; próg rozwarcia = 1.6× (większy luz).
                // Raz złapany pinch trzyma się aż do WYRAŹNEGO rozwarcia → klik nie ucieka przy drżeniu ręki.
                float close = pinchThreshold;
                float open = pinchThreshold * 1.6f;
                float gap = MinPinchGap();
                if (gap < 0f) { pinchLatched = false; return false; }   // brak śledzonej dłoni
                if (pinchLatched) { if (gap > open) pinchLatched = false; }
                else { if (gap < close) pinchLatched = true; }
                return pinchLatched;
            }
            pinchLatched = false;
            return false;
        }

        /// <summary>Najmniejszy dystans kciuk–wskazujący spośród śledzonych dłoni (−1 gdy żadna nie śledzona).</summary>
        private float MinPinchGap()
        {
            float g = float.MaxValue;
            float l = PinchGap(handSubsystem.leftHand); if (l >= 0f) g = Mathf.Min(g, l);
            float r = PinchGap(handSubsystem.rightHand); if (r >= 0f) g = Mathf.Min(g, r);
            return g == float.MaxValue ? -1f : g;
        }

        private float PinchGap(XRHand hand)
        {
            if (!hand.isTracked) return -1f;
            var thumb = hand.GetJoint(XRHandJointID.ThumbTip);
            var index = hand.GetJoint(XRHandJointID.IndexTip);
            if (!thumb.TryGetPose(out var tp) || !index.TryGetPose(out var ip)) return -1f;
            return Vector3.Distance(tp.position, ip.position);
        }
    }
}
