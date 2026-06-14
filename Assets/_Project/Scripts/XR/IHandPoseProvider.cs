using UnityEngine;

namespace MRCrisisTrainer.XR
{
    public enum HandSide { Left, Right }

    public interface IHandPoseProvider
    {
        bool TryGetHandPosition(HandSide side, out Vector3 worldPosition);
        bool TryGetPalmRotation(HandSide side, out Quaternion worldRotation);
        bool IsTracked(HandSide side);
    }
}
