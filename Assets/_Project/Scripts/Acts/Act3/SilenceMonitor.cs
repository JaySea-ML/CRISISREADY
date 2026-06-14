using System;
using MRCrisisTrainer.Config;

namespace MRCrisisTrainer.Acts.Act3
{
    public enum SilenceOutcome
    {
        InProgress,
        Success,
        Failed
    }

    /// <summary>
    /// Mierzy postęp 30-sekundowej ciszy. Każde przekroczenie progu głośności = strike.
    /// Po N strike-ach lub timeout → Failed. Po wymaganym czasie ciszy → Success.
    /// Logika czysta - bez Unity dependencies, testable.
    /// </summary>
    public class SilenceMonitor
    {
        private readonly Act3Config config;
        private float silenceElapsed;
        private float lastNoiseTime;
        private int noiseStrikes;
        private bool finished;
        private SilenceOutcome outcome = SilenceOutcome.InProgress;

        public float SilenceElapsed => silenceElapsed;
        public float NormalizedProgress => Math.Min(1f, silenceElapsed / Math.Max(0.01f, config.silenceRequiredDuration));
        public int NoiseStrikes => noiseStrikes;
        public SilenceOutcome Outcome => outcome;
        public event Action<int> OnNoiseStrike;
        public event Action OnSilenceComplete;
        public event Action OnFailed;

        public SilenceMonitor(Act3Config config)
        {
            this.config = config;
            lastNoiseTime = -10f;
        }

        public void Tick(float volume, float dt, float currentTime)
        {
            if (finished) return;

            bool aboveThreshold = volume > config.silenceVolumeThreshold;

            if (aboveThreshold)
            {
                // Debounce strikes: only count once per 2s
                if (currentTime - lastNoiseTime > 2f)
                {
                    noiseStrikes++;
                    lastNoiseTime = currentTime;
                    OnNoiseStrike?.Invoke(noiseStrikes);

                    if (noiseStrikes > config.allowedNoiseStrikes)
                    {
                        outcome = SilenceOutcome.Failed;
                        finished = true;
                        OnFailed?.Invoke();
                        return;
                    }
                }
                // strike resets a small portion of progress
                silenceElapsed = Math.Max(0, silenceElapsed - 1.0f);
            }
            else
            {
                silenceElapsed += dt;
            }

            if (silenceElapsed >= config.silenceRequiredDuration)
            {
                outcome = SilenceOutcome.Success;
                finished = true;
                OnSilenceComplete?.Invoke();
            }
        }

        public void Reset()
        {
            silenceElapsed = 0f;
            noiseStrikes = 0;
            lastNoiseTime = -10f;
            finished = false;
            outcome = SilenceOutcome.InProgress;
        }
    }
}
