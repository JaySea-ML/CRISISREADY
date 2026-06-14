using System;
using UnityEngine;
using MRCrisisTrainer.Config;

namespace MRCrisisTrainer.Acts.Act3
{
    /// <summary>
    /// MonoBehaviour wrapper na SpeechCueDetector - pobiera volume z MicrophoneInputProvider.
    /// </summary>
    public class SpeechDetectorBehaviour : MonoBehaviour
    {
        [SerializeField] private Act3Config config;
        [SerializeField] private MicrophoneInputProvider micProvider;

        private SpeechCueDetector detector;
        private bool active;

        public event Action OnSpeechDetected;
        public event Action OnTimeout;

        void Awake()
        {
            detector = new SpeechCueDetector(config);
            detector.OnSpeechDetected += () => OnSpeechDetected?.Invoke();
            detector.OnTimeout += () => OnTimeout?.Invoke();
        }

        public void BeginListening()
        {
            detector.Reset();
            active = true;
        }

        public void StopListening()
        {
            active = false;
        }

        void Update()
        {
            if (!active || detector == null || micProvider == null) return;
            detector.Tick(micProvider.CurrentVolume, Time.deltaTime, Time.time);
            if (detector.HasDetectedSpeech) active = false;
        }
    }
}
