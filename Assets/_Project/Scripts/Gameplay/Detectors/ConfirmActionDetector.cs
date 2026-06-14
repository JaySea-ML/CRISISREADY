using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Management;
using XRInputDevice = UnityEngine.XR.InputDevice;
using XRInputDevices = UnityEngine.XR.InputDevices;
using XRInputDeviceCharacteristics = UnityEngine.XR.InputDeviceCharacteristics;
using XRCommonUsages = UnityEngine.XR.CommonUsages;
using Keyboard = UnityEngine.InputSystem.Keyboard;

namespace MRCrisisTrainer.Gameplay.Detectors
{
    /// <summary>
    /// Mikrokrok poznawczy potwierdzany akcją gracza: trigger kontrolera, pinch dłoni
    /// lub ENTER/SPACE. Używane do kroków typu "oceniłem bezpieczeństwo", "sprawdziłem przytomność".
    /// </summary>
    public class ConfirmActionDetector : MicrostepDetector
    {
        [SerializeField] private float pinchThreshold = 0.035f;
        [SerializeField] private float minActiveTime = 0.5f; // żeby nie złapać akcji z poprzedniego kroku
        [Tooltip("Po tylu sekundach krok zalicza się automatycznie (fallback, gdy brak inputu). 0 = wyłączony.")]
        [SerializeField] private float autoConfirmAfter = 9f;

        private readonly List<XRInputDevice> devices = new List<XRInputDevice>();
        private XRHandSubsystem handSubsystem;
        private float beginTime;

        protected override void OnBegin() => beginTime = Time.time;

        void Update()
        {
            if (!IsActive) return;
            float elapsed = Time.time - beginTime;
            if (elapsed < minActiveTime) return;
            if (Controller() || Pinch() || Keyboard()) { Complete(); return; }
            if (autoConfirmAfter > 0f && elapsed >= autoConfirmAfter) Complete();
        }

        private bool Keyboard()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            return kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame);
        }

        private bool Controller()
        {
            XRInputDevices.GetDevices(devices);
            foreach (var d in devices)
            {
                if (!d.isValid || (d.characteristics & XRInputDeviceCharacteristics.Controller) == 0) continue;
                if (d.TryGetFeatureValue(XRCommonUsages.triggerButton, out bool t) && t) return true;
                if (d.TryGetFeatureValue(XRCommonUsages.primaryButton, out bool p) && p) return true;
            }
            return false;
        }

        private bool Pinch()
        {
            if (handSubsystem == null)
            {
                var loader = XRGeneralSettings.Instance?.Manager?.activeLoader;
                if (loader != null) handSubsystem = loader.GetLoadedSubsystem<XRHandSubsystem>();
            }
            if (handSubsystem == null) return false;
            return HandPinch(handSubsystem.leftHand) || HandPinch(handSubsystem.rightHand);
        }

        private bool HandPinch(XRHand hand)
        {
            if (!hand.isTracked) return false;
            var thumb = hand.GetJoint(XRHandJointID.ThumbTip);
            var index = hand.GetJoint(XRHandJointID.IndexTip);
            if (!thumb.TryGetPose(out var tp) || !index.TryGetPose(out var ip)) return false;
            return Vector3.Distance(tp.position, ip.position) < pinchThreshold;
        }
    }
}
