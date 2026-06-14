using UnityEngine;
using MRCrisisTrainer.Acts.Act3;

namespace MRCrisisTrainer.Gameplay.Detectors
{
    /// <summary>
    /// Mikrokrok mowy: gracz musi mówić (głośność mikrofonu > próg) przez min. czas.
    /// Używane do komunikatu do 112 (lokalizacja, opis, liczba poszkodowanych...).
    /// Opcjonalnie odtwarza linię operatora po ukończeniu.
    /// </summary>
    public class VoiceSpeakDetector : MicrostepDetector
    {
        [SerializeField] private MicrophoneInputProvider mic;
        [SerializeField] private float volumeThreshold = 0.02f;   // normalna mowa to RMS ~0.02-0.04; 0.06 było za wysoko
        [SerializeField] private float requiredSpeechSeconds = 1.5f;
        [SerializeField] private string operatorVoiceClip; // nazwa klipu z Resources/Audio/Voice
        [SerializeField] private float decayRate = 0.4f;

        [SerializeField] private float maxSeconds = 18f;   // auto-zalicz po tylu s — gra NIGDY nie utknie na formułce (mowa bywa zawodna w grze) → flow dochodzi do intruza
        private float speechAccum;
        private float elapsed;

        protected override void OnBegin() { speechAccum = 0f; elapsed = 0f; }

        void Update()
        {
            if (!IsActive) return;
            elapsed += Time.deltaTime;
            if (mic != null && mic.CurrentVolume > volumeThreshold)
            {
                speechAccum += Time.deltaTime;
                if (speechAccum >= requiredSpeechSeconds) { Done(); return; }
            }
            else
            {
                speechAccum = Mathf.Max(0, speechAccum - Time.deltaTime * decayRate);
            }
            if (elapsed >= maxSeconds) Done();   // auto-przejście — nawet gdy mikrofon nie łapie, gra idzie dalej do intruza
        }

        private void Done()
        {
            if (!string.IsNullOrEmpty(operatorVoiceClip) && Core.AudioManager.Instance != null)
                Core.AudioManager.Instance.QueueVoice(operatorVoiceClip);
            Complete();
        }
    }
}
