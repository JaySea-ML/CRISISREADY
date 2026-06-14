using System.Collections.Generic;
using UnityEngine;
using XRNode = UnityEngine.XR.XRNode;
using XRInputDevice = UnityEngine.XR.InputDevice;
using XRInputDevices = UnityEngine.XR.InputDevices;
using XRCommonUsages = UnityEngine.XR.CommonUsages;

namespace MRCrisisTrainer.XR
{
    /// <summary>
    /// Dostarcza pozycje kontrolerów (Touch) w przestrzeni ŚWIATA jako IHandPoseProvider.
    /// Pozycja urządzenia jest w przestrzeni trackingu — transformujemy przez origin (XR Rig).
    /// </summary>
    public class ControllerPoseProvider : MonoBehaviour, IHandPoseProvider
    {
        [SerializeField] private Transform trackingOrigin;
        private Transform Origin => trackingOrigin != null ? trackingOrigin : transform;

        public bool IsTracked(HandSide side)
        {
            var d = Device(side);
            return d.isValid && d.TryGetFeatureValue(XRCommonUsages.isTracked, out bool t) && t;
        }

        public bool TryGetHandPosition(HandSide side, out Vector3 worldPosition)
        {
            worldPosition = Vector3.zero;
            var d = Device(side);
            if (!d.isValid) return false;
            if (d.TryGetFeatureValue(XRCommonUsages.devicePosition, out Vector3 pos))
            {
                worldPosition = Origin.TransformPoint(pos);
                return true;
            }
            return false;
        }

        public bool TryGetPalmRotation(HandSide side, out Quaternion worldRotation)
        {
            worldRotation = Quaternion.identity;
            var d = Device(side);
            if (!d.isValid) return false;
            if (d.TryGetFeatureValue(XRCommonUsages.deviceRotation, out Quaternion rot))
            {
                worldRotation = Origin.rotation * rot;
                return true;
            }
            return false;
        }

        private XRInputDevice Device(HandSide side)
        {
            return XRInputDevices.GetDeviceAtXRNode(side == HandSide.Left ? XRNode.LeftHand : XRNode.RightHand);
        }
    }
}
