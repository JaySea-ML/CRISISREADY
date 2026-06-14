using UnityEngine;

namespace MRCrisisTrainer.XR
{
    /// <summary>
    /// Łączy hand tracking i kontrolery: zwraca pozycję dłoni jeśli śledzona,
    /// w przeciwnym razie pozycję kontrolera. Dzięki temu gra działa z rękami LUB padami.
    /// </summary>
    public class CompositeHandPoseProvider : MonoBehaviour, IHandPoseProvider
    {
        [SerializeField] private XRHandsPoseProvider hands;
        [SerializeField] private ControllerPoseProvider controllers;

        void Awake()
        {
            if (hands == null) hands = GetComponent<XRHandsPoseProvider>();
            if (controllers == null) controllers = GetComponent<ControllerPoseProvider>();
        }

        public bool IsTracked(HandSide side)
        {
            if (hands != null && hands.IsTracked(side)) return true;
            return controllers != null && controllers.IsTracked(side);
        }

        public bool TryGetHandPosition(HandSide side, out Vector3 worldPosition)
        {
            if (hands != null && hands.IsTracked(side) && hands.TryGetHandPosition(side, out worldPosition))
                return true;
            if (controllers != null && controllers.TryGetHandPosition(side, out worldPosition))
                return true;
            worldPosition = Vector3.zero;
            return false;
        }

        public bool TryGetPalmRotation(HandSide side, out Quaternion worldRotation)
        {
            if (hands != null && hands.IsTracked(side) && hands.TryGetPalmRotation(side, out worldRotation))
                return true;
            if (controllers != null && controllers.TryGetPalmRotation(side, out worldRotation))
                return true;
            worldRotation = Quaternion.identity;
            return false;
        }
    }
}
