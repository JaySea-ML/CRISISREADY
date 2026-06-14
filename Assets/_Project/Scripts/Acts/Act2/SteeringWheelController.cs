using System;
using System.Collections.Generic;
using UnityEngine;
using MRCrisisTrainer.Config;
using MRCrisisTrainer.XR;
using XRInputDevice = UnityEngine.XR.InputDevice;
using XRInputDevices = UnityEngine.XR.InputDevices;
using XRCharacteristics = UnityEngine.XR.InputDeviceCharacteristics;
using XRCommonUsages = UnityEngine.XR.CommonUsages;

namespace MRCrisisTrainer.Acts.Act2
{
    /// <summary>
    /// Wirtualna kierownica. Dwa sposoby sterowania (kontrolery działają zawsze):
    ///  1) CHWYT: ręka/kontroler blisko środka koła → obracasz ruchem ręki wokół osi koła.
    ///  2) GAŁKA (thumbstick): wychylenie poziome gałki kontrolera steruje bezpośrednio.
    /// Udostępnia NormalizedSteering (-1..+1) dla fizyki poślizgu.
    /// </summary>
    public class SteeringWheelController : MonoBehaviour
    {
        [SerializeField] private VehicleConfig config;
        [SerializeField] private MonoBehaviour handProviderBehaviour;
        [SerializeField] private Transform wheelTransform;
        [SerializeField] private Transform wheelCenter;
        [SerializeField] private float thumbstickDeadzone = 0.15f;
        [SerializeField] private float autoCenterRate = 90f; // deg/s powrotu do środka gdy brak inputu

        private IHandPoseProvider handProvider;
        private float currentWheelAngleDeg;
        private float gripStartTime = -1f;
        private readonly List<XRInputDevice> devices = new List<XRInputDevice>();
        private Renderer[] wheelRenderers;
        private Color[] wheelOrigColors;
        private MaterialPropertyBlock mpb;
        private bool gripVisual;

        public bool IsGripped { get; private set; }
        public float CurrentWheelAngleDeg => currentWheelAngleDeg;
        private float FullLock => config != null && config.steeringFullLockDeg > 1f ? config.steeringFullLockDeg : 95f;
        /// <summary>-1..+1 (-1 = pełny lewo, +1 = pełny prawo). Liczone wg steeringFullLockDeg, by ręka dawała mocny skręt.</summary>
        [SerializeField] private float steerSign = 1f;    // prawa strona = dodatni skręt; dodatni skręt koryguje dodatni poślizg.
        public float NormalizedSteering => config != null ? Mathf.Clamp(steerSign * currentWheelAngleDeg / FullLock, -1f, 1f) : 0f;
        public event Action OnDriveStarted;

        void Awake()
        {
            handProvider = handProviderBehaviour as IHandPoseProvider;
            if (wheelTransform == null) wheelTransform = transform;
            if (wheelCenter == null) wheelCenter = transform;
            mpb = new MaterialPropertyBlock();
            if (wheelTransform != null) wheelRenderers = wheelTransform.GetComponentsInChildren<Renderer>(true);
            if (wheelRenderers != null)
            {
                wheelOrigColors = new Color[wheelRenderers.Length];
                for (int i = 0; i < wheelRenderers.Length; i++)
                {
                    var m = wheelRenderers[i] != null ? wheelRenderers[i].sharedMaterial : null;
                    wheelOrigColors[i] = m == null ? Color.gray
                        : (m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : (m.HasProperty("_Color") ? m.color : Color.gray));
                }
            }
            SetGripVisual(false);
        }

        void Update()
        {
            if (config == null) return;

            // --- Hand/controller grip ---
            Vector3 lp = default, rp = default;
            // Hojny zasięg chwytu — wystarczy sięgnąć dłonią w stronę koła (~0.5 m), nie trzeba trafić idealnie w środek.
            const float grabReach = 0.5f;
            bool leftIn = handProvider != null && handProvider.TryGetHandPosition(HandSide.Left, out lp)
                          && (lp - wheelCenter.position).sqrMagnitude <= grabReach * grabReach;
            bool rightIn = handProvider != null && handProvider.TryGetHandPosition(HandSide.Right, out rp)
                           && (rp - wheelCenter.position).sqrMagnitude <= grabReach * grabReach;

            // --- Thumbstick (zawsze dostępny fallback) ---
            float stick = ReadThumbstickX();
            bool stickActive = Mathf.Abs(stick) > thumbstickDeadzone;

            bool nowGripped = leftIn || rightIn || stickActive;
            if (nowGripped && !IsGripped) gripStartTime = Time.time;
            if (!nowGripped) gripStartTime = -1f;
            IsGripped = nowGripped;
            if (IsGripped != gripVisual) { gripVisual = IsGripped; SetGripVisual(gripVisual); }   // podświetl gdy złapana

            if (!IsGripped)
            {
                // auto-center kierownicy gdy puszczona
                twoHandActive = false;
                currentWheelAngleDeg = Mathf.MoveTowards(currentWheelAngleDeg, 0f, autoCenterRate * Time.deltaTime);
                ApplyWheelVisual();
                return;
            }

            if (gripStartTime > 0 && Time.time - gripStartTime >= config.gripHoldToStart)
            {
                OnDriveStarted?.Invoke();
                gripStartTime = float.MaxValue;
            }

            if (stickActive)
            {
                // Gałka steruje bezpośrednio: pełne wychylenie = pełny skręt (FullLock)
                float target = stick * FullLock;
                currentWheelAngleDeg = Mathf.MoveTowards(currentWheelAngleDeg, target, FullLock * 4f * Time.deltaTime);
                ApplyWheelVisual();
            }
            else if (leftIn && rightIn && (lp - rp).sqrMagnitude > 0.0004f)
            {
                SteerTwoHanded(lp, rp);   // OBIE RĘCE: realny obrót koła — kręcisz jak prawdziwą kierownicą
            }
            else
            {
                twoHandActive = false;   // jedna ręka → tryb przesunięcia bocznego
                Vector3? hand = rightIn ? (Vector3?)rp : (leftIn ? (Vector3?)lp : null);
                if (hand.HasValue) SteerFromHand(hand.Value);
            }
        }

