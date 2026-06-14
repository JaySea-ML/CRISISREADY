using UnityEngine;

namespace MRCrisisTrainer.XR
{
    public class FallbackTransformPoseProvider : MonoBehaviour, IHandPoseProvider
    {
        [SerializeField] private Transform leftHandTransform;
        [SerializeField] private Transform rightHandTransform;

        public bool TryGetHandPosition(HandSide side, out Vector3 worldPosition)
        {
            var t = side == HandSide.Left ? leftHandTransform : rightHandTransform;
            if (t != null)
            {
                worldPosition = t.position;
                return true;
            }
            worldPosition = Vector3.zero;
            return false;
        }

        public bool TryGetPalmRotation(HandSide side, out Quaternion worldRotation)
        {
            var t = side == HandSide.Left ? leftHandTransform : rightHandTransform;
            if (t != null)
            {
                worldRotation = t.rotation;
                return true;
            }
            worldRotation = Quaternion.identity;
            return false;
        }

        public bool IsTracked(HandSide side)
        {
            var t = side == HandSide.Left ? leftHandTransform : rightHandTransform;
            return t != null;
        }
    }
}
