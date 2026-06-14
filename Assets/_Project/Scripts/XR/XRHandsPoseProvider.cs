using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Management;

namespace MRCrisisTrainer.XR
{
    /// <summary>
    /// Dostarcza pozycje dłoni (XR Hands) w przestrzeni ŚWIATA. Pozy ze subsystemu są
    /// w przestrzeni trackingu — transformujemy je przez origin (XR Rig root), tak samo
    /// jak TrackedPoseDriver ustawia kamerę. Bez tego ręce byłyby w złym miejscu.
    /// </summary>
    public class XRHandsPoseProvider : MonoBehaviour, IHandPoseProvider
    {
        [Tooltip("Origin trackingu = XR Rig root. Domyślnie obiekt z tym komponentem.")]
        [SerializeField] private Transform trackingOrigin;

        private XRHandSubsystem handSubsystem;

        private Transform Origin => trackingOrigin != null ? trackingOrigin : transform;

        void OnEnable() => TryAcquireSubsystem();

        private void TryAcquireSubsystem()
        {
            if (handSubsystem != null) return;
            var loader = XRGeneralSettings.Instance?.Manager?.activeLoader;
            if (loader == null) return;
            handSubsystem = loader.GetLoadedSubsystem<XRHandSubsystem>();
        }

        public bool IsTracked(HandSide side)
        {
            if (handSubsystem == null) TryAcquireSubsystem();
            if (handSubsystem == null) return false;
            var hand = side == HandSide.Left ? handSubsystem.leftHand : handSubsystem.rightHand;
            return hand.isTracked;
        }

        public bool TryGetHandPosition(HandSide side, out Vector3 worldPosition)
        {
            worldPosition = Vector3.zero;
            if (handSubsystem == null) TryAcquireSubsystem();
            if (handSubsystem == null) return false;

            var hand = side == HandSide.Left ? handSubsystem.leftHand : handSubsystem.rightHand;
            if (!hand.isTracked) return false;

            var joint = hand.GetJoint(XRHandJointID.MiddleMetacarpal);
            if (joint.TryGetPose(out var pose))
            {
                worldPosition = Origin.TransformPoint(pose.position);
                return true;
            }
            return false;
        }

        /// <summary>Promień wskazujący „z palca" (origin = staw u nasady palca wskazującego, kierunek = wzdłuż palca).
        /// Do laserowego wskaźnika UI w menu (hands-only). W przestrzeni świata.</summary>
        public bool TryGetPointer(HandSide side, out Vector3 worldOrigin, out Vector3 worldDir)
        {
            worldOrigin = Vector3.zero; worldDir = Vector3.forward;
            if (handSubsystem == null) TryAcquireSubsystem();
            if (handSubsystem == null) return false;
            var hand = side == HandSide.Left ? handSubsystem.leftHand : handSubsystem.rightHand;
            if (!hand.isTracked) return false;
            var prox = hand.GetJoint(XRHandJointID.IndexProximal);
            var tip = hand.GetJoint(XRHandJointID.IndexTip);
            if (prox.TryGetPose(out var pp) && tip.TryGetPose(out var tp))
            {
                Vector3 d = tp.position - pp.position;
                if (d.sqrMagnitude < 1e-6f) return false;
                worldOrigin = Origin.TransformPoint(pp.position);
                worldDir = Origin.TransformDirection(d.normalized);
                return true;
            }
            return false;
        }

        public bool TryGetPalmRotation(HandSide side, out Quaternion worldRotation)
        {
            worldRotation = Quaternion.identity;
            if (handSubsystem == null) TryAcquireSubsystem();
            if (handSubsystem == null) return false;

            var hand = side == HandSide.Left ? handSubsystem.leftHand : handSubsystem.rightHand;
            if (!hand.isTracked) return false;

            var palm = hand.GetJoint(XRHandJointID.Palm);
            if (palm.TryGetPose(out var pose))
            {
                worldRotation = Origin.rotation * pose.rotation;
                return true;
            }
            return false;
        }
    }
}
