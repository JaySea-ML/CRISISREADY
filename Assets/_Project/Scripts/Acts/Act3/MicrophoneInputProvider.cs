using UnityEngine;

namespace MRCrisisTrainer.Acts.Act3
{
    /// <summary>
    /// Owija Unity Microphone API. Udostępnia RMS volume (0..1) na żywo ORAZ ostatnie N sekund nagrania
    /// (do wysłania do Wit.ai /dictation — prawdziwe rozpoznawanie mowy).
    /// W Editorze może symulować input za pomocą KeyCode.Space.
    /// </summary>
    public class MicrophoneInputProvider : MonoBehaviour
    {
        [SerializeField] private int sampleWindow = 1024;
        [SerializeField] private int recordSeconds = 15;     // pętla bufora — z niej bierzemy ostatnią wypowiedź
        [SerializeField] private int requestRate = 16000;    // Wit.ai lubi 16 kHz; realny rate = micClip.frequency
#if UNITY_EDITOR
        [SerializeField] private bool simulateInEditor = true;
        [SerializeField] private float simulatedVolume = 0.2f;
#endif

        private string deviceName;
        private AudioClip micClip;
        private bool initialized;

        public bool IsActive { get; private set; }
        public float CurrentVolume { get; private set; }
        public int ClipRate => micClip != null ? micClip.frequency : requestRate;

        void Start()
        {
            TryInitialize();
        }

        private void TryInitialize()
        {
            if (Microphone.devices.Length == 0)
            {
                Debug.LogWarning("[Microphone] No devices found. Will use simulated input in Editor.");
                return;
            }
            deviceName = Microphone.devices[0];
            micClip = Microphone.Start(deviceName, true, recordSeconds, requestRate);
            initialized = true;
            IsActive = true;
            Debug.Log($"[Microphone] Started: {deviceName} @ {(micClip != null ? micClip.frequency : 0)}Hz, {recordSeconds}s buffer");
        }

        void Update()
        {
            if (!initialized)
            {
#if UNITY_EDITOR
                if (simulateInEditor)
                {
                    CurrentVolume = Input.GetKey(KeyCode.Space) ? simulatedVolume : 0f;
                }
#endif
                return;
            }

            CurrentVolume = ComputeRMS();
        }

        private float ComputeRMS()
        {
            if (micClip == null) return 0f;
            int micPos = Microphone.GetPosition(deviceName) - sampleWindow + 1;
            if (micPos < 0) return 0f;

            float[] samples = new float[sampleWindow];
            micClip.GetData(samples, micPos);

            float sumSq = 0f;
            for (int i = 0; i < samples.Length; i++) sumSq += samples[i] * samples[i];
            return Mathf.Sqrt(sumSq / samples.Length);
        }

        /// <summary>Ostatnie `seconds` sekund nagrania (z bufora pętli), do wysłania do Wit.ai. rate = realny ClipRate.</summary>
        public bool TryGetRecentSamples(float seconds, out float[] samples, out int rate)
        {
            samples = null; rate = ClipRate;
            if (micClip == null) return false;
            int total = micClip.samples;
            if (total <= 0) return false;
            int want = Mathf.Clamp(Mathf.RoundToInt(seconds * rate), 1, total);
            int pos = Microphone.GetPosition(deviceName);
            var all = new float[total];
            micClip.GetData(all, 0);
            samples = new float[want];
            int start = pos - want;
            for (int i = 0; i < want; i++)
            {
                int idx = start + i;
                if (idx < 0) idx += total;
                samples[i] = all[idx % total];
            }
            return true;
        }

        void OnDestroy()
        {
            if (initialized && !string.IsNullOrEmpty(deviceName))
            {
                Microphone.End(deviceName);
            }
        }
    }
}
