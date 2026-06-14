using System.Collections.Generic;
using UnityEngine;
using XRInputDevice = UnityEngine.XR.InputDevice;
using XRInputDevices = UnityEngine.XR.InputDevices;
using XRInputDeviceCharacteristics = UnityEngine.XR.InputDeviceCharacteristics;
using XRCommonUsages = UnityEngine.XR.CommonUsages;

namespace MRCrisisTrainer.Core
{
    /// <summary>
    /// Przełącznik trybu otoczenia (Passthrough MR ↔ wirtualny pokój VR) przyciskiem B/Y
    /// kontrolera lub klawiszem M. Bezpiecznik: jeśli passthrough nie działa (czarny ekran),
    /// gracz może przełączyć na wirtualny pokój.
    /// </summary>
    public class EnvironmentModeToggle : MonoBehaviour
    {
        private readonly List<XRInputDevice> devices = new List<XRInputDevice>();
        private bool latch;

        void Update()
        {
            if (RoomEnvironment.Instance == null) return;
            bool pressed = SecondaryPressed() || KeyPressed();
            if (pressed && !latch) RoomEnvironment.Instance.ToggleMode();
            latch = pressed;
        }

        private bool KeyPressed()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            return kb != null && kb.mKey.wasPressedThisFrame;
        }

        private bool SecondaryPressed()
        {
            XRInputDevices.GetDevices(devices);
            foreach (var d in devices)
            {
                if (!d.isValid || (d.characteristics & XRInputDeviceCharacteristics.Controller) == 0) continue;
                if (d.TryGetFeatureValue(XRCommonUsages.secondaryButton, out bool b) && b) return true;
            }
            return false;
        }
    }
}
