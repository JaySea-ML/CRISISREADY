using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MRCrisisTrainer.Config;
using MRCrisisTrainer.Core;
using MRCrisisTrainer.Logging;

namespace MRCrisisTrainer.Acts.Act3
{
    public enum Act3State
    {
        Idle,
        Intro,
        PhoneRinging,
        ReceiverPickedUp,
        TransitioningToVR,
        Dialogue112,
        SilencePhase,
        PoliceArrival,
        Complete,
        Failed
    }

    public class Act3Controller : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private Act3Config config;

        [Header("Refs")]
        [SerializeField] private PhoneRingController phone;
        [SerializeField] private PhoneReceiverGrabber receiverGrabber;
        [SerializeField] private VRModeTransitioner transitioner;
        [SerializeField] private SpeechDetectorBehaviour speechDetector;
        [SerializeField] private SilenceMonitorBehaviour silenceMonitor;
        [SerializeField] private ThiefWanderAI thief;
        [SerializeField] private PoliceArrivalSequence policeSequence;
        [SerializeField] private Act3FeedbackUI ui;
        [SerializeField] private GameObject closetEnvironment;
        [SerializeField] private GameObject preVRPhoneRoom;

        public Act3State CurrentState { get; private set; } = Act3State.Idle;
        private float silenceStartTime;

#if UNITY_EDITOR
        void OnValidate()
        {
            if (config == null)
            {
                config = UnityEditor.AssetDatabase.LoadAssetAtPath<Act3Config>("Assets/_Project/ScriptableObjects/Act3Config.asset");
            }
        }
#endif

        void Start()
        {
            if (config == null)
            {
                Debug.LogError("[Act3Controller] Act3Config not assigned. Aborting.");
                return;
            }

            if (receiverGrabber != null) receiverGrabber.OnReceiverGrabbed += OnReceiverPickedUp;
            if (speechDetector != null)
            {
                speechDetector.OnSpeechDetected += OnSpeechDetected;
                speechDetector.OnTimeout += OnSpeechTimeout;
            }
            if (silenceMonitor != null)
            {
                silenceMonitor.OnNoiseStrike += OnNoiseStrike;
                silenceMonitor.OnSilenceComplete += OnSilenceSurvived;
                silenceMonitor.OnFailed += OnSilenceFailed;
            }
            if (policeSequence != null) policeSequence.OnSequenceComplete += OnPoliceSequenceDone;

            if (closetEnvironment != null) closetEnvironment.SetActive(false);

            JSONLLogger.Instance?.LogEvent("act3_start", null);
            StartCoroutine(IntroSequence());
        }

        void OnDestroy()
        {
            if (receiverGrabber != null) receiverGrabber.OnReceiverGrabbed -= OnReceiverPickedUp;
            if (speechDetector != null)
            {
                speechDetector.OnSpeechDetected -= OnSpeechDetected;
                speechDetector.OnTimeout -= OnSpeechTimeout;
            }
            if (silenceMonitor != null)
            {
                silenceMonitor.OnNoiseStrike -= OnNoiseStrike;
                silenceMonitor.OnSilenceComplete -= OnSilenceSurvived;
                silenceMonitor.OnFailed -= OnSilenceFailed;
            }
            if (policeSequence != null) policeSequence.OnSequenceComplete -= OnPoliceSequenceDone;
        }

        private IEnumerator IntroSequence()
        {
            TransitionTo(Act3State.Intro);
            ui?.SetPrompt("...");
            ui?.HideSilenceUI();
            yield return new WaitForSeconds(config.secondsBeforePhoneRing);

            TransitionTo(Act3State.PhoneRinging);
            phone?.StartRinging();
            ui?.SetPrompt("Telefon dzwoni - odbierz słuchawkę");
        }

        private void OnReceiverPickedUp()
        {
            if (CurrentState != Act3State.PhoneRinging) return;
            TransitionTo(Act3State.ReceiverPickedUp);
            phone?.StopRinging();
            ui?.SetPrompt("Operator 112 odbiera...");
            JSONLLogger.Instance?.LogEvent("act3_receiver_grabbed", null);
            StartCoroutine(VRTransitionSequence());
        }

