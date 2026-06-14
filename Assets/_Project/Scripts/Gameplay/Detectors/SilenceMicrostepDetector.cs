using System;
using System.Collections.Generic;
using UnityEngine;
using MRCrisisTrainer.Acts.Act3;
using MRCrisisTrainer.Logging;

namespace MRCrisisTrainer.Gameplay.Detectors
{
    /// <summary>
    /// Mikrokrok ciszy: gracz musi pozostać cicho (głośność < próg) przez wymagany czas.
    /// Każde przekroczenie progu = strike. Po N strike-ach krok = porażka.
    /// </summary>
    public class SilenceMicrostepDetector : MicrostepDetector
    {
        [SerializeField] private MicrophoneInputProvider mic;
        [SerializeField] private float requiredSilenceSeconds = 120f;
        [SerializeField] private float volumeThreshold = 0.03f;   // łap normalne mówienie (RMS ~0.03), nie tylko podniesiony głos
        [SerializeField] private int allowedStrikes = 2;
        [SerializeField] private float progressLogInterval = 10f;

        public event Action<float, float> OnProgress; // elapsed, required
        public event Action<int> OnStrike;

        private float silenceElapsed;
        private int strikes;
        private float lastStrikeTime;
        private float lastProgressLogTime;

        protected override void OnBegin()
        {
            silenceElapsed = 0f; strikes = 0; lastStrikeTime = -10f; lastProgressLogTime = Time.time;
            LogSilence("start");
        }

        void Update()
        {
            if (!IsActive || mic == null) return;
            if (mic.CurrentVolume > volumeThreshold)
            {
                if (Time.time - lastStrikeTime > 2f)
                {
                    strikes++;
                    lastStrikeTime = Time.time;
                    OnStrike?.Invoke(strikes);
                    LogSilence("strike");
                    if (strikes >= allowedStrikes)
                    {
                        LogSilence("failed");
                        Fail("too_loud");
                        return;
                    }
                }
                silenceElapsed = Mathf.Max(0, silenceElapsed - 1f);
            }
            else
            {
                silenceElapsed += Time.deltaTime;
            }
            OnProgress?.Invoke(silenceElapsed, requiredSilenceSeconds);
            if (Time.time - lastProgressLogTime >= progressLogInterval)
            {
                lastProgressLogTime = Time.time;
                LogSilence("progress");
            }
            if (silenceElapsed >= requiredSilenceSeconds)
            {
                LogSilence("complete");
                Complete();
            }
        }

        private void LogSilence(string phase)
        {
            JSONLLogger.Instance?.LogEvent("silence", new Dictionary<string, object>
            {
                { "phase", phase },
                { "elapsed_s", silenceElapsed },
                { "required_s", requiredSilenceSeconds },
                { "strikes", strikes },
                { "volume", mic != null ? mic.CurrentVolume : 0f }
            });
        }
    }
}
