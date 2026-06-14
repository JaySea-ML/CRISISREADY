using UnityEngine;
using MRCrisisTrainer.Config;

namespace MRCrisisTrainer.Acts.Act1
{
    [RequireComponent(typeof(AudioSource))]
    public class MetronomeController : MonoBehaviour
    {
        [SerializeField] private BLSConfig config;
        [SerializeField] private AudioConfig audioConfig;
        [SerializeField] private bool playOnStart = false;

        private AudioSource audioSource;
        private double nextTickDsp;
        private bool ticking;

        void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioConfig != null)
            {
                audioSource.clip = audioConfig.metronomeTick;
                audioSource.volume = audioConfig.metronomeVolume;
            }
            audioSource.playOnAwake = false;
        }

        void Start()
        {
            if (playOnStart) StartTicking();
        }

        public void StartTicking()
        {
            if (audioSource.clip == null || config == null)
            {
                Debug.LogWarning($"[{nameof(MetronomeController)}] Missing audio clip or config.");
                return;
            }
            ticking = true;
            nextTickDsp = AudioSettings.dspTime + 0.1f;
        }

        public void StopTicking()
        {
            ticking = false;
        }

        void Update()
        {
            if (!ticking || audioSource.clip == null) return;
            double interval = 60.0 / config.targetBPM;
            double now = AudioSettings.dspTime;
            if (now + 0.1f >= nextTickDsp)
            {
                audioSource.PlayScheduled(nextTickDsp);
                nextTickDsp += interval;
            }
        }
    }
}