        private IEnumerator VRTransitionSequence()
        {
            TransitionTo(Act3State.TransitioningToVR);
            if (transitioner != null) yield return transitioner.TransitionToFullVR();
            else yield return new WaitForSeconds(config.vrTransitionDuration);

            if (closetEnvironment != null) closetEnvironment.SetActive(true);
            if (preVRPhoneRoom != null) preVRPhoneRoom.SetActive(false);

            TransitionTo(Act3State.Dialogue112);
            ui?.SetPrompt("Powiedz spokojnie: 'Halo 112, włamanie do mieszkania'");
            speechDetector?.BeginListening();
        }

        private void OnSpeechDetected()
        {
            if (CurrentState != Act3State.Dialogue112) return;
            JSONLLogger.Instance?.LogEvent("act3_dispatch_message_sent", null);
            speechDetector?.StopListening();
            StartCoroutine(EnterSilencePhase());
        }

        private void OnSpeechTimeout()
        {
            if (CurrentState != Act3State.Dialogue112) return;
            JSONLLogger.Instance?.LogEvent("act3_dispatch_timeout", null);
            ui?.SetPrompt("Operator się rozłączył. Mimo wszystko - cisza");
            StartCoroutine(EnterSilencePhase());
        }

        private IEnumerator EnterSilencePhase()
        {
            yield return new WaitForSeconds(1.5f);
            TransitionTo(Act3State.SilencePhase);
            ui?.ShowSilenceUI();
            ui?.SetPrompt("CISZA - włamywacz nie może Cię usłyszeć");
            silenceStartTime = Time.time;
            silenceMonitor?.BeginSilence();
        }

        void Update()
        {
            if (CurrentState != Act3State.SilencePhase || silenceMonitor == null) return;
            float progress = silenceMonitor.NormalizedProgress;
            float remaining = Mathf.Max(0, config.silenceRequiredDuration - (Time.time - silenceStartTime));
            ui?.SetSilenceProgress(progress, remaining);
            ui?.SetStrikes(silenceMonitor.NoiseStrikes, config.allowedNoiseStrikes);
        }

        private void OnNoiseStrike(int strikes)
        {
            JSONLLogger.Instance?.LogEvent("act3_noise_strike", new Dictionary<string, object>
            {
                { "strike_n", strikes }
            });
            thief?.OnNoiseDetected();
        }

        private void OnSilenceSurvived()
        {
            JSONLLogger.Instance?.LogEvent("act3_silence_complete", null);
            ui?.SetPrompt("Słychać syreny w oddali...");
            TransitionTo(Act3State.PoliceArrival);
            policeSequence?.StartSequence();
        }

        private void OnSilenceFailed()
        {
            JSONLLogger.Instance?.LogEvent("act3_silence_failed", null);
            ui?.SetPrompt("Włamywacz Cię usłyszał!");
            TransitionTo(Act3State.Failed);
            StartCoroutine(FailedRetry());
        }

        private IEnumerator FailedRetry()
        {
            yield return new WaitForSeconds(4f);
            ui?.SetPrompt("Próbuj jeszcze raz - bądź cicho");
            silenceStartTime = Time.time;
            TransitionTo(Act3State.SilencePhase);
            silenceMonitor?.BeginSilence();
        }

        private void OnPoliceSequenceDone()
        {
            TransitionTo(Act3State.Complete);
            ui?.SetPrompt("Bezpieczne. Koniec scenariusza.");
            ui?.HideSilenceUI();
            JSONLLogger.Instance?.LogEvent("act3_complete", null);
            GameStateManager.Instance?.SetCurrentAct(ActId.Finished);
        }

        private void TransitionTo(Act3State next)
        {
            Debug.Log($"[Act3] {CurrentState} -> {next}");
            CurrentState = next;
        }
    }
}
