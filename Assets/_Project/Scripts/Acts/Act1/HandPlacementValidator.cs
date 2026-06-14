using System;
using UnityEngine;
using MRCrisisTrainer.XR;

namespace MRCrisisTrainer.Acts.Act1
{
    public class HandPlacementValidator : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour handProviderBehaviour;
        [SerializeField] private Transform chestHotspot;
        [SerializeField] private float radius = 0.18f;
        [SerializeField] private bool requireBothHands = true;

        public bool AreHandsPlaced { get; private set; }
        public event Action<bool> OnPlacementChanged;

        private IHandPoseProvider handProvider;

        void Awake()
        {
            handProvider = handProviderBehaviour as IHandPoseProvider;
        }

        void Update()
        {
            if (handProvider == null || chestHotspot == null) return;

            bool leftIn = IsHandInHotspot(HandSide.Left);
            bool rightIn = IsHandInHotspot(HandSide.Right);
            bool nowPlaced = requireBothHands ? (leftIn && rightIn) : (leftIn || rightIn);

            if (nowPlaced != AreHandsPlaced)
            {
                AreHandsPlaced = nowPlaced;
                OnPlacementChanged?.Invoke(AreHandsPlaced);
            }
        }

        private bool IsHandInHotspot(HandSide side)
        {
            if (!handProvider.TryGetHandPosition(side, out var pos)) return false;
            float sqDist = (pos - chestHotspot.position).sqrMagnitude;
            return sqDist <= radius * radius;
        }

        void OnDrawGizmosSelected()
        {
            if (chestHotspot == null) return;
            Gizmos.color = AreHandsPlaced ? Color.green : new Color(1f, 0.5f, 0f, 0.6f);
            Gizmos.DrawWireSphere(chestHotspot.position, radius);
        }
    }
}
