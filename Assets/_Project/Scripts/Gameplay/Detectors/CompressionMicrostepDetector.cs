using System;
using UnityEngine;
using MRCrisisTrainer.Config;
using MRCrisisTrainer.Core;
using MRCrisisTrainer.Logging;
using MRCrisisTrainer.XR;
using MRCrisisTrainer.Acts.Act1;

namespace MRCrisisTrainer.Gameplay.Detectors
{
    /// <summary>
    /// Mikrokrok "uciśnięcia BLS": liczy uciśnięcia (ruch dłoni w dół/górę nad targetem),
    /// mierzy tempo (BPM) i kończy po osiągnięciu wymaganej liczby. Steruje metronomem.
    /// </summary>
    public class CompressionMicrostepDetector : MicrostepDetector
    {
        [SerializeField] private BLSConfig config;
        [SerializeField] private MonoBehaviour handProviderBehaviour;
        [SerializeField] private Transform chest;
        [SerializeField] private MetronomeController metronome;

        public event Action<int, int> OnProgress;       // (current, target)
        public event Action<float, TempoClassification> OnTempo; // bpm, class

        private IHandPoseProvider hands;
        private RhythmCalculator rhythm;
        private int count;
        private CompressionPhase phase = CompressionPhase.Idle;
        private float startY, lowestY, phaseStart;

        void Awake() => hands = handProviderBehaviour as IHandPoseProvider;

        protected override void OnBegin()
        {
            count = 0;
            phase = CompressionPhase.Idle;
            rhythm = new RhythmCalculator(config != null ? config.rollingWindowSize : 5);
            metronome?.StartTicking();
            OnProgress?.Invoke(0, Target);
        }

        protected override void OnCancel() => metronome?.StopTicking();

        private int Target => config != null ? config.targetCompressions : 30;

        void Update()
        {
            if (!IsActive || hands == null || config == null) return;
            float? y = AvgHandY();
            if (!y.HasValue) { phase = CompressionPhase.Idle; return; }
            float now = Time.time;
            switch (phase)
            {
                case CompressionPhase.Idle:
                    startY = lowestY = y.Value; phaseStart = now; phase = CompressionPhase.Descending; break;
                case CompressionPhase.Descending:
                    if (y.Value < lowestY) lowestY = y.Value;
                    if (startY - lowestY >= config.minCompressionDepth && y.Value > lowestY + 0.005f)
                    { phase = CompressionPhase.Ascending; phaseStart = now; }
                    else if (now - phaseStart > config.maxCycleDuration) phase = CompressionPhase.Idle;
                    break;
                case CompressionPhase.Ascending:
                    float full = startY - lowestY;
                    if (y.Value - lowestY >= full * 0.6f) { RegisterCompression(now); phase = CompressionPhase.Idle; }
                    else if (now - phaseStart > config.maxCycleDuration) phase = CompressionPhase.Idle;
                    break;
            }
        }

        private void RegisterCompression(float now)
        {
            count++;
            rhythm.RegisterCompression(now);
            float bpm = rhythm.CurrentBPM;
            var cls = rhythm.Classify(config);
            OnTempo?.Invoke(bpm, cls);
            OnProgress?.Invoke(count, Target);
            JSONLLogger.Instance?.LogEvent("compression", new System.Collections.Generic.Dictionary<string, object>
            { { "n", count }, { "bpm", bpm }, { "class", cls.ToString() } });
            if (count >= Target) { metronome?.StopTicking(); Complete(); }
        }

        private float? AvgHandY()
        {
            bool l = hands.TryGetHandPosition(HandSide.Left, out var lp);
            bool r = hands.TryGetHandPosition(HandSide.Right, out var rp);
            // require near chest
            if (chest != null)
            {
                if (l && (lp - chest.position).sqrMagnitude > 0.09f) l = false;
                if (r && (rp - chest.position).sqrMagnitude > 0.09f) r = false;
            }
            if (!l && !r) return null;
            if (l && r) return (lp.y + rp.y) * 0.5f;
            return l ? lp.y : rp.y;
        }
    }
}
