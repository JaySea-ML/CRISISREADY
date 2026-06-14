using UnityEngine;

namespace MRCrisisTrainer.Acts.Act3
{
    [RequireComponent(typeof(AudioSource))]
    public class PhoneRingController : MonoBehaviour
    {
        [SerializeField] private AudioClip ringClip;
        [SerializeField] private float volume = 0.7f;
        [SerializeField] private Light indicatorLight;

        private AudioSource audioSource;

        // Leniwa inicjalizacja: StartRinging/StopRinging bywają wołane ZANIM Awake się wykona
        // (obiekt telefonu jest nieaktywny na starcie, a SessionFlowManager wyłącza akt → OnDisable
        // dyrektora woła StopRinging). [RequireComponent] gwarantuje, że AudioSource istnieje nawet
        // na nieaktywnym obiekcie, więc GetComponent zawsze zwróci komponent (zero NRE).
        private AudioSource Source
        {
            get
            {
                if (audioSource == null) audioSource = GetComponent<AudioSource>();
                return audioSource;
            }
        }

        void Awake()
        {
            var src = Source;
            src.spatialBlend = 1f; // full 3D
            src.loop = true;
            src.volume = volume;
            src.playOnAwake = false;
            src.minDistance = 0.5f;
            src.maxDistance = 8f;
            if (ringClip != null) src.clip = ringClip;
            if (indicatorLight != null) indicatorLight.enabled = false;
        }

        public void StartRinging()
        {
            var src = Source;
            if (src != null && src.clip != null) src.Play();
            if (indicatorLight != null) indicatorLight.enabled = true;
        }

        public void StopRinging()
        {
            var src = Source;
            if (src != null) src.Stop();
            if (indicatorLight != null) indicatorLight.enabled = false;
        }

        void Update()
        {
            if (indicatorLight != null && indicatorLight.enabled)
            {
                indicatorLight.intensity = 0.6f + 0.4f * Mathf.Sin(Time.time * 6f);
            }
        }
    }
}