        private float ReadThumbstickX()
        {
            XRInputDevices.GetDevices(devices);
            foreach (var d in devices)
            {
                if (!d.isValid || (d.characteristics & XRCharacteristics.Controller) == 0) continue;
                if (d.TryGetFeatureValue(XRCommonUsages.primary2DAxis, out Vector2 axis) && Mathf.Abs(axis.x) > thumbstickDeadzone)
                    return axis.x;
            }
            return 0f;
        }

        /// <summary>
        /// Sterowanie ręką = POZIOME PRZESUNIĘCIE dłoni względem środka koła: dłoń w prawo => skręt w prawo.
        /// Bezpośrednie i intuicyjne (nie trzeba kręcić ręką po okręgu). ~20 cm przesunięcia = pełny skręt.
        /// </summary>
        private void SteerFromHand(Vector3 hand)
        {
            const float handTravelForFullLock = 0.20f;
            float lateral = Vector3.Dot(hand - wheelCenter.position, wheelCenter.right);
            float target = Mathf.Clamp(lateral / handTravelForFullLock, -1f, 1f) * FullLock;
            currentWheelAngleDeg = target;
            ApplyWheelVisual();
        }

        private bool twoHandActive;
        private float lastHandsAngle;
        /// <summary>OBIE RĘCE na obwodzie: kąt linii między dłońmi (w płaszczyźnie koła) napędza obrót koła —
        /// kręcisz dłońmi wokół osi jak prawdziwą kierownicą. Akumulujemy deltę kąta → realne, dwuręczne sterowanie.</summary>
        private void SteerTwoHanded(Vector3 lp, Vector3 rp)
        {
            Vector3 d = rp - lp;
            float x = Vector3.Dot(d, wheelCenter.right);
            float y = Vector3.Dot(d, wheelCenter.up);
            if (x * x + y * y < 1e-6f) return;
            float handsAngle = Mathf.Atan2(y, x) * Mathf.Rad2Deg;
            if (!twoHandActive) { twoHandActive = true; lastHandsAngle = handsAngle; return; }   // pierwsza klatka = kalibracja
            float delta = Mathf.DeltaAngle(lastHandsAngle, handsAngle);
            lastHandsAngle = handsAngle;
            // naturalna akumulacja; kierunkiem steruje jeden steerSign w NormalizedSteering (spójnie z jedną ręką)
            currentWheelAngleDeg = Mathf.Clamp(currentWheelAngleDeg + delta, -FullLock, FullLock);
            ApplyWheelVisual();
        }

        private void ApplyWheelVisual()
        {
            if (wheelTransform != null)
                wheelTransform.localRotation = Quaternion.Euler(0, 0, currentWheelAngleDeg);   // koło obraca się ZGODNIE z ruchem rąk (było odwrócone: lewo dawało prawo)
        }

        /// <summary>Wizualne potwierdzenie chwytu: kierownica świeci cyjanem gdy trzymana, ciemna gdy puszczona.</summary>
        private void SetGripVisual(bool gripped)
        {
            // BEZ KOLORU na kierownicy (życzenie użytkownika): nie barwimy koła ani nie dodajemy poświaty.
            // Czytelność chwytu daje OBRÓT koła zgodny z dłońmi (+ realne dłonie XR na obręczy).
            if (wheelRenderers == null || mpb == null) return;
            for (int i = 0; i < wheelRenderers.Length; i++)
            {
                var r = wheelRenderers[i]; if (r == null) continue;
                r.GetPropertyBlock(mpb);
                mpb.SetColor("_EmissionColor", Color.black);   // wygaś ewentualną wcześniejszą emisję, zostaw naturalny materiał
                r.SetPropertyBlock(mpb);
            }
        }

        /// <summary>Bezpośrednie ustawienie sterowania (-1..1) — używane w testach i przez fallbacki.</summary>
        public void ApplySteeringByValue(float normalized)
        {
            float sign = Mathf.Abs(steerSign) > 0.001f ? steerSign : 1f;
            currentWheelAngleDeg = Mathf.Clamp(normalized, -1f, 1f) * FullLock / sign;
            ApplyWheelVisual();
        }
    }
}
