using System;
using UnityEngine;
using MRCrisisTrainer.Config;

namespace MRCrisisTrainer.Acts.Act3
{
    public class SilenceMonitorBehaviour : MonoBehaviour
    {
        [SerializeField] private Act3Config config;
        [SerializeField] private MicrophoneInputProvider micProvider;

        private SilenceMonitor monitor;
        private bool active;

        public event Action<int> OnNoiseStrike;
        public event Action OnSilenceComplete;
        public event Action OnFailed;

        public float NormalizedProgress => monitor != null ? monitor.NormalizedProgress : 0f;
        public int NoiseStrikes => monitor != null ? monitor.NoiseStrikes : 0;

        void Awake()
        {
            monitor = new SilenceMonitor(config);
            monitor.OnNoiseStrike += n => OnNoiseStrike?.Invoke(n);
            monitor.OnSilenceComplete += () => OnSilenceComplete?.Invoke();
            monitor.OnFailed += () => OnFailed?.Invoke();
        }

        public void BeginSilence()
        {
            monitor.Reset();
            active = true;
        }

        public void StopMonitoring()
        {
            active = false;
        }

        void Update()
        {
            if (!active || monitor == null || micProvider == null) return;
            monitor.Tick(micProvider.CurrentVolume, Time.deltaTime, Time.time);
            if (monitor.Outcome != SilenceOutcome.InProgress) active = false;
        }
    }
}
