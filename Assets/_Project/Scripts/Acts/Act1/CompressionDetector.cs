using System;
using UnityEngine;
using MRCrisisTrainer.Config;
using MRCrisisTrainer.XR;

namespace MRCrisisTrainer.Acts.Act1
{
    public enum CompressionPhase { Idle, Descending, Ascending }

    public class CompressionDetector : MonoBehaviour
    {
        [SerializeField] private BLSConfig config;
        [SerializeField] private MonoBehaviour handProviderBehaviour;
        [SerializeField] private HandPlacementValidator placementValidator;

        public event Action<float, float> OnCompression;

        private IHandPoseProvider handProvider;
        private CompressionPhase phase = CompressionPhase.Idle;
        private float startY;
        private float lowestY;
        private float phaseStartTime;
        private bool enabledByGate;

        void Awake()
        {
            handProvider = handProviderBehaviour as IHandPoseProvider;
            if (handProvider == null)
            {
                Debug.LogError($"[{nameof(CompressionDetector)}] handProviderBehaviour does not implement IHandPoseProvider");
            }
        }

        public void SetGateOpen(bool open)
        {
            enabledByGate = open;
            if (!open) ResetCycle();
        }

        void Update()
        {
            if (!enabledByGate || handProvider == null || config == null) return;
            if (placementValidator != null && !placementValidator.AreHandsPlaced) return;

            float? avgY = TryGetAverageHandY();
            if (!avgY.HasValue)
            {
                ResetCycle();
                return;
            }

            float y = avgY.Value;
            float now = Time.time;

            switch (phase)
            {
                case CompressionPhase.Idle:
                    startY = y;
                    lowestY = y;
                    phaseStartTime = now;
                    phase = CompressionPhase.Descending;
                    break;

                case CompressionPhase.Descending:
                    if (y < lowestY) lowestY = y;
                    float descentDepth = startY - lowestY;
                    if (descentDepth >= config.minCompressionDepth && y > lowestY + 0.005f)
                    {
                        phase = CompressionPhase.Ascending;
                        phaseStartTime = now;
                    }
                    else if (now - phaseStartTime > config.maxCycleDuration)
                    {
                        ResetCycle();
                    }
                    break;

                case CompressionPhase.Ascending:
                    float ascent = y - lowestY;
                    float fullDepth = startY - lowestY;
                    if (ascent >= fullDepth * 0.6f)
                    {
                        OnCompression?.Invoke(now, fullDepth);
                        ResetCycle();
                    }
                    else if (now - phaseStartTime > config.maxCycleDuration)
                    {
                        ResetCycle();
                    }
                    break;
            }
        }

        private float? TryGetAverageHandY()
        {
            bool leftTracked = handProvider.TryGetHandPosition(HandSide.Left, out var lp);
            bool rightTracked = handProvider.TryGetHandPosition(HandSide.Right, out var rp);
            if (!leftTracked && !rightTracked) return null;
            if (leftTracked && rightTracked) return (lp.y + rp.y) * 0.5f;
            return leftTracked ? lp.y : rp.y;
        }

        private void ResetCycle()
        {
            phase = CompressionPhase.Idle;
        }
    }
}
