using System;
using MRCrisisTrainer.Config;

namespace MRCrisisTrainer.Acts.Act3
{
    /// <summary>
    /// Detektor 'czy gracz wykonał komunikat do 112' - na bazie głośności i czasu trwania.
    /// Nie próbujemy rozpoznawać konkretnych słów (to byłby OS-level STT).
    /// Czysta logika - testable.
    /// </summary>
    public class SpeechCueDetector
    {
        private readonly Act3Config config;
        private float speechAccumulated;
        private bool detected;
        private float startTime = -1f;

        public bool HasDetectedSpeech => detected;
        public float SpeechElapsed => speechAccumulated;
        public event Action OnSpeechDetected;
        public event Action OnTimeout;

        public SpeechCueDetector(Act3Config config)
        {
            this.config = config;
        }

        public void Tick(float volume, float dt, float currentTime)
        {
            if (detected) return;
            if (startTime < 0) startTime = currentTime;

            if (volume > config.speechVolumeThreshold)
            {
                speechAccumulated += dt;
                if (speechAccumulated >= config.minSpeechDuration)
                {
                    detected = true;
                    OnSpeechDetected?.Invoke();
                    return;
                }
            }
            else
            {
                // Decay slowly - allow short pauses
                speechAccumulated = Math.Max(0, speechAccumulated - dt * 0.3f);
            }

            if (currentTime - startTime >= config.maxTimeForDispatch)
            {
                OnTimeout?.Invoke();
            }
        }

        public void Reset()
        {
            speechAccumulated = 0f;
            detected = false;
            startTime = -1f;
        }
    }
}
