using UnityEngine;
using MRCrisisTrainer.XR;

namespace MRCrisisTrainer.Gameplay.Detectors
{
    /// <summary>Ukończenie gdy obie (lub jedna) dłonie znajdą się na targecie (np. klatka piersiowa).</summary>
    public class HandsOnTargetDetector : MicrostepDetector
    {
        [SerializeField] private MonoBehaviour handProviderBehaviour;
        [SerializeField] private Transform target;
        [SerializeField] private float radius = 0.18f;
        [SerializeField] private bool requireBoth = true;
        [SerializeField] private float holdSeconds = 0.4f;

        private IHandPoseProvider hands;
        private float onTime;

        public bool AreHandsOnTarget { get; private set; }

        void Awake() => hands = handProviderBehaviour as IHandPoseProvider;

        protected override void OnBegin() => onTime = 0f;

        void Update()
        {
            if (!IsActive || hands == null || target == null) return;
            bool left = InRange(HandSide.Left);
            bool right = InRange(HandSide.Right);
            AreHandsOnTarget = requireBoth ? (left && right) : (left || right);
            if (AreHandsOnTarget)
            {
                onTime += Time.deltaTime;
                if (onTime >= holdSeconds) Complete();
            }
            else onTime = 0f;
        }

        private bool InRange(HandSide s)
        {
            if (!hands.TryGetHandPosition(s, out var p)) return false;
            return (p - target.position).sqrMagnitude <= radius * radius;
        }
    }
}
