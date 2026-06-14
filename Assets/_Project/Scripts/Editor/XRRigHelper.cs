using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;

namespace MRCrisisTrainer.EditorTools
{
    /// <summary>
    /// Konfiguracja rigu VR dla Quest 3 (OpenXR):
    /// - TrackedPoseDriver na kamerze (head tracking),
    /// - XR Origin z trybem Floor + fallback CameraYOffset (poprawna wysokość oczu).
    /// Bez tego kamera jest na poziomie podłogi i scena "wisi nad graczem".
    /// </summary>
    public static class XRRigHelper
    {
        public static void AddHeadTracking(GameObject camGo)
        {
            var tpd = camGo.GetComponent<TrackedPoseDriver>();
            if (tpd == null) tpd = camGo.AddComponent<TrackedPoseDriver>();
            tpd.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            tpd.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;

            var posAction = new InputAction("HMDPosition", InputActionType.Value,
                "<XRHMD>/centerEyePosition", expectedControlType: "Vector3");
            var rotAction = new InputAction("HMDRotation", InputActionType.Value,
                "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion");
            var trackAction = new InputAction("HMDTracking", InputActionType.Value,
                "<XRHMD>/trackingState", expectedControlType: "Integer");

            tpd.positionInput = new InputActionProperty(posAction);
            tpd.rotationInput = new InputActionProperty(rotAction);
            tpd.trackingStateInput = new InputActionProperty(trackAction);
        }

        /// <summary>
        /// Dodaje XR Origin na rigRoot, ustawia kamerę + offset + tryb Floor.
        /// rigRoot = "XR Rig", camOffset = "Camera Offset", cam = "Main Camera".
        /// </summary>
        public static void ConfigureXROrigin(GameObject rigRoot, GameObject camOffset, Camera cam)
        {
            var origin = rigRoot.GetComponent<XROrigin>();
            if (origin == null) origin = rigRoot.AddComponent<XROrigin>();
            origin.Camera = cam;
            origin.CameraFloorOffsetObject = camOffset;
            // Floor: wirtualna podłoga = realna podłoga; gdy brak Stage → fallback na CameraYOffset.
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
            origin.CameraYOffset = 1.6f;
        }
    }
}
